using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>How far a mine is built: marked, the entrance dug, the materials in, and the frame and rails fitted.</summary>
public enum MineStage { Marked, Dug, Supplied, Open }

/// <summary>Mines: marked free at an ore cliff, built in three stages, then worked underground for ore and rocks that never run out.</summary>
public sealed partial class World {
    /// <summary>How far from a cliff a mine plan still snaps to it.</summary>
    private const float MineReach = 10;
    // Mines already reported full with nowhere to take the output, so the message shows once per stoppage.
    private readonly HashSet<int> mineFullReported = new();

    public static Cliff CliffAt(Vector2 p) {
        var nearest = Cliffs[0];
        foreach (var c in Cliffs) if (c.DistanceTo(p) < nearest.DistanceTo(p)) nearest = c;
        return nearest;
    }
    /// <summary>How far out from its cliff's spine a mine's spot stands.</summary>
    public const float MineOut = 4.2f;
    /// <summary>The cart's spot, outward from the cliff center, with the portal projecting from the mountain foot.</summary>
    public static Vector2 MineSpot(Cliff c) => c.Center + c.Face * MineOut;
    /// <summary>Whether a point lies within the solid rock skin; the tunnel approach at depth -1.4 stays open.</summary>
    private static bool InSpur(Vector2 p, float margin = 0) {
        foreach (var c in Cliffs) if (InOutcrop(p - MineSpot(c), c.Face, margin)) return true;
        // A den is the same outcrop at a smaller size.
        foreach (var den in Dens) if (InOutcrop((p - den.Spot) / Den.Scale, den.Face, margin / Den.Scale)) return true;
        return false;
    }
    /// <summary>Whether a point <paramref name="offset"/> from a mine's spot lies within its outcrop, which faces <paramref name="face"/>.</summary>
    private static bool InOutcrop(Vector2 offset, Vector2 face, float margin) {
        float depth = offset.Dot(face);
        // The margin reaches twice as far out in front, so scattered trees and rocks also stay off the rails and cart.
        if (depth > -2.28f + margin * 2) return false;
        float across = Math.Abs(offset.Dot(new Vector2(-face.Y, face.X)));
        float width = depth >= -4.2f ? 2.55f + (depth + 2.28f) * (2.8f - 2.55f) / (-4.2f + 2.28f)
            : depth >= -6 ? 2.8f + (depth + 4.2f) * (3.35f - 2.8f) / (-6 + 4.2f)
            : depth >= -32 ? 3.35f + (depth + 6) * (4.6f - 3.35f) / (-32 + 6)
            : 4.6f;
        return across < width + margin;
    }

    /// <summary>Where diggers and miners stand: at the entrance, against the rock.</summary>
    public static Vector2 MineDoor(Thing mine) => mine.Pos - CliffAt(mine.Pos).Face * 1.4f;
    /// <summary>Where a plan placed at <paramref name="p"/> goes: a mine near a cliff snaps to that cliff's spot.</summary>
    private static bool NearMineSite(Cliff cliff, Vector2 p) => cliff.DistanceTo(p) <= MineReach || p.DistanceTo(MineSpot(cliff)) <= 2.5f;
    private static Vector2 SnapMine(Vector2 p) => CliffAt(p) is var c && NearMineSite(c, p) ? MineSpot(c) : p;
    public static string MineOre(Thing mine) => ResourceItem(CliffAt(mine.Pos).Ore);
    public static string MineName(Thing mine) => CliffAt(mine.Pos).Ore switch { ThingKind.Iron => "iron mine", ThingKind.Coal => "coal mine", ThingKind.Tin => "tin mine", var ore => $"{ore.Id()} mine" };
    public float MineStageSeconds => Data.Mine.StageWorkdays * Data.WorkdaySeconds;
    /// <summary>Materials the site still needs before its frame can go in.</summary>
    public Dictionary<string, int> MineNeeds(Thing mine) => mine.MineStage != MineStage.Dug ? new() :
        Data.Mine.Materials.Where(p => mine.Inventory[p.Key] < p.Value).ToDictionary(p => p.Key, p => p.Value - mine.Inventory[p.Key]);
    /// <summary>How much more of one material the site needs, counted without building <see cref="MineNeeds"/>.</summary>
    public int MineNeed(Thing mine, string item) => mine.MineStage == MineStage.Dug && Data.Mine.Materials.TryGetValue(item, out int wanted) ? Math.Max(0, wanted - mine.Inventory[item]) : 0;
    // The waiting status names what the site lacks; it is rebuilt only when that changes.
    private readonly Dictionary<int, (int Key, string Text)> mineWants = new();
    private string MineWants(Thing mine) {
        int key = 17;
        foreach (var pair in Data.Mine.Materials) key = key * 31 + MineNeed(mine, pair.Key);
        if (!mineWants.TryGetValue(mine.Id, out var cached) || cached.Key != key) mineWants[mine.Id] = cached = (key, $"Needs {Goods(MineNeeds(mine))} in output-enabled storage");
        return cached.Text;
    }

    /// <summary>Mining stops as soon as either the ore or the rock slot holds a full stack.</summary>
    public bool MineFull(Thing mine) => mine.Output is Inventory output && (output[MineOre(mine)] >= output.StackSize || output["stone"] >= output.StackSize);
    private string? MineBlock(Vector2 p) {
        var cliff = CliffAt(p);
        if (!NearMineSite(cliff, p)) return "Mark a mine at the foot of an ore cliff.";
        var spot = MineSpot(cliff);
        foreach (var t in S.Things.Of(ThingKind.Mine)) if (t.Pos.DistanceTo(spot) < .1f) return "This cliff already has a mine.";
        return Player.DistanceTo(spot) > 8 ? "Walk closer to the cliff." : null;
    }

