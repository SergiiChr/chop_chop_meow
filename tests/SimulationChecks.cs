using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChopChopMeow;
using Godot;
using static Checks;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Deterministic checks for the playable simulation, independent of the rendered scene.</summary>
public static class SimulationChecks {
    public static void Run(GameData data) {
        SteelPipeChecks.Run(data);
        SteelPipeModelChecks.Run();
        WaterPumpModelChecks.Run();
        PlumbingChecks.Run(data);
        Test(WaterPumpChecks.Run, data);
        Test(BuildingPlumbingChecks.Run, data);
        Test(InventoryLayoutChecks.Run, data);
        Test(HeldStackChecks.Run, data);
        Test(HotbarStackChecks.Run, data);
        Test(ChopChopMeow.Tests.WaterChecks.Run, data);
        Test(ChopChopMeow.Tests.BarrelChecks.Run, data);
        Test(LakeChecks.Run, data);
        Test(EconomyChecks.Run, data);
        Test(AnimationChecks.Run, data);
        Test(PropMotionChecks.Run);
        Test(HumanModelChecks.Run);
        Test(NatureModelChecks.Run);
        Test(PileModelChecks.Run, data);
        Test(PileModelChecks.Goods, data);
        Test(PileModelChecks.Heaps, data);
        Test(MineRockChecks.Run);
        Test(WorkerStateChecks.Run, data);
        Test(WorkerJobChecks.Run, data);
        StorageChecks.Run(data);
        HaulingChecks.Run(data);
        GatheringChecks.Run(data);
        CraftingChecks.Run(data);
        PackingChecks.Run(data);
        LunchChecks.Run(data);
        Test(CheckOutputFilterBlocksAutomatedWithdrawal, data);
        Test(CheckFilterNamesEachEntryOnceInTheOrderNamed);
        Test(CheckPlayerMovesItemsWithinFifteenMetres, data);
        Test(CheckDismantlingABuildingReturnsItsCostAndContents, data);
        Test(CheckBuildingsKnockIntoPlaceAndClatterDown, data);
        Test(CheckBuyingJinglesCoinsAndHiringSigns, data);
        Test(CheckStomachGrowlsAsFoodRunsLow, data);
        Test(CheckDismantlingWhatDoesNotFitLeavesItInABox, data);
        Test(CheckItemsRouteToAcceptingStorageBeforeNearerGeneralChest, data);
        Test(CheckWorkerDetoursAroundLake, data);
        Test(CheckCampfireWorkerFetchesFuelFromAllowedStorage, data);
        Test(CheckCartDeliversAfterFullTrip, data);
        Test(CheckLooseGoodsNeverSellOrSendACart, data);
        Test(CheckSaleLowersDemandAndNewsTravels, data);
        HiringChecks.Run(data);
        Test(CheckUnpaidWorkerStrikes, data);
        Test(CheckBoughtToolsArriveOneByOneAndSellBackInCrate, data);
        Test(RecoveryChecks.Run, data);
        Test(CheckRecoveryBoxesStayPrivate, data);
        Test(CheckDeathLeavesRecoveryBoxAndRespawnsAndSavesKeepState, data);
        Test(CheckBuildingPlacementTakesANearbyClearTileAndSpendsItsCost, data);
        Test(CheckCliffsHaveDepositsAndLooseCoal, data);
        Test(CheckResourcesScatterAcrossTheOpenValley, data);
        Test(CheckThingsOfAKindStayListedAsTheyComeAndGo, data);
        Test(CheckSunAndMoonFollowTheirArcs, data);
        WolfChecks.Run(data);
        WolfGaitChecks.Run();
        WolfBodiesChecks.Run(data);
        SwingsChecks.Run(data);
        WolfSnapChecks.Run(data);
        WolfFollowChecks.Run(data);
        WolfPatrolsChecks.Run(data);
        Test(CheckWageSlotTakesOnlyCoinsAndPaysWagesFirst, data);
        Test(CheckBankKeepsCoinsPastAThousandAcrossASave, data);
        Test(CheckSprintBurnsFood, data);
        CabinChecks.Run(data);
        GravelPathChecks.Run(data);
        Test(CheckPressingTheOppositeWayBrakesBeforeRunningBack, data);
        DoorTugChecks.Run(data);
        DoorCheckChecks.Run(data);
        DoorCheckViewChecks.Run(data);
        CatgirlDanceChecks.Run();
        Test(CheckOnlyCoinsUseTheLargerStack, data);
        Test(CheckSaveFromAnotherVersionIsRefused, data);
        SaveChecks.Run(data);
        Test(CheckCrewKeepsForgeHot, data);
        Test(CheckCrewSignsOutAtDuskKeepingLoadsAndJobs, data);
        Test(CheckWorkersWalkOffDownTheValleyAtWalkingPace, data);
        Test(CheckCrewWalksBackAtDawnPaidAndKeepsItsJobs, data);
        Test(CheckFogHidesUnseenGroundAndRemembersExploredGround, data);
        Test(CheckCratesShowOneSlotPerItemTheyCanHold, data);
        Test(CheckPlayerMovesItemsInAndOutOfACarriedCrate, data);
        Test(CheckSettlementRemembersWhatItHasHad, data);
        Test(CheckVillageSellsSeedsAndFertilizerByTheCrate, data);
        Test(CheckWorkerTakesAxeFromStationAndPutsItBackForAJobWithoutTools, data);
        Test(CheckFullStationLeavesWorkerHoldingTheWrongToolUntilThereIsRoom, data);
        Test(CheckWorkerPutsToolBackGoingHomeAndLeavesItWhenDismissed, data);
        Test(CheckPilesSnapTogetherHoldOneResourceAndMakeCompost, data);
        Test(CheckEmptiedPileKeepsItsFilterWhichChangesOnlyWhileEmpty, data);
        Test(CheckOnlyAnEmptyPileWithNoFilterTurnsIntoCompost, data);
        Test(CheckWorkerWithTwoJobsTakesTurns, data);
        Test(CheckStockerFillsToolRack, data);
        Test(CheckTenderFeedsCompostWithoutFood, data);
        Test(CheckRunnerCarriesDeliveriesIntoStorage, data);
        Test(CheckWorkerLeavesToolTheFilterHolds, data);
        Test(FarmChecks.Run, data);
        Test(CheckFoodsFeedAndWaterByTheirDataWithoutHealing, data);
        Test(CheckMarketTakesCratesAndDonations, data);
        TradeChecks.Run(data);
        GoalChecks.Run(data);
        Test(CheckTorchLightsAndBurnsOut, data);
        Test(CheckALitTorchMovedOutOfTheBackpackGoesOut, data);
        Test(CheckEachToolTakesItsOwnSlot, data);
        Test(MineChecks.Run, data);
        Test(ProductionChainChecks.Run, data);
        ProgressionChecks.Run(data);
        Test(MultiplayerChecks.Run, data);
        Test(AllocationChecks.Run, data);
        GD.Print("SELF-TEST PASS");
    }

