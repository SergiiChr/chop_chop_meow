using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

/// <summary>Nettles in checks: a plant to pick and a yard a worker picks them in.</summary>
internal static class NettleSteps {
    /// <summary>An empty world with one nettle on open ground.</summary>
    public static (World World, Thing Nettle) NettlePatch(GameData data) {
        var world = EmptyWorld(data);
        return (world, world.Add(ThingKind.Nettle, new Vector2(10, 10), 1));
    }

    /// <summary>
    /// A bench beside the player on nettle rope, with a hammer in its slot and a nettle in the player's backpack.
    /// Gives what the player is told from then on.
    /// </summary>
    public static (World World, Thing Bench, List<string> Said) RopeBench(GameData data, string slotted) {
        var world = EmptyWorld(data);
        var bench = Bench(world, Site(0, 4), "nettle_rope", slotted);
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        Give(world, "nettle", 1);
        return (world, bench, Told(world));
    }

    /// <summary>A nettle a worker has the job of picking, with a chest and an empty tool station by it, all in the player's sight.</summary>
    public static (World World, Thing Nettle, Thing Chest, Thing Rack, Worker Picker) NettleYard(GameData data) {
        var (world, nettle) = NettlePatch(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(12, 12)));
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, new Vector2(14, 10)));
        world.Player = rack.Pos + Vector2.One; world.Look();
        var picker = AddWorker(world, nettle.Pos + new Vector2(1, 1));
        world.Assign(picker, nettle);
        return (world, nettle, chest, rack, picker);
    }
}
