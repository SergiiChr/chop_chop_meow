using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Mines: marked free at an ore cliff, built in three stages, then worked underground for ore and rocks that never run out.</summary>
public sealed partial class World {
    /// <summary>Values of <see cref="Thing.Stage"/>: the entrance is dug, the materials are in, and the frame and rails are fitted.</summary>
    public const int MineDug = 1, MineSupplied = 2, MineOpen = 3;
    /// <summary>How far from a cliff a mine plan still snaps to it.</summary>
    private const float MineReach = 10;
    // Mines already reported full with nowhere to take the output, so the message shows once per stoppage.
    private readonly HashSet<int> mineFullReported = new();

    public static Cliff CliffAt(Vector2 p) {
        var nearest = Cliffs[0];
        foreach (var c in Cliffs) if (c.DistanceTo(p) < nearest.DistanceTo(p)) nearest = c;
        return nearest;
    }
    /// <summary>The outward normal of this cliff: each portal follows its own rock face, with rails toward the valley.</summary>
    public static Vector2 MineOut(Cliff c) => c.Face;
    /// <summary>The cart's spot, outward from the cliff center, with the portal projecting from the mountain foot.</summary>
    public static Vector2 MineSpot(Cliff c) => c.Center + MineOut(c) * 4.2f;
    /// <summary>The earlier end-facing cart position, retained for loading old settlements.</summary>
    private static Vector2 LegacyMineSpot(Cliff c) {
        var along = c.Along * Math.Sign(c.Along.Dot(Terrain.Seaward));
        var end = c.Center + along * (c.HalfLength + 6.7f);
        return end + c.Face * Math.Max(2.5f, 6 - Terrain.FromMountains(end));
    }
    /// <summary>Whether a point lies within the solid rock skin; the tunnel approach at depth -1.4 stays open.</summary>
    private static bool InSpur(Vector2 p, float margin = 0) {
        foreach (var c in Cliffs) {
            var offset = p - MineSpot(c);
            float depth = offset.Dot(MineOut(c));
            // The margin reaches twice as far out in front, so scattered trees and rocks also stay off the rails and cart.
            if (depth > -2.28f + margin * 2) continue;
            float across = Math.Abs(offset.Dot(c.Along));
            float width = depth >= -4.2f ? 2.55f + (depth + 2.28f) * (2.8f - 2.55f) / (-4.2f + 2.28f)
                : depth >= -6 ? 2.8f + (depth + 4.2f) * (3.35f - 2.8f) / (-6 + 4.2f)
                : depth >= -32 ? 3.35f + (depth + 6) * (4.6f - 3.35f) / (-32 + 6)
                : 4.6f;
            if (across < width + margin) return true;
        }
        return false;
    }

    /// <summary>Old saves keep mine inventories, stages and worker assignments while the cart snaps to the aligned entrance.</summary>
    private static void AlignMines(SaveState state) {
        foreach (var mine in state.Things.Concat(state.Remembered.Values).Where(t => t.Kind == "mine")) {
            var cliff = Cliffs.MinBy(c => mine.Pos.DistanceSquaredTo(LegacyMineSpot(c)));
            mine.Pos = MineSpot(cliff);
        }
        state.Version = 5;
    }
    /// <summary>Where diggers and miners stand: at the entrance, against the rock.</summary>
    public static Vector2 MineDoor(Thing mine) => mine.Pos - MineOut(CliffAt(mine.Pos)) * 1.4f;
    /// <summary>Where a plan placed at <paramref name="p"/> goes: a mine near a cliff snaps to that cliff's spot.</summary>
    private static bool NearMineSite(Cliff cliff, Vector2 p) => cliff.DistanceTo(p) <= MineReach || p.DistanceTo(MineSpot(cliff)) <= 2.5f;
    private static Vector2 SnapMine(Vector2 p) => CliffAt(p) is var c && NearMineSite(c, p) ? MineSpot(c) : p;
    public static string MineOre(Thing mine) => ResourceItem(CliffAt(mine.Pos).Ore);
    public static string MineName(Thing mine) => CliffAt(mine.Pos).Ore switch { "iron" => "iron mine", "coal" => "coal mine", "tin" => "tin mine", var ore => $"{ore} mine" };
    public float MineStageSeconds => Data.Mine.StageWorkdays * Data.WorkdaySeconds;
    /// <summary>Materials the site still needs before its frame can go in.</summary>
    public Dictionary<string, int> MineNeeds(Thing mine) => mine.Stage != MineDug ? new() :
        Data.Mine.Materials.Where(p => mine.Inventory[p.Key] < p.Value).ToDictionary(p => p.Key, p => p.Value - mine.Inventory[p.Key]);
    /// <summary>How much more of one material the site needs, counted without building <see cref="MineNeeds"/>.</summary>
    public int MineNeed(Thing mine, string item) => mine.Stage == MineDug && Data.Mine.Materials.TryGetValue(item, out int wanted) ? Math.Max(0, wanted - mine.Inventory[item]) : 0;
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
    /// <summary>The tool a stage needs each worker to have, or null for the stage where materials are hauled in.</summary>
    public static string? MineTool(int stage) => stage switch { 0 => "pickaxe", MineDug => null, MineSupplied => "hammer", _ => "pickaxe" };

