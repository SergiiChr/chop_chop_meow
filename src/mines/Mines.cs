using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Mines in <paramref name="world"/>: marked free at an ore cliff, built in three stages, then worked underground for
/// ore and rocks that never run out.
/// </summary>
public sealed class Mines(World world) {
    internal const string DiggingTool = "pickaxe", FittingTool = "hammer";
    /// <summary>How far from a cliff a mine plan still snaps to it.</summary>
    private const float Reach = 10;
    /// <summary>How far behind a mine's spot its cliff's rock face stands, with the cave mouth in it.</summary>
    private const float RockFace = 2.28f;
    /// <summary>Half the cave mouth's width.</summary>
    public const float MouthHalfWidth = .72f;
    /// <summary>How far beside the cave mouth's edge a pick digging the entrance strikes the rock, and how far out from it a worker stands to dig.</summary>
    private const float BesideMouth = .12f, DigOut = 1;

    // Mines already reported full with nowhere to take the output, so the message shows once per stoppage.
    private readonly HashSet<int> fullReported = new();
    // The waiting status names what the site lacks; it is rebuilt only when that changes.
    private readonly Dictionary<int, (int Key, string Text)> wants = new();
    /// <summary>The materials a dug site takes before its frame goes in, as messages name them.</summary>
    private readonly string materials = world.Goods(world.Data.Mine.Materials);

    /// <summary>
    /// The outcrop's half width at depths behind a mine's spot, from the rock face back into the mountain, narrowing
    /// evenly between them; the rock drawn and the rock that blocks walking both follow it.
    /// </summary>
    internal static readonly (float Depth, float HalfWidth)[] OutcropWidths = { (-RockFace, 2.55f), (-4.2f, 2.8f), (-6, 3.35f), (-32, 4.6f) };

    /// <summary>Whether a point lies within a cliff's outcrop of solid rock; the tunnel approach at depth -1.4 stays open.</summary>
    public static bool InOutcrop(Vector2 p, float margin = 0) {
        foreach (var c in Cliff.All) if (OutcropCovers(p - c.MineSpot, c.Face, margin)) return true;
        return false;
    }
    /// <summary>Whether a point <paramref name="offset"/> from a mine's spot lies within its outcrop, which faces <paramref name="face"/>.</summary>
    internal static bool OutcropCovers(Vector2 offset, Vector2 face, float margin) {
        float depth = offset.Dot(face);
        // The margin reaches twice as far out in front, so scattered trees and rocks also stay off the rails and cart.
        if (depth > -RockFace + margin * 2) return false;
        float across = Math.Abs(offset.Dot(new Vector2(-face.Y, face.X)));
        float width = OutcropWidths[^1].HalfWidth;
        for (int i = 1; i < OutcropWidths.Length; i++) {
            if (depth < OutcropWidths[i].Depth) continue;
            var ((near, nearWidth), (far, farWidth)) = (OutcropWidths[i - 1], OutcropWidths[i]);
            width = nearWidth + (depth - near) * (farWidth - nearWidth) / (far - near);
            break;
        }
        return across < width + margin;
    }

    /// <summary>Where miners and those fitting the frame stand: at the entrance, against the rock.</summary>
    public static Vector2 Door(Thing mine) => mine.Pos - Cliff.Nearest(mine.Pos).Face * 1.4f;
    /// <summary>
    /// The spot on a marked mine's rock face, just beside its cave mouth, that someone digging the entrance from
    /// <paramref name="from"/> strikes, on their side of the mouth.
    /// </summary>
    public static Vector2 DigFace(Thing mine, Vector2 from) {
        var cliff = Cliff.Nearest(mine.Pos);
        var mouth = mine.Pos - cliff.Face * RockFace;
        return mouth + cliff.Along * ((from - mouth).Dot(cliff.Along) < 0 ? -1 : 1) * (MouthHalfWidth + BesideMouth);
    }
    /// <summary>Where someone works on a mine from <paramref name="from"/>: beside the cave mouth while the entrance is dug, at the entrance after.</summary>
    public static Vector2 WorkSpot(Thing mine, Vector2 from) => mine.MineStage == MineStage.Marked ? DigFace(mine, from) : Door(mine);
    /// <summary>What someone working on a mine faces: the rock they dig, or past the entrance into the rock rather than the cart out front.</summary>
    public static Vector2 Facing(Thing mine, Vector2 from) => mine.MineStage == MineStage.Marked ? DigFace(mine, from) : Door(mine) * 2 - mine.Pos;
    /// <summary>The work a mine's stage asks for, as the player's status and refusals name it.</summary>
    internal string Task(Thing mine) => mine.MineStage switch {
        MineStage.Marked => "dig the mine",
        MineStage.Supplied => "fit the mine frame",
        _ => world.Phrase("mine ", Cliff.Nearest(mine.Pos).Ore.Id())
    };
    /// <summary>Where a worker stands to dig a marked mine's entrance: a step out from the rock face they strike.</summary>
    private static Vector2 DigStand(Thing mine, Vector2 from) => DigFace(mine, from) + Cliff.Nearest(mine.Pos).Face * DigOut;
    /// <summary>Whether a plan placed at <paramref name="p"/> is close enough to a cliff to go to its mine spot.</summary>
    private static bool NearSite(Cliff cliff, Vector2 p) => cliff.DistanceTo(p) <= Reach || p.DistanceTo(cliff.MineSpot) <= 2.5f;
    /// <summary>Where a plan placed at <paramref name="p"/> goes: a mine near a cliff snaps to that cliff's spot.</summary>
    internal static Vector2 Snap(Vector2 p) => Cliff.Nearest(p) is var c && NearSite(c, p) ? c.MineSpot : p;
    public static string Ore(Thing mine) => World.ResourceItem(Cliff.Nearest(mine.Pos).Ore);
    public static string Name(Thing mine) => Cliff.Nearest(mine.Pos).Ore switch { ThingKind.Iron => "iron mine", ThingKind.Coal => "coal mine", ThingKind.Tin => "tin mine", var ore => $"{ore.Id()} mine" };

