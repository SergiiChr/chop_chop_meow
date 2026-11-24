using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static WorkerSteps;

/// <summary>Checks of the tool station: workers taking tools from it, putting them back and stocking it, by its filters.</summary>
public static class ToolStationChecks {
    public static void Run(GameData data) {
        Test(CheckStockerFillsToolRack, data);
        Test(CheckWorkerLeavesToolTheFilterHolds, data);
        Test(CheckWorkerTakesAxeFromStationAndPutsItBackForAJobWithoutTools, data);
        Test(CheckFullStationLeavesWorkerHoldingTheWrongToolUntilThereIsRoom, data);
        Test(CheckWorkerPutsToolBackGoingHomeAndLeavesItWhenDismissed, data);
        Test(CheckGathererWithNoRoomToPutBackTheWrongToolKeepsTheirSpot, data);
        Test(CheckWorkerPutsBackAToolNoneOfTheirJobsUses, data);
        Test(CheckWaitingWorkerTakesAToolOnceOneIsHung, data);
    }

    private static void CheckStockerFillsToolRack(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, new Vector2(16, 20)));
        chest.Inventory.Add("steel_axe", 1);
        var stocker = AddWorker(world, new Vector2(18, 20));
        world.SetDuty(stocker, rack, JobKind.Stock, true);
        SafeTick(world, 20);
        Require(rack.Inventory["steel_axe"] == 1 && chest.Inventory["steel_axe"] == 0, "a stocker should bring a stored tool to the rack");
    }

    /// <summary>A tool station's output filter keeps workers from taking a tool it does not let out.</summary>
    private static void CheckWorkerLeavesToolTheFilterHolds(GameData data) {
        var world = EmptyWorld(data);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        var heldRack = TakeEverything(world, world.Add(ThingKind.Toolstation, new Vector2(30, 20)));
        heldRack.Inventory.Add("steel_axe", 1);
        world.SetFilter(heldRack, "steel_pick", true, true);
        var oak = world.Add(ThingKind.Tree, new Vector2(32, 22), 5);
        var cutter = AddWorker(world, new Vector2(30, 22));
        world.Assign(cutter, oak);
        SafeTick(world, 10);
        Require(cutter.Tool == "", "a worker should not take a tool the station's output filter holds back");
        Require(cutter.Missing == "axe", "a worker with no axe to take should say an axe is missing");
        Require(heldRack.Inventory["steel_axe"] == 1, "the station should keep the held back axe");
        world.SetFilter(heldRack, Inventory.Everything, true, true);
        // The cutter waited in the clearing, with no trading post to wait at, and walks back for the axe.
        Work(world, 90, () => cutter.Tool == "steel_axe");
        Require(cutter.Tool == "steel_axe", "with the output filter set to Everything, the worker should take the axe");
    }

    /// <summary>Workers take the best tool their job needs from a tool station, say once when none is there, and put it back when reassigned or going home.</summary>
    private static void CheckWorkerTakesAxeFromStationAndPutsItBackForAJobWithoutTools(GameData data) {
        var (world, tree, bush, station) = ToolYard(data);
        var said = new List<string>();
        world.Notify = said.Add;
        Require(!world.Holds(station, "wood") && world.Holds(station, "steel_axe"), "a tool station should hold tools only");
        var worker = AddWorker(world, tree.Pos + Vector2.One);
        worker.Name = "Fern";
        world.Assign(worker, tree);
        SafeTick(world, 5);
        Require(worker.Status == "Needs an axe to chop wood", "a woodcutter without an axe should say so");
        Require(worker.Bag.Count == 0, "a woodcutter without an axe should not chop");
        Require(said.Count(m => m == "Fern needs an axe to chop wood. Put one in a tool station.") == 1,
            "a woodcutter without an axe should say so only once");
        station.Inventory.Add("steel_axe", 1);
        SafeTick(world, 8);
        Require(worker.Tool == "steel_axe", "the worker should take the axe once one is there");
        Require(station.Inventory["steel_axe"] == 0, "the axe should leave the tool station");
        Require(tree.Remaining < 400, "the worker should start chopping");
        world.Assign(worker, bush);
        for (int i = 0; i < 80 && worker.Tool != ""; i++) SafeTick(world, Step);
        Require(worker.Tool == "" && station.Inventory["steel_axe"] == 1, "a worker moved to berries, which need no tool, should put the axe back");
        SafeTick(world, 10);
        Require(bush.Remaining < 400, "berries need no tool");
    }

    /// <summary>A full station leaves a worker holding the wrong tool, who must wait rather than mine with an axe.</summary>
    private static void CheckFullStationLeavesWorkerHoldingTheWrongToolUntilThereIsRoom(GameData data) {
        var (world, _, _, station) = ToolYard(data);
        station.Inventory.Add("steel_axe", 1);
        var iron = world.Add(ThingKind.Iron, new Vector2(18, 14), 400);
        var miner = AddWorker(world, iron.Pos + Vector2.One); miner.Tool = "steel_axe";
        station.Inventory.Add("steel_pick", station.Inventory.Room("steel_pick"));
        world.Assign(miner, iron);
        SafeTick(world, 10);
        Require(miner.Tool == "steel_axe", "a worker who cannot put back the wrong tool should keep holding it");
        Require(iron.Remaining == 400, "a worker should not mine with the wrong tool");
        Require(miner.Status == "No room to put back the steel axe",
            "a worker who cannot put back the wrong tool should say so");
        station.Inventory.Remove("steel_pick", 1);
        for (int i = 0; i < 120 && miner.Tool != "steel_pick"; i++) SafeTick(world, Step);
        Require(miner.Tool == "steel_pick" && station.Inventory["steel_axe"] == 2, "with room again the worker should swap the axe for a pickaxe");
    }

    private static void CheckWorkerPutsToolBackGoingHomeAndLeavesItWhenDismissed(GameData data) {
        var (world, tree, _, station) = ToolYard(data);
        station.Inventory.Add("steel_axe", 1);
        var worker = AddWorker(world, tree.Pos + Vector2.One);
        world.Assign(worker, tree);
        SafeTick(world, 10);
        Require(worker.Tool == "steel_axe", "back on the tree the worker should fetch the axe again");
        RunUntil(world, (world.Day - 1 + data.DuskStarts) * data.DaySeconds - .5f);
        // By dusk the day's wood has filled the chest, so the worker waits in the clearing holding a load and walks back to the rack with the axe first.
        for (int i = 0; i < 800 && !worker.Home; i++) SafeTick(world, Step);
        Require(worker.Home, "a worker going home should be home");
        Require(worker.Tool == "", "a worker going home should put the axe back");
        Require(station.Inventory["steel_axe"] == 1, "a worker going home should leave the axe in the tool station");
        Require(worker.Assignment == tree.Id, "a worker going home should keep the assignment");

        worker.Home = false; worker.Commute = 0; worker.Tool = "steel_pick";
        world.Fire(worker);
        Require(world.Things.Any(t => t.Kind == ThingKind.Grave && t.Inventory["steel_pick"] == 1), "a dismissed worker's tool should be left in a recovery box");
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

    /// <summary>A worker waiting for a tool takes one as soon as it hangs in a tool station that gives it out, and not while its filter keeps it in.</summary>
    private static void CheckWaitingWorkerTakesAToolOnceOneIsHung(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn;
        TakeEverything(world, world.Add(ThingKind.Chest, at + new Vector2(2, 2)));
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, at + new Vector2(-2, 2)));
        var tree = world.Add(ThingKind.Tree, at + new Vector2(1, 0), 40);
        world.Player = at; world.Look();
        var cutter = AddWorker(world, tree.Pos);
        world.Assign(cutter, tree);
        for (int i = 0; i < 20; i++) world.Tick(.1f);
        Require(cutter.Missing == "axe", "a woodcutter with no axe anywhere should wait for one");
        // An output filter naming only the pickaxe keeps the axe in.
        world.SetFilter(rack, "steel_pick", true, true);
        Give(world, "steel_axe", 2); world.Transfer(world.Backpack, rack.Inventory, "steel_axe", 1);
        for (int i = 0; i < 20; i++) world.Tick(.1f);
        Require(cutter.Tool == "" && rack.Inventory["steel_axe"] == 1, "a tool station whose filter keeps its axe in should not give it out");
        world.SetFilter(rack, Inventory.Everything, true, true);
        for (int i = 0; i < 100 && cutter.Tool == ""; i++) world.Tick(.1f);
        Require(cutter.Tool == "steel_axe" && rack.Inventory["steel_axe"] == 0, "a waiting woodcutter should take the axe once the tool station gives it out");
    }
}
