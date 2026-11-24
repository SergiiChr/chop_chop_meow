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

/// <summary>Simulation checks for mines: marking, building, supplying and mining one, and the end goal's mines.</summary>
public static class MineChecks {
    public static void Run(GameData data) {
        Test(CheckMineSitesAlignWithCliffs, data);
        Test(CheckMinesAreMarkedAtCliffsOnceEach, data);
        Test(CheckWorkersDigAndFrameMineWithTheirTools, data);
        Test(CheckWorkerTakesTheFreeHammerFromTheRack, data);
        Test(CheckPlayerDigsAndFitsAMineWithTheirTools, data);
        Test(CheckPlayerDeliversOnlyWhatAMineStillNeeds, data);
        Test(CheckMinersBringOreOutAndStopWhenOutputIsFull, data);
        Test(CheckOnlyAvailableMinersUnload, data);
        Test(CheckAMinerFetchesNoPickaxeForAFullMineWithNowhereToUnload, data);
        Test(CheckFullMineUnloadsIntoAPackingStationPackingItsOre, data);
        Test(CheckMiningIsSlowerThanDeposits, data);
        Test(CheckEndGoalsWantAWorkingMineOnEveryOre, data);
    }

    private static void CheckMineSitesAlignWithCliffs(GameData data) {
        foreach (var cliff in Cliff.All) {
            Require(Cliff.Nearest(cliff.MineSpot) == cliff, "each aligned site must belong to its own cliff");
            var marking = FundedWorld(data); marking.Player = cliff.MineSpot + cliff.Face * 3;
            Require(marking.Construction.Build(ThingKind.Mine, cliff.MineSpot + cliff.Along + cliff.Face)?.Pos == cliff.MineSpot, "every aligned site must remain markable and snap correctly");
            var outward = cliff.Face;
            var basis = Basis.FromEuler(MineModel.Yaw(cliff.Face));
            var renderedOut = basis * Vector3.Back;
            var renderedHeader = basis * Vector3.Right;
            Require(outward.Dot(cliff.Face) > .999f && Math.Abs(outward.Dot(cliff.Along)) < .001f,
                "a mine must face outward from its own cliff rather than a common camera direction");
            Require(new Vector2(renderedOut.X, renderedOut.Z).Dot(outward) > .999f && Math.Abs(new Vector2(renderedHeader.X, renderedHeader.Z).Dot(cliff.Along)) > .999f,
                "the rendered portal must follow the cliff and its rails must face out from it");
            Require(cliff.MineSpot.IsEqualApprox(cliff.Center + cliff.Face * 4.2f), "each new entrance must project directly from its cliff center");
        }
    }

    private static void CheckMinesAreMarkedAtCliffsOnceEach(GameData data) {
        var world = FundedWorld(data);
        var cliff = Cliff.Of(ThingKind.Iron);
        world.Player = cliff.MineSpot + cliff.Face * 3;
        Require(world.Construction.PlacementBlock(ThingKind.Mine, world.Player + cliff.Face * 15) != null, "a mine must be marked at a cliff, not out in the valley");
        int carried = world.Backpack.Count;
        var mine = MarkMine(world);
        Require(mine.Pos == cliff.MineSpot, "marking must snap to the cliff's mine spot");
        Require(mine.MineStage == MineStage.Marked, "marking must leave the mine in the marked stage");
        Require(world.Backpack.Count == carried, "marking must be free");
        Require(world.Construction.Build(ThingKind.Mine, cliff.MineSpot - cliff.Along * 2) == null && world.Things.Count(t => t.Kind == ThingKind.Mine) == 1, "a cliff must take only one mine");
        Require(Cliff.All.All(c => world.CanStand(c.MineSpot) && world.CanStand(Mines.Door(new Thing { Kind = ThingKind.Mine, Pos = c.MineSpot }))), "every cliff's mine spot and entrance must be open ground");
        Require(!world.CanStand(Mines.Door(mine) - cliff.Face * 2), "the outcrop the tunnel runs into must block walking");
        var coal = MarkMine(world, ThingKind.Coal);
        Require(Mines.Ore(coal) == "coal" && Mines.Ore(mine) == "iron_ore", "each mine must yield the ore of its own cliff");
    }

