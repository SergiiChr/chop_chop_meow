using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>Deterministic checks for the playable simulation, independent of the rendered scene.</summary>
public static class SimulationChecks {
    private const float Step = .25f;

    public static void Run(GameData data) {
        InventoryLayoutChecks.Run(data);
        ChopChopMeow.Tests.WaterChecks.Run(data);
        PondChecks.Run(data);
        EconomyChecks.Run(data);
        AnimationChecks.Run();
        HumanModelChecks.Run();
        NatureModelChecks.Run();
        PileModelChecks.Run(data);
        MineRockChecks.Run();
        WorkerStateChecks.Run(data);
        CheckInventoryFilters(data);
        CheckStorageRouting(data);
        CheckWorkerGatherAndRetarget(data);
        CheckWorkerDetoursAroundLake(data);
        CheckCampfireWorkerFuel(data);
        CheckWorkerFurnaceCrew(data);
        CheckTradeAndWages(data);
        CheckMarketRoundTrip(data);
        RecoveryChecks.Run(data);
        CheckRecoveryBoxesStayPrivate(data);
        CheckRecipeSwitchLeftovers(data);
        CheckDeathAndSaveState(data);
        CheckFuelAndHeatGates(data);
        CheckAnvilNeedsHammering(data);
        CheckProcessingOutputCapacity(data);
        CheckToolGatesAndPlacement(data);
        CheckCliffsAndLoosePickup(data);
        CheckStoneYieldWithSteelTools(data);
        CheckSkyAndMoon(data);
        CheckNightAndSurvival(data);
        CheckWolfHuntAndMeat(data);
        CheckWolfThroatLeap(data);
        CheckSleepAdvancesWorld(data);
        CheckWageSlot(data);
        CheckSleepNeedsFoodAndWater(data);
        CheckSprintBurnsFood(data);
        CheckCabinAndChaseIntoLight(data);
        CheckSprintOutrunsWolf(data);
        CheckCoinStacks(data);
        CheckLegacySave(data);
        CheckCrewKeepsForgeHot(data);
        CheckWorkerSchedule(data);
        CheckFogOfWar(data);
        CheckCrateCapacityAndPacking(data);
        CheckHandCrafting(data);
        CheckBenchTools(data);
        CheckTinAndToolTiers(data);
        CheckToolStation(data);
        CheckRaking(data);
        CheckPilesAndCompost(data);
        CheckCookingAndCanning(data);
        CheckWorkerDuties(data);
        CheckFarmPlot(data);
        CheckNewFoodsAndRename(data);
        CheckMarketTakesOnlyCrates(data);
        CheckLegacySteelTools(data);
        CheckNoToolDeadlocks(data);
        CheckTorch(data);
        CheckTorchAndWolves(data);
        CheckStacking(data);
        MineChecks.Run(data);
        CheckActualProgression(data);
        MultiplayerChecks.Run(data);
        AllocationChecks.Run(data);
        GD.Print("SELF-TEST PASS");
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException($"Simulation check failed: {message}");
    }

    /// <summary>Rewrites a save into an older form, failing when a text it rewrites is missing so the fixture cannot silently stop testing anything.</summary>
    private static string Patch(string json, params (string Old, string New)[] edits) {
        foreach (var (old, replacement) in edits) {
            Require(json.Contains(old), $"save fixture text {old} is missing");
            json = json.Replace(old, replacement);
        }
        return json;
    }

    private static void SetCoins(World world, int coins) {
        world.S.Backpack.Remove(Inventory.Coins, world.Coins);
        world.S.Backpack.Add(Inventory.Coins, coins);
    }

    private static World EmptyWorld(GameData data) {
        var world = new World(data, false);
        world.S.Hunger = 100;
        world.S.Thirst = 100;
        world.S.Health = 100;
        world.S.Time = 55;
        return world;
    }

    private static void SafeTick(World world, float seconds) {
        world.S.Hunger = 100;
        world.S.Thirst = 100;
        while (seconds > 0) {
            float dt = Math.Min(Step, seconds);
            world.Tick(dt);
            seconds -= dt;
            world.S.Hunger = 100;
            world.S.Thirst = 100;
        }
    }

    /// <summary>Fills a crate at the player's feet and picks it up, returning the packed crate item now in the backpack.</summary>
    private static string PackCrate(World world, params (string Item, int Count)[] goods) {
        var crate = world.Add("crate", world.Player + new Vector2(1, 0));
        foreach (var (item, count) in goods) Require(crate.Inventory.Add(item, count) == count, $"a crate should take {count} {item}");
        var before = world.S.Backpack.Items.Keys.ToHashSet();
        Require(world.PickUpCrate(crate), "a nearby crate should be picked up");
        return world.S.Backpack.Items.Keys.Single(id => !before.Contains(id));
    }

    private static Worker AddWorker(World world, Vector2 position) {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Worker {world.S.Workers.Count + 1}", Pos = position };
        world.S.Workers.Add(worker);
        return worker;
    }

    /// <summary>
    /// Workers take jobs one at a time: a worker with jobs at two places takes turns at them, haulers and crafters split a bench's work,
    /// a stocker fills the tool rack from storage, a tender feeds compost without food when told to, and a runner carries deliveries in.
    /// </summary>
    private static void CheckWorkerDuties(GameData data) {
        var world = EmptyWorld(data);
        float morning = world.S.Time;
        var chest = world.Add("chest", new Vector2(20, 20));
        chest.Inventory.Add("wood", 20);
        var rock = world.Add("stone", new Vector2(24, 20), 30);
        var fire = world.Add("campfire", new Vector2(20, 24));
        var both = AddWorker(world, new Vector2(22, 22));
        world.SetDuty(both, rock, "gather", true);
        world.SetDuty(both, fire, "fuel", true);
        Require(both.Duties.Count == 2 && both.Assignment == rock.Id, "a worker should keep jobs at two places and start at the first");
        SafeTick(world, 60);
        Require(rock.Remaining < 30 && fire.Burn + fire.Inventory["wood"] * world.Data.CampfireSeconds > 0,
                "a worker with jobs at two places should take turns: gathering stone and feeding the fire");
        world.SetDuty(both, fire, "fuel", false);
        Require(both.Duties.Count == 1 && World.HasDuty(both, rock.Id, "gather"), "taking one job away should keep the other");
        world.Assign(both, null);

        world.S.Time = morning;
        // A bench hauler only stocks it; the work itself waits for a crafter.
        var bench = world.Add("bench", new Vector2(28, 24));
        bench.Tools!.Add("stone_chisel", 1);
        world.SetRecipe(bench, "planks");
        var hauler = AddWorker(world, new Vector2(27, 23));
        world.SetDuty(hauler, bench, "haul", true);
        SafeTick(world, 20);
        Require(bench.Inventory["wood"] > 0 && bench.Inventory["planks"] == 0 && !bench.Processing,
                "a bench hauler should stock the bench without working it");
        var crafter = AddWorker(world, new Vector2(28, 25));
        world.SetDuty(crafter, bench, "craft", true);
        SafeTick(world, 20);
        Require(bench.Inventory["planks"] + chest.Inventory["planks"] > 0, "a crafter should work the stocked bench");
        world.Assign(hauler, null); world.Assign(crafter, null);

        world.S.Time = morning;
        // A stocker brings tools from storage to the rack.
        var rack = world.Add("toolstation", new Vector2(16, 20));
        chest.Inventory.Add("steel_axe", 1);
        var stocker = AddWorker(world, new Vector2(18, 20));
        world.SetDuty(stocker, rack, "stock", true);
        SafeTick(world, 20);
        Require(rack.Inventory["steel_axe"] == 1 && chest.Inventory["steel_axe"] == 0, "a stocker should bring a stored tool to the rack");
        world.Assign(stocker, null);

        world.S.Time = morning;
        // A tender feeds compost from storage, skipping food when told to, and carries its fertilizer back.
        var compost = world.Add("pile", new Vector2(16, 24));
        Require(world.SetCompost(compost, true), "an empty pile should turn into compost");
        world.SetSkipFood(compost, true);
        compost.Inventory.Add("fertilizer", 2);
        chest.Inventory.Add("leaves", 8); chest.Inventory.Add("berries", 6);
        var tender = AddWorker(world, new Vector2(17, 22));
        world.SetDuty(tender, compost, "tend", true);
        SafeTick(world, 40);
        Require(chest.Inventory["fertilizer"] == 2 && compost.Inventory["leaves"] > 0 && chest.Inventory["berries"] == 6 && compost.Inventory["berries"] == 0,
                "a tender should carry fertilizer to storage and bring leaves, but no food when food deliveries are skipped");
        world.Assign(tender, null);

        world.S.Time = morning;
        // A trading post's runner carries what the cart delivered, other than coins, to storage.
        var store = world.Add("chest", new Vector2(26, 28));
        var sign = world.Add("signpost", new Vector2(24, 26));
        sign.Output!.Add("tin_bar", 3); sign.Output.Add(Inventory.Coins, 20);
        var runner = AddWorker(world, new Vector2(24, 25));
        world.SetDuty(runner, sign, "deliveries", true);
        SafeTick(world, 20);
        Require(store.Inventory["tin_bar"] == 3 && sign.Output["tin_bar"] == 0 && sign.Output[Inventory.Coins] == 20,
                "a runner should carry deliveries into storage and leave the coins");

        // A tool station's output filter keeps workers from taking a tool it does not let out.
        world.S.Time = morning;
        foreach (var other in world.S.Things.Where(t => t.Kind == "toolstation")) other.Inventory = new Inventory { Slots = other.Inventory.Slots, StackSize = other.Inventory.StackSize };
        var heldRack = world.Add("toolstation", new Vector2(30, 20));
        heldRack.Inventory.Add("steel_axe", 1);
        world.SetFilter(heldRack, "steel_pick", true, true);
        var oak = world.Add("tree", new Vector2(32, 22), 5);
        var cutter = AddWorker(world, new Vector2(30, 22));
        world.Assign(cutter, oak);
        SafeTick(world, 10);
        Require(cutter.Tool == "" && cutter.Missing == "axe" && heldRack.Inventory["steel_axe"] == 1, "a worker should not take a tool the station's output filter holds back");
        world.SetFilter(heldRack, "steel_pick", true, false);
        SafeTick(world, 10);
        Require(cutter.Tool == "steel_axe", "with the filter cleared, the worker should take the axe");
    }

    private static void CheckInventoryFilters(GameData data) {
        var source = new Inventory { Slots = 1 };
        var destination = new Inventory { Slots = 1 };
        source.Add("iron_bar", 4);
        source.OutputFilter.Add("wood");
        destination.InputFilter.Add("wood");
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated withdrawal must respect the source output filter");
        Require(source["iron_bar"] == 4 && destination["iron_bar"] == 0,
                "blocked automated withdrawal must leave both inventories unchanged");

        source.OutputFilter.Clear();
        source.OutputFilter.Add("iron_bar");
        destination.InputFilter.Clear();
        destination.InputFilter.Add("wood");
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated delivery must respect the destination input filter");
        destination.InputFilter.Add("iron_bar");
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 3,
                "matching source and destination filters should permit transfer");
        Require(source["iron_bar"] == 1 && destination["iron_bar"] == 3,
                "permitted transfer should move exactly the requested quantity");

