using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

/// <summary>Checks for how a worker carries, picks the next job from their list and hauls for a station.</summary>
public static class WorkerJobChecks {
    public static void Run(GameData data) {
        CheckWorkerGathersFourPiecesACycleUntilTwelveAreInHand(data);
        CheckGathererGoesOnToTheNextSpotUntilTheirHandsAreFull(data);
        CheckWorkerKeepsToTheFirstJobWhileItCanBeDone(data);
        CheckWorkerGoesBackToTheFirstJobAfterOneDeliveryOfTheNext(data);
        CheckHaulerFillsInputsToAFullStack(data);
        CheckHaulerBringsAnInputShortOfABatchBeforeTakingAFullOutput(data);
        CheckHaulerTakesAFullOutputBeforeToppingUpInputs(data);
        CheckHaulerTopsUpTheInputCoveringTheFewestBatches(data);
        CheckHaulerTopsUpInputsBeforeTakingAnOutputThatIsNotFull(data);
        CheckCarriedGoodsGoToAStationThatUsesThem(data);
        CheckCarriedGoodsGoToTheNearestOfAStationAndFilteredStorage(data);
        CheckFuelGoesStraightIntoAFuelSlotThatBurnsIt(data);
        CheckBellowsWorkerStopsWhenThereIsNothingToSmelt(data);
        CheckWorkerWithNothingToDoWaitsAtTheTradingPost(data);
        CheckGathererWithNoRoomToPutBackTheWrongToolKeepsTheirSpot(data);
        CheckWorkerPutsBackAToolNoneOfTheirJobsUses(data);
        CheckALoadOfCoalForSteelIsSharedBetweenInputAndFuel(data);
        CheckSpareSeedWithNoStorageStacksInThePlot(data);
        CheckACrafterLowerInTheListGivesWayAfterOneBatch(data);
        CheckAWorkerBesideAStationWalksRoundItToWorkIt(data);
        CheckAWorkerWhoseWorkSpotIsBlockedWorksFromAsNearAsTheyGet(data);
        CheckASaveKeepsTheJobOrderGroupsAndTool(data);
        CheckAToolHeldFromAWorkerIsLetGoOnLoad(data);
        CheckALoadForATradingPostGoesElsewhereOnceItsFilterNoLongerAsks(data);
        CheckAWorkerDeliversWhatAPlayerDoesNotHold(data);
        CheckAChestsHaulerCarriesCratesTheTradingPostAsksForOneAtATime(data);
        CheckAWorkersHandsHoldOnePackedCrateAndNothingBesideIt(data);
        CheckAChestsHaulerFillsItFromStorageThatDoesNotAskForTheItem(data);
        CheckAChestAskingForCratesSendsThemToTheTradingPostNotAnotherChestAskingForThem(data);
        CheckAChestsHaulerFetchesFinishedGoodsFromAStation(data);
        CheckAChestsHaulerCarriesOutBeforeFilling(data);
        CheckAHaulGroupTakesAChestsCarryBeforeToppingUpAStation(data);
        CheckAPlacedCrateTakesLoadsLikeAChestButNoCrate(data);
        CheckAPackedCrateKeepsItsFiltersWhenPutDownAgain(data);
        CheckAChestsHaulerNeverCarriesWaterOutOfAStationByHand(data);
        CheckAChestsHaulerTakesAStationOnlyTheBottlesItsWaterRoomNeeds(data);
        CheckSaveWithAJobOtherThanAHaulInAGroupIsRefused(data);
        CheckAJobMovedUpComesFirst(data);
        CheckAHaulGroupBringsTheShortestInputAcrossItsPlaces(data);
        CheckAHaulGroupTakesOutTheBiggestOutputAcrossItsPlaces(data);
        CheckJobsMoveInAndOutOfHaulGroups(data);
        CheckPlayerTakesWhatAWorkerCarries(data);
        CheckPlayerGivesAWorkerALoadToCarryOff(data);
        CheckPlayerTakesAndGivesAWorkersTool(data);
        CheckAWorkerKeepsALoadAPlayerHoldsUntilTheyLetGo(data);
    }

