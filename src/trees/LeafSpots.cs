using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Leaf spots beneath oaks and the acorns that can be gathered with their leaves.</summary>
public sealed class LeafSpots(World world) {
    private const int AcornRefillDays = 10;
    private readonly PriorityQueue<Thing, float> refilling = new();
    private readonly Dictionary<int, Thing> attached = new();

    private static int Capacity(Thing spot) => 1 + (spot.Id & 1);
    public static int Acorns(Thing spot) => Capacity(spot) - spot.LeafAcornsTaken;
    internal static float Radius(int stage) => .6f * TreePlanting.GrowthScale(stage);

    /// <summary>Places leaf spots offset from the trunks when the valley is made.</summary>
    internal void Scatter() {
        foreach (var tree in world.ThingsOf(ThingKind.Tree)) {
            var at = tree.Pos + World.LeafOffset;
            if (tree.Id % World.TreeSpecies == 0 && Terrain.Open(at)) Add(tree, at);
        }
    }

    /// <summary>Finds room for a new adult oak's leaf spot, close enough to the trunk to lie under the crown.</summary>
    internal void Drop(Thing tree) {
        var offset = World.LeafOffset;
        for (int ring = 0; ring < 2; ring++, offset *= .5f)
            for (int direction = 0; direction < 8; direction++) {
                var at = tree.Pos + offset.Rotated(direction * Mathf.Tau / 8);
                if (!Room(at)) continue;
                attached.Add(tree.Id, Add(tree, at));
                return;
            }
        attached.Add(tree.Id, Add(tree, tree.Pos));
    }

    private Thing Add(Thing tree, Vector2 at) {
        var spot = world.Add(ThingKind.LeafSpot, at, World.LeavesPerSpot);
        spot.OfTree = tree.Id;
        spot.TreeStage = tree.TreeStage;
        return spot;
    }

    internal void Grow(Thing tree) {
        if (attached.TryGetValue(tree.Id, out var spot)) spot.TreeStage = tree.TreeStage;
    }

    internal void TreeFelled(Thing tree) => attached.Remove(tree.Id);

    private bool Room(Vector2 at) {
        if (!world.CanStand(at) || world.GravelPaths.Covers(at)) return false;
        foreach (var t in world.Things) {
            if (t.Kind == ThingKind.LeafSpot && t.Pos.DistanceSquaredTo(at) < .36f) return false;
            if (!Construction.IsZone(t.Kind)) continue;
            var offset = (at - t.Pos).Abs();
            if (Mathf.Max(offset.X, offset.Y) < World.Footprint(t.Kind)) return false;
        }
        return true;
    }

    /// <summary>Takes as many acorns as will fit alongside the gathered leaves.</summary>
    internal void Gather(Thing spot, Inventory bag) {
        int available = Acorns(spot);
        if (available == 0) return;
        int taken = bag.Add(TreePlanting.Acorn, available);
        if (taken == 0) return;
        spot.LeafAcornsTaken += taken;
        world.Got(TreePlanting.Acorn);
        if (spot.Regrow == 0) RefillAt(spot, world.Time + AcornRefillDays * world.Data.DaySeconds);
    }

    internal void Tick() {
        while (refilling.TryPeek(out var spot, out float at) && world.Time >= at) {
            refilling.Dequeue();
            if (world.Find(spot.Id) != spot) continue;
            if (--spot.LeafAcornsTaken == 0) spot.Regrow = 0;
            else RefillAt(spot, at + AcornRefillDays * world.Data.DaySeconds);
        }
    }

    private void RefillAt(Thing spot, float at) {
        spot.Regrow = at;
        refilling.Enqueue(spot, at);
    }

    internal void Reset() {
        refilling.Clear();
        attached.Clear();
        foreach (var spot in world.ThingsOf(ThingKind.LeafSpot)) {
            if (spot.Regrow > 0) refilling.Enqueue(spot, spot.Regrow);
            if (world.Find(spot.OfTree) is { Kind: ThingKind.Tree }) attached.Add(spot.OfTree, spot);
        }
    }

    internal static bool Valid(Thing thing) => thing.Kind != ThingKind.LeafSpot ? thing.LeafAcornsTaken == 0
        : thing.LeafAcornsTaken >= 0 && thing.LeafAcornsTaken <= Capacity(thing)
        && (thing.LeafAcornsTaken == 0 ? thing.Regrow == 0 : float.IsFinite(thing.Regrow) && thing.Regrow > 0);
}
