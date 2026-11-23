using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Marks where an acorn goes and turns the mark into a planted tree after shovel work.</summary>
public sealed class TreePlanting(World world) {
    public const string Acorn = "acorn";
    public const int WaterNeeded = 5;
    public const int GrowDays = 10;

    private readonly PriorityQueue<Thing, float> growing = new();

    public static bool NeedsWater(Thing tree) => tree.Kind == ThingKind.Tree && tree.TreeStage == 1 && tree.NextTreeStageAt == 0;

    public static int DaysGrown(Thing tree) => tree.TreeStage == 0 ? GrowDays : tree.TreeStage - 1;

    private static int WoodAt(int days) => World.TreeWood * days / GrowDays;

    internal void BeginGrowth(Thing tree) {
        tree.Inventory.Remove(Water.Liquid, WaterNeeded);
        tree.NextTreeStageAt = world.Time + world.Data.DaySeconds;
        growing.Enqueue(tree, tree.NextTreeStageAt);
    }

    /// <summary>Only trees due for their next stage do work on a tick, including any days passed while sleeping.</summary>
    internal void Tick() {
        while (growing.TryPeek(out var tree, out float at) && world.Time >= at) {
            growing.Dequeue();
            if (world.Find(tree.Id) != tree) continue;
            int before = DaysGrown(tree), after = before + 1;
            tree.Remaining += WoodAt(after) - WoodAt(before);
            tree.TreeStage = after == GrowDays ? 0 : after + 1;
            tree.NextTreeStageAt = after == GrowDays ? 0 : at + world.Data.DaySeconds;
            if (tree.NextTreeStageAt > 0) growing.Enqueue(tree, tree.NextTreeStageAt);
        }
    }

    internal void Reset() {
        growing.Clear();
        foreach (var tree in world.ThingsOf(ThingKind.Tree))
            if (tree.NextTreeStageAt > 0) growing.Enqueue(tree, tree.NextTreeStageAt);
    }

    internal static bool ValidStage(Thing thing) => thing.TreeStage == 0 || thing.Kind switch {
        ThingKind.Tree => thing.TreeStage is >= 1 and <= GrowDays,
        ThingKind.Stump => thing.TreeStage is >= 2 and <= GrowDays,
        _ => false
    };

    internal static bool ValidGrowth(Thing thing) {
        if (thing.TreeStage == 0 || thing.Kind == ThingKind.Stump) return thing.NextTreeStageAt == 0;
        return thing.Remaining >= 0 && thing.Remaining <= WoodAt(DaysGrown(thing))
            && float.IsFinite(thing.NextTreeStageAt) && thing.NextTreeStageAt >= 0
            && (thing.NextTreeStageAt > 0 || thing.TreeStage == 1)
            && thing.Inventory.Slots == 1 && thing.Inventory.StackSize == WaterNeeded && thing.Inventory.Capacity == WaterNeeded
            && thing.Inventory.Count == thing.Inventory[Water.Liquid]
            && thing.Inventory.Count < WaterNeeded && (thing.NextTreeStageAt == 0 || thing.Inventory.Count == 0);
    }

    /// <summary>Puts an outline on open ground, leaving the held acorn in its stack.</summary>
    public bool Mark(Vector2 at) {
        at = at.Round();
        if (world.Held is not { Item: Acorn } held || !world.InReach(held.Box)) return false;
        if (world.Construction.PlacementBlock(ThingKind.PlantingSpot, at) is string blocked) { world.Say(blocked); return false; }
        if (world.Forwarded(Verb.MarkTree, at: at)) return true;
        world.Add(ThingKind.PlantingSpot, at);
        return true;
    }

    internal string? DigBlock() {
        if (world.BestTool("shovel") == null) return "Carry a shovel to plant the acorn.";
        return world.S.Backpack.Free(Acorn) == 0 ? "Carry an acorn in your backpack." : null;
    }

    internal void Dig(Thing spot, float dt) {
        if (!world.Near(spot, 1.35f)) return;
        spot.Progress = Mathf.Min(GatherRules.DigSeconds, spot.Progress + dt * world.Speed(world.BestTool("shovel")));
        if (!World.DugOut(spot) || world.S.Backpack.Remove(Acorn, 1) == 0) return;
        world.S.Things.Remove(spot);
        Plant(spot.Pos);
        world.Play("dig_toss", spot.Pos);
    }

    private void Plant(Vector2 at) {
        // Oaks are the trees whose id has no remainder across the three species.
        world.S.NextId += (World.TreeSpecies - world.S.NextId % World.TreeSpecies) % World.TreeSpecies;
        var tree = world.Add(ThingKind.Tree, at);
        tree.TreeStage = 1;
        tree.Inventory = new Inventory { Slots = 1, StackSize = WaterNeeded, Capacity = WaterNeeded };
    }
}
