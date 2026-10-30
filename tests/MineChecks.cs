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

/// <summary>Simulation checks for mines and the end goals.</summary>
public static class MineChecks {
    public static void Run(GameData data) {
        CheckMineSitesAlignWithCliffsAndOldSavesUpgrade(data);
        CheckMinesAreMarkedAtCliffsOnceEach(data);
        CheckWorkersDigAndFrameMineWithTheirTools(data);
        CheckMinersBringOreOutAndStopWhenOutputIsFull(data);
        CheckOnlyAvailableMinersUnload(data);
        CheckMiningIsSlowerThanDeposits(data);
        CheckDailyProfitIsSalesMinusWagesAndMeetsEndGoals(data);
    }

    private static void CheckMineSitesAlignWithCliffsAndOldSavesUpgrade(GameData data) {
        var world = FundedWorld(data); var expected = new Dictionary<int, Vector2>();
        foreach (var cliff in World.Cliffs) {
            Require(World.CliffAt(World.MineSpot(cliff)) == cliff, "each aligned site must belong to its own cliff");
            var marking = FundedWorld(data); marking.Player = World.MineSpot(cliff) + cliff.Face * 3;
            Require(marking.Build(ThingKind.Mine, World.MineSpot(cliff) + cliff.Along + cliff.Face)?.Pos == World.MineSpot(cliff), "every aligned site must remain markable and snap correctly");
            var outward = cliff.Face;
            var basis = Basis.FromEuler(WorldModels.MineYaw(World.MineSpot(cliff)));
            var renderedOut = basis * Vector3.Back;
            var renderedHeader = basis * Vector3.Right;
            Require(outward.Dot(cliff.Face) > .999f && Math.Abs(outward.Dot(cliff.Along)) < .001f,
                "a mine must face outward from its own cliff rather than a common camera direction");
            Require(new Vector2(renderedOut.X, renderedOut.Z).Dot(outward) > .999f && Math.Abs(new Vector2(renderedHeader.X, renderedHeader.Z).Dot(cliff.Along)) > .999f,
                "the rendered portal must follow the cliff and its rails must face out from it");
            var oldAlong = cliff.Along * Math.Sign(cliff.Along.Dot(Terrain.Seaward));
            var end = cliff.Center + oldAlong * (cliff.HalfLength + 6.7f);
            var oldSpot = end + cliff.Face * Math.Max(2.5f, 6 - Terrain.FromMountains(end));
            Require(World.MineSpot(cliff).IsEqualApprox(cliff.Center + cliff.Face * 4.2f), "each new entrance must project directly from its cliff center");
            var mine = world.Add(ThingKind.Mine, oldSpot); expected[mine.Id] = World.MineSpot(cliff); mine.MineStage = MineStage.Open; mine.Progress = 2;
            mine.Inventory.Add("wood", 7); mine.Output!.Add(World.MineOre(mine), 12);
            world.S.Remembered[mine.Id] = new Thing { Id = mine.Id, Kind = ThingKind.Mine, Pos = oldSpot, MineStage = MineStage.Open };
            var worker = AddWorker(world, oldSpot + cliff.Face * 3); worker.Assignment = mine.Id;
        }
        world.S.Version = 4;
        var loaded = FundedWorld(data); loaded.Deserialize(world.Serialize());
        Require(loaded.SaveVersion == 5, "version 4 valley saves must upgrade to the current mine layout");
        foreach (var mine in loaded.Things.Where(t => t.Kind == ThingKind.Mine)) {
            Require(mine.Pos.IsEqualApprox(expected[mine.Id]) && loaded.Remembered(mine.Id)!.Pos.IsEqualApprox(mine.Pos),
                "saved mines and their fog memories must use the aligned site");
            Require(mine.MineStage == MineStage.Open, "aligning an older mine must preserve its construction stage");
            Require(mine.Progress == 2, "aligning an older mine must preserve its construction progress");
            Require(mine.Inventory["wood"] == 7, "aligning an older mine must preserve its stocked wood");
            Require(mine.Output![World.MineOre(mine)] == 12, "aligning an older mine must preserve its output");
            Require(loaded.Workers.Any(w => w.Assignment == mine.Id && World.HasDuty(w, mine.Id, "mine")), "aligning an older mine must preserve its assigned workers");
        }
        Require(loaded.Things.Where(t => t.Kind == ThingKind.Mine).Select(t => t.Pos).Distinct().Count() == World.Cliffs.Length, "migrating all cliffs must keep distinct mine sites");
        var restored = FundedWorld(data); restored.Deserialize(loaded.Serialize());
        Require(restored.Things.Where(t => t.Kind == ThingKind.Mine).All(m => m.Pos.IsEqualApprox(loaded.Find(m.Id)!.Pos)),
            "loading an aligned save must not move its mines again");
    }

