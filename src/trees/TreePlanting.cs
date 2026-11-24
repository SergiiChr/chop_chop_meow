using System;
using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>Plants acorns and advances standing trees as they grow.</summary>
public sealed class TreePlanting(World world) {
    public const string Acorn = "acorn";
    public const int WaterNeeded = 5;
    public const int AdultDays = 10;
    private const int StagesPerTick = 32;

    private readonly PriorityQueue<Thing, float> growing = new();

    public static bool NeedsWater(Thing tree) => tree.Kind == ThingKind.Tree && tree.TreeStage == 1 && tree.NextTreeStageAt == 0;

    public static bool IsAdult(Thing tree) => DaysGrown(tree) >= AdultDays;

    internal static bool IsYoung(Thing thing) => thing.Kind == ThingKind.Tree && !IsAdult(thing);

    public static int DaysGrown(Thing tree) => DaysGrown(tree.TreeStage);

    private static int DaysGrown(int stage) => stage == 0 ? AdultDays : stage - 1;

    /// <summary>Size relative to adulthood, with a straight start and slower gains each day from then on.</summary>
    internal static float GrowthScale(int stage) => (float)GrowthAt(DaysGrown(stage));

    internal static float Tallness(int id) => Thing.Variation(id, 2);

    /// <summary>Size variation between trees of the same age.</summary>
    internal static float BaseScale(int id) => .9f + Tallness(id) * .6f;

    /// <summary>The same trunk radius used to draw and pick the tree.</summary>
    internal static float TrunkRadius(int id, int stage) {
        if (stage == 1) return .32f;
        float trunk = (id % World.TreeSpecies) switch { 1 => .3f, 2 => .25f, _ => .35f };
        return Math.Max(.2f, trunk * BaseScale(id) * GrowthScale(stage));
    }

    private static double GrowthAt(int days) {
        double age = days / (double)AdultDays;
        return days <= AdultDays ? age : 1 + Math.Log(age);
    }

    private static int WoodAt(int days) => (int)(World.TreeWood * GrowthAt(days));

    internal void BeginGrowth(Thing tree) {
        tree.Inventory.Remove(Water.Liquid, WaterNeeded);
        tree.NextTreeStageAt = world.Time + world.Data.DaySeconds;
        growing.Enqueue(tree, tree.NextTreeStageAt);
    }

    /// <summary>Advances due trees, spreading any backlog after a clock jump over several ticks.</summary>
    internal void Tick() {
        for (int processed = 0; processed < StagesPerTick && growing.TryPeek(out var tree, out float at)
            && world.Time >= at; processed++) {
            growing.Dequeue();
            if (world.Find(tree.Id) != tree) continue;
            float previousRadius = TrunkRadius(tree.Id, tree.TreeStage);
            int before = DaysGrown(tree), after = before + 1;
            tree.Remaining += WoodAt(after) - WoodAt(before);
            tree.TreeStage = after + 1;
            if (after == AdultDays) world.LeafSpots.Drop(tree);
            else if (after > AdultDays) world.LeafSpots.Grow(tree);
            float radius = TrunkRadius(tree.Id, tree.TreeStage);
            if (radius > previousRadius) world.Construction.CrushInTrunk(tree.Pos, radius);
            tree.NextTreeStageAt = at + world.Data.DaySeconds;
            growing.Enqueue(tree, tree.NextTreeStageAt);
        }
    }

    internal void Reset() {
        growing.Clear();
        var trees = world.ThingsOf(ThingKind.Tree);
        for (int slot = 0; slot < trees.Count; slot++) {
            var tree = trees[slot];
            // The starting trees each grow once a day, with their first changes spread across that day.
            if (tree.TreeStage == 0 && tree.NextTreeStageAt == 0)
                tree.NextTreeStageAt = world.Time + world.Data.DaySeconds * (slot + 1) / trees.Count;
            if (tree.NextTreeStageAt > 0) growing.Enqueue(tree, tree.NextTreeStageAt);
        }
    }

    internal static bool ValidStage(Thing thing) => thing.TreeStage == 0 || thing.Kind switch {
        ThingKind.Tree => thing.TreeStage >= 1,
        ThingKind.Stump => thing.TreeStage >= 2,
        ThingKind.LeafSpot => thing.TreeStage > AdultDays && thing.OfTree > 0,
        _ => false
    };

    internal static bool ValidGrowth(Thing thing) {
        if (thing.Kind != ThingKind.Tree) return thing.NextTreeStageAt == 0;
        if (!float.IsFinite(thing.NextTreeStageAt) || thing.NextTreeStageAt < 0 || thing.Remaining < 0) return false;
        if (IsAdult(thing)) return thing.TreeStage == 0 || thing.NextTreeStageAt > 0;
        return thing.Remaining <= WoodAt(DaysGrown(thing))
            && (thing.NextTreeStageAt > 0 || thing.TreeStage == 1)
            && thing.Inventory.Slots == 1 && thing.Inventory.StackSize == WaterNeeded && thing.Inventory.Capacity == WaterNeeded
            && thing.Inventory.Count == thing.Inventory[Water.Liquid]
            && thing.Inventory.Count < WaterNeeded && (thing.NextTreeStageAt == 0 || thing.Inventory.Count == 0);
    }

    /// <summary>Consumes an acorn just placed in a hole and replaces it with an oak nub.</summary>
    internal void PlantIn(Thing hole) {
        if (hole.Inventory.Remove(Acorn, 1) != 1) return;
        world.S.Things.Remove(hole);
        // Oaks are the trees whose id has no remainder across the three species.
        world.S.NextId += (World.TreeSpecies - world.S.NextId % World.TreeSpecies) % World.TreeSpecies;
        var tree = world.Add(ThingKind.Tree, hole.Pos);
        tree.TreeStage = 1;
        tree.Inventory = new Inventory { Slots = 1, StackSize = WaterNeeded, Capacity = WaterNeeded };
    }
}
