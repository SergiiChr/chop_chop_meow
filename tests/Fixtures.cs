using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>World setup shared by the simulation checks.</summary>
internal static class Fixtures {
    public const float Step = .25f;

    /// <summary>A world without its starting map, with a fed and healthy player on the first morning.</summary>
    public static World EmptyWorld(GameData data) => new(data, false);

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
    public static Worker AddWorker(World world, Vector2 position) {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Worker {world.S.Workers.Count + 1}", Pos = position };
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

    /// <summary>An empty map with coins enough to pay every wage, so no worker strikes.</summary>
    public static World FundedWorld(GameData data) {
        var world = EmptyWorld(data);
        world.S.Backpack.Add(Inventory.Coins, 1000);
        return world;
    }

    /// <summary>Runs the world with the player fed and out of the wolves' way, through nights as well, until a condition holds.</summary>
    public static void Work(World world, float seconds, Func<bool>? until = null) {
        for (; seconds > 0 && until?.Invoke() != true; seconds -= Step) {
            world.S.Hunger = world.S.Thirst = world.S.Health = 100;
            world.Tick(Step, true);
        }
    }

    /// <summary>A mine marked at the first cliff of an ore, with the player standing in front of it.</summary>
    public static Thing MarkMine(World world, ThingKind ore = ThingKind.Iron) {
        var cliff = World.CliffOf(ore);
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        var mine = world.Build(ThingKind.Mine, World.MineSpot(cliff) + cliff.Along + cliff.Face);
        Require(mine != null, $"a mine should be marked at the {ore} cliff");
        return mine!;
    }

    /// <summary>Rewrites a save into an older form, failing when a text it rewrites is missing so the fixture cannot silently stop testing anything.</summary>
    public static string Patch(string json, params (string Old, string New)[] edits) {
        foreach (var (old, replacement) in edits) {
            Require(json.Contains(old), $"save fixture text {old} is missing");
            json = json.Replace(old, replacement);
        }
        return json;
    }

    public static void SetCoins(World world, int coins) {
        world.S.Backpack.Remove(Inventory.Coins, world.Coins);
        world.S.Backpack.Add(Inventory.Coins, coins);
    }

    public static World NightWorld(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        world.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        return world;
    }

    public static List<Thing> Known(World world) {
        var known = new List<Thing>();
        world.CollectKnown(known);
        return known;
    }

    public static void GatherUntil(World world, Thing resource, string item, int quantity) {
        world.Player = resource.Pos;
        int attempts = 0;
        while (world.S.Backpack[item] < quantity && attempts++ < 200) {
            int before = world.S.Backpack[item];
            world.Gather(resource, world.Data.GatherSeconds);
            Require(world.S.Backpack[item] > before, $"gathering {item} should produce raw material");
            SafeTick(world, world.Data.GatherSeconds);
        }
        Require(world.S.Backpack[item] >= quantity, $"gathered enough {item} for the progression");
    }

    public static void PickUp(World world, Thing loose) {
        world.Player = loose.Pos;
        world.Gather(loose, 0);
        Require(world.Find(loose.Id) == null, $"picking up {loose.Kind} should take the whole piece at once");
    }

    public static Thing BuildAt(World world, ThingKind kind, Vector2 position) {
        world.Player = position + new Vector2(-2, 0);
        var built = world.Build(kind, position);
        Require(built != null, $"build {kind} at a clear nearby location");
        return built!;
    }

    public static void CompleteBatches(World world, Thing station, string output, int quantity, bool pump = false) {
        int limit = 3000;
        while (station.Inventory[output] < quantity && limit-- > 0) {
            if (pump && station.Heat < 90) world.Pump(station);
            SafeTick(world, Step);
        }
        Require(limit > 0 && station.Inventory[output] >= quantity,
                $"station should complete {quantity} {output} batch(es): kind={station.Kind}, recipe={station.Recipe}, output={station.Inventory[output]}, processing={station.Processing}, progress={station.Progress:0.0}, player={world.Player}, stationPos={station.Pos}, health={world.S.Health:0.0}, time={world.S.Time:0.0}");
    }

    public static void CraftByHand(World world, string recipe, int times) {
        for (int i = 0; i < times; i++) Require(world.Craft(recipe), $"{recipe} should be craftable by hand: {world.CraftBlock(recipe)}");
        for (int limit = 400; world.S.Crafting.Count > 0 && limit > 0; limit--) SafeTick(world, Step);
        Require(world.S.Crafting.Count == 0, $"hand crafting {recipe} should finish: rate={world.CraftRate}, progress={world.S.CraftTime}, slots={world.S.Backpack.UsedSlots}, items={string.Join(",", world.S.Backpack.Items.Select(p => p.Key + "=" + p.Value))}, room={world.S.Backpack.Room(world.Data.Recipes[recipe].Output)}");
    }

    /// <summary>A spot in the open middle of the valley, so the progression run has room to build.</summary>
    public static Vector2 Site(float x, float y) => new(x + 60, y + 60);

    public static World TorchNight(GameData data) {
        var world = NightWorld(data);
        world.S.Backpack.Add("torch", 1); world.UseTorch(Vector2.Zero); world.Tick(World.KindleSeconds + .1f);
        Require(world.S.TorchLit, "the torch fixture should be lit");
        return world;
    }

    /// <summary>Ticks a night with only the given wolves, so new arrivals do not interfere.</summary>
    public static void WolfTick(World world, float seconds, params Wolf[] keep) {
        for (float t = 0; t < seconds; t += .1f) {
            world.S.Hunger = world.S.Thirst = 100; world.Tick(.1f);
            world.S.Wolves.RemoveAll(w => !keep.Contains(w));
        }
    }
}
