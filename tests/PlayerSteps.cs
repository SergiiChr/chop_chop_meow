using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>What the player does by hand in the simulation checks.</summary>
internal static class PlayerSteps {
    /// <summary>Fills a crate at the player's feet and picks it up, returning the packed crate item now in the backpack.</summary>
    public static string PackCrate(World world, params (string Item, int Count)[] goods) {
        var crate = world.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        foreach (var (item, count) in goods) Require(crate.Inventory.Add(item, count) == count, $"a crate should take {count} {item}");
        var before = world.Backpack.Items.Keys.ToHashSet();
        Require(world.PickUpCrate(crate), "a nearby crate should be picked up");
        return world.Backpack.Items.Keys.Single(id => !before.Contains(id));
    }

    /// <summary>A compost pile on open ground with a farm plot built beside it, the player standing by with one sowing's seeds and fertilizer.</summary>
    public static (World World, Thing Compost, Thing Farm) FarmBesideCompost(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin + new Vector2(1.5f, 2.5f);
        Give(world, "wood", 16); Give(world, "wheat_seeds", 1); Give(world, "fertilizer", 1);
        var compost = world.Build(ThingKind.Pile, origin);
        Require(compost != null, "a pile should build on open ground");
        world.SetCompost(compost!, true);
        var farm = world.Build(ThingKind.Farm, origin + new Vector2(3.4f, .3f));
        Require(farm != null, "a farm plot should build beside a pile");
        return (world, compost!, farm!);
    }

    public static void GatherUntil(World world, Thing resource, string item, int quantity) {
        world.Player = resource.Pos;
        int attempts = 0;
        while (world.Carried(item) < quantity && attempts++ < 200) {
            int before = world.Carried(item);
            world.Gather(resource, world.Data.GatherSeconds);
            Require(world.Carried(item) > before, $"gathering {item} should produce raw material");
            SafeTick(world, world.Data.GatherSeconds);
        }
        Require(world.Carried(item) >= quantity, $"gathered enough {item} for the progression");
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
                $"station should complete {quantity} {output} batch(es): kind={station.Kind}, recipe={station.Recipe}, output={station.Inventory[output]}, processing={station.Processing}, progress={station.Progress:0.0}, player={world.Player}, stationPos={station.Pos}, health={world.Health:0.0}, time={world.Time:0.0}");
    }

    public static void CraftByHand(World world, string recipe, int times) {
        for (int i = 0; i < times; i++) Require(world.Craft(recipe), $"{recipe} should be craftable by hand: {world.CraftBlock(recipe)}");
        for (int limit = 400; world.Crafting.Count > 0 && limit > 0; limit--) SafeTick(world, Step);
        Require(world.Crafting.Count == 0, $"hand crafting {recipe} should finish: rate={world.CraftRate}, progress={world.CraftTime}, slots={world.Backpack.UsedSlots}, items={string.Join(",", world.Backpack.Items.Select(p => p.Key + "=" + p.Value))}, room={world.Backpack.Room(world.Data.Recipes[recipe].Output)}");
    }
}