    /// <summary>A steel axe makes a cycle 2 s / 1.8, and a cycle yields 4 pieces as it does for the player.</summary>
    private static void CheckWorkerGathersFourPiecesACycleUntilTwelveAreInHand(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4000);
        var chest = world.Add(ThingKind.Chest, new Vector2(18, 10));
        var cutter = Staff(world, tree, JobKind.Gather);
        cutter.Tool = "steel_axe";
        Work(world, 2);
        Require(cutter.Bag["wood"] == 4, $"a woodcutter with a steel axe should hold 4 wood after one cycle of 1.1 s, not {cutter.Bag["wood"]}");
        Work(world, 1.75f);
        Require(cutter.Bag["wood"] == 12, $"a woodcutter should hold 12 wood after three cycles, not {cutter.Bag["wood"]}");
        Work(world, 60, () => chest.Inventory["wood"] > 0);
        Require(chest.Inventory["wood"] == 12 && tree.Remaining == 3988, $"a woodcutter should carry 12 wood at a time: {chest.Inventory["wood"]} stored, {4000 - tree.Remaining} cut");
    }

    private static void CheckGathererGoesOnToTheNextSpotUntilTheirHandsAreFull(GameData data) {
        var world = EmptyWorld(data);
        var first = world.Add(ThingKind.LooseCoal, new Vector2(10, 10), 1);
        world.Add(ThingKind.LooseCoal, new Vector2(12, 10), 1);
        world.Add(ThingKind.LooseCoal, new Vector2(14, 10), 1);
        var chest = world.Add(ThingKind.Chest, new Vector2(18, 10));
        world.Player = new Vector2(12, 12); world.Look();
        Staff(world, first, JobKind.Gather);
        Work(world, 60, () => chest.Inventory["coal"] > 0);
        Require(chest.Inventory["coal"] == 3, $"a picker should bring all three known pieces of coal in one load, not {chest.Inventory["coal"]}");
    }

    private static void CheckWorkerKeepsToTheFirstJobWhileItCanBeDone(GameData data) {
        var world = EmptyWorld(data);
        var berries = world.Add(ThingKind.Berries, new Vector2(10, 10), 400);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 14), 400);
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 12));
        var picker = Staff(world, berries, JobKind.Gather);
        world.SetDuty(picker, leaves, JobKind.Gather, true);
        Work(world, 90);
        Require(chest.Inventory["berries"] >= 24 && chest.Inventory["leaves"] == 0,
            $"a worker should pick berries, their first job, for as long as there are some: {chest.Inventory["berries"]} berries and {chest.Inventory["leaves"]} leaves stored");
    }

    /// <summary>With no storage at all, the wood a crafter cuts goes into the table, and they saw it all before cutting more.</summary>
    private static void CheckWorkerGoesBackToTheFirstJobAfterOneDeliveryOfTheNext(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks", "steel_chisel");
        var tree = world.Add(ThingKind.Tree, new Vector2(14, 10), 4000);
        var crafter = Staff(world, bench, JobKind.Craft);
        world.SetDuty(crafter, tree, JobKind.Gather, true);
        world.Add(ThingKind.Toolstation, new Vector2(12, 12)).Inventory.Add("steel_axe", 1);
        Work(world, 200, () => bench.Inventory["planks"] >= 8);
        Require(bench.Inventory["planks"] >= 8, $"a crafter who also cuts wood should saw planks from the wood they brought: {bench.Inventory["planks"]} planks");
        Require(tree.Remaining == 3988, $"a crafter should go back to sawing after one load of wood, not cut {4000 - tree.Remaining}");
    }

    private static void CheckHaulerFillsInputsToAFullStack(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "steel_pick");
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 10));
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
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "wood", $"a hauler should first bring wood to a table with none, not carry {hauler.Bag.First()}");
    }

    private static void CheckHaulerTakesAFullOutputBeforeToppingUpInputs(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 64); bench.Inventory.Add("wood", 5);
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "planks", $"a hauler should take out a full stack of planks before topping up wood, not carry {hauler.Bag.First()}");
        Require(hauler.Bag["planks"] == 12, $"a hauler should carry 12 planks at a time, not {hauler.Bag["planks"]}");
    }

    /// <summary>Four steel parts cover two pickaxes and three wood parts cover three, so steel parts come first though there are more of them.</summary>
    private static void CheckHaulerTopsUpTheInputCoveringTheFewestBatches(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "steel_pick");
        bench.Inventory.Add("steel_parts", 4); bench.Inventory.Add("wood_parts", 3);
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 10));
        chest.Inventory.Add("steel_parts", 40); chest.Inventory.Add("wood_parts", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "steel_parts", $"a hauler should top up the input that covers the fewest batches, not carry {hauler.Bag.First()}");
    }

    private static void CheckHaulerTopsUpInputsBeforeTakingAnOutputThatIsNotFull(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 20); bench.Inventory.Add("wood", 10);
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "wood", $"a hauler should top up wood before taking out planks that are not a full stack, not carry {hauler.Bag.First()}");
    }

    /// <summary>The case from a player's save: a load meant for a chest that filled up, next to a table that kneads it.</summary>
    private static void CheckCarriedGoodsGoToAStationThatUsesThem(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(10, 10));
        world.SetRecipe(table, "dough");
        table.Inventory.Add("flour", 5);
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 10));
        chest.Inventory.Slots = 1; chest.Inventory.Add("stone", 64);
        var hauler = Staff(world, table, JobKind.Haul);
        hauler.Bag.Add("flour", 1); hauler.Destination = chest.Id;
        Work(world, 30, () => hauler.Bag.Count == 0);
        Require(table.Inventory["flour"] == 6, $"flour a worker holds should go into the table that kneads it when the chest is full: the table holds {table.Inventory["flour"]}");
    }

    private static void CheckCarriedGoodsGoToTheNearestOfAStationAndFilteredStorage(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4000);
        var near = Bench(world, new Vector2(12, 10), "planks");
        var asking = world.Add(ThingKind.Chest, new Vector2(20, 10));
        asking.Inventory.InputFilter.Add("wood");
        var open = world.Add(ThingKind.Chest, new Vector2(11, 12));
        var cutter = Staff(world, tree, JobKind.Gather);
        cutter.Tool = "steel_axe";
        Work(world, 60, () => near.Inventory["wood"] > 0);
        Require(near.Inventory["wood"] == 12 && open.Inventory["wood"] == 0, "wood should go to the table that saws it before a nearer chest with no filter");
        near.Pos = new Vector2(40, 10);
        Work(world, 60, () => asking.Inventory["wood"] > 0);
        Require(asking.Inventory["wood"] == 12 && near.Inventory["wood"] == 12, "wood should go to a chest asking for it when that is nearer than the table");
    }

    private static void CheckFuelGoesStraightIntoAFuelSlotThatBurnsIt(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 4000);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(13, 10));
        world.SetRecipe(furnace, "bread");
        world.Add(ThingKind.Chest, new Vector2(11, 12));
        var cutter = Staff(world, tree, JobKind.Gather);
        cutter.Tool = "steel_axe";
        Work(world, 60, () => furnace.Burner!["wood"] > 0);
        Require(furnace.Burner!["wood"] == 12, $"wood should go into the fuel slot of a furnace that burns it: the slot holds {furnace.Burner["wood"]}");
    }

    private static void CheckBellowsWorkerStopsWhenThereIsNothingToSmelt(GameData data) {
        var world = EmptyWorld(data);
        var forge = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        var berries = world.Add(ThingKind.Berries, new Vector2(20, 25), 400);
        var chest = world.Add(ThingKind.Chest, new Vector2(24, 22));
        chest.Inventory.InputFilter.Add("berries"); chest.Inventory.InputFilter.Add("steel_bar");
        var pumper = Staff(world, forge, JobKind.Bellows);
        world.SetDuty(pumper, berries, JobKind.Gather, true);
        Work(world, 60, () => chest.Inventory["berries"] > 0);
        Require(chest.Inventory["berries"] > 0 && forge.Heat == 0, $"with nothing to smelt a bellows worker should pick berries, their next job: heat {forge.Heat}");
        forge.Inventory.Add("iron_bar", 2); forge.Inventory.Add("coal", 2); forge.Burner!.Add("coal", 1);
        Work(world, 120, () => forge.Inventory["steel_bar"] > 0);
        Require(forge.Inventory["steel_bar"] == 2, "a bellows worker should pump again once the furnace holds a batch, until the steel is made");
        float berriesBefore = chest.Inventory["berries"];
        Work(world, 60, () => chest.Inventory["berries"] > berriesBefore);
        Require(chest.Inventory["berries"] > berriesBefore, "a bellows worker should go back to picking berries once the batch is done");
    }

    private static void CheckWorkerWithNothingToDoWaitsAtTheTradingPost(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks", "steel_chisel");
        var post = world.Add(ThingKind.Signpost, new Vector2(24, 10));
        var crafter = Staff(world, bench, JobKind.Craft);
        Work(world, 60, () => crafter.Pos.DistanceTo(post.Pos) < 3);
        Require(crafter.Pos.DistanceTo(post.Pos) < 3, $"a worker whose jobs cannot be done should wait at the trading post, not {crafter.Pos.DistanceTo(post.Pos):0} m from it");
        Require(crafter.Status.Contains("wood", System.StringComparison.OrdinalIgnoreCase), $"a waiting worker's status should say what their first job lacks, not \"{crafter.Status}\"");
    }

    /// <summary>The rack is full and gives out an axe, so the next tree looks workable; the shovel in hand still has nowhere to go.</summary>
    private static void CheckGathererWithNoRoomToPutBackTheWrongToolKeepsTheirSpot(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 400);
        var next = world.Add(ThingKind.Tree, new Vector2(12, 10), 400);
        world.Add(ThingKind.Chest, new Vector2(14, 12));
        var rack = world.Add(ThingKind.Toolstation, new Vector2(11, 13));
        foreach (string tool in new[] { "steel_axe", "steel_pick", "steel_hammer", "rake" }) rack.Inventory.Add(tool, 1);
        world.Player = new Vector2(11, 12); world.Look();
        var cutter = Staff(world, tree, JobKind.Gather);
        cutter.Tool = "steel_shovel";
        Work(world, 3 * Step);
        Require(World.HasDuty(cutter, tree.Id, JobKind.Gather) && !World.HasDuty(cutter, next.Id),
            "a woodcutter who cannot put back the wrong tool should keep their tree, not move the job to the next one");
        Require(cutter.Tool == "steel_shovel" && cutter.Status == "No room to put back the steel shovel", $"a woodcutter who cannot put back the wrong tool should say so, not \"{cutter.Status}\"");
    }

    private static void CheckAJobMovedUpComesFirst(GameData data) {
        var world = EmptyWorld(data);
        var berries = world.Add(ThingKind.Berries, new Vector2(10, 10), 400);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 14), 400);
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 12));
        var picker = Staff(world, berries, JobKind.Gather);
        world.SetDuty(picker, leaves, JobKind.Gather, true);
        world.MoveJob(picker, new JobSpot(1), new JobSpot(0));
        Work(world, 90);
        Require(chest.Inventory["leaves"] >= 24 && chest.Inventory["berries"] == 0,
            $"a job dragged to the top should be the one a worker does: {chest.Inventory["leaves"]} leaves and {chest.Inventory["berries"]} berries stored");
    }

    /// <summary>Alone, the first haul in the list is topped up first; in a group the table with no wood at all comes before it.</summary>
    private static void CheckAHaulGroupBringsTheShortestInputAcrossItsPlaces(GameData data) {
        var world = EmptyWorld(data);
        var stocked = Bench(world, new Vector2(10, 10), "planks");
        stocked.Inventory.Add("wood", 5);
        var bare = Bench(world, new Vector2(10, 16), "planks");
        world.Add(ThingKind.Chest, new Vector2(13, 13)).Inventory.Add("wood", 60);
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
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 13));
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

    private static void CheckPlayerTakesWhatAWorkerCarries(GameData data) {
        var world = EmptyWorld(data);
        var said = new System.Collections.Generic.List<string>();
        world.Notify += said.Add;
        var worker = AddWorker(world, world.Player + new Vector2(40, 0));
        worker.Bag.Add("wood", 12);
        world.Hold(worker.Bag, 0, 12);
        Require(world.Held == null && said.Contains("Walk closer first."), "a player far from a worker should not pick up what they carry, and be told to walk closer");
        world.Player = worker.Pos + Vector2.One;
        world.Hold(worker.Bag, 0, 12);
        Require(world.Held is { Item: "wood", Count: 12 } && worker.Bag["wood"] == 12, "a player beside a worker should pick up their load, which stays in the worker's hands while held");
        world.FromHeldStack(() => world.Transfer(worker.Bag, world.Backpack, "wood", 12));
        Require(world.Backpack["wood"] == 12 && worker.Bag.Count == 0 && world.Held == null, "putting the held load in the backpack should take it out of the worker's hands");
    }

    private static void CheckPlayerGivesAWorkerALoadToCarryOff(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        var worker = AddWorker(world, chest.Pos + new Vector2(6, 0));
        world.Player = worker.Pos + Vector2.One;
        Give(world, "stone", 20);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "stone"), 20);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.Bag, "stone", 20));
        Require(worker.Bag["stone"] == 12 && world.Backpack["stone"] == 8 && world.Held is { Count: 8 }, $"a worker should take 12 of a held stack of 20 and leave 8 held: {worker.Bag["stone"]} taken");
        world.Release();
        Work(world, 30, () => chest.Inventory["stone"] > 0);
        Require(chest.Inventory["stone"] == 12 && worker.Bag.Count == 0, "a worker with no jobs should carry what they are given to storage");
    }

    private static void CheckPlayerTakesAndGivesAWorkersTool(GameData data) {
        var world = EmptyWorld(data);
        var said = new System.Collections.Generic.List<string>();
        world.Notify += said.Add;
        var worker = AddWorker(world, new Vector2(20, 20));
        worker.Tool = "steel_axe";
        world.Player = worker.Pos + Vector2.One;
        world.Hold(worker.ToolSlot, 0, 1);
        world.FromHeldStack(() => world.Transfer(worker.ToolSlot, world.Backpack, "steel_axe", 1));
        Require(worker.Tool == "" && world.Backpack["steel_axe"] == 1, "a player should take the tool out of a worker's tool slot");
        Give(world, "wood", 1); Give(world, "steel_pick", 1);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "wood"), 1);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.ToolSlot, "wood", 1));
        Require(worker.Tool == "" && said.Contains("A worker's tool slot only takes tools."), "a worker's tool slot should refuse wood and say why");
        world.Hold(world.Backpack, SlotOf(world.Backpack, "steel_pick"), 1);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.ToolSlot, "steel_pick", 1));
        Require(worker.Tool == "steel_pick" && world.Backpack["steel_pick"] == 0, "a player should put a tool into a worker's empty tool slot");
        world.Hold(world.Backpack, SlotOf(world.Backpack, "steel_axe"), 1);
        world.FromHeldStack(() => world.Transfer(world.Backpack, worker.ToolSlot, "steel_axe", 1));
        Require(worker.Tool == "steel_pick" && world.Backpack["steel_axe"] == 1, "a worker's tool slot should hold one tool");
    }

    private static void CheckAWorkerKeepsALoadAPlayerHoldsUntilTheyLetGo(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        var worker = AddWorker(world, chest.Pos + new Vector2(1, 0));
        worker.Bag.Add("wood", 12);
        world.Player = worker.Pos + Vector2.One;
        world.Hold(worker.Bag, 0, 12);
        Work(world, 5);
        Require(chest.Inventory["wood"] == 0 && worker.Bag["wood"] == 12, "a worker should not store a load a player holds on their cursor");
        world.Release();
        Work(world, 5, () => chest.Inventory["wood"] > 0);
        Require(chest.Inventory["wood"] == 12, "a worker should store the load once the player lets go of it");
    }

    private static void CheckWorkerPutsBackAToolNoneOfTheirJobsUses(GameData data) {
        var (world, tree, _, rack) = ToolYard(data);
        rack.Inventory.Add("steel_axe", 1);
        var cutter = Staff(world, tree, JobKind.Gather);
        Work(world, 60, () => cutter.Tool == "steel_axe");
        Require(cutter.Tool == "steel_axe", "a woodcutter should take the axe from the tool station");
        world.SetDuty(cutter, tree, JobKind.Gather, false);
        Work(world, 60, () => rack.Inventory["steel_axe"] == 1);
        Require(rack.Inventory["steel_axe"] == 1 && cutter.Tool == "", "a worker whose jobs no longer use their tool should put it back for someone else");
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

    /// <summary>A batch of steel takes 2 coal as input and burns 1, so of a hand load of 12, 8 go in as input and 4 as fuel.</summary>
    private static void CheckALoadOfCoalForSteelIsSharedBetweenInputAndFuel(GameData data) {
        var world = EmptyWorld(data);
        var forge = world.Add(ThingKind.Reinforced, new Vector2(10, 10));
        forge.Inventory.Add("iron_bar", 64);
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("coal", 100);
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

    /// <summary>Haul at the first table has nothing to do until wood reaches storage; the crafter at the second finishes the batch under way, then hauls.</summary>
    private static void CheckACrafterLowerInTheListGivesWayAfterOneBatch(GameData data) {
        var world = EmptyWorld(data);
        var first = Bench(world, new Vector2(10, 10), "planks");
        var second = Bench(world, new Vector2(10, 16), "planks", "steel_chisel");
        second.Inventory.Add("wood", 5);
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 13));
        var worker = Staff(world, first, JobKind.Haul);
        world.SetDuty(worker, second, JobKind.Craft, true);
        Work(world, 10, () => second.Processing);
        Require(second.Processing, "with nothing to haul, the worker should work the second table");
        chest.Inventory.Add("wood", 40);
        FirstCarry(world, worker);
        Require(worker.Bag["wood"] > 0 && second.Inventory["planks"] == 4 && !second.Processing,
            $"the worker should finish the batch under way, one round, then haul for the first table: {second.Inventory["planks"]} planks made");
    }

    /// <summary>
    /// From a player's save: a packer stood on the far side of their packing station, close enough to the packing spot to count as there for the path search,
    /// with the station between them; they stood still with "Path blocked".
    /// </summary>
    private static void CheckAWorkerBesideAStationWalksRoundItToWorkIt(GameData data) {
        var world = EmptyWorld(data);
        var packer = world.Add(ThingKind.Packer, Terrain.Spawn + new Vector2(6, 6));
        world.SetPacking(packer, "berries");
        packer.Inventory.Add("berries", 12); packer.Inventory.Add("crate", 2);
        var worker = Staff(world, packer, JobKind.Craft);
        worker.Pos = packer.Pos + new Vector2(-.49f, .5f);
        Work(world, 10, () => worker.WorkState == WorkerWorkState.Hammering);
        Require(worker.WorkState == WorkerWorkState.Hammering, $"a packer beside the far side of the station should walk round it and pack, not \"{worker.Status}\"");
    }

    /// <summary>A chest stands on the spot a pumper works the bellows from; they pump from as near to it as they get, rather than walking to and fro.</summary>
    private static void CheckAWorkerWhoseWorkSpotIsBlockedWorksFromAsNearAsTheyGet(GameData data) {
        var world = EmptyWorld(data);
        var forge = world.Add(ThingKind.Reinforced, Terrain.Spawn + new Vector2(6, 6));
        forge.Inventory.Add("iron_bar", 20); forge.Inventory.Add("coal", 20); forge.Burner!.Add("coal", 10);
        world.Add(ThingKind.Chest, forge.Pos + World.BellowsStand);
        var pumper = Staff(world, forge, JobKind.Bellows);
        Work(world, 10, () => pumper.WorkState == WorkerWorkState.Pumping);
        Require(pumper.WorkState == WorkerWorkState.Pumping, $"a pumper whose spot a chest stands on should pump from beside it, not \"{pumper.Status}\"");
    }

    private static void CheckASaveKeepsTheJobOrderGroupsAndTool(GameData data) {
        var world = EmptyWorld(data);
        var a = Bench(world, new Vector2(10, 10), "planks");
        var b = Bench(world, new Vector2(10, 16), "planks");
        var worker = Staff(world, a, JobKind.Craft, JobKind.Haul);
        world.SetDuty(worker, b, JobKind.Haul, true);
        GroupHauls(world, worker, b, a);
        worker.Tool = "steel_axe";
        string saved = JobsText(worker);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var back = loaded.Workers.Single(w => w.Id == worker.Id);
        Require(JobsText(back) == saved && back.Tool == "steel_axe", $"a save should keep a worker's job order, haul groups and tool: {JobsText(back)} with \"{back.Tool}\"");
    }

    /// <summary>The autosave can run while a player holds a worker's tool on the cursor; after loading, the slot works as any other.</summary>
    private static void CheckAToolHeldFromAWorkerIsLetGoOnLoad(GameData data) {
        var world = EmptyWorld(data);
        var worker = AddWorker(world, world.Player + Vector2.One);
        worker.Tool = "steel_axe";
        world.Hold(worker.ToolSlot, 0, 1);
        Require(world.Held is { Item: "steel_axe" }, "the player should hold the worker's axe");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var back = loaded.Workers.Single(w => w.Id == worker.Id);
        var rack = loaded.Add(ThingKind.Toolstation, back.Pos + new Vector2(3, 0));
        Work(loaded, 30, () => rack.Inventory["steel_axe"] == 1);
        Require(rack.Inventory["steel_axe"] == 1 && back.Tool == "" && back.ToolSlot.Room("steel_pick") == 1,
            $"after loading, a worker with no job should put the axe back and have room for another tool: rack {rack.Inventory["steel_axe"]}, tool \"{back.Tool}\"");
    }

    private static void CheckALoadForATradingPostGoesElsewhereOnceItsFilterNoLongerAsks(GameData data) {
        var world = EmptyWorld(data);
        var post = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        string crate = World.CrateOf("berries");
        world.SetFilter(post, crate, false, true);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 10));
        var worker = AddWorker(world, new Vector2(15, 12));
        worker.Bag.Add(PackCrate(world, ("berries", 12)), 1);
        world.Tick(.1f);
        Require(worker.Destination == post.Id, "a worker should carry a crate of berries to the trading post asking for it");
        world.ClearFilters(post);
        Work(world, 30, () => worker.Bag.Count == 0);
        Require(post.Inventory.Count == 0 && chest.Inventory.Count == 1, "a crate on its way to a post whose filter no longer asks for it should go to storage");
    }

    private static void CheckAWorkerDeliversWhatAPlayerDoesNotHold(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + new Vector2(3, 0));
        var worker = AddWorker(world, world.Player + Vector2.One);
        worker.Bag.Add("wood", 6); worker.Bag.Add("stone", 6);
        world.Hold(worker.Bag, worker.Bag.Layout.FindIndex(s => s.Item == "wood"), 6);
        Work(world, 20, () => chest.Inventory["stone"] > 0);
        Require(chest.Inventory["stone"] == 6 && worker.Bag["wood"] == 6, "a worker should store the stone while the player holds the wood");
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
        var asking = world.Add(ThingKind.Chest, new Vector2(10, 10));
        world.SetFilter(asking, "stone", false, true);
        var alsoAsking = world.Add(ThingKind.Chest, new Vector2(12, 12));
        alsoAsking.Inventory.Add("stone", 20);
        world.SetFilter(alsoAsking, "stone", false, true);
        var open = world.Add(ThingKind.Chest, new Vector2(18, 10));
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
        var nearer = world.Add(ThingKind.Chest, new Vector2(13, 10));
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
        var chest = world.Add(ThingKind.Chest, new Vector2(14, 10));
        world.SetFilter(chest, "planks", false, true);
        Staff(world, chest, JobKind.Haul);
        Work(world, 60, () => chest.Inventory["planks"] == 20);
        Require(chest.Inventory["planks"] == 20 && bench.Inventory["planks"] == 0, $"a chest's hauler should fetch the table's planks for its filter: {chest.Inventory["planks"]} brought");
    }

    private static void CheckAChestsHaulerCarriesOutBeforeFilling(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(10, 10));
        chest.Inventory.Add("wood", 12);
        world.SetFilter(chest, "stone", false, true);
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("stone", 12);
        Bench(world, new Vector2(16, 10), "planks");
        var hauler = Staff(world, chest, JobKind.Haul);
        Require(FirstCarry(world, hauler) == "wood", $"a chest's hauler should carry wood out to the table asking for it before fetching stone, not carry {hauler.Bag.First()}");
    }

    private static void CheckAHaulGroupTakesAChestsCarryBeforeToppingUpAStation(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("wood", 5);
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("wood", 40);
        var asking = world.Add(ThingKind.Chest, new Vector2(10, 16));
        world.SetFilter(asking, "stone", false, true);
        world.Add(ThingKind.Chest, new Vector2(13, 16)).Inventory.Add("stone", 12);
        var hauler = Staff(world, bench, JobKind.Haul);
        world.SetDuty(hauler, asking, JobKind.Haul, true);
        GroupHauls(world, hauler, bench, asking);
        Require(FirstCarry(world, hauler) == "stone", $"in a haul group, a chest's carry should come before topping up the table's wood, not {hauler.Bag.First()}");
    }

    private static void CheckAPlacedCrateTakesLoadsLikeAChestButNoCrate(GameData data) {
        var world = EmptyWorld(data);
        var crate = world.Add(ThingKind.Crate, new Vector2(14, 10));
        var worker = AddWorker(world, new Vector2(10, 10));
        worker.Bag.Add("stone", 6);
        Work(world, 30, () => worker.Bag.Count == 0);
        Require(crate.Inventory["stone"] == 6, "a worker should store a load in a placed crate as in a chest");
        world.SetFilter(crate, World.CrateOf("berries"), false, true);
        Require(crate.Inventory.InputFilter.Count == 0, "a placed crate's filter should not name a crate");
        string packed = PackCrate(world, ("berries", 12));
        Warp(world, worker.Pos + Vector2.One);
        Require(world.Transfer(world.Backpack, worker.Bag, packed, 1) == 1, "a player should hand a worker a packed crate");
        Work(world, 10);
        Require(crate.Inventory.Count == 6 && worker.Bag.Count == 1, "a worker should never put a packed crate into a placed crate");
    }

    private static void CheckAPackedCrateKeepsItsFiltersWhenPutDownAgain(GameData data) {
        var world = EmptyWorld(data);
        var crate = world.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        crate.Inventory.Add("wood", 3);
        world.SetFilter(crate, "wood", false, true); world.SetFilter(crate, "stone", true, true);
        Require(world.PickUpCrate(crate), "a nearby crate should be picked up");
        string packed = world.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        var placed = world.PlaceCrate(packed, world.Player + new Vector2(2, 0))!;
        Require(placed.Inventory.InputFilter.SetEquals(new[] { "wood" }) && placed.Inventory.OutputFilter.SetEquals(new[] { "stone" }),
            "a crate of goods put down again should keep the filters it had");
        world.Transfer(placed.Inventory, world.Backpack, "wood", 3);
        Require(world.PickUpCrate(placed), "the emptied crate should be picked up");
        var empty = world.PlaceCrate("crate", world.Player + new Vector2(2, 0))!;
        Require(empty.Inventory.InputFilter.Count == 0 && empty.Inventory.OutputFilter.Count == 0, "an empty crate put down again should have no filters");
    }

    private static void CheckAChestsHaulerNeverCarriesWaterOutOfAStationByHand(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(10, 10));
        world.SetRecipe(table, "crushed_berries");
        table.Inventory.Add(Water.Liquid, 5);
        var chest = world.Add(ThingKind.Chest, new Vector2(14, 10));
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
        var chest = world.Add(ThingKind.Chest, new Vector2(14, 10));
        chest.Inventory.Add(Water.Full, 5);
        var hauler = Staff(world, chest, JobKind.Haul);
        Work(world, 30, () => hauler.Bag.Count > 0);
        Require(hauler.Bag[Water.Full] == 2, $"a chest's hauler should take the table two full bottles for its 6 liters of room, not {hauler.Bag[Water.Full]}");
    }
}
