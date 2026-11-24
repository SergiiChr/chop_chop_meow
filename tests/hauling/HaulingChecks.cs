using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Checks of hauling: what a worker on Haul carries between places, in what order, where a hauler's load is delivered, and what a chest or placed crate offers a hauler.</summary>
public static class HaulingChecks {
    public static void Run(GameData data) {
        Test(CheckHaulerFillsInputsToAFullStack, data);
        Test(CheckHaulerBringsAnInputShortOfABatchBeforeTakingAFullOutput, data);
        Test(CheckHaulerTakesAFullOutputBeforeToppingUpInputs, data);
        Test(CheckHaulerTakesOutOnlyWhatStorageHasRoomFor, data);
        Test(CheckHaulerTakesOutOnlyWhatStorageStillHasRoomForAtPickup, data);
        Test(CheckHaulerTakesOutOnlyTheRejectedFuelStorageHasRoomFor, data);
        Test(CheckHaulerPutsALoadBackWhenNothingTakesItAnyMore, data);
        Test(CheckALoadNothingTakesGoesToTheNearestStorageWhoseOutputFilterLetsItOut, data);
        Test(CheckAChestsHaulerPutsTheLastOfAStackBackIntoTheChest, data);
        Test(CheckHaulerTopsUpTheInputCoveringTheFewestBatches, data);
        Test(CheckHaulerTopsUpInputsBeforeTakingAnOutputThatIsNotFull, data);
        Test(CheckCarriedGoodsGoToAStationThatUsesThem, data);
        Test(CheckALoadOfCoalForSteelIsSharedBetweenInputAndFuel, data);
        Test(CheckSpareSeedWithNoStorageStacksInThePlot, data);
        Test(CheckAHaulGroupBringsTheShortestInputAcrossItsPlaces, data);
        Test(CheckAHaulGroupTakesOutTheBiggestOutputAcrossItsPlaces, data);
        Test(CheckJobsMoveInAndOutOfHaulGroups, data);
        Test(CheckAHaulGroupTakesAChestsCarryBeforeToppingUpAStation, data);
        Test(CheckSaveWithAJobOtherThanAHaulInAGroupIsRefused, data);
        Test(CheckALoadForATradingPostGoesElsewhereOnceItsFilterNoLongerAsks, data);
        Test(CheckAChestsHaulerCarriesCratesTheTradingPostAsksForOneAtATime, data);
        Test(CheckAWorkersHandsHoldOnePackedCrateAndNothingBesideIt, data);
        Test(CheckAChestsHaulerFillsItFromStorageThatDoesNotAskForTheItem, data);
        Test(CheckAChestAskingForCratesSendsThemToTheTradingPostNotAnotherChestAskingForThem, data);
        Test(CheckAChestsHaulerFetchesFinishedGoodsFromAStation, data);
        Test(CheckAChestsHaulerCarriesOutBeforeFilling, data);
        Test(CheckAPlacedCrateTakesLoadsLikeAChestButNoCrate, data);
        Test(CheckAChestsHaulerNeverCarriesWaterOutOfAStationByHand, data);
        Test(CheckAChestsHaulerTakesAStationOnlyTheBottlesItsWaterRoomNeeds, data);
        Test(CheckHaulerStocksBenchForCrafter, data);
        Test(CheckWorkersHaulLeftoversAfterRecipeSwitch, data);
        Test(CheckHaulerFuelsSmeltingWithCoalAndReturnsWood, data);
        Test(CheckTheSecondFurnaceWorkerHaulsRecipeInputsAndStoresSteel, data);
        Test(CheckHaulerStocksPackerAndCarriesCratesToStorageAskingForThem, data);
        Test(CheckHaulerFetchesInputsFromTheStationThatMadeThem, data);
        Test(CheckHaulerPoursWaterAndStoresTheEmptyBottle, data);
        Test(CheckHaulerTakesLeftoverWaterOutInBottles, data);
    }