    private static void CheckMinesAreMarkedAtCliffsOnceEach(GameData data) {
        var world = FundedWorld(data);
        var cliff = World.CliffOf(ThingKind.Iron);
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        Require(world.PlacementBlock(ThingKind.Mine, world.Player + cliff.Face * 15) != null, "a mine must be marked at a cliff, not out in the valley");
        int carried = world.Backpack.Count;
        var mine = MarkMine(world);
        Require(mine.Pos == World.MineSpot(cliff) && mine.MineStage == MineStage.Marked && world.Backpack.Count == carried, "marking must be free and snap to the cliff's mine spot");
        Require(world.Build(ThingKind.Mine, World.MineSpot(cliff) - cliff.Along * 2) == null && world.Things.Count(t => t.Kind == ThingKind.Mine) == 1, "a cliff must take only one mine");
        Require(World.Cliffs.All(c => world.CanStand(World.MineSpot(c)) && world.CanStand(World.MineDoor(new Thing { Kind = ThingKind.Mine, Pos = World.MineSpot(c) }))), "every cliff's mine spot and entrance must be open ground");
        Require(!world.CanStand(World.MineDoor(mine) - cliff.Face * 2), "the rock spur the tunnel runs into must block walking");
        var coal = MarkMine(world, ThingKind.Coal);
        Require(World.MineOre(coal) == "coal" && World.MineOre(mine) == "iron_ore", "each mine must yield the ore of its own cliff");
    }

