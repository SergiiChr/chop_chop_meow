using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

/// <summary>Checks for how a worker picks the next job from their list and what they hold and keep while they work it.</summary>
public static class WorkerJobChecks {
    public static void Run(GameData data) {
        CheckWorkerKeepsToTheFirstJobWhileItCanBeDone(data);
        CheckWorkerGoesBackToTheFirstJobAfterOneDeliveryOfTheNext(data);
        CheckBellowsWorkerStopsWhenThereIsNothingToSmelt(data);
        CheckWorkerWithNothingToDoWaitsAtTheTradingPost(data);
        CheckGathererWithNoRoomToPutBackTheWrongToolKeepsTheirSpot(data);
        CheckWorkerPutsBackAToolNoneOfTheirJobsUses(data);
        CheckACrafterLowerInTheListGivesWayAfterOneBatch(data);
        CheckASaveKeepsTheJobOrderGroupsAndTool(data);
        CheckAToolHeldFromAWorkerIsLetGoOnLoad(data);
        CheckAWorkerDeliversWhatAPlayerDoesNotHold(data);
        CheckAPackedCrateKeepsItsFiltersWhenPutDownAgain(data);
        CheckAJobMovedUpComesFirst(data);
        CheckPlayerTakesWhatAWorkerCarries(data);
        CheckPlayerGivesAWorkerALoadToCarryOff(data);
        CheckPlayerTakesAndGivesAWorkersTool(data);
        CheckAWorkerKeepsALoadAPlayerHoldsUntilTheyLetGo(data);
    }

    private static void CheckWorkerKeepsToTheFirstJobWhileItCanBeDone(GameData data) {
        var world = EmptyWorld(data);
        var berries = world.Add(ThingKind.Berries, new Vector2(10, 10), 400);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 14), 400);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 12)));
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
        TakeEverything(world, world.Add(ThingKind.Toolstation, new Vector2(12, 12))).Inventory.Add("steel_axe", 1);
        Work(world, 200, () => bench.Inventory["planks"] >= 8);
        Require(bench.Inventory["planks"] >= 8, $"a crafter who also cuts wood should saw planks from the wood they brought: {bench.Inventory["planks"]} planks");
        Require(tree.Remaining == 3988, $"a crafter should go back to sawing after one load of wood, not cut {4000 - tree.Remaining}");
    }

    private static void CheckBellowsWorkerStopsWhenThereIsNothingToSmelt(GameData data) {
        var world = EmptyWorld(data);
        var forge = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        var berries = world.Add(ThingKind.Berries, new Vector2(20, 25), 400);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(24, 22)));
        chest.Inventory.Filter("berries", false, true); chest.Inventory.Filter("steel_bar", false, true);
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
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(14, 12)));
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, new Vector2(11, 13)));
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
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 12)));
        var picker = Staff(world, berries, JobKind.Gather);
        world.SetDuty(picker, leaves, JobKind.Gather, true);
        world.MoveJob(picker, new JobSpot(1), new JobSpot(0));
        Work(world, 90);
        Require(chest.Inventory["leaves"] >= 24 && chest.Inventory["berries"] == 0,
            $"a job dragged to the top should be the one a worker does: {chest.Inventory["leaves"]} leaves and {chest.Inventory["berries"]} berries stored");
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
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
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
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
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

    /// <summary>Haul at the first table has nothing to do until wood reaches storage; the crafter at the second finishes the batch under way, then hauls.</summary>
    private static void CheckACrafterLowerInTheListGivesWayAfterOneBatch(GameData data) {
        var world = EmptyWorld(data);
        var first = Bench(world, new Vector2(10, 10), "planks");
        var second = Bench(world, new Vector2(10, 16), "planks", "steel_chisel");
        second.Inventory.Add("wood", 5);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 13)));
        var worker = Staff(world, first, JobKind.Haul);
        world.SetDuty(worker, second, JobKind.Craft, true);
        Work(world, 10, () => second.Processing);
        Require(second.Processing, "with nothing to haul, the worker should work the second table");
        chest.Inventory.Add("wood", 40);
        FirstCarry(world, worker);
        Require(worker.Bag["wood"] > 0 && second.Inventory["planks"] == 4 && !second.Processing,
            $"the worker should finish the batch under way, one round, then haul for the first table: {second.Inventory["planks"]} planks made");
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
        var rack = TakeEverything(loaded, loaded.Add(ThingKind.Toolstation, back.Pos + new Vector2(3, 0)));
        Work(loaded, 30, () => rack.Inventory["steel_axe"] == 1);
        Require(rack.Inventory["steel_axe"] == 1 && back.Tool == "" && back.ToolSlot.Room("steel_pick") == 1,
            $"after loading, a worker with no job should put the axe back and have room for another tool: rack {rack.Inventory["steel_axe"]}, tool \"{back.Tool}\"");
    }

    private static void CheckAWorkerDeliversWhatAPlayerDoesNotHold(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + new Vector2(3, 0)));
        var worker = AddWorker(world, world.Player + Vector2.One);
        worker.Bag.Add("wood", 6); worker.Bag.Add("stone", 6);
        world.Hold(worker.Bag, worker.Bag.Layout.FindIndex(s => s.Item == "wood"), 6);
        Work(world, 20, () => chest.Inventory["stone"] > 0);
        Require(chest.Inventory["stone"] == 6 && worker.Bag["wood"] == 6, "a worker should store the stone while the player holds the wood");
    }

    private static void CheckAPackedCrateKeepsItsFiltersWhenPutDownAgain(GameData data) {
        var world = EmptyWorld(data);
        var crate = TakeEverything(world, world.Add(ThingKind.Crate, world.Player + new Vector2(1, 0)));
        crate.Inventory.Add("wood", 3);
        world.SetFilter(crate, "wood", false, true); world.SetFilter(crate, "stone", true, true);
        Require(world.PickUpCrate(crate), "a nearby crate should be picked up");
        string packed = world.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        var placed = world.PlaceCrate(packed, world.Player + new Vector2(2, 0))!;
        Require(placed.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && placed.Inventory.OutputFilter.SequenceEqual(new[] { "stone" }),
            "a crate of goods put down again should keep the filters it had");
        world.Transfer(placed.Inventory, world.Backpack, "wood", 3);
        Require(world.PickUpCrate(placed), "the emptied crate should be picked up");
        var empty = world.PlaceCrate("crate", world.Player + new Vector2(2, 0))!;
        Require(empty.Inventory.InputFilter.Count == 0 && empty.Inventory.OutputFilter.SequenceEqual(new[] { Inventory.Everything }),
            "an empty crate put down again should have a new crate's filters: nothing in, everything out");
    }
}
