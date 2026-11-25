using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class StrawBedSteps {
    internal static Thing Build(World world, float rotation = 0) {
        Give(world, "leaves", 20);
        var bed = world.Construction.Build(ThingKind.StrawBed, world.Player + Vector2.Right * 2, rotation);
        Require(bed != null, "twenty leaves alone should build a straw bed on clear, dry ground");
        return bed!;
    }
}