        destination.InputFilter.Clear();
        Require(source.TransferTo(destination, "iron_bar", 1) == 1,
                "manual transfers should not be blocked by automation filters");
    }

    private static void CheckStorageRouting(GameData data) {
        var world = EmptyWorld(data);
        var nearestGeneral = world.Add("chest", new Vector2(11, 10));
        var explicitWood = world.Add("chest", new Vector2(17, 10));
        explicitWood.Inventory.InputFilter.Add("wood");
        Require(world.DepositFor("wood", new Vector2(10, 10))?.Id == explicitWood.Id,
                "explicitly accepting storage should outrank a nearer general chest");
        Require(world.DepositFor("stone", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "an unrestricted chest should accept items not claimed by a filtered chest");

        nearestGeneral.Inventory.Add("wood", 4);
        nearestGeneral.Inventory.OutputFilter.Add("stone");
        Require(world.SourceFor("wood", new Vector2(10, 10)) == null,
                "workers must not withdraw items forbidden by the source output filter");
        nearestGeneral.Inventory.OutputFilter.Add("wood");
        Require(world.SourceFor("wood", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "workers should use a chest once its output filter allows withdrawal");
    }

    private static void CheckWorkerGatherAndRetarget(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add("tree", new Vector2(10, 10), 5);
        var general = world.Add("chest", new Vector2(8.5f, 10));
        var filtered = world.Add("chest", new Vector2(13, 10));
        filtered.Inventory.InputFilter.Add("wood");
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        SafeTick(world, data.GatherSeconds * 1.6f / data.Items[worker.Tool].Speed + .3f);
        Require(worker.Bag["wood"] == 5 && tree.Remaining == 0,
                "gathering worker should collect actual wood from its assigned resource");
        SafeTick(world, .1f);
        Require(worker.Destination == filtered.Id,
                "worker should route gathered wood to the explicitly accepting chest");

        // The worker already chose this destination and is walking with a load. A filter edit must
        // be applied before the eventual deposit, with the carrying bag kept intact during reroute.
        filtered.Inventory.InputFilter.Clear();
        filtered.Inventory.InputFilter.Add("stone");
        SafeTick(world, .1f);
        Require(worker.Destination == general.Id && worker.Bag["wood"] == 5,
                "worker should retarget in transit when the destination filter changes");
        for (int i = 0; i < 32 && general.Inventory["wood"] == 0; i++) SafeTick(world, .25f);
        Require(general.Inventory["wood"] == 5 && filtered.Inventory["wood"] == 0 && worker.Bag["wood"] == 0,
                "retargeted load should reach the newly accepting chest without leaking into the old one");
    }

    private static void CheckWorkerFurnaceCrew(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add("reinforced", new Vector2(20, 20));
        var chest = world.Add("chest", new Vector2(23, 20));
        chest.Inventory.Add("iron_bar", 4);
        chest.Inventory.Add("coal", 6);
        var bellowsWorker = AddWorker(world, furnace.Pos + World.BellowsStand);
        var hauler = AddWorker(world, chest.Pos);
        world.Assign(bellowsWorker, furnace);
        world.Assign(hauler, furnace);

        bool sawHauling = false;
        for (int i = 0; i < 600 && furnace.Inventory["steel_bar"] + chest.Inventory["steel_bar"] == 0; i++) {
            SafeTick(world, .25f);
            sawHauling |= hauler.Destination == furnace.Id || hauler.Bag.Count > 0;
        }

        Require(bellowsWorker.Assignment == furnace.Id && furnace.Heat > 0,
                "the first of two reinforced-furnace workers should operate the bellows");
        Require(sawHauling && furnace.Inventory["steel_bar"] + chest.Inventory["steel_bar"] > 0,
                "the second furnace worker should haul recipe inputs, collect actual steel, and return it to storage");
        Require(chest.Inventory["iron_bar"] < 4 && chest.Inventory["coal"] < 6,
                "hauling should remove ingredients from the source chest");
    }

    private static void CheckWorkerDetoursAroundLake(GameData data) {
        var world = EmptyWorld(data);
        static Vector2 Bank(Vector2 direction, float past) => Terrain.Lake + direction * (Terrain.Shore(MathF.Atan2(direction.Y, direction.X)) + past);
        var tree = world.Add("tree", Bank(Terrain.Seaward, 2), 12);
        var chest = world.Add("chest", Bank(-Terrain.Seaward, 1.5f));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        bool stayedOutOfPond = true;
        for (int i = 0; i < 700 && chest.Inventory["wood"] == 0; i++) {
            SafeTick(world, .25f);
            stayedOutOfPond &= !Terrain.InLake(worker.Pos);
        }

        Require(stayedOutOfPond,
                "worker pathing should route around the pond without entering its blocked radius");
        Require(chest.Inventory["wood"] >= worker.Bag.StackSize && worker.Bag["wood"] == 0,
                "worker assigned across the pond should eventually gather wood and deliver it to accepting storage");
    }

    private static void CheckCampfireWorkerFuel(GameData data) {
        var world = EmptyWorld(data);
        var fire = world.Add("campfire", new Vector2(20, 20));
        var chest = world.Add("chest", new Vector2(24, 20));
        chest.Inventory.Add("wood", 6);
        chest.Inventory.OutputFilter.Add("stone");
        var worker = AddWorker(world, fire.Pos);
        world.Assign(worker, fire);

        SafeTick(world, 3);
        Require(chest.Inventory["wood"] == 6 && fire.Inventory["wood"] == 0,
                "campfire worker must respect the source chest output filter when fetching fuel");

        chest.Inventory.OutputFilter.Add("wood");
        bool sawFirewoodInTransit = false;
        for (int i = 0; i < 120 && chest.Inventory["wood"] == 6; i++) {
            SafeTick(world, .25f);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        for (int i = 0; i < 80 && (worker.Bag["wood"] > 0 || fire.Inventory["wood"] == 0); i++) {
            SafeTick(world, .25f);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        Require(sawFirewoodInTransit && chest.Inventory["wood"] == 3,
                "campfire worker should carry three real wood from output-enabled storage");
        SafeTick(world, .25f);
        Require(fire.Inventory["wood"] > 0 && fire.Burn > 0,
                "carried firewood should be deposited into the fire and consumed to sustain its burn");
        int storedBeforeBurn = fire.Inventory["wood"];
        SafeTick(world, fire.Burn - 29);
        Require(fire.Inventory["wood"] < storedBeforeBurn && fire.Burn > 0,
                "campfire should consume stored wood when its burn falls low");
    }

    private static void CheckTradeAndWages(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add("signpost", new Vector2(10, 10));
        var output = sign.Output!;
        world.Player = sign.Pos;
        SetCoins(world, 100);
        float coinsBeforeBuy = world.Coins;
        Require(!world.Buy(sign, "wood", 1) && world.S.Orders.Count == 0, "the village should not sell goods missing from its list");
        Require(world.Buy(sign, "water", 1), "market should accept an affordable buy order");
        Require(world.Coins == coinsBeforeBuy - world.LotPrice("water") && world.S.Orders.Count == 1 && output.Count == 0,
                "buy order should reserve coins immediately while goods remain in transit");
        SafeTick(world, 29.9f);
        Require(world.S.Orders.Count == 1 && output.Count == 0,
                "cart must not deliver before the full 30-second trip");
        SafeTick(world, .2f);
        var box = output.Items.Keys.SingleOrDefault(World.IsCrate);
        Require(world.S.Orders.Count == 0 && box == null && output["water"] == 1 && world.S.Backpack["water"] == 0,
                "cart should deliver one refillable bottle directly to the market output");

        world.S.Backpack.Add("wood", 3);
        world.S.Backpack.TransferTo(sign.Inventory, "wood", 3);
        Require(!world.Sell(sign) && world.S.Orders.Count == 0 && sign.Inventory["wood"] == 3, "loose goods must not be sold");
        sign.Inventory.TransferTo(world.S.Backpack, "wood", 3);
        // Tools have no steady amount, so even a small sale lowers their price.
        world.S.Backpack.TransferTo(sign.Inventory, PackCrate(world, ("steel_axe", 3)), 1);
        world.Player = sign.Pos;
        float coinsBeforeSale = world.Coins, quotedPrice = world.KnownPrice("steel_axe");
        Require(world.Sell(sign), "market should sell the goods loaded into it");
        Require(sign.Inventory.Count == 0 && world.Coins == coinsBeforeSale && sign.Wages![Inventory.Coins] == 0,
                "sale removes goods at dispatch but does not pay before the cart returns");
        float loweredDemand = world.S.Demand["steel_axe"];
        Require(loweredDemand < 1 && world.KnownPrice("steel_axe") == quotedPrice, "selling should lower demand while the price sheet still shows old news");
        Require(!world.Sell(sign), "an empty market has nothing to sell");
        SafeTick(world, 30.1f);
        Require(world.S.Orders.Count == 0 && sign.Wages![Inventory.Coins] > 0 && output[Inventory.Coins] == 0 && world.Coins == coinsBeforeSale,
                "sale proceeds should arrive in the wage slot when the cart returns");
        Require(world.KnownPrice("steel_axe") < quotedPrice && world.PriceAge == 0, "the returning carter should bring today's lower price");
        world.S.Time += data.DaySeconds * 2;
        Require(world.S.Demand["steel_axe"] > loweredDemand && world.PriceAge == 2, "the sheet should age by whole days");
        SafeTick(world, 60);
        Require(world.S.Demand["steel_axe"] > loweredDemand && world.S.Demand["steel_axe"] < 1 && world.KnownPrice("steel_axe") < world.UnitPrice("steel_axe", false),
                "market demand should recover while the price sheet stays stale");

        Require(world.FetchBlock(sign) == "No worker assigned." && !world.FetchPrices(sign), "fetching prices needs a worker assigned to the signpost");
        var cheap = data.HireOffers.OrderBy(o => o.Upfront).First(); var dear = data.HireOffers.OrderBy(o => o.Upfront).Last();
        Require(cheap.Wage > dear.Wage, "a smaller payment up front must come with a higher wage");
        SetCoins(world, (int)dear.Upfront);
        var runner = world.Hire(sign, dear);
        Require(runner != null && runner.Wage == dear.Wage && world.Coins == 0, "a hiring offer should charge its payment up front and fix the worker's wage");
        world.Assign(runner!, sign);
        Require(!world.S.Milestones.Contains("assigned"), "minding the signpost is not the first automation goal");
        Require(world.FetchPrices(sign) && runner!.Away > 0 && world.FetchBlock(sign) != null, "the assigned worker should set off once for fresh prices");
        SafeTick(world, data.TradeSeconds / 2);
        Require(runner!.Pos.DistanceTo(sign.Pos + Vector2.One + Terrain.Seaward * World.TripDistance) < 1, "the runner should be furthest down the valley halfway through the errand");
        SafeTick(world, data.TradeSeconds / 2 + .1f);
        Require(runner.Away == 0 && world.PriceAge == 0 && Math.Abs(world.KnownPrice("berries") - world.UnitPrice("berries", false)) < .01f && world.FetchBlock(sign) == null,
                "the runner should return with current prices");
        world.Fire(runner);

        SetCoins(world, (int)cheap.Upfront);
        var worker = world.Hire(sign, cheap);
        Require(worker != null && worker.Wage == cheap.Wage, "player should be able to hire an individually tracked worker");
        var workTarget = world.Add("tree", new Vector2(12, 10), 6);
        worker!.Tool = "steel_axe";
        world.Assign(worker, workTarget);
        SetCoins(world, 0);
        sign.Wages!.Remove(Inventory.Coins, sign.Wages[Inventory.Coins]);
        world.S.Time = data.DaySeconds * world.Day - .1f;
        SafeTick(world, .2f);
        Require(worker!.WageDebt == cheap.Wage && workTarget.Remaining == 6 && worker.Bag.Count == 0,
                "an unpaid dawn wage should put the worker on strike");
        SetCoins(world, (int)(cheap.Wage - 1));
        world.SettleDebt(worker);
        Require(worker.WageDebt == cheap.Wage, "insufficient funds must not partly clear wage debt");
        SetCoins(world, (int)cheap.Wage);
        world.SettleDebt(worker);
        Require(worker.WageDebt == 0, "paying the full owed wage should clear the strike debt");
        SetCoins(world, 0);
        world.S.Time = data.DaySeconds * world.Day - .1f;
        SafeTick(world, .2f);
        world.S.Time = data.DaySeconds * world.Day - .1f;
        SafeTick(world, .2f);
        Require(worker.WageDebt == cheap.Wage, "a striking worker should not keep adding wages to the debt");
    }

    private static void CheckMarketRoundTrip(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add("signpost", new Vector2(10, 10));
        world.Player = sign.Pos;
        SetCoins(world, 20000);
        int lot = data.CrateCapacity;
        Require(world.Buy(sign, "steel_axe", lot), "market should accept a large affordable purchase of tools");
        SafeTick(world, data.TradeSeconds + .1f);
        // Tools take a slot each, so what the output boxes cannot hold waits in a box beside the post.
        int delivered = sign.Output!["steel_axe"] + world.S.Things.Where(t => t.Kind == "grave").Sum(t => t.Inventory["steel_axe"]);
        Require(delivered == lot && sign.Output["steel_axe"] == World.MarketSlots && !sign.Output.Items.Keys.Any(World.IsCrate), "tools should arrive one by one, not in a crate, overflowing into a box");
        sign.Output.Remove("steel_axe", World.MarketSlots); world.S.Things.RemoveAll(t => t.Kind == "grave");
        string crate = PackCrate(world, ("steel_axe", lot));
        world.Player = sign.Pos;
        Require(world.S.Backpack.TransferTo(sign.Inventory, crate, 1) == 1 && world.Sell(sign), "market should accept the purchased tools back in a crate");
        SafeTick(world, data.TradeSeconds + .1f);
        int coins = world.Coins + sign.Wages![Inventory.Coins] + sign.Output[Inventory.Coins];
        float price = data.Items["steel_axe"].Price;
        Require(coins < 20000 - lot * price * 1.25f + lot * price,
                "buying tools and selling them straight back must lose coins, and a bulk sale must be paid less than its full pre-sale price");
    }

    private static void CheckRecoveryBoxesStayPrivate(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(12, 10);
        world.S.Backpack.Add("wood", 5);
        world.Die();
        var box = world.S.Things.Last(t => t.Kind == "grave");
        var tree = world.Add("tree", new Vector2(13, 10), 30);
        var chest = world.Add("chest", new Vector2(20, 10));
        var worker = AddWorker(world, new Vector2(14, 10));
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        SafeTick(world, 25);
        Require(box.Inventory["wood"] == 5 && chest.Inventory["wood"] > 0,
                "workers must store gathered goods in chests, not in the player's recovery box");

        var furnace = world.Add("furnace", new Vector2(13, 13));
        world.Player = box.Pos;
        world.S.Backpack.Add("iron_ore", 4);
        world.Die();
        var oreBox = world.S.Things.Last(t => t.Kind == "grave");
        world.Assign(worker, furnace);
        SafeTick(world, 15);
        Require(oreBox.Inventory["iron_ore"] == 4, "workers must not haul inputs out of a recovery box");

        world.Player = box.Pos;
        foreach (var id in box.Inventory.Items.Keys.ToArray()) box.Inventory.TransferTo(world.S.Backpack, id, 999);
        SafeTick(world, .1f);
        Require(world.S.Things.All(t => t.Id != box.Id), "an emptied recovery box should disappear");
    }

    private static void CheckRecipeSwitchLeftovers(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add("furnace", new Vector2(10, 10));
        world.Player = furnace.Pos + Vector2.One;
        world.SetRecipe(furnace, "iron_bar");
        furnace.Inventory.Add("iron_bar", 1);
        world.SetRecipe(furnace, "tin_bar");
        world.Collect(furnace);
        Require(world.S.Backpack["iron_bar"] == 1 && furnace.Inventory["iron_bar"] == 0,
                "collecting should also return goods made by a previous recipe");

        furnace.Inventory.Add("iron_bar", 1);
        var chest = world.Add("chest", new Vector2(14, 10));
        var worker = AddWorker(world, new Vector2(11, 11));
        world.Player = new Vector2(30, 30);
        world.Assign(worker, furnace);
        SafeTick(world, 10);
        Require(chest.Inventory["iron_bar"] == 1, "station workers should haul leftovers from a previous recipe to storage");
    }

    private static void CheckDeathAndSaveState(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(9, 12);
        world.S.Backpack.Add("iron_ore", 7);
        world.S.Backpack.Add("berries", 3);
        world.Die();
        var grave = world.S.Things.LastOrDefault(t => t.Kind == "grave");
        Require(grave != null && grave.Pos == new Vector2(9, 12) && grave.Inventory["iron_ore"] == 7,
                "death should place carried items in a recoverable box at the death location");
        Require(world.Dead && world.S.RespawnIn == data.RespawnSeconds && world.S.Backpack.Count == 0,
                "death should start the respawn timer and replace the emptied backpack");
        world.MovePlayer(Vector2.One);
        Require(world.Player == new Vector2(9, 12), "a collapsed player must not move before respawning");
        SafeTick(world, data.RespawnSeconds - .5f);
        Require(world.Dead && world.Player == new Vector2(9, 12), "the player must stay down for the whole respawn timer");
        SafeTick(world, 1);
        Require(!world.Dead && world.Player == World.Spawn && world.S.Health == 100,
                "the player should respawn healthy at the clearing once the timer ends");

        var saved = EmptyWorld(data);
        var furnace = saved.Add("furnace", new Vector2(14, 17));
        furnace.Inventory.Add("wood", 2);
        furnace.Processing = true;
        furnace.Progress = 2.5f;
        saved.S.Orders.Add(new TradeOrder { Goods = new() { ["wood"] = 4 }, Buying = true, Value = 7.5f, Elapsed = 17, Signpost = 31 });
        saved.S.Prices["wood"] = 1.1f;
        saved.S.Demand["wood"] = 1.4f;
        var worker = AddWorker(saved, new Vector2(15, 18));
        worker.Bag.Add("stone", 2);
        worker.Destination = furnace.Id;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(saved.Serialize());
        var loadedFurnace = loaded.Find(furnace.Id);
        Require(loadedFurnace != null && loadedFurnace.Processing && Math.Abs(loadedFurnace.Progress - 2.5f) < .01f,
                "save/load should preserve a batch already in progress");
        Require(loaded.S.Orders.Count == 1 && Math.Abs(loaded.S.Orders[0].Elapsed - 17) < .01f && loaded.S.Orders[0].Goods["wood"] == 4 && Math.Abs(loaded.KnownPrice("wood") - 1.1f) < .01f,
                "save/load should preserve an order and its elapsed cart time");
        Require(loaded.S.Workers.Count == 1 && loaded.S.Workers[0].Bag["stone"] == 2 &&
                loaded.S.Workers[0].Destination == furnace.Id,
                "save/load should preserve worker cargo and its current destination");
        Require(Math.Abs(loaded.S.Demand["wood"] - 1.4f) < .01f,
                "save/load should preserve market demand");
    }

    private static void CheckFuelAndHeatGates(GameData data) {
        var world = EmptyWorld(data);
        var basic = world.Add("furnace", new Vector2(10, 10));
        Require(world.FuelOf(basic) == "wood" && !world.BatchCost(basic).ContainsKey("wood") && basic.Burner != null,
                "basic furnace should burn wood from its own fuel slot by default, apart from its ingredients");
        basic.Burner!.Add("coal", 4);
        Require(world.FuelOf(basic) == "coal" && world.FuelBlock(basic, "wood") != null,
                "basic furnace should burn the coal in its fuel slot and take one kind of fuel at a time");

        var reinforced = world.Add("reinforced", new Vector2(15, 10));
        reinforced.Fuel = "wood"; // Even malformed or edited state must not make wood a valid steel fuel.
        Require(world.FuelOf(reinforced) == "coal" && world.FuelBlock(reinforced, "wood") != null,
                "the reinforced furnace should only ever burn coal, even if Fuel is tampered with");
        var steelCost = world.BatchCost(reinforced);
        Require(steelCost["coal"] == 2 && steelCost["iron_bar"] == 2 && steelCost.Count == 2,
                "a steel batch's ingredients should be iron and coal, with its fuel coal in the fuel slot");
        reinforced.Inventory.Add("iron_bar", 2);
        reinforced.Inventory.Add("coal", 2);
        world.Player = reinforced.Pos;
        reinforced.Heat = 100;
        SafeTick(world, .25f);
        Require(!reinforced.Processing && reinforced.Inventory["iron_bar"] == 2,
                "a steel batch must wait for fuel in the fuel slot, even with coal among its ingredients");
        reinforced.Heat = 0;
        reinforced.Burner!.Add("coal", 1);
        SafeTick(world, 12);
        Require(!reinforced.Processing && reinforced.Progress == 0 && reinforced.Inventory["iron_bar"] == 2 && reinforced.Inventory["coal"] == 2 && reinforced.Burner["coal"] == 1,
                "the reinforced furnace must leave a complete steel batch untouched while below the steel heat");
        reinforced.Heat = 100;
        SafeTick(world, .25f);
        Require(reinforced.Processing && reinforced.Inventory["iron_bar"] == 0 && reinforced.Inventory["coal"] == 0 && reinforced.Burner["coal"] == 0,
                "once hot enough, the reinforced furnace should consume the batch's ingredients and one coal of fuel");
        reinforced.Burner.Add("coal", 3);
        SafeTick(world, 30);
        Require(reinforced.Processing && reinforced.Progress < data.Recipes["steel_bar"].Seconds && reinforced.Burner["coal"] == 1,
                "a cooled forge should keep burning coal from its fuel slot while the steel batch stops progressing");
    }

    private static void CheckAnvilNeedsHammering(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add("anvil", new Vector2(10, 10));
        world.SetRecipe(anvil, "steel_parts");
        anvil.Inventory.Add("steel_bar", 3);
        world.Player = anvil.Pos + new Vector2(1.5f, 0);
        for (int i = 0; i < 8; i++) { world.MovePlayer(new Vector2(i % 2 == 0 ? .1f : -.1f, 0)); world.Tick(.25f); }
        Require(!anvil.Processing && anvil.Inventory["steel_bar"] == 3, "an anvil must not start while the player only walks past it");
        world.Tick(.25f);
        Require(anvil.Processing && anvil.Progress > 0, "a player standing at the anvil should hammer the batch");
        float progress = anvil.Progress;
        world.Player = anvil.Pos + new Vector2(1.5f, 0);
        for (int i = 0; i < 4; i++) { world.Gather(anvil, .25f); world.Tick(.25f); }
        Require(anvil.Progress == progress, "a busy player must not advance the anvil");

        // A paid smith stays to hammer a running batch; one on strike beside the anvil does not count.
        var chest = world.Add("chest", new Vector2(24, 10));
        chest.Inventory.Add("steel_bar", 6);
        var worker = AddWorker(world, anvil.Pos + Vector2.One);
        world.Assign(worker, anvil);
        world.Player = new Vector2(30, 30);
        worker.WageDebt = 8;
        SafeTick(world, 1);
        Require(anvil.Progress == progress, "a striking worker next to the anvil must not advance it");
        worker.WageDebt = 0;
        SafeTick(world, 1);
        Require(worker.WorkState == WorkerWorkState.Hammering && anvil.Progress > progress, "a lone smith should hammer the running batch before fetching the next load");
        SafeTick(world, 45);
        Require(chest.Inventory["steel_parts"] >= 1, "a worker hammering at the anvil should finish the batch and store it");
    }

    private static void CheckProcessingOutputCapacity(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add("furnace", new Vector2(10, 10));
        furnace.Inventory.Slots = 2;
        world.Player = furnace.Pos;
        world.S.Backpack.Add("iron_ore", 2);
        world.S.Backpack.Add("wood", 1);
        world.LoadBatch(furnace);
        world.Tick(.25f);
        Require(furnace.Processing && furnace.Inventory.Count == 0,
                "furnace should consume a valid batch before cooking begins");

        furnace.Inventory.Add("stone", 128);
        float cookSeconds = data.Recipes["iron_bar"].Seconds;
        world.Tick(cookSeconds + 1);
        Require(furnace.Processing && Math.Abs(furnace.Progress - cookSeconds) < .01f && furnace.Inventory["iron_bar"] == 0,
                "a completed batch must remain pending at full progress when the station has no output capacity");

        furnace.Inventory.Remove("stone", 64);
        world.Tick(.25f);
        Require(!furnace.Processing && furnace.Progress == 0 && furnace.Inventory["iron_bar"] == 1,
                "pending output should be added exactly once as soon as one slot becomes available");

        furnace.Inventory.Slots = 18; furnace.Inventory.Remove("stone", 64);
        furnace.Inventory.Add("iron_bar", furnace.Inventory.StackSize - 1);
        world.S.Backpack.Add("iron_ore", 2); world.S.Backpack.Add("wood", 1);
        world.LoadBatch(furnace);
        world.Tick(.25f); world.Tick(cookSeconds + 1);
        Require(furnace.Processing && furnace.Inventory["iron_bar"] == furnace.Inventory.StackSize,
                "a full one-stack output slot must hold the next batch at full progress");
        Require(world.LoadInput(furnace, "iron_ore", 999) == 0 && world.LoadInput(furnace, "stone", 1) == 0,
                "manual loading must reject empty backpacks and items the recipe does not use");
        world.S.Backpack.Add("iron_ore", 100);
        Require(world.LoadInput(furnace, "iron_ore", 999) == furnace.Inventory.StackSize && world.LoadInput(furnace, "iron_ore", 1) == 0,
                "an input slot must hold exactly one stack");
    }

    private static void CheckToolGatesAndPlacement(GameData data) {
        var world = EmptyWorld(data);
        var iron = world.Add("iron", new Vector2(10, 10), 20);
        world.S.Milestones.Add("pick");
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true,
                "the player must still carry a usable pickaxe to mine after previously forging one");
        Require(world.GatherBlock(iron, true) == null,
                "a worker's tools come from a tool station, so what the player carries does not block them");
        world.S.Backpack.Add("steel_pick", 1);
        Require(world.GatherBlock(iron) == null,
                "carrying a steel pickaxe should unlock player mining");
        world.S.Backpack.Remove("steel_pick", 1);
        world.S.Backpack.Add("steel_axe", 1);
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true && world.GatherBlock(world.Add("tree", new Vector2(12, 12), 5)) == null,
                "an axe should fell trees but not mine ore");

        world.S.Backpack.Add("wood", 8);
        Require(world.Build("chest", World.Spawn) == null,
                "building placement must reject the player arrival tile");
        var chest = world.Build("chest", World.Spawn + new Vector2(2, 0));
        Require(chest != null && chest.Kind == "chest" && world.S.Backpack["wood"] == 0,
                "building placement should accept a nearby clear tile and spend its cost");
    }

    private static void CheckStoneYieldWithSteelTools(GameData data) {
        var world = EmptyWorld(data);
        var stone = world.Add("stone", world.Player, 20);
        world.S.Backpack.Add("steel_pick", 1);

        world.Gather(stone, data.GatherSeconds / data.Items["steel_pick"].Speed);

        Require(world.S.Backpack["stone"] == 4,
                "a steel pickaxe should yield the full stone amount at its faster pace");
    }

    /// <summary>The sun crosses the sky from dawn to sunset, the moon from sunset to dawn, and the moon's light comes and goes over its cycle.</summary>
    private static void CheckSkyAndMoon(GameData data) {
        var sky = EmptyWorld(data);
        float day = data.DaySeconds;
        sky.S.Time = 0;
        Require(!sky.MoonUp && sky.SkyArc == 0 && sky.Moonlight > .99f, "a new game should start at sunrise under a full moon");
        sky.S.Time = day * data.Sunset - .1f;
        Require(!sky.MoonUp && sky.SkyArc > .99f, "the sun should reach the far end of its arc just before sunset");
        sky.S.Time = day * data.Sunset + .1f;
        Require(sky.MoonUp && sky.SkyArc < .01f, "the moon should start its arc at sunset");
        sky.S.Time = day - .1f;
        Require(sky.MoonUp && sky.SkyArc > .99f, "the moon should reach the far end of its arc just before dawn");
        sky.S.Time = day * data.MoonCycleDays / 2;
        Require(sky.Moonlight < .01f, "halfway through its cycle the moon should be new and give no light");
        sky.S.Time = day * data.MoonCycleDays;
        Require(sky.Moonlight > .99f, "the moon should be full again after a whole cycle");
    }

    private static void CheckNightAndSurvival(GameData data) {
        var dark = EmptyWorld(data);
        dark.Player = new Vector2(10, 10);
        dark.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        dark.S.Health = 100;
        dark.S.Wolves.Add(new Wolf { Pos = dark.Player + new Vector2(.8f, 0) });
        SafeTick(dark, .2f);
        Require(dark.S.Health < 100, "an unprotected player near a wolf should take damage at night");

        var lit = EmptyWorld(data);
        lit.Player = new Vector2(10, 10);
        lit.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        lit.S.Health = 100;
        var fire = lit.Add("campfire", lit.Player + new Vector2(3, 0));
        fire.Burn = 90;
        var wolf = new Wolf { Pos = lit.Player + new Vector2(1.3f, 0) };
        lit.S.Wolves.Add(wolf);
        SafeTick(lit, .2f);
        Require(lit.S.Health == 100 && wolf.Pos.DistanceTo(lit.Player) > 1.3f,
                "active campfire light should repel a nearby wolf without harming the player");
        lit.S.Time = data.DaySeconds * .2f;
        SafeTick(lit, .1f);
        Require(lit.S.Wolves.Count == 0,
                "the day cycle should clear wolves when daylight returns");

        foreach (var cause in new[] { "hunger", "thirst" }) {
            var world = EmptyWorld(data);
            world.Player = new Vector2(12, 13);
            world.S.Health = 1;
            if (cause == "hunger") world.S.Hunger = 0; else world.S.Thirst = 0;
            world.S.Backpack.Add("berries", 2);
            world.S.Backpack.Add("water", 1);
            world.Tick(1);
            var recovery = world.S.Things.LastOrDefault(t => t.Kind == "grave");
            Require(recovery != null && recovery.Pos == new Vector2(12, 13) && recovery.Inventory["berries"] == 2 && recovery.Inventory["water"] == 1,
                    $"{cause} death should recover the player's carried items at the collapse location");
            SafeTick(world, data.RespawnSeconds + .1f);
            Require(world.Player == World.Spawn && world.S.Health == 100,
                    $"{cause} death should respawn the player in a healthy state");
        }
    }

    private static World NightWorld(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        world.S.Time = data.DaySeconds * (data.NightStarts + .02f);
        return world;
    }

    private static void CheckWolfHuntAndMeat(GameData data) {
        var chase = NightWorld(data);
        var runner = new Wolf { Pos = chase.Player + new Vector2(8, 0) };
        chase.S.Wolves.Add(runner);
        SafeTick(chase, 1);
        Require(runner.Pos.DistanceTo(chase.Player) < 5, "a wolf should close at least three tiles per second");

        var hunt = NightWorld(data);
        hunt.S.Backpack.Add("steel_axe", 1);
        hunt.S.Backpack.Add("wood", 1);
        var prey = new Wolf { Pos = hunt.Player + new Vector2(1.5f, 0), Health = 1 };
        hunt.S.Wolves.Add(prey);
        hunt.Attack();
        var carcass = hunt.S.Things.SingleOrDefault(t => t.Kind == "wolf_corpse");
        Require(!hunt.S.Wolves.Contains(prey) && carcass != null && carcass.Remaining == 4,
                "a killed wolf should leave a carcass with meat instead of retreating");
        hunt.S.Time = data.DaySeconds * 1.2f;
        SafeTick(hunt, .5f);
        Require(hunt.S.Things.Contains(carcass!) && hunt.S.Wolves.Count == 0, "a carcass should outlast the night");

        hunt.Player = carcass!.Pos + new Vector2(1, 0);
        while (hunt.GatherBlock(carcass) == null) hunt.Gather(carcass, data.GatherSeconds);
        Require(hunt.S.Backpack["raw_meat"] == 4, "butchering a carcass should yield all of its raw meat");
        SafeTick(hunt, .1f);
        Require(!hunt.S.Things.Contains(carcass), "a fully butchered carcass should disappear");

        hunt.S.Hunger = 40;
        hunt.Consume("raw_meat");
        Require(hunt.S.Backpack["raw_meat"] == 4 && hunt.S.Hunger == 40, "raw meat must be cooked before it can be eaten");
        var furnace = hunt.Add("furnace", hunt.Player + new Vector2(1, 1));
        hunt.SetRecipe(furnace, "burnt_steak");
        hunt.LoadBatch(furnace);
        for (int i = 0; i < 28; i++) hunt.Tick(Step);
        hunt.Collect(furnace);
        Require(hunt.S.Backpack["burnt_steak"] == 2 && hunt.S.Backpack["raw_meat"] == 2 && hunt.S.Backpack["wood"] == 0,
                "the stone furnace should cook two raw meat with one fuel");
        hunt.S.Hunger = 40;
        hunt.Consume("burnt_steak");
        Require(hunt.S.Backpack["burnt_steak"] == 1 && hunt.S.Hunger == 80, "cooked meat should be edible and filling");
    }

    private static void CheckWolfThroatLeap(GameData data) {
        var healthy = NightWorld(data);
        healthy.S.Health = 80;
        var stalker = new Wolf { Pos = healthy.Player + new Vector2(3, 0), Attack = 5 };
        healthy.S.Wolves.Add(stalker);
        healthy.Tick(.2f);
        Require(stalker.Leap == 0 && !healthy.S.Mauled, "wolves should not leap at a player above half health");

        var sheltered = NightWorld(data);
        sheltered.S.Health = 40;
        sheltered.Add("campfire", sheltered.Player + new Vector2(2, 0)).Burn = 90;
        var circling = new Wolf { Pos = sheltered.Player + new Vector2(-3, 0), Attack = 5 };
        sheltered.S.Wolves.Add(circling);
        sheltered.Tick(.2f);
        Require(circling.Leap == 0 && !sheltered.S.Mauled, "campfire light should prevent the throat leap");

        var maul = NightWorld(data);
        maul.S.Health = 49;
        foreach (var (item, count) in new[] { ("berries", 3), ("burnt_steak", 2), ("raw_meat", 1), ("water", 2), ("steel_axe", 1), ("wood", 4) })
            maul.S.Backpack.Add(item, count);
        var leaper = new Wolf { Pos = maul.Player + new Vector2(3, 0), Attack = 5 };
        var packmate = new Wolf { Pos = maul.Player + new Vector2(-9, 4), Attack = 5 };
        maul.S.Wolves.AddRange(new[] { leaper, packmate });
        maul.Tick(.1f);
        Require(leaper.Leap > 0 && !maul.S.Mauled, "a nearby wolf should leap at a player on half health");
        maul.Tick(World.LeapSeconds);
        Require(maul.S.Mauled && leaper.Feeding && leaper.Pinning && leaper.Pos.DistanceTo(maul.Player) < 1.6f,
                "the leap should land at the throat and pin the player");
        var pinned = maul.Player;
        maul.MovePlayer(Vector2.Right);
        maul.Attack();
        Require(maul.Player == pinned && leaper.Health == 4, "a pinned player can neither move nor fight back");
        for (int i = 0; i < 40 && !maul.Dead; i++) maul.Tick(.1f);
        Require(maul.Dead, "a pinned player should be killed within seconds");
        for (int i = 0; i < 20; i++) maul.Tick(.1f);
        Require(packmate.Feeding && packmate.Pos.DistanceTo(pinned) < 1.6f && maul.S.Wolves.Contains(leaper),
                "the rest of the pack should run in and stay to feed");
        var box = maul.S.Things.Single(t => t.Kind == "grave");
        Require(box.Inventory["berries"] == 0 && box.Inventory["burnt_steak"] == 0 && box.Inventory["raw_meat"] == 0,
                "a wolf kill should leave no food in the recovery box");
        Require(box.Inventory["water"] == 2 && box.Inventory["steel_axe"] == 1 && box.Inventory["wood"] == 4,
                "a wolf kill should keep everything except food in the recovery box");
        SafeTick(maul, data.RespawnSeconds);
        Require(!maul.Dead && !maul.S.Mauled && !maul.S.Wolves.Contains(leaper) && !maul.S.Wolves.Contains(packmate),
                "respawning should end the feast and free the player");
    }

    private static void CheckSleepAdvancesWorld(GameData data) {
        var world = EmptyWorld(data);
        world.Player = World.Spawn;
        world.S.Time = data.DaySeconds * (data.NightStarts + .01f);
        world.S.WageDay = world.Day;
        var hut = world.Add("hut", world.Player + new Vector2(2, 0));
        var sign = world.Add("signpost", world.Player + new Vector2(1, 2));
        var furnace = world.Add("furnace", world.Player + new Vector2(-2, 1));
        furnace.Processing = true;
        var order = new TradeOrder { Goods = new() { ["wood"] = 1 }, Buying = false, Value = 12, Signpost = sign.Id };
        world.S.Orders.Add(order);
        var worker = AddWorker(world, sign.Pos);
        SetCoins(world, (int)worker.Wage + 3);
        world.S.Wolves.Add(new Wolf { Pos = world.Player + new Vector2(8, 0) });
        float priorTime = world.S.Time;
        float priorHunger = world.S.Hunger;
        world.Sleep(hut);
        Require(world.S.Time > priorTime && world.Day > 1,
                "sleep should advance the world clock through dawn");
        Require(world.S.Orders.Count == 0 && sign.Wages![Inventory.Coins] == 12 - worker.Wage && world.Coins == worker.Wage + 3,
                "trade settlement should fill the wage slot and the daily wage should come out of it during sleep");
        Require(worker.WageDebt == 0,
                "a funded worker should receive the normal daily wage during sleep");
        Require(!furnace.Processing && furnace.Inventory["iron_bar"] > 0,
                "furnace production should continue while the player sleeps");
        Require(world.S.Wolves.Count == 0 && world.S.Hunger < priorHunger,
                "nighttime sleep should advance survival needs and clear wolves at dawn");
    }

    private static void CheckFogOfWar(GameData data) {
        var fresh = new World(data);
        var far = World.CliffOf("coal").Center;
        Require(fresh.Explored(World.Spawn) && !fresh.Explored(far),
                "a new game should reveal only the clearing around the player");
        Require(fresh.Known.All(t => t.Pos.DistanceTo(World.Spawn) <= data.SightRadius),
                "things beyond sight should stay unknown until seen");

        var world = EmptyWorld(data);
        world.Player = new Vector2(10, 10);
        var tree = world.Add("tree", new Vector2(12, 10), 42);
        var hidden = world.Add("stone", new Vector2(44, 44), 64);
        var furnace = world.Add("furnace", new Vector2(9, 12));
        furnace.Processing = true;
        SafeTick(world, Step);
        Require(world.Known.Contains(tree) && !world.Known.Any(t => t.Id == hidden.Id),
                "only things within the sight circle should become known");

        world.Player = new Vector2(40, 10);
        SafeTick(world, Step);
        tree.Remaining = 0;
        world.S.Things.Remove(world.Add("chest", new Vector2(11, 11)));
        var memory = world.Known.Single(t => t.Id == tree.Id);
        Require(memory != tree && memory.Remaining == 42 && world.Explored(new Vector2(12, 10)) && !world.Known.Single(t => t.Id == furnace.Id).Processing,
                "explored ground out of sight should keep the last seen look, not the live state or its activity");

        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Explored(new Vector2(12, 10)) && !loaded.Explored(new Vector2(44, 44)) && loaded.Known.Single(t => t.Id == tree.Id).Remaining == 42,
                "save/load should keep explored ground and remembered things");

        world.Player = new Vector2(10, 10);
        SafeTick(world, Step);
        Require(world.Known.Single(t => t.Id == tree.Id).Remaining == 0,
                "looking again should replace the memory with the current state");
    }

    private static void GatherUntil(World world, Thing resource, string item, int quantity) {
        world.Player = resource.Pos;
        int attempts = 0;
        while (world.S.Backpack[item] < quantity && attempts++ < 200) {
            int before = world.S.Backpack[item];
            world.Gather(resource, world.Data.GatherSeconds);
            Require(world.S.Backpack[item] > before, $"gathering {item} should produce raw material");
            SafeTick(world, world.Data.GatherSeconds);
        }
        Require(world.S.Backpack[item] >= quantity, $"gathered enough {item} for the progression");
    }

    private static void PickUp(World world, Thing loose) {
        world.Player = loose.Pos;
        world.Gather(loose, 0);
        Require(world.Find(loose.Id) == null, $"picking up {loose.Kind} should take the whole piece at once");
    }

    private static void CheckCliffsAndLoosePickup(GameData data) {
        var world = new World(data);
        foreach (var cliff in World.Cliffs) {
            Require(!world.CanStand(cliff.Center) && Terrain.FromMountains(cliff.Center) < 0 && Terrain.FromMountains(cliff.Center + cliff.Face * 2.6f) > 0,
                    $"each {cliff.Ore} cliff should block movement and stand at the foot of a mountain range");
            Require(world.S.Things.Count(t => t.Kind == cliff.Ore && cliff.DistanceTo(t.Pos) < 3) == 2, $"each {cliff.Ore} cliff should have deposits at its foot");
        }
        foreach (var cliff in new[] { World.CliffOf("iron"), World.CliffOf("coal") }) {
            Require(world.S.Things.Where(t => t.Kind == cliff.Ore).All(t => World.Cliffs.Any(c => c.Ore == cliff.Ore && c.DistanceTo(t.Pos) < 3)),
                    $"{cliff.Ore} deposits should only spawn at the foot of their cliffs");
        }
        {
            var cliff = World.CliffOf("coal");
            var loose = world.S.Things.Where(t => t.IsLoose).ToList();
            Require(loose.Count > 0 && loose.All(t => cliff.DistanceTo(t.Pos) is > 3 and < 12 && world.CanStand(t.Pos)),
                    $"loose {cliff.Ore} should spread further than the deposits but stay near the nearer cliff");
            Require(loose.All(a => loose.All(b => a == b || a.Pos.DistanceTo(b.Pos) >= 2)), $"loose {cliff.Ore} pieces should lie apart");
        }

        foreach (var (kind, spacing) in new[] { ("stone", 15f), ("berries", 14f) }) {
            var found = world.S.Things.Where(t => t.Kind == kind).ToList();
            Require(found.Count >= 15 && found.All(t => t.Pos.DistanceTo(World.Spawn) >= 9 && found.All(o => o == t || o.Pos.DistanceTo(t.Pos) >= spacing)),
                    $"{kind} should be scattered singly away from the clearing, so more of it takes exploring");
        }
        Require(world.S.Things.All(t => Terrain.Open(t.Pos) || t.IsDeposit),
                "nothing should be placed in the sea, the lake or the mountains");
        var trees = world.S.Things.Where(t => t.Kind == "tree").ToList();
        float woods = trees.Count(t => Terrain.FromMountains(t.Pos) < 15), plains = trees.Count(t => Terrain.FromMountains(t.Pos) >= 30);
        Require(trees.Count > 400 && woods > plains * 3, $"trees should thicken toward the mountains ({trees.Count} in all, {woods} near them, {plains} on the plains)");
        Require(world.S.Things.Count(t => t.Kind == "tree" && t.Pos.DistanceTo(World.Spawn) < 9) == 1,
                "the clearing should keep only its one nearby tree");

        // Loose pieces need no tool and no gather time; the whole piece goes into the backpack.
        var piece = world.S.Things.First(t => t.Kind == "loose_coal");
        world.S.Backpack.Remove("steel_axe", 99);
        Require(world.GatherBlock(piece) == null, "loose coal should need no tool");
        PickUp(world, piece);
        Require(world.S.Backpack["coal"] == 2, "a loose coal piece should give coal");

        world = EmptyWorld(data);
        world.Add("chest", new Vector2(12, 10));
        var worker = AddWorker(world, new Vector2(10, 10));
        var assigned = world.Add("loose_coal", new Vector2(10.5f, 10), 2);
        world.Assign(worker, assigned);
        SafeTick(world, Step);
        Require(world.Find(assigned.Id) == null && worker.Bag["coal"] == 2,
                "a worker should pick up a loose piece at once, and it should not linger as an empty pile");
    }

    private static Thing BuildAt(World world, string kind, Vector2 position) {
        world.Player = position + new Vector2(-2, 0);
        var built = world.Build(kind, position);
        Require(built != null, $"build {kind} at a clear nearby location");
        return built!;
    }

    private static void CompleteBatches(World world, Thing station, string output, int quantity, bool pump = false) {
        int limit = 3000;
        while (station.Inventory[output] < quantity && limit-- > 0) {
            if (pump && station.Heat < 90) world.Pump(station);
            SafeTick(world, .25f);
        }
        Require(limit > 0 && station.Inventory[output] >= quantity,
                $"station should complete {quantity} {output} batch(es): kind={station.Kind}, recipe={station.Recipe}, output={station.Inventory[output]}, processing={station.Processing}, progress={station.Progress:0.0}, player={world.Player}, stationPos={station.Pos}, health={world.S.Health:0.0}, time={world.S.Time:0.0}");
    }

    private static void CraftByHand(World world, string recipe, int times) {
        for (int i = 0; i < times; i++) Require(world.Craft(recipe), $"{recipe} should be craftable by hand: {world.CraftBlock(recipe)}");
        for (int limit = 400; world.S.Crafting.Count > 0 && limit > 0; limit--) SafeTick(world, .25f);
        Require(world.S.Crafting.Count == 0, $"hand crafting {recipe} should finish: rate={world.CraftRate}, progress={world.S.CraftTime}, slots={world.S.Backpack.UsedSlots}, items={string.Join(",", world.S.Backpack.Items.Select(p => p.Key + "=" + p.Value))}, room={world.S.Backpack.Room(world.Data.Recipes[recipe].Output)}");
    }

    /// <summary>A spot in the open middle of the valley, so the progression run has room to build.</summary>
    private static Vector2 Site(float x, float y) => new(x + 60, y + 60);

    private static void CheckActualProgression(GameData data) {
        var world = EmptyWorld(data);
        float startTime = world.S.Time;
        world.S.Backpack.Add("steel_axe", 1);
        world.S.Backpack.Add("berries", 10);
        world.S.Backpack.Add("wood", 6);

        var tree = world.Add("tree", Site(8, 8), 300);
        var stone = world.Add("stone", Site(8, 14), 300);
        var bush = world.Add("berries", Site(14, 8), 60);
        var iron = world.Add("iron", Site(12, 14), 120);
        var coal = world.Add("coal", Site(16, 8), 120);
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true && world.GatherBlock(coal)?.Contains("pickaxe") == true, "deposits should wait for a pickaxe the player does not start with");
        GatherUntil(world, tree, "wood", 64);
        GatherUntil(world, stone, "stone", 58);
        GatherUntil(world, bush, "berries", 48);

        // A stone chisel from bare stone opens wood crafting by hand, which leads to the bench, its mallet and crates.
        CraftByHand(world, "stone_chisel", 1);
        CraftByHand(world, "planks", 6);
        CraftByHand(world, "planks", 5);
        CraftByHand(world, "wood_parts", 2);
        CraftByHand(world, "wood_hammer", 1);
        Require(world.S.Backpack["planks"] == 44 && world.S.Backpack["wood_parts"] == 3 && world.S.Backpack["wood_hammer"] == 1,
                "hand crafting should turn wood into planks, parts and a mallet");
        var bench = BuildAt(world, "bench", Site(28, 28));
        Require(world.SlotTool(bench, "wood_hammer") && world.BenchTool(bench, "hammer") == "wood_hammer", "the mallet should go into the bench's hammer slot");
        world.SetRecipe(bench, "crate");
        world.LoadBatch(bench, 4);
        CompleteBatches(world, bench, "crate", 4);
        world.Collect(bench);
        Require(world.S.Backpack["crate"] == 4 && world.S.Milestones.Contains("crate"), "the bench should make crates from planks with the mallet slotted");

        // The first pickaxe is bought with berry money: four crates of berries pay for it.
        var sign = BuildAt(world, "signpost", Site(34, 25));
        for (int i = 0; i < 4; i++) {
            world.Player = sign.Pos;
            var box = world.PlaceCrate("crate", sign.Pos + new Vector2(0, 2));
            Require(box != null && world.S.Backpack.TransferTo(box!.Inventory, "berries", 12) == 12 && world.PickUpCrate(box), "a crate of berries should be packed from the backpack");
            world.S.Backpack.TransferTo(sign.Inventory, world.S.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix)), 1);
        }
        Require(world.Sell(sign), "signpost should dispatch crates of berries");
        SafeTick(world, data.TradeSeconds + .2f);
        sign.Wages!.TransferTo(world.S.Backpack, Inventory.Coins, sign.Wages[Inventory.Coins]);
        sign.Output!.TransferTo(world.S.Backpack, Inventory.Coins, sign.Output[Inventory.Coins]);
        Require(world.S.Milestones.Contains("sold") && world.Coins >= world.LotPrice("steel_pick"), $"four crates of berries should pay for a steel pickaxe: coins={world.Coins}");
        Require(world.Buy(sign, "steel_pick", 1), "the village should sell a steel pickaxe");
        SafeTick(world, data.TradeSeconds + .2f);
        Require(sign.Output.TransferTo(world.S.Backpack, "steel_pick", 1) == 1 && world.S.Milestones.Contains("pick") && world.GatherBlock(iron) == null,
                "the bought pickaxe should arrive at the signpost and unlock mining deposits");

        GatherUntil(world, iron, "iron_ore", 22);
        GatherUntil(world, coal, "coal", 6);
        var furnace = BuildAt(world, "furnace", Site(25, 25));
        world.LoadBatch(furnace, 11);
        Require(furnace.Inventory["iron_ore"] == 22 && furnace.Burner!["wood"] == 11,
                "loading the furnace should transfer the recipe inputs, and one fuel per batch into its fuel slot");
        CompleteBatches(world, furnace, "iron_bar", 11);
        world.Player = furnace.Pos;
        world.Collect(furnace);
        Require(world.S.Backpack["iron_bar"] == 11, "player should collect bars actually smelted by the furnace from mined ore");

        var reinforced = BuildAt(world, "reinforced", Site(31, 25));
        BuildAt(world, "campfire", Site(31, 28));
        world.Player = reinforced.Pos;
        int bars = world.S.Backpack["iron_bar"];
        world.LoadBatch(reinforced, 1);
        Require(reinforced.Inventory["iron_bar"] == 2 && reinforced.Inventory["coal"] == 2 && reinforced.Burner!["coal"] == 1,
                "the reinforced furnace should load iron plus mined coal, and coal fuel into its fuel slot");
        CompleteBatches(world, reinforced, "steel_bar", 2, true);
        world.Player = reinforced.Pos;
        world.Collect(reinforced);
        Require(world.S.Backpack["steel_bar"] == 2 && world.S.Backpack["iron_bar"] == bars - 2,
                "one smelt of 2 iron bars and mined coal should make two steel bars from real inputs");

        var anvil = BuildAt(world, "anvil", Site(28, 25));
        world.Player = anvil.Pos + Vector2.One;
        world.SetRecipe(anvil, "steel_parts");
        world.LoadBatch(anvil, 2);
        CompleteBatches(world, anvil, "steel_parts", 2);
        world.Collect(anvil);
        world.Player = bench.Pos + Vector2.One;
        world.SetRecipe(bench, "steel_pick");
        world.LoadBatch(bench);
        CompleteBatches(world, bench, "steel_pick", 1);
        world.SetRecipe(bench, "crate");
        world.LoadBatch(bench);
        CompleteBatches(world, bench, "crate", 1);
        world.Collect(bench);
        Require(world.S.Backpack["steel_pick"] == 2 && world.S.Backpack["crate"] == 1 && world.S.Milestones.Contains("steel_tool"),
                "the bench should craft a steel pickaxe from steel parts and a crate from planks");

        world.Player = sign.Pos;
        var crate = world.PlaceCrate("crate", sign.Pos + new Vector2(0, 2));
        Require(crate != null && world.S.Backpack["crate"] == 0, "an empty crate should be placed from the backpack");
        world.S.Backpack.TransferTo(crate!.Inventory, "steel_pick", 1);
        Require(world.PickUpCrate(crate), "the packed crate should be picked up");
        string packed = world.S.Backpack.Items.Keys.Single(World.IsCrate);
        Require(world.Packed(packed)?["steel_pick"] == 1 && world.CanSell(packed), "the picked-up crate should still hold the steel pickaxe");
        world.Player = sign.Pos;
        world.S.Backpack.TransferTo(sign.Inventory, packed, 1);
        Require(world.Sell(sign), "signpost should dispatch a crate of steel tools");
        float beforeReturn = world.Coins;
        SafeTick(world, data.TradeSeconds - .1f);
        Require(world.Coins == beforeReturn && world.S.Orders.Count == 1,
                "steel-tool payment should remain in transit until the trade delay elapses");
        SafeTick(world, .2f);
        sign.Wages!.TransferTo(world.S.Backpack, Inventory.Coins, sign.Wages[Inventory.Coins]);
        var offer = data.HireOffers[1];
        Require(world.Coins >= offer.Upfront && world.S.Milestones.Contains("sold_steel"),
                $"returned cart should fund hire and mark the sale: coins={world.Coins:0.0}, upfront={offer.Upfront:0.0}, soldSteel={world.S.Milestones.Contains("sold_steel")}, health={world.S.Health:0.0}, time={world.S.Time:0.0}, backpackSteel={world.S.Backpack["steel_pick"]}");

        var worker = world.Hire(sign, offer);
        Require(worker != null, "sale proceeds should fund a new worker");
        var chest = BuildAt(world, "chest", Site(37, 25));
        // A woodcutter takes an axe from a tool station, so the player racks theirs: a station is 8 planks and 4 stone.
        GatherUntil(world, tree, "wood", 2);
        GatherUntil(world, stone, "stone", 4);
        CraftByHand(world, "planks", 2);
        var station = BuildAt(world, "toolstation", Site(37, 28));
        Require(world.S.Backpack.TransferTo(station.Inventory, "steel_axe", 1) == 1, "the player should rack their axe in the tool station");
        var workerTree = world.S.Things.FirstOrDefault(t => t.Kind == "tree" && t.Remaining > 0);
        Require(workerTree != null, "a remaining tree should be available for the hired worker");
        world.Assign(worker!, workerTree);
        Require(worker!.Assignment == workerTree!.Id && worker.Job == "gather" && world.S.Milestones.Contains("assigned"),
                "player should assign the hired worker to gather from a resource");
        // Selling for the first pickaxe takes the opening day, so a worker hired at night starts in the morning.
        for (int i = 0; i < 4000 && !world.OnDuty(worker); i++) { SafeTick(world, .25f); world.S.Health = 100; }
        for (int i = 0; i < 480 && chest.Inventory["wood"] == 0; i++) SafeTick(world, .25f);
        Require(chest.Inventory["wood"] > 0 && worker.Bag["wood"] == 0,
                "hired worker should gather actual wood and deliver it to a built chest");
        // Reuse the worker's real gathered output after lending them the player's axe.
        for (int i = 0; i < 8000 && chest.Inventory["wood"] < 26; i++) SafeTick(world, .25f);
        world.Player = chest.Pos;
        Require(chest.Inventory.TransferTo(world.S.Backpack, "wood", 26) == 26, "worker output should fund the shelter and fire");
        GatherUntil(world, stone, "stone", 12);
        var pond = world.Add("water", Terrain.WaterPoint(World.Spawn), 9999);
        world.Player = Terrain.WaterPoint(World.Spawn);
        world.Gather(pond, data.GatherSeconds);
        world.Player = Site(3, 3);
        Require(world.Build("hut", Site(5, 3)) != null, "gathered materials should build the opening shelter");
        Require(world.Build("campfire", Site(3, 6)) != null, "gathered materials should build the opening fire");

        world.Tick(.01f, true);
        Require(world.GoalComplete, "objective chain should reach the first automation milestone");
        GD.Print($"Progression simulation elapsed: {world.S.Time - startTime:0} seconds.");
    }

    private static void CheckCrateCapacityAndPacking(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(10, 10);
        var crate = world.Add("crate", new Vector2(11, 10)).Inventory;
        int cap = data.CrateCapacity;
        Require(crate.ShownSlots == cap, "an empty crate should show one slot per item it can hold");
        Require(crate.Add("berries", cap + 5) == cap && crate.ShownSlots == 1 && crate.Room("wood") == 0, "a full stack fills the crate and leaves no more slots");
        crate.Remove("berries", cap / 2);
        Require(crate.ShownSlots == 1 + cap - cap / 2, "half a stack leaves one single-item slot per free unit");
        crate.Remove("berries", cap);
        foreach (var item in new[] { "wood", "stone", "berries", "water", "coal", "iron_ore", "planks", "wood_parts", "iron_bar", "steel_bar", "tin_bar", "steel_parts" }.Take(cap)) crate.Add(item, 1);
        Require(crate.UsedSlots == cap && crate.Count == cap && crate.Room("raw_meat") == 0, "a crate should also take as many different single items");

        // Packed crates keep their contents through the backpack, a save and placing them again.
        crate.Items.Clear(); crate.Add("raw_meat", 5); crate.Add("steel_axe", 2);
        string packed = world.S.Backpack.Items.Keys.Any(World.IsCrate) ? "" : PackCrate(world);
        Require(world.S.Things.Count(t => t.Kind == "crate") == 1, "picking up should remove only the picked crate");
        var placed = world.S.Things.Single(t => t.Kind == "crate");
        Require(world.PickUpCrate(placed), "a filled crate should be picked up");
        string meat = world.S.Backpack.Items.Keys.Single(id => World.IsCrate(id) && id != packed);
        Require(world.S.Backpack.StackOf(meat) == 1 && world.Describe(meat).Contains("5 Raw meat") && world.S.Backpack["crate"] == 1, "a packed crate is one unstackable item that names its contents, and an empty one is a plain crate");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Packed(meat)?["steel_axe"] == 2, "save/load should keep what a packed crate holds");
        loaded.Player = new Vector2(10, 10);
        var again = loaded.PlaceCrate(meat, new Vector2(12, 12));
        Require(again != null && again.Inventory["raw_meat"] == 5 && again.Inventory.Capacity == cap && loaded.Packed(meat) == null && loaded.S.Backpack[meat] == 0,
                "placing a packed crate should put its contents back into the placed crate");
        Require(world.DepositFor("wood", new Vector2(10, 10)) == null, "crates are manual storage that workers never fill");
    }

    private static void CheckHandCrafting(GameData data) {
        var world = EmptyWorld(data);
        world.S.Backpack.Add("wood", 3);
        Require(world.CraftBlock("planks")?.Contains("chisel") == true && !world.Craft("planks"), "wood crafting by hand needs a chisel");
        Require(world.CraftBlock("crate") != null && world.CraftBlock("steel_pick") != null, "crates and metal tools are bench work");
        world.S.Backpack.Add("stone", 2);
        Require(world.Craft("stone_chisel") && world.S.Backpack["stone"] == 0, "a stone chisel should need only 2 stone and take them at once");
        SafeTick(world, data.Recipes["stone_chisel"].Seconds - .5f);
        Require(world.S.Backpack["stone_chisel"] == 0, "hand crafting should take its time");
        SafeTick(world, 1);
        Require(world.S.Backpack["stone_chisel"] == 1 && world.BestTool("chisel") == "stone_chisel", "the chisel should arrive once its time is up");
        Require(world.Craft("planks") && world.Craft("wood_parts") && world.S.Backpack["wood"] == 1, "two wood recipes should queue with a chisel");
        world.MovePlayer(new Vector2(.5f, 0));
        SafeTick(world, data.Recipes["planks"].Seconds + .25f);
        Require(world.S.Backpack["planks"] == 4 && world.S.Crafting.Count == 1, "crafting by hand should keep going on the go, one recipe at a time");
        world.CancelCraft(0);
        Require(world.S.Crafting.Count == 0 && world.S.Backpack["wood"] == 2 && world.S.CraftTime == 0, "cancelling should give the materials back");
        world.Craft("wood_parts");
        world.S.Backpack.Remove("stone_chisel", 1);
        SafeTick(world, 20);
        Require(world.S.Backpack["wood_parts"] == 0 && world.CraftRate == 0, "a queued recipe should wait while its tool is not carried");
        world.S.Backpack.Add("stone_chisel", 1);
        SafeTick(world, data.Recipes["wood_parts"].Seconds);
        Require(world.S.Backpack["wood_parts"] == 2, "it should continue once the tool is back");
    }

    private static void CheckTorch(GameData data) {
        var world = EmptyWorld(data);
        world.S.Backpack.Add("wood", 3);
        world.UseTorch(Vector2.Zero);
        Require(!world.Craft("torch") && world.S.Torch == 0 && world.S.TorchKindling == 0, "a torch needs a chisel to make and a torch to light");
        world.S.Backpack.Add("stone_chisel", 1);
        Require(world.Craft("torch") && world.S.Backpack["wood"] == 0, "a torch should take 3 wood and nothing else");
        SafeTick(world, data.Recipes["torch"].Seconds + .25f);
        world.S.Backpack.Add("torch", 1);
        world.UseTorch(Vector2.Zero);
        Require(world.S.Backpack["torch"] == 1 && world.S.Torch == data.CampfireSeconds && !world.S.TorchLit, "the first use should take a torch in hand and start striking flint");
        SafeTick(world, World.KindleSeconds - .2f);
        Require(!world.S.TorchLit, "lighting should take its time");
        SafeTick(world, .3f);
        Require(world.S.TorchLit && world.S.Backpack["torch"] == 1, "the torch should catch after the lighting animation");

        // Steady swings keep it burning and flare the light; four in two seconds put it out.
        for (int i = 0; i < 4; i++) { world.UseTorch(Vector2.Right); Require(world.TorchFlare > 0, "a swing should flare the light"); SafeTick(world, .7f); }
        Require(world.S.TorchLit, "swinging at a steady pace should keep the torch burning");
        SafeTick(world, World.WildWindow);
        string said = "";
        world.Notify += message => said = message;
        for (int i = 0; i < World.WildSwings; i++) { world.UseTorch(Vector2.Right); SafeTick(world, .65f); }
        float left = world.S.Torch;
        Require(!world.S.TorchLit && said == "Torch went off due to wild swinging.", "wild swinging should put the torch out with a message");
        SafeTick(world, 5);
        Require(world.S.Torch == left, "an unlit torch should keep its burn time");
        world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.S.TorchLit && world.S.Backpack["torch"] == 1 && world.S.Torch < left && world.S.Torch > left - 1, "relighting should not use a new torch or reset its time");
        SafeTick(world, world.S.Torch + .25f);
        Require(world.S.Torch == 0 && !world.S.TorchLit && world.S.Backpack["torch"] == 1, "a torch should burn out for good");
        world.UseTorch(Vector2.Zero); world.Die();
        Require(world.S.Torch == 0 && !world.S.TorchLit && world.S.TorchKindling == 0, "a torch should go out when the player falls");
    }

    private static World TorchNight(GameData data) {
        var world = NightWorld(data);
        world.S.Backpack.Add("torch", 1); world.UseTorch(Vector2.Zero); world.Tick(World.KindleSeconds + .1f);
        Require(world.S.TorchLit, "the torch fixture should be lit");
        return world;
    }

    /// <summary>Ticks a night with only the given wolves, so new arrivals do not interfere.</summary>
    private static void WolfTick(World world, float seconds, params Wolf[] keep) {
        for (float t = 0; t < seconds; t += .1f) {
            world.S.Hunger = world.S.Thirst = 100; world.Tick(.1f);
            world.S.Wolves.RemoveAll(w => !keep.Contains(w));
        }
    }

    private static void CheckTorchAndWolves(GameData data) {
        var creeping = TorchNight(data);
        var lone = new Wolf { Pos = creeping.Player + new Vector2(8, 0), Health = 4 };
        creeping.S.Wolves.Add(lone);
        WolfTick(creeping, 3, lone);
        Require(lone.Pos.DistanceTo(creeping.Player) < 6.5f && lone.Pos.DistanceTo(creeping.Player) > 3.5f, "a lone wolf should creep in slowly even with a torch");

        var scared = TorchNight(data);
        var wolf = new Wolf { Pos = scared.Player + new Vector2(6, 0), Health = 4 };
        var behind = new Wolf { Pos = scared.Player + new Vector2(-6, 0), Health = 4 };
        scared.S.Wolves.AddRange(new[] { wolf, behind });
        scared.UseTorch(Vector2.Right);
        Require(wolf.Scared && !behind.Scared, "a swing should scare only the wolves it is aimed at");
        scared.S.Wolves.Remove(behind);
        WolfTick(scared, 8, wolf);
        Require(scared.S.Health == 100 && Math.Abs(wolf.Pos.DistanceTo(scared.Player) - World.StalkDistance) < .3f, "a scared wolf should follow at the edge of the light without biting");

        var hit = TorchNight(data);
        var close = new Wolf { Pos = hit.Player + new Vector2(2, 0), Health = 4 };
        hit.S.Wolves.Add(close);
        hit.UseTorch(Vector2.Right);
        float struck = close.Pos.DistanceTo(hit.Player);
        Require(close.Stagger == World.StaggerSeconds && struck > 3, "a direct torch hit should knock the wolf back and stagger it");
        WolfTick(hit, World.StaggerSeconds / 2, close);
        Require(close.Pos.DistanceTo(hit.Player) == struck, "a staggering wolf should hold still");
        WolfTick(hit, 1, close);
        Require(close.Health == 3 && close.Flee > 0 && close.Pos.DistanceTo(hit.Player) > struck + 1, "a direct torch hit should hurt the wolf and send it running");

        var edge = TorchNight(data);
        edge.Player = new Vector2(World.MapSize - 5, 20);
        var doomed = new Wolf { Pos = edge.Player + new Vector2(1.8f, 0), Health = 1 };
        edge.S.Wolves.Add(doomed);
        edge.UseTorch(Vector2.Right);
        var corpse = edge.S.Things.Single(t => t.Kind == "wolf_corpse");
        Require(corpse.Pos.X == World.MapSize - 3 && corpse.Pos.X > 45,
            "a wolf killed by a torch near the map edge should leave its corpse within current map bounds");

        var gathering = TorchNight(data);
        for (int i = 0; i < 100; i++) { gathering.S.Hunger = gathering.S.Thirst = 100; gathering.Tick(.1f); }
        Require(gathering.S.Wolves.Count < World.PackSize, "the pack should gather slowly");

        var pack = TorchNight(data);
        var wolves = Enumerable.Range(0, World.PackSize).Select(i => new Wolf { Pos = pack.Player + Vector2.Right.Rotated(i * Mathf.Tau / World.PackSize) * World.StalkDistance, Health = 4 }).ToArray();
        pack.S.Wolves.AddRange(wolves);
        pack.UseTorch(Vector2.Right);
        Require(wolves.All(w => !w.Scared), "a complete pack should ignore a swing");
        WolfTick(pack, 4, wolves);
        Require(pack.S.Health < 100, $"{World.PackSize} wolves around a torch should attack together");
    }

    private static void CheckStacking(GameData data) {
        var world = EmptyWorld(data);
        world.S.Backpack.Add("water", 3); world.S.Backpack.Add("steel_axe", 2);
        Require(world.S.Backpack.UsedSlots == 5, "each refillable bottle and each tool should take its own slot");
        var crate = world.NewCrate();
        Require(crate.Add("steel_pick", data.CrateCapacity) == data.CrateCapacity && crate.UsedSlots == data.CrateCapacity, "a crate should hold a full crate of single tools");
        var chest = world.Add("chest", new Vector2(10, 10));
        Require(crate.TransferTo(chest.Inventory, "steel_pick", 8) == World.ChestStacks, "a chest should take one tool per slot");
        var hands = new Worker().Bag;
        Require(hands.Add("steel_axe", 9) == Worker.BagStack && hands.Add("wood", 1) == 0, "a worker should carry five tools, one per slot");
        var mixed = new Worker().Bag;
        Require(mixed.Add("wood", 3) == 3 && mixed.Add("stone", 1) == 1 && mixed.Add("steel_pick", 1) == 1 && mixed.Add("coal", 1) == 0, "a worker should carry five things in all, of any kinds");
    }

    private static void CheckBenchTools(GameData data) {
        var world = EmptyWorld(data);
        var bench = world.Add("bench", new Vector2(10, 10));
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        world.S.Backpack.Add("wood", 4); world.S.Backpack.Add("stone_chisel", 1); world.S.Backpack.Add("wood_hammer", 1);
        world.SetRecipe(bench, "planks");
        world.LoadBatch(bench, 2);
        SafeTick(world, 6);
        Require(!bench.Processing && bench.Inventory["planks"] == 0, "a bench recipe should wait for its tool in the slot");
        Require(!world.SlotTool(bench, "wood_hammer") || world.BenchTool(bench, "chisel") == null, "a hammer must not fill the chisel slot");
        Require(world.SlotTool(bench, "stone_chisel") && world.S.Backpack["stone_chisel"] == 0 && !world.SlotTool(bench, "stone_chisel"), "the chisel should go into its one slot");
        SafeTick(world, .25f);
        SafeTick(world, data.Recipes["planks"].Seconds / data.BenchSpeed);
        Require(bench.Inventory["planks"] == 4, "a bench should work twice as fast as crafting by hand");
        var worker = AddWorker(world, bench.Pos + Vector2.One);
        var chest = world.Add("chest", new Vector2(14, 10));
        chest.Inventory.Add("wood", 2);
        world.UnslotTool(bench, "stone_chisel");
        world.Assign(worker, bench);
        world.Player = new Vector2(30, 30);
        SafeTick(world, 12);
        Require(worker.Status.Contains("chisel") && world.S.Backpack["stone_chisel"] == 1, "a bench worker should say which tool the bench is missing");
        bench.Tools!.Add("stone_chisel", 1);
        SafeTick(world, 40);
        Require(chest.Inventory["planks"] >= 8 && chest.Inventory["wood"] == 0, "a bench worker should fetch wood, work the bench and store the planks");
    }

    /// <summary>Tin is mined with a pickaxe and smelted like iron; cans and the mortar need steel tools, and anything better than the tier asked for will do.</summary>
    private static void CheckTinAndToolTiers(GameData data) {
        var world = EmptyWorld(data);
        var fresh = new World(data);
        var cliff = World.CliffOf("tin");
        Require(fresh.S.Things.Count(t => t.Kind == "tin" && cliff.DistanceTo(t.Pos) < 3) == 2 && !fresh.S.Things.Any(t => t.Kind == "loose_tin"),
                "the tin cliff should have two deposits and no loose pieces");
        var tin = world.Add("tin", Site(0, 0), 400);
        world.Player = tin.Pos;
        Require(world.GatherBlock(tin)?.Contains("pickaxe") == true, "tin should need a pickaxe");
        world.S.Backpack.Add("steel_pick", 1);
        GatherUntil(world, tin, "tin_ore", 4);

        var furnace = world.Add("furnace", Site(3, 0));
        world.Player = furnace.Pos + new Vector2(1.2f, 0);
        world.S.Backpack.Add("wood", 2);
        world.SetRecipe(furnace, "tin_bar");
        world.LoadBatch(furnace, 2);
        SafeTick(world, data.Recipes["tin_bar"].Seconds * 2 + 1);
        world.Collect(furnace);
        Require(world.S.Backpack["tin_bar"] == 2 && world.S.Backpack["tin_ore"] == 0, "the stone furnace should smelt 2 tin ore into a tin bar");

        var bench = world.Add("bench", Site(0, 4));
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        world.S.Backpack.Add("wood_hammer", 1);
        world.SetRecipe(bench, "tin_can");
        Require(world.SlotTool(bench, "wood_hammer"), "a wooden mallet should fit the hammer slot");
        world.LoadBatch(bench);
        SafeTick(world, 20);
        Require(!bench.Processing && bench.Inventory["tin_can"] == 0 && world.BenchTool(bench, "hammer", 2) == null, "tin cans should wait while only a wooden mallet is slotted");
        world.UnslotTool(bench, "wood_hammer");
        world.S.Backpack.Add("steel_hammer", 1);
        world.SlotTool(bench, "steel_hammer");
        SafeTick(world, data.Recipes["tin_can"].Seconds / data.BenchSpeed / data.Items["steel_hammer"].Speed + 1);
        Require(bench.Inventory["tin_can"] == 4, "a steel hammer should turn one tin bar into four cans");

        world.S.Backpack.Add("stone", 3); world.S.Backpack.Add("stone_chisel", 1);
        Require(world.CraftBlock("mortar") == "Needs a steel chisel in your backpack.", "a stone chisel should not carve a mortar");
        world.S.Backpack.Add("steel_chisel", 1);
        Require(world.BestTool("chisel") == "steel_chisel" && world.Craft("mortar"), "a steel chisel should carve a mortar from stone by hand");
        SafeTick(world, data.Recipes["mortar"].Seconds / 2 + .25f);
        Require(world.S.Backpack["mortar"] == 1, "a steel chisel should work twice as fast");
        world.S.Backpack.Add("wood", 1);
        Require(world.Craft("planks"), "a steel chisel should do everything a stone chisel does");
        SafeTick(world, data.Recipes["planks"].Seconds / 2 + .25f);
        Require(world.S.Backpack["planks"] == 4, "planks should come twice as fast with a steel chisel");

        var sign = world.Add("signpost", Site(6, 6));
        world.Player = sign.Pos;
        SetCoins(world, 100);
        Require(world.Buy(sign, "wheat_seeds", 1) && world.Buy(sign, "fertilizer", 1), "the village should sell wheat seeds and fertilizer");
        SafeTick(world, data.TradeSeconds + .1f);
        var bags = sign.Output!.Items.Keys.Where(World.IsCrate).Select(id => world.Packed(id)!).ToList();
        Require(bags.Any(b => b["wheat_seeds"] == data.Market.Buys["wheat_seeds"]) && bags.Any(b => b["fertilizer"] == data.Market.Buys["fertilizer"]), "seeds and fertilizer should arrive by the crate");
    }

    /// <summary>Workers take the best tool their job needs from a tool station, say once when none is there, and put it back when reassigned or going home.</summary>
    private static void CheckToolStation(GameData data) {
        var world = EmptyWorld(data);
        var said = new List<string>();
        world.Notify = said.Add;
        var tree = world.Add("tree", new Vector2(10, 10), 400);
        var rock = world.Add("stone", new Vector2(10, 14), 400);
        world.Add("chest", new Vector2(12, 12));
        var station = world.Add("toolstation", new Vector2(14, 10));
        world.Player = station.Pos + Vector2.One;
        Require(!world.Holds(station, "wood") && world.Holds(station, "steel_axe"), "a tool station should hold tools only");
        var worker = AddWorker(world, tree.Pos + Vector2.One);
        worker.Name = "Fern";
        world.Assign(worker, tree);
        SafeTick(world, 5);
        Require(worker.Status == "Needs an axe to chop wood" && worker.Bag.Count == 0 && said.Count(m => m == "Fern needs an axe to chop wood. Put one in a tool station.") == 1,
                "a woodcutter without an axe should wait and say so once");
        station.Inventory.Add("steel_axe", 1);
        SafeTick(world, 8);
        Require(worker.Tool == "steel_axe" && station.Inventory["steel_axe"] == 0 && tree.Remaining < 400,
                "the worker should take the axe once one is there and start chopping");
        world.Assign(worker, rock);
        for (int i = 0; i < 80 && worker.Tool != ""; i++) SafeTick(world, .25f);
        Require(worker.Tool == "" && station.Inventory["steel_axe"] == 1, "a worker moved to stone, which needs no tool, should put the axe back");
        SafeTick(world, 10);
        Require(rock.Remaining < 400, "stone needs no tool");

        // A full station leaves a worker holding the wrong tool, who must wait rather than mine with an axe.
        var iron = world.Add("iron", new Vector2(18, 14), 400);
        var miner = AddWorker(world, iron.Pos + Vector2.One); miner.Tool = "steel_axe";
        station.Inventory.Add("steel_pick", station.Inventory.Room("steel_pick"));
        world.Assign(miner, iron);
        SafeTick(world, 10);
        Require(miner.Tool == "steel_axe" && iron.Remaining == 400 && miner.Status == "No room to put back the steel axe",
                "a worker who cannot put back the wrong tool should wait instead of mining with it");
        station.Inventory.Remove("steel_pick", 1);
        for (int i = 0; i < 120 && miner.Tool != "steel_pick"; i++) SafeTick(world, .25f);
        Require(miner.Tool == "steel_pick" && station.Inventory["steel_axe"] == 2, "with room again the worker should swap the axe for a pickaxe");
        world.S.Workers.Remove(miner); station.Inventory.Remove("steel_pick", 99); station.Inventory.Remove("steel_axe", 1);

        world.Assign(worker, tree);
        SafeTick(world, 10);
        Require(worker.Tool == "steel_axe", "back on the tree the worker should fetch the axe again");
        world.S.Time = (world.Day - 1 + data.DuskStarts) * data.DaySeconds - .5f;
        for (int i = 0; i < 400 && !worker.Home; i++) SafeTick(world, .25f);
        Require(worker.Home && worker.Tool == "" && station.Inventory["steel_axe"] == 1 && worker.Assignment == tree.Id,
                "a worker going home should put the axe back and keep the assignment");

        worker.Home = false; worker.Commute = 0; worker.Tool = "steel_pick";
        world.Fire(worker);
        Require(world.S.Things.Any(t => t.Kind == "grave" && t.Inventory["steel_pick"] == 1), "a dismissed worker's tool should be left in a recovery box");
    }

    /// <summary>Leaves lie under oaks, need a rake, and fall again every day.</summary>
    private static void CheckRaking(GameData data) {
        var fresh = new World(data);
        var oaks = fresh.S.Things.Where(t => t.Kind == "tree" && t.Id % 3 == 0).Select(t => t.Pos + World.LeafOffset).ToHashSet();
        var patches = fresh.S.Things.Where(t => t.Kind == "leaves").ToList();
        Require(patches.Count > oaks.Count * .8f && patches.All(p => oaks.Contains(p.Pos)), "oaks should drop patches of leaves beside their trunks");
        var world = EmptyWorld(data);
        var leaves = world.Add("leaves", new Vector2(10, 10), World.LeafPatch);
        world.Player = leaves.Pos;
        Require(world.GatherBlock(leaves)?.Contains("rake") == true, "raking should need a rake");
        world.S.Backpack.Add("rake", 1);
        GatherUntil(world, leaves, "leaves", World.LeafPatch);
        Require(leaves.Remaining == 0 && world.GatherBlock(leaves) != null, "a raked patch should be empty until it refills");
        SafeTick(world, data.DaySeconds - world.S.Time % data.DaySeconds + 1);
        Require(leaves.Remaining == World.LeafPatch, "leaves should fall again by the next day");

        var station = world.Add("toolstation", new Vector2(14, 10));
        station.Inventory.Add("rake", 1);
        world.Add("chest", new Vector2(12, 12));
        var worker = AddWorker(world, station.Pos);
        world.Assign(worker, leaves);
        world.Player = new Vector2(30, 30);
        SafeTick(world, 20);
        Require(worker.Tool == "rake" && leaves.Remaining < World.LeafPatch, "a worker should fetch the rake and rake leaves");
    }

    /// <summary>Piles snap into grids, hold one resource whose filters follow what is in them, and compost turns leaves and food into fertilizer.</summary>
    private static void CheckPilesAndCompost(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin + new Vector2(0, 2);
        world.S.Backpack.Add("wood", 40);
        var pile = world.Build("pile", origin);
        Require(pile != null && world.S.Backpack["wood"] == 32 && pile!.Inventory.Slots == World.PileStacks, "a pile should cost 8 wood and hold 14 stacks");
        Require(world.Add("chest", origin + new Vector2(0, 8)).Inventory.Slots == 5, "a chest should hold a third of a pile, 5 stacks");
        Require(world.Snap("pile", origin + new Vector2(3.4f, .4f)) == origin + new Vector2(3, 0) && world.Snap("pile", origin + new Vector2(9, 0)) == origin + new Vector2(9, 0),
                "a pile placed beside another should snap edge to edge, and one far away should not");
        var east = world.Build("pile", origin + new Vector2(3.3f, 0)) ?? throw new InvalidOperationException("a pile beside another should build");
        Require(world.PlacementBlock("pile", origin + new Vector2(2, 0)) != null && east.Pos == origin + new Vector2(3, 0), "piles must not overlap but may touch");
        Require(world.CanStand(origin), "a pile should be walkable");

        world.Player = origin;
        Require(world.S.Backpack.TransferTo(pile!.Inventory, "wood", 10) == 10 && world.HoldBlock(pile, "stone") != null, "the first resource in should be the only one the pile takes");
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.SetEquals(new[] { "wood" }) && pile.Inventory.OutputFilter.SetEquals(new[] { "wood" }), "a pile's filters should follow its one resource");
        var chest = world.Add("chest", origin + new Vector2(0, 5));
        Require(world.DepositFor("wood", chest.Pos) == pile && world.DepositFor("stone", origin) != pile, "workers should prefer the matching pile and keep other goods out of it");
        pile.Inventory.Remove("wood", 10);
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.Count == 0 && world.Holds(pile, "stone"), "emptying a pile should clear its filters");

        Require(world.SetCompost(east, true) && east.Inventory.InputFilter.Contains("leaves") && east.Inventory.InputFilter.Contains("crushed_berries") && !east.Inventory.InputFilter.Contains("stone")
                && east.Inventory.OutputFilter.SetEquals(new[] { "fertilizer" }), "compost should take leaves, grass and any food, and give fertilizer");
        east.Inventory.Add("leaves", 6); east.Inventory.Add("burnt_steak", 2);
        Require(world.HoldBlock(east, "stone") != null && !world.SetCompost(east, false), "compost should refuse stone and stay compost while mixed");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 1 && east.Inventory.Count == 5, "4 inputs should rot into 1 fertilizer in an in-game hour");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 2 && east.Inventory.Items.Keys.All(k => k == "fertilizer"), "compost should keep going while it has 4 inputs");
        Require(world.SourceFor("fertilizer", origin) == east, "workers should be able to take the fertilizer out");

        var station = world.Add("toolstation", origin + new Vector2(-4, 4));
        station.Inventory.Add("rake", 1);
        var leaves = world.Add("leaves", origin + new Vector2(-4, -4), World.LeafPatch);
        var worker = AddWorker(world, station.Pos);
        world.Assign(worker, leaves);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 40);
        Require(east.Inventory["leaves"] + east.Inventory["fertilizer"] * 4 > 8, "raked leaves should go to the compost, which takes them explicitly");
    }

    /// <summary>
    /// The cooking table's mortar recipes only advance while someone works the slotted mortar, its fire recipes cook unattended on their fuel, and dough needs no fire.
    /// The canning station seals food into tin cans over its fire.
    /// </summary>
    private static void CheckCookingAndCanning(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add("cooktable", new Vector2(20, 20));
        world.Player = table.Pos + new Vector2(1, 0);
        world.S.Backpack.Add("mortar", 1); world.S.Backpack.Add("stone_chisel", 1); world.S.Backpack.Add("berries", 8);
        Require(!world.SlotTool(table, "stone_chisel") && world.SlotTool(table, "mortar") && world.BenchTool(table, "mortar") == "mortar", "the cooking table should only slot a mortar");
        world.SetRecipe(table, "crushed_berries");
        Require(!world.BatchCost(table).ContainsKey("wood"), "crushing berries should burn no fuel");
        world.LoadBatch(table);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 30);
        Require(table.Inventory["crushed_berries"] == 0, "the mortar should not work without someone at it");
        var cook = AddWorker(world, table.Pos + new Vector2(0, 1));
        world.Assign(cook, table);
        SafeTick(world, 15);
        Require(table.Inventory["crushed_berries"] == 4, "a worker should work the mortar into crushed berries");
        world.Assign(cook, null);

        world.Player = table.Pos + new Vector2(1, 0);
        world.Collect(table);
        world.SetRecipe(table, "berry_jam");
        table.Fuel = "coal"; world.S.Backpack.Add("coal", 1);
        Require(world.BatchCost(table).SequenceEqual(new Dictionary<string, int> { ["crushed_berries"] = 2 }) && world.FuelOf(table) == "coal", "jam should take crushed berries and burn the chosen fuel");
        world.LoadBatch(table);
        world.Player = new Vector2(40, 40);
        SafeTick(world, data.Recipes["berry_jam"].Seconds + 1);
        Require(table.Inventory["berry_jam"] == 1 && table.Burner!["coal"] == 0, "jam should cook on its own over the fire");
        world.SetRecipe(table, "dough");
        Require(world.BatchCost(table).Keys.Order().SequenceEqual(new[] { "flour", "water" }), "dough should need no fire");

        var cannery = world.Add("cannery", new Vector2(24, 20));
        world.SetRecipe(cannery, "canned_meat_stew");
        cannery.Inventory.Add("meat_stew", 1); cannery.Inventory.Add("tin_can", 1); cannery.Burner!.Add("wood", 1);
        SafeTick(world, data.Recipes["canned_meat_stew"].Seconds + 1);
        Require(cannery.Inventory["canned_meat_stew"] == 1 && cannery.Inventory.Count == 1 && cannery.Burner.Count == 0, "the canning station should seal stew into a can over wood");

        var furnace = world.Add("furnace", new Vector2(28, 20));
        world.SetRecipe(furnace, "bread");
        Require(world.BatchCost(furnace).Keys.SequenceEqual(new[] { "dough" }) && world.FuelOf(furnace) == "wood", "bread should bake from dough over the furnace's fire");

        // By hand, the mortar is carried like any other tool.
        world.Player = table.Pos + new Vector2(1, 0);
        world.UnslotTool(table, "mortar");
        world.S.Backpack.Add("berries", 4);
        Require(world.CraftBlock("crushed_berries") == null, "berries should be crushed by hand with a carried mortar");
        world.S.Backpack.Remove("mortar", 1);
        Require(world.CraftBlock("crushed_berries") != null, "crushing berries by hand should need the mortar");
    }

    /// <summary>
    /// A farm plot snaps beside a pile, sows itself from seeds and fertilizer, shows its growth over its days, and is harvested by the player and a worker sharing one progress.
    /// The worker carries the wheat to storage rather than to compost and the grass to compost, keeps the seeds and sows again.
    /// </summary>
    private static void CheckFarmPlot(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin + new Vector2(1.5f, 2.5f);
        world.S.Backpack.Add("wood", 16); world.S.Backpack.Add("wheat_seeds", 1); world.S.Backpack.Add("fertilizer", 1);
        var compost = world.Build("pile", origin) ?? throw new InvalidOperationException("a pile should build on open ground");
        world.SetCompost(compost, true);
        var farm = world.Build("farm", origin + new Vector2(3.4f, .3f)) ?? throw new InvalidOperationException("a farm plot should build beside a pile");
        Require(farm.Pos == origin + new Vector2(3, 0), "a farm plot should snap edge to edge with a pile");
        Require(world.CanStand(farm.Pos) && !world.Holds(farm, "stone") && world.GatherBlock(farm) != null, "a plot is walkable, takes only farm goods, and has nothing to harvest before sowing");

        world.S.Backpack.TransferTo(farm.Inventory, "wheat_seeds", 1); world.S.Backpack.TransferTo(farm.Inventory, "fertilizer", 1);
        world.Tick(.1f);
        Require(farm.Stage == World.FarmGrowing && farm.Inventory.Count == 0 && World.CropLook(farm, world.GrowSeconds) == 1, "seeds and fertilizer should sow the plot into soil");
        var looks = new List<int>();
        for (int day = 0; day < 3; day++) { SafeTick(world, data.DaySeconds - 1); looks.Add(World.CropLook(farm, world.GrowSeconds)); SafeTick(world, 1); }
        Require(looks.SequenceEqual(new[] { 1, 2, 3 }) && farm.Stage == World.FarmRipe && World.CropLook(farm, world.GrowSeconds) == 4,
                $"wheat should grow through soil, straws and green plants to ripe in {data.Farm.GrowDays} days: {string.Join(",", looks)}");

        // A plot harvested with nobody to haul keeps its seed in the output: nothing sows it back on its own.
        var lone = world.Add("farm", origin + new Vector2(0, -6));
        lone.Stage = World.FarmRipe; world.Player = lone.Pos;
        world.Gather(lone, world.HarvestSeconds); world.Tick(1);
        Require(lone.Stage == World.FarmFallow && lone.Output!["wheat_seeds"] == data.Farm.Yield["wheat_seeds"] && lone.Inventory["wheat_seeds"] == 0, "harvested seed should wait in the output until someone moves it into the plot");
        world.S.Things.Remove(lone);

        world.Player = farm.Pos;
        world.Gather(farm, world.HarvestSeconds / 2);
        Require(farm.Stage == World.FarmRipe && Math.Abs(farm.Progress - world.HarvestSeconds / 2) < .01f, "the player should harvest by hand, without a tool");
        var chest = world.Add("chest", origin + new Vector2(3, 4));
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, farm);
        world.S.Backpack.Add(Inventory.Coins, 500);
        // The player waits out the nights in a cabin, out of the wolves' reach.
        var hut = world.Add("hut", new Vector2(40, 40));
        world.Player = hut.Pos + new Vector2(0, 2); world.S.Sheltered = hut.Id;
        for (int i = 0; i < 4 * data.DaySeconds && farm.Stage == World.FarmRipe; i++) SafeTick(world, 1);
        Require(farm.Stage == World.FarmFallow && farm.Output!["wheat_seeds"] + farm.Inventory["wheat_seeds"] == data.Farm.Yield["wheat_seeds"], "a worker should finish the shared harvest into the plot");
        SafeTick(world, 30);
        Require(farm.Inventory["wheat_seeds"] == data.Farm.Sow["wheat_seeds"] && chest.Inventory["wheat_seeds"] == data.Farm.Yield["wheat_seeds"] - data.Farm.Sow["wheat_seeds"],
                "the hauler should move one sowing's seeds from the output into the plot and carry the rest to storage");
        Require(chest.Inventory["wheat"] == data.Farm.Yield["wheat"] && compost.Inventory["wheat"] == 0 && compost.Inventory["grass"] == 1, "the wheat should go to storage rather than rot, and the grass to the compost");
        chest.Inventory.Add("fertilizer", 1);
        SafeTick(world, 60);
        Require(farm.Stage == World.FarmGrowing && farm.Inventory["wheat_seeds"] == 0 && chest.Inventory["fertilizer"] == 0, "the worker should bring fertilizer and sow again from the seeds moved back in");
    }

    /// <summary>Foods restore what their data says, raw ones only say they need cooking, and saves from before the rename keep their cooked meat.</summary>
    private static void CheckNewFoodsAndRename(GameData data) {
        var world = EmptyWorld(data);
        world.S.Hunger = 20; world.S.Thirst = 20; world.S.Health = 50;
        world.S.Backpack.Add("meat_stew", 1); world.S.Backpack.Add("wheat", 1);
        world.Consume("meat_stew");
        var stew = data.Items["meat_stew"];
        Require(world.S.Hunger == 20 + stew.Hunger && world.S.Thirst == 20 + stew.Thirst && world.S.Health == 50 + stew.Health && world.S.Backpack["meat_stew"] == 0, "meat stew should feed, water and heal by its data");
        world.Consume("wheat");
        Require(world.S.Backpack["wheat"] == 1 && data.IsFood("wheat") && data.IsConsumable("wheat"), "raw wheat is food for compost and wolves but not eaten as it is");
        Require(!data.IsFood("water") && data.IsConsumable("water") && !data.IsFood("tin_can"), "water is drunk but is not food, and cans are neither");

        var old = EmptyWorld(data);
        var furnace = old.Add("furnace", new Vector2(10, 10));
        furnace.Recipe = "burnt_steak"; furnace.Inventory.Add("burnt_steak", 2);
        old.S.Backpack.Add("burnt_steak", 3);
        var json = Patch(old.Serialize(), ("\"burnt_steak\"", "\"cooked_meat\""));
        var loaded = EmptyWorld(data);
        loaded.Deserialize(json);
        Require(loaded.S.Backpack["burnt_steak"] == 3 && loaded.Find(furnace.Id) is { Recipe: "burnt_steak" } f && f.Inventory["burnt_steak"] == 2,
                "cooked meat from older saves should become burnt steak, furnace recipe included");
    }

    private static void CheckMarketTakesOnlyCrates(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add("signpost", new Vector2(10, 10));
        world.Player = sign.Pos;
        string axes = PackCrate(world, ("steel_chisel", 1), ("steel_axe", 1));
        string empty = PackCrate(world);
        Require(!world.CanSell(axes) && !world.CanSell(empty) && !world.CanSell("crate") && !world.CanSell("steel_axe"), "the village only takes packed crates of goods it buys");
        world.S.Backpack.TransferTo(sign.Inventory, axes, 1);
        Require(!world.Sell(sign) && world.Packed(axes) != null, "a crate holding a tool the village does not buy stays put");
        string meat = PackCrate(world, ("raw_meat", 6), ("burnt_steak", 6));
        string tools = PackCrate(world, ("steel_hammer", 1), ("steel_pick", 1));
        world.Player = sign.Pos;
        world.S.Backpack.TransferTo(sign.Inventory, meat, 1); world.S.Backpack.TransferTo(sign.Inventory, tools, 1);
        Require(world.Sell(sign) && world.S.Orders.Single().Crates == 2 && sign.Inventory[axes] == 1 && world.Packed(meat) == null,
                "a cart should take every acceptable crate, count them for the cart, and leave the rest");
        Require(world.S.Orders.Single().Goods["raw_meat"] == 6 && world.S.Orders.Single().Goods["steel_pick"] == 1, "the sale should pay for what the crates hold");
        SetCoins(world, 1000);
        Require(world.Buy(sign, "canned_food", 2) && world.S.Orders.Last().Crates == 2 && world.Coins == 1000 - 2 * world.LotPrice("canned_food"), "boxed goods are bought by the crate");
        Require(world.LotPrice("canned_food") > data.Market.Buys["canned_food"] * world.UnitPrice("canned_food", false), "a bought crate costs more than its contents would sell for");
        SafeTick(world, data.TradeSeconds + .1f);
        var bought = sign.Output!.Items.Keys.Where(World.IsCrate).ToList();
        Require(bought.Count == 2 && bought.All(id => world.Packed(id)?["canned_food"] == data.Market.Buys["canned_food"]), "each bought crate should arrive packed with one purchase");
        world.S.Hunger = 40; world.S.Backpack.Add("canned_food", 1); world.Consume("canned_food");
        Require(world.S.Hunger > 40 && data.IsFood("canned_food"), "canned food should be edible");
        Require(world.KnownPrice("raw_meat") > 0 && !world.S.Prices.ContainsKey("canned_food"), "the price sheet only tracks goods the village buys");
    }

    private static void CheckLegacySteelTools(GameData data) {
        var old = EmptyWorld(data);
        var anvil = old.Add("anvil", new Vector2(10, 10));
        anvil.Recipe = "steel_pick"; anvil.Inventory.Add("iron_bar", 3);
        old.S.Backpack.Add("steel_pick", 1); old.S.Backpack.Add("steel_hammer", 1); old.S.Backpack.Add("iron_bar", 2);
        old.Add("stone", new Vector2(20, 20), 2);
        old.S.Milestones.UnionWith(new[] { "gather", "furnace", "iron_bar", "anvil", "pick" });
        old.S.Goal = 5;
        // Older saves had steel tools as one item, and iron tools and loose iron that have since left the game.
        var json = Patch(old.Serialize(), ("\"iron_bar\": 2", "\"steel_tools\": 3"), ("\"steel_pick\"", "\"iron_pick\""), ("\"steel_hammer\"", "\"iron_hammer\""), ("\"stone\"", "\"loose_iron\""));
        var loaded = EmptyWorld(data);
        loaded.Deserialize(json);
        Require(loaded.S.Backpack["steel_axe"] == 1 && loaded.S.Backpack["steel_parts"] == 2,
                "old steel tools become steel parts, except one that becomes the missing axe");
        Require(loaded.S.Backpack["steel_pick"] == 1 && loaded.S.Backpack["steel_hammer"] == 1 && !loaded.S.Things.Any(t => t.Kind == "loose_iron"),
                "old iron tools become steel ones and loose iron is gone");
        Require(loaded.Find(anvil.Id)!.Recipe == "" && loaded.Find(anvil.Id)!.Inventory["iron_bar"] == 3, "an anvil set to a tool now made at the bench is reset and keeps its inputs");
        Require(loaded.S.Goal == 0, "an older save should begin with the new collect-water survival goal");
    }

    /// <summary>
    /// Every item and building must be reachable from a fresh start with only gathering, recipes and berry trade, and the tools that unlock the loop must never be lost.
    /// Resources need their tool first: trees an axe, deposits a pickaxe; stone, berries, water and loose pieces need none.
    /// </summary>
    private static void CheckNoToolDeadlocks(GameData data) {
        var fresh = new World(data);
        var have = new HashSet<string>(fresh.S.Backpack.Items.Keys) { "stone", "berries", "water", "water_1", "water_2", "raw_meat" };
        var built = new HashSet<string>();
        bool HasTool(string tool, int tier = 0) => have.Any(id => data.Items[id].Tool == tool && data.Items[id].Tier >= tier);
        for (bool grew = true; grew;) {
            grew = false;
            if (HasTool("axe")) grew |= have.Add("wood");
            foreach (var ore in new[] { "iron_ore", "coal", "tin_ore" }) if (HasTool("pickaxe")) grew |= have.Add(ore);
            // Crates of berries sold at a signpost buy what the village sells, the first pickaxe among it.
            if (built.Contains("signpost") && have.Contains("crate") && have.Contains("berries")) foreach (var item in data.Market.Buys.Keys) grew |= have.Add(item);
            if (HasTool("rake")) grew |= have.Add("leaves");
            foreach (var (id, b) in data.Buildings) if (b.Cost.Keys.All(have.Contains)) grew |= built.Add(id);
            if (built.Contains("pile") && have.Any(data.IsFood)) grew |= have.Add("fertilizer");
            // Seeds are bought with berry money to start the first plot.
            if (built.Contains("farm") && data.Farm.Sow.Keys.All(id => have.Contains(id) || data.Market.Buys.ContainsKey(id))) foreach (var item in data.Farm.Yield.Keys) grew |= have.Add(item);
            foreach (var r in data.Recipes.Values) {
                bool where = r.Hand && (r.Tool == "" || HasTool(r.Tool, r.Tier)) || built.Contains(r.Station) && (World.SlotKinds(r.Station).Length == 0 || r.Tool == "" || HasTool(r.Tool, r.Tier));
                if (where && r.Inputs.Keys.All(have.Contains)) grew |= have.Add(r.Output);
            }
        }
        var unreachable = data.Items.Keys.Where(id => id != Inventory.Coins && !data.Market.Buys.ContainsKey(id) && !have.Contains(id)).Concat(data.Buildings.Keys.Where(id => !built.Contains(id))).ToList();
        Require(unreachable.Count == 0, $"everything should be reachable from a fresh start: {string.Join(", ", unreachable)}");
        // The village never takes the tools that make crates, so selling cannot strand the player.
        Require(!data.Market.Sells.Keys.Any(id => id is "stone_chisel" or "wood_hammer"), "bootstrap tools must not be sellable");
        // The first pickaxe, and a lost axe, are bought with berry money; a crate needs only planks and a mallet.
        Require(data.Market.Sells.ContainsKey("berries") && data.Market.Buys.ContainsKey("steel_axe") && data.Market.Buys.ContainsKey("steel_pick"), "an axe or pickaxe must be buyable with berry money");
        float berryCrate = data.CrateCapacity * data.Items["berries"].Price + data.Items["crate"].Price;
        Require(fresh.LotPrice("steel_pick") <= 4 * berryCrate, "four crates of berries should pay for the first pickaxe");
    }

    private static void CheckSleepNeedsFoodAndWater(GameData data) {
        Require(Math.Abs(100 / data.HungerPerSecond - data.DaySeconds) < .01f && Math.Abs(100 / data.ThirstPerSecond - data.DaySeconds) < .01f,
                "a full stomach should last one in-game day and a full water droplet one day");
        foreach (var (hunger, thirst, reason) in new[] { (10f, 100f, "hungry"), (100f, 10f, "thirsty") }) {
            var world = NightWorld(data);
            var hut = world.Add("hut", world.Player + new Vector2(2, 0));
            world.S.Hunger = hunger; world.S.Thirst = thirst;
            float time = world.S.Time;
            world.Sleep(hut);
            Require(world.S.Time == time && world.SleepBlock()?.Contains(reason) == true,
                    $"sleep must be refused when the player is too {reason} to last until morning");
        }
        var early = NightWorld(data);
        early.Add("hut", early.Player + new Vector2(2, 0));
        early.S.Time = data.DaySeconds * (World.SleepHour - 6 + .2f) / 24;
        Require(!early.Night && early.SleepBlock() == null, "sleep must be allowed from 10 pm, before night falls");
        early.S.Time = data.DaySeconds * (World.SleepHour - 6 - .2f) / 24;
        Require(early.SleepBlock()?.Contains("10 pm") == true, "sleep must be refused before 10 pm");
    }

    private static void CheckSprintBurnsFood(GameData data) {
        (float Hunger, float Thirst) Burned(bool sprinting) {
            var world = new World(data);
            world.Me.Sprinting = sprinting;
            for (int i = 0; i < 10; i++) world.Tick(1);
            return (100 - world.S.Hunger, 100 - world.S.Thirst);
        }
        var (walking, sprinting) = (Burned(false), Burned(true));
        Require(Math.Abs(sprinting.Hunger / walking.Hunger - data.SprintDrain) < .001f && Math.Abs(sprinting.Thirst / walking.Thirst - data.SprintDrain) < .001f,
                "sprinting must drain food and drink sprintDrain times as fast");
    }

    private static void CheckSprintOutrunsWolf(GameData data) {
        // Fleeing straight away from a wolf four tiles behind for two seconds: walking loses ground, sprinting slowly gains it.
        float Gap(bool sprinting) {
            var world = NightWorld(data);
            var wolf = new Wolf { Pos = world.Player + new Vector2(-4, 0) };
            world.S.Wolves.Add(wolf);
            world.Me.Sprinting = sprinting;
            var start = world.Player;
            for (int i = 0; i < 40; i++) { world.MovePlayer(Vector2.Right * .05f * data.WalkSpeed * (sprinting ? data.SprintSpeed : 1)); world.Tick(.05f); }
            Require(world.Player.DistanceTo(start) > 7, "the open ground east of the test spot must let the player flee");
            return wolf.Pos.DistanceTo(world.Player);
        }
        Require(Gap(false) < 3, "a chasing wolf must be faster than a walking player");
        Require(Gap(true) > 4.1f, "a sprinting player must outrun a chasing wolf");
    }

    private static void CheckCabinAndChaseIntoLight(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add("hut", world.Player + new Vector2(2, 0));
        var wolf = new Wolf { Pos = world.Player + new Vector2(-4, 0) };
        world.S.Wolves.Add(wolf);
        SafeTick(world, .5f);
        Require(world.Enter(hut) && world.S.Sheltered == hut.Id, "clicking a nearby cabin should take the player inside");
        float before = wolf.Pos.DistanceTo(world.Player);
        world.S.Health = 40;
        SafeTick(world, 1);
        Require(wolf.Pos.DistanceTo(world.Player) > before && wolf.Leap <= 0 && world.S.Health >= 40,
                "a wolf must back off at once when the player is inside a cabin");
        var inside = world.Player;
        world.MovePlayer(new Vector2(0, .1f));
        Require(world.S.Sheltered == hut.Id && world.Player == inside, "moving should not take the player back outside");
        // A chaser that caught up stands right on the player; it must still back off and give up.
        var caught = new Wolf { Pos = world.Player, Target = world.Me.Id };
        world.S.Wolves.Add(caught);
        SafeTick(world, 10);
        Require(!world.S.Wolves.Contains(caught) && !world.S.Wolves.Contains(wolf), "wolves should leave once the player has been inside long enough");
        world.Leave();
        Require(world.S.Sheltered == 0, "stepping outside should leave the cabin");
        world.Player = hut.Pos + new Vector2(12, 0);
        world.S.Health = -1;
        for (int i = 0; i < 200 && world.S.Sheltered == 0; i++) { world.Tick(.25f); world.S.Health = Math.Max(world.S.Health, 1); }
        Require(world.S.Sheltered == hut.Id && world.Player.DistanceTo(hut.Pos) < 3 && world.CanStand(world.Player), "a fallen player should wake inside the nearest cabin");

        var lit = NightWorld(data);
        var chaser = new Wolf { Pos = lit.Player + new Vector2(-5, 0) };
        lit.S.Wolves.Add(chaser);
        SafeTick(lit, .5f);
        lit.Add("campfire", lit.Player + new Vector2(2, 0)).Burn = 300;
        float gap = chaser.Pos.DistanceTo(lit.Player);
        SafeTick(lit, .5f);
        Require(chaser.Pos.DistanceTo(lit.Player) < gap, "a wolf already chasing should follow the player briefly into firelight");
        SafeTick(lit, 4);
        gap = chaser.Pos.DistanceTo(lit.Player);
        SafeTick(lit, .5f);
        Require(chaser.Pos.DistanceTo(lit.Player) > gap, "a chasing wolf should give up and back away after a few seconds in the light");
    }

    private static void CheckCoinStacks(GameData data) {
        var bag = new Inventory { Slots = 2 };
        Require(bag.Add(Inventory.Coins, 1500) == 1500 && bag.UsedSlots == 2 && bag.Stacks().First().Count == 1000,
                "coins should stack to 1000 per slot");
        Require(bag.Room(Inventory.Coins) == 500 && bag.Room("wood") == 0, "only coins should use the larger stack");

        var world = EmptyWorld(data);
        var sign = world.Add("signpost", new Vector2(10, 10));
        world.Player = sign.Pos;
        SetCoins(world, 5);
        world.S.Backpack.TransferTo(sign.Inventory, PackCrate(world, ("steel_axe", 1)), 1);
        Require(world.Sell(sign), "a loaded crate should sell");
        SafeTick(world, data.TradeSeconds + .1f);
        Require(world.Coins == 5 && sign.Wages![Inventory.Coins] == (int)(data.Items["steel_axe"].Price + data.Items["crate"].Price), "sale proceeds for the goods and their crate should arrive as whole coins in the wage slot");
        world.S.Backpack.TransferTo(sign.Inventory, Inventory.Coins, 1);
        Require(!world.Sell(sign) && sign.Inventory[Inventory.Coins] == 1, "coins themselves cannot be sold");
        Require(!world.Buy(sign, Inventory.Coins, 1), "coins themselves cannot be bought");
    }

    private static void CheckWageSlot(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add("signpost", new Vector2(10, 10));
        world.Player = sign.Pos;
        int full = sign.Wages!.StackOf(Inventory.Coins);
        sign.Wages.Add(Inventory.Coins, full - 5);
        world.S.Orders.Add(new TradeOrder { Goods = new() { ["wood"] = 1 }, Buying = false, Value = 12, Signpost = sign.Id });
        SafeTick(world, data.TradeSeconds + .1f);
        Require(sign.Wages[Inventory.Coins] == full && sign.Output![Inventory.Coins] == 7, "sale coins should top up the one-stack wage slot and spill the rest into the output");
        world.S.Backpack.Add("wood", 1);
        Require(world.Transfer(world.S.Backpack, sign.Wages, "wood", 1) == 0 && world.S.Backpack["wood"] == 1, "the wage slot should refuse anything but coins, even by hand");

        var worker = AddWorker(world, sign.Pos);
        sign.Wages.Remove(Inventory.Coins, full - 3);
        SetCoins(world, 10);
        world.PayWages();
        Require(worker.WageDebt == 0 && sign.Wages[Inventory.Coins] == 0 && world.Coins == 10 - (worker.Wage - 3), "wages should come out of the wage slot first and the backpack after");
        SetCoins(world, 0);
        world.PayWages();
        Require(worker.WageDebt == worker.Wage && sign.Output![Inventory.Coins] == 7, "wages should not be taken from the market output");
    }

    private static void CheckLegacySave(GameData data) {
        var old = EmptyWorld(data);
        var sign = old.Add("signpost", new Vector2(10, 10));
        old.S.Orders.Add(new TradeOrder { Buying = true, Value = 8, Signpost = sign.Id });
        // Older saves kept coins outside the backpack and one item per order.
        var json = System.Text.Json.Nodes.JsonNode.Parse(old.Serialize())!;
        json["Coins"] = 30;
        var post = json["Things"]!.AsArray().First(t => t!["Id"]!.GetValue<int>() == sign.Id)!;
        post["Output"] = null; post["Wages"] = null; post["Inventory"]!["Slots"] = 18;
        var order = json["Orders"]![0]!;
        order["Goods"] = new System.Text.Json.Nodes.JsonObject(); order["Item"] = "wood"; order["Quantity"] = 4;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(json.ToJsonString());
        Require(loaded.Coins == 30 && loaded.S.Coins == null, "coins from an older save should move into the backpack");
        Require(loaded.Find(sign.Id) is { Output.Slots: World.MarketSlots, Inventory.Slots: World.MarketSlots, Wages.Slots: 1 }, "an older signpost should get its market boxes");
        Require(loaded.S.Orders.Single().Goods.GetValueOrDefault("wood") == 4 && loaded.S.Orders.Single().Item == null, "an older single-item order should keep its goods");
        var migrated = System.Text.Json.Nodes.JsonNode.Parse(loaded.Serialize())!;
        Require(migrated["Coins"] == null && migrated["Orders"]!.AsArray().All(order => order!["Item"] == null), "migrated fields should not be written back");

        // Saves from the earlier, smaller maps would place the settlement in the mountains or the lake, so they are refused.
        var small = System.Text.Json.Nodes.JsonNode.Parse(old.Serialize())!;
        small["Version"] = 3;
        bool refused = false;
        try { EmptyWorld(data).Deserialize(small.ToJsonString()); } catch (InvalidOperationException error) { refused = error.Message.Contains("older, smaller map"); }
        Require(refused, "a save from the smaller map should be refused with a clear reason");
    }

    private static void CheckCrewKeepsForgeHot(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add("reinforced", new Vector2(20, 20));
        furnace.Heat = World.SteelHeat + 5;
        // The worker starts far off, so the forge would cool past the bar before they arrive to pump.
        AddWorker(world, new Vector2(30, 30)).Assignment = furnace.Id;
        world.S.Workers[0].Job = "building";
        SafeTick(world, 4);
        Require(furnace.Heat >= World.SteelHeat, "a working crew should keep the forge at or above the clear bar");
        var crew = world.S.Workers[0];
        crew.WageDebt = crew.Wage;
        SafeTick(world, 5);
        Require(furnace.Heat < World.SteelHeat, "a striking crew should let the forge cool below the clear bar");
        world.S.Workers.Clear();
        SafeTick(world, 5);
        Require(furnace.Heat < World.SteelHeat, "without a crew the forge should cool below the clear bar");

        var sign = world.Add("signpost", new Vector2(10, 10));
        var runner = AddWorker(world, sign.Pos);
        world.Player = sign.Pos; world.Assign(runner, sign);
        Require(world.FetchPrices(sign), "the assigned worker should set off for prices");
        SafeTick(world, data.TradeSeconds / 4);
        world.Assign(runner, null);
        Require(runner.Away == 0, "reassigning a runner should call off the errand rather than freeze them on the road");
    }

    private static void CheckWorkerSchedule(GameData data) {
        var world = EmptyWorld(data);
        float At(float hour) => (hour - 6) / 24 * data.DaySeconds;
        SetCoins(world, 200);
        var tree = world.Add("tree", new Vector2(10, 10), 400);
        var chest = world.Add("chest", new Vector2(12, 10));
        var near = world.Add("campfire", new Vector2(14, 14));
        world.Add("campfire", new Vector2(36, 36));
        var forge = world.Add("reinforced", new Vector2(20, 12));
        var cutter = AddWorker(world, tree.Pos);
        cutter.Tool = "steel_axe";
        var pumper = AddWorker(world, forge.Pos);
        var hauler = AddWorker(world, forge.Pos);
        world.Assign(cutter, tree); world.Assign(pumper, forge); world.Assign(hauler, forge);

        world.S.Time = At(data.LunchHour) - 5;
        forge.Heat = 100;
        SafeTick(world, 25);
        Require(world.Lunch && world.LunchFire == near && world.S.Workers.All(w => w.WorkState == WorkerWorkState.Resting && w.Pos.DistanceTo(near.Pos) < 2),
                "at lunch the whole crew should sit around the campfire closest to them");
        Require(cutter.Assignment == tree.Id && pumper.Assignment == forge.Id && hauler.Assignment == forge.Id,
                "lunch should keep every assignment");
        Require(forge.Heat < World.SteelHeat, "with the crew at lunch the forge should cool below the clear bar");
        SafeTick(world, At(data.LunchHour + 1) - world.S.Time + 10);
        Require(cutter.WorkState == WorkerWorkState.Gathering && pumper.WorkState == WorkerWorkState.Pumping,
                "after lunch workers should walk back to their jobs");

        float dusk = data.DuskStarts * data.DaySeconds;
        world.S.Time = dusk - 2;
        cutter.Bag.Remove("wood", 99); cutter.Bag.Add("wood", 2);
        chest.Inventory.InputFilter.Add("iron_bar");
        var sign = world.Add("signpost", new Vector2(30, 20));
        world.Assign(AddWorker(world, sign.Pos), sign);
        SafeTick(world, 40);
        Require(world.S.Workers.All(w => w.Home && !world.Sees(w) && w.Commute == data.TradeSeconds / 2), "at dusk every worker should walk off like a cart and sign out");
        // Walking arrives within reach of the signpost, so the walk-off starts up to that far from it.
        Require(cutter.Pos.DistanceTo(sign.Pos + Vector2.One + Terrain.Seaward * World.TripDistance) < 1.25f,
                "a worker should sign out a cart's trip down the valley from the signpost");
        Require(cutter.Bag["wood"] == 2 && cutter.Assignment == tree.Id && pumper.Assignment == forge.Id,
                "signing out should keep assignments and carried items");
        Require(world.FetchBlock(sign) == "Your workers have signed out until morning.", "nobody should run for prices after signing out");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.S.Workers.All(w => w.Home), "signed-out workers should stay signed out after loading");

        world.S.Time = data.DaySeconds - 1;
        chest.Inventory.InputFilter.Clear();
        SafeTick(world, 1 + data.TradeSeconds / 4);
        Require(cutter.Commute > 0 && !cutter.Home && cutter.Pos.DistanceTo(sign.Pos + Vector2.One) < World.TripDistance,
                "at dawn a worker should walk back up the valley as they fade in");
        SafeTick(world, 25);
        Require(world.S.Workers.All(w => !w.Home && w.WageDebt == 0) && cutter.Pos.DistanceTo(sign.Pos) > 5 && pumper.Pos.DistanceTo(sign.Pos) > 5,
                "at dawn paid workers should reappear at the signpost and walk back to work");
        Require(cutter.Assignment == tree.Id && pumper.Assignment == forge.Id && hauler.Assignment == forge.Id,
                "a night away should keep every assignment");
    }
}
