using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

/// <summary>Checks for how a worker carries, picks the next job from their list and hauls for a station.</summary>
public static class WorkerTaskChecks {
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
        CheckSaveWithAHaulOutsideItsGroupIsRefused(data);
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
        var cutter = Staff(world, tree, "gather");
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
        Staff(world, first, "gather");
        Work(world, 60, () => chest.Inventory["coal"] > 0);
        Require(chest.Inventory["coal"] == 3, $"a picker should bring all three known pieces of coal in one load, not {chest.Inventory["coal"]}");
    }

    private static void CheckWorkerKeepsToTheFirstJobWhileItCanBeDone(GameData data) {
        var world = EmptyWorld(data);
        var berries = world.Add(ThingKind.Berries, new Vector2(10, 10), 400);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 14), 400);
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 12));
        var picker = Staff(world, berries, "gather");
        world.SetDuty(picker, leaves, "gather", true);
        Work(world, 90);
        Require(chest.Inventory["berries"] >= 24 && chest.Inventory["leaves"] == 0,
            $"a worker should pick berries, their first job, for as long as there are some: {chest.Inventory["berries"]} berries and {chest.Inventory["leaves"]} leaves stored");
    }

    /// <summary>With no storage at all, the wood a crafter cuts goes into the table, and they saw it all before cutting more.</summary>
    private static void CheckWorkerGoesBackToTheFirstJobAfterOneDeliveryOfTheNext(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks", "steel_chisel");
        var tree = world.Add(ThingKind.Tree, new Vector2(14, 10), 4000);
        var crafter = Staff(world, bench, "craft");
        world.SetDuty(crafter, tree, "gather", true);
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
        Staff(world, bench, "haul");
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
        var hauler = Staff(world, bench, "haul");
        Require(FirstCarry(world, hauler) == "wood", $"a hauler should first bring wood to a table with none, not carry {hauler.Bag.First()}");
    }

    private static void CheckHaulerTakesAFullOutputBeforeToppingUpInputs(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 64); bench.Inventory.Add("wood", 5);
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, "haul");
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
        var hauler = Staff(world, bench, "haul");
        Require(FirstCarry(world, hauler) == "steel_parts", $"a hauler should top up the input that covers the fewest batches, not carry {hauler.Bag.First()}");
    }

    private static void CheckHaulerTopsUpInputsBeforeTakingAnOutputThatIsNotFull(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        bench.Inventory.Add("planks", 20); bench.Inventory.Add("wood", 10);
        world.Add(ThingKind.Chest, new Vector2(13, 10)).Inventory.Add("wood", 40);
        var hauler = Staff(world, bench, "haul");
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
        var hauler = Staff(world, table, "haul");
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
        var cutter = Staff(world, tree, "gather");
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
        var cutter = Staff(world, tree, "gather");
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
        var pumper = Staff(world, forge, "bellows");
        world.SetDuty(pumper, berries, "gather", true);
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
        var crafter = Staff(world, bench, "craft");
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
        var cutter = Staff(world, tree, "gather");
        cutter.Tool = "steel_shovel";
        Work(world, 3 * Step);
        Require(World.HasDuty(cutter, tree.Id, "gather") && !World.HasDuty(cutter, next.Id),
            "a woodcutter who cannot put back the wrong tool should keep their tree, not move the job to the next one");
        Require(cutter.Tool == "steel_shovel" && cutter.Status == "No room to put back the steel shovel", $"a woodcutter who cannot put back the wrong tool should say so, not \"{cutter.Status}\"");
    }

    private static void CheckAJobMovedUpComesFirst(GameData data) {
        var world = EmptyWorld(data);
        var berries = world.Add(ThingKind.Berries, new Vector2(10, 10), 400);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 14), 400);
        var chest = world.Add(ThingKind.Chest, new Vector2(13, 12));
        var picker = Staff(world, berries, "gather");
        world.SetDuty(picker, leaves, "gather", true);
        world.MoveDuty(picker, 1, 0);
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
        var hauler = Staff(world, stocked, "haul");
        world.SetDuty(hauler, bare, "haul", true);
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
        var hauler = Staff(world, few, "haul");
        world.SetDuty(hauler, many, "haul", true);
        GroupHauls(world, hauler, few, many);
        Require(FirstCarry(world, hauler) == "planks" && many.Inventory["planks"] == 18 && few.Inventory["planks"] == 20,
            $"in a haul group, a hauler should take planks from the table holding the most: {many.Inventory["planks"]} and {few.Inventory["planks"]} left");
    }

    private static void CheckJobsMoveInAndOutOfHaulGroups(GameData data) {
        var world = EmptyWorld(data);
        var a = Bench(world, new Vector2(10, 10), "planks");
        var b = Bench(world, new Vector2(10, 16), "planks");
        var worker = Staff(world, a, "craft", "haul");
        world.SetDuty(worker, b, "craft", true); world.SetDuty(worker, b, "haul", true);
        string A = a.Id.ToString(), B = b.Id.ToString();
        int group = GroupHauls(world, worker, a, b);
        Require(JobList(worker) == $"craft@{A} craft@{B} group[haul@{A} haul@{B}]", $"hauls moved into a new group should follow it in the order they were added: {JobList(worker)}");
        world.MoveDuty(worker, 2, 0);
        Require(JobList(worker) == $"group[haul@{A} haul@{B}] craft@{A} craft@{B}", $"a group moved to the top should take its hauls along: {JobList(worker)}");
        world.MoveDuty(worker, 3, 2);
        Require(JobList(worker) == $"group[haul@{A} haul@{B}] craft@{A} craft@{B}", $"a craft job dropped among a group's hauls should land after them: {JobList(worker)}");
        world.MoveDuty(worker, 2, 5);
        Require(JobList(worker) == $"group[haul@{A}] craft@{A} craft@{B} haul@{B}", $"a haul moved out of its group should be a job of its own: {JobList(worker)}");
        world.RemoveHaulGroup(worker, group);
        Require(JobList(worker) == $"haul@{A} craft@{A} craft@{B} haul@{B}", $"a group taken apart should leave its hauls where they were: {JobList(worker)}");
        world.AddHaulGroup(worker);
        Require(JobList(worker) == $"haul@{A} craft@{A} craft@{B} haul@{B} group[]", $"a new group should start empty at the end of the list: {JobList(worker)}");
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
        var cutter = Staff(world, tree, "gather");
        Work(world, 60, () => cutter.Tool == "steel_axe");
        Require(cutter.Tool == "steel_axe", "a woodcutter should take the axe from the tool station");
        world.SetDuty(cutter, tree, "gather", false);
        Work(world, 60, () => rack.Inventory["steel_axe"] == 1);
        Require(rack.Inventory["steel_axe"] == 1 && cutter.Tool == "", "a worker whose jobs no longer use their tool should put it back for someone else");
    }

    private static void CheckSaveWithAHaulOutsideItsGroupIsRefused(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks");
        var worker = Staff(world, bench, "haul", "craft");
        GroupHauls(world, worker, bench);
        string json = world.Serialize();
        EmptyWorld(data).Deserialize(json);
        // Turned round, as an edit by hand could leave it, the list has the haul before its group's own entry.
        worker.Duties.Reverse();
        bool refused = false;
        try { EmptyWorld(data).Deserialize(world.Serialize()); } catch (System.InvalidOperationException error) { refused = error.Message.Contains("unknown items"); }
        Require(refused, "a save whose haul stands before its group's own entry should be refused with a clear reason");
    }
}
