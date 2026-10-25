using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>Simulation checks for mines and the end goals.</summary>
public static class MineChecks
{
    private const float Step = .25f;

    public static void Run(GameData data)
    {
        CheckAlignmentAndLegacySave(data);
        CheckMarking(data);
        CheckConstruction(data);
        CheckMiningAndFullOutput(data);
        CheckMiningIsSlowerThanDeposits(data);
        CheckDailyProfitAndEndGoals(data);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Mine check failed: {message}");
    }

    /// <summary>An empty map with coins enough to pay every wage, so no worker strikes.</summary>
    private static World EmptyWorld(GameData data)
    {
        var world = new World(data, false);
        world.S.Backpack.Add(Inventory.Coins, 1000);
        return world;
    }

    private static Worker AddWorker(World world, Vector2 position)
    {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Miner {world.S.Workers.Count + 1}", Pos = position };
        world.S.Workers.Add(worker);
        return worker;
    }

    /// <summary>Runs the world with the player fed and out of the wolves' way, through nights as well, until a condition holds.</summary>
    private static void Work(World world, float seconds, Func<bool>? until = null)
    {
        for (; seconds > 0 && until?.Invoke() != true; seconds -= Step)
        {
            world.S.Hunger = world.S.Thirst = world.S.Health = 100;
            world.Tick(Step, true);
        }
    }

    /// <summary>A mine marked at the first cliff of an ore, with the player standing in front of it.</summary>
    private static Thing MarkMine(World world, string ore = "iron")
    {
        var cliff = World.CliffOf(ore);
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        return world.Build("mine", World.MineSpot(cliff) + cliff.Along + cliff.Face) ?? throw new InvalidOperationException($"Mine check failed: a mine should be marked at the {ore} cliff");
    }