    public float StageSeconds => world.Data.Mine.StageWorkdays * world.Data.WorkdaySeconds;
    /// <summary>Materials the site still needs before its frame can go in.</summary>
    private Dictionary<string, int> Needs(Thing mine) {
        var needs = new Dictionary<string, int>();
        foreach (var item in world.Data.Mine.Materials.Keys) if (Need(mine, item) is > 0 and int need) needs[item] = need;
        return needs;
    }
    /// <summary>How much more of one material the site needs, counted without building <see cref="Needs"/>.</summary>
    public int Need(Thing mine, string item) => mine.MineStage == MineStage.Dug && world.Data.Mine.Materials.TryGetValue(item, out int wanted) ? Math.Max(0, wanted - mine.Inventory[item]) : 0;
    /// <summary>What a dug site waiting for its materials says it lacks.</summary>
    internal string Wants(Thing mine) {
        int key = 17;
        foreach (var item in world.Data.Mine.Materials.Keys) key = key * 31 + Need(mine, item);
        if (!wants.TryGetValue(mine.Id, out var cached) || cached.Key != key) wants[mine.Id] = cached = (key, $"Needs {world.Goods(Needs(mine))} in output-enabled storage");
        return cached.Text;
    }

    /// <summary>Mining stops as soon as either the ore or the rock slot holds a full stack.</summary>
    private static bool Full(Thing mine) => mine.Output is Inventory output && (output[Ore(mine)] >= output.StackSize || output["stone"] >= output.StackSize);
    /// <summary>How many workers are mining a mine underground now.</summary>
    public int Underground(Thing mine) {
        int miners = 0;
        foreach (var w in world.S.Workers) if (w.Assignment == mine.Id && w.WorkState == WorkerWorkState.Underground) miners++;
        return miners;
    }
    /// <summary>Whether an open mine works an ore, as the end goal wants.</summary>
    public bool Working(ThingKind ore) {
        foreach (var t in world.S.Things.Of(ThingKind.Mine)) if (t.MineStage == MineStage.Open && Cliff.Nearest(t.Pos).Ore == ore) return true;
        return false;
    }

    /// <summary>Why a mine cannot be marked at <paramref name="p"/>: no cliff near, one there already or too far to walk.</summary>
    internal string? Block(Vector2 p) {
        var cliff = Cliff.Nearest(p);
        if (!NearSite(cliff, p)) return "Mark a mine at the foot of an ore cliff.";
        foreach (var t in world.S.Things.Of(ThingKind.Mine)) if (t.Pos.DistanceTo(cliff.MineSpot) < .1f) return "This cliff already has a mine.";
        return world.Player.DistanceTo(cliff.MineSpot) > 8 ? "Walk closer to the cliff." : null;
    }

    /// <summary>Hands the site up to <paramref name="quantity"/> of one material from the backpack, no more than it still needs.</summary>
    public int Supply(Thing mine, string item, int quantity) {
        if (!world.Reaches(mine)) { world.Say("Walk closer to the mine."); return 0; }
        if (Need(mine, item) == 0) { world.Say(Needs(mine) is { Count: > 0 } needs ? $"The mine still needs {world.Goods(needs)}." : "The mine needs nothing now."); return 0; }
        if (world.Busy() || world.Forwarded(Verb.SupplyMine, mine.Id, item: item, count: quantity)) return 0;
        return world.S.Backpack.TransferTo(mine.Inventory, item, Math.Min(quantity, Need(mine, item)));
    }

