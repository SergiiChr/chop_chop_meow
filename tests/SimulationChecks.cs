using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Deterministic checks for the playable simulation, independent of the rendered scene.</summary>
public static class SimulationChecks {
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
        CheckOutputFilterBlocksAutomatedWithdrawal(data);
        CheckItemsRouteToAcceptingStorageBeforeNearerGeneralChest(data);
        CheckWorkerGathersWoodAndRetargetsWhenFilterChanges(data);
        CheckWorkerDetoursAroundLake(data);
        CheckCampfireWorkerFetchesFuelFromAllowedStorage(data);
        CheckFurnaceCrewSplitsBellowsAndHauling(data);
        CheckCartDeliversAfterFullTrip(data);
        CheckLooseGoodsNeverSellOrSendACart(data);
        CheckSaleLowersDemandAndNewsTravels(data);
        CheckHiredRunnerFetchesCurrentPrices(data);
        CheckUnpaidWorkerStrikes(data);
        CheckBoughtToolsArriveOneByOneAndSellBackInCrate(data);
        RecoveryChecks.Run(data);
        CheckRecoveryBoxesStayPrivate(data);
        CheckWorkersHaulLeftoversAfterRecipeSwitch(data);
        CheckDeathLeavesRecoveryBoxAndRespawnsAndSavesKeepState(data);
        CheckFurnacesBurnOneFuelAndWaitForSteelHeat(data);
        CheckAnvilNeedsHammering(data);
        CheckFinishedBatchWaitsForOutputRoom(data);
        CheckToolsGateMiningAndFelling(data);
        CheckCliffsHaveDepositsAndLooseCoal(data);
        CheckResourcesScatterAcrossTheOpenValley(data);
        CheckLoosePiecesNeedNoToolOrGatherTime(data);
        CheckSteelPickaxeYieldsFullStone(data);
        CheckSunAndMoonFollowTheirArcs(data);
        CheckWolvesHurtUnprotectedPlayerAtNightAndAvoidFire(data);
        CheckWolvesHuntAndCarcassesYieldMeat(data);
        CheckWolvesLeaveHealthyOrFirelitPlayersAlone(data);
        CheckWolfLeapPinsAndKillsAPlayerOnHalfHealth(data);
        CheckSleepingPassesTheNightForTheWorld(data);
        CheckWageSlotTakesOnlyCoinsAndPaysWagesFirst(data);
        CheckSleepNeedsFoodAndWater(data);
        CheckSprintBurnsFood(data);
        CheckCabinShelterAndChasingWolfGivesUpInLight(data);
        CheckSprintOutrunsWolf(data);
        CheckOnlyCoinsUseTheLargerStack(data);
        CheckOlderSaveMigratesCoinsMarketBoxesAndOrders(data);
        CheckSaveWithUnknownItemRefused(data);
        CheckCrewKeepsForgeHot(data);
        CheckCrewLunchesAtTheNearestFireAndGoesBackToWork(data);
        CheckCrewSignsOutAtDuskKeepingLoadsAndJobs(data);
        CheckCrewWalksBackAtDawnPaidAndKeepsItsJobs(data);
        CheckFogHidesUnseenGroundAndRemembersExploredGround(data);
        CheckCratesShowOneSlotPerItemTheyCanHold(data);
        CheckHandCraftingNeedsChiselAndTakesTime(data);
        CheckBenchRecipesWaitForSlottedTools(data);
        CheckTinNeedsPickaxeAndSmeltsIntoBars(data);
        CheckTinCansNeedASteelHammer(data);
        CheckSteelChiselCarvesAMortarAndWorksTwiceAsFast(data);
        CheckVillageSellsSeedsAndFertilizerByTheCrate(data);
        CheckWorkerTakesAxeFromStationAndPutsItBackForAJobWithoutTools(data);
        CheckFullStationLeavesWorkerHoldingTheWrongToolUntilThereIsRoom(data);
        CheckWorkerPutsToolBackGoingHomeAndLeavesItWhenDismissed(data);
        CheckRakeClearsLeavesWhichFallAgain(data);
        CheckPilesSnapTogetherHoldOneResourceAndMakeCompost(data);
        CheckCookingTableCrushesBerriesAndCooksJam(data);
        CheckCanneryAndFurnaceTakeTheirFoodRecipes(data);
        CheckCrushingBerriesByHandNeedsACarriedMortar(data);
        CheckWorkerWithTwoJobsTakesTurns(data);
        CheckHaulerStocksBenchForCrafter(data);
        CheckStockerFillsToolRack(data);
        CheckTenderFeedsCompostWithoutFood(data);
        CheckRunnerCarriesDeliveriesIntoStorage(data);
        CheckWorkerLeavesToolTheFilterHolds(data);
        CheckFarmPlotBuildsBesidePileAndTakesOnlyFarmGoods(data);
        CheckSownWheatGrowsToRipeInItsDays(data);
        CheckPlotHarvestedAloneKeepsSeedInItsOutput(data);
        CheckWorkerFinishesHarvestHaulsCropAndSowsAgain(data);
        CheckFoodsFeedWaterAndHealByTheirData(data);
        CheckMarketTakesOnlyCrates(data);
        CheckOlderSavesResetMadeToolAnvilsAndStartWaterGoal(data);
        CheckLoadedSaveDropsOldStageNumber(data);
        CheckEverythingIsReachableFromFreshStart(data);
        CheckTorchLightsAndBurnsOut(data);
        CheckTorchSwingScaresOnlyWolvesItIsAimedAt(data);
        CheckDirectTorchHitKnocksAWolfBack(data);
        CheckWolfKilledNearTheEdgeLeavesItsCorpseOnTheMap(data);
        CheckFullPackGathersSlowlyAndIgnoresASwing(data);
        CheckEachToolTakesItsOwnSlot(data);
        MineChecks.Run(data);
        ProductionChainChecks.Run(data);
        CheckPlayerProgressesFromRawMaterialsToFirstAutomation(data);
        MultiplayerChecks.Run(data);
        AllocationChecks.Run(data);
        GD.Print("SELF-TEST PASS");
    }

    /// <summary>A worker with jobs at two places takes turns at them, and taking one job away keeps the other.</summary>
    private static void CheckWorkerWithTwoJobsTakesTurns(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        chest.Inventory.Add("wood", 20);
        var rock = world.Add(ThingKind.Stone, new Vector2(24, 20), 30);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 24));
        var both = AddWorker(world, new Vector2(22, 22));
        world.SetDuty(both, rock, "gather", true);
        world.SetDuty(both, fire, "fuel", true);
        Require(both.Duties.Count == 2 && both.Assignment == rock.Id, "a worker should keep jobs at two places and start at the first");
        SafeTick(world, 60);
        Require(rock.Remaining < 30 && fire.Burn + fire.Inventory["wood"] * world.Data.CampfireSeconds > 0,
                "a worker with jobs at two places should take turns: gathering stone and feeding the fire");
        world.SetDuty(both, fire, "fuel", false);
        Require(both.Duties.Count == 1 && World.HasDuty(both, rock.Id, "gather"), "taking one job away should keep the other");
    }

    /// <summary>A bench hauler only stocks it; the work itself waits for a crafter.</summary>
    private static void CheckHaulerStocksBenchForCrafter(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        chest.Inventory.Add("wood", 20);
        var bench = world.Add(ThingKind.Bench, new Vector2(28, 24));
        bench.Tools!.Add("stone_chisel", 1);
        world.SetRecipe(bench, "planks");
        var hauler = AddWorker(world, new Vector2(27, 23));
        world.SetDuty(hauler, bench, "haul", true);
        SafeTick(world, 20);
        Require(bench.Inventory["wood"] > 0, "a bench hauler should stock the bench with wood");
        Require(bench.Inventory["planks"] == 0, "a bench hauler should not make planks");
        Require(!bench.Processing, "a bench hauler should not start the bench");
        var crafter = AddWorker(world, new Vector2(28, 25));
        world.SetDuty(crafter, bench, "craft", true);
        SafeTick(world, 20);
        Require(bench.Inventory["planks"] + chest.Inventory["planks"] > 0, "a crafter should work the stocked bench");
    }

    private static void CheckStockerFillsToolRack(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        var rack = world.Add(ThingKind.Toolstation, new Vector2(16, 20));
        chest.Inventory.Add("steel_axe", 1);
        var stocker = AddWorker(world, new Vector2(18, 20));
        world.SetDuty(stocker, rack, "stock", true);
        SafeTick(world, 20);
        Require(rack.Inventory["steel_axe"] == 1 && chest.Inventory["steel_axe"] == 0, "a stocker should bring a stored tool to the rack");
    }

    /// <summary>A tender feeds compost from storage, skipping food when told to, and carries its fertilizer back.</summary>
    private static void CheckTenderFeedsCompostWithoutFood(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        var compost = world.Add(ThingKind.Pile, new Vector2(16, 24));
        Require(world.SetCompost(compost, true), "an empty pile should turn into compost");
        world.SetSkipFood(compost, true);
        compost.Inventory.Add("fertilizer", 2);
        chest.Inventory.Add("leaves", 8); chest.Inventory.Add("berries", 6);
        var tender = AddWorker(world, new Vector2(17, 22));
        world.SetDuty(tender, compost, "tend", true);
        SafeTick(world, 40);
        Require(chest.Inventory["fertilizer"] == 2, "a tender should carry fertilizer to storage");
        Require(compost.Inventory["leaves"] > 0, "a tender should bring leaves to the compost");
        Require(chest.Inventory["berries"] == 6, "a tender should leave food in storage when food deliveries are skipped");
        Require(compost.Inventory["berries"] == 0, "a tender should not bring food to the compost when food deliveries are skipped");
    }

    /// <summary>A trading post's runner carries what the cart delivered, other than coins, to storage.</summary>
    private static void CheckRunnerCarriesDeliveriesIntoStorage(GameData data) {
        var world = EmptyWorld(data);
        var store = world.Add(ThingKind.Chest, new Vector2(26, 28));
        var sign = world.Add(ThingKind.Signpost, new Vector2(24, 26));
        sign.Output!.Add("tin_bar", 3); sign.Output.Add(Inventory.Coins, 20);
        var runner = AddWorker(world, new Vector2(24, 25));
        world.SetDuty(runner, sign, "deliveries", true);
        SafeTick(world, 20);
        Require(store.Inventory["tin_bar"] == 3, "a runner should carry deliveries into storage");
        Require(sign.Output["tin_bar"] == 0, "a runner should leave no delivery in the post's output");
        Require(sign.Output[Inventory.Coins] == 20, "a runner should leave the coins in the post's output");
    }

    /// <summary>A tool station's output filter keeps workers from taking a tool it does not let out.</summary>
    private static void CheckWorkerLeavesToolTheFilterHolds(GameData data) {
        var world = EmptyWorld(data);
        world.Add(ThingKind.Chest, new Vector2(20, 20));
        var heldRack = world.Add(ThingKind.Toolstation, new Vector2(30, 20));
        heldRack.Inventory.Add("steel_axe", 1);
        world.SetFilter(heldRack, "steel_pick", true, true);
        var oak = world.Add(ThingKind.Tree, new Vector2(32, 22), 5);
        var cutter = AddWorker(world, new Vector2(30, 22));
        world.Assign(cutter, oak);
        SafeTick(world, 10);
        Require(cutter.Tool == "", "a worker should not take a tool the station's output filter holds back");
        Require(cutter.Missing == "axe", "a worker with no axe to take should say an axe is missing");
        Require(heldRack.Inventory["steel_axe"] == 1, "the station should keep the held back axe");
        world.SetFilter(heldRack, "steel_pick", true, false);
        SafeTick(world, 10);
        Require(cutter.Tool == "steel_axe", "with the filter cleared, the worker should take the axe");
    }

    private static void CheckOutputFilterBlocksAutomatedWithdrawal(GameData data) {
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

    private static void CheckItemsRouteToAcceptingStorageBeforeNearerGeneralChest(GameData data) {
        var world = EmptyWorld(data);
        var nearestGeneral = world.Add(ThingKind.Chest, new Vector2(11, 10));
        var explicitWood = world.Add(ThingKind.Chest, new Vector2(17, 10));
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

    private static void CheckWorkerGathersWoodAndRetargetsWhenFilterChanges(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 5);
        var general = world.Add(ThingKind.Chest, new Vector2(8.5f, 10));
        var filtered = world.Add(ThingKind.Chest, new Vector2(13, 10));
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
        for (int i = 0; i < 32 && general.Inventory["wood"] == 0; i++) SafeTick(world, Step);
        Require(general.Inventory["wood"] == 5, "retargeted load should reach the newly accepting chest");
        Require(filtered.Inventory["wood"] == 0, "retargeted load should not leak into the old chest");
        Require(worker.Bag["wood"] == 0, "retargeted load should leave the worker's bag empty");
    }

    private static void CheckFurnaceCrewSplitsBellowsAndHauling(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        var chest = world.Add(ThingKind.Chest, new Vector2(23, 20));
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
                "the first of two reinforced-furnace workers should operate the bellows");
        Require(sawHauling && furnace.Inventory["steel_bar"] + chest.Inventory["steel_bar"] > 0,
                "the second furnace worker should haul recipe inputs, collect actual steel, and return it to storage");
        Require(chest.Inventory["iron_bar"] < 4 && chest.Inventory["coal"] < 6,
                "hauling should remove ingredients from the source chest");
    }

    private static void CheckWorkerDetoursAroundLake(GameData data) {
        var world = EmptyWorld(data);
        static Vector2 Bank(Vector2 direction, float past) => Terrain.Lake + direction * (Terrain.Shore(MathF.Atan2(direction.Y, direction.X)) + past);
        var tree = world.Add(ThingKind.Tree, Bank(Terrain.Seaward, 2), 12);
        var chest = world.Add(ThingKind.Chest, Bank(-Terrain.Seaward, 1.5f));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        bool stayedOutOfPond = true;
        for (int i = 0; i < 700 && chest.Inventory["wood"] == 0; i++) {
            SafeTick(world, Step);
            stayedOutOfPond &= !Terrain.InLake(worker.Pos);
        }

        Require(stayedOutOfPond,
                "worker pathing should route around the pond without entering its blocked radius");
        Require(chest.Inventory["wood"] >= worker.Bag.StackSize && worker.Bag["wood"] == 0,
                "worker assigned across the pond should eventually gather wood and deliver it to accepting storage");
    }

    private static void CheckCampfireWorkerFetchesFuelFromAllowedStorage(GameData data) {
        var world = EmptyWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var chest = world.Add(ThingKind.Chest, new Vector2(24, 20));
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
            SafeTick(world, Step);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        for (int i = 0; i < 80 && (worker.Bag["wood"] > 0 || fire.Inventory["wood"] == 0); i++) {
            SafeTick(world, Step);
            sawFirewoodInTransit |= worker.Bag["wood"] > 0 || worker.Destination == fire.Id;
        }
        Require(sawFirewoodInTransit && chest.Inventory["wood"] == 3,
                "campfire worker should carry three real wood from output-enabled storage");
        SafeTick(world, Step);
        Require(fire.Inventory["wood"] > 0 && fire.Burn > 0,
                "carried firewood should be deposited into the fire and consumed to sustain its burn");
        int storedBeforeBurn = fire.Inventory["wood"];
        SafeTick(world, fire.Burn - 29);
        Require(fire.Inventory["wood"] < storedBeforeBurn && fire.Burn > 0,
                "campfire should consume stored wood when its burn falls low");
    }

    private static void CheckCartDeliversAfterFullTrip(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        var output = sign.Output!;
        world.Player = sign.Pos;
        SetCoins(world, 100);
        float coinsBeforeBuy = world.Coins;
        Require(!world.Buy(sign, "wood", 1) && world.Orders.Count == 0, "the village should not sell goods missing from its list");
        Require(world.Buy(sign, "water", 1), "market should accept an affordable buy order");
        Require(world.Coins == coinsBeforeBuy - 15, "buy order should reserve coins immediately");
        Require(world.Orders.Count == 1, "buy order should put one cart on the road");
        Require(output.Count == 0, "buy order should leave goods in transit, not in the output");
        SafeTick(world, 29.9f);
        Require(world.Orders.Count == 1 && output.Count == 0,
                "cart must not deliver before the full 30-second trip");
        SafeTick(world, .2f);
        var box = output.Items.Keys.SingleOrDefault(World.IsCrate);
        Require(world.Orders.Count == 0, "cart should finish the order after delivering");
        Require(box == null, "cart should not leave a box for a refillable bottle");
        Require(output["water"] == 1, "cart should deliver one refillable bottle directly to the market output");
        Require(world.Carried("water") == 0, "cart should not put the delivered bottle in the backpack");
    }

    /// <summary>A sale pays when the cart returns and lowers demand, and the price sheet only changes when someone brings news.</summary>
    private static void CheckLooseGoodsNeverSellOrSendACart(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        // The post refuses loose goods by hand, so this is what an old save might hold.
        sign.Inventory.Add("wood", 3);
        Require(!world.Sell(sign), "loose goods must not be sold");
        Require(world.Orders.Count == 0, "loose goods must not send a cart");
        Require(sign.Inventory["wood"] == 3, "loose goods must stay on the post");
    }

    private static void CheckSaleLowersDemandAndNewsTravels(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        var output = sign.Output!;
        world.Player = sign.Pos;
        SetCoins(world, 100);
        // Tools have no steady amount, so even a small sale lowers their price.
        world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("steel_axe", 3)), 1);
        world.Player = sign.Pos;
        float coinsBeforeSale = world.Coins, quotedPrice = world.KnownPrice("steel_axe");
        Require(world.Sell(sign), "market should sell the goods loaded into it");
        Require(sign.Inventory.Count == 0, "sale should remove goods at dispatch");
        Require(world.Coins == coinsBeforeSale, "sale should not pay the backpack before the cart returns");
        Require(sign.Wages![Inventory.Coins] == 0, "sale should not pay the wage slot before the cart returns");
        float loweredDemand = world.Demand("steel_axe");
        Require(loweredDemand < 1 && world.KnownPrice("steel_axe") == quotedPrice, "selling should lower demand while the price sheet still shows old news");
        Require(!world.Sell(sign), "an empty market has nothing to sell");
        SafeTick(world, 30.1f);
        Require(world.Orders.Count == 0, "sale proceeds should arrive after the order is complete");
        Require(sign.Wages![Inventory.Coins] > 0, "sale proceeds should arrive in the wage slot when the cart returns");
        Require(output[Inventory.Coins] == 0, "sale proceeds should not go to the market output");
        Require(world.Coins == coinsBeforeSale, "sale proceeds should not go straight to the player");
        Require(world.KnownPrice("steel_axe") < quotedPrice && world.PriceAge == 0, "the returning carter should bring today's lower price");
        SkipClock(world, data.DaySeconds * 2);
        Require(world.Demand("steel_axe") > loweredDemand && world.PriceAge == 2, "the sheet should age by whole days");
        SafeTick(world, 60);
        Require(world.Demand("steel_axe") > loweredDemand, "market demand should recover");
        Require(world.Demand("steel_axe") < 1, "market demand should still be below normal");
        Require(world.KnownPrice("steel_axe") < world.UnitPrice("steel_axe", false),
            "the price sheet should stay stale while demand recovers");
    }

    private static void CheckHiredRunnerFetchesCurrentPrices(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        SkipClock(world, data.DaySeconds * 2);
        Require(world.PriceAge == 2, "the price sheet should have aged before the errand");
        // Past the skipped dawn, so its wages fall due before the runner is hired.
        SafeTick(world, 90);
        Require(world.FetchBlock(sign) == "No worker assigned." && !world.FetchPrices(sign), "fetching prices needs a worker assigned to the signpost");
        var cheap = data.HireOffers.OrderBy(o => o.Upfront).First(); var dear = data.HireOffers.OrderBy(o => o.Upfront).Last();
        Require(cheap.Wage > dear.Wage, "a smaller payment up front must come with a higher wage");
        SetCoins(world, (int)dear.Upfront);
        var runner = world.Hire(sign, dear);
        Require(runner != null, "a hiring offer should bring a worker");
        Require(runner.Wage == dear.Wage, "a hiring offer should fix the worker's wage");
        Require(world.Coins == 0, "a hiring offer should charge its payment up front");
        world.Assign(runner!, sign);
        Require(!world.Reached("assigned"), "minding the signpost is not the first automation goal");
        Require(world.FetchPrices(sign), "the assigned worker should agree to fetch prices");
        Require(runner!.Away > 0, "the assigned worker should set off for fresh prices");
        Require(world.FetchBlock(sign) != null, "the assigned worker should set off only once");
        SafeTick(world, data.TradeSeconds / 2);
        Require(runner!.Pos.DistanceTo(sign.Pos + Vector2.One + Terrain.Seaward * World.TripDistance) < 1, "the runner should be furthest down the valley halfway through the errand");
        SafeTick(world, data.TradeSeconds / 2 + .1f);
        Require(runner.Away == 0, "the runner should be back from the village");
        Require(world.PriceAge == 0, "the runner should return with current prices");
        Require(Math.Abs(world.KnownPrice("berries") - world.UnitPrice("berries", false)) < .01f, "the runner should return with prices matching the market");
        Require(world.FetchBlock(sign) == null, "the runner's return should let prices be fetched again");
    }

    /// <summary>A worker the player cannot pay at dawn strikes for that one wage until the debt is paid in full.</summary>
    private static void CheckUnpaidWorkerStrikes(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        RunUntil(world, data.DaySeconds * world.Day - .1f);
        var cheap = data.HireOffers.OrderBy(o => o.Upfront).First();
        SetCoins(world, (int)cheap.Upfront);
        var worker = world.Hire(sign, cheap);
        Require(worker != null && worker.Wage == cheap.Wage, "player should be able to hire an individually tracked worker");
        var workTarget = world.Add(ThingKind.Tree, new Vector2(12, 10), 6);
        worker!.Tool = "steel_axe";
        world.Assign(worker, workTarget);
        SetCoins(world, 0);
        SafeTick(world, .2f);
        Require(worker!.WageDebt == cheap.Wage, "an unpaid dawn wage should leave the worker owed their wage");
        Require(workTarget.Remaining == 6, "a worker on strike should not work the target");
        Require(worker.Bag.Count == 0, "a worker on strike should carry nothing");
        SetCoins(world, (int)(cheap.Wage - 1));
        world.SettleDebt(worker);
        Require(worker.WageDebt == cheap.Wage, "insufficient funds must not partly clear wage debt");
        SetCoins(world, (int)cheap.Wage);
        world.SettleDebt(worker);
        Require(worker.WageDebt == 0, "paying the full owed wage should clear the strike debt");
        SetCoins(world, 0);
        RunUntilDawn(world);
        RunUntilDawn(world);
        Require(worker.WageDebt == cheap.Wage, "a striking worker should not keep adding wages to the debt");
    }

    private static void CheckBoughtToolsArriveOneByOneAndSellBackInCrate(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        SetCoins(world, 20000);
        int lot = data.CrateCapacity;
        Require(world.Buy(sign, "steel_axe", lot), "market should accept a large affordable purchase of tools");
        SafeTick(world, data.TradeSeconds + .1f);
        // Tools take a slot each, so what the output boxes cannot hold waits in a box beside the post.
        int delivered = sign.Output!["steel_axe"] + world.Things.Where(t => t.Kind == ThingKind.Grave).Sum(t => t.Inventory["steel_axe"]);
        Require(delivered == lot, "every bought tool should arrive");
        Require(sign.Output["steel_axe"] == World.MarketSlots, "tools should fill the post's output first");
        Require(!sign.Output.Items.Keys.Any(World.IsCrate), "tools should not arrive in a crate");
        sign.Output.Remove("steel_axe", World.MarketSlots); RemoveThings(world, t => t.Kind == ThingKind.Grave);
        string crate = PackCrate(world, ("steel_axe", lot));
        world.Player = sign.Pos;
        Require(world.Transfer(world.Backpack, sign.Inventory, crate, 1) == 1 && world.Sell(sign), "market should accept the purchased tools back in a crate");
        SafeTick(world, data.TradeSeconds + .1f);
        int coins = world.Coins + sign.Wages![Inventory.Coins] + sign.Output[Inventory.Coins];
        float price = data.Items["steel_axe"].Price;
        Require(coins < 20000 - lot * price * 1.25f + lot * price,
                "buying tools and selling them straight back must lose coins, and a bulk sale must be paid less than its full pre-sale price");
    }

    private static void CheckRecoveryBoxesStayPrivate(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(12, 10);
        Give(world, "wood", 5);
        world.Die();
        var box = world.Things.Last(t => t.Kind == ThingKind.Grave);
        var tree = world.Add(ThingKind.Tree, new Vector2(13, 10), 30);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 10));
        var worker = AddWorker(world, new Vector2(14, 10));
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        SafeTick(world, 25);
        Require(box.Inventory["wood"] == 5 && chest.Inventory["wood"] > 0,
                "workers must store gathered goods in chests, not in the player's recovery box");

        var furnace = world.Add(ThingKind.Furnace, new Vector2(13, 13));
        world.Player = box.Pos;
        Give(world, "iron_ore", 4);
        world.Die();
        var oreBox = world.Things.Last(t => t.Kind == ThingKind.Grave);
        world.Assign(worker, furnace);
        SafeTick(world, 15);
        Require(oreBox.Inventory["iron_ore"] == 4, "workers must not haul inputs out of a recovery box");

        world.Player = box.Pos;
        foreach (var id in box.Inventory.Items.Keys.ToArray()) box.Inventory.TransferTo(world.Backpack, id, 999);
        SafeTick(world, .1f);
        Require(world.Things.All(t => t.Id != box.Id), "an emptied recovery box should disappear");
    }

    private static void CheckWorkersHaulLeftoversAfterRecipeSwitch(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(10, 10));
        world.Player = furnace.Pos + Vector2.One;
        world.SetRecipe(furnace, "iron_bar");
        furnace.Inventory.Add("iron_bar", 1);
        world.SetRecipe(furnace, "tin_bar");
        world.Collect(furnace);
        Require(world.Carried("iron_bar") == 1 && furnace.Inventory["iron_bar"] == 0,
                "collecting should also return goods made by a previous recipe");

        furnace.Inventory.Add("iron_bar", 1);
        var chest = world.Add(ThingKind.Chest, new Vector2(14, 10));
        var worker = AddWorker(world, new Vector2(11, 11));
        world.Player = new Vector2(30, 30);
        world.Assign(worker, furnace);
        SafeTick(world, 10);
        Require(chest.Inventory["iron_bar"] == 1, "station workers should haul leftovers from a previous recipe to storage");
    }

    private static void CheckDeathLeavesRecoveryBoxAndRespawnsAndSavesKeepState(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(9, 12);
        Give(world, "iron_ore", 7);
        Give(world, "berries", 3);
        world.Die();
        var grave = world.Things.LastOrDefault(t => t.Kind == ThingKind.Grave);
        Require(grave != null, "death should leave a recovery box");
        Require(grave.Pos == new Vector2(9, 12), "death should place the box at the death location");
        Require(grave.Inventory["iron_ore"] == 7, "death should put carried items in the box");
        Require(world.Dead, "death should mark the player dead");
        Require(world.RespawnIn == data.RespawnSeconds, "death should start the full respawn timer");
        Require(world.Backpack.Count == 0, "death should replace the emptied backpack");
        world.MovePlayer(Vector2.One);
        Require(world.Player == new Vector2(9, 12), "a collapsed player must not move before respawning");
        SafeTick(world, data.RespawnSeconds - .5f);
        Require(world.Dead && world.Player == new Vector2(9, 12), "the player must stay down for the whole respawn timer");
        SafeTick(world, 1);
        Require(!world.Dead, "the player should come back once the timer ends");
        Require(world.Player == Terrain.Spawn, "the player should respawn at the clearing");
        Require(world.Health == 100, "the player should respawn healthy");

        var saved = EmptyWorld(data);
        var furnace = saved.Add(ThingKind.Furnace, new Vector2(14, 17));
        furnace.Inventory.Add("wood", 2);
        furnace.Processing = true;
        furnace.Progress = 2.5f;
        PlaceOrder(saved, new TradeOrder { Goods = new() { ["wood"] = 4 }, Buying = true, Value = 7.5f, Elapsed = 17, Signpost = 31 });
        SetMarket(saved, "wood", 1.1f, 1.4f);
        var worker = AddWorker(saved, new Vector2(15, 18));
        worker.Bag.Add("stone", 2);
        worker.Destination = furnace.Id;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(saved.Serialize());
        var loadedFurnace = loaded.Find(furnace.Id);
        Require(loadedFurnace != null, "save/load should keep the furnace");
        Require(loadedFurnace.Processing, "save/load should keep the batch running");
        Require(Math.Abs(loadedFurnace.Progress - 2.5f) < .01f, "save/load should keep the batch's progress");
        Require(loaded.Orders.Count == 1, "save/load should preserve the order");
        Require(Math.Abs(loaded.Orders[0].Elapsed - 17) < .01f, "save/load should preserve the order's elapsed cart time");
        Require(loaded.Orders[0].Goods["wood"] == 4, "save/load should preserve the order's goods");
        Require(Math.Abs(loaded.KnownPrice("wood") - 1.1f) < .01f, "save/load should preserve market prices");
        Require(loaded.Workers.Count == 1, "save/load should keep the worker");
        Require(loaded.Workers[0].Bag["stone"] == 2, "save/load should preserve worker cargo");
        Require(loaded.Workers[0].Destination == furnace.Id, "save/load should preserve the worker's destination");
        Require(Math.Abs(loaded.Demand("wood") - 1.4f) < .01f,
                "save/load should preserve market demand");
    }

    private static void CheckFurnacesBurnOneFuelAndWaitForSteelHeat(GameData data) {
        var world = EmptyWorld(data);
        var basic = world.Add(ThingKind.Furnace, new Vector2(10, 10));
        Require(world.FuelOf(basic) == "wood", "basic furnace should burn wood by default");
        Require(!world.BatchCost(basic).ContainsKey("wood"),
            "basic furnace's wood fuel should not count as an ingredient");
        Require(basic.Burner != null, "basic furnace should have its own fuel slot");
        basic.Burner!.Add("coal", 4);
        Require(world.FuelOf(basic) == "coal" && world.FuelBlock(basic, "wood") != null,
                "basic furnace should burn the coal in its fuel slot and take one kind of fuel at a time");

        var reinforced = world.Add(ThingKind.Reinforced, new Vector2(15, 10));
        reinforced.Fuel = "wood"; // Even malformed or edited state must not make wood a valid steel fuel.
        Require(world.FuelOf(reinforced) == "coal" && world.FuelBlock(reinforced, "wood") != null,
                "the reinforced furnace should only ever burn coal, even if Fuel is tampered with");
        var steelCost = world.BatchCost(reinforced);
        Require(steelCost["coal"] == 2, "a steel batch should need two coal");
        Require(steelCost["iron_bar"] == 2, "a steel batch should need two iron bars");
        Require(steelCost.Count == 2, "a steel batch should need nothing else");
        reinforced.Inventory.Add("iron_bar", 2);
        reinforced.Inventory.Add("coal", 2);
        world.Player = reinforced.Pos;
        reinforced.Heat = 100;
        SafeTick(world, Step);
        Require(!reinforced.Processing && reinforced.Inventory["iron_bar"] == 2,
                "a steel batch must wait for fuel in the fuel slot, even with coal among its ingredients");
        reinforced.Heat = 0;
        reinforced.Burner!.Add("coal", 1);
        SafeTick(world, 12);
        Require(!reinforced.Processing, "the reinforced furnace must not start a steel batch while below the steel heat");
        Require(reinforced.Progress == 0, "the reinforced furnace must make no progress while below the steel heat");
        Require(reinforced.Inventory["iron_bar"] == 2, "the reinforced furnace must keep the batch's iron bars while below the steel heat");
        Require(reinforced.Inventory["coal"] == 2, "the reinforced furnace must keep the batch's coal while below the steel heat");
        Require(reinforced.Burner["coal"] == 1, "the reinforced furnace must keep its fuel coal while below the steel heat");
        reinforced.Heat = 100;
        SafeTick(world, Step);
        Require(reinforced.Processing, "once hot enough, the reinforced furnace should start the batch");
        Require(reinforced.Inventory["iron_bar"] == 0, "once hot enough, the reinforced furnace should consume the batch's iron bars");
        Require(reinforced.Inventory["coal"] == 0, "once hot enough, the reinforced furnace should consume the batch's coal");
        Require(reinforced.Burner["coal"] == 0, "once hot enough, the reinforced furnace should consume one coal of fuel");
        reinforced.Burner.Add("coal", 3);
        SafeTick(world, 30);
        Require(reinforced.Processing, "a cooled forge should keep its batch running");
        Require(reinforced.Progress < data.Recipes["steel_bar"].Seconds,
            "a cooled forge's steel batch should stop progressing");
        Require(reinforced.Burner["coal"] == 1, "a cooled forge should keep burning coal from its fuel slot");
    }

    private static void CheckAnvilNeedsHammering(GameData data) {
        var world = EmptyWorld(data);
        var anvil = world.Add(ThingKind.Anvil, new Vector2(10, 10));
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
        var chest = world.Add(ThingKind.Chest, new Vector2(24, 10));
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

    private static void CheckFinishedBatchWaitsForOutputRoom(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(10, 10));
        furnace.Inventory.Slots = 2;
        world.Player = furnace.Pos;
        Give(world, "iron_ore", 2);
        Give(world, "wood", 1);
        world.LoadBatch(furnace);
        world.Tick(.25f);
        Require(furnace.Processing && furnace.Inventory.Count == 0,
                "furnace should consume a valid batch before cooking begins");

        furnace.Inventory.Add("stone", 128);
        float cookSeconds = data.Recipes["iron_bar"].Seconds;
        world.Tick(cookSeconds + 1);
        Require(furnace.Processing, "a completed batch must stay pending when the station has no output capacity");
        Require(Math.Abs(furnace.Progress - cookSeconds) < .01f, "a pending batch must remain at full progress");
        Require(furnace.Inventory["iron_bar"] == 0, "a pending batch must not add output without capacity");

        furnace.Inventory.Remove("stone", 64);
        world.Tick(.25f);
        Require(!furnace.Processing, "pending output should finish the batch as soon as one slot becomes available");
        Require(furnace.Progress == 0, "pending output should reset the progress");
        Require(furnace.Inventory["iron_bar"] == 1, "pending output should be added exactly once");

        furnace.Inventory.Slots = 18; furnace.Inventory.Remove("stone", 64);
        furnace.Inventory.Add("iron_bar", furnace.Inventory.StackSize - 1);
        Give(world, "iron_ore", 2); Give(world, "wood", 1);
        world.LoadBatch(furnace);
        world.Tick(.25f); world.Tick(cookSeconds + 1);
        Require(furnace.Processing && furnace.Inventory["iron_bar"] == furnace.Inventory.StackSize,
                "a full one-stack output slot must hold the next batch at full progress");
        Require(world.LoadInput(furnace, "iron_ore", 999) == 0 && world.LoadInput(furnace, "stone", 1) == 0,
                "manual loading must reject empty backpacks and items the recipe does not use");
        Give(world, "iron_ore", 100);
        Require(world.LoadInput(furnace, "iron_ore", 999) == furnace.Inventory.StackSize && world.LoadInput(furnace, "iron_ore", 1) == 0,
                "an input slot must hold exactly one stack");
    }

    private static void CheckToolsGateMiningAndFelling(GameData data) {
        var world = EmptyWorld(data);
        var iron = world.Add(ThingKind.Iron, new Vector2(10, 10), 20);
        Achieve(world, "pick");
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true,
                "the player must still carry a usable pickaxe to mine after previously forging one");
        Require(world.GatherBlock(iron, true) == null,
                "a worker's tools come from a tool station, so what the player carries does not block them");
        Give(world, "steel_pick", 1);
        Require(world.GatherBlock(iron) == null,
                "carrying a steel pickaxe should unlock player mining");
        Drop(world, "steel_pick", 1);
        Give(world, "steel_axe", 1);
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true && world.GatherBlock(world.Add(ThingKind.Tree, new Vector2(12, 12), 5)) == null,
                "an axe should fell trees but not mine ore");

        Give(world, "wood", 8);
        Require(world.Build(ThingKind.Chest, Terrain.Spawn) == null,
                "building placement must reject the player arrival tile");
        var chest = world.Build(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 0));
        Require(chest != null, "building placement should accept a nearby clear tile");
        Require(chest.Kind == ThingKind.Chest, "building placement should build the chosen kind");
        Require(world.Carried("wood") == 0, "building placement should spend its cost");
    }

    private static void CheckSteelPickaxeYieldsFullStone(GameData data) {
        var world = EmptyWorld(data);
        var stone = world.Add(ThingKind.Stone, world.Player, 20);
        Give(world, "steel_pick", 1);

        world.Gather(stone, data.GatherSeconds / data.Items["steel_pick"].Speed);

        Require(world.Carried("stone") == 4,
                "a steel pickaxe should yield the full stone amount at its faster pace");
    }

    /// <summary>The sun crosses the sky from dawn to sunset, the moon from sunset to dawn, and the moon's light comes and goes over its cycle.</summary>
    private static void CheckSunAndMoonFollowTheirArcs(GameData data) {
        var sky = EmptyWorld(data);
        float day = data.DaySeconds;
        Require(!sky.MoonUp, "a new game should start with the sun, not the moon, in the sky");
        Require(sky.SkyArc < .15f, "a new game should start just after sunrise");
        Require(sky.Moonlight > .99f, "a new game should start under a full moon");
        RunUntil(sky, day * data.Sunset - .1f);
        Require(!sky.MoonUp && sky.SkyArc > .99f, "the sun should reach the far end of its arc just before sunset");
        RunUntil(sky, day * data.Sunset + .1f);
        Require(sky.MoonUp && sky.SkyArc < .01f, "the moon should start its arc at sunset");
        RunUntil(sky, day - .1f);
        Require(sky.MoonUp && sky.SkyArc > .99f, "the moon should reach the far end of its arc just before dawn");
        RunUntil(sky, day * data.MoonCycleDays / 2);
        Require(sky.Moonlight < .01f, "halfway through its cycle the moon should be new and give no light");
        RunUntil(sky, day * data.MoonCycleDays);
        Require(!sky.MoonUp && sky.SkyArc == 0, "a whole cycle after the first sunrise, the sun should be rising");
        Require(sky.Moonlight > .99f, "the moon should be full again after a whole cycle");
    }

    private static void CheckWolvesHurtUnprotectedPlayerAtNightAndAvoidFire(GameData data) {
        var dark = EmptyWorld(data);
        dark.Player = new Vector2(10, 10);
        RunUntilNight(dark);
        SetNeeds(dark, health: 100);
        PlaceWolf(dark, new Wolf { Pos = dark.Player + new Vector2(.8f, 0) });
        SafeTick(dark, .2f);
        Require(dark.Health < 100, "an unprotected player near a wolf should take damage at night");

        var lit = EmptyWorld(data);
        lit.Player = new Vector2(10, 10);
        RunUntilNight(lit);
        SetNeeds(lit, health: 100);
        var fire = lit.Add(ThingKind.Campfire, lit.Player + new Vector2(3, 0));
        fire.Burn = 90;
        var wolf = new Wolf { Pos = lit.Player + new Vector2(1.3f, 0) };
        PlaceWolf(lit, wolf);
        SafeTick(lit, .2f);
        Require(lit.Health == 100 && wolf.Pos.DistanceTo(lit.Player) > 1.3f,
                "active campfire light should repel a nearby wolf without harming the player");
        RunUntilPhase(lit, .2f);
        SafeTick(lit, .1f);
        Require(lit.Wolves.Count == 0,
                "the day cycle should clear wolves when daylight returns");

        foreach (var cause in new[] { "hunger", "thirst" }) {
            var world = EmptyWorld(data);
            world.Player = new Vector2(12, 13);
            SetNeeds(world, health: 1);
            if (cause == "hunger") SetNeeds(world, hunger: 0); else SetNeeds(world, thirst: 0);
            Give(world, "berries", 2);
            Give(world, "water", 1);
            world.Tick(1);
            var recovery = world.Things.LastOrDefault(t => t.Kind == ThingKind.Grave);
            Require(recovery != null, $"{cause} death should leave a recovery box");
            Require(recovery.Pos == new Vector2(12, 13), $"{cause} death should leave the recovery box at the collapse location");
            Require(recovery.Inventory["berries"] == 2, $"{cause} death should recover the player's carried berries");
            Require(recovery.Inventory["water"] == 1, $"{cause} death should recover the player's carried water");
            SafeTick(world, data.RespawnSeconds + .1f);
            Require(world.Player == Terrain.Spawn && world.Health == 100,
                    $"{cause} death should respawn the player in a healthy state");
        }
    }

    private static void CheckWolvesHuntAndCarcassesYieldMeat(GameData data) {
        var chase = NightWorld(data);
        var runner = new Wolf { Pos = chase.Player + new Vector2(8, 0) };
        PlaceWolf(chase, runner);
        SafeTick(chase, 1);
        Require(runner.Pos.DistanceTo(chase.Player) < 5, "a wolf should close at least three tiles per second");

        var hunt = NightWorld(data);
        Give(hunt, "steel_axe", 1);
        Give(hunt, "wood", 1);
        var prey = new Wolf { Pos = hunt.Player + new Vector2(1.5f, 0), Health = 1 };
        PlaceWolf(hunt, prey);
        hunt.Attack();
        var carcass = hunt.Things.SingleOrDefault(t => t.Kind == ThingKind.WolfCorpse);
        Require(!hunt.Wolves.Contains(prey), "a killed wolf should not stay among the wolves");
        Require(carcass != null, "a killed wolf should leave a carcass");
        Require(carcass.Remaining == 4, "the carcass should hold the wolf's meat");
        RunUntilPhase(hunt, .2f);
        SafeTick(hunt, .5f);
        Require(hunt.Things.Contains(carcass!) && hunt.Wolves.Count == 0, "a carcass should outlast the night");

        hunt.Player = carcass!.Pos + new Vector2(1, 0);
        while (hunt.GatherBlock(carcass) == null) hunt.Gather(carcass, data.GatherSeconds);
        Require(hunt.Carried("raw_meat") == 4, "butchering a carcass should yield all of its raw meat");
        SafeTick(hunt, .1f);
        Require(!hunt.Things.Contains(carcass), "a fully butchered carcass should disappear");

        SetNeeds(hunt, hunger: 40);
        hunt.Consume("raw_meat");
        Require(hunt.Carried("raw_meat") == 4 && hunt.Hunger == 40, "raw meat must be cooked before it can be eaten");
        var furnace = hunt.Add(ThingKind.Furnace, hunt.Player + new Vector2(1, 1));
        hunt.SetRecipe(furnace, "burnt_steak");
        hunt.LoadBatch(furnace);
        for (int i = 0; i < 28; i++) hunt.Tick(Step);
        hunt.Collect(furnace);
        Require(hunt.Carried("burnt_steak") == 2, "the stone furnace should cook two steaks");
        Require(hunt.Carried("raw_meat") == 2, "the stone furnace should use two raw meat");
        Require(hunt.Carried("wood") == 0, "the stone furnace should use one fuel for two raw meat");
        SetNeeds(hunt, hunger: 40);
        hunt.Consume("burnt_steak");
        Require(hunt.Carried("burnt_steak") == 1 && hunt.Hunger == 80, "cooked meat should be edible and filling");
    }

    private static void CheckWolvesLeaveHealthyOrFirelitPlayersAlone(GameData data) {
        var healthy = NightWorld(data);
        SetNeeds(healthy, health: 80);
        var stalker = new Wolf { Pos = healthy.Player + new Vector2(3, 0), Attack = 5 };
        PlaceWolf(healthy, stalker);
        healthy.Tick(.2f);
        Require(stalker.Leap == 0 && !healthy.Mauled, "wolves should not leap at a player above half health");

        var sheltered = NightWorld(data);
        SetNeeds(sheltered, health: 40);
        sheltered.Add(ThingKind.Campfire, sheltered.Player + new Vector2(2, 0)).Burn = 90;
        var circling = new Wolf { Pos = sheltered.Player + new Vector2(-3, 0), Attack = 5 };
        PlaceWolf(sheltered, circling);
        sheltered.Tick(.2f);
        Require(circling.Leap == 0 && !sheltered.Mauled, "campfire light should prevent the throat leap");
    }

    private static void CheckWolfLeapPinsAndKillsAPlayerOnHalfHealth(GameData data) {
        var maul = NightWorld(data);
        SetNeeds(maul, health: 49);
        foreach (var (item, count) in new[] { ("berries", 3), ("burnt_steak", 2), ("raw_meat", 1), ("water", 2), ("steel_axe", 1), ("wood", 4) })
            Give(maul, item, count);
        var leaper = new Wolf { Pos = maul.Player + new Vector2(3, 0), Attack = 5 };
        var packmate = new Wolf { Pos = maul.Player + new Vector2(-9, 4), Attack = 5 };
        PlaceWolf(maul, leaper); PlaceWolf(maul, packmate);
        maul.Tick(.1f);
        Require(leaper.Leap > 0 && !maul.Mauled, "a nearby wolf should leap at a player on half health");
        maul.Tick(World.LeapSeconds);
        Require(maul.Mauled, "the leap should pin the player");
        Require(leaper.Feeding, "the leap should make the wolf feed");
        Require(leaper.Pinning, "the leap should make the wolf pin");
        Require(leaper.Pos.DistanceTo(maul.Player) < 1.6f, "the leap should land at the throat");
        var pinned = maul.Player;
        maul.MovePlayer(Vector2.Right);
        maul.Attack();
        Require(maul.Player == pinned && leaper.Health == 4, "a pinned player can neither move nor fight back");
        for (int i = 0; i < 40 && !maul.Dead; i++) maul.Tick(.1f);
        Require(maul.Dead, "a pinned player should be killed within seconds");
        for (int i = 0; i < 20; i++) maul.Tick(.1f);
        Require(packmate.Feeding, "the rest of the pack should stay to feed");
        Require(packmate.Pos.DistanceTo(pinned) < 1.6f, "the rest of the pack should run in to the pinned player");
        Require(maul.Wolves.Contains(leaper), "the leaping wolf should stay on the kill");
        var box = maul.Things.Single(t => t.Kind == ThingKind.Grave);
        Require(box.Inventory["berries"] == 0, "a wolf kill should leave no berries in the recovery box");
        Require(box.Inventory["burnt_steak"] == 0, "a wolf kill should leave no steak in the recovery box");
        Require(box.Inventory["raw_meat"] == 0, "a wolf kill should leave no raw meat in the recovery box");
        Require(box.Inventory["water"] == 2, "a wolf kill should keep water in the recovery box");
        Require(box.Inventory["steel_axe"] == 1, "a wolf kill should keep tools in the recovery box");
        Require(box.Inventory["wood"] == 4, "a wolf kill should keep wood in the recovery box");
        SafeTick(maul, data.RespawnSeconds);
        Require(!maul.Dead, "respawning should revive the player");
        Require(!maul.Mauled, "respawning should end the mauling");
        Require(!maul.Wolves.Contains(leaper), "respawning should send the leaping wolf away");
        Require(!maul.Wolves.Contains(packmate), "respawning should send its packmate away");
    }

    private static void CheckSleepingPassesTheNightForTheWorld(GameData data) {
        var world = EmptyWorld(data);
        world.Player = Terrain.Spawn;
        RunUntilPhase(world, data.NightStarts + .01f);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0));
        var sign = world.Add(ThingKind.Signpost, world.Player + new Vector2(1, 2));
        var furnace = world.Add(ThingKind.Furnace, world.Player + new Vector2(-2, 1));
        furnace.Processing = true;
        var order = new TradeOrder { Goods = new() { ["wood"] = 1 }, Buying = false, Value = 12, Signpost = sign.Id };
        PlaceOrder(world, order);
        var worker = AddWorker(world, sign.Pos);
        SetCoins(world, (int)worker.Wage + 3);
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(8, 0) });
        float priorTime = world.Time;
        float priorHunger = world.Hunger;
        world.Sleep(hut);
        Require(world.Time > priorTime && world.Day > 1,
                "sleep should advance the world clock through dawn");
        Require(world.Orders.Count == 0, "trade settlement should finish the order");
        Require(sign.Wages![Inventory.Coins] == 12 - worker.Wage,
            "the daily wage should come out of the wage slot during sleep");
        Require(world.Coins == worker.Wage + 3, "the worker's wage should reach the player");
        Require(worker.WageDebt == 0,
                "a funded worker should receive the normal daily wage during sleep");
        Require(!furnace.Processing && furnace.Inventory["iron_bar"] > 0,
                "furnace production should continue while the player sleeps");
        Require(world.Wolves.Count == 0 && world.Hunger < priorHunger,
                "nighttime sleep should advance survival needs and clear wolves at dawn");
    }

    private static void CheckFogHidesUnseenGroundAndRemembersExploredGround(GameData data) {
        var fresh = new World(data);
        var far = World.CliffOf(ThingKind.Coal).Center;
        Require(fresh.Explored(Terrain.Spawn) && !fresh.Explored(far),
                "a new game should reveal only the clearing around the player");
        Require(Known(fresh).All(t => t.Pos.DistanceTo(Terrain.Spawn) <= data.SightRadius),
                "things beyond sight should stay unknown until seen");

        var world = EmptyWorld(data);
        world.Player = new Vector2(10, 10);
        var tree = world.Add(ThingKind.Tree, new Vector2(12, 10), 42);
        var hidden = world.Add(ThingKind.Stone, new Vector2(44, 44), 64);
        var furnace = world.Add(ThingKind.Furnace, new Vector2(9, 12));
        furnace.Processing = true;
        SafeTick(world, Step);
        Require(Known(world).Contains(tree) && !Known(world).Any(t => t.Id == hidden.Id),
                "only things within the sight circle should become known");

        world.Player = new Vector2(40, 10);
        SafeTick(world, Step);
        tree.Remaining = 0;
        var gone = world.Add(ThingKind.Chest, new Vector2(11, 11));
        RemoveThings(world, t => t == gone);
        var memory = Known(world).Single(t => t.Id == tree.Id);
        Require(memory != tree, "explored ground out of sight should show a remembered copy of the tree");
        Require(memory.Remaining == 42, "explored ground out of sight should keep the tree as last seen");
        Require(world.Explored(new Vector2(12, 10)), "explored ground should be marked explored");
        Require(!Known(world).Single(t => t.Id == furnace.Id).Processing, "explored ground out of sight should not show the furnace's live activity");

        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Explored(new Vector2(12, 10)), "save/load should keep explored ground");
        Require(!loaded.Explored(new Vector2(44, 44)), "save/load should not explore new ground");
        Require(Known(loaded).Single(t => t.Id == tree.Id).Remaining == 42, "save/load should keep remembered things");

        world.Player = new Vector2(10, 10);
        SafeTick(world, Step);
        Require(Known(world).Single(t => t.Id == tree.Id).Remaining == 0,
                "looking again should replace the memory with the current state");
    }

    private static void CheckCliffsHaveDepositsAndLooseCoal(GameData data) {
        var world = new World(data);
        foreach (var cliff in World.Cliffs) {
            Require(!world.CanStand(cliff.Center), $"each {cliff.Ore} cliff should block movement");
            Require(Terrain.FromMountains(cliff.Center) < 0,
                $"each {cliff.Ore} cliff's middle should be off the mountain range");
            Require(Terrain.FromMountains(cliff.Center + cliff.Face * 2.6f) > 0,
                $"each {cliff.Ore} cliff should stand at the foot of a mountain range");
            Require(world.Things.Count(t => t.Kind == cliff.Ore && cliff.DistanceTo(t.Pos) < 3) == 2, $"each {cliff.Ore} cliff should have deposits at its foot");
        }
        foreach (var cliff in new[] { World.CliffOf(ThingKind.Iron), World.CliffOf(ThingKind.Coal) }) {
            Require(world.Things.Where(t => t.Kind == cliff.Ore).All(t => World.Cliffs.Any(c => c.Ore == cliff.Ore && c.DistanceTo(t.Pos) < 3)),
                    $"{cliff.Ore} deposits should only spawn at the foot of their cliffs");
        }
        {
            var cliff = World.CliffOf(ThingKind.Coal);
            var loose = world.Things.Where(t => t.IsLoose).ToList();
            Require(loose.Count > 0 && loose.All(t => cliff.DistanceTo(t.Pos) is > 3 and < 12 && world.CanStand(t.Pos)),
                    $"loose {cliff.Ore} should spread further than the deposits but stay near the nearer cliff");
            Require(loose.All(a => loose.All(b => a == b || a.Pos.DistanceTo(b.Pos) >= 2)), $"loose {cliff.Ore} pieces should lie apart");
        }
    }

    private static void CheckResourcesScatterAcrossTheOpenValley(GameData data) {
        var world = new World(data);
        foreach (var (kind, spacing) in new[] { (ThingKind.Stone, 15f), (ThingKind.Berries, 14f) }) {
            var found = world.Things.Where(t => t.Kind == kind).ToList();
            Require(found.Count >= 15 && found.All(t => t.Pos.DistanceTo(Terrain.Spawn) >= 9 && found.All(o => o == t || o.Pos.DistanceTo(t.Pos) >= spacing)),
                    $"{kind} should be scattered singly away from the clearing, so more of it takes exploring");
        }
        Require(world.Things.All(t => Terrain.Open(t.Pos) || t.IsDeposit),
                "nothing should be placed in the sea, the lake or the mountains");
        var trees = world.Things.Where(t => t.Kind == ThingKind.Tree).ToList();
        float woods = trees.Count(t => Terrain.FromMountains(t.Pos) < 15), plains = trees.Count(t => Terrain.FromMountains(t.Pos) >= 30);
        Require(trees.Count > 400 && woods > plains * 3, $"trees should thicken toward the mountains ({trees.Count} in all, {woods} near them, {plains} on the plains)");
        Require(world.Things.Count(t => t.Kind == ThingKind.Tree && t.Pos.DistanceTo(Terrain.Spawn) < 9) == 1,
                "the clearing should keep only its one nearby tree");
    }

    /// <summary>Loose pieces need no tool and no gather time: the whole piece goes into the backpack, or a worker's bag at once.</summary>
    private static void CheckLoosePiecesNeedNoToolOrGatherTime(GameData data) {
        var world = new World(data);
        var piece = world.Things.First(t => t.Kind == ThingKind.LooseCoal);
        Drop(world, "steel_axe");
        Require(world.GatherBlock(piece) == null, "loose coal should need no tool");
        PickUp(world, piece);
        Require(world.Carried("coal") == 2, "a loose coal piece should give coal");

        world = EmptyWorld(data);
        world.Add(ThingKind.Chest, new Vector2(12, 10));
        var worker = AddWorker(world, new Vector2(10, 10));
        var assigned = world.Add(ThingKind.LooseCoal, new Vector2(10.5f, 10), 2);
        world.Assign(worker, assigned);
        SafeTick(world, Step);
        Require(world.Find(assigned.Id) == null && worker.Bag["coal"] == 2,
                "a worker should pick up a loose piece at once, and it should not linger as an empty pile");
    }

    /// <summary>Plays from raw materials to a first hired worker: each stage hands the next what the player built with real recipes and trade delays.</summary>
    private static void CheckPlayerProgressesFromRawMaterialsToFirstAutomation(GameData data) {
        var world = EmptyWorld(data);
        float startTime = world.Time;
        Give(world, "steel_axe", 1);
        Give(world, "berries", 10);
        Give(world, "wood", 6);

        var tree = world.Add(ThingKind.Tree, Site(8, 8), 300);
        var stone = world.Add(ThingKind.Stone, Site(8, 14), 300);
        var bush = world.Add(ThingKind.Berries, Site(14, 8), 60);
        var iron = world.Add(ThingKind.Iron, Site(12, 14), 120);
        var coal = world.Add(ThingKind.Coal, Site(16, 8), 120);
        Require(world.GatherBlock(iron)?.Contains("pickaxe") == true && world.GatherBlock(coal)?.Contains("pickaxe") == true, "deposits should wait for a pickaxe the player does not start with");
        var bench = CheckGatheredMaterialsMakeCrates(world, tree, stone, bush);
        var sign = CheckBerryCratesBuyFirstPickaxe(world, iron);
        CheckMinedOreBecomesSteelTools(world, iron, coal, bench);
        var offer = CheckSoldSteelToolFundsHire(world, sign);
        CheckHiredWorkerSuppliesOpeningCamp(world, sign, offer, tree, stone);
        world.Tick(.01f, true);
        Require(world.GoalComplete, "objective chain should reach the first automation milestone");
        GD.Print($"Progression simulation elapsed: {world.Time - startTime:0} seconds.");
    }

    /// <summary>A stone chisel from bare stone opens wood crafting by hand, which leads to the bench, its mallet and crates.</summary>
    private static Thing CheckGatheredMaterialsMakeCrates(World world, Thing tree, Thing stone, Thing bush) {
        GatherUntil(world, tree, "wood", 64);
        GatherUntil(world, stone, "stone", 58);
        GatherUntil(world, bush, "berries", 48);

        CraftByHand(world, "stone_chisel", 1);
        CraftByHand(world, "planks", 6);
        CraftByHand(world, "planks", 5);
        CraftByHand(world, "wood_parts", 2);
        CraftByHand(world, "wood_hammer", 1);
        Require(world.Carried("planks") == 44, "hand crafting should turn wood into planks");
        Require(world.Carried("wood_parts") == 3, "hand crafting should turn wood into parts");
        Require(world.Carried("wood_hammer") == 1, "hand crafting should turn wood into a mallet");
        var bench = BuildAt(world, ThingKind.Bench, Site(28, 28));
        Require(world.SlotTool(bench, "wood_hammer") && world.BenchTool(bench, "hammer") == "wood_hammer", "the mallet should go into the bench's hammer slot");
        world.SetRecipe(bench, "crate");
        world.LoadBatch(bench, 4);
        CompleteBatches(world, bench, "crate", 4);
        world.Collect(bench);
        Require(world.Carried("crate") == 4 && world.Reached("crate"), "the bench should make crates from planks with the mallet slotted");
        return bench;
    }

    /// <summary>The first pickaxe is bought with berry money: four crates of berries pay for it.</summary>
    private static Thing CheckBerryCratesBuyFirstPickaxe(World world, Thing iron) {
        var data = world.Data;
        var sign = BuildAt(world, ThingKind.Signpost, Site(34, 25));
        for (int i = 0; i < 4; i++) {
            world.Player = sign.Pos;
            var box = world.PlaceCrate("crate", sign.Pos + new Vector2(0, 2));
            Require(box != null, "a crate should be in the box");
            Require(world.Transfer(world.Backpack, box!.Inventory, "berries", 12) == 12,
                "the berries should move from the backpack into the crate");
            Require(world.PickUpCrate(box), "the packed crate should be picked up");
            world.Transfer(world.Backpack, sign.Inventory, world.Backpack.Items.Keys.First(id => id.StartsWith(World.PackedPrefix)), 1);
        }
        Require(world.Sell(sign), "signpost should dispatch crates of berries");
        SafeTick(world, data.TradeSeconds + .2f);
        sign.Wages!.TransferTo(world.Backpack, Inventory.Coins, sign.Wages[Inventory.Coins]);
        sign.Output!.TransferTo(world.Backpack, Inventory.Coins, sign.Output[Inventory.Coins]);
        Require(world.Reached("sold") && world.Coins >= world.LotPrice("steel_pick"), $"four crates of berries should pay for a steel pickaxe: coins={world.Coins}");
        Require(world.Buy(sign, "steel_pick", 1), "the village should sell a steel pickaxe");
        SafeTick(world, data.TradeSeconds + .2f);
        Require(sign.Output.TransferTo(world.Backpack, "steel_pick", 1) == 1,
            "the bought pickaxe should arrive at the signpost");
        Require(world.Reached("pick"), "the bought pickaxe should meet the pick milestone");
        Require(world.GatherBlock(iron) == null, "the bought pickaxe should unlock mining deposits");
        return sign;
    }

    private static void CheckMinedOreBecomesSteelTools(World world, Thing iron, Thing coal, Thing bench) {
        GatherUntil(world, iron, "iron_ore", 22);
        GatherUntil(world, coal, "coal", 6);
        var furnace = BuildAt(world, ThingKind.Furnace, Site(25, 25));
        world.LoadBatch(furnace, 11);
        Require(furnace.Inventory["iron_ore"] == 22 && furnace.Burner!["wood"] == 11,
                "loading the furnace should transfer the recipe inputs, and one fuel per batch into its fuel slot");
        CompleteBatches(world, furnace, "iron_bar", 11);
        world.Player = furnace.Pos;
        world.Collect(furnace);
        Require(world.Carried("iron_bar") == 11, "player should collect bars actually smelted by the furnace from mined ore");

        var reinforced = BuildAt(world, ThingKind.Reinforced, Site(31, 25));
        BuildAt(world, ThingKind.Campfire, Site(31, 28));
        world.Player = reinforced.Pos;
        int bars = world.Carried("iron_bar");
        world.LoadBatch(reinforced, 1);
        Require(reinforced.Inventory["iron_bar"] == 2, "the reinforced furnace should load iron");
        Require(reinforced.Inventory["coal"] == 2, "the reinforced furnace should load mined coal");
        Require(reinforced.Burner!["coal"] == 1, "the reinforced furnace should load coal fuel into its fuel slot");
        CompleteBatches(world, reinforced, "steel_bar", 2, true);
        world.Player = reinforced.Pos;
        world.Collect(reinforced);
        Require(world.Carried("steel_bar") == 2 && world.Carried("iron_bar") == bars - 2,
                "one smelt of 2 iron bars and mined coal should make two steel bars from real inputs");

        var anvil = BuildAt(world, ThingKind.Anvil, Site(28, 25));
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
        Require(world.Carried("steel_pick") == 2, "the bench should craft a steel pickaxe from steel parts");
        Require(world.Carried("crate") == 1, "the bench should craft a crate from planks");
        Require(world.Reached("steel_tool"), "the steel tool milestone should be reached");
    }

    private static HireOffer CheckSoldSteelToolFundsHire(World world, Thing sign) {
        var data = world.Data;
        world.Player = sign.Pos;
        var crate = world.PlaceCrate("crate", sign.Pos + new Vector2(0, 2));
        Require(crate != null && world.Carried("crate") == 0, "an empty crate should be placed from the backpack");
        world.Transfer(world.Backpack, crate!.Inventory, "steel_pick", 1);
        Require(world.PickUpCrate(crate), "the packed crate should be picked up");
        string packed = world.Backpack.Items.Keys.Single(World.IsCrate);
        Require(world.Packed(packed)?["steel_pick"] == 1 && world.CanSell(packed), "the picked-up crate should still hold the steel pickaxe");
        world.Player = sign.Pos;
        world.Transfer(world.Backpack, sign.Inventory, packed, 1);
        Require(world.Sell(sign), "signpost should dispatch a crate of steel tools");
        float beforeReturn = world.Coins;
        SafeTick(world, data.TradeSeconds - .1f);
        Require(world.Coins == beforeReturn && world.Orders.Count == 1,
                "steel-tool payment should remain in transit until the trade delay elapses");
        SafeTick(world, .2f);
        sign.Wages!.TransferTo(world.Backpack, Inventory.Coins, sign.Wages[Inventory.Coins]);
        var offer = data.HireOffers[1];
        Require(world.Coins >= offer.Upfront && world.Reached("sold_steel"),
                $"returned cart should fund hire and mark the sale: coins={world.Coins:0.0}, upfront={offer.Upfront:0.0}, soldSteel={world.Reached("sold_steel")}, health={world.Health:0.0}, time={world.Time:0.0}, backpackSteel={world.Carried("steel_pick")}");
        return offer;
    }

    private static void CheckHiredWorkerSuppliesOpeningCamp(World world, Thing sign, HireOffer offer, Thing tree, Thing stone) {
        var data = world.Data;
        var worker = world.Hire(sign, offer);
        Require(worker != null, "sale proceeds should fund a new worker");
        var chest = BuildAt(world, ThingKind.Chest, Site(37, 25));
        // A woodcutter takes an axe from a tool station, so the player racks theirs: a station is 8 planks and 4 stone.
        GatherUntil(world, tree, "wood", 2);
        GatherUntil(world, stone, "stone", 4);
        CraftByHand(world, "planks", 2);
        var station = BuildAt(world, ThingKind.Toolstation, Site(37, 28));
        Require(world.Transfer(world.Backpack, station.Inventory, "steel_axe", 1) == 1, "the player should rack their axe in the tool station");
        var workerTree = world.Things.FirstOrDefault(t => t.Kind == ThingKind.Tree && t.Remaining > 0);
        Require(workerTree != null, "a remaining tree should be available for the hired worker");
        world.Assign(worker!, workerTree);
        Require(worker!.Assignment == workerTree!.Id, "player should assign the hired worker to the resource");
        Require(worker.Duties.Count == 1, "the hired worker should have one duty");
        Require(World.HasDuty(worker, workerTree.Id, "gather"), "the hired worker's duty should be gathering from that resource");
        Require(world.Reached("assigned"), "assigning a worker should reach the assigned milestone");
        // Selling for the first pickaxe takes the opening day, so a worker hired at night starts in the morning.
        for (int i = 0; i < 4000 && !world.OnDuty(worker); i++) { SafeTick(world, Step); SetNeeds(world, health: 100); }
        for (int i = 0; i < 480 && chest.Inventory["wood"] == 0; i++) SafeTick(world, Step);
        Require(chest.Inventory["wood"] > 0 && worker.Bag["wood"] == 0,
                "hired worker should gather actual wood and deliver it to a built chest");
        // Reuse the worker's real gathered output after lending them the player's axe.
        for (int i = 0; i < 8000 && chest.Inventory["wood"] < 26; i++) SafeTick(world, Step);
        world.Player = chest.Pos;
        Require(chest.Inventory.TransferTo(world.Backpack, "wood", 26) == 26, "worker output should fund the shelter and fire");
        GatherUntil(world, stone, "stone", 12);
        var pond = world.Add(ThingKind.Water, Terrain.WaterPoint(Terrain.Spawn), 9999);
        world.Player = Terrain.WaterPoint(Terrain.Spawn);
        world.Gather(pond, data.GatherSeconds);
        world.Player = Site(3, 3);
        Require(world.Build(ThingKind.Hut, Site(5, 3)) != null, "gathered materials should build the opening shelter");
        Require(world.Build(ThingKind.Campfire, Site(3, 6)) != null, "gathered materials should build the opening fire");
    }

    private static void CheckCratesShowOneSlotPerItemTheyCanHold(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(10, 10);
        var crate = world.Add(ThingKind.Crate, new Vector2(11, 10)).Inventory;
        int cap = data.CrateCapacity;
        Require(crate.ShownSlots == cap, "an empty crate should show one slot per item it can hold");
        Require(crate.Add("berries", cap + 5) == cap, "a full stack should fill the crate only up to its capacity");
        Require(crate.ShownSlots == 1, "a full crate should show one slot");
        Require(crate.Room("wood") == 0, "a full crate should have no room for more");
        crate.Remove("berries", cap / 2);
        Require(crate.ShownSlots == 1 + cap - cap / 2, "half a stack leaves one single-item slot per free unit");
        crate.Remove("berries", cap);
        foreach (var item in new[] { "wood", "stone", "berries", "water", "coal", "iron_ore", "planks", "wood_parts", "iron_bar", "steel_bar", "tin_bar", "steel_parts" }.Take(cap)) crate.Add(item, 1);
        Require(crate.UsedSlots == cap, "a crate should give each different item its own slot");
        Require(crate.Count == cap, "a crate should count as many different single items as it holds");
        Require(crate.Room("raw_meat") == 0, "a crate full of single items should have no room for more");

        // Packed crates keep their contents through the backpack, a save and placing them again.
        crate.Clear(); crate.Add("raw_meat", 5); crate.Add("steel_axe", 2);
        string packed = world.Backpack.Items.Keys.Any(World.IsCrate) ? "" : PackCrate(world);
        Require(world.Things.Count(t => t.Kind == ThingKind.Crate) == 1, "picking up should remove only the picked crate");
        var placed = world.Things.Single(t => t.Kind == ThingKind.Crate);
        Require(world.PickUpCrate(placed), "a filled crate should be picked up");
        string meat = world.Backpack.Items.Keys.Single(id => World.IsCrate(id) && id != packed);
        Require(world.Backpack.StackOf(meat) == 1, "a packed crate should be one unstackable item");
        Require(world.Describe(meat).Contains("5 Raw meat"), "a packed crate should name its contents");
        Require(world.Carried("crate") == 1, "an empty crate should be a plain crate");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Packed(meat)?["steel_axe"] == 2, "save/load should keep what a packed crate holds");
        loaded.Player = new Vector2(10, 10);
        var again = loaded.PlaceCrate(meat, new Vector2(12, 12));
        Require(again != null, "placing a packed crate should place a crate");
        Require(again.Inventory["raw_meat"] == 5, "placing a packed crate should put its contents back into the placed crate");
        Require(again.Inventory.Capacity == cap, "placing a packed crate should give it its full capacity");
        Require(loaded.Packed(meat) == null, "a loaded save should no longer hold the packed crate in the world");
        Require(loaded.Carried(meat) == 0, "a loaded save should no longer hold the packed crate in the backpack");
        Require(world.DepositFor("wood", new Vector2(10, 10)) == null, "crates are manual storage that workers never fill");
    }

    private static void CheckHandCraftingNeedsChiselAndTakesTime(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 3);
        Require(world.CraftBlock("planks")?.Contains("chisel") == true && !world.Craft("planks"), "wood crafting by hand needs a chisel");
        Require(world.CraftBlock("crate") != null && world.CraftBlock("steel_pick") != null, "crates and metal tools are bench work");
        Give(world, "stone", 2);
        Require(world.Craft("stone_chisel") && world.Carried("stone") == 0, "a stone chisel should need only 2 stone and take them at once");
        SafeTick(world, data.Recipes["stone_chisel"].Seconds - .5f);
        Require(world.Carried("stone_chisel") == 0, "hand crafting should take its time");
        SafeTick(world, 1);
        Require(world.Carried("stone_chisel") == 1 && world.BestTool("chisel") == "stone_chisel", "the chisel should arrive once its time is up");
        Require(world.Craft("planks"), "planks should queue with a chisel");
        Require(world.Craft("wood_parts"), "wood parts should queue with a chisel");
        Require(world.Carried("wood") == 1, "the two queued recipes should each take their wood");
        world.MovePlayer(new Vector2(.5f, 0));
        SafeTick(world, data.Recipes["planks"].Seconds + .25f);
        Require(world.Carried("planks") == 4 && world.Crafting.Count == 1, "crafting by hand should keep going on the go, one recipe at a time");
        world.CancelCraft(0);
        Require(world.Crafting.Count == 0, "cancelling should clear the queue");
        Require(world.Carried("wood") == 2, "cancelling should give the materials back");
        Require(world.CraftTime == 0, "cancelling should reset the craft clock");
        world.Craft("wood_parts");
        Drop(world, "stone_chisel", 1);
        SafeTick(world, 20);
        Require(world.Carried("wood_parts") == 0 && world.CraftRate == 0, "a queued recipe should wait while its tool is not carried");
        Give(world, "stone_chisel", 1);
        SafeTick(world, data.Recipes["wood_parts"].Seconds);
        Require(world.Carried("wood_parts") == 2, "it should continue once the tool is back");
    }

    private static void CheckTorchLightsAndBurnsOut(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 3);
        world.UseTorch(Vector2.Zero);
        Require(!world.Craft("torch"), "a torch should need a chisel to make");
        Require(world.Torch == 0, "using no torch should not put one in hand");
        Require(world.TorchKindling == 0, "using no torch should not start kindling");
        Give(world, "stone_chisel", 1);
        Require(world.Craft("torch") && world.Carried("wood") == 0, "a torch should take 3 wood and nothing else");
        SafeTick(world, data.Recipes["torch"].Seconds + .25f);
        Give(world, "torch", 1);
        world.UseTorch(Vector2.Zero);
        Require(world.Carried("torch") == 1, "the first use should take a torch out of the backpack");
        Require(world.Torch == data.CampfireSeconds, "the first use should put a full torch in hand");
        Require(!world.TorchLit, "the first use should not light the torch before the flint strikes");
        SafeTick(world, World.KindleSeconds - .2f);
        Require(!world.TorchLit, "lighting should take its time");
        SafeTick(world, .3f);
        Require(world.TorchLit && world.Carried("torch") == 1, "the torch should catch after the lighting animation");

        // Steady swings keep it burning and flare the light; four in two seconds put it out.
        for (int i = 0; i < 4; i++) { world.UseTorch(Vector2.Right); Require(world.TorchFlare > 0, "a swing should flare the light"); SafeTick(world, .7f); }
        Require(world.TorchLit, "swinging at a steady pace should keep the torch burning");
        SafeTick(world, World.WildWindow);
        string said = "";
        world.Notify += message => said = message;
        for (int i = 0; i < World.WildSwings; i++) { world.UseTorch(Vector2.Right); SafeTick(world, .65f); }
        float left = world.Torch;
        Require(!world.TorchLit && said == "Torch went off due to wild swinging.", "wild swinging should put the torch out with a message");
        SafeTick(world, 5);
        Require(world.Torch == left, "an unlit torch should keep its burn time");
        world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.TorchLit, "relighting should light the torch");
        Require(world.Carried("torch") == 1, "relighting should not use a new torch");
        Require(world.Torch < left, "relighting should not reset the torch's time");
        Require(world.Torch > left - 1, "relighting should not add time to the torch");
        SafeTick(world, world.Torch + .25f);
        Require(world.Torch == 0, "a torch should burn out completely");
        Require(!world.TorchLit, "a burnt out torch should not stay lit");
        Require(world.Carried("torch") == 1, "a burnt out torch should leave the spare torch in the backpack");
        world.UseTorch(Vector2.Zero); world.Die();
        Require(world.Torch == 0, "a torch should burn out when the player falls");
        Require(!world.TorchLit, "a torch should not stay lit when the player falls");
        Require(world.TorchKindling == 0, "a torch should stop kindling when the player falls");
    }

    private static void CheckTorchSwingScaresOnlyWolvesItIsAimedAt(GameData data) {
        var creeping = TorchNight(data);
        var lone = new Wolf { Pos = creeping.Player + new Vector2(8, 0), Health = 4 };
        PlaceWolf(creeping, lone);
        WolfTick(creeping, 3, lone);
        Require(lone.Pos.DistanceTo(creeping.Player) < 6.5f && lone.Pos.DistanceTo(creeping.Player) > 3.5f, "a lone wolf should creep in slowly even with a torch");

        var scared = TorchNight(data);
        var wolf = new Wolf { Pos = scared.Player + new Vector2(6, 0), Health = 4 };
        var behind = new Wolf { Pos = scared.Player + new Vector2(-6, 0), Health = 4 };
        PlaceWolf(scared, wolf); PlaceWolf(scared, behind);
        scared.UseTorch(Vector2.Right);
        Require(wolf.Scared && !behind.Scared, "a swing should scare only the wolves it is aimed at");
        RemoveWolves(scared, w => w == behind);
        WolfTick(scared, 8, wolf);
        Require(scared.Health == 100 && Math.Abs(wolf.Pos.DistanceTo(scared.Player) - World.StalkDistance) < .3f, "a scared wolf should follow at the edge of the light without biting");
    }

    private static void CheckDirectTorchHitKnocksAWolfBack(GameData data) {
        var hit = TorchNight(data);
        var close = new Wolf { Pos = hit.Player + new Vector2(2, 0), Health = 4 };
        PlaceWolf(hit, close);
        hit.UseTorch(Vector2.Right);
        float struck = close.Pos.DistanceTo(hit.Player);
        Require(close.Stagger == World.StaggerSeconds && struck > 3, "a direct torch hit should knock the wolf back and stagger it");
        WolfTick(hit, World.StaggerSeconds / 2, close);
        Require(close.Pos.DistanceTo(hit.Player) == struck, "a staggering wolf should hold still");
        WolfTick(hit, 1, close);
        Require(close.Health == 3, "a direct torch hit should hurt the wolf");
        Require(close.Flee > 0, "a direct torch hit should send the wolf running");
        Require(close.Pos.DistanceTo(hit.Player) > struck + 1, "a direct torch hit should push the wolf back");
    }

    private static void CheckWolfKilledNearTheEdgeLeavesItsCorpseOnTheMap(GameData data) {
        var edge = TorchNight(data);
        edge.Player = new Vector2(Terrain.Size - 5, 20);
        var doomed = new Wolf { Pos = edge.Player + new Vector2(1.8f, 0), Health = 1 };
        PlaceWolf(edge, doomed);
        edge.UseTorch(Vector2.Right);
        var corpse = edge.Things.Single(t => t.Kind == ThingKind.WolfCorpse);
        Require(corpse.Pos.X == Terrain.Size - 3 && corpse.Pos.X > 45,
            "a wolf killed by a torch near the map edge should leave its corpse within current map bounds");
    }

    private static void CheckFullPackGathersSlowlyAndIgnoresASwing(GameData data) {
        var gathering = TorchNight(data);
        for (int i = 0; i < 100; i++) { Nourished(gathering); gathering.Tick(.1f); }
        Require(gathering.Wolves.Count < World.PackSize, "the pack should gather slowly");

        var pack = TorchNight(data);
        var wolves = Enumerable.Range(0, World.PackSize).Select(i => new Wolf { Pos = pack.Player + Vector2.Right.Rotated(i * Mathf.Tau / World.PackSize) * World.StalkDistance, Health = 4 }).ToArray();
        foreach (var w in wolves) PlaceWolf(pack, w);
        pack.UseTorch(Vector2.Right);
        Require(wolves.All(w => !w.Scared), "a complete pack should ignore a swing");
        WolfTick(pack, 4, wolves);
        Require(pack.Health < 100, $"{World.PackSize} wolves around a torch should attack together");
    }

    private static void CheckEachToolTakesItsOwnSlot(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "water", 3); Give(world, "steel_axe", 2);
        Require(world.Backpack.UsedSlots == 5, "each refillable bottle and each tool should take its own slot");
        var crate = world.NewCrate();
        Require(crate.Add("steel_pick", data.CrateCapacity) == data.CrateCapacity && crate.UsedSlots == data.CrateCapacity, "a crate should hold a full crate of single tools");
        var chest = world.Add(ThingKind.Chest, new Vector2(10, 10));
        Require(crate.TransferTo(chest.Inventory, "steel_pick", 8) == World.ChestStacks, "a chest should take one tool per slot");
        var hands = new Worker().Bag;
        Require(hands.Add("steel_axe", 9) == Worker.BagStack && hands.Add("wood", 1) == 0, "a worker should carry five tools, one per slot");
        var mixed = new Worker().Bag;
        Require(mixed.Add("wood", 3) == 3, "a worker should carry three wood");
        Require(mixed.Add("stone", 1) == 1, "a worker should carry a stone beside the wood");
        Require(mixed.Add("steel_pick", 1) == 1, "a worker should carry a tool beside the other things");
        Require(mixed.Add("coal", 1) == 0, "a worker should carry no more than five things in all");
    }

    private static void CheckBenchRecipesWaitForSlottedTools(GameData data) {
        var world = EmptyWorld(data);
        var bench = world.Add(ThingKind.Bench, new Vector2(10, 10));
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        Give(world, "wood", 4); Give(world, "stone_chisel", 1); Give(world, "wood_hammer", 1);
        world.SetRecipe(bench, "planks");
        world.LoadBatch(bench, 2);
        SafeTick(world, 6);
        Require(!bench.Processing && bench.Inventory["planks"] == 0, "a bench recipe should wait for its tool in the slot");
        Require(!world.SlotTool(bench, "wood_hammer") || world.BenchTool(bench, "chisel") == null, "a hammer must not fill the chisel slot");
        Require(world.SlotTool(bench, "stone_chisel"), "the chisel should go into its slot");
        Require(world.Carried("stone_chisel") == 0, "the chisel should leave the backpack");
        Require(!world.SlotTool(bench, "stone_chisel"), "a second chisel should not fit the one slot");
        SafeTick(world, Step);
        SafeTick(world, data.Recipes["planks"].Seconds / data.BenchSpeed);
        Require(bench.Inventory["planks"] == 4, "a bench should work twice as fast as crafting by hand");
        var worker = AddWorker(world, bench.Pos + Vector2.One);
        var chest = world.Add(ThingKind.Chest, new Vector2(14, 10));
        chest.Inventory.Add("wood", 2);
        world.UnslotTool(bench, "stone_chisel");
        world.Assign(worker, bench);
        world.Player = new Vector2(30, 30);
        SafeTick(world, 12);
        Require(worker.Status.Contains("chisel") && world.Carried("stone_chisel") == 1, "a bench worker should say which tool the bench is missing");
        bench.Tools!.Add("stone_chisel", 1);
        SafeTick(world, 40);
        Require(chest.Inventory["planks"] >= 8 && chest.Inventory["wood"] == 0, "a bench worker should fetch wood, work the bench and store the planks");
    }

    /// <summary>Tin is mined with a pickaxe and smelted like iron; cans and the mortar need steel tools, and anything better than the tier asked for will do.</summary>
    private static void CheckTinNeedsPickaxeAndSmeltsIntoBars(GameData data) {
        var world = EmptyWorld(data);
        var fresh = new World(data);
        var cliff = World.CliffOf(ThingKind.Tin);
        Require(fresh.Things.Count(t => t.Kind == ThingKind.Tin && cliff.DistanceTo(t.Pos) < 3) == 2,
                "the tin cliff should have two deposits and no loose pieces");
        var tin = world.Add(ThingKind.Tin, Site(0, 0), 400);
        world.Player = tin.Pos;
        Require(world.GatherBlock(tin)?.Contains("pickaxe") == true, "tin should need a pickaxe");
        Give(world, "steel_pick", 1);
        GatherUntil(world, tin, "tin_ore", 4);

        var furnace = world.Add(ThingKind.Furnace, Site(3, 0));
        world.Player = furnace.Pos + new Vector2(1.2f, 0);
        Give(world, "wood", 2);
        world.SetRecipe(furnace, "tin_bar");
        world.LoadBatch(furnace, 2);
        SafeTick(world, data.Recipes["tin_bar"].Seconds * 2 + 1);
        world.Collect(furnace);
        Require(world.Carried("tin_bar") == 2 && world.Carried("tin_ore") == 0, "the stone furnace should smelt 2 tin ore into a tin bar");
    }

    private static void CheckTinCansNeedASteelHammer(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "tin_bar", 2);
        var bench = world.Add(ThingKind.Bench, Site(0, 4));
        world.Player = bench.Pos + new Vector2(1.2f, 0);
        Give(world, "wood_hammer", 1);
        world.SetRecipe(bench, "tin_can");
        Require(world.SlotTool(bench, "wood_hammer"), "a wooden mallet should fit the hammer slot");
        world.LoadBatch(bench);
        SafeTick(world, 20);
        Require(!bench.Processing, "tin cans should wait while only a wooden mallet is slotted");
        Require(bench.Inventory["tin_can"] == 0, "tin cans should not be made with a wooden mallet");
        Require(world.BenchTool(bench, "hammer", 2) == null,
            "a wooden mallet should not count as a hammer for tin cans");
        world.UnslotTool(bench, "wood_hammer");
        Give(world, "steel_hammer", 1);
        world.SlotTool(bench, "steel_hammer");
        SafeTick(world, data.Recipes["tin_can"].Seconds / data.BenchSpeed / data.Items["steel_hammer"].Speed + 1);
        Require(bench.Inventory["tin_can"] == 4, "a steel hammer should turn one tin bar into four cans");
    }

    private static void CheckSteelChiselCarvesAMortarAndWorksTwiceAsFast(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "stone", 3); Give(world, "stone_chisel", 1);
        Require(world.CraftBlock("mortar") == "Needs a steel chisel in your backpack.", "a stone chisel should not carve a mortar");
        Give(world, "steel_chisel", 1);
        Require(world.BestTool("chisel") == "steel_chisel" && world.Craft("mortar"), "a steel chisel should carve a mortar from stone by hand");
        SafeTick(world, data.Recipes["mortar"].Seconds / 2 + .25f);
        Require(world.Carried("mortar") == 1, "a steel chisel should work twice as fast");
        Give(world, "wood", 1);
        Require(world.Craft("planks"), "a steel chisel should do everything a stone chisel does");
        SafeTick(world, data.Recipes["planks"].Seconds / 2 + .25f);
        Require(world.Carried("planks") == 4, "planks should come twice as fast with a steel chisel");
    }

    private static void CheckVillageSellsSeedsAndFertilizerByTheCrate(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, Site(6, 6));
        world.Player = sign.Pos;
        SetCoins(world, 100);
        Require(world.Buy(sign, "wheat_seeds", 1) && world.Buy(sign, "fertilizer", 1), "the village should sell wheat seeds and fertilizer");
        SafeTick(world, data.TradeSeconds + .1f);
        var bags = sign.Output!.Items.Keys.Where(World.IsCrate).Select(id => world.Packed(id)!).ToList();
        Require(bags.Any(b => b["wheat_seeds"] == data.Market.Buys["wheat_seeds"]) && bags.Any(b => b["fertilizer"] == data.Market.Buys["fertilizer"]), "seeds and fertilizer should arrive by the crate");
    }

    /// <summary>Workers take the best tool their job needs from a tool station, say once when none is there, and put it back when reassigned or going home.</summary>
    private static void CheckWorkerTakesAxeFromStationAndPutsItBackForAJobWithoutTools(GameData data) {
        var (world, tree, rock, station) = ToolYard(data);
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
        world.Assign(worker, rock);
        for (int i = 0; i < 80 && worker.Tool != ""; i++) SafeTick(world, Step);
        Require(worker.Tool == "" && station.Inventory["steel_axe"] == 1, "a worker moved to stone, which needs no tool, should put the axe back");
        SafeTick(world, 10);
        Require(rock.Remaining < 400, "stone needs no tool");
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
        for (int i = 0; i < 400 && !worker.Home; i++) SafeTick(world, Step);
        Require(worker.Home, "a worker going home should be home");
        Require(worker.Tool == "", "a worker going home should put the axe back");
        Require(station.Inventory["steel_axe"] == 1, "a worker going home should leave the axe in the tool station");
        Require(worker.Assignment == tree.Id, "a worker going home should keep the assignment");

        worker.Home = false; worker.Commute = 0; worker.Tool = "steel_pick";
        world.Fire(worker);
        Require(world.Things.Any(t => t.Kind == ThingKind.Grave && t.Inventory["steel_pick"] == 1), "a dismissed worker's tool should be left in a recovery box");
    }

    /// <summary>Leaves lie under oaks, need a rake, and fall again every day.</summary>
    private static void CheckRakeClearsLeavesWhichFallAgain(GameData data) {
        var fresh = new World(data);
        var oaks = fresh.Things.Where(t => t.Kind == ThingKind.Tree && t.Id % 3 == 0).Select(t => t.Pos + World.LeafOffset).ToHashSet();
        var patches = fresh.Things.Where(t => t.Kind == ThingKind.Leaves).ToList();
        Require(patches.Count > oaks.Count * .8f && patches.All(p => oaks.Contains(p.Pos)), "oaks should drop patches of leaves beside their trunks");
        var world = EmptyWorld(data);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 10), World.LeafPatch);
        world.Player = leaves.Pos;
        Require(world.GatherBlock(leaves)?.Contains("rake") == true, "raking should need a rake");
        Give(world, "rake", 1);
        GatherUntil(world, leaves, "leaves", World.LeafPatch);
        Require(leaves.Remaining == 0 && world.GatherBlock(leaves) != null, "a raked patch should be empty until it refills");
        SafeTick(world, data.DaySeconds - world.Time % data.DaySeconds + 1);
        Require(leaves.Remaining == World.LeafPatch, "leaves should fall again by the next day");

        var station = world.Add(ThingKind.Toolstation, new Vector2(14, 10));
        station.Inventory.Add("rake", 1);
        world.Add(ThingKind.Chest, new Vector2(12, 12));
        var worker = AddWorker(world, station.Pos);
        world.Assign(worker, leaves);
        world.Player = new Vector2(30, 30);
        SafeTick(world, 20);
        Require(worker.Tool == "rake" && leaves.Remaining < World.LeafPatch, "a worker should fetch the rake and rake leaves");
    }

    /// <summary>Piles snap into grids, hold one resource whose filters follow what is in them, and compost turns leaves and food into fertilizer.</summary>
    private static void CheckPilesSnapTogetherHoldOneResourceAndMakeCompost(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin + new Vector2(0, 2);
        Give(world, "wood", 40);
        var pile = world.Build(ThingKind.Pile, origin);
        Require(pile != null, "a pile should be built");
        Require(world.Carried("wood") == 32, "a pile should cost 8 wood");
        Require(pile!.Inventory.Slots == World.PileStacks, "a pile should hold 14 stacks");
        Require(world.Add(ThingKind.Chest, origin + new Vector2(0, 8)).Inventory.Slots == 5, "a chest should hold a third of a pile, 5 stacks");
        Require(world.Snap(ThingKind.Pile, origin + new Vector2(3.4f, .4f)) == origin + new Vector2(3, 0) && world.Snap(ThingKind.Pile, origin + new Vector2(9, 0)) == origin + new Vector2(9, 0),
                "a pile placed beside another should snap edge to edge, and one far away should not");
        var east = world.Build(ThingKind.Pile, origin + new Vector2(3.3f, 0));
        Require(east != null, "a pile beside another should build");
        Require(world.PlacementBlock(ThingKind.Pile, origin + new Vector2(2, 0)) != null && east.Pos == origin + new Vector2(3, 0), "piles must not overlap but may touch");
        Require(world.CanStand(origin), "a pile should be walkable");

        world.Player = origin;
        Require(world.Transfer(world.Backpack, pile!.Inventory, "wood", 10) == 10 && world.HoldBlock(pile, "stone") != null, "the first resource in should be the only one the pile takes");
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.SetEquals(new[] { "wood" }) && pile.Inventory.OutputFilter.SetEquals(new[] { "wood" }), "a pile's filters should follow its one resource");
        var chest = world.Add(ThingKind.Chest, origin + new Vector2(0, 5));
        Require(world.DepositFor("wood", chest.Pos) == pile && world.DepositFor("stone", origin) != pile, "workers should prefer the matching pile and keep other goods out of it");
        pile.Inventory.Remove("wood", 10);
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.Count == 0 && world.Holds(pile, "stone"), "emptying a pile should clear its filters");

        Require(world.SetCompost(east, true), "compost should be set on the pile");
        Require(east.Inventory.InputFilter.Contains("leaves"), "compost should take leaves");
        Require(east.Inventory.InputFilter.Contains("crushed_berries"), "compost should take crushed berries");
        Require(!east.Inventory.InputFilter.Contains("stone"), "compost should refuse stone");
        Require(east.Inventory.OutputFilter.SetEquals(new[] { "fertilizer" }), "compost should give only fertilizer");
        east.Inventory.Add("leaves", 6); east.Inventory.Add("burnt_steak", 2);
        Require(world.HoldBlock(east, "stone") != null && !world.SetCompost(east, false), "compost should refuse stone and stay compost while mixed");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 1 && east.Inventory.Count == 5, "4 inputs should rot into 1 fertilizer in an in-game hour");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 2 && east.Inventory.Items.Keys.All(k => k == "fertilizer"), "compost should keep going while it has 4 inputs");
        Require(world.SourceFor("fertilizer", origin) == east, "workers should be able to take the fertilizer out");

        var station = world.Add(ThingKind.Toolstation, origin + new Vector2(-4, 4));
        station.Inventory.Add("rake", 1);
        var leaves = world.Add(ThingKind.Leaves, origin + new Vector2(-4, -4), World.LeafPatch);
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
    private static void CheckCookingTableCrushesBerriesAndCooksJam(GameData data) {
        var world = EmptyWorld(data);
        var table = world.Add(ThingKind.Cooktable, new Vector2(20, 20));
        world.Player = table.Pos + new Vector2(1, 0);
        Give(world, "mortar", 1); Give(world, "stone_chisel", 1); Give(world, "berries", 8);
        Require(!world.SlotTool(table, "stone_chisel"), "the cooking table should refuse a chisel");
        Require(world.SlotTool(table, "mortar"), "the cooking table should slot a mortar");
        Require(world.BenchTool(table, "mortar") == "mortar",
            "the cooking table's slotted mortar should count as its tool");
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
        table.Fuel = "coal"; Give(world, "coal", 1);
        Require(world.BatchCost(table).SequenceEqual(new Dictionary<string, int> { ["crushed_berries"] = 2 }) && world.FuelOf(table) == "coal", "jam should take crushed berries and burn the chosen fuel");
        world.LoadBatch(table);
        world.Player = new Vector2(40, 40);
        SafeTick(world, data.Recipes["berry_jam"].Seconds + 1);
        Require(table.Inventory["berry_jam"] == 1 && table.Burner!["coal"] == 0, "jam should cook on its own over the fire");
        world.SetRecipe(table, "dough");
        Require(world.BatchCost(table).Keys.Order().SequenceEqual(new[] { "flour", "water" }), "dough should need no fire");
    }

    private static void CheckCanneryAndFurnaceTakeTheirFoodRecipes(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(40, 40);
        var cannery = world.Add(ThingKind.Cannery, new Vector2(24, 20));
        world.SetRecipe(cannery, "canned_meat_stew");
        cannery.Inventory.Add("meat_stew", 1); cannery.Inventory.Add("tin_can", 1); cannery.Burner!.Add("wood", 1);
        SafeTick(world, data.Recipes["canned_meat_stew"].Seconds + 1);
        Require(cannery.Inventory["canned_meat_stew"] == 1, "the canning station should seal stew into a can");
        Require(cannery.Inventory.Count == 1, "the canning station should use up its ingredients");
        Require(cannery.Burner.Count == 0, "the canning station should burn its wood");

        var furnace = world.Add(ThingKind.Furnace, new Vector2(28, 20));
        world.SetRecipe(furnace, "bread");
        Require(world.BatchCost(furnace).Keys.SequenceEqual(new[] { "dough" }) && world.FuelOf(furnace) == "wood", "bread should bake from dough over the furnace's fire");
    }

    /// <summary>By hand, the mortar is carried like any other tool.</summary>
    private static void CheckCrushingBerriesByHandNeedsACarriedMortar(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "mortar", 1); Give(world, "berries", 4);
        Require(world.CraftBlock("crushed_berries") == null, "berries should be crushed by hand with a carried mortar");
        Drop(world, "mortar", 1);
        Require(world.CraftBlock("crushed_berries") != null, "crushing berries by hand should need the mortar");
    }

    /// <summary>
    /// A farm plot snaps beside a pile, sows itself from seeds and fertilizer, shows its growth over its days, and is harvested by the player and a worker sharing one progress.
    /// The worker carries the wheat to storage rather than to compost and the grass to compost, keeps the seeds and sows again.
    /// </summary>
    private static void CheckFarmPlotBuildsBesidePileAndTakesOnlyFarmGoods(GameData data) {
        var (world, compost, farm) = FarmBesideCompost(data);
        Require(farm.Pos == compost.Pos + new Vector2(3, 0), "a farm plot should snap edge to edge with a pile");
        Require(world.CanStand(farm.Pos), "a plot should be walkable");
        Require(!world.Holds(farm, "stone"), "a plot should take only farm goods");
        Require(world.GatherBlock(farm) != null, "a plot should have nothing to harvest before sowing");
    }

    private static void CheckSownWheatGrowsToRipeInItsDays(GameData data) {
        var (world, _, farm) = FarmBesideCompost(data);
        world.Transfer(world.Backpack, farm.Inventory, "wheat_seeds", 1); world.Transfer(world.Backpack, farm.Inventory, "fertilizer", 1);
        world.Tick(.1f);
        Require(farm.FarmStage == FarmStage.Growing, "seeds and fertilizer should sow the plot");
        Require(farm.Inventory.Count == 0, "sowing should use up the seeds and fertilizer");
        Require(World.CropLook(farm, world.GrowSeconds) == 1, "a sown plot should show soil");
        var looks = new List<int>();
        for (int day = 0; day < 3; day++) { SafeTick(world, data.DaySeconds - 1); looks.Add(World.CropLook(farm, world.GrowSeconds)); SafeTick(world, 1); }
        Require(looks.SequenceEqual(new[] { 1, 2, 3 }),
            $"wheat should grow through soil, straws and green plants: {string.Join(",", looks)}");
        Require(farm.FarmStage == FarmStage.Ripe, $"wheat should be ripe in {data.Farm.GrowDays} days");
        Require(World.CropLook(farm, world.GrowSeconds) == 4, "ripe wheat should look ripe");
    }

    /// <summary>A plot harvested with nobody to haul keeps its seed in the output: nothing sows it back on its own.</summary>
    private static void CheckPlotHarvestedAloneKeepsSeedInItsOutput(GameData data) {
        var world = EmptyWorld(data);
        var lone = world.Add(ThingKind.Farm, new Vector2(20, 14));
        lone.FarmStage = FarmStage.Ripe; world.Player = lone.Pos;
        world.Gather(lone, world.HarvestSeconds); world.Tick(1);
        Require(lone.FarmStage == FarmStage.Fallow, "a harvested plot should lie fallow");
        Require(lone.Output!["wheat_seeds"] == 2, "harvested seed should wait in the output");
        Require(lone.Inventory["wheat_seeds"] == 0, "harvested seed should not go into the plot by itself");
    }

    private static void CheckWorkerFinishesHarvestHaulsCropAndSowsAgain(GameData data) {
        var (world, compost, farm) = FarmBesideCompost(data);
        farm.FarmStage = FarmStage.Ripe;
        var origin = compost.Pos;
        world.Player = farm.Pos;
        world.Gather(farm, world.HarvestSeconds / 2);
        Require(farm.FarmStage == FarmStage.Ripe && Math.Abs(farm.Progress - world.HarvestSeconds / 2) < .01f, "the player should harvest by hand, without a tool");
        var chest = world.Add(ThingKind.Chest, origin + new Vector2(3, 4));
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, farm);
        Give(world, Inventory.Coins, 500);
        // The player waits out the nights in a cabin, out of the wolves' reach.
        var hut = world.Add(ThingKind.Hut, new Vector2(40, 40));
        world.Player = hut.Pos + new Vector2(0, 2); world.Enter(hut);
        for (int i = 0; i < 4 * data.DaySeconds && farm.FarmStage == FarmStage.Ripe; i++) SafeTick(world, 1);
        Require(farm.FarmStage == FarmStage.Fallow && farm.Output!["wheat_seeds"] + farm.Inventory["wheat_seeds"] == 2, "a worker should finish the shared harvest into the plot");
        SafeTick(world, 30);
        Require(farm.Inventory["wheat_seeds"] == 1 && chest.Inventory["wheat_seeds"] == 1,
                "the hauler should move one sowing's seeds from the output into the plot and carry the rest to storage");
        Require(chest.Inventory["wheat"] == 4, "the wheat should go to storage");
        Require(compost.Inventory["wheat"] == 0, "the wheat should not rot in the compost");
        Require(compost.Inventory["grass"] == 1, "the grass should go to the compost");
        chest.Inventory.Add("fertilizer", 1);
        SafeTick(world, 60);
        Require(farm.FarmStage == FarmStage.Growing, "the worker should sow again");
        Require(farm.Inventory["wheat_seeds"] == 0, "the worker should use the seeds moved back in");
        Require(chest.Inventory["fertilizer"] == 0, "the worker should bring the fertilizer");
    }

    /// <summary>Foods restore what their data says, raw ones only say they need cooking, and saves from before the rename keep their cooked meat.</summary>
    private static void CheckFoodsFeedWaterAndHealByTheirData(GameData data) {
        var world = EmptyWorld(data);
        SetNeeds(world, 20, 20, 50);
        Give(world, "meat_stew", 1); Give(world, "wheat", 1);
        world.Consume("meat_stew");
        var stew = data.Items["meat_stew"];
        Require(world.Hunger == 20 + stew.Hunger, "meat stew should feed by its data");
        Require(world.Thirst == 20 + stew.Thirst, "meat stew should water by its data");
        Require(world.Health == 50 + stew.Health, "meat stew should heal by its data");
        Require(world.Carried("meat_stew") == 0, "meat stew should be used up");
        world.Consume("wheat");
        Require(world.Carried("wheat") == 1, "raw wheat should not be eaten as it is");
        Require(data.IsFood("wheat"), "raw wheat should count as food for compost and wolves");
        Require(data.IsConsumable("wheat"), "raw wheat should count as consumable");
        Require(!data.IsFood("water"), "water should not be food");
        Require(data.IsConsumable("water"), "water should be drinkable");
        Require(!data.IsFood("tin_can"), "cans should not be food");

        var old = EmptyWorld(data);
        var furnace = old.Add(ThingKind.Furnace, new Vector2(10, 10));
        furnace.Recipe = "burnt_steak"; furnace.Inventory.Add("burnt_steak", 2);
        Give(old, "burnt_steak", 3);
        var json = Patch(old.Serialize(), ("\"burnt_steak\"", "\"cooked_meat\""));
        var loaded = EmptyWorld(data);
        loaded.Deserialize(json);
        Require(loaded.Carried("burnt_steak") == 3,
            "cooked meat from older saves should become burnt steak in the backpack");
        var f = loaded.Find(furnace.Id);
        Require(f?.Recipe == "burnt_steak",
            "the furnace's cooked meat recipe from older saves should become burnt steak");
        Require(f.Inventory["burnt_steak"] == 2, "cooked meat in an older save's furnace should become burnt steak");
    }

    private static void CheckMarketTakesOnlyCrates(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        string axes = PackCrate(world, ("steel_chisel", 1), ("steel_axe", 1));
        string empty = PackCrate(world);
        Require(!world.CanSell(axes), "the village should not take a crate holding tools it does not buy");
        Require(!world.CanSell(empty), "the village should not take an empty crate");
        Require(!world.CanSell("crate"), "the village should not take a bare crate");
        Require(!world.CanSell("steel_axe"), "the village should not take a loose steel axe");
        // The post refuses this crate by hand, so this is what an old save might hold.
        Drop(world, axes, 1); sign.Inventory.Add(axes, 1);
        Require(!world.Sell(sign) && world.Packed(axes) != null, "a crate holding a tool the village does not buy stays put");
        string meat = PackCrate(world, ("raw_meat", 6), ("burnt_steak", 6));
        string tools = PackCrate(world, ("steel_hammer", 1), ("steel_pick", 1));
        world.Player = sign.Pos;
        world.Transfer(world.Backpack, sign.Inventory, meat, 1); world.Transfer(world.Backpack, sign.Inventory, tools, 1);
        Require(world.Sell(sign), "a cart should take every acceptable crate");
        Require(world.Orders.Single().Crates == 2, "a cart should count the crates it takes");
        Require(sign.Inventory[axes] == 1, "a cart should leave the crate of tools the village does not buy");
        Require(world.Packed(meat) == null, "a cart should take away the crate of meat it sells");
        Require(world.Orders.Single().Goods["raw_meat"] == 6 && world.Orders.Single().Goods["steel_pick"] == 1, "the sale should pay for what the crates hold");
        SetCoins(world, 1000);
        Require(world.Buy(sign, "canned_food", 2), "boxed goods should be bought from the post");
        Require(world.Orders.Last().Crates == 2, "boxed goods should be bought by the crate");
        Require(world.Coins == 830, "boxed goods should cost the crate price");
        Require(world.LotPrice("canned_food") > data.Market.Buys["canned_food"] * world.UnitPrice("canned_food", false), "a bought crate costs more than its contents would sell for");
        SafeTick(world, data.TradeSeconds + .1f);
        var bought = sign.Output!.Items.Keys.Where(World.IsCrate).ToList();
        Require(bought.Count == 2 && bought.All(id => world.Packed(id)?["canned_food"] == data.Market.Buys["canned_food"]), "each bought crate should arrive packed with one purchase");
        SetNeeds(world, hunger: 40); Give(world, "canned_food", 1); world.Consume("canned_food");
        Require(world.Hunger > 40 && data.IsFood("canned_food"), "canned food should be edible");
        Require(world.KnownPrice("raw_meat") > 0 && !world.Priced("canned_food"), "the price sheet only tracks goods the village buys");
    }

    private static void CheckLoadedSaveDropsOldStageNumber(GameData data) {
        var old = EmptyWorld(data);
        var mine = old.Add(ThingKind.Mine, World.MineSpot(World.CliffOf(ThingKind.Iron))); mine.MineStage = MineStage.Supplied;
        var farm = old.Add(ThingKind.Farm, new Vector2(20, 20)); farm.FarmStage = FarmStage.Ripe;
        old.Look();
        // Older saves kept both stages in one number.
        var save = JsonNode.Parse(old.Serialize())!.AsObject();
        foreach (var thing in save["Things"]!.AsArray().Concat(save["Remembered"]!.AsObject().Select(pair => pair.Value))) {
            var fields = thing!.AsObject();
            int stage = (int)fields["MineStage"]! + (int)fields["FarmStage"]!;
            fields.Remove("MineStage"); fields.Remove("FarmStage"); fields["Stage"] = stage;
        }
        var loaded = EmptyWorld(data);
        loaded.Deserialize(save.ToJsonString());
        Require(loaded.Find(mine.Id)?.MineStage == MineStage.Supplied,
            "an older save's stage number should load as the mine's stage");
        Require(loaded.Find(farm.Id)?.FarmStage == FarmStage.Ripe,
            "an older save's stage number should load as the farm plot's stage");
        Require(loaded.Find(farm.Id)?.MineStage == MineStage.Marked,
            "an older save's stage number should leave the farm plot's mine stage at its default");
        Require(!loaded.Serialize().Contains("\"Stage\""), "a loaded save should no longer write the old stage number");
    }

    private static void CheckOlderSavesResetMadeToolAnvilsAndStartWaterGoal(GameData data) {
        var old = EmptyWorld(data);
        var anvil = old.Add(ThingKind.Anvil, new Vector2(10, 10));
        anvil.Recipe = "steel_pick"; anvil.Inventory.Add("iron_bar", 3);
        Give(old, "steel_pick", 1); Give(old, "steel_hammer", 1); Give(old, "iron_bar", 2);
        old.Add(ThingKind.Stone, new Vector2(20, 20), 2);
        Achieve(old, "gather", "furnace", "iron_bar", "anvil", "pick");
        SetGoal(old, 5);
        // Older saves had steel tools as one item, and iron tools and loose iron that have since left the game.
        var json = Patch(old.Serialize(), ("\"iron_bar\": 2", "\"steel_tools\": 3"), ("\"steel_pick\"", "\"iron_pick\""), ("\"steel_hammer\"", "\"iron_hammer\""), ("\"stone\"", "\"loose_iron\""));
        var loaded = EmptyWorld(data);
        loaded.Deserialize(json);
        Require(loaded.Carried("steel_axe") == 1 && loaded.Carried("steel_parts") == 2,
                "old steel tools become steel parts, except one that becomes the missing axe");
        Require(loaded.Carried("steel_pick") == 1, "old iron pickaxes should become steel ones");
        Require(loaded.Carried("steel_hammer") == 1, "old iron hammers should become steel ones");
        Require(loaded.Things.Count == old.Things.Count(t => t.Kind != ThingKind.Stone), "loose iron should be gone");
        Require(loaded.Find(anvil.Id)!.Recipe == "" && loaded.Find(anvil.Id)!.Inventory["iron_bar"] == 3, "an anvil set to a tool now made at the bench is reset and keeps its inputs");
        Require(loaded.Goal == 0, "an older save should begin with the new collect-water survival goal");
    }

    /// <summary>
    /// Every item and building must be reachable from a fresh start with only gathering, recipes and berry trade, and the tools that unlock the loop must never be lost.
    /// Resources need their tool first: trees an axe, deposits a pickaxe; stone, berries, water and loose pieces need none.
    /// </summary>
    private static void CheckEverythingIsReachableFromFreshStart(GameData data) {
        var fresh = new World(data);
        var have = new HashSet<string>(fresh.Backpack.Items.Keys) { "stone", "berries", "water", "water_1", "water_2", "raw_meat" };
        var built = new HashSet<ThingKind>();
        bool HasTool(string tool, int tier = 0) => have.Any(id => data.Items[id].Tool == tool && data.Items[id].Tier >= tier);
        for (bool grew = true; grew;) {
            grew = false;
            if (HasTool("axe")) grew |= have.Add("wood");
            foreach (var ore in new[] { "iron_ore", "coal", "tin_ore" }) if (HasTool("pickaxe")) grew |= have.Add(ore);
            // Crates of berries sold at a signpost buy what the village sells, the first pickaxe among it.
            if (built.Contains(ThingKind.Signpost) && have.Contains("crate") && have.Contains("berries")) foreach (var item in data.Market.Buys.Keys) grew |= have.Add(item);
            if (HasTool("rake")) grew |= have.Add("leaves");
            foreach (var (id, b) in data.Buildings) if (b.Cost.Keys.All(have.Contains)) grew |= built.Add(id);
            if (built.Contains(ThingKind.Pile) && have.Any(data.IsFood)) grew |= have.Add("fertilizer");
            // Seeds are bought with berry money to start the first plot.
            if (built.Contains(ThingKind.Farm) && data.Farm.Sow.Keys.All(id => have.Contains(id) || data.Market.Buys.ContainsKey(id))) foreach (var item in data.Farm.Yield.Keys) grew |= have.Add(item);
            foreach (var r in data.Recipes.Values) {
                bool where = r.Hand && (r.Tool == "" || HasTool(r.Tool, r.Tier)) || r.Station is ThingKind station && built.Contains(station) && (World.SlotKinds(station).Length == 0 || r.Tool == "" || HasTool(r.Tool, r.Tier));
                if (where && r.Inputs.Keys.All(have.Contains)) grew |= have.Add(r.Output);
            }
        }
        var unreachable = data.Items.Keys.Where(id => id != Inventory.Coins && !data.Market.Buys.ContainsKey(id) && !have.Contains(id)).Concat(data.Buildings.Keys.Where(kind => !built.Contains(kind)).Select(kind => kind.Id())).ToList();
        Require(unreachable.Count == 0, $"everything should be reachable from a fresh start: {string.Join(", ", unreachable)}");
        // The village never takes the tools that make crates, so selling cannot strand the player.
        Require(!data.Market.Sells.Keys.Any(id => id is "stone_chisel" or "wood_hammer"), "bootstrap tools must not be sellable");
        // The first pickaxe, and a lost axe, are bought with berry money; a crate needs only planks and a mallet.
        Require(data.Market.Sells.ContainsKey("berries"), "berries must be sellable");
        Require(data.Market.Buys.ContainsKey("steel_axe"), "an axe must be buyable");
        Require(data.Market.Buys.ContainsKey("steel_pick"), "a pickaxe must be buyable");
        float berryCrate = data.CrateCapacity * data.Items["berries"].Price + data.Items["crate"].Price;
        Require(fresh.LotPrice("steel_pick") <= 4 * berryCrate, "four crates of berries should pay for the first pickaxe");
    }

    private static void CheckSleepNeedsFoodAndWater(GameData data) {
        Require(Math.Abs(100 / data.HungerPerSecond - data.DaySeconds) < .01f && Math.Abs(100 / data.ThirstPerSecond - data.DaySeconds) < .01f,
                "a full stomach should last one in-game day and a full water droplet one day");
        foreach (var (hunger, thirst, reason) in new[] { (10f, 100f, "hungry"), (100f, 10f, "thirsty") }) {
            var world = NightWorld(data);
            var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0));
            SetNeeds(world, hunger, thirst);
            float time = world.Time;
            world.Sleep(hut);
            Require(world.Time == time && world.SleepBlock()?.Contains(reason) == true,
                    $"sleep must be refused when the player is too {reason} to last until morning");
        }
        var early = EmptyWorld(data);
        early.Add(ThingKind.Hut, early.Player + new Vector2(2, 0));
        RunUntilPhase(early, (World.SleepHour - 6 - .2f) / 24);
        Require(early.SleepBlock()?.Contains("10 pm") == true, "sleep must be refused before 10 pm");
        RunUntilPhase(early, (World.SleepHour - 6 + .2f) / 24);
        Require(!early.Night && early.SleepBlock() == null, "sleep must be allowed from 10 pm, before night falls");
    }

    private static void CheckSprintBurnsFood(GameData data) {
        (float Hunger, float Thirst) Burned(bool sprinting) {
            var world = new World(data);
            world.Me.Sprinting = sprinting;
            for (int i = 0; i < 10; i++) world.Tick(1);
            return (100 - world.Hunger, 100 - world.Thirst);
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
            PlaceWolf(world, wolf);
            world.Me.Sprinting = sprinting;
            var start = world.Player;
            for (int i = 0; i < 40; i++) { world.MovePlayer(Vector2.Right * .05f * data.WalkSpeed * (sprinting ? data.SprintSpeed : 1)); world.Tick(.05f); }
            Require(world.Player.DistanceTo(start) > 7, "the open ground east of the test spot must let the player flee");
            return wolf.Pos.DistanceTo(world.Player);
        }
        Require(Gap(false) < 3, "a chasing wolf must be faster than a walking player");
        Require(Gap(true) > 4.1f, "a sprinting player must outrun a chasing wolf");
    }

    private static void CheckCabinShelterAndChasingWolfGivesUpInLight(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0));
        var wolf = new Wolf { Pos = world.Player + new Vector2(-4, 0) };
        PlaceWolf(world, wolf);
        SafeTick(world, .5f);
        Require(world.Enter(hut) && world.Sheltered == hut.Id, "clicking a nearby cabin should take the player inside");
        float before = wolf.Pos.DistanceTo(world.Player);
        SetNeeds(world, health: 40);
        SafeTick(world, 1);
        Require(wolf.Pos.DistanceTo(world.Player) > before,
            "a wolf must back off at once when the player is inside a cabin");
        Require(wolf.Leap <= 0, "a wolf must stop its leap when the player is inside a cabin");
        Require(world.Health >= 40, "a wolf must not hurt a player inside a cabin");
        var inside = world.Player;
        world.MovePlayer(new Vector2(0, .1f));
        Require(world.Sheltered == hut.Id && world.Player == inside, "moving should not take the player back outside");
        // A chaser that caught up stands right on the player; it must still back off and give up.
        var caught = new Wolf { Pos = world.Player, Target = world.Me.Id };
        PlaceWolf(world, caught);
        SafeTick(world, 10);
        Require(!world.Wolves.Contains(caught) && !world.Wolves.Contains(wolf), "wolves should leave once the player has been inside long enough");
        world.Leave();
        Require(world.Sheltered == 0, "stepping outside should leave the cabin");
        world.Player = hut.Pos + new Vector2(12, 0);
        SetNeeds(world, health: -1);
        for (int i = 0; i < 200 && world.Sheltered == 0; i++) { world.Tick(.25f); SetNeeds(world, health: Math.Max(world.Health, 1)); }
        Require(world.Sheltered == hut.Id, "a fallen player should wake inside the nearest cabin");
        Require(world.Player.DistanceTo(hut.Pos) < 3, "a fallen player should wake beside that cabin");
        Require(world.CanStand(world.Player), "a fallen player should wake on standable ground");

        var lit = NightWorld(data);
        var chaser = new Wolf { Pos = lit.Player + new Vector2(-5, 0) };
        PlaceWolf(lit, chaser);
        SafeTick(lit, .5f);
        lit.Add(ThingKind.Campfire, lit.Player + new Vector2(2, 0)).Burn = 300;
        float gap = chaser.Pos.DistanceTo(lit.Player);
        SafeTick(lit, .5f);
        Require(chaser.Pos.DistanceTo(lit.Player) < gap, "a wolf already chasing should follow the player briefly into firelight");
        SafeTick(lit, 4);
        gap = chaser.Pos.DistanceTo(lit.Player);
        SafeTick(lit, .5f);
        Require(chaser.Pos.DistanceTo(lit.Player) > gap, "a chasing wolf should give up and back away after a few seconds in the light");
    }

    private static void CheckOnlyCoinsUseTheLargerStack(GameData data) {
        var bag = new Inventory { Slots = 2 };
        Require(bag.Add(Inventory.Coins, 1500) == 1500, "every coin should fit in the bag");
        Require(bag.UsedSlots == 2, "coins should stack to 1000 per slot, so 1500 need two slots");
        Require(bag.Stacks().First().Count == 1000, "the first coin stack should hold 1000");
        Require(bag.Room(Inventory.Coins) == 500 && bag.Room("wood") == 0, "only coins should use the larger stack");

        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        SetCoins(world, 5);
        world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("steel_axe", 1)), 1);
        Require(world.Sell(sign), "a loaded crate should sell");
        SafeTick(world, data.TradeSeconds + .1f);
        Require(world.Coins == 5 && sign.Wages![Inventory.Coins] == 40, "sale proceeds for the goods and their crate should arrive as whole coins in the wage slot");
        sign.Inventory.Add(Inventory.Coins, 1);
        Require(!world.Sell(sign) && sign.Inventory[Inventory.Coins] == 1, "coins themselves cannot be sold");
        Require(!world.Buy(sign, Inventory.Coins, 1), "coins themselves cannot be bought");
    }

    private static void CheckWageSlotTakesOnlyCoinsAndPaysWagesFirst(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        int full = sign.Wages!.StackOf(Inventory.Coins);
        sign.Wages.Add(Inventory.Coins, full - 5);
        PlaceOrder(world, new TradeOrder { Goods = new() { ["wood"] = 1 }, Buying = false, Value = 12, Signpost = sign.Id });
        SafeTick(world, data.TradeSeconds + .1f);
        Require(sign.Wages[Inventory.Coins] == full && sign.Output![Inventory.Coins] == 7, "sale coins should top up the one-stack wage slot and spill the rest into the output");
        Give(world, "wood", 1);
        Require(world.Transfer(world.Backpack, sign.Wages, "wood", 1) == 0 && world.Carried("wood") == 1, "the wage slot should refuse anything but coins, even by hand");

        var worker = AddWorker(world, sign.Pos);
        sign.Wages.Remove(Inventory.Coins, full - 3);
        SetCoins(world, 10);
        world.PayWages();
        Require(worker.WageDebt == 0, "a worker's wage should be paid in full");
        Require(sign.Wages[Inventory.Coins] == 0, "wages should come out of the wage slot first");
        Require(world.Coins == 10 - (worker.Wage - 3), "wages should come out of the backpack after");
        SetCoins(world, 0);
        world.PayWages();
        Require(worker.WageDebt == worker.Wage && sign.Output![Inventory.Coins] == 7, "wages should not be taken from the market output");
    }

    /// <summary>A save naming an item the game does not know, in the backpack or as a worker's tool, is refused with a clear reason instead of failing mid-game.</summary>
    private static void CheckSaveWithUnknownItemRefused(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "tin_can", 2);
        var worker = AddWorker(world, new Vector2(12, 12));
        worker.Tool = "steel_hammer"; worker.Bag.Add("wood", 2);
        string json = world.Serialize();
        EmptyWorld(data).Deserialize(json);
        foreach (var edit in new[] { ("\"tin_can\"", "\"unobtainium\""), ("\"Tool\": \"steel_hammer\"", "\"Tool\": \"unobtainium\""), ("\"wood\": 2", "\"unobtainium\": 2") }) {
            bool refused = false;
            try { EmptyWorld(data).Deserialize(Patch(json, edit)); } catch (InvalidOperationException error) { refused = error.Message.Contains("unknown items"); }
            Require(refused, $"a save with {edit.Item2} in place of {edit.Item1} should be refused with a clear reason");
        }
    }

    private static void CheckOlderSaveMigratesCoinsMarketBoxesAndOrders(GameData data) {
        var old = EmptyWorld(data);
        var sign = old.Add(ThingKind.Signpost, new Vector2(10, 10));
        PlaceOrder(old, new TradeOrder { Buying = true, Value = 8, Signpost = sign.Id });
        // Older saves kept coins outside the backpack and one item per order.
        var json = System.Text.Json.Nodes.JsonNode.Parse(old.Serialize())!;
        json["Coins"] = 30;
        var post = json["Things"]!.AsArray().First(t => t!["Id"]!.GetValue<int>() == sign.Id)!;
        post["Output"] = null; post["Wages"] = null; post["Inventory"]!["Slots"] = 18;
        var order = json["Orders"]![0]!;
        order["Goods"] = new System.Text.Json.Nodes.JsonObject(); order["Item"] = "wood"; order["Quantity"] = 4;
        var loaded = EmptyWorld(data);
        loaded.Deserialize(json.ToJsonString());
        Require(loaded.Coins == 30, "coins from an older save should move into the backpack");
        Require(OldCoins(loaded) == null, "an older save's loose coins should not stay behind after moving");
        Require(loaded.Find(sign.Id) is { Output.Slots: World.MarketSlots, Inventory.Slots: World.MarketSlots, Wages.Slots: 1 }, "an older signpost should get its market boxes");
        Require(loaded.Orders.Single().Goods.GetValueOrDefault("wood") == 4 && loaded.Orders.Single().Item == null, "an older single-item order should keep its goods");
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
        var furnace = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        furnace.Heat = World.SteelHeat + 5;
        // The worker starts far off, so the forge would cool past the bar before they arrive to pump.
        world.Assign(AddWorker(world, new Vector2(30, 30)), furnace);
        SafeTick(world, 4);
        Require(furnace.Heat >= World.SteelHeat, "a working crew should keep the forge at or above the clear bar");
        var crew = world.Workers[0];
        crew.WageDebt = crew.Wage;
        SafeTick(world, 5);
        Require(furnace.Heat < World.SteelHeat, "a striking crew should let the forge cool below the clear bar");
        foreach (var w in world.Workers.ToList()) world.Fire(w);
        SafeTick(world, 5);
        Require(furnace.Heat < World.SteelHeat, "without a crew the forge should cool below the clear bar");

        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        var runner = AddWorker(world, sign.Pos);
        world.Player = sign.Pos; world.Assign(runner, sign);
        Require(world.FetchPrices(sign), "the assigned worker should set off for prices");
        SafeTick(world, data.TradeSeconds / 4);
        world.Assign(runner, null);
        Require(runner.Away == 0, "reassigning a runner should call off the errand rather than freeze them on the road");
    }

    private static void CheckCrewLunchesAtTheNearestFireAndGoesBackToWork(GameData data) {
        var (world, tree, _, near, forge, cutter, pumper, hauler) = StartDayCrew(data);
        float At(float hour) => (hour - 6) / 24 * data.DaySeconds;
        RunUntil(world, At(data.LunchHour) - 5);
        forge.Heat = 100;
        SafeTick(world, 25);
        Require(world.Lunch, "the clock should be at lunch hour");
        Require(world.LunchFire == near, "at lunch the crew's fire should be the one closest to them");
        Require(world.Workers.All(w => w.WorkState == WorkerWorkState.Resting && w.Pos.DistanceTo(near.Pos) < 2),
            "at lunch the whole crew should sit around the campfire");
        Require(cutter.Assignment == tree.Id, "lunch should keep the cutter's assignment");
        Require(pumper.Assignment == forge.Id, "lunch should keep the pumper's assignment");
        Require(hauler.Assignment == forge.Id, "lunch should keep the hauler's assignment");
        Require(forge.Heat < World.SteelHeat, "with the crew at lunch the forge should cool below the clear bar");
        SafeTick(world, At(data.LunchHour + 1) - world.Time + 10);
        Require(cutter.WorkState == WorkerWorkState.Gathering && pumper.WorkState == WorkerWorkState.Pumping,
                "after lunch workers should walk back to their jobs");
    }

    private static void CheckCrewSignsOutAtDuskKeepingLoadsAndJobs(GameData data) {
        var crew = StartDayCrew(data);
        var (world, tree, _, _, forge, cutter, pumper, _) = crew;
        var sign = SignOutCrew(crew);
        Require(world.Workers.All(w => w.Home && !world.Sees(w) && w.Commute == data.TradeSeconds / 2), "at dusk every worker should walk off like a cart and sign out");
        // Walking arrives within reach of the signpost, so the walk-off starts up to that far from it.
        Require(cutter.Pos.DistanceTo(sign.Pos + Vector2.One + Terrain.Seaward * World.TripDistance) < 1.25f,
                "a worker should sign out a cart's trip down the valley from the signpost");
        Require(cutter.Bag["wood"] == 2, "signing out should keep carried items");
        Require(cutter.Assignment == tree.Id, "signing out should keep the cutter's assignment");
        Require(pumper.Assignment == forge.Id, "signing out should keep the pumper's assignment");
        Require(world.FetchBlock(sign) == "Your workers have signed out until morning.", "nobody should run for prices after signing out");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Workers.All(w => w.Home), "signed-out workers should stay signed out after loading");
    }

    private static void CheckCrewWalksBackAtDawnPaidAndKeepsItsJobs(GameData data) {
        var crew = StartDayCrew(data);
        var (world, tree, chest, _, forge, cutter, pumper, hauler) = crew;
        var sign = SignOutCrew(crew);
        RunUntil(world, data.DaySeconds - 1);
        chest.Inventory.InputFilter.Clear();
        SafeTick(world, 1 + data.TradeSeconds / 4);
        Require(cutter.Commute > 0, "at dawn a worker should fade in on the walk");
        Require(!cutter.Home, "at dawn a worker should no longer be home");
        Require(cutter.Pos.DistanceTo(sign.Pos + Vector2.One) < World.TripDistance,
            "at dawn a worker should walk back up the valley from nearby");
        SafeTick(world, 25);
        Require(world.Workers.All(w => !w.Home && w.WageDebt == 0),
            "at dawn paid workers should all be back and owed nothing");
        Require(cutter.Pos.DistanceTo(sign.Pos) > 5, "at dawn the cutter should walk back to work");
        Require(pumper.Pos.DistanceTo(sign.Pos) > 5, "at dawn the pumper should walk back to work");
        Require(cutter.Assignment == tree.Id, "a night away should keep the cutter's assignment");
        Require(pumper.Assignment == forge.Id, "a night away should keep the pumper's assignment");
        Require(hauler.Assignment == forge.Id, "a night away should keep the hauler's assignment");
    }
}