    private static void CheckAlignmentAndLegacySave(GameData data)
    {
        var world = EmptyWorld(data); var expected = new Dictionary<int, Vector2>();
        foreach (var cliff in World.Cliffs)
        {
            Require(World.CliffAt(World.MineSpot(cliff)) == cliff, "each aligned site must belong to its own cliff");
            var marking = EmptyWorld(data); marking.Player = World.MineSpot(cliff) + cliff.Face * 3;
            Require(marking.Build("mine", World.MineSpot(cliff) + cliff.Along + cliff.Face)?.Pos == World.MineSpot(cliff), "every aligned site must remain markable and snap correctly");
            var outward = World.MineOut(cliff);
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
            var mine = world.Add("mine", oldSpot); expected[mine.Id] = World.MineSpot(cliff); mine.Stage = World.MineOpen; mine.Progress = 2;
            mine.Inventory.Add("wood", 7); mine.Output!.Add(World.MineOre(mine), 12);
            world.S.Remembered[mine.Id] = new Thing { Id = mine.Id, Kind = "mine", Pos = oldSpot, Stage = World.MineOpen };
            var worker = AddWorker(world, oldSpot + cliff.Face * 3); worker.Assignment = mine.Id;
        }
        world.S.Version = 4;
        var loaded = EmptyWorld(data); loaded.Deserialize(world.Serialize());
        Require(loaded.S.Version == 6, "version 4 valley saves must upgrade to the current mine layout");
        foreach (var mine in loaded.S.Things.Where(t => t.Kind == "mine"))
        {
            Require(mine.Pos.IsEqualApprox(expected[mine.Id]) && loaded.S.Remembered[mine.Id].Pos.IsEqualApprox(mine.Pos),
                "saved mines and their fog memories must use the aligned site");
            Require(mine.Stage == World.MineOpen && mine.Progress == 2 && mine.Inventory["wood"] == 7 && mine.Output![World.MineOre(mine)] == 12 && loaded.S.Workers.Any(w => w.Assignment == mine.Id),
                "aligning an older mine must preserve construction, stock, output and assigned workers");
        }
        Require(loaded.S.Things.Where(t => t.Kind == "mine").Select(t => t.Pos).Distinct().Count() == World.Cliffs.Length, "migrating all cliffs must keep distinct mine sites");
        var restored = EmptyWorld(data); restored.Deserialize(loaded.Serialize());
        Require(restored.S.Things.Where(t => t.Kind == "mine").All(m => m.Pos.IsEqualApprox(loaded.Find(m.Id)!.Pos)),
            "loading an aligned save must not move its mines again");

        // Version 5 had already moved each mine to the old spur anchor. Loading it must select by that
        // intermediate position, then preserve the same completed mine and fog-memory state.
        var v5 = EmptyWorld(data);
        var v5Expected = new Dictionary<int, Vector2>();
        foreach (var cliff in World.Cliffs)
        {
            var oldAlong = cliff.Along * Math.Sign(cliff.Along.Dot(Terrain.Seaward));
            var end = cliff.Center + oldAlong * (cliff.HalfLength + 6.7f);
            var legacy = end + cliff.Face * Math.Max(2.5f, 6 - Terrain.FromMountains(end));
            var oldV5Spot = legacy - oldAlong * 4.2f + cliff.Face * 4.2f;
            var mine = v5.Add("mine", oldV5Spot); mine.Stage = World.MineOpen; mine.Progress = 3;
            mine.Inventory.Add("wood", 9); mine.Output!.Add(World.MineOre(mine), 14);
            v5Expected[mine.Id] = World.MineSpot(cliff);
            v5.S.Remembered[mine.Id] = new Thing { Id = mine.Id, Kind = "mine", Pos = oldV5Spot, Stage = World.MineOpen };
            var worker = AddWorker(v5, oldV5Spot + cliff.Face * 3); worker.Assignment = mine.Id;
        }
        v5.S.Version = 5;
        var migratedV5 = EmptyWorld(data); migratedV5.Deserialize(v5.Serialize());
        Require(migratedV5.S.Version == 6, "version 5 mine saves must upgrade to version 6");
        foreach (var mine in migratedV5.S.Things.Where(t => t.Kind == "mine"))
        {
            Require(mine.Pos.IsEqualApprox(v5Expected[mine.Id]) && migratedV5.S.Remembered[mine.Id].Pos.IsEqualApprox(mine.Pos),
                "version 5 mines and their fog memories must move from the intermediate anchor");
            Require(mine.Stage == World.MineOpen && mine.Progress == 3 && mine.Inventory["wood"] == 9 && mine.Output![World.MineOre(mine)] == 14 && migratedV5.S.Workers.Any(w => w.Assignment == mine.Id),
                "version 5 migration must preserve mine stage, progress, stock, output and assigned workers");
        }
    }

    private static void CheckMarking(GameData data)
    {
        var world = EmptyWorld(data);
        var cliff = World.CliffOf("iron");
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        Require(world.PlacementBlock("mine", world.Player + cliff.Face * 15) != null, "a mine must be marked at a cliff, not out in the valley");
        int carried = world.S.Backpack.Count;
        var mine = MarkMine(world);
        Require(mine.Pos == World.MineSpot(cliff) && mine.Stage == 0 && world.S.Backpack.Count == carried, "marking must be free and snap to the cliff's mine spot");
        Require(world.Build("mine", World.MineSpot(cliff) - cliff.Along * 2) == null && world.S.Things.Count(t => t.Kind == "mine") == 1, "a cliff must take only one mine");
        Require(World.Cliffs.All(c => world.CanStand(World.MineSpot(c)) && world.CanStand(World.MineDoor(new Thing { Kind = "mine", Pos = World.MineSpot(c) }))), "every cliff's mine spot and entrance must be open ground");
        Require(!world.CanStand(World.MineDoor(mine) - World.MineOut(cliff) * 2), "the rock spur the tunnel runs into must block walking");
        var coal = MarkMine(world, "coal");
        Require(World.MineOre(coal) == "coal" && World.MineOre(mine) == "iron_ore", "each mine must yield the ore of its own cliff");
    }

