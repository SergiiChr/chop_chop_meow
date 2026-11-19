using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

/// <summary>Laying gravel paths in checks, and sending a worker along one.</summary>
internal static class GravelPathSteps {
    /// <summary>
    /// Lays a path tile on each of the given tiles as a player does: within reach of it, with its gravel in the
    /// backpack.
    /// </summary>
    public static void LayPath(World world, params Vector2I[] tiles) {
        foreach (var tile in tiles) {
            Give(world, world.Data.Buildings[ThingKind.GravelPath].Cost);
            // A player already within a building's reach of the tile lays it from where they stand.
            if (world.Player.DistanceTo(tile) > 5) Warp(world, tile + new Vector2(0, 1));
            world.Construction.Build(ThingKind.GravelPath, tile);
            Require(world.GravelPaths.Laid(tile), $"a gravel path tile should be laid at {tile}");
        }
    }

    /// <summary>Lays a straight path of <paramref name="length"/> tiles from a tile toward higher x.</summary>
    public static void LayRow(World world, Vector2I from, int length) {
        for (int i = 0; i < length; i++) LayPath(world, from + new Vector2I(i, 0));
    }

    /// <summary>
    /// Lays <paramref name="count"/> path tiles on the first tiles that take one, along the rows from
    /// <paramref name="from"/>, and puts the player back where they stood.
    /// </summary>
    public static void LayWhereFree(World world, Vector2I from, int count) {
        var stood = world.Player;
        for (int i = 0; i < count; i++) Give(world, world.Data.Buildings[ThingKind.GravelPath].Cost);
        for (var tile = from; count > 0; tile = tile.X < Terrain.Size - 1 ? tile + Vector2I.Right : new Vector2I(0, tile.Y + 1)) {
            world.Player = tile + new Vector2(0, 1);
            world.Construction.Build(ThingKind.GravelPath, tile);
            if (world.GravelPaths.Laid(tile)) count--;
        }
        world.Player = stood;
    }

    /// <summary>
    /// A work day with a worker at <paramref name="from"/> sent to craft at a bench <paramref name="benchAfter"/> tiles
    /// toward higher x, along a path of <paramref name="tiles"/> gravel tiles; the player stands well aside.
    /// </summary>
    public static (World World, Worker Worker) WorkerSentAlongARow(GameData data, Vector2I from, int tiles, int benchAfter) {
        var world = WorldSteps.WorkDay(data);
        LayRow(world, from, tiles);
        var bench = WorkerSteps.Bench(world, from + new Vector2(benchAfter, 0), "planks", "stone_chisel");
        bench.Inventory.Add("wood", 4);
        var worker = AddWorker(world, from);
        worker.Wage = 18;
        world.SetDuty(worker, bench, JobKind.Craft, true);
        world.Player = from + new Vector2(0, 20);
        return (world, worker);
    }
}