    private static void CheckHaulerFillsInputsToAFullStack(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "steel_pick");
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10)));
        chest.Inventory.Add("steel_parts", 100); chest.Inventory.Add("wood_parts", 100);
        Staff(world, bench, JobKind.Haul);
        Work(world, 300, () => bench.Inventory["steel_parts"] == 64 && bench.Inventory["wood_parts"] == 64);
        Require(bench.Inventory["steel_parts"] == 64 && bench.Inventory["wood_parts"] == 64,
            $"a hauler should fill each input to a stack of 64: {bench.Inventory["steel_parts"]} steel parts, {bench.Inventory["wood_parts"]} wood parts");
        Work(world, 20);
        Require(chest.Inventory["steel_parts"] == 36 && chest.Inventory["wood_parts"] == 36, "a hauler should bring no more once the inputs are full");
    }

    private static void CheckHaulerBringsAnInputShortOfABatchBeforeTakingAFullOutput(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 64);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10))).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "wood", $"a hauler should first bring wood to a table with none, not carry {hauler.Bag.First()}");
    }

    private static void CheckHaulerTakesAFullOutputBeforeToppingUpInputs(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 64); bench.Inventory.Add("wood", 5);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10))).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "planks", $"a hauler should take out a full stack of planks before topping up wood, not carry {hauler.Bag.First()}");
        Require(hauler.Bag["planks"] == 12, $"a hauler should carry 12 planks at a time, not {hauler.Bag["planks"]}");
    }

    /// <summary>The chest fills while the hauler walks to the table, so the load is sized again when it is picked up.</summary>
    private static void CheckHaulerTakesOutOnlyWhatStorageStillHasRoomForAtPickup(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 64);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10)));
        chest.Inventory.Slots = 1;
        var hauler = Staff(world, bench, JobKind.Haul);
        hauler.Pos = bench.Pos + new Vector2(0, 10);
        Work(world, 1);
        Require(hauler.Bag.Count == 0 && hauler.Pos.DistanceTo(bench.Pos) > 5, "the hauler should still be on the way to the table");
        chest.Inventory.Add("planks", 63);
        Work(world, 30, () => hauler.Bag.Count > 0);
        Require(hauler.Bag["planks"] == 1, $"a hauler should take out only the 1 plank the chest still has room for, not {hauler.Bag["planks"]}");
    }

    private static void CheckHaulerTakesOutOnlyTheRejectedFuelStorageHasRoomFor(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.SetRecipe(furnace, "iron_bar");
        furnace.Burner!.Add("wood", 3);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(24, 20)));
        chest.Inventory.Slots = 1; chest.Inventory.Add("wood", 63);
        var hauler = Staff(world, furnace, JobKind.Haul);
        Work(world, 30, () => hauler.Bag.Count > 0);
        Require(hauler.Bag["wood"] == 1, $"a hauler should take only the 1 wood iron cannot burn that the chest has room for, not {hauler.Bag["wood"]}");
    }

    private static void CheckHaulerTakesOutOnlyWhatStorageHasRoomFor(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 64);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10)));
        chest.Inventory.Slots = 1; chest.Inventory.Add("planks", 61);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "planks" && hauler.Bag["planks"] == 3,
            $"a hauler should take out only the 3 planks the chest has room for, not {hauler.Bag["planks"]}");
    }

    private static void CheckHaulerPutsALoadBackWhenNothingTakesItAnyMore(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 20);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(30, 10)));
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "planks" && bench.Inventory["planks"] == 8, "a hauler should take 12 planks out of the table");
        world.SetFilter(chest, "wood", false, true);
        Work(world, 30, () => hauler.Bag.Count == 0);
        Require(bench.Inventory["planks"] == 20 && chest.Inventory["planks"] == 0,
            $"a hauler whose chest stops taking planks should put them back into the table: the table holds {bench.Inventory["planks"]}, the hands {hauler.Bag["planks"]}");
    }

    private static void CheckALoadNothingTakesGoesToTheNearestStorageWhoseOutputFilterLetsItOut(GameData data) {
        var world = EmptyWorld(data);
        // Neither chest's input filter takes wood: a new chest takes nothing in and lets everything out.
        var stoneOnly = world.Add(ThingKind.Chest, new Vector2(12, 10));
        world.SetFilter(stoneOnly, "stone", true, true);
        var open = world.Add(ThingKind.Chest, new Vector2(16, 10));
        var worker = AddWorker(world, new Vector2(10, 10));
        worker.Bag.Add("wood", 6);
        Work(world, 30, () => worker.Bag.Count == 0);
        Require(open.Inventory["wood"] == 6 && stoneOnly.Inventory.Count == 0,
            $"a load nothing takes should go to the nearest chest whose output filter lets wood out: it holds {open.Inventory["wood"]}, the hands {worker.Bag["wood"]}");
    }

    private static void CheckAChestsHaulerPutsTheLastOfAStackBackIntoTheChest(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(10, 10));
        chest.Inventory.Add("iron_ore", 5);
        var asking = world.Add(ThingKind.Chest, new Vector2(20, 10));
        world.SetFilter(asking, "iron_ore", false, true);
        var hauler = Staff(world, chest, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "iron_ore" && chest.Inventory["iron_ore"] == 0, "a chest's hauler should carry all 5 iron ore out to the chest asking for it");
        world.SetFilter(asking, "iron_ore", false, false);
        Work(world, 30, () => hauler.Bag.Count == 0);
        Require(chest.Inventory["iron_ore"] == 5 && asking.Inventory["iron_ore"] == 0,
            $"once nothing takes the ore, the hauler should put it back into the chest it came from, which lets everything out: it holds {chest.Inventory["iron_ore"]}, the hands {hauler.Bag["iron_ore"]}");
    }

    /// <summary>Four steel parts cover two pickaxes and three wood parts cover three, so steel parts come first though there are more of them.</summary>
    private static void CheckHaulerTopsUpTheInputCoveringTheFewestBatches(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "steel_pick");
        bench.Inventory.Add("steel_parts", 4); bench.Inventory.Add("wood_parts", 3);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10)));
        chest.Inventory.Add("steel_parts", 40); chest.Inventory.Add("wood_parts", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "steel_parts", $"a hauler should top up the input that covers the fewest batches, not carry {hauler.Bag.First()}");
    }

    private static void CheckHaulerTopsUpInputsBeforeTakingAnOutputThatIsNotFull(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 20); bench.Inventory.Add("wood", 10);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10))).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "wood", $"a hauler should top up wood before taking out planks that are not a full stack, not carry {hauler.Bag.First()}");
    }

    /// <summary>The case from a player's save: a load meant for a chest that filled up, next to a table that kneads it.</summary>
    private static void CheckCarriedGoodsGoToAStationThatUsesThem(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(10, 10));
        world.SetRecipe(table, "dough");
        table.Inventory.Add("flour", 5);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10)));
        chest.Inventory.Slots = 1; chest.Inventory.Add("stone", 64);
        var hauler = Staff(world, table, JobKind.Haul);
        hauler.Bag.Add("flour", 1); hauler.Destination = chest.Id;
        Work(world, 30, () => hauler.Bag.Count == 0);
        Require(table.Inventory["flour"] == 6, $"flour a worker holds should go into the table that kneads it when the chest is full: the table holds {table.Inventory["flour"]}");
    }

    /// <summary>A batch of steel takes 2 coal as input and burns 1, so of a hand load of 12, 8 go in as input and 4 as fuel.</summary>
    private static void CheckALoadOfCoalForSteelIsSharedBetweenInputAndFuel(GameData data) {
        var world = EmptyWorld(data);
        var forge = world.Add(ThingKind.Reinforced, new Vector2(10, 10));
        forge.Inventory.Add("iron_bar", 64);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10))).Inventory.Add("coal", 100);
        var hauler = Staff(world, forge, JobKind.Haul);
        FirstCarry(world, hauler);
        Work(world, 30, () => hauler.Bag.Count == 0);
        Require(forge.Inventory["coal"] == 8 && forge.Burner!["coal"] == 4, $"a load of 12 coal should go 8 into the input slot and 4 into the fuel slot: {forge.Inventory["coal"]} and {forge.Burner!["coal"]}");
    }

    /// <summary>A harvest gives 2 seeds; one goes back for the next sowing and, with no storage anywhere, the other stays in the plot's seed slot.</summary>
    private static void CheckSpareSeedWithNoStorageStacksInThePlot(GameData data) {
        var world = EmptyWorld(data);
        var farm = world.Add(ThingKind.Farm, new Vector2(10, 10));
        farm.Output!.Add("wheat_seeds", 2);
        Staff(world, farm, JobKind.Haul);
        Work(world, 30, () => farm.Output["wheat_seeds"] == 0);
        Require(farm.Inventory["wheat_seeds"] == 2 && farm.Output["wheat_seeds"] == 0, $"with no storage, both seeds should end up in the plot's seed slot: {farm.Inventory["wheat_seeds"]} in it, {farm.Output["wheat_seeds"]} left in the output");
    }

    /// <summary>Alone, the first haul in the list is topped up first; in a group the table with no wood at all comes before it.</summary>
    private static void CheckAHaulGroupBringsTheShortestInputAcrossItsPlaces(GameData data) {
        var world = EmptyWorld(data);
        var stocked = Bench(world, new Vector2(10, 10), "planks");
        stocked.Inventory.Add("wood", 5);
        var bare = Bench(world, new Vector2(10, 16), "planks");
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 13))).Inventory.Add("wood", 60);
        var hauler = Staff(world, stocked, JobKind.Haul);
        world.SetDuty(hauler, bare, JobKind.Haul, true);
        FirstCarry(world, hauler);
        Require(hauler.Destination == stocked.Id, "with two haul jobs of their own, a hauler should stock the first in the list first");
        Work(world, 30, () => hauler.Bag.Count == 0);
        GroupHauls(world, hauler, stocked, bare);
        bare.Inventory.Remove("wood", 64);
        FirstCarry(world, hauler);
        Require(hauler.Destination == bare.Id, "in a haul group, a hauler should first bring wood to the table that has none");
    }

    private static void CheckAHaulGroupTakesOutTheBiggestOutputAcrossItsPlaces(GameData data) {
        var world = EmptyWorld(data);
        var few = Bench(world, new Vector2(10, 10), "planks");
        few.Inventory.Add("planks", 20);
        var many = Bench(world, new Vector2(10, 16), "planks");
        many.Inventory.Add("planks", 30);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 13)));
        var hauler = Staff(world, few, JobKind.Haul);
        world.SetDuty(hauler, many, JobKind.Haul, true);
        GroupHauls(world, hauler, few, many);
        Require(FirstCarry(world, hauler) == "planks" && many.Inventory["planks"] == 18 && few.Inventory["planks"] == 20,
            $"in a haul group, a hauler should take planks from the table holding the most: {many.Inventory["planks"]} and {few.Inventory["planks"]} left");
    }

    private static void CheckJobsMoveInAndOutOfHaulGroups(GameData data) {
        var world = EmptyWorld(data);
        var a = Bench(world, new Vector2(10, 10), "planks");
        var b = Bench(world, new Vector2(10, 16), "planks");
        var worker = Staff(world, a, JobKind.Craft, JobKind.Haul);
        world.SetDuty(worker, b, JobKind.Craft, true); world.SetDuty(worker, b, JobKind.Haul, true);
        string A = a.Id.ToString(), B = b.Id.ToString();
        int group = GroupHauls(world, worker, a, b);
        Require(JobsText(worker) == $"craft@{A} craft@{B} group[haul@{A} haul@{B}]", $"hauls moved into a new group should be in it in the order they were added: {JobsText(worker)}");
        world.MoveJob(worker, new JobSpot(2), new JobSpot(0));
        Require(JobsText(worker) == $"group[haul@{A} haul@{B}] craft@{A} craft@{B}", $"a group moved to the top should take its hauls along: {JobsText(worker)}");
        world.MoveJob(worker, new JobSpot(0, 1), new JobSpot(0, 0));
        Require(JobsText(worker) == $"group[haul@{B} haul@{A}] craft@{A} craft@{B}", $"a haul moved up inside its group should come first there: {JobsText(worker)}");
        world.MoveJob(worker, new JobSpot(0, 0), new JobSpot(0, 2));
        Require(JobsText(worker) == $"group[haul@{A} haul@{B}] craft@{A} craft@{B}", $"a haul moved to the end of its group should come last there: {JobsText(worker)}");
        world.MoveJob(worker, new JobSpot(2), new JobSpot(0, 1));
        Require(JobsText(worker) == $"group[haul@{A} haul@{B}] craft@{A} craft@{B}", $"a craft job should not go into a haul group: {JobsText(worker)}");
        world.MoveJob(worker, new JobSpot(0, 1), new JobSpot(3));
        Require(JobsText(worker) == $"group[haul@{A}] craft@{A} craft@{B} haul@{B}", $"a haul moved out of its group should be a job of its own: {JobsText(worker)}");
        world.RemoveHaulGroup(worker, group);
        Require(JobsText(worker) == $"haul@{A} craft@{A} craft@{B} haul@{B}", $"a group taken apart should leave its hauls in its place: {JobsText(worker)}");
        world.AddHaulGroup(worker);
        Require(JobsText(worker) == $"haul@{A} craft@{A} craft@{B} haul@{B} group[]", $"a new group should start empty at the end of the list: {JobsText(worker)}");
    }

    private static void CheckAHaulGroupTakesAChestsCarryBeforeToppingUpAStation(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("wood", 5);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10))).Inventory.Add("wood", 40);
        var asking = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(10, 16)));
        world.SetFilter(asking, "stone", false, true);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 16))).Inventory.Add("stone", 12);
        var hauler = Staff(world, bench, JobKind.Haul);
        world.SetDuty(hauler, asking, JobKind.Haul, true);
        GroupHauls(world, hauler, bench, asking);
        Require(FirstCarry(world, hauler) == "stone", $"in a haul group, a chest's carry should come before topping up the table's wood, not {hauler.Bag.First()}");
    }

    private static void CheckSaveWithAJobOtherThanAHaulInAGroupIsRefused(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        var worker = Staff(world, bench, JobKind.Haul, JobKind.Craft);
        GroupHauls(world, worker, bench);
        string json = world.Serialize();
        EmptyWorld(data).Deserialize(json);
        // Edited by hand, the group holds a craft job.
        ((HaulGroup)worker.Jobs[^1]).Hauls[0].Job = JobKind.Craft;
        bool refused = false;
        try { EmptyWorld(data).Deserialize(world.Serialize()); } catch (System.InvalidOperationException error) { refused = error.Message.Contains("unknown items"); }
        Require(refused, "a save whose haul group holds a job other than a haul should be refused with a clear reason");
    }

    private static void CheckALoadForATradingPostGoesElsewhereOnceItsFilterNoLongerAsks(GameData data) {
        var world = EmptyWorld(data);
        var post = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        string crate = World.CrateOf("berries");
        world.SetFilter(post, crate, false, true);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 10)));
        var worker = AddWorker(world, new Vector2(15, 12));
        worker.Bag.Add(PackCrate(world, ("berries", 12)), 1);
        world.Tick(.1f);
        Require(worker.Destination == post.Id, "a worker should carry a crate of berries to the trading post asking for it");
        world.ClearFilters(post);
        Work(world, 30, () => worker.Bag.Count == 0);
        Require(post.Inventory.Count == 0 && chest.Inventory.Count == 1, "a crate on its way to a post whose filter no longer asks for it should go to storage");
    }

    /// <summary>The case from a player's save: crates of a good in a chest with no filter, and a trading post whose filter asks for them.</summary>
    private static void CheckAChestsHaulerCarriesCratesTheTradingPostAsksForOneAtATime(GameData data) {
        var world = EmptyWorld(data);
        var chest = StoreCrates(world, new Vector2(10, 10), 3)[0];
        var post = world.Add(ThingKind.Signpost, new Vector2(20, 10));
        world.SetFilter(post, World.CrateOf("berries"), false, true);
        var hauler = Staff(world, chest, JobKind.Haul);
        int most = 0;
        Work(world, 120, () => { most = Math.Max(most, hauler.Bag.Count); return post.Inventory.Count == 3; });
        Require(post.Inventory.Count == 3 && chest.Inventory.Count == 0,
            $"a chest's hauler should carry its three crates of berries to the trading post asking for them: the post holds {post.Inventory.Count}, the chest {chest.Inventory.Count}");
        Require(most == 1, $"a hauler should carry one packed crate at a time, not {most}");
    }

    private static void CheckAWorkersHandsHoldOnePackedCrateAndNothingBesideIt(GameData data) {
        var world = EmptyWorld(data);
        var worker = AddWorker(world, world.Player + Vector2.One);
        string first = PackCrate(world, ("berries", 12)), second = PackCrate(world, ("berries", 12));
        Give(world, "stone", 5);
        Require(world.Transfer(world.Backpack, worker.Bag, "stone", 5) == 5 && world.Transfer(world.Backpack, worker.Bag, first, 1) == 0,
            "a worker holding stone should take no packed crate beside it");
        world.Transfer(worker.Bag, world.Backpack, "stone", 5);
        Require(world.Transfer(world.Backpack, worker.Bag, first, 1) == 1, "a worker with empty hands should take a packed crate");
        Require(world.Transfer(world.Backpack, worker.Bag, second, 1) == 0 && world.Transfer(world.Backpack, worker.Bag, "stone", 5) == 0,
            "a worker holding a packed crate should take neither a second crate nor stone beside it");
    }

    private static void CheckAChestsHaulerFillsItFromStorageThatDoesNotAskForTheItem(GameData data) {
        var world = EmptyWorld(data);
        var asking = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(10, 10)));
        world.SetFilter(asking, "stone", false, true);
        var alsoAsking = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(12, 12)));
        alsoAsking.Inventory.Add("stone", 20);
        world.SetFilter(alsoAsking, "stone", false, true);
        var open = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(18, 10)));
        open.Inventory.Add("stone", 20);
        Staff(world, asking, JobKind.Haul);
        Work(world, 90, () => asking.Inventory["stone"] == 20);
        Work(world, 20);
        Require(asking.Inventory["stone"] == 20 && open.Inventory["stone"] == 0 && alsoAsking.Inventory["stone"] == 20,
            $"a chest's hauler should fill it from the chest with no filter and leave the nearer chest asking for stone alone: {asking.Inventory["stone"]} brought, {alsoAsking.Inventory["stone"]} left there");
    }

    private static void CheckAChestAskingForCratesSendsThemToTheTradingPostNotAnotherChestAskingForThem(GameData data) {
        var world = EmptyWorld(data);
        string berries = World.CrateOf("berries");
        var chest = StoreCrates(world, new Vector2(10, 10), 1)[0];
        world.SetFilter(chest, berries, false, true);
        var nearer = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10)));
        world.SetFilter(nearer, berries, false, true);
        var post = world.Add(ThingKind.Signpost, new Vector2(24, 10));
        world.SetFilter(post, berries, false, true);
        Staff(world, chest, JobKind.Haul);
        Work(world, 90, () => post.Inventory.Count == 1);
        Require(post.Inventory.Count == 1 && nearer.Inventory.Count == 0,
            "a chest asking for crates of berries should send its crate to the trading post asking for it, not to a nearer chest that asks too");
    }

    private static void CheckAChestsHaulerFetchesFinishedGoodsFromAStation(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 20);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(14, 10)));
        world.SetFilter(chest, "planks", false, true);
        Staff(world, chest, JobKind.Haul);
        Work(world, 60, () => chest.Inventory["planks"] == 20);
        Require(chest.Inventory["planks"] == 20 && bench.Inventory["planks"] == 0, $"a chest's hauler should fetch the table's planks for its filter: {chest.Inventory["planks"]} brought");
    }

    private static void CheckAChestsHaulerCarriesOutBeforeFilling(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(10, 10)));
        chest.Inventory.Add("wood", 12);
        world.SetFilter(chest, "stone", false, true);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 10))).Inventory.Add("stone", 12);
        Bench(world, new Vector2(16, 10), "planks");
        var hauler = Staff(world, chest, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "wood", $"a chest's hauler should carry wood out to the table asking for it before fetching stone, not carry {hauler.Bag.First()}");
    }

    private static void CheckAPlacedCrateTakesLoadsLikeAChestButNoCrate(GameData data) {
        var world = EmptyWorld(data);
        var crate = TakeEverything(world, world.Add(ThingKind.Crate, new Vector2(14, 10)));
        var worker = AddWorker(world, new Vector2(10, 10));
        worker.Bag.Add("stone", 6);
        Work(world, 30, () => worker.Bag.Count == 0);
        Require(crate.Inventory["stone"] == 6, "a worker should store a load in a placed crate as in a chest");
        world.SetFilter(crate, World.CrateOf("berries"), false, true);
        Require(crate.Inventory.InputFilter.SequenceEqual(new[] { Inventory.Everything }), "a placed crate's filter should not name a crate");
        string packed = PackCrate(world, ("berries", 12));
        Warp(world, worker.Pos + Vector2.One);
        Require(world.Transfer(world.Backpack, worker.Bag, packed, 1) == 1, "a player should hand a worker a packed crate");
        Work(world, 10);
        Require(crate.Inventory.Count == 6 && worker.Bag.Count == 1, "a worker should never put a packed crate into a placed crate");
    }

    private static void CheckAChestsHaulerNeverCarriesWaterOutOfAStationByHand(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(10, 10));
        world.SetRecipe(table, "crushed_berries");
        table.Inventory.Add(Water.Liquid, 5);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(14, 10)));
        world.SetFilter(chest, Water.Liquid, false, true);
        var hauler = Staff(world, chest, JobKind.Haul);
        Work(world, 30);
        Require(table.Inventory[Water.Liquid] == 5 && hauler.Bag[Water.Liquid] == 0 && chest.Inventory[Water.Liquid] == 0,
            $"a chest's hauler should leave the water a station holds, which leaves it only in vessels: {chest.Inventory[Water.Liquid]} liters in the chest");
    }

    private static void CheckAChestsHaulerTakesAStationOnlyTheBottlesItsWaterRoomNeeds(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(10, 10));
        world.SetRecipe(table, "meat_stew");
        // A stack of 64 liters with 58 in it has room for 6: two full bottles of 3 liters.
        table.Inventory.Add(Water.Liquid, 58);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(14, 10)));
        chest.Inventory.Add(Water.Full, 5);
        var hauler = Staff(world, chest, JobKind.Haul);
        Work(world, 30, () => hauler.Bag.Count > 0);
        Require(hauler.Bag[Water.Full] == 2, $"a chest's hauler should take the table two full bottles for its 6 liters of room, not {hauler.Bag[Water.Full]}");
    }

    /// <summary>A bench hauler only stocks it; the work itself waits for a crafter.</summary>
    private static void CheckHaulerStocksBenchForCrafter(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        chest.Inventory.Add("wood", 20);
        var bench = world.Add(ThingKind.Bench, new Vector2(28, 24));
        bench.Tools!.Add("stone_chisel", 1);
        world.SetRecipe(bench, "planks");
        var hauler = AddWorker(world, new Vector2(27, 23));
        world.SetDuty(hauler, bench, JobKind.Haul, true);
        SafeTick(world, 20);
        Require(bench.Inventory["wood"] > 0, "a bench hauler should stock the bench with wood");
        Require(bench.Inventory["planks"] == 0, "a bench hauler should not make planks");
        Require(!bench.Processing, "a bench hauler should not start the bench");
        var crafter = AddWorker(world, new Vector2(28, 25));
        world.SetDuty(crafter, bench, JobKind.Craft, true);
        SafeTick(world, 20);
        Require(bench.Inventory["planks"] + chest.Inventory["planks"] > 0, "a crafter should work the stocked bench");
    }

    private static void CheckWorkersHaulLeftoversAfterRecipeSwitch(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(10, 10));
        world.Player = furnace.Pos + Vector2.One;
        // A bar left in the slots of a furnace that is on tin now.
        world.SetRecipe(furnace, "tin_bar");
        furnace.Inventory.Add("iron_bar", 1);
        world.Collect(furnace);
        Require(world.Backpack["iron_bar"] == 1 && furnace.Inventory["iron_bar"] == 0,
                "collecting should also return goods made by a previous recipe");

        furnace.Inventory.Add("iron_bar", 1);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(14, 10)));
        var worker = AddWorker(world, new Vector2(11, 11));
        world.Player = new Vector2(30, 30);
        world.Assign(worker, furnace);
        SafeTick(world, 10);
        Require(chest.Inventory["iron_bar"] == 1, "station workers should haul leftovers from a previous recipe to storage");
    }

    /// <summary>A stone furnace smelting iron with wood in its fuel slot and only coal in storage: the hauler swaps the fuel and the iron gets smelted.</summary>
    private static void CheckHaulerFuelsSmeltingWithCoalAndReturnsWood(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(20, 20));
        world.SetRecipe(furnace, "iron_bar");
        furnace.Burner!.Add("wood", 3);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(24, 20)));
        chest.Inventory.Add("iron_ore", 4); chest.Inventory.Add("coal", 4);
        world.Assign(AddWorker(world, furnace.Pos), furnace);
        SafeTick(world, 60);
        Require(chest.Inventory["wood"] == 3, "the hauler should carry the wood iron cannot burn back to storage");
        Require(furnace.Inventory["iron_bar"] + chest.Inventory["iron_bar"] >= 1 && chest.Inventory["coal"] < 4,
            "the hauler should fuel the furnace with coal from storage and the iron should smelt");

        // A recipe that also burns wood, with an empty fuel slot and no wood anywhere, still gets coal.
        var grill = world.Add(ThingKind.Furnace, new Vector2(20, 26));
        world.SetRecipe(grill, "burnt_steak");
        chest.Inventory.Add("raw_meat", 2);
        world.Assign(AddWorker(world, grill.Pos), grill);
        SafeTick(world, 60);
        Require(grill.Inventory["burnt_steak"] + chest.Inventory["burnt_steak"] == 2, "a hauler should fuel an empty fuel slot with coal when storage has no wood");
    }

    private static void CheckTheSecondFurnaceWorkerHaulsRecipeInputsAndStoresSteel(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(23, 20)));
        chest.Inventory.Add("iron_bar", 4);
        chest.Inventory.Add("coal", 6);
        var bellowsWorker = AddWorker(world, furnace.Pos + World.BellowsStand);
        var hauler = AddWorker(world, chest.Pos);
        world.Assign(bellowsWorker, furnace);
        world.Assign(hauler, furnace);

        bool sawHauling = false;
        for (int i = 0; i < 600 && furnace.Inventory["steel_bar"] + chest.Inventory["steel_bar"] == 0; i++) {
            SafeTick(world, Step);
            sawHauling |= hauler.Destination == furnace.Id || hauler.Bag.Count > 0;
        }

        Require(bellowsWorker.Assignment == furnace.Id && furnace.Heat > 0,
                "the first of two reinforced-furnace workers should stay at the bellows while the second hauls");
        Require(sawHauling && furnace.Inventory["steel_bar"] + chest.Inventory["steel_bar"] > 0,
                "the second furnace worker should haul recipe inputs, collect actual steel, and return it to storage");
        Require(chest.Inventory["iron_bar"] < 4 && chest.Inventory["coal"] < 6,
                "hauling should remove ingredients from the source chest");
    }

    /// <summary>A hauler stocks goods and crates from storage, carries the full crate to the nearest storage asking for crates of that good, and clears out the old good when the station changes.</summary>
    private static void CheckHaulerStocksPackerAndCarriesCratesToStorageAskingForThem(GameData data) {
        var world = EmptyWorld(data);
        var packer = world.Add(ThingKind.Packer, new Vector2(20, 20));
        world.Player = packer.Pos + new Vector2(1, 0);
        world.SetPacking(packer, "bread");
        packer.Inventory.Add("bread", 13); packer.Inventory.Add("crate", 2);
        SafeTick(world, data.PackSeconds * data.CrateCapacity + 1);
        string crate = packer.Output!.First()!;
        world.Player = new Vector2(40, 40);
        var general = TakeEverything(world, world.Add(ThingKind.Chest, packer.Pos + new Vector2(2, 0)));
        var crates = TakeEverything(world, world.Add(ThingKind.Chest, packer.Pos + new Vector2(6, 0)));
        crates.Inventory.Filter(World.CrateOf("bread"), false, true);
        general.Inventory.Add("bread", 30); general.Inventory.Add("crate", 4);
        var worker = AddWorker(world, packer.Pos + new Vector2(0, 1));
        worker.Wage = 18;
        world.Assign(worker, packer);
        for (float waited = 0; waited < 40 && crates.Inventory[crate] == 0; waited += .1f) world.Tick(.1f);
        Require(crates.Inventory[crate] == 1 && general.Inventory[crate] == 0, "the hauler should carry the full crate past general storage to the chest asking for crates of bread");
        bool Packing() => packer.Output.Count == 1 && world.Packed(packer.Output.First()!)!["bread"] > 1;
        for (float waited = 0; waited < 40 && !Packing(); waited += .1f) world.Tick(.1f);
        Require(Packing(), "the worker should restock bread and pack the next crate");
        // A worker who packs and hauls keeps packing while there is something to pack, so empty crates come once the crate in hand is full.
        for (float waited = 0; waited < 60 && packer.Inventory["crate"] == 0; waited += .1f) world.Tick(.1f);
        Require(packer.Inventory["crate"] >= 1, "the worker should restock empty crates once the crate in hand is packed");

        // Changing the good leaves the old goods for the hauler to carry out.
        world.SetPacking(packer, "berries");
        SafeTick(world, 20);
        Require(packer.Inventory["bread"] == 0, "after switching to berries, the hauler should carry the leftover bread out");
    }

    /// <summary>A bench's hauler takes steel parts straight from the anvil that hammered them.</summary>
    private static void CheckHaulerFetchesInputsFromTheStationThatMadeThem(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, new Vector2(20, 20));
        anvil.Inventory.Add("steel_parts", 6);
        var bench = world.Add(ThingKind.Bench, new Vector2(26, 20));
        world.SetRecipe(bench, "steel_hammer");
        var worker = AddWorker(world, bench.Pos + new Vector2(0, 1));
        worker.Wage = 18;
        world.SetDuty(worker, bench, JobKind.Haul, true);
        Work(world, 30, () => bench.Inventory["steel_parts"] > 0);
        Require(bench.Inventory["steel_parts"] == 6 && anvil.Inventory["steel_parts"] == 0, "the bench's hauler should fetch the steel parts from the anvil's finished goods");
    }

    private static void CheckHaulerPoursWaterAndStoresTheEmptyBottle(GameData data) {
        var world = new World(data, false);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        chest.Inventory.Add(Water.Full, 1); chest.Inventory.Add("flour", 4);
        var table = world.Add(ThingKind.Cooktable, new Vector2(26, 22));
        world.SetRecipe(table, "dough");
        var hauler = AddWorker(world, new Vector2(24, 21));
        world.SetDuty(hauler, table, JobKind.Haul, true); world.SetDuty(hauler, table, JobKind.Craft, true);
        SafeTick(world, 40);
        Require(chest.Inventory["dough"] == 3 && table.Inventory[Water.Full] + table.Inventory[Water.Empty] == 0,
            "a hauler must pour a full bottle into the table for its dough, leaving no bottle there");
        Require(chest.Inventory[Water.Empty] == 1 && hauler.Bag.Count == 0, "a hauler must store the emptied bottle again");
    }

    private static void CheckHaulerTakesLeftoverWaterOutInBottles(GameData data) {
        var world = new World(data, false);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        chest.Inventory.Add(Water.Empty, 1);
        var table = world.Add(ThingKind.Cooktable, new Vector2(26, 22));
        world.SetRecipe(table, "berry_jam"); table.Inventory.Add(Water.Liquid, 1);
        var hauler = AddWorker(world, new Vector2(24, 21));
        world.SetDuty(hauler, table, JobKind.Haul, true);
        SafeTick(world, 40);
        Require(table.Inventory[Water.Liquid] == 0, "a hauler must clear water the table's recipe no longer takes");
        Require(chest.Inventory[Water.OneLiter] == 1 && chest.Inventory[Water.Empty] == 0 && hauler.Bag.Count == 0,
            "the leftover liter must go back to storage in the empty bottle the hauler fetched");
    }
}