    private static void CheckConstruction(GameData data)
    {
        var world = EmptyWorld(data);
        var mine = MarkMine(world);
        var cliff = World.CliffAt(mine.Pos);
        var chest = world.Add("chest", mine.Pos + cliff.Face * 3 - cliff.Along * 2);
        foreach (var (item, amount) in data.Mine.Materials) chest.Inventory.Add(item, amount);
        chest.Inventory.Add("wood", 20);
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, mine);
        Work(world, 30);
        Require(mine.Stage == 0 && mine.Progress == 0 && worker.Status.Contains("pickaxe"), "digging must wait until workers have a pickaxe");

        // Workers take their tools from a tool station by the storage.
        var station = world.Add("toolstation", chest.Pos + cliff.Along * 2);
        station.Inventory.Add("iron_pick", 1);
        float started = world.S.Time;
        Work(world, data.DaySeconds * 3, () => mine.Stage > 0);
        Require(mine.Stage == World.MineDug && world.S.Time - started >= world.MineStageSeconds && world.S.Time - started < data.DaySeconds * 2, $"one worker must dig the entrance in about one working day: stage {mine.Stage}, {world.S.Time - started:0}s, progress {mine.Progress:0}, {worker.Status}");

        // The surplus wood stays in storage: the site takes only what it still needs.
        Work(world, data.DaySeconds, () => mine.Stage > World.MineDug);
        Require(mine.Stage == World.MineSupplied && mine.Inventory.Count == 0 && chest.Inventory["wood"] == 20 && chest.Inventory["stone"] == 0 && chest.Inventory["iron_bar"] == 0 && worker.Bag.Count == 0,
                $"a worker must haul exactly the mine's materials from storage: chest wood {chest.Inventory["wood"]}, bag {worker.Bag.Count}");

        Work(world, 30);
        Require(mine.Stage == World.MineSupplied && mine.Progress == 0 && worker.Status.Contains("hammer"), "fitting the frame must wait for a hammer");
        Require(station.Inventory["iron_pick"] == 1, "a worker hauling materials needs no pickaxe and puts it back");
        station.Inventory.Add("wood_hammer", 1);
        started = world.S.Time;
        Work(world, data.DaySeconds * 3, () => mine.Stage == World.MineOpen);
        Require(mine.Stage == World.MineOpen && world.S.Time - started >= world.MineStageSeconds, "a worker with a hammer must fit the frame in about one working day");

