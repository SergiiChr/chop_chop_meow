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
        Test(CheckOutputFilterBlocksAutomatedWithdrawal, data);
        Test(CheckFilterNamesEachEntryOnceInTheOrderNamed);
        Test(CheckItemsRouteToAcceptingStorageBeforeNearerGeneralChest, data);
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

    private static void CheckOutputFilterBlocksAutomatedWithdrawal(GameData data) {
        var source = new Inventory { Slots = 1 };
        var destination = new Inventory { Slots = 1 };
        source.Add("iron_bar", 4);
        source.Filter("wood", true, true);
        destination.Filter("wood", false, true);
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated withdrawal must respect the source output filter");
        Require(source["iron_bar"] == 4 && destination["iron_bar"] == 0,
                "blocked automated withdrawal must leave both inventories unchanged");

        source.OutputFilter.Clear();
        source.Filter("iron_bar", true, true);
        destination.InputFilter.Clear();
        destination.Filter("wood", false, true);
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated delivery must respect the destination input filter");
        destination.Filter("iron_bar", false, true);
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 3,
                "matching source and destination filters should permit transfer");
        Require(source["iron_bar"] == 1 && destination["iron_bar"] == 3,
                "permitted transfer should move exactly the requested quantity");

        destination.InputFilter.Clear();
        Require(source.TransferTo(destination, "iron_bar", 1) == 1,
                "manual transfers should not be blocked by automation filters");
    }

    /// <summary>A filter keeps its entries in the order they were named, and naming one it has again leaves it where it is.</summary>
    private static void CheckFilterNamesEachEntryOnceInTheOrderNamed() {
        var chest = new Inventory();
        chest.Filter("stone", false, true); chest.Filter("wood", false, true); chest.Filter("stone", false, true);
        Require(chest.InputFilter.SequenceEqual(["stone", "wood"]), $"naming stone again should leave the filter as stone, wood, not {string.Join(", ", chest.InputFilter)}");
        chest.Filter("stone", false, false);
        Require(chest.InputFilter.SequenceEqual(["wood"]) && !chest.Accepts("stone"), "taking stone off once should leave no stone in the filter");
    }

    private static void CheckItemsRouteToAcceptingStorageBeforeNearerGeneralChest(GameData data) {
        var world = EmptyWorld(data);
        var nearestGeneral = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(11, 10)));
        var explicitWood = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(17, 10)));
        explicitWood.Inventory.Filter("wood", false, true);
        Require(world.DepositFor("wood", new Vector2(10, 10))?.Id == explicitWood.Id,
                "explicitly accepting storage should outrank a nearer general chest");
        Require(world.DepositFor("stone", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "an unrestricted chest should accept items not claimed by a filtered chest");

        nearestGeneral.Inventory.Add("wood", 4);
        nearestGeneral.Inventory.Filter("stone", true, true);
        Require(world.SourceFor("wood", new Vector2(10, 10)) == null,
                "workers must not withdraw items forbidden by the source output filter");
        nearestGeneral.Inventory.Filter("wood", true, true);
        Require(world.SourceFor("wood", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "workers should use a chest once its output filter allows withdrawal");
    }
}