    /// <summary>Hands the site up to <paramref name="quantity"/> of one material from the backpack, no more than it still needs.</summary>
    public int SupplyMine(Thing mine, string item, int quantity) {
        if (!Reaches(mine)) { Say("Walk closer to the mine."); return 0; }
        if (MineNeed(mine, item) == 0) { Say(MineNeeds(mine) is { Count: > 0 } needs ? $"The mine still needs {Goods(needs)}." : "The mine needs nothing now."); return 0; }
        if (Forwarded(Verb.SupplyMine, mine.Id, item: item, count: quantity)) return 0;
        return S.Backpack.TransferTo(mine.Inventory, item, Math.Min(quantity, MineNeed(mine, item)));
    }

    /// <summary>Why the player cannot work on a mine now, or null: a pickaxe digs the entrance and a hammer fits the frame and rails.</summary>
    private string? MineBuildBlock(Thing mine) => mine.MineStage switch {
        MineStage.Marked => BestTool("pickaxe") == null ? "A pickaxe is required to dig the entrance." : null,
        MineStage.Dug => "Deliver the timber, iron and stone first.",
        MineStage.Supplied => BestTool("hammer") == null ? "A hammer is required to fit the frame and rails." : null,
        _ => "The mine is open. Workers with pickaxes mine it."
    };
    /// <summary>Adds the player's digging or fitting to the stage's one shared progress, which workers add to as well.</summary>
    private void BuildMine(Thing mine, float dt) {
        mine.Progress += dt;
        if (mine.Progress >= MineStageSeconds) FinishMineStage(mine);
    }
    private void FinishMineStage(Thing mine) {
        bool dug = mine.MineStage == MineStage.Marked;
        mine.MineStage++; mine.Progress = 0; mine.Rounds++;
        Say(dug ? $"The {MineName(mine)} entrance is dug. Deliver {Goods(Data.Mine.Materials)}." : $"The {MineName(mine)} is open. Up to {Data.Mine.Workers} workers with pickaxes can mine it.");
    }

    private void MineTick(Thing mine, float dt) {
        if (mine.MineStage == MineStage.Dug && mine.Inventory.Spend(Data.Mine.Materials)) {
            mine.MineStage = MineStage.Supplied; mine.Progress = 0; Say($"The {MineName(mine)} has its timber, iron and stone. You or a worker with a hammer can fit the frame and rails.");
        }
        if (mine.MineStage != MineStage.Open || MineFull(mine)) return;
        int miners = 0;
        foreach (var w in S.Workers) if (w.Assignment == mine.Id && w.WorkState == WorkerWorkState.Underground) miners++;
        mine.Progress += dt * miners;
        while (mine.Progress >= Data.Mine.Seconds && !MineFull(mine)) {
            string ore = MineOre(mine);
            mine.Progress -= Data.Mine.Seconds; mine.Output!.Add(ore, 1); mine.Output.Add("stone", 1); mine.Rounds++; Got(ore); Got("stone");
        }
    }

    /// <summary>A builder digs a marked mine's entrance with a pickaxe and fits a supplied one's frame with a hammer; each finished stage is a round.</summary>
    private bool BuildSite(Worker w, Thing mine, float dt) {
        if (mine.MineStage is not (MineStage.Marked or MineStage.Supplied)) { w.Status = mine.MineStage == MineStage.Dug ? MineWants(mine) : "The mine is open"; return false; }
        bool digging = mine.MineStage == MineStage.Marked;
        switch (Equip(w, digging ? "pickaxe" : "hammer", mine, dt)) {
            case Kit.Busy: return true;
            case Kit.Lacking: return false;
        }
        w.Status = digging ? "Digging the mine entrance" : "Fitting the mine frame and rails";
        if (!Walk(w, MineDoor(mine), dt, .5f)) return true;
        w.WorkState = digging ? WorkerWorkState.Digging : WorkerWorkState.Hammering;
        BuildMine(mine, dt);
        return true;
    }

    /// <summary>
    /// A miner works an open mine underground with a pickaxe; each piece mined is a round.
    /// Once the mine's output is full, a miner brings a load of it out to storage.
    /// </summary>
    private bool Dig(Worker w, Thing mine, float dt) {
        if (mine.MineStage != MineStage.Open) { w.Status = mine.MineStage == MineStage.Marked ? "Waiting for the entrance to be dug" : "Waiting for the frame to be fitted"; return false; }
        string ore = MineOre(mine);
        // A full mine with nowhere to unload has nothing to do, so no pickaxe is fetched for it.
        string full = mine.Output![ore] >= mine.Output.StackSize ? ore : "stone";
        var dest = MineFull(mine) ? DepositFor(full, mine.Pos, mine.Id) : null;
        if (MineFull(mine) && dest == null) {
            w.Status = Phrase("Mine full · no storage takes ", Data.Name(full));
            if (mineFullReported.Add(mine.Id)) Say($"The {MineName(mine)} is full with no output available.");
            return false;
        }
        switch (Equip(w, "pickaxe", mine, dt)) {
            case Kit.Busy: return true;
            case Kit.Lacking: return false;
        }
        if (dest != null) {
            w.Status = Phrase("Bringing out ", Data.Name(full));
            if (Walk(w, MineDoor(mine), dt, .5f)) { mine.Output.TransferTo(w.Bag, full, Worker.BagStack); w.Destination = dest.Id; }
            return true;
        }
        mineFullReported.Remove(mine.Id);
        w.Status = Phrase("Mining ", Data.Name(ore), " underground");
        if (Walk(w, MineDoor(mine), dt, .5f)) w.WorkState = WorkerWorkState.Underground;
        return true;
    }
}