    /// <summary>A worker with jobs at two places takes turns at them, and taking one job away keeps the other.</summary>
    private static void CheckWorkerWithTwoJobsTakesTurns(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        chest.Inventory.Add("wood", 20);
        var bush = world.Add(ThingKind.Berries, new Vector2(24, 20), 30);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 24));
        var both = AddWorker(world, new Vector2(22, 22));
        world.SetDuty(both, bush, JobKind.Gather, true);
        world.SetDuty(both, fire, JobKind.Fuel, true);
        Require(both.Jobs.Count == 2 && both.Assignment == bush.Id, "a worker should keep jobs at two places and start at the first");
        SafeTick(world, 60);
        Require(bush.Remaining < 30 && fire.Burn + fire.Inventory["wood"] * world.Data.CampfireSeconds > 0,
                "a worker with jobs at two places should do both: picking berries, and feeding the fire once it runs low");
        world.SetDuty(both, fire, JobKind.Fuel, false);
        Require(both.Jobs.Count == 1 && World.HasDuty(both, bush.Id, JobKind.Gather), "taking one job away should keep the other");
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

    /// <summary>A tender feeds compost from storage, skipping food when told to, and carries its fertilizer back.</summary>
    private static void CheckTenderFeedsCompostWithoutFood(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        var compost = world.Add(ThingKind.Pile, new Vector2(16, 24));
        Require(world.SetCompost(compost, true), "an empty pile should turn into compost");
        world.SetSkipFood(compost, true);
        compost.Inventory.Add("fertilizer", 2);
        chest.Inventory.Add("leaves", 8); chest.Inventory.Add("berries", 6);
        var tender = AddWorker(world, new Vector2(17, 22));
        world.SetDuty(tender, compost, JobKind.Tend, true);
        SafeTick(world, 40);
        Require(chest.Inventory["fertilizer"] == 2, "a tender should carry fertilizer to storage");
        Require(compost.Inventory["leaves"] > 0, "a tender should bring leaves to the compost");
        Require(chest.Inventory["berries"] == 6, "a tender should leave food in storage when food deliveries are skipped");
        Require(compost.Inventory["berries"] == 0, "a tender should not bring food to the compost when food deliveries are skipped");
    }

    /// <summary>A trading post's runner carries what the cart delivered, other than coins, to storage.</summary>
    private static void CheckRunnerCarriesDeliveriesIntoStorage(GameData data) {
        var world = EmptyWorld(data);
        var store = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(26, 28)));
        var sign = world.Add(ThingKind.Signpost, new Vector2(24, 26));
        sign.Output!.Add("tin_bar", 3); sign.Output.Add(Inventory.Coins, 20);
        var runner = AddWorker(world, new Vector2(24, 25));
        world.SetDuty(runner, sign, JobKind.Deliveries, true);
        SafeTick(world, 20);
        Require(store.Inventory["tin_bar"] == 3, "a runner should carry deliveries into storage");
        Require(sign.Output["tin_bar"] == 0, "a runner should leave no delivery in the post's output");
        Require(sign.Output[Inventory.Coins] == 20, "a runner should leave the coins in the post's output");
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

    private static void CheckDismantlingABuildingReturnsItsCostAndContents(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        Give(world, "wood", 8); Give(world, "stone", 30);
        var chest = world.Construction.Build(ThingKind.Chest, world.Player + new Vector2(2, 0))!;
        world.Transfer(world.Backpack, chest.Inventory, "stone", 30);
        Require(world.Backpack["wood"] == 0 && world.Backpack["stone"] == 0, "building the chest and filling it should empty the backpack");
        Require(world.Construction.Dismantle(chest), "the player should dismantle a chest in reach");
        Require(world.Find(chest.Id) == null, "a dismantled chest should be gone");
        Require(world.Backpack["wood"] == 8 && world.Backpack["stone"] == 30, "dismantling a chest should return its 8 wood and the 30 stone in it");
        Require(!world.Things.Any(t => t.Kind == ThingKind.Grave), "with room in the backpack nothing should go into a box");

        Give(world, "wood", 12); Give(world, "stone", 8);
        var cabin = world.Construction.Build(ThingKind.Hut, world.Player + new Vector2(-4, 0))!;
        StepInside(world, cabin);
        Require(!world.Construction.Dismantle(cabin) && world.Find(cabin.Id) != null, "a cabin someone shelters in should not come down");
    }

    private static void CheckBuildingsKnockIntoPlaceAndClatterDown(GameData data) {
        var world = EmptyWorld(data);
        var heard = Listen(world);
        Give(world, data.Buildings[ThingKind.Chest].Cost);
        var chest = BuildAt(world, ThingKind.Chest, new Vector2(22, 20));
        Require(heard.SequenceEqual(new[] { "place_building" }), "a building should knock into place as it goes up");
        Give(world, data.Buildings[ThingKind.Campfire].Cost);
        heard.Clear(); BuildAt(world, ThingKind.Campfire, new Vector2(17, 23));
        Require(heard.SequenceEqual(new[] { "place_building", "start_fire" }), "a campfire should knock into place and catch fire");
        world.Player = new Vector2(20, 20);
        heard.Clear(); world.Construction.Dismantle(chest);
        Require(heard.SequenceEqual(new[] { "take_down_building" }), "a building taken down should clatter apart");
    }

    private static void CheckBuyingJinglesCoinsAndHiringSigns(GameData data) {
        var world = EmptyWorld(data);
        var heard = Listen(world);
        var sign = world.Add(ThingKind.Signpost, new Vector2(20, 16));
        world.Player = sign.Pos;
        SetCoins(world, 100);
        heard.Clear(); world.Buy(sign, "water", 1);
        Require(heard.SequenceEqual(new[] { "buy_coins" }), "a buy order should jingle the coins paid");
        heard.Clear(); world.Hire(sign, data.HireOffers[0]);
        Require(heard.SequenceEqual(new[] { "hire_sign" }), "hiring a worker should sign them on");
    }

    private static void CheckStomachGrowlsAsFoodRunsLow(GameData data) {
        var world = EmptyWorld(data);
        var heard = Listen(world);
        // Seconds that burn a hundredth of a full stomach.
        float hundredth = 1 / data.HungerPerSecond;
        SetNeeds(world, hunger: 21.5f); world.Tick(hundredth);
        Require(heard.Count == 0, "a stomach with over a fifth of its food left should stay quiet");
        world.Tick(hundredth);
        Require(heard.SequenceEqual(new[] { "stomach_growl" }), "the stomach should growl as food falls under a fifth");
        world.Tick(hundredth);
        Require(heard.Count == 1, "the stomach should stay quiet until another twentieth of its food is gone");
        SetNeeds(world, hunger: 15.5f); world.Tick(hundredth);
        Require(heard.Count == 2, "the stomach should growl again as food falls under 15%");
    }

    private static void CheckDismantlingWhatDoesNotFitLeavesItInABox(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        Give(world, "planks", 10);
        var bench = world.Construction.Build(ThingKind.Bench, world.Player + new Vector2(2, 0))!;
        Give(world, "stone", 24 * 64);
        Require(world.Backpack.Room("planks") == 0, "the fixture should fill the backpack");
        Require(world.Construction.Dismantle(bench), "the player should dismantle a bench in reach");
        var box = world.Things.Single(t => t.Kind == ThingKind.Grave);
        Require(box.Inventory["planks"] == 10 && box.Pos == bench.Pos, "the planks that do not fit should wait in a recovery box where the bench stood");
    }

    private static void CheckPlayerMovesItemsWithinFifteenMetres(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + new Vector2(14, 0)));
        var furnace = world.Add(ThingKind.Furnace, world.Player + new Vector2(0, 14));
        var far = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + new Vector2(-17, 0)));
        Give(world, "wood", 6); Give(world, "iron_ore", 4);
        Require(world.Transfer(world.Backpack, chest.Inventory, "wood", 2) == 2, "a chest 14 m away should take items from the backpack");
        Require(world.LoadInput(furnace, "iron_ore", 4) == 4, "a station 14 m away should take its inputs from the backpack");
        Require(world.Transfer(world.Backpack, far.Inventory, "wood", 2) == 0, "a chest 17 m away should be out of reach");
    }

    /// <summary>A filter keeps its entries in the order they were named, and naming one it has again leaves it where it is.</summary>
    private static void CheckFilterNamesEachEntryOnceInTheOrderNamed() {
        var chest = new Inventory();
        chest.Filter("stone", false, true); chest.Filter("wood", false, true); chest.Filter("stone", false, true);
        Require(chest.InputFilter.SequenceEqual(["stone", "wood"]), $"naming stone again should leave the filter as stone, wood, not {string.Join(", ", chest.InputFilter)}");
        chest.Filter("stone", false, false);
        Require(chest.InputFilter.SequenceEqual(["wood"]) && !chest.Accepts("stone"), "taking stone off once should leave no stone in the filter");
    }

    private static void CheckOutputFilterBlocksAutomatedWithdrawal(GameData data) {
        var source = new Inventory { Slots = 1 };
        var destination = new Inventory { Slots = 1 };
        source.Add("iron_bar", 4);
        source.Filter("wood", true, true);
        destination.Filter("wood", false, true);
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated withdrawal must respect the source output filter");
        Require(source["iron_bar"] == 4 && destination["iron_bar"] == 0,
                "blocked automated withdrawal must leave both inventories unchanged");

        source.OutputFilter.Clear();
        source.Filter("iron_bar", true, true);
        destination.InputFilter.Clear();
        destination.Filter("wood", false, true);
        Require(source.TransferTo(destination, "iron_bar", 3, true) == 0,
                "automated delivery must respect the destination input filter");
        destination.Filter("iron_bar", false, true);
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
        var nearestGeneral = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(11, 10)));
        var explicitWood = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(17, 10)));
        explicitWood.Inventory.Filter("wood", false, true);
        Require(world.DepositFor("wood", new Vector2(10, 10))?.Id == explicitWood.Id,
                "explicitly accepting storage should outrank a nearer general chest");
        Require(world.DepositFor("stone", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "an unrestricted chest should accept items not claimed by a filtered chest");

        nearestGeneral.Inventory.Add("wood", 4);
        nearestGeneral.Inventory.Filter("stone", true, true);
        Require(world.SourceFor("wood", new Vector2(10, 10)) == null,
                "workers must not withdraw items forbidden by the source output filter");
        nearestGeneral.Inventory.Filter("wood", true, true);
        Require(world.SourceFor("wood", new Vector2(10, 10))?.Id == nearestGeneral.Id,
                "workers should use a chest once its output filter allows withdrawal");
    }

    private static void CheckWorkerDetoursAroundLake(GameData data) {
        var world = EmptyWorld(data);
        static Vector2 Bank(Vector2 direction, float past) => Terrain.Lake + direction * (Terrain.Shore(MathF.Atan2(direction.Y, direction.X)) + past);
        var tree = world.Add(ThingKind.Tree, Bank(Terrain.Seaward, 2), 12);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, Bank(-Terrain.Seaward, 1.5f)));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        bool stayedOutOfLake = true;
        for (int i = 0; i < 700 && chest.Inventory["wood"] == 0; i++) {
            SafeTick(world, Step);
            stayedOutOfLake &= !Terrain.InLake(worker.Pos);
        }

        Require(stayedOutOfLake,
                "worker pathing should route around the lake without entering its blocked radius");
        Require(chest.Inventory["wood"] >= worker.Bag.StackSize && worker.Bag["wood"] == 0,
                "worker assigned across the lake should eventually gather wood and deliver it to accepting storage");
    }

    private static void CheckCampfireWorkerFetchesFuelFromAllowedStorage(GameData data) {
        var world = EmptyWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(24, 20)));
        chest.Inventory.Add("wood", 6);
        chest.Inventory.Filter("stone", true, true);
        var worker = AddWorker(world, fire.Pos);
        world.Assign(worker, fire);

        SafeTick(world, 3);
        Require(chest.Inventory["wood"] == 6 && fire.Inventory["wood"] == 0,
                "campfire worker must respect the source chest output filter when fetching fuel");

        chest.Inventory.Filter("wood", true, true);
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
        Require(world.Coins == coinsBeforeBuy - 16, "buy order should reserve coins immediately");
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
        Require(world.Backpack["water"] == 0, "cart should not put the delivered bottle in the backpack");
    }

    /// <summary>A sale pays when the cart returns and lowers demand, and the price sheet only changes when someone brings news.</summary>
    private static void CheckLooseGoodsNeverSellOrSendACart(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        // The post refuses loose goods by hand, so they are put in directly.
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
        int paid = sign.Wages![Inventory.Coins] + sign.Output[Inventory.Coins];
        Require(world.Coins + paid < 20000, "buying tools and selling them straight back must lose coins");
        // 12 axes at 39 coins and the crate at 3.15, rounded down.
        Require(paid == 471, $"a sale should pay every unit of a crate at the pre-sale price: {paid} coins");
    }

    private static void CheckRecoveryBoxesStayPrivate(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(12, 10);
        Give(world, "wood", 5);
        var tree = world.Add(ThingKind.Tree, new Vector2(13, 10), 30);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 10)));
        var worker = AddWorker(world, new Vector2(14, 10));
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        world.Die();
        var box = world.Things.Last(t => t.Kind == ThingKind.Grave);
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

        SafeTick(world, data.RespawnSeconds);
        world.Player = box.Pos;
        foreach (var id in box.Inventory.Items.Keys.ToArray()) world.Transfer(box.Inventory, world.Backpack, id, 999);
        SafeTick(world, .1f);
        Require(world.Things.All(t => t.Id != box.Id), "an emptied recovery box should disappear");
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

    private static void CheckBuildingPlacementTakesANearbyClearTileAndSpendsItsCost(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 8);
        Require(world.Construction.Build(ThingKind.Chest, Terrain.Spawn) == null,
                "building placement must reject the player arrival tile");
        var chest = world.Construction.Build(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 0));
        Require(chest != null, "building placement should accept a nearby clear tile");
        Require(chest.Kind == ThingKind.Chest, "building placement should build the chosen kind");
        Require(world.Backpack["wood"] == 0, "building placement should spend its cost");
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

    private static void CheckFogHidesUnseenGroundAndRemembersExploredGround(GameData data) {
        var fresh = new World(data);
        var far = Cliff.Of(ThingKind.Coal).Center;
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
        var gone = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(11, 11)));
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
        foreach (var cliff in Cliff.All) {
            Require(!world.CanStand(cliff.Center), $"each {cliff.Ore} cliff should block movement");
            Require(Terrain.FromMountains(cliff.Center) < 0,
                $"each {cliff.Ore} cliff's middle should be off the mountain range");
            Require(Terrain.FromMountains(cliff.Center + cliff.Face * 2.6f) > 0,
                $"each {cliff.Ore} cliff should stand at the foot of a mountain range");
            Require(world.Things.Count(t => t.Kind == cliff.Ore && cliff.DistanceTo(t.Pos) < 3) == 2, $"each {cliff.Ore} cliff should have deposits at its foot");
        }
        foreach (var cliff in new[] { Cliff.Of(ThingKind.Iron), Cliff.Of(ThingKind.Coal) }) {
            Require(world.Things.Where(t => t.Kind == cliff.Ore).All(t => Cliff.All.Any(c => c.Ore == cliff.Ore && c.DistanceTo(t.Pos) < 3)),
                    $"{cliff.Ore} deposits should only spawn at the foot of their cliffs");
        }
        {
            var cliff = Cliff.Of(ThingKind.Coal);
            var loose = world.Things.Where(t => t.Kind == ThingKind.LooseCoal).ToList();
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

    /// <summary>Asking for one kind of thing gives every thing of that kind and no other, after placing, picking up, clearing and loading a save.</summary>
    private static void CheckThingsOfAKindStayListedAsTheyComeAndGo(GameData data) {
        var world = EmptyWorld(data);
        var picked = world.Add(ThingKind.LooseStone, new Vector2(20, 20), 1);
        var left = world.Add(ThingKind.LooseStone, new Vector2(24, 20), 1);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(30, 20)));
        PickUp(world, picked);
        RemoveThings(world, t => t == chest);
        var pile = world.Add(ThingKind.Pile, new Vector2(40, 20));
        Require(world.ThingsOf(ThingKind.LooseStone).Count == 1 && world.ThingsOf(ThingKind.LooseStone)[0] == left && world.ThingsOf(ThingKind.Chest).Count == 0,
            "a picked-up stone and a cleared chest should leave the lists of their kinds");
        Require(world.ThingsOf(ThingKind.Pile).Count == 1 && world.ThingsOf(ThingKind.Pile)[0] == pile, "a placed pile should be listed with its kind");
        var loaded = EmptyWorld(data);
        TakeEverything(loaded, loaded.Add(ThingKind.Chest, new Vector2(50, 20)));
        loaded.Deserialize(world.Serialize());
        Require(loaded.ThingsOf(ThingKind.LooseStone).Count == 1 && loaded.ThingsOf(ThingKind.LooseStone)[0].Id == left.Id && loaded.ThingsOf(ThingKind.Chest).Count == 0
            && loaded.ThingsOf(ThingKind.Pile).Count == 1 && loaded.Find(pile.Id)?.Kind == ThingKind.Pile,
            "a loaded save should list its own things by kind and none from before");
    }

    private static void CheckPlayerMovesItemsInAndOutOfACarriedCrate(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        Give(world, "crate", 1); Give(world, "berries", 12); Give(world, "wood", 2); Give(world, "crate", 1);
        var placed = world.PlaceCrate("crate", world.Player + new Vector2(0, 2))!;
        world.Transfer(world.Backpack, placed.Inventory, "berries", 12);
        Require(world.PickUpCrate(placed), "the filled crate should be picked up");
        string packed = world.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        int id = World.PackedId(packed);

        Require(world.MoveInCrate(id, "berries", 3, packing: false) == 3 && world.Backpack["berries"] == 3, "taking 3 berries out of a carried crate should put them in the backpack");
        Require(world.CarriedCrate(id) == World.CrateOf("berries") + $"_{id}" && world.Packed(world.CarriedCrate(id)!)!["berries"] == 9, "a crate of berries should stay one with 9 left");
        Require(world.MoveInCrate(id, "wood", 2, packing: true) == 2 && world.Backpack["wood"] == 0, "packing 2 wood into the carried crate should take them from the backpack");
        Require(world.CarriedCrate(id) == World.PackedPrefix + id, "a crate of berries and wood should be named as a mixed crate");
        Require(world.MoveInCrate(id, "crate", 1, packing: true) == 0 && world.Backpack["crate"] == 1, "a crate should never go into a crate");
        world.MoveInCrate(id, "berries", 9, packing: false); world.MoveInCrate(id, "wood", 2, packing: false);
        Require(world.CarriedCrate(id) == null && world.Backpack["crate"] == 2 && world.Packed(packed) == null, "an emptied crate should become a plain empty crate");
    }

    private static void CheckCratesShowOneSlotPerItemTheyCanHold(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(10, 10);
        var crate = TakeEverything(world, world.Add(ThingKind.Crate, new Vector2(11, 10))).Inventory;
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
        Require(world.Backpack["crate"] == 1, "an empty crate should be a plain crate");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Packed(meat)?["steel_axe"] == 2, "save/load should keep what a packed crate holds");
        loaded.Player = new Vector2(10, 10);
        var again = loaded.PlaceCrate(meat, new Vector2(12, 12));
        Require(again != null, "placing a packed crate should place a crate");
        Require(again.Inventory["raw_meat"] == 5, "placing a packed crate should put its contents back into the placed crate");
        Require(again.Inventory.Capacity == cap, "placing a packed crate should give it its full capacity");
        Require(loaded.Packed(meat) == null, "a loaded save should no longer hold the packed crate in the world");
        Require(loaded.Backpack[meat] == 0, "a loaded save should no longer hold the packed crate in the backpack");
        Require(world.DepositFor("wood", new Vector2(10, 10)) == null, "crates are manual storage that workers never fill");
    }

    private static void CheckALitTorchMovedOutOfTheBackpackGoesOut(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + Vector2.One));
        Give(world, "torch", 1); world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.TorchLit, "the torch should light");
        world.Transfer(world.Backpack, chest.Inventory, "torch", 1);
        SafeTick(world, .1f);
        Require(!world.TorchLit && world.Torch == 0 && chest.Inventory["torch"] == 1, "a lit torch moved into a chest should go out and lie there as a torch");
    }

    private static void CheckTorchLightsAndBurnsOut(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 3);
        world.UseTorch(Vector2.Zero);
        Require(!world.Craft("torch"), "a torch should need a chisel to make");
        Require(world.Torch == 0, "using no torch should not put one in hand");
        Require(world.TorchKindling == 0, "using no torch should not start kindling");
        Give(world, "stone_chisel", 1);
        Require(world.Craft("torch") && world.Backpack["wood"] == 0, "a torch should take 3 wood and nothing else");
        SafeTick(world, data.Recipes["torch"].Seconds + .25f);
        Give(world, "torch", 1);
        world.UseTorch(Vector2.Zero);
        Require(world.Backpack["torch"] == 2, "the first use should leave the torch it lights in the backpack");
        Require(world.Torch == data.CampfireSeconds, "the first use should start a full torch");
        Require(!world.TorchLit, "the first use should not light the torch before the flint strikes");
        SafeTick(world, World.KindleSeconds - .2f);
        Require(!world.TorchLit, "lighting should take its time");
        SafeTick(world, .3f);
        Require(world.TorchLit && world.Backpack["torch"] == 2, "the torch should catch after the lighting animation and stay in the backpack");

        // Steady swings keep it burning and flare the light; four in two seconds put it out.
        for (int i = 0; i < 4; i++) { world.UseTorch(Vector2.Right); Require(world.TorchFlare > 0, "a swing should flare the light"); SafeTick(world, .7f); }
        Require(world.TorchLit, "swinging at a steady pace should keep the torch burning");
        SafeTick(world, World.WildWindow);
        string said = "";
        world.Notify += message => said = message;
        var heard = Listen(world);
        for (int i = 0; i < World.WildSwings; i++) { world.UseTorch(Vector2.Right); SafeTick(world, .65f); }
        float left = world.Torch;
        Require(!world.TorchLit && said == "Torch went off due to wild swinging.", "wild swinging should put the torch out with a message");
        Require(heard.Count(name => name == "torch_out") == 1, "a torch going out should fizzle once");
        SafeTick(world, 5);
        Require(world.Torch == left, "an unlit torch should keep its burn time");
        world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.TorchLit, "relighting should light the torch");
        Require(world.Backpack["torch"] == 2, "relighting should not use a new torch");
        Require(world.Torch < left, "relighting should not reset the torch's time");
        Require(world.Torch > left - 1, "relighting should not add time to the torch");
        SafeTick(world, world.Torch + .25f);
        Require(world.Torch == 0, "a torch should burn out completely");
        Require(!world.TorchLit, "a burnt out torch should not stay lit");
        Require(heard.Count(name => name == "torch_out") == 2, "a torch burning out should fizzle");
        Require(world.Backpack["torch"] == 1, "a burnt out torch should be used up, leaving the spare torch in the backpack");
        world.UseTorch(Vector2.Zero); SafeTick(world, World.KindleSeconds + .1f);
        Require(world.TorchLit, "a fresh torch should light");
        world.Die();
        Require(heard.Count(name => name == "torch_out") == 3, "a lit torch should fizzle out when the player falls");
        SafeTick(world, data.RespawnSeconds + .1f);
        Give(world, "torch", 1); world.UseTorch(Vector2.Zero); world.Die();
        Require(world.Torch == 0, "a torch should burn out when the player falls");
        Require(!world.TorchLit, "a torch should not stay lit when the player falls");
        Require(world.TorchKindling == 0, "a torch should stop kindling when the player falls");
        Require(heard.Count(name => name == "torch_out") == 3, "a torch still being lit when the player falls should not fizzle");
    }

    private static void CheckEachToolTakesItsOwnSlot(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "water", 3); Give(world, "steel_axe", 2);
        Require(world.Backpack.UsedSlots == 5, "each refillable bottle and each tool should take its own slot");
        var crate = world.NewCrate();
        Require(crate.Add("steel_pick", data.CrateCapacity) == data.CrateCapacity && crate.UsedSlots == data.CrateCapacity, "a crate should hold a full crate of single tools");
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(10, 10)));
        Require(crate.TransferTo(chest.Inventory, "steel_pick", 8) == World.ChestStacks, "a chest should take one tool per slot");
        var hands = new Worker().Bag;
        Require(hands.Add("steel_axe", 20) == 12 && hands.Add("wood", 1) == 0, "a worker should carry twelve tools, one per slot");
        var mixed = new Worker().Bag;
        Require(mixed.Add("wood", 10) == 10, "a worker should carry ten wood");
        Require(mixed.Add("stone", 1) == 1, "a worker should carry a stone beside the wood");
        Require(mixed.Add("steel_pick", 1) == 1, "a worker should carry a tool beside the other things");
        Require(mixed.Add("coal", 1) == 0, "a worker should carry no more than twelve things in all");
    }

    private static void CheckSettlementRemembersWhatItHasHad(GameData data) {
        var fresh = new World(data);
        Require(fresh.Had("steel_axe") && fresh.Had("berries") && !fresh.Had("stone") && !fresh.Had("stone_chisel") && !fresh.Had("planks"), "a new settlement should know only what it starts with");
        Require(fresh.Had("steel_parts") && fresh.Had("iron_ore") && fresh.Had("coal") && fresh.Had("tin_bar") && fresh.Had("tin_ore"), "what the starting axe and bottle are made of should count as had along with them");
        var world = EmptyWorld(data);
        var tin = world.Add(ThingKind.Tin, Site(0, 0), 400);
        world.Player = tin.Pos;
        Give(world, "steel_pick", 1);
        GatherUntil(world, tin, "tin_ore", 4);
        Require(world.Had("tin_ore") && !world.Had("steel_pick"), "gathering should mark the ore as had, and a tool only handed over should not be");
        Give(world, "stone", 2);
        Require(world.Craft("stone_chisel"), "two stone should queue a stone chisel");
        SafeTick(world, data.Recipes["stone_chisel"].Seconds + 1);
        Require(world.Had("stone_chisel") && world.Had("stone"), "crafting should mark the product as had, and the stone it is made of with it");
        Require(!world.Had("iron_ore"), "ore nobody dug and nothing was made of should not count as had");
        Achieve(world, "iron_bar");
        Require(world.Had("iron_bar") && world.Had("iron_ore") && !world.Had("steel_bar"), "what the settlement's milestones say it made should count as had with its ingredients, and what is made of it should not");
        string save = world.Serialize();
        var loose = world.Add(ThingKind.LooseCoal, Site(2, 0), 1);
        world.Player = loose.Pos; world.Gather(loose, Step);
        var loaded = new World(data, false); loaded.Deserialize(save);
        Require(world.Had("coal") && loaded.Had("tin_ore") && !loaded.Had("coal"), "a save should keep what the settlement had when it was written");
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

    /// <summary>A pile's filter stays when the pile empties, and is set, taken off or cleared only while the pile is empty.</summary>
    private static void CheckEmptiedPileKeepsItsFilterWhichChangesOnlyWhileEmpty(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin;
        Give(world, "wood", 11);
        var pile = world.Add(ThingKind.Pile, origin);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, origin + new Vector2(0, 5)));
        var said = new List<string>();
        world.Notify += said.Add;
        world.Transfer(world.Backpack, pile.Inventory, "wood", 10); world.Tick(.1f);
        world.SetFilter(pile, "wood", false, false); world.ClearFilters(pile);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && said.Count(m => m == "Can't change filter on a non-empty pile.") == 2,
            "a pile holding something should refuse to have its filter taken off or cleared");
        pile.Inventory.Remove("wood", 10);
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && world.HoldBlock(pile, "stone") == "This is a wood pile." && world.DepositFor("wood", chest.Pos) == pile,
            "an emptied pile should keep its filter and still ask for its resource");
        world.SetFilter(pile, "stone", false, true);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }), "an empty pile should keep its resource until it is taken off");
        world.SetFilter(pile, "wood", false, false);
        Require(pile.Inventory.InputFilter.Count == 0 && world.Holds(pile, "stone"), "taking an empty pile's filter off should free it");
        world.SetFilter(pile, "stone", false, true);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "stone" }) && pile.Inventory.OutputFilter.SequenceEqual(new[] { "stone" }) && world.HoldBlock(pile, "wood") == "This is a stone pile.",
            "a filter set on an empty pile should make it a pile of that resource, in and out");
        Require(world.DepositFor("stone", chest.Pos) == pile, "workers should bring stone to an empty pile set to stone");
        Require(world.Transfer(world.Backpack, pile.Inventory, "wood", 1) == 0, "a pile set to stone should refuse wood put in by hand");
        world.ClearFilters(pile);
        Require(pile.Inventory.InputFilter.Count == 0, "clearing an empty pile's filter should free it");
        world.SetFilter(pile, World.CrateOf("raw_meat"), false, true); world.SetFilter(pile, World.CrateOf("raw_meat"), false, false);
        world.SetFilter(pile, World.CrateOf("raw_meat"), false, true); world.SetFilter(pile, "stone", false, false);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { World.CrateOf("raw_meat") }) && world.HoldBlock(pile, "stone") == "This is a crate of raw meat pile.",
            "a pile set to crates of a good should name that good, and keep it when another resource is taken off");
    }

    /// <summary>Only an empty pile with no filter turns into compost.</summary>
    private static void CheckOnlyAnEmptyPileWithNoFilterTurnsIntoCompost(GameData data) {
        var world = EmptyWorld(data);
        var pile = world.Add(ThingKind.Pile, new Vector2(20, 20));
        var said = new List<string>();
        world.Notify += said.Add;
        world.SetFilter(pile, "wood", false, true);
        Require(!world.SetCompost(pile, true) && said.Contains("Take the pile's filter off before composting."), "an empty pile with a filter should not turn into compost");
        world.ClearFilters(pile); pile.Inventory.Add("leaves", 2);
        Require(!world.SetCompost(pile, true) && said.Contains("Empty the pile before composting."), "a pile holding leaves should not turn into compost");
        pile.Inventory.Remove("leaves", 2);
        Require(world.SetCompost(pile, true), "an empty pile with no filter should turn into compost");
    }

    /// <summary>Piles snap into grids and hold one resource, the first put in, and compost turns leaves and food into fertilizer.</summary>
    private static void CheckPilesSnapTogetherHoldOneResourceAndMakeCompost(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin + new Vector2(0, 2);
        Give(world, "wood", 40);
        var pile = world.Construction.Build(ThingKind.Pile, origin);
        Require(pile != null, "a pile should be built");
        Require(world.Backpack["wood"] == 32, "a pile should cost 8 wood");
        Require(pile!.Inventory.Slots == World.PileStacks, "a pile should hold 14 stacks");
        Require(TakeEverything(world, world.Add(ThingKind.Chest, origin + new Vector2(0, 8))).Inventory.Slots == 5, "a chest should hold a third of a pile, 5 stacks");
        Require(world.Construction.Snap(ThingKind.Pile, origin + new Vector2(3.4f, .4f)) == origin + new Vector2(3, 0) && world.Construction.Snap(ThingKind.Pile, origin + new Vector2(9, 0)) == origin + new Vector2(9, 0),
                "a pile placed beside another should snap edge to edge, and one far away should not");
        var east = world.Construction.Build(ThingKind.Pile, origin + new Vector2(3.3f, 0));
        Require(east != null, "a pile beside another should build");
        Require(world.Construction.PlacementBlock(ThingKind.Pile, origin + new Vector2(2, 0)) != null && east.Pos == origin + new Vector2(3, 0), "piles must not overlap but may touch");
        Require(world.CanStand(origin), "a pile should be walkable");

        world.Player = origin;
        Require(world.Transfer(world.Backpack, pile!.Inventory, "wood", 10) == 10 && world.HoldBlock(pile, "stone") == "This is a wood pile.", "the first resource in should be the only one the pile takes");
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && pile.Inventory.OutputFilter.SequenceEqual(new[] { "wood" }), "the first resource in should set the pile's filters, in and out");
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, origin + new Vector2(0, 5)));
        Require(world.DepositFor("wood", chest.Pos) == pile && world.DepositFor("stone", origin) != pile, "workers should prefer the matching pile and keep other goods out of it");

        Require(world.SetCompost(east, true), "compost should be set on the pile");
        Require(east.Inventory.InputFilter.Contains("leaves"), "compost should take leaves");
        Require(east.Inventory.InputFilter.Contains("crushed_berries"), "compost should take crushed berries");
        Require(!east.Inventory.InputFilter.Contains("stone"), "compost should refuse stone");
        Require(east.Inventory.OutputFilter.SequenceEqual(new[] { "fertilizer" }), "compost should give only fertilizer");
        east.Inventory.Add("leaves", 6); east.Inventory.Add("burnt_steak", 2);
        Require(world.HoldBlock(east, "stone") != null && !world.SetCompost(east, false), "compost should refuse stone and stay compost while mixed");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 1 && east.Inventory.Count == 5, "4 inputs should rot into 1 fertilizer in an in-game hour");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 2 && east.Inventory.Items.Keys.All(k => k == "fertilizer"), "compost should keep going while it has 4 inputs");
        Require(world.SourceFor("fertilizer", origin) == east, "workers should be able to take the fertilizer out");

        var station = TakeEverything(world, world.Add(ThingKind.Toolstation, origin + new Vector2(-4, 4)));
        station.Inventory.Add("rake", 1);
        var leaves = world.Add(ThingKind.Leaves, origin + new Vector2(-4, -4), World.LeafPatch);
        var worker = AddWorker(world, station.Pos);
        world.Assign(worker, leaves);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 40);
        Require(east.Inventory["leaves"] + east.Inventory["fertilizer"] * 4 > 8, "raked leaves should go to the compost, which takes them explicitly");
    }

    /// <summary>Foods restore the hunger and thirst their data says and no health, and raw ones only say they need cooking.</summary>
    private static void CheckFoodsFeedAndWaterByTheirDataWithoutHealing(GameData data) {
        var world = EmptyWorld(data);
        SetNeeds(world, 20, 20, 50);
        Give(world, "meat_stew", 1); Give(world, "wheat", 1);
        world.Consume("meat_stew");
        var stew = data.Items["meat_stew"];
        Require(world.Hunger == 20 + stew.Hunger, "meat stew should feed by its data");
        Require(world.Thirst == 20 + stew.Thirst, "meat stew should water by its data");
        Require(world.Health == 50, "meat stew should not heal: only sleep does");
        Require(world.Backpack["meat_stew"] == 0, "meat stew should be used up");
        world.Consume("wheat");
        Require(world.Backpack["wheat"] == 1, "raw wheat should not be eaten as it is");
        Require(data.IsFood("wheat"), "raw wheat should count as food for compost and wolves");
        Require(data.IsConsumable("wheat"), "raw wheat should count as consumable");
        Require(!data.IsFood("water"), "water should not be food");
        Require(data.IsConsumable("water"), "water should be drinkable");
        Require(!data.IsFood("tin_can"), "cans should not be food");
    }

    private static void CheckMarketTakesCratesAndDonations(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        string chisel = PackCrate(world, ("steel_chisel", 1));
        string empty = PackCrate(world);
        Require(world.CanSell(chisel), "the village should take a crate of goods it does not buy as a donation");
        Require(!world.CanSell(empty), "the village should not take an empty crate");
        Require(!world.CanSell("crate"), "the village should not take a bare crate");
        Require(!world.CanSell("steel_axe"), "the village should not take a loose steel axe");
        string meat = PackCrate(world, ("raw_meat", 6), ("burnt_steak", 6));
        string tools = PackCrate(world, ("steel_hammer", 1), ("steel_pick", 1));
        world.Player = sign.Pos;
        world.Transfer(world.Backpack, sign.Inventory, meat, 1); world.Transfer(world.Backpack, sign.Inventory, tools, 1);
        Require(world.Sell(sign), "a cart should take every packed crate");
        Require(world.Orders.Single().Crates == 2, "a cart should count the crates it takes");
        Require(world.Packed(meat) == null, "a cart should take away the crate of meat it sells");
        Require(world.Orders.Single().Goods["raw_meat"] == 6 && world.Orders.Single().Goods["steel_pick"] == 1, "the sale should pay for what the crates hold");
        // Donated goods ride along for nothing: the crate alone is paid for.
        world.Transfer(world.Backpack, sign.Inventory, chisel, 1);
        Require(world.Sell(sign), "a cart should take a crate of goods the village does not buy");
        Require(world.Orders.Last().Value == 3, "donated goods should pay nothing beyond the crate");
        var said = new List<string>(); world.Notify = said.Add;
        SafeTick(world, data.TradeSeconds + .1f);
        Require(said.Any(m => m.EndsWith("Donated 1 Steel chisel.")), "the returning cart should say what was donated");
        SetCoins(world, 1000);
        Require(world.Buy(sign, "canned_meat_stew", 2), "boxed goods should be bought from the post");
        Require(world.Orders.Last().Crates == 2, "boxed goods should be bought by the crate");
        // A crate of 12 at 12.15 each plus its crate at 3.15 is 148.95; a quarter more, rounded up, is 187.
        Require(world.Coins == 1000 - 2 * 187, "boxed goods should cost the crate price");
        Require(world.LotPrice("canned_meat_stew") > data.Market.Buys["canned_meat_stew"] * world.UnitPrice("canned_meat_stew", false), "a bought crate costs more than its contents would sell for");
        SafeTick(world, data.TradeSeconds + .1f);
        var bought = sign.Output!.Items.Keys.Where(World.IsCrate).ToList();
        Require(bought.Count == 2 && bought.All(id => world.Packed(id)?["canned_meat_stew"] == 12), "each bought crate should arrive packed with one purchase");
        SetNeeds(world, hunger: 30); Give(world, "canned_meat_stew", 1); world.Consume("canned_meat_stew");
        // A can of meat stew feeds 60.
        Require(world.Hunger == 90, "canned meat stew should be eaten from the can");
        Require(world.Had("canned_meat_stew"), "what the cart brings back should count as had");
        Require(world.KnownPrice("raw_meat") > 0 && !world.Priced("wheat_seeds"), "the price sheet only tracks goods the village buys");
        Require(!data.Items.ContainsKey("canned_food") && data.Recipes.Values.All(r => !r.Inputs.ContainsKey("raw_meat") || r.Station != ThingKind.Cannery), "raw meat should sell in crates and never go into a can");
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

    private static void CheckPressingTheOppositeWayBrakesBeforeRunningBack(GameData data) {
        var momentum = new Momentum();
        Require(momentum.Step(Vector2.Right, 1 / 60f) == Vector2.Right, "a player setting off from a stand should run at full pace at once");
        Require(momentum.Step(Vector2.Down, 1 / 60f) == Vector2.Down, "a key at right angles should turn the run at once");
        var step = Vector2.Zero;
        for (int i = 0; i < 6; i++) step = momentum.Step(Vector2.Up, 1 / 60f);
        Require(momentum.Braking && step.Dot(Vector2.Down) is > 0 and < 1, $"0.1 s after pressing the opposite way the player should still slide on, slower, not {step}");
        for (int i = 0; i < 21; i++) step = momentum.Step(Vector2.Up, 1 / 60f);
        Require(!momentum.Braking && step.Dot(Vector2.Up) > 0, $"0.45 s after pressing the opposite way the player should be running back, not {step}");
        for (int i = 0; i < 12; i++) step = momentum.Step(Vector2.Up, 1 / 60f);
        Require(step == Vector2.Up, "the player should be back to full pace soon after turning round");
        Require(momentum.Step(Vector2.Zero, 1 / 60f) == Vector2.Zero && momentum.Step(Vector2.Down, 1 / 60f) == Vector2.Down, "letting go should stop the player at once, and the next key start a fresh run");
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
        Require(world.Coins == 5 && sign.Wages![Inventory.Coins] == 42, "sale proceeds for the goods and their crate should arrive as whole coins in the wage slot");
        sign.Inventory.Add(Inventory.Coins, 1);
        Require(!world.Sell(sign) && sign.Inventory[Inventory.Coins] == 1, "coins themselves cannot be sold");
        Require(!world.Buy(sign, Inventory.Coins, 1), "coins themselves cannot be bought");
    }

    /// <summary>A bank holding more than a stack of coins keeps them in its one cell through a save and still takes more.</summary>
    private static void CheckBankKeepsCoinsPastAThousandAcrossASave(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        sign.Wages!.Add(Inventory.Coins, 5000);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var bank = loaded.Things.Single(t => t.Kind == ThingKind.Signpost).Wages!;
        Require(bank[Inventory.Coins] == 5000 && bank.UsedSlots == 1, $"a loaded bank should keep its 5000 coins in one cell: {bank.UsedSlots} cells");
        Require(bank.Add(Inventory.Coins, 12) == 12, "a loaded bank should still take sale coins");
    }

    private static void CheckWageSlotTakesOnlyCoinsAndPaysWagesFirst(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        sign.Wages!.Add(Inventory.Coins, 4995);
        PlaceOrder(world, new TradeOrder { Goods = new() { ["wood"] = 1 }, Buying = false, Value = 12, Signpost = sign.Id });
        SafeTick(world, data.TradeSeconds + .1f);
        Require(sign.Wages[Inventory.Coins] == 5007 && sign.Output![Inventory.Coins] == 0, "the wage slot should have no limit: every sale coin lands there, past a thousand too");
        sign.Output.Add(Inventory.Coins, 7);
        Give(world, "wood", 1);
        Require(world.Transfer(world.Backpack, sign.Wages, "wood", 1) == 0 && world.Backpack["wood"] == 1, "the wage slot should refuse anything but coins, even by hand");

        var worker = AddWorker(world, sign.Pos);
        sign.Wages.Remove(Inventory.Coins, 5004);
        SetCoins(world, 10);
        world.PayWages();
        Require(worker.WageDebt == 0, "a worker's wage should be paid in full");
        Require(sign.Wages[Inventory.Coins] == 0, "wages should come out of the wage slot first");
        Require(world.Coins == 10 - (worker.Wage - 3), "wages should come out of the backpack after");
        SetCoins(world, 0);
        world.PayWages();
        Require(worker.WageDebt == worker.Wage && sign.Output![Inventory.Coins] == 7, "wages should not be taken from the market output");
    }

    /// <summary>A save from another version of the game is refused with a clear reason, never converted.</summary>
    private static void CheckSaveFromAnotherVersionIsRefused(GameData data) {
        var old = EmptyWorld(data);
        var older = JsonNode.Parse(old.Serialize())!;
        older["Version"] = 5;
        bool refused = false;
        try { EmptyWorld(data).Deserialize(older.ToJsonString()); } catch (InvalidOperationException error) { refused = error.Message.Contains("another version"); }
        Require(refused, "a save from another version should be refused with a clear reason");
    }

    private static void CheckCrewKeepsForgeHot(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        furnace.Heat = World.SteelHeat + 5;
        // Bellows are pumped only while there is something to smelt.
        furnace.Inventory.Add("iron_bar", 20); furnace.Inventory.Add("coal", 20); furnace.Burner!.Add("coal", 10);
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

    private static void CheckCrewSignsOutAtDuskKeepingLoadsAndJobs(GameData data) {
        var crew = StartDayCrew(data);
        var (world, tree, _, _, forge, cutter, pumper, _) = crew;
        var sign = SignOutCrew(crew);
        Require(world.Workers.All(w => w.Home), "at dusk every worker should sign out");
        Require(world.Workers.All(w => !world.Sees(w)), "a signed-out worker should be out of sight");
        Require(world.Workers.All(w => w.Commute == data.TradeSeconds / 2), "a signed-out worker should walk off for as long as a cart's trip");
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

    /// <summary>A worker signing out at dusk and a runner off for prices walk down the valley as fast as a worker walks, not stretched over the whole trip.</summary>
    private static void CheckWorkersWalkOffDownTheValleyAtWalkingPace(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(30, 20));
        var walker = AddWorker(world, sign.Pos + Vector2.One);
        RunUntil(world, data.DuskStarts * data.DaySeconds + .2f);
        var from = walker.Pos;
        SafeTick(world, 1);
        Require(Math.Abs(walker.Pos.DistanceTo(from) - World.WorkerSpeed) < .3f, $"a worker heading home should walk at walking pace, not {walker.Pos.DistanceTo(from):0.00} tiles a second");
        world = EmptyWorld(data);
        sign = world.Add(ThingKind.Signpost, new Vector2(30, 20));
        var runner = AddWorker(world, sign.Pos + Vector2.One);
        world.Assign(runner, sign);
        Require(world.FetchPrices(sign), "a morning runner should set off for prices");
        from = runner.Pos;
        SafeTick(world, 1);
        Require(Math.Abs(runner.Pos.DistanceTo(from) - World.WorkerSpeed) < .3f, $"a runner off for prices should walk at walking pace, not {runner.Pos.DistanceTo(from):0.00} tiles a second");
    }

    private static void CheckCrewWalksBackAtDawnPaidAndKeepsItsJobs(GameData data) {
        var crew = StartDayCrew(data);
        var (world, tree, chest, _, forge, cutter, pumper, hauler) = crew;
        var sign = SignOutCrew(crew);
        RunUntil(world, data.DaySeconds - 1);
        world.SetFilter(chest, Inventory.Everything, false, true);
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