    private static void CheckWorkersDigAndFrameMineWithTheirTools(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world);
        var cliff = World.CliffAt(mine.Pos);
        var chest = world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3 - cliff.Along * 2);
        foreach (var (item, amount) in data.Mine.Materials) chest.Inventory.Add(item, amount);
        chest.Inventory.Add("wood", 20);
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, mine);
        Work(world, 30);
        Require(mine.MineStage == MineStage.Marked && mine.Progress == 0 && worker.Status.Contains("pickaxe"), "digging must wait until workers have a pickaxe");

        // Workers take their tools from a tool station by the storage.
        var station = world.Add(ThingKind.Toolstation, chest.Pos + cliff.Along * 2);
        station.Inventory.Add("steel_pick", 1);
        float started = world.Time;
        Work(world, data.DaySeconds * 3, () => mine.MineStage > MineStage.Marked);
        Require(mine.MineStage == MineStage.Dug && world.Time - started >= world.MineStageSeconds && world.Time - started < data.DaySeconds * 2, $"one worker must dig the entrance in about one working day: stage {mine.MineStage}, {world.Time - started:0}s, progress {mine.Progress:0}, {worker.Status}");

        // The surplus wood stays in storage: the site takes only what it still needs.
        Work(world, data.DaySeconds, () => mine.MineStage > MineStage.Dug);
        Require(mine.MineStage == MineStage.Supplied, $"a worker must finish hauling with the mine supplied: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(mine.Inventory.Count == 0, $"a supplied mine must have used up the materials hauled to it: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(chest.Inventory["wood"] == 20, $"a worker must leave the surplus wood in storage: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(chest.Inventory["stone"] == 0, $"a worker must leave stone in storage: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(chest.Inventory["iron_bar"] == 0, $"a worker must leave iron bars in storage: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");
        Require(worker.Bag.Count == 0, $"a worker must deliver everything it carried: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");

        Work(world, 30);
        Require(mine.MineStage == MineStage.Supplied && mine.Progress == 0 && worker.Status.Contains("hammer"), "fitting the frame must wait for a hammer");
        Require(station.Inventory["steel_pick"] == 1, "a worker hauling materials needs no pickaxe and puts it back");
        station.Inventory.Add("wood_hammer", 1);
        started = world.Time;
        Work(world, data.DaySeconds * 3, () => mine.MineStage == MineStage.Open);
        Require(mine.MineStage == MineStage.Open && world.Time - started >= world.MineStageSeconds, "a worker with a hammer must fit the frame in about one working day");

        // The player can also deliver the materials by hand.
        var other = MarkMine(world, ThingKind.Coal);
        other.MineStage = MineStage.Dug;
        world.Player = other.Pos + Vector2.One;
        Give(world, "wood", 60); Give(world, "stone", 10);
        world.SupplyMine(other);
        Require(other.Inventory["wood"] == data.Mine.Materials["wood"] && other.Inventory["stone"] == 10 && world.Carried("wood") == 10, "delivering from the backpack must hand over only what the mine still needs");
    }

    private static void CheckMinersBringOreOutAndStopWhenOutputIsFull(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world);
        mine.MineStage = MineStage.Open;
        var cliff = World.CliffAt(mine.Pos);
        var chest = world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3);
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
        Require(hauler.Bag["iron_ore"] == mine.Output.StackSize && mine.Output["iron_ore"] == 0, "the first miner must carry the whole full ore stack out");
        Require(mine.Output["stone"] <= stone + 3, "mining must stop once a slot is full");
        Work(world, 60, () => chest.Inventory["iron_ore"] >= mine.Output.StackSize);
        Require(chest.Inventory["iron_ore"] == mine.Output.StackSize && hauler.Bag.Count == 0 && hauler.Bag.StackSize == Worker.BagStack, "the full stack must reach the nearest storage and the miner's hands go back to a normal load");
        Work(world, 10);
        Require(mine.Output["iron_ore"] > 0, "mining must resume once the full stack is out");

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

        // A miner dismissed mid-haul leaves the whole stack in a recovery box.
        world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3);
        Work(world, 3, () => crew.Any(w => w.Bag.Count > 0));
        var carrier = crew.First(w => w.Bag.Count > 0);
        world.Fire(carrier);
        Require(world.Things.Any(t => t.Kind == ThingKind.Grave && t.Inventory["stone"] == mine.Output.StackSize), "dismissing a miner must not lose the stack they carry");
    }

    private static void CheckOnlyAvailableMinersUnload(GameData data) {
        var world = FundedWorld(data);
        var mine = MarkMine(world); mine.MineStage = MineStage.Open;
        var cliff = World.CliffAt(mine.Pos);
        var chest = world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3);
        var builder = AddWorker(world, mine.Pos);
        world.SetDuty(builder, mine, "build", true);
        var elsewhere = AddWorker(world, chest.Pos);
        var tree = world.Add(ThingKind.Tree, chest.Pos + cliff.Face * 3, 100);
        world.SetDuty(elsewhere, tree, "gather", true);
        world.SetDuty(elsewhere, mine, "mine", true);
        // This earlier-hired miner remains at another job waiting for an axe.
        var miner = AddWorker(world, World.MineDoor(mine)); miner.Tool = "steel_pick";
        world.SetDuty(miner, mine, "mine", true);
        mine.Output!.Add("iron_ore", mine.Output.StackSize);
        Work(world, 15, () => chest.Inventory["iron_ore"] == mine.Output.StackSize);
        Require(chest.Inventory["iron_ore"] == mine.Output.StackSize && miner.Bag.Count == 0,
            "an active miner must unload even when earlier workers only build or have a mine duty while working elsewhere");
        Require(builder.Bag.Count == 0 && elsewhere.Bag.Count == 0,
            "workers without an active mining job must not take the full mine stack");
    }

    private static void CheckMiningIsSlowerThanDeposits(GameData data) {
        int Gathered(bool mining) {
            var world = FundedWorld(data);
            var cliff = World.CliffOf(ThingKind.Iron);
            Thing source;
            if (mining) { source = MarkMine(world); source.MineStage = MineStage.Open; } else source = world.Add(ThingKind.Iron, World.MineSpot(cliff), 400);
            var chest = world.Add(ThingKind.Chest, source.Pos + cliff.Face * 2.5f);
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

    private static void CheckDailyProfitIsSalesMinusWagesAndMeetsEndGoals(GameData data) {
        var world = FundedWorld(data);
        var worker = AddWorker(world, new Vector2(30, 30));
        worker.Wage = 26;
        var sign = world.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("berries", 12)), 1);
        Require(world.Sell(sign), "a crate of berries should go to the village");
        Work(world, data.TradeSeconds + .5f);
        int revenue = sign.Wages![Inventory.Coins] + sign.Output![Inventory.Coins];
        Require(revenue > 0 && world.DayRevenue == revenue, "coins from a returned sale must count as the day's revenue");

        void NextDawn() => RunUntilDawn(world);
        NextDawn();
        Require(world.Profits.SequenceEqual(new[] { revenue - 26 }) && world.DayRevenue == 0, "a day's profit must be its sales minus the wages charged at dawn");

        int goal = data.Goals.DailyProfit;
        for (int day = 0; day < data.Goals.ProfitDays - 1; day++) { SetDayRevenue(world, goal + 26); NextDawn(); }
        Require(world.ProfitStreak == data.Goals.ProfitDays - 1 && !world.ProfitGoalMet, "a streak one day short must not meet the profit goal");
        SetDayRevenue(world, goal + 25); NextDawn();
        Require(world.ProfitStreak == 0, "a day below the target must break the streak");
        for (int day = 0; day < data.Goals.ProfitDays; day++) { SetDayRevenue(world, goal + 26); NextDawn(); }
        Require(world.ProfitGoalMet, "enough days in a row at the target must meet the profit goal");
        NextDawn();
        Require(world.ProfitGoalMet && world.ProfitStreak == 0, "the profit goal must stay met after a later slow day");

        Require(!world.EndGoalsComplete, "the end goals must also need a working mine on every ore");
        foreach (var ore in World.MineOres) { var mine = MarkMine(world, ore); if (ore != World.MineOres.Last()) mine.MineStage = MineStage.Open; }
        Require(!world.EndGoalsComplete, "an unfinished mine must not count");
        world.Things.Last(t => t.Kind == ThingKind.Mine).MineStage = MineStage.Open;
        Require(world.EndGoalsComplete, "a working mine on every ore and a week of profit must complete the end goals");
    }
}
