using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class SteelPipeSteps {
    /// <summary>Lays a pipe with the player within reach and the plan's materials in the backpack.</summary>
    public static void LayPipe(World world, Vector2 at, float rotation = 0, PipeShape shape = PipeShape.Straight) {
        if (world.Player.DistanceTo(at) > 5) world.Player = at + Vector2.Down;
        Give(world, world.Data.Buildings[ThingKind.SteelPiping].Cost);
        world.Construction.Build(ThingKind.SteelPiping, at, rotation, shape);
        Require(world.SteelPipes.RotationAt(Terrain.TileAt(at)) == rotation && world.SteelPipes.ShapeAt(Terrain.TileAt(at)) == shape, "Construction should lay the pipe on its tile.");
    }
}
