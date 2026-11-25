using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class StrawBedSteps {
    internal static Thing Build(World world) {
        Give(world, "grass", 2);
        Give(world, "leaves", 20);
        var bed = world.Construction.Build(ThingKind.StrawBed, world.Player + Vector2.Right * 2);
        Require(bed != null, "the player should be able to build a straw bed on clear, dry ground");
        return bed!;
    }
}
