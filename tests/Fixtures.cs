using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>World setup shared by the simulation checks.</summary>
internal static class Fixtures {
    private const float Step = .25f;

    /// <summary>A world without its starting map, with a fed and healthy player on the first morning.</summary>
    public static World EmptyWorld(GameData data) {
        var world = new World(data, false);
        world.S.Hunger = 100;
        world.S.Thirst = 100;
        world.S.Health = 100;
        world.S.Time = 55;
        return world;
    }

    /// <summary>Runs the world for a time with the player kept fed and watered.</summary>
    public static void SafeTick(World world, float seconds) {
        world.S.Hunger = 100;
        world.S.Thirst = 100;
        while (seconds > 0) {
            float dt = Math.Min(Step, seconds);
            world.Tick(dt);
            seconds -= dt;
            world.S.Hunger = 100;
            world.S.Thirst = 100;
        }
    }

    /// <summary>Adds a worker directly, without a hiring offer or its payment.</summary>
    public static Worker AddWorker(World world, Vector2 position, int? wage = null) {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Worker {world.S.Workers.Count + 1}", Pos = position };
        if (wage != null) worker.Wage = wage.Value;
        world.S.Workers.Add(worker);
        return worker;
    }

    /// <summary>Fills a crate at the player's feet and picks it up, returning the packed crate item now in the backpack.</summary>
    public static string PackCrate(World world, params (string Item, int Count)[] goods) {
        var crate = world.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        foreach (var (item, count) in goods) Require(crate.Inventory.Add(item, count) == count, $"a crate should take {count} {item}");
        var before = world.S.Backpack.Items.Keys.ToHashSet();
        Require(world.PickUpCrate(crate), "a nearby crate should be picked up");
        return world.S.Backpack.Items.Keys.Single(id => !before.Contains(id));
    }
}