    private string? MineBlock(Vector2 p) {
        var cliff = CliffAt(p);
        if (!NearMineSite(cliff, p)) return "Mark a mine at the foot of an ore cliff.";
        if (S.Things.Any(t => t.Kind == "mine" && t.Pos.DistanceTo(MineSpot(cliff)) < .1f)) return "This cliff already has a mine.";
        return Player.DistanceTo(MineSpot(cliff)) > 8 ? "Walk closer to the cliff." : null;
    }

    /// <summary>Hands the site whatever it still needs from the backpack.</summary>
    public void SupplyMine(Thing mine) {
        if (!Near(mine)) { Say("Walk closer to the mine."); return; }
        var needs = MineNeeds(mine);
        if (needs.Count == 0 || Forwarded("supply_mine", mine.Id)) return;
        if (needs.Sum(p => S.Backpack.TransferTo(mine.Inventory, p.Key, p.Value)) == 0) Say($"The mine still needs {Goods(needs)}.");
    }

    private void MineTick(Thing mine, float dt) {
        if (mine.Stage == MineDug && mine.Inventory.Spend(Data.Mine.Materials)) {
            mine.Stage = MineSupplied; mine.Progress = 0; Say($"The {MineName(mine)} has its timber, iron and stone. A worker with a hammer can fit the frame and rails.");
        }
        if (mine.Stage != MineOpen || MineFull(mine)) return;
        int miners = 0;
        foreach (var w in S.Workers) if (w.Assignment == mine.Id && w.WorkState == WorkerWorkState.Underground) miners++;
        mine.Progress += dt * miners;
        while (mine.Progress >= Data.Mine.Seconds && !MineFull(mine)) {
            mine.Progress -= Data.Mine.Seconds; mine.Output!.Add(MineOre(mine), 1); mine.Output.Add("stone", 1);
        }
    }

    private Worker? FirstFreeHands(List<Worker> crew) {
        foreach (var c in crew) if (c.Bag.Count == 0 && OnDuty(c)) return c;
        throw new InvalidOperationException("A mine crew member with free hands is the one asking.");
    }

    private void MineWork(Worker w, Thing mine, List<Worker> crew, float dt) {
        if (MineTool(mine.Stage) is string tool && w.Tool == "") { FetchTool(w, tool, mine, dt); return; }
        if (mine.Stage == MineDug) {
            foreach (var pair in Data.Mine.Materials) {
                string item = pair.Key; int need = MineNeed(mine, item);
                if (need == 0) continue;
                var source = SourceFor(item, w.Pos);
                if (source == null) continue;
                w.Status = Phrase("Fetching ", Data.Name(item), " for the ", MineName(mine));
                if (Walk(w, source.Pos, dt)) { source.Inventory.TransferTo(w.Bag, item, need, true); w.Destination = mine.Id; }
                return;
            }
            w.Status = MineWants(mine); return;
        }
        if (mine.Stage < MineOpen) {
            bool digging = mine.Stage == 0;
            w.Status = digging ? "Digging the mine entrance" : "Fitting the mine frame and rails";
            if (!Walk(w, MineDoor(mine), dt, .5f)) return;
            w.WorkState = digging ? WorkerWorkState.Digging : WorkerWorkState.Hammering;
            mine.Progress += dt;
            if (mine.Progress < MineStageSeconds) return;
            mine.Stage++; mine.Progress = 0;
            Say(digging ? $"The {MineName(mine)} entrance is dug. Deliver {Goods(Data.Mine.Materials)}." : $"The {MineName(mine)} is open. Up to {Data.Mine.Workers} workers with pickaxes can mine it.");
            return;
        }
        string ore = MineOre(mine);
        if (MineFull(mine)) {
            // The first miner with free hands brings the full stack out to storage; the rest wait underground.
            string full = mine.Output![ore] >= mine.Output.StackSize ? ore : "stone";
            var dest = DepositFor(full, mine.Pos);
            if (dest == null) {
                w.Status = Phrase("Mine full · no storage takes ", Data.Name(full));
                if (mineFullReported.Add(mine.Id)) Say($"The {MineName(mine)} is full with no output available.");
            } else if (FirstFreeHands(crew) == w) {
                w.Status = Phrase("Bringing out ", Data.Name(full));
                if (Walk(w, MineDoor(mine), dt, .5f)) { w.Bag.StackSize = mine.Output.StackSize; mine.Output.TransferTo(w.Bag, full, mine.Output.StackSize); w.Destination = dest.Id; }
                return;
            } else w.Status = "Waiting · the mine output is full";
        } else {
            mineFullReported.Remove(mine.Id);
            w.Status = Phrase("Mining ", Data.Name(ore), " underground");
        }
        if (Walk(w, MineDoor(mine), dt, .5f)) w.WorkState = WorkerWorkState.Underground;
    }
}