    private static void CheckWorkersDigAndFrameMineWithTheirTools(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world);
        var cliff = Cliff.Nearest(mine.Pos);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3 - cliff.Along * 2));
        foreach (var (item, amount) in data.Mine.Materials) chest.Inventory.Add(item, amount);
        chest.Inventory.Add("wood", 20);
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, mine);
        Work(world, 30);
        Require(mine.MineStage == MineStage.Marked, "digging must not start without a pickaxe");
        Require(mine.Progress == 0, "digging must show no progress without a pickaxe");
        Require(worker.Status.Contains("pickaxe"), "a worker without a pickaxe must say so");

        // Workers take their tools from a tool station by the storage.
        var station = TakeEverything(world, world.Add(ThingKind.Toolstation, chest.Pos + cliff.Along * 2));
        station.Inventory.Add("steel_pick", 1);
        float started = world.Time;
        Work(world, data.DaySeconds * 3, () => mine.MineStage > MineStage.Marked);
        Require(mine.MineStage == MineStage.Dug,
            $"one worker must dig the entrance: stage {mine.MineStage}, progress {mine.Progress:0}, {worker.Status}");
        Require(world.Time - started >= world.Mines.StageSeconds,
            $"digging the entrance must take at least one working day: {world.Time - started:0}s");
        Require(world.Time - started < data.DaySeconds * 2,
            $"digging the entrance must take less than two days: {world.Time - started:0}s");

        // The surplus wood stays in storage: the site takes only what it still needs.
        Work(world, data.DaySeconds, () => mine.MineStage > MineStage.Dug);
        Require(mine.MineStage == MineStage.Supplied, $"a worker must finish hauling with the mine supplied: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(mine.Inventory.Count == 0, $"a supplied mine must have used up the materials hauled to it: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(chest.Inventory["wood"] == 20, $"a worker must leave the surplus wood in storage: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(chest.Inventory["stone"] == 0, $"a worker must leave stone in storage: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(chest.Inventory["iron_bar"] == 0, $"a worker must leave iron bars in storage: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(worker.Bag.Count == 0, $"a worker must deliver everything it carried: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");

        Work(world, 30);
        Require(mine.MineStage == MineStage.Supplied, "fitting the frame must not start without a hammer");
        Require(mine.Progress == 0, "fitting the frame must show no progress without a hammer");
        Require(worker.Status.Contains("hammer"), "a worker without a hammer must say so");
        Require(station.Inventory["steel_pick"] == 1, "a worker hauling materials needs no pickaxe and puts it back");
        station.Inventory.Add("wood_hammer", 1);
        started = world.Time;
        Work(world, data.DaySeconds * 3, () => mine.MineStage == MineStage.Open);
        Require(mine.MineStage == MineStage.Open && world.Time - started >= world.Mines.StageSeconds, "a worker with a hammer must fit the frame in about one working day");
    }

    private static void CheckWorkerTakesTheFreeHammerFromTheRack(GameData data) {
        var world = EmptyWorld(data);
        var cliff = Cliff.Of(ThingKind.Iron);
        var mine = world.Add(ThingKind.Mine, cliff.MineSpot);
        mine.MineStage = MineStage.Supplied;
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, mine.Pos + cliff.Face * 2));
        rack.Inventory.Add("steel_hammer", 1);
        rack.Inventory.Add("wood_hammer", 1);
        world.Player = rack.Pos + cliff.Along;
        world.Hold(rack.Inventory, SlotOf(rack.Inventory, "steel_hammer"), 1);
        Require(world.Held is { Item: "steel_hammer" }, "the player is holding the steel hammer from the rack");
        var worker = AddWorker(world, world.Player);
        world.SetDuty(worker, mine, JobKind.Build, true);
        Work(world, 10, () => mine.Progress > 0);
        Require(worker.Tool == "wood_hammer" && mine.Progress > 0 && world.Held is { Item: "steel_hammer" },
            "the worker takes the free wooden mallet and fits the frame while the player still holds the steel hammer");
    }

    private static void CheckPlayerDigsAndFitsAMineWithTheirTools(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world);
        world.Player = Mines.Door(mine);
        world.Gather(mine, 1);
        Require(mine.Progress == 0 && world.GatherBlock(mine)?.Contains("pickaxe") == true, $"digging the entrance should need a pickaxe: {world.GatherBlock(mine)}");
        Give(world, "steel_pick", 1);
        for (float held = 0; held <= world.Mines.StageSeconds && mine.MineStage == MineStage.Marked; held++) world.Gather(mine, 1);
        Require(mine.MineStage == MineStage.Dug, "a player holding right mouse on a marked mine with a pickaxe should dig its entrance in a working day");
        Require(world.GatherBlock(mine) == "Deliver 50 Wood, 10 Iron bar, 50 Stone first.", $"a dug mine should name the materials it takes: {world.GatherBlock(mine)}");
        Give(world, data.Mine.Materials);
        foreach (var (item, amount) in data.Mine.Materials) world.Mines.Supply(mine, item, amount);
        var said = new List<string>();
        world.Notify = said.Add;
        world.Tick(.1f);
        Require(mine.MineStage == MineStage.Supplied, "the delivered materials should shore the mine up");
        Require(said.Contains("The iron mine has its 50 Wood, 10 Iron bar, 50 Stone. You or a worker with a hammer can fit the frame and rails."),
            $"a supplied mine should name the materials it took: {string.Join(" | ", said)}");
        world.Gather(mine, 1);
        Require(mine.Progress == 0 && world.GatherBlock(mine)?.Contains("hammer") == true, $"fitting the frame should need a hammer: {world.GatherBlock(mine)}");
        Give(world, "wood_hammer", 1);
        for (float held = 0; held <= world.Mines.StageSeconds && mine.MineStage == MineStage.Supplied; held++) world.Gather(mine, 1);
        Require(mine.MineStage == MineStage.Open, "a player holding right mouse on a supplied mine with a hammer should fit its frame in a working day");
    }

    private static void CheckPlayerDeliversOnlyWhatAMineStillNeeds(GameData data) {
        var world = FundedWorld(data);
        var other = MarkMine(world, ThingKind.Coal);
        other.MineStage = MineStage.Dug;
        world.Player = other.Pos + Vector2.One;
        Give(world, "wood", 60); Give(world, "stone", 10);
        world.Mines.Supply(other, "wood", 60); world.Mines.Supply(other, "stone", 10);
        Require(other.Inventory["wood"] == data.Mine.Materials["wood"],
            "delivering from the backpack must hand over only the wood the mine still needs");
        Require(other.Inventory["stone"] == 10,
            "delivering from the backpack must hand over only the stone the mine still needs");
        Require(world.Backpack["wood"] == 10, "delivering from the backpack must leave the spare wood with the player");
    }

    private static void CheckMinersBringOreOutAndStopWhenOutputIsFull(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world);
        mine.MineStage = MineStage.Open;
        var cliff = Cliff.Nearest(mine.Pos);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3));
        var crew = Enumerable.Range(0, 4).Select(_ => AddWorker(world, chest.Pos + cliff.Along)).ToList();
        foreach (var w in crew) { w.Tool = "steel_pick"; world.Assign(w, mine); }
        Require(crew.Take(3).All(w => w.Assignment == mine.Id) && crew[3].Assignment == 0, "a mine must take three workers and refuse a fourth");

        Work(world, 20);
        Require(crew.Take(3).All(w => w.WorkState == WorkerWorkState.Underground && !world.Sees(w)), "miners must work out of sight underground");
        int ore = mine.Output!["iron_ore"];
        Require(ore > 0 && ore == mine.Output["stone"], "a mine must bring out ore and rocks together");

        mine.Output.Add("iron_ore", mine.Output.StackSize - 1 - ore);
        int stone = mine.Output["stone"];
        var hauler = crew.First();
        Work(world, 3, () => hauler.Bag.Count > 0);
        Require(hauler.Bag["iron_ore"] == 12 && mine.Output["iron_ore"] == mine.Output.StackSize - 12, "the first miner must carry a load of 12 ore out of the full stack");
        Require(mine.Output["stone"] <= stone + 3, "mining must stop once a slot is full");
        Work(world, 60, () => chest.Inventory["iron_ore"] >= 12);
        Require(chest.Inventory["iron_ore"] == 12, "the load must reach the nearest storage");
        Require(hauler.Bag.Count == 0, "the miner must leave nothing in their hands");
        Work(world, 10);
        Require(mine.Output["iron_ore"] > mine.Output.StackSize - 12, "mining must resume once a load is out");

        // Without storage for it, the output stays put and the corner message shows once.
        RemoveThings(world, t => t == chest);
        var said = new List<string>();
        world.Notify = said.Add;
        mine.Output.Add("stone", mine.Output.StackSize - mine.Output["stone"]);
        int held = mine.Output.Count;
        Work(world, 15);
        Require(said.Count(m => m.Contains("iron mine is full with no output available")) == 1 && mine.Output.Count == held, $"a full mine with nowhere to put its output must stop and say so once: {string.Join(" | ", said)}");

        var saved = new World(data, false); saved.Deserialize(world.Serialize());
        var loaded = saved.Find(mine.Id)!;
        Require(loaded.MineStage == MineStage.Open && loaded.Output!.Count == held, "a save must keep the mine's stage and output");

        // A miner dismissed mid-haul leaves their load in a recovery box.
        TakeEverything(world, world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3));
        // The miners waited in the clearing while the mine had nowhere to unload, and walk back.
        Work(world, 90, () => crew.Any(w => w.Bag.Count > 0));
        var carrier = crew.First(w => w.Bag.Count > 0);
        world.Fire(carrier);
        Require(world.Things.Any(t => t.Kind == ThingKind.Grave && t.Inventory["stone"] == 12), "dismissing a miner must not lose the load they carry");
    }

    private static void CheckAMinerFetchesNoPickaxeForAFullMineWithNowhereToUnload(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world); OpenMine(mine);
        mine.Output!.Add(Mines.Ore(mine), mine.Output.StackSize);
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, mine.Pos + Cliff.Nearest(mine.Pos).Face * 4));
        rack.Inventory.Add("steel_pick", 1);
        var miner = Staff(world, mine, JobKind.Mine);
        Work(world, 20);
        Require(rack.Inventory["steel_pick"] == 1 && miner.Tool == "", "a miner should not fetch a pickaxe for a full mine with nowhere to unload");
    }

    private static void CheckFullMineUnloadsIntoAPackingStationPackingItsOre(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world); mine.MineStage = MineStage.Open;
        var cliff = Cliff.Nearest(mine.Pos);
        var packer = world.Add(ThingKind.Packer, mine.Pos + cliff.Face * 3);
        world.SetPacking(packer, "iron_ore");
        var miner = AddWorker(world, packer.Pos + cliff.Along);
        miner.Tool = "steel_pick"; world.Assign(miner, mine);
        mine.Output!.Add("iron_ore", mine.Output.StackSize);
        Work(world, 60, () => packer.Inventory["iron_ore"] > 0);
        Require(packer.Inventory["iron_ore"] == 12, "with no chest, a load of a full mine's ore should go to a packing station packing iron ore");
    }

    private static void CheckOnlyAvailableMinersUnload(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world); mine.MineStage = MineStage.Open;
        var cliff = Cliff.Nearest(mine.Pos);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3));
        var builder = AddWorker(world, mine.Pos);
        world.SetDuty(builder, mine, JobKind.Build, true);
        var elsewhere = AddWorker(world, chest.Pos);
        var tree = world.Add(ThingKind.Tree, chest.Pos + cliff.Face * 3, 100);
        world.SetDuty(elsewhere, tree, JobKind.Gather, true);
        world.SetDuty(elsewhere, mine, JobKind.Mine, true);
        // This earlier-hired worker has no axe for the tree and no pickaxe for the mine.
        var miner = AddWorker(world, Mines.Door(mine)); miner.Tool = "steel_pick";
        world.SetDuty(miner, mine, JobKind.Mine, true);
        mine.Output!.Add("iron_ore", mine.Output.StackSize);
        Work(world, 15, () => chest.Inventory["iron_ore"] > 0);
        Require(chest.Inventory["iron_ore"] == 12 && miner.Bag.Count == 0,
            "an active miner must unload even when earlier workers only build or have a mine duty while working elsewhere");
        Require(builder.Bag.Count == 0 && elsewhere.Bag.Count == 0,
            "workers without an active mining job must not take the full mine's ore");
    }

    private static void CheckMiningIsSlowerThanDeposits(GameData data) {
        int Gathered(bool mining) {
            var world = FundedWorld(data);
            var cliff = Cliff.Of(ThingKind.Iron);
            Thing source;
            if (mining) { source = MarkMine(world); source.MineStage = MineStage.Open; } else source = world.Add(ThingKind.Iron, cliff.MineSpot, 400);
            var chest = TakeEverything(world, world.Add(ThingKind.Chest, source.Pos + cliff.Face * 2.5f));
            var worker = AddWorker(world, chest.Pos);
            worker.Tool = "steel_pick";
            world.Assign(worker, source);
            Work(world, 300);
            return chest.Inventory["iron_ore"] + worker.Bag["iron_ore"] + (source.Output?["iron_ore"] ?? 0);
        }
        int mined = Gathered(true), dug = Gathered(false);
        GD.Print($"Mine: one worker over 300 s brings out {mined} ore from a mine and {dug} from a deposit.");
        Require(mined > 0 && mined < dug, "a mine must be slower than working a deposit, at the gain of never running out");
    }

    private static void CheckEndGoalsWantAWorkingMineOnEveryOre(GameData data) {
        var world = FundedWorld(data);
        Achieve(world, "profit_week");
        Require(world.ProfitGoalMet && !world.EndGoalsComplete, "the profit goal alone must not complete the end goals");
        foreach (var ore in Cliff.Ores) { var mine = MarkMine(world, ore); if (ore != Cliff.Ores.Last()) OpenMine(mine); }
        Require(!world.EndGoalsComplete, "an unfinished mine must not count");
        OpenMine(world.Things.Last(t => t.Kind == ThingKind.Mine));
        Require(world.EndGoalsComplete, "a working mine on every ore and a week of profit must complete the end goals");
    }
}
