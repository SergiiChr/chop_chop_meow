using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Stumps and bushes: the stump a felled tree leaves, the bushes of the valley, digging either up and building over them.</summary>
public partial class World {
    /// <summary>Wood a stump gives when it is dug up.</summary>
    public const int StumpWood = 3;
    /// <summary>Leaves a bush gives when it is dug up, three oak patches' worth.</summary>
    public const int BushLeaves = 3 * LeafPatch;

    /// <summary>Grows a bush on every scenery spot that is one, away from the clearing and the mountain feet, unless something already stands there.</summary>
    private void GrowBushes() {
        var points = Scenery.Points;
        for (int i = 0; i < points.Count; i++) {
            if (!Scenery.Bush(i)) continue;
            var p = points[i].At;
            if (Terrain.FromMountains(p) >= 3 && p.DistanceTo(Terrain.Spawn) >= 6 && FreeSpot(p)) Add(ThingKind.Bush, p, BushLeaves);
        }
    }

    /// <summary>
    /// Takes a tree whose last wood is gone out of the world and returns the stump it leaves in its place; the tree falls
    /// away from <paramref name="feller"/>, who landed the last blow.
    /// </summary>
    internal Thing Fell(Thing tree, Vector2 feller) {
        S.Things.Remove(tree);
        var stump = Add(ThingKind.Stump, tree.Pos, StumpWood);
        stump.OfTree = tree.Id;
        stump.TreeStage = tree.TreeStage;
        stump.FellToward = (tree.Pos - feller).Angle();
        return stump;
    }

    /// <summary>Whether enough digging has gone into a stump or a bush to bring it out of the ground.</summary>
    internal static bool DugOut(Thing t) => t.Progress >= GatherRules.DigSeconds;

    /// <summary>Lifts a dug out stump or bush into an inventory and takes it out of the world; false while that has no room for all of it.</summary>
    internal bool Unearth(Thing t, Inventory into) {
        string item = ResourceItem(t);
        int taken = into.Add(item, t.Remaining); t.Remaining -= taken;
        if (taken > 0) Got(item);
        if (t.Remaining > 0) return false;
        S.Things.Remove(t); Play("dig_up", t.Pos);
        return true;
    }

    /// <summary>Whether a kind grows low enough to build on: a building or yard goes up over it and clears it away.</summary>
    public static bool BuiltOver(ThingKind kind) => kind is ThingKind.Stump or ThingKind.Bush or ThingKind.PlantingSpot or ThingKind.Hole;

    /// <summary>Whether a spot is on the ground a building, yard or crate keeps clear of other resources, where it leaves no stump or bush.</summary>
    public static bool Clears(Thing built, Vector2 p) {
        if (!Construction.IsZone(built.Kind)) return p.DistanceTo(built.Pos) < Footprint(built.Kind) + Construction.ResourceRoom;
        float room = Footprint(built.Kind) + Construction.YardResourceRoom;
        return Math.Abs(p.X - built.Pos.X) < room && Math.Abs(p.Y - built.Pos.Y) < room;
    }

    /// <summary>Clears away the stumps and bushes under a new building, yard or crate.</summary>
    internal void ClearGround(Thing built) {
        foreach (var kind in ClearedKinds) S.Things.RemoveAll(kind, t => Clears(built, t.Pos));
    }
    /// <summary>Clears away a stump or bush standing on a tile a gravel path is laid on.</summary>
    internal void ClearTile(Vector2I tile) {
        foreach (var kind in ClearedKinds) S.Things.RemoveAll(kind, t => Terrain.TileAt(t.Pos) == tile);
    }
    private static readonly ThingKind[] ClearedKinds = Array.FindAll(Kinds.All, BuiltOver);
}
