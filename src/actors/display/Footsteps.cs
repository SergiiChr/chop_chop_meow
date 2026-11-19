using Godot;

namespace ChopChopMeow;

/// <summary>Which footstep sound the ground gives at a spot.</summary>
internal static class Footsteps {
    /// <summary>The sound of a foot landing at <paramref name="p"/>: gravel on a gravel path, water at the lake's edge, rock by cliffs, boulders and deposits, and grass elsewhere.</summary>
    public static string Sound(World world, Vector2 p) {
        if (world.GravelPaths.Covers(p)) return "footsteps_gravel";
        if (Terrain.InLake(p, 1)) return "footsteps_water";
        foreach (var c in World.Cliffs) if (c.DistanceTo(p) < Cliff.Depth + 3) return "footsteps_rocks";
        foreach (var t in world.Things) if ((t.Kind == ThingKind.Stone || t.IsDeposit) && t.Remaining > 0 && t.Pos.DistanceTo(p) < 1.3f) return "footsteps_rocks";
        return "footsteps_default";
    }
}
