using System.Collections.Generic;
using ChopChopMeow;
using static SetupSteps;
using static WorldSteps;
using Godot;

/// <summary>A crew at work for a whole day: a cutter on a big tree, and a pumper and a hauler at a forge.</summary>
internal sealed record DayCrew(World World, Thing Tree, Thing Chest, Thing NearFire, Thing Forge, Worker Cutter, Worker Pumper, Worker Hauler);

/// <summary>A woodcutter on a tree about to fall, with a bigger tree further off, a chest and a tool station holding an axe.</summary>
internal sealed record Woodlot(World World, Thing Next, Thing Chest, Thing Rack, Worker Cutter);

/// <summary>Adding workers to a world for the simulation checks.</summary>
internal static class WorkerSteps {
    /// <summary>Starts a crew with wood and room for a whole day's work, and two campfires, one much nearer than the other.</summary>
    public static DayCrew StartDayCrew(GameData data) {
        var world = EmptyWorld(data);
        SetCoins(world, 200);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4000);
        var chest = world.Add(ThingKind.Chest, new Vector2(12, 10));
        chest.Inventory.Slots = 60;
        var near = world.Add(ThingKind.Campfire, new Vector2(14, 14));
        world.Add(ThingKind.Campfire, new Vector2(36, 36));
        var forge = world.Add(ThingKind.Reinforced, new Vector2(20, 12));
        var cutter = AddWorker(world, tree.Pos);
        cutter.Tool = "steel_axe";
        var pumper = AddWorker(world, forge.Pos);
        var hauler = AddWorker(world, forge.Pos);
        world.Assign(cutter, tree); world.Assign(pumper, forge); world.Assign(hauler, forge);
        return new DayCrew(world, tree, chest, near, forge, cutter, pumper, hauler);
    }

    /// <summary>Runs a crew to dusk with a load in the cutter's bag and nowhere to put it, adds a trading post with a worker, and lets them all sign out.</summary>
    public static Thing SignOutCrew(DayCrew crew) {
        var world = crew.World;
        RunUntil(world, world.Data.DuskStarts * world.Data.DaySeconds - 2);
        crew.Cutter.Bag.Remove("wood", 99); crew.Cutter.Bag.Add("wood", 2);
        crew.Chest.Inventory.InputFilter.Add("iron_bar");
        var sign = world.Add(ThingKind.Signpost, new Vector2(30, 20));
        world.Assign(AddWorker(world, sign.Pos), sign);
        Work(world, 40);
        return sign;
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
        world.Add(ThingKind.Chest, at + new Vector2(2, 2));
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

    /// <summary>A tree and a stone deposit worked from a chest and a tool station, with the player at the station.</summary>
    public static (World World, Thing Tree, Thing Rock, Thing Station) ToolYard(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 400);
        var rock = world.Add(ThingKind.Stone, new Vector2(10, 14), 400);
        world.Add(ThingKind.Chest, new Vector2(12, 12));
        var station = world.Add(ThingKind.Toolstation, new Vector2(14, 10));
        world.Player = station.Pos + Vector2.One;
        return (world, tree, rock, station);
    }

    /// <summary>Starts a woodcutter on a tree of 5 wood, all in the player's sight.</summary>
    public static Woodlot Woodlot(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 5);
        var next = world.Add(ThingKind.Tree, new Vector2(16, 10), 400);
        var chest = world.Add(ThingKind.Chest, new Vector2(12, 12));
        var rack = world.Add(ThingKind.Toolstation, new Vector2(12, 8));
        rack.Inventory.Add("steel_axe", 1);
        world.Player = new Vector2(13, 14); world.Look();
        var cutter = AddWorker(world, tree.Pos + new Vector2(1, 1));
        world.Assign(cutter, tree);
        return new Woodlot(world, next, chest, rack, cutter);
    }

    /// <summary>A bush with an oak's leaf patch beside it, a chest and an empty tool station, all in the player's sight.</summary>
    public static (World World, Thing Bush, Thing Leaves, Thing Chest, Thing Rack) Thicket(GameData data) {
        var world = EmptyWorld(data);
        var bush = world.Add(ThingKind.Bush, new Vector2(10, 10), World.BushLeaves);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(11, 11), World.LeafPatch);
        var chest = world.Add(ThingKind.Chest, new Vector2(12, 13));
        var rack = world.Add(ThingKind.Toolstation, new Vector2(12, 8));
        world.Player = new Vector2(14, 12); world.Look();
        return (world, bush, leaves, chest, rack);
    }

    public static void Hire(World world, Thing target) => world.Assign(AddWorker(world, target.Pos + new Vector2(1, 1)), target);
}
