using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

/// <summary>Gathering in checks: holding a gather for a set time, and the clearings and thickets workers gather in.</summary>
internal static class GatheringSteps {
    /// <summary>Holds a gather on a thing from beside it, for a time or until it is gone.</summary>
    public static void HoldGather(World world, Thing thing, float seconds) {
        world.Player = thing.Pos + new Vector2(1, 0);
        for (; seconds > 0 && world.Find(thing.Id) != null; seconds -= Step) {
            world.Gather(thing, Step);
            SafeTick(world, Step);
        }
    }

    /// <summary>
    /// A clearing with a trading post, a chest, a spent tree and a loose coal beside it, more of each further off that a player has seen, and a closer tree and coal under black fog.
    /// The player has walked away, so everything known is out of sight.
    /// </summary>
    public static (World World, List<string> Said, Thing Post) KnownSpots(GameData data) {
        var world = EmptyWorld(data);
        var said = new List<string>();
        world.Notify += said.Add;
        var at = Terrain.Spawn;
        var post = world.Add(ThingKind.Signpost, at + new Vector2(-3, 2));
        TakeEverything(world, world.Add(ThingKind.Chest, at + new Vector2(2, 2)));
        world.Add(ThingKind.Tree, at + new Vector2(1, 0), 1);
        world.Add(ThingKind.Tree, at + new Vector2(6, 0), 2);
        world.Add(ThingKind.LooseCoal, at + new Vector2(0, 3), 1);
        world.Add(ThingKind.LooseCoal, at + new Vector2(4, 4), 1);
        world.Player = at; world.Look();
        // Out of every player's sight, so these stay under black fog.
        world.Player = at + new Vector2(0, 40); world.Look();
        world.Add(ThingKind.Tree, at + new Vector2(3, 0), 5);
        world.Add(ThingKind.LooseCoal, at + new Vector2(1, 4), 1);
        return (world, said, post);
    }

    /// <summary>A bush with an oak's leaf spot beside it, a chest and an empty tool station, all in the player's sight.</summary>
    public static (World World, Thing Bush, Thing Leaves, Thing Chest, Thing Rack) Thicket(GameData data) {
        var world = EmptyWorld(data);
        var bush = world.Add(ThingKind.Bush, new Vector2(10, 10), World.BushLeaves);
        var leaves = world.Add(ThingKind.LeafSpot, new Vector2(11, 11), World.LeavesPerSpot);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(12, 13)));
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, new Vector2(12, 8)));
        world.Player = new Vector2(14, 12); world.Look();
        return (world, bush, leaves, chest, rack);
    }
}
