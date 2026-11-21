using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

public static class FarmSteps {
    /// <summary>A compost pile on open ground with a farm plot built beside it, the player standing by with one sowing's seeds and fertilizer.</summary>
    public static (World World, Thing Compost, Thing Farm) FarmBesideCompost(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin + new Vector2(1.5f, 2.5f);
        Give(world, "wood", 16); Give(world, "wheat_seeds", 1); Give(world, "fertilizer", 1);
        var compost = world.Construction.Build(ThingKind.Pile, origin);
        Require(compost != null, "a pile should build on open ground");
        world.SetCompost(compost!, true);
        var farm = world.Construction.Build(ThingKind.Farm, origin + new Vector2(4.7f, 1.65f));
        Require(farm != null, "a farm plot should build beside a pile");
        return (world, compost!, farm!);
    }
}
