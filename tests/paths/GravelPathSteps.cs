using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

/// <summary>Laying gravel paths in the simulation checks.</summary>
public static class GravelPathSteps {
    /// <summary>Lays a path tile on each of the given tiles as a player does: within reach of it, with its gravel in the backpack.</summary>
    public static void LayPath(World world, params Vector2I[] tiles) {
        foreach (var tile in tiles) {
            Give(world, "gravel", world.Data.Buildings[ThingKind.GravelPath].Cost["gravel"]);
            // A player already within a building's reach of the tile lays it from where they stand.
            if (world.Player.DistanceTo(tile) > 5) world.Player = tile + new Vector2(0, 1);
            world.Construction.Build(ThingKind.GravelPath, tile);
            Require(world.GravelPaths.Laid(tile), $"a gravel path tile should be laid at {tile}");
        }
    }

    /// <summary>Lays a straight path of <paramref name="length"/> tiles from a tile toward higher x.</summary>
    public static void LayRow(World world, Vector2I from, int length) {
        for (int i = 0; i < length; i++) LayPath(world, from + new Vector2I(i, 0));
    }
}