        // The player can also deliver the materials by hand.
        var other = MarkMine(world, "coal");
        other.Stage = World.MineDug;
        world.Player = other.Pos + Vector2.One;
        world.S.Backpack.Add("wood", 60); world.S.Backpack.Add("stone", 10);
        world.SupplyMine(other);
        Require(other.Inventory["wood"] == data.Mine.Materials["wood"] && other.Inventory["stone"] == 10 && world.S.Backpack["wood"] == 10, "delivering from the backpack must hand over only what the mine still needs");
    }

    private static void CheckMiningAndFullOutput(GameData data)
    {
        var world = EmptyWorld(data);
        var mine = MarkMine(world);
        mine.Stage = World.MineOpen;
        var cliff = World.CliffAt(mine.Pos);
        var chest = world.Add("chest", mine.Pos + cliff.Face * 3);
        var crew = Enumerable.Range(0, 4).Select(_ => AddWorker(world, chest.Pos + cliff.Along)).ToList();
        foreach (var w in crew) { w.Tool = "iron_pick"; world.Assign(w, mine); }
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
        world.S.Things.Remove(chest);
        var said = new List<string>();
        world.Notify = said.Add;
        mine.Output.Add("stone", mine.Output.StackSize - mine.Output["stone"]);
        int held = mine.Output.Count;
        Work(world, 15);
        Require(said.Count(m => m.Contains("iron mine is full with no output available")) == 1 && mine.Output.Count == held, $"a full mine with nowhere to put its output must stop and say so once: {string.Join(" | ", said)}");

        var saved = new World(data, false); saved.Deserialize(world.Serialize());
        var loaded = saved.Find(mine.Id)!;
        Require(loaded.Stage == World.MineOpen && loaded.Output!.Count == held, "a save must keep the mine's stage and output");

        // A miner dismissed mid-haul leaves the whole stack in a recovery box.
        world.Add("chest", mine.Pos + cliff.Face * 3);
        Work(world, 3, () => crew.Any(w => w.Bag.Count > 0));
        var carrier = crew.First(w => w.Bag.Count > 0);
        world.Fire(carrier);
        Require(world.S.Things.Any(t => t.Kind == "grave" && t.Inventory["stone"] == mine.Output.StackSize), "dismissing a miner must not lose the stack they carry");
    }

    private static void CheckMiningIsSlowerThanDeposits(GameData data)
    {
        int Gathered(bool mining)
        {
            var world = EmptyWorld(data);
            var cliff = World.CliffOf("iron");
            Thing source;
            if (mining) { source = MarkMine(world); source.Stage = World.MineOpen; }
            else source = world.Add("iron", World.MineSpot(cliff), 400);
            var chest = world.Add("chest", source.Pos + cliff.Face * 2.5f);
            var worker = AddWorker(world, chest.Pos);
            worker.Tool = "iron_pick";
            world.Assign(worker, source);
            Work(world, 300);
            return chest.Inventory["iron_ore"] + worker.Bag["iron_ore"] + (source.Output?["iron_ore"] ?? 0);
        }
        int mined = Gathered(true), dug = Gathered(false);
        GD.Print($"Mine: one worker over 300 s brings out {mined} ore from a mine and {dug} from a deposit.");
        Require(mined > 0 && mined < dug, "a mine must be slower than working a deposit, at the gain of never running out");
    }

    private static void CheckDailyProfitAndEndGoals(GameData data)
    {
        var world = EmptyWorld(data);
        var worker = AddWorker(world, new Vector2(30, 30));
        worker.Wage = 26;
        var sign = world.Add("signpost", world.Player + new Vector2(2, 0));
        var crate = world.Add("crate", world.Player + new Vector2(0, 1));
        crate.Inventory.Add("berries", 12);
        Require(world.PickUpCrate(crate), "a crate of berries should be picked up");
        world.S.Backpack.TransferTo(sign.Inventory, world.S.Backpack.Items.Keys.Single(World.IsCrate), 1);
        Require(world.Sell(sign), "a crate of berries should go to the village");
        Work(world, data.TradeSeconds + .5f);
        int revenue = sign.Wages![Inventory.Coins] + sign.Output![Inventory.Coins];
        Require(revenue > 0 && world.S.DayRevenue == revenue, "coins from a returned sale must count as the day's revenue");

        void NextDawn() { world.S.Time = world.Day * data.DaySeconds - .1f; world.S.Hunger = world.S.Thirst = 100; world.Tick(.2f, true); }
        NextDawn();
        Require(world.S.Profits.SequenceEqual(new[] { revenue - 26 }) && world.S.DayRevenue == 0, "a day's profit must be its sales minus the wages charged at dawn");

        int goal = data.Goals.DailyProfit;
        for (int day = 0; day < data.Goals.ProfitDays - 1; day++) { world.S.DayRevenue = goal + 26; NextDawn(); }
        Require(world.ProfitStreak == data.Goals.ProfitDays - 1 && !world.ProfitGoalMet, "a streak one day short must not meet the profit goal");
        world.S.DayRevenue = goal + 25; NextDawn();
        Require(world.ProfitStreak == 0, "a day below the target must break the streak");
        for (int day = 0; day < data.Goals.ProfitDays; day++) { world.S.DayRevenue = goal + 26; NextDawn(); }
        Require(world.ProfitGoalMet, "enough days in a row at the target must meet the profit goal");
        NextDawn();
        Require(world.ProfitGoalMet && world.ProfitStreak == 0, "the profit goal must stay met after a later slow day");

        Require(!world.EndGoalsComplete, "the end goals must also need a working mine on every ore");
        foreach (string ore in World.MineOres) { var mine = MarkMine(world, ore); if (ore != World.MineOres.Last()) mine.Stage = World.MineOpen; }
        Require(!world.EndGoalsComplete, "an unfinished mine must not count");
        world.S.Things.Last(t => t.Kind == "mine").Stage = World.MineOpen;
        Require(world.EndGoalsComplete, "a working mine on every ore and a week of profit must complete the end goals");
    }
}
