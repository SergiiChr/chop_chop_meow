using System.Collections.Generic;
using System.Linq;
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
        // Iron and coal for a day of steel, so the forge crew has something to smelt.
        chest.Inventory.Add("iron_bar", 300); chest.Inventory.Add("coal", 500);
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

    /// <summary>A tree and a berry bush worked from a chest and a tool station, with the player at the station.</summary>
    public static (World World, Thing Tree, Thing Bush, Thing Station) ToolYard(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 400);
        var bush = world.Add(ThingKind.Berries, new Vector2(10, 14), 400);
        world.Add(ThingKind.Chest, new Vector2(12, 12));
        var station = world.Add(ThingKind.Toolstation, new Vector2(14, 10));
        world.Player = station.Pos + Vector2.One;
        return (world, tree, bush, station);
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

    public static void Hire(World world, Thing target) => world.Assign(AddWorker(world, target.Pos + new Vector2(1, 1)), target);

    /// <summary>Adds a paid worker beside a place with the given jobs there, in that order.</summary>
    public static Worker Staff(World world, Thing place, params JobKind[] jobs) {
        var worker = AddWorker(world, place.Pos + new Vector2(0, 1.2f));
        worker.Wage = 18;
        foreach (var job in jobs) world.SetDuty(worker, place, job, true);
        return worker;
    }

    /// <summary>A crafting table on a recipe with the tool it needs slotted, and nobody working it.</summary>
    public static Thing Bench(World world, Vector2 at, string recipe, string tool = "") {
        var bench = world.Add(ThingKind.Bench, at);
        world.SetRecipe(bench, recipe);
        if (tool != "") bench.Tools!.Add(tool, 1);
        return bench;
    }

    /// <summary>Makes a haul group at the end of a worker's jobs and moves their hauls at the given places into it, in that order; returns the group's number.</summary>
    public static int GroupHauls(World world, Worker worker, params Thing[] places) {
        world.AddHaulGroup(worker);
        var group = (HaulGroup)worker.Jobs[^1];
        foreach (var place in places) {
            int from = worker.Jobs.FindIndex(e => e is Duty { Job: JobKind.Haul } d && d.Thing == place.Id);
            world.MoveJob(worker, new JobSpot(from), new JobSpot(worker.Jobs.IndexOf(group), group.Hauls.Count));
        }
        return group.Id;
    }

    /// <summary>A worker's jobs as text, such as "craft@3 group[haul@3 haul@5]", to compare a whole list at once.</summary>
    public static string JobsText(Worker worker) => string.Join(" ", worker.Jobs.Select(e => e is HaulGroup group ? $"group[{string.Join(" ", group.Hauls.Select(World.JobKey))}]" : World.JobKey(e)));

    /// <summary>Runs the world until a worker has something in hand, and names it; null when they pick nothing up in a minute.</summary>
    public static string? FirstCarry(World world, Worker worker) {
        Work(world, 60, () => worker.Bag.Count > 0);
        return worker.Bag.First();
    }
}