    /// <summary>Why the player cannot work on a mine now, or null: a pickaxe digs the entrance and a hammer fits the frame and rails.</summary>
    internal string? BuildBlock(Thing mine) => mine.MineStage switch {
        MineStage.Marked => world.BestTool(DiggingTool) == null ? "A pickaxe is required to dig the entrance." : null,
        MineStage.Dug => world.Phrase("Deliver ", materials, " first."),
        MineStage.Supplied => world.BestTool(FittingTool) == null ? "A hammer is required to fit the frame and rails." : null,
        _ => "The mine is open. Workers with pickaxes mine it."
    };
    /// <summary>Adds digging or fitting, by the player or a worker, to the stage's one shared progress.</summary>
    internal void Work(Thing mine, float dt) {
        mine.Progress += dt;
        if (mine.Progress < StageSeconds) return;
        bool dug = mine.MineStage == MineStage.Marked;
        mine.MineStage++; mine.Progress = 0; mine.Rounds++;
        world.Say(dug ? $"The {Name(mine)} entrance is dug. Deliver {materials}." : $"The {Name(mine)} is open. Up to {world.Data.Mine.Workers} workers with pickaxes can mine it.");
    }

    /// <summary>A dug site takes its delivered materials at once; an open mine brings out ore and stone at its miners' pace.</summary>
    internal void Tick(Thing mine, float dt) {
        var data = world.Data.Mine;
        if (mine.MineStage == MineStage.Dug && mine.Inventory.Spend(data.Materials)) {
            mine.MineStage = MineStage.Supplied; mine.Progress = 0; world.Say($"The {Name(mine)} has its {materials}. You or a worker with a hammer can fit the frame and rails.");
        }
        if (mine.MineStage != MineStage.Open || Full(mine)) return;
        mine.Progress += dt * Underground(mine);
        string ore = Ore(mine);
        while (mine.Progress >= data.Seconds && !Full(mine)) {
            mine.Progress -= data.Seconds; mine.Output!.Add(ore, 1); mine.Output.Add("stone", 1); mine.Rounds++; world.Got(ore); world.Got("stone");
        }
    }

    /// <summary>A builder digs a marked mine's entrance with a pickaxe and fits a supplied one's frame with a hammer; each finished stage is a round.</summary>
    internal bool Build(Worker w, Thing mine, float dt) {
        if (mine.MineStage is not (MineStage.Marked or MineStage.Supplied)) { w.Status = mine.MineStage == MineStage.Dug ? Wants(mine) : "The mine is open"; return false; }
        bool digging = mine.MineStage == MineStage.Marked;
        switch (world.Equip(w, digging ? DiggingTool : FittingTool, mine, dt)) {
            case World.Kit.Busy: return true;
            case World.Kit.Lacking: return false;
        }
        w.Status = digging ? "Digging the mine entrance" : "Fitting the mine frame and rails";
        if (!world.Walk(w, digging ? DigStand(mine, w.Pos) : Door(mine), dt, digging ? .2f : .5f)) return true;
        w.WorkState = digging ? WorkerWorkState.Digging : WorkerWorkState.Hammering;
        Work(mine, dt);
        return true;
    }

    /// <summary>
    /// A miner works an open mine underground with a pickaxe; each piece mined is a round.
    /// Once the mine's output is full, a miner brings a load of it out to storage.
    /// </summary>
    internal bool Mine(Worker w, Thing mine, float dt) {
        if (mine.MineStage != MineStage.Open) { w.Status = mine.MineStage == MineStage.Marked ? "Waiting for the entrance to be dug" : "Waiting for the frame to be fitted"; return false; }
        string ore = Ore(mine);
        // A full mine with nowhere to unload has nothing to do, so no pickaxe is fetched for it.
        bool full = Full(mine);
        string unload = mine.Output![ore] >= mine.Output.StackSize ? ore : "stone";
        var dest = full ? world.DepositFor(unload, mine.Pos, mine.Id) : null;
        if (full && dest == null) {
            w.Status = world.Phrase("Mine full · no storage takes ", world.Data.Name(unload));
            if (fullReported.Add(mine.Id)) world.Say($"The {Name(mine)} is full with no output available.");
            return false;
        }
        switch (world.Equip(w, DiggingTool, mine, dt)) {
            case World.Kit.Busy: return true;
            case World.Kit.Lacking: return false;
        }
        if (dest != null) {
            w.Status = world.Phrase("Bringing out ", world.Data.Name(unload));
            if (world.Walk(w, Door(mine), dt, .5f)) { mine.Output.TransferTo(w.Bag, unload, Worker.BagStack); w.Destination = dest.Id; }
            return true;
        }
        fullReported.Remove(mine.Id);
        w.Status = world.Phrase("Mining ", world.Data.Name(ore), " underground");
        if (world.Walk(w, Door(mine), dt, .5f)) w.WorkState = WorkerWorkState.Underground;
        return true;
    }
}
