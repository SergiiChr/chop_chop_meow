using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static WorkerSteps;
using static EconomySteps;

/// <summary>World setup and passing time for the simulation checks.</summary>
internal static class WorldSteps {
    public const float Step = .25f;

    /// <summary>A world without its starting map, with a fed and healthy player on the first morning.</summary>
    public static World EmptyWorld(GameData data) => new(data, false);

    /// <summary>An empty map with coins enough to pay every wage, so no worker strikes.</summary>
    public static World FundedWorld(GameData data) {
        var world = EmptyWorld(data);
        world.S.Backpack.Add(Inventory.Coins, 1000);
        return world;
    }

    public static World NightWorld(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        world.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        return world;
    }

    public static World TorchNight(GameData data) {
        var world = NightWorld(data);
        world.S.Backpack.Add("torch", 1); world.UseTorch(Vector2.Zero); world.Tick(World.KindleSeconds + .1f);
        Require(world.S.TorchLit, "the torch fixture should be lit");
        return world;
    }

    public static World WorkDay(GameData data) {
        var world = new World(data, false);
        world.S.Time = 0;
        world.S.Backpack.Add(Inventory.Coins, 1000);
        return world;
    }

    /// <summary>A daytime camp in the generated valley: woodcutters, a bellows crew at a hot forge, a furnace, a campfire and storage.</summary>
    public static World Settlement(GameData data) {
        var world = new World(data);
        world.S.Time = data.DaySeconds * .3f;
        var at = world.Player;
        var chest = world.Add(ThingKind.Chest, at + new Vector2(2, 0));
        world.Add(ThingKind.Chest, at + new Vector2(2, -2));
        chest.Inventory.Add("iron_bar", 20); chest.Inventory.Add("coal", 30); chest.Inventory.Add("iron_ore", 20); chest.Inventory.Add("wood", 30);
        var rack = world.Add(ThingKind.Toolstation, at + new Vector2(-2, 0));
        rack.Inventory.Add("steel_axe", 2);
        var fire = world.Add(ThingKind.Campfire, at + new Vector2(0, 3)); fire.Inventory.Add("wood", 20);
        var furnace = world.Add(ThingKind.Furnace, at + new Vector2(3, 3));
        var forge = world.Add(ThingKind.Reinforced, at + new Vector2(-3, 3)); forge.Heat = 90;
        foreach (var tree in world.S.Things.Where(t => t.Kind == ThingKind.Tree).OrderBy(t => t.Pos.DistanceTo(at)).Take(2))
            world.Assign(AddWorker(world, rack.Pos), tree);
        world.Assign(AddWorker(world, forge.Pos + World.BellowsStand), forge);
        world.Assign(AddWorker(world, chest.Pos), forge);
        world.Assign(AddWorker(world, chest.Pos), furnace);
        return world;
    }

    /// <summary>An open iron mine with a full crew of pickaxe-carrying miners and a chest beside it.</summary>
    public static World WorkingMine(GameData data, out Thing mine) {
        var world = new World(data, false);
        world.S.Time = data.DaySeconds * .3f;
        var cliff = World.CliffOf(ThingKind.Iron);
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        mine = world.Add(ThingKind.Mine, World.MineSpot(cliff)); mine.MineStage = MineStage.Open;
        world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3);
        for (int i = 0; i < data.Mine.Workers; i++) {
            var w = AddWorker(world, mine.Pos + cliff.Face * 2);
            w.Tool = "steel_pick"; world.Assign(w, mine);
        }
        return world;
    }

    /// <summary>A packing station packing berries, its packer, a chest of berries and empty crates, and a post asking for crates of berries that sells on its own.</summary>
    public static World PackingLine(GameData data, out Thing stock) {
        var world = new World(data, false);
        world.S.Time = data.DaySeconds * .3f;
        var at = Terrain.Spawn;
        world.Player = at + new Vector2(-20, 0);
        stock = world.Add(ThingKind.Chest, at + new Vector2(2, 0));
        var packer = world.Add(ThingKind.Packer, at); world.SetPacking(packer, "berries");
        var sign = world.Add(ThingKind.Signpost, at + new Vector2(0, 3));
        world.SetFilter(sign, World.CrateOf("berries"), false, true); world.SetAutoSell(sign, true);
        world.Assign(AddWorker(world, at + new Vector2(1, 1)), packer);
        return world;
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

    /// <summary>A spot in the open middle of the valley, so the progression run has room to build.</summary>
    public static Vector2 Site(float x, float y) => new(x + 60, y + 60);

    public static List<Thing> Known(World world) {
        var known = new List<Thing>();
        world.CollectKnown(known);
        return known;
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

    /// <summary>Runs the world with the player fed and out of the wolves' way, through nights as well, until a condition holds.</summary>
    public static void Work(World world, float seconds, Func<bool>? until = null) {
        for (; seconds > 0 && until?.Invoke() != true; seconds -= Step) {
            world.S.Hunger = world.S.Thirst = world.S.Health = 100;
            world.Tick(Step, true);
        }
    }

    /// <summary>Ticks a night with only the given wolves, so new arrivals do not interfere.</summary>
    public static void WolfTick(World world, float seconds, params Wolf[] keep) {
        for (float t = 0; t < seconds; t += .1f) {
            world.S.Hunger = world.S.Thirst = 100; world.Tick(.1f);
            world.S.Wolves.RemoveAll(w => !keep.Contains(w));
        }
    }

    public static void FedPlayersTick(World world, float seconds) {
        for (; seconds > 0; seconds -= .25f) {
            foreach (var p in world.S.Players) { p.Hunger = 100; p.Thirst = 100; }
            world.Tick(.25f);
        }
    }

    public static void Fed(World world) { world.S.Hunger = 100; world.S.Thirst = 100; world.S.Health = 100; }
}
