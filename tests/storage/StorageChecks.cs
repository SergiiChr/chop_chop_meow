using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;

/// <summary>Checks of storage filters: what new storage takes from workers and lets them take out, and the Everything entry.</summary>
public static class StorageChecks {
    public static void Run(GameData data) {
        Test(CheckNewStorageTakesNothingInAndLetsEverythingOut, data);
        Test(CheckEverythingStandsAloneInItsRow, data);
        Test(CheckANewPileTakesNothingFromWorkersUntilItsResourceIsSet, data);
        Test(CheckATradingPostWithEverythingAsksForEveryCrateItBuys, data);
    }

    private static void CheckNewStorageTakesNothingInAndLetsEverythingOut(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(12, 10));
        var crate = world.Add(ThingKind.Crate, new Vector2(14, 10));
        var rack = world.Add(ThingKind.Toolstation, new Vector2(16, 10));
        foreach (var store in new[] { chest, crate, rack })
            Require(store.Inventory.InputFilter.Count == 0 && store.Inventory.OutputFilter.SequenceEqual(new[] { Inventory.Everything }),
                $"a new {store.Kind} should take nothing in and let everything out");
        Require(world.DepositFor("wood", new Vector2(10, 10)) == null, "a new chest or crate should take no load from a worker");
        chest.Inventory.Add("wood", 5);
        Require(world.SourceFor("wood", new Vector2(10, 10)) == chest, "a worker should take wood out of a new chest");
        world.SetFilter(chest, Inventory.Everything, false, true);
        Require(world.DepositFor("wood", new Vector2(10, 10)) == chest, "a chest set to Everything should take a worker's load");
    }

    private static void CheckEverythingStandsAloneInItsRow(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(12, 10));
        chest.Inventory.Add("wood", 5);
        world.SetFilter(chest, "stone", true, true);
        Require(chest.Inventory.OutputFilter.SequenceEqual(new[] { "stone" }), "naming an item in a row should take Everything off it");
        Require(world.SourceFor("wood", new Vector2(10, 10)) == null, "a chest letting only stone out should keep its wood");
        world.SetFilter(chest, Inventory.Everything, true, true);
        Require(chest.Inventory.OutputFilter.SequenceEqual(new[] { Inventory.Everything }), "adding Everything to a row should take its items off");
        world.ClearFilters(chest);
        Require(world.SourceFor("wood", new Vector2(10, 10)) == null && world.DepositFor("wood", new Vector2(10, 10)) == null,
            "a chest with both rows cleared should let nothing in or out");
    }

    private static void CheckANewPileTakesNothingFromWorkersUntilItsResourceIsSet(GameData data) {
        var world = EmptyWorld(data);
        var pile = world.Add(ThingKind.Pile, world.Player + new Vector2(2, 0));
        var barrel = world.Add(ThingKind.Barrel, world.Player + new Vector2(-2, 0));
        Require(world.DepositFor("wood", world.Player) == null, "a new pile should take no load from a worker");
        Require(world.DepositFor(Water.Liquid, world.Player) == null, "a barrel put down empty should take no water from a worker");
        Give(world, "wood", 1);
        Require(world.Transfer(world.Backpack, pile.Inventory, "wood", 1) == 1, "a player should put a first log into a new pile by hand");
        world.Tick(.1f);
        Require(world.DepositFor("wood", world.Player) == pile, "a pile set to wood by its first log should take a worker's wood");
        Require(barrel.Inventory.InputFilter.Count == 0, "an empty barrel should keep its filter empty");
    }

    private static void CheckATradingPostWithEverythingAsksForEveryCrateItBuys(GameData data) {
        var world = EmptyWorld(data);
        var post = world.Add(ThingKind.Signpost, world.Player + new Vector2(4, 0));
        string berries = PackCrate(world, ("berries", 12));
        Require(world.DepositFor(berries, world.Player) == null, "a new trading post should ask for no crates");
        world.SetFilter(post, Inventory.Everything, false, true);
        Require(world.DepositFor(berries, world.Player) == post, "a trading post set to Everything should ask for a crate of berries");
        Require(world.DepositFor("berries", world.Player) == null, "a trading post set to Everything should still take only packed crates");
    }
}
