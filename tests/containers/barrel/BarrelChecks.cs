using ChopChopMeow;
using Godot;
using static Checks;
using static CabinSteps;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow.Tests;

internal static class BarrelChecks {
    public static void Run(GameData data) {
        Test(CheckBenchStacksBarrelsMadeWithSteelHammer, data);
        Test(CheckPileStacksEmptyBarrelsAndKeepsFullBarrelsSeparate, data);
        Test(CheckBackpackCarriesEmptyBarrelsAlongsideOneFilledBarrel, data);
        Test(CheckFillingOneBarrelKeepsItsCell, data);
        Test(CheckFillingABarrelNeedsRoomToSplitTheStack, data);
        Test(CheckBarrelKeepsWaterWhenPlacedAndLifted, data);
        Test(CheckWorkerHandsOnlyFitOneBarrel, data);
        Test(CheckSaveKeepsVesselFillLevels, data);
        Test(CheckRecoveryKeepsWorkerBarrelAndWaterVessel, data);
        Test(CheckFullBucketsDoNotPreventRefillingABottle, data);
        Test(CheckAFullBucketDoesNotPreventDrinkingAtTheLake, data);
        Test(CheckHotbarBoundBarrelAcceptsWaterFromSpareBottles, data);
        Test(CheckBucketsRefillAndPourWithoutLosingTheirStack, data);
        Test(CheckFillingABucketInAFullCrateKeepsItsItemCount, data);
        Test(CheckWorkerFetchesWaterFromClosestFullBarrelAndClimbsToCabin, data);
        Test(CheckWorkerFindsShoreAccessAroundACabin, data);
        Test(CheckBlockedToolSwapLetsTheWorkerDoTheirNextJob, data);
        Test(CheckWorkerSwapsABottleForAnAxe, data);
        Test(CheckRackTakesJamButDoesNotOfferItForWatering, data);
        Test(CheckWorkerFetchesABottleFromTheRack, data);
        Test(CheckBlockedBarrelLetsTheWorkerDoTheirNextJob, data);
        Test(CheckWorkerReachesBarrelAroundAChest, data);
        Test(CheckWorkerKeepsTheirWaterSourceWhileWalking, data);
        Test(CheckCabinServesThirstBeforeBottlesInEntryOrder, data);
        Test(CheckCabinBarrelReplacesThirstLostWhileSleeping, data);
        Test(CheckSleepingPreviewUsesRemainingWaterOnWaking, data);
        Test(CheckWakingEarlyLeavesAnEarlierCabinMateTheirWater, data);
        Test(CheckCabinFillsBottlesAtBedtime, data);
        Test(CheckCabinServesScarceWakeUpWaterInEntryOrder, data);
        Test(CheckAWholeLiterAtDawnServesEachOccupant, data);
        Test(CheckPlayerOutsideCannotDrinkFromTheCabin, data);
        Test(CheckRemovingCabinReturnsItsBarrelWithWater, data);
    }

    private static void CheckBenchStacksBarrelsMadeWithSteelHammer(GameData data) {
        var world = new World(data, false);
        var bench = world.Add(ThingKind.Bench, world.Player + Vector2.Right);
        Require(Give(world, "wood_hammer") == 1 && world.SlotTool(bench, "wood_hammer"), "bench should accept a wooden mallet");
        Give(world, "steel_sheet", 13); Give(world, "steel_parts", 5); Give(world, "wood_parts", 1);
        world.QueueBatch(bench, Barrel.Item, 2);
        Work(world, 12);
        Require(bench.Inventory[Barrel.Item] == 0 && !bench.Processing, "the barrel recipe should wait for the steel hammer");
        world.UnslotTool(bench, "wood_hammer");
        Require(Give(world, "steel_hammer") == 1 && world.SlotTool(bench, "steel_hammer"), "bench should accept a steel hammer");
        Require(data.Recipes[Barrel.Item].Seconds == 30, "one barrel should require thirty seconds of base crafting work");
        Work(world, 5);
        Require(bench.Inventory[Barrel.Item] == 0 && bench.Processing, "a steel hammer should still be working on the first barrel after five seconds");
        CompleteBatches(world, bench, Barrel.Item, 2);
        Require(bench.Inventory.UsedSlots == 1 && world.Backpack["steel_sheet"] == 3 && world.Backpack["steel_parts"] == 1, "two barrels stack and cost ten steel sheets and four steel parts");
        Require(world.Transfer(bench.Inventory, world.Backpack, Barrel.Item, 2) == 2, "both empty barrels go into the backpack");
        var pile = world.Add(ThingKind.Pile, world.Player + Vector2.Down * 3);
        Require(world.Transfer(world.Backpack, pile.Inventory, Barrel.Item, 2) == 2, "the backpack can move the stack into a pile");
        world.QueueBatch(bench, Bucket.Item);
        CompleteBatches(world, bench, Bucket.Item, 1);
        Require(world.Backpack["steel_sheet"] == 0 && world.Backpack["steel_parts"] == 0 && world.Backpack["wood_parts"] == 0, "one bucket costs three steel sheets, a steel part and a wood part");
    }

    private static void CheckPileStacksEmptyBarrelsAndKeepsFullBarrelsSeparate(GameData data) {
        var world = new World(data, false);
        var pile = world.Add(ThingKind.Pile, world.Player + Vector2.Down * 3);
        var full = world.Add(ThingKind.Pile, world.Player + Vector2.Up * 3);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, world.Player + Vector2.Right));
        Give(world, Barrel.Item);
        var said = new System.Collections.Generic.List<string>(); world.Notify += said.Add;
        Require(world.Transfer(world.Backpack, chest.Inventory, Barrel.Item, 1) == 0 && said.Contains("Barrel does not fit in storage chest."),
            "a chest must say why it refuses a barrel");
        world.Transfer(world.Backpack, pile.Inventory, Barrel.Item, 1);
        Give(world, Barrel.Item);
        world.Transfer(world.Backpack, pile.Inventory, Barrel.Item, 1);
        world.Tick(.1f);
        string first = GiveVessel(world, Barrel.Item, Water.Liquid, 50);
        Require(world.Transfer(world.Backpack, pile.Inventory, first, 1) == 0, "a pile of empty barrels takes no filled barrel");
        for (int i = 0; i < 2; i++) {
            string barrel = i == 0 ? first : GiveVessel(world, Barrel.Item, Water.Liquid, 50);
            Require(world.Transfer(world.Backpack, full.Inventory, barrel, 1) == 1, "a pile takes barrels of water");
            world.Tick(.1f);
        }
        Require(world.PileFilter(full) == World.HoldingOf(Barrel.Item, Water.Liquid) && pile.Inventory.UsedSlots == 1 && pile.Inventory[Barrel.Item] == 2 && full.Inventory.UsedSlots == 2,
            "empty barrels share one cell and each barrel of water needs its own, on a pile of its own");
    }

    private static void CheckBackpackCarriesEmptyBarrelsAlongsideOneFilledBarrel(GameData data) {
        var world = EmptyWorld(data);
        var pile = world.Add(ThingKind.Pile, world.Player + Vector2.Down * 3);
        pile.Inventory.Add(Barrel.Item, 4);
        string full = StockVessel(world, pile.Inventory, Barrel.Item, Water.Liquid, 50), other = StockVessel(world, pile.Inventory, Barrel.Item, Water.Liquid, 7);
        Require(world.Transfer(pile.Inventory, world.Backpack, Barrel.Item, 4) == 4, "a backpack takes a stack of four empty barrels");
        Require(world.Transfer(pile.Inventory, world.Backpack, full, 1) == 1 && world.Backpack.UsedSlots == 2,
            "one filled barrel fits alongside the stack and has its own cell");
        Require(world.Transfer(pile.Inventory, world.Backpack, other, 1) == 0 && pile.Inventory[other] == 1,
            "a second filled barrel of a different fill stays in the pile");
        var empty = world.Add(ThingKind.Barrel, world.Player + Vector2.Right * 2);
        Require(world.PickUpBarrel(empty) && world.Backpack[Barrel.Item] == 5 && world.Backpack.UsedSlots == 2,
            "lifting an empty barrel adds it to the existing stack");
        var filled = world.Add(ThingKind.Barrel, world.Player + Vector2.Left * 2);
        filled.Inventory.Add(Water.Liquid, 7);
        Require(!world.PickUpBarrel(filled) && filled.Inventory[Water.Liquid] == 7, "a second filled barrel stays placed with its water");
        Require(System.Math.Abs(world.PlayerSpeed - data.WalkSpeed / 2) < .01f, "the filled barrel determines speed when the empty stack came first");
    }

    private static void CheckFillingOneBarrelKeepsItsCell(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Barrel.Item, 5);
        GiveVessel(world, Water.Empty, Water.Liquid, 3, 2);
        int cell = SlotOf(world.Backpack, Barrel.Item);
        PutOnHotbar(world, cell, 0);
        Require(world.MoveInBarrel(Barrel.Item, VesselIn(world.Backpack, Water.Empty, Water.Liquid, 3), 1, true) == 3, "one barrel takes the water from one bottle");
        Require(world.HotbarStack(0) == cell && Vessels.First(world.Backpack, Barrel.Item, Water.Liquid, 3) is string filled && world.HotbarItem(0) == filled && world.Backpack[Barrel.Item] == 4,
            "the filled barrel stays in its cell with its binding and the four empty barrels stay carried");
        Require(world.MoveInBarrel(Barrel.Item, VesselIn(world.Backpack, Water.Empty, Water.Liquid, 3), 1, true) == 0 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1,
            "pouring into a second barrel leaves the other bottle full");
        Require(world.MoveInBarrel(VesselIn(world.Backpack, Barrel.Item, Water.Liquid, 3), Water.Liquid, 3, false) == 3 && world.HotbarItem(0) == Barrel.Item,
            "drawing the water back out keeps the same barrel available");
        world.SortBackpack();
        Require(world.Backpack[Barrel.Item] == 5 && world.Backpack.UsedSlots == 3, "all five empty barrels can share one cell again");
    }

    private static void CheckFillingABarrelNeedsRoomToSplitTheStack(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Barrel.Item, 4);
        GiveVessel(world, Water.Empty, Water.Liquid, 3);
        Give(world, "steel_axe", 22);
        Require(world.Backpack.UsedSlots == 24, "the player starts with a full backpack");
        Require(world.MoveInBarrel(Barrel.Item, VesselIn(world.Backpack, Water.Empty, Water.Liquid, 3), 1, true) == 0 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1 && world.Backpack[Barrel.Item] == 4,
            "the full backpack leaves the water in its bottle when a barrel cannot split from its stack");
        Drop(world, "steel_axe", 1);
        int opened = SlotOf(world.Backpack, Barrel.Item);
        int other = SplitStack(world, opened, 2);
        Require(world.Backpack.UsedSlots == 24 && world.Backpack.Layout[other].Count == 2, "the full backpack has two partial barrel stacks");
        Require(world.MoveInBarrel(Barrel.Item, VesselIn(world.Backpack, Water.Empty, Water.Liquid, 3), 1, true, opened) == 3 && world.Backpack.Layout[other].Count == 3,
            "another stack with room for the remaining empty barrel allows filling in a full backpack");
    }

    private static void CheckBarrelKeepsWaterWhenPlacedAndLifted(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Barrel.Item);
        for (int i = 0; i < 5; i++) {
            string bucket = GiveVessel(world, Bucket.Item, Water.Liquid, 10);
            string barrelItem = i == 0 ? Barrel.Item : VesselIn(world.Backpack, Barrel.Item, Water.Liquid, i * 10);
            Require(world.MoveInBarrel(barrelItem, bucket, 1, true) == 10, "each bucket pours ten liters");
        }
        Require(!world.CanSprint && System.Math.Abs(world.PlayerSpeed - data.WalkSpeed / 2) < .01f, "a full barrel halves walk speed and prevents sprinting");
        var barrel = world.PlaceBarrel(VesselIn(world.Backpack, Barrel.Item, Water.Liquid, 50), world.Player + Vector2.Left * 3);
        Require(barrel != null && barrel.Inventory[Water.Liquid] == 50 && world.PileFilter(barrel) == Water.Liquid, "placement keeps its water and sets the water filter");
        Require(world.CanSprint && world.PickUpBarrel(barrel!), "placing frees the hands, and the barrel can be lifted again");
        Require(Vessels.Count(world.Backpack, Barrel.Item, Water.Liquid, 50) == 1, "lifting keeps all fifty liters");
    }

    private static void CheckWorkerHandsOnlyFitOneBarrel(GameData data) {
        var world = EmptyWorld(data);
        var worker = AddWorker(world, world.Player + Vector2.Right);
        worker.Tool = Bucket.Item;
        var pile = world.Add(ThingKind.Pile, world.Player + Vector2.Down * 3);
        pile.Inventory.Add(Barrel.Item, 2);
        Require(world.Transfer(pile.Inventory, worker.Bag, Barrel.Item, 2) == 1 && worker.Tool == Bucket.Item, "a worker takes one barrel and keeps their tool");
        Give(world, "stone");
        Require(world.Transfer(world.Backpack, worker.Bag, "stone", 1) == 0 && pile.Inventory[Barrel.Item] == 1,
            "no other goods or barrels fit in the worker's hands");
        world.Transfer(worker.Bag, pile.Inventory, Barrel.Item, 1);
        Require(world.Transfer(world.Backpack, worker.Bag, "stone", 1) == 1, "setting down the barrel frees their hands");
    }

    private static void CheckSaveKeepsVesselFillLevels(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Barrel.Item, 4);
        GiveVessel(world, Barrel.Item, Water.Liquid, 13);
        GiveVessel(world, Bucket.Item, Water.Liquid, 7);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Backpack[Barrel.Item] == 4 && Vessels.Count(loaded.Backpack, Barrel.Item, Water.Liquid, 13) == 1 && Vessels.Count(loaded.Backpack, Bucket.Item, Water.Liquid, 7) == 1 && !loaded.CanSprint,
            "the barrel and bucket keep their fill and carry rules after a save loads");
        world.Die();
        loaded.Deserialize(world.Serialize());
        Require(loaded.Dead && loaded.CanSprint && loaded.Nearest(ThingKind.Grave, world.Player) is Thing grave && Vessels.Count(grave.Inventory, Barrel.Item, Water.Liquid, 13) == 1,
            "the new backpack and the recovery box both load after death");
    }

    private static void CheckRecoveryKeepsWorkerBarrelAndWaterVessel(GameData data) {
        var world = EmptyWorld(data);
        var worker = AddWorker(world, world.Player + Vector2.Right);
        string bottle = GiveVessel(world, Water.Empty, Water.Liquid, 1);
        Require(world.Transfer(world.Backpack, worker.ToolSlot, bottle, 1) == 1, "worker tool slots take a bottle");
        string barrel = GiveVessel(world, Barrel.Item, Water.Liquid, 17);
        Require(world.Transfer(world.Backpack, worker.Bag, barrel, 1) == 1, "worker can carry a barrel with a bottle in the tool slot");
        world.Fire(worker);
        var grave = world.Nearest(ThingKind.Grave, worker.Pos);
        Require(grave != null && Vessels.Count(grave.Inventory, Barrel.Item, Water.Liquid, 17) == 1 && Vessels.Count(grave.Inventory, Water.Empty, Water.Liquid, 1) == 1,
            "dismissing the worker leaves the barrel with its water and their tool");
    }

    private static void CheckFullBucketsDoNotPreventRefillingABottle(GameData data) {
        var world = EmptyWorld(data);
        GiveVessel(world, Bucket.Item, Water.Liquid, 10);
        Give(world, Water.Empty);
        var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(world.Player), 10);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.RefilledBottle && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1,
            "a bottle can fill at the lake even when the bucket is already full");
    }

    private static void CheckAFullBucketDoesNotPreventDrinkingAtTheLake(GameData data) {
        var world = EmptyWorld(data);
        GiveVessel(world, Bucket.Item, Water.Liquid, 10);
        SetNeeds(world, thirst: 0);
        var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(world.Player), 10);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceLiter && world.Thirst > 33
            && Vessels.Count(world.Backpack, Bucket.Item, Water.Liquid, 10) == 1, "without bottles a player can drink at the lake while keeping a full bucket");
    }

    private static void CheckHotbarBoundBarrelAcceptsWaterFromSpareBottles(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Barrel.Item);
        string spare = GiveVessel(world, Water.Empty, Water.Liquid, 3), bound = GiveVessel(world, Water.Empty, Water.Liquid, 3);
        PutOnHotbar(world, SlotOf(world.Backpack, Barrel.Item), 0);
        PutOnHotbar(world, SlotOf(world.Backpack, bound), 1);
        world.FromSpares(() => { world.MoveInBarrel(Barrel.Item, bound, 1, true); world.MoveInBarrel(Barrel.Item, spare, 1, true); });
        Require(Vessels.First(world.Backpack, Barrel.Item, Water.Liquid, 3) is string filled && world.HotbarItem(0) == filled && world.HotbarItem(1) == bound && Vessels.LitersIn(world.Backpack, bound) == 3 && world.Backpack[Water.Empty] == 1,
            "Ctrl pours the unbound bottle into the barrel and preserves both hotbar bindings");
    }

    private static void CheckBucketsRefillAndPourWithoutLosingTheirStack(GameData data) {
        var world = new World(data, false);
        Give(world, Bucket.Item, 3);
        Require(world.Backpack.UsedSlots == 1, "empty buckets stack");
        var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(world.Player), 10);
        Water.CollectWater(world, lake);
        Water.CollectWater(world, lake);
        Require(Vessels.Count(world.Backpack, Bucket.Item, Water.Liquid, 10) == 2 && world.Backpack[Bucket.Item] == 1 && world.Backpack.UsedSlots == 3,
            "filling two buckets gives each its own cell");
        var barrel = world.Add(ThingKind.Barrel, world.Player + Vector2.Left);
        barrel.Inventory.Add(Water.Liquid, 45);
        Require(world.Transfer(world.Backpack, barrel.Inventory, VesselIn(world.Backpack, Bucket.Item, Water.Liquid, 10), 2) == 1, "only one bucket starts pouring into five liters of room");
        Require(barrel.Inventory[Water.Liquid] == 50 && Vessels.Count(world.Backpack, Bucket.Item, Water.Liquid, 5) == 1 && Vessels.Count(world.Backpack, Bucket.Item, Water.Liquid, 10) == 1,
            "partial pouring preserves the excess and the other bucket");
    }

    private static void CheckFillingABucketInAFullCrateKeepsItsItemCount(GameData data) {
        var world = EmptyWorld(data);
        var crate = TakeEverything(world, world.Add(ThingKind.Crate, world.Player + Vector2.Right));
        var barrel = world.Add(ThingKind.Barrel, world.Player + Vector2.Left);
        Give(world, Bucket.Item, 12);
        Require(world.Transfer(world.Backpack, crate.Inventory, Bucket.Item, 12) == 12, "a crate takes twelve empty buckets");
        barrel.Inventory.Add(Water.Liquid, 10);
        Require(world.Transfer(barrel.Inventory, crate.Inventory, Water.Liquid, 10) == 10,
            "a bucket can take water even when the crate already holds its limit of items");
        Require(crate.Inventory[Bucket.Item] == 11 && Vessels.Count(crate.Inventory, Bucket.Item, Water.Liquid, 10) == 1 && barrel.Inventory.Count == 0,
            "one bucket becomes full, leaving eleven empty buckets and no water in the barrel");
    }

    private static void CheckWorkerFetchesWaterFromClosestFullBarrelAndClimbsToCabin(GameData data) {
        var world = EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        var source = world.Add(ThingKind.Barrel, cabin.Pos + new Vector2(5, 0));
        var farther = world.Add(ThingKind.Barrel, cabin.Pos + new Vector2(9, 0));
        source.Inventory.Add(Water.Liquid, 50);
        farther.Inventory.Add(Water.Liquid, 50);
        cabin.Inventory.Add(Barrel.Item, 1);
        var worker = AddWorker(world, source.Pos + new Vector2(0, .6f));
        world.SetDuty(worker, cabin, JobKind.Haul, true);
        Work(world, 2);
        Require(source.Inventory[Water.Liquid] == 50 && worker.Tool == "", "hauling requires a vessel");
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, source.Pos + new Vector2(2, 1)));
        rack.Inventory.Add(Bucket.Item, 1);
        Work(world, .5f, () => worker.Status == "Fetching a steel bucket from the tool station");
        Require(worker.Status == "Fetching a steel bucket from the tool station", "the worker names the bucket as a person would see it");
        bool climbed = false;
        for (int i = 0; i < 500 && Vessels.Count(cabin.Inventory, Barrel.Item, Water.Liquid, 10) == 0; i++) {
            SafeTick(world, .05f);
            climbed |= worker.WaterAction == WorkerWaterAction.ClimbUp;
        }
        Require(climbed && Vessels.Count(cabin.Inventory, Barrel.Item, Water.Liquid, 10) == 1, $"worker delivers ten liters by ladder, status was {worker.Status}");
        Require(source.Inventory[Water.Liquid] == 40 && farther.Inventory[Water.Liquid] == 50 && worker.Tool == Bucket.Item,
            "the closest full barrel loses only what reaches the cabin and the bucket is kept");
    }

    private static void CheckWorkerFindsShoreAccessAroundACabin(GameData data) {
        var world = EmptyWorld(data);
        var at = new Vector2(111.6f, 111.5f);
        var cabin = world.Add(ThingKind.Hut, new Vector2(111, 113));
        world.Add(ThingKind.Water, Terrain.WaterPoint(at), 10);
        cabin.Inventory.Add(Barrel.Item, 1);
        Require(world.CanStand(at) && !world.CanStand(Terrain.WaterPoint(at)), "the cabin blocks the direct shore point");
        var worker = AddWorker(world, at);
        worker.Tool = Water.Empty;
        world.SetDuty(worker, cabin, JobKind.Haul, true);
        for (int i = 0; i < 600 && Vessels.Count(cabin.Inventory, Barrel.Item, Water.Liquid, 3) == 0; i++) SafeTick(world, .05f);
        Require(Vessels.Count(cabin.Inventory, Barrel.Item, Water.Liquid, 3) == 1, $"worker should go around the obstruction to fill the barrel: {worker.Status}");
    }

    private static void CheckBlockedToolSwapLetsTheWorkerDoTheirNextJob(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn + new Vector2(0, 6);
        var barrel = world.Add(ThingKind.Barrel, at);
        world.SetFilter(barrel, Water.Liquid, false, true);
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, at + Vector2.Right * 4));
        rack.Inventory.Add(Bucket.Item, 1);
        rack.Inventory.Filter(Bucket.Item, false, true);
        var tree = world.Add(ThingKind.Tree, at + Vector2.Left * 4, 10);
        TakeEverything(world, world.Add(ThingKind.Chest, at + Vector2.Down * 4));
        var worker = AddWorker(world, tree.Pos + Vector2.Down);
        worker.Tool = "steel_axe";
        world.SetDuty(worker, barrel, JobKind.Haul, true);
        world.SetDuty(worker, tree, JobKind.Gather, true);
        Work(world, 10, () => tree.Remaining < 10);
        Require(tree.Remaining < 10, "a worker who cannot put back their axe moves on to the woodcutting job");
    }

    private static void CheckWorkerSwapsABottleForAnAxe(GameData data) {
        var world = EmptyWorld(data);
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, world.Player + Vector2.Right));
        var tree = world.Add(ThingKind.Tree, world.Player + Vector2.Left * 2, 10);
        TakeEverything(world, world.Add(ThingKind.Chest, world.Player + Vector2.Down * 2));
        var worker = AddWorker(world, world.Player);
        string bottle = GiveVessel(world, Water.Empty, Water.Liquid, 3);
        Require(world.Transfer(world.Backpack, rack.Inventory, bottle, 1) == 1, "a rack accepts a filled bottle from the player");
        Require(world.Transfer(rack.Inventory, worker.ToolSlot, bottle, 1) == 1, "a player can equip a filled bottle from the rack");
        rack.Inventory.Add("steel_axe", 1);
        world.SetDuty(worker, tree, JobKind.Gather, true);
        Work(world, 10, () => tree.Remaining < 10);
        Require(tree.Remaining < 10 && Vessels.Count(rack.Inventory, Water.Empty, Water.Liquid, 3) == 1, "the worker puts the full bottle back and uses the axe");
    }

    private static void CheckRackTakesJamButDoesNotOfferItForWatering(GameData data) {
        var world = EmptyWorld(data);
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, world.Player + Vector2.Right));
        string jam = GiveVessel(world, Water.Empty, BerryJam.Liquid, 3);
        Require(world.Transfer(world.Backpack, rack.Inventory, jam, 1) == 1,
            "a tool rack must take the same flask when it holds jam");
        Require(!world.StationsHold(BarrelHaul.WaterTool), "a jam bottle is not available for a water delivery");
    }

    private static void CheckWorkerFetchesABottleFromTheRack(GameData data) {
        var world = EmptyWorld(data);
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, world.Player + Vector2.Right));
        var target = world.Add(ThingKind.Barrel, world.Player + Vector2.Left * 3);
        string bottle = GiveVessel(world, Water.Empty, Water.Liquid, 3);
        Require(world.Transfer(world.Backpack, rack.Inventory, bottle, 1) == 1, "the only available vessel is the full bottle on the rack");
        var worker = AddWorker(world, world.Player + Vector2.Down);
        world.SetDuty(worker, target, JobKind.Haul, true);
        Work(world, 6);
        Require(target.Inventory[Water.Liquid] == 0, "a worker hauling for a barrel whose filter does not name water should pour none in");
        world.SetFilter(target, Water.Liquid, false, true);
        Work(world, 6, () => target.Inventory[Water.Liquid] > 0);
        Require(target.Inventory[Water.Liquid] == 3 && worker.Tool == Water.Empty, "a worker fetches a bottle and pours its three liters");
    }

    private static void CheckBlockedBarrelLetsTheWorkerDoTheirNextJob(GameData data) {
        var world = EmptyWorld(data);
        var cliff = Cliff.Of(ThingKind.Coal);
        var barrel = world.Add(ThingKind.Barrel, cliff.Center + cliff.Face * 1.7f);
        world.SetFilter(barrel, Water.Liquid, false, true);
        var tree = world.Add(ThingKind.Tree, barrel.Pos + cliff.Face * 4, 10);
        TakeEverything(world, world.Add(ThingKind.Chest, tree.Pos + cliff.Face * 2));
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, tree.Pos + cliff.Along * 2));
        rack.Inventory.Add("steel_axe", 1);
        var worker = AddWorker(world, tree.Pos);
        StockVessel(world, worker.ToolSlot, Bucket.Item, Water.Liquid, 10);
        Require(world.CanStand(barrel.Pos + cliff.Face * .5f) && !world.CanStand(barrel.Pos + BarrelHaul.SpoutApproach),
            "the barrel is on open ground with its pour point against the cliff");
        world.SetDuty(worker, barrel, JobKind.Haul, true);
        world.SetDuty(worker, tree, JobKind.Gather, true);
        Work(world, 16, () => tree.Remaining < 10);
        Require(tree.Remaining < 10 && barrel.Inventory[Water.Liquid] == 0, "a worker who cannot pour at the barrel switches to the tree");
    }

    private static void CheckWorkerReachesBarrelAroundAChest(GameData data) {
        var world = EmptyWorld(data);
        var barrel = world.Add(ThingKind.Barrel, new Vector2(74.08f, 123.28f));
        world.SetFilter(barrel, Water.Liquid, false, true);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(74.98f, 124.44f)));
        var worker = AddWorker(world, new Vector2(80.91f, 126.81f));
        StockVessel(world, worker.ToolSlot, Bucket.Item, Water.Liquid, 10);
        Require(world.CanStand(barrel.Pos + BarrelHaul.SpoutApproach), "the chest leaves the barrel's spout on open ground");
        world.SetDuty(worker, barrel, JobKind.Haul, true);
        for (int i = 0; i < 300 && barrel.Inventory[Water.Liquid] == 0 && worker.Status != World.BlockedPath; i++)
            SafeTick(world, .05f);
        Require(barrel.Inventory[Water.Liquid] == 10,
            $"a worker should go around the chest to pour at the barrel, at {worker.Pos} with status {worker.Status}");
    }

    private static void CheckWorkerKeepsTheirWaterSourceWhileWalking(GameData data) {
        var world = EmptyWorld(data);
        var target = world.Add(ThingKind.Barrel, world.Player + Vector2.Down * 3);
        target.Inventory.Add(Water.Liquid, 47);
        var first = world.Add(ThingKind.Barrel, world.Player + Vector2.Right * 9);
        first.Inventory.Add(Water.Liquid, 50);
        var worker = AddWorker(world, world.Player);
        worker.Tool = Water.Empty;
        world.SetDuty(worker, target, JobKind.Haul, true);
        Work(world, .5f);
        Require(worker.Status == "Fetching water" && worker.Pos.X > world.Player.X, "the worker has started walking to the first source");
        var closer = world.Add(ThingKind.Barrel, world.Player + new Vector2(3, 2));
        closer.Inventory.Add(Water.Liquid, 50);
        Work(world, 18, () => target.Inventory[Water.Liquid] == 50);
        Require(target.Inventory[Water.Liquid] == 50 && first.Inventory[Water.Liquid] == 47 && closer.Inventory[Water.Liquid] == 50,
            "a closer barrel becoming available does not change the source mid-walk");
    }

    private static void CheckCabinServesThirstBeforeBottlesInEntryOrder(GameData data) {
        var world = EmptyWorld(data);
        var guest = world.Arrive("first in", 2);
        Give(world, Water.Empty);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 3);
        StepInside(world, cabin, guest);
        StepInside(world, cabin);
        world.Doorways.TurnKey();
        RunUntilNight(world);
        ShutDens(world);
        SetNeeds(world, hunger: 100, thirst: 55);
        SetNeeds(guest, hunger: 100, thirst: 55);
        world.Sleep.Start(cabin);
        world.As(guest, () => world.Sleep.Start(cabin));
        UntilAllAwake(world);
        Require(cabin.Inventory[Barrel.Item] == 1 && world.Backpack[Water.Empty] == 1 && guest.Backpack[Water.Empty] == 1,
            "both players' thirst takes all available water before either bottle");
        Require(guest.Thirst - world.Thirst > 11 && guest.Thirst - world.Thirst < 12, "the first player inside received two liters and the next received one");
    }

    private static void CheckCabinBarrelReplacesThirstLostWhileSleeping(GameData data) {
        var world = EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 20);
        Give(world, Water.Empty);
        StepInside(world, cabin);
        world.Doorways.TurnKey();
        RunUntilPhase(world, .75f);
        ShutDens(world);
        SetNeeds(world, hunger: 100, thirst: 55);
        Require(world.Sleep.Preview(cabin).Thirst == 100, "enough water in the cabin barrel should predict full thirst at dawn");
        world.Sleep.Start(cabin);
        UntilAllAwake(world);
        Require(world.Thirst == 100 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1, "the player wakes fully watered with a full bottle");
        Require(Vessels.Count(cabin.Inventory, Barrel.Item, Water.Liquid, 14) == 1, "two liters at bedtime, one at dawn and three in the bottle leave fourteen");
    }

    private static void CheckSleepingPreviewUsesRemainingWaterOnWaking(GameData data) {
        var (world, guest, _) = Hosted(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        StepInside(world, cabin);
        StepInside(world, cabin, guest);
        world.Doorways.TurnKey();
        RunUntilPhase(world, .75f);
        Drop(world.Me, Water.Empty);
        SetNeeds(world, 100, 90);
        SetNeeds(guest, 100, 100);
        world.Sleep.Start(cabin);
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 1);
        float preview = world.Sleep.Preview(cabin).Thirst;
        world.Tick(data.DaySeconds / 4, sleeping: true);
        Require(preview is > 98 and < 99 && System.Math.Abs(world.Thirst - preview) < .05f,
            $"a sleeper's forecast should leave the last liter for morning, not an awake cabin mate, forecast {preview:0.0}, actual {world.Thirst:0.0}");
        Require(guest.Backpack[Water.Empty] == 1 && guest.Thirst < 100, "an awake cabin mate should not use the morning water");
    }

    private static void CheckWakingEarlyLeavesAnEarlierCabinMateTheirWater(GameData data) {
        var (world, guest, _) = Hosted(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 3);
        Drop(world.Me, Water.Empty);
        Drop(guest, Water.Empty);
        StepInside(world, cabin, guest);
        StepInside(world, cabin);
        SetNeeds(guest, hunger: 100, thirst: 40);
        SetNeeds(world, hunger: 100, thirst: 1);
        world.Sleep.Start(cabin);
        world.Sleep.Wake();
        Require(Vessels.Count(cabin.Inventory, Barrel.Item, Water.Liquid, 2) == 1,
            "a player who sleeps and gets up at once should leave the two liters an awake cabin mate who came in first needs");
        world.As(guest, () => world.Sleep.Start(cabin));
        Require(guest.Thirst == 100, $"the cabin mate who came in first should drink their fill at bedtime, got {guest.Thirst:0.0}");
    }

    private static void CheckCabinFillsBottlesAtBedtime(GameData data) {
        var world = EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 5);
        Give(world, Water.Empty);
        StepInside(world, cabin);
        world.Doorways.TurnKey();
        RunUntilPhase(world, .75f);
        ShutDens(world);
        SetNeeds(world, hunger: 100, thirst: 55);
        float preview = world.Sleep.Preview(cabin).Thirst;
        Require(preview is > 74 and < 76, "after two bedtime liters, the bottle uses the remaining three before dawn");
        world.Sleep.Start(cabin);
        UntilAllAwake(world);
        Require(System.Math.Abs(world.Thirst - preview) < .1f && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1 && cabin.Inventory[Barrel.Item] == 1,
            "the player wakes thirsty with a full bottle when bedtime uses all five liters");
    }

    private static void CheckCabinServesScarceWakeUpWaterInEntryOrder(GameData data) {
        var world = EmptyWorld(data);
        var guest = world.Arrive("first in", 2);
        world.Arrive("stays up", 3);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 5);
        Drop(guest, Water.Empty);
        StepInside(world, cabin, guest);
        StepInside(world, cabin);
        world.Doorways.TurnKey();
        RunUntilPhase(world, .75f);
        SetNeeds(world, hunger: 100, thirst: 55);
        SetNeeds(guest, hunger: 100, thirst: 55);
        world.Sleep.Start(cabin);
        world.As(guest, () => world.Sleep.Start(cabin));
        float first = world.As(guest, () => world.Sleep.Preview(cabin).Thirst);
        float second = world.Sleep.Preview(cabin).Thirst;
        Require(first == 100 && second is > 74 and < 76, "the only wake-up liter goes to the first player who entered");
        world.Tick(world.Sleep.SecondsToDawn, sleeping: true);
        Require(guest.Thirst == first && System.Math.Abs(world.Thirst - second) < .1f,
            "both players wake with the thirst their cabin preview showed");
        Require(cabin.Inventory[Barrel.Item] == 1 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 0 && Vessels.Count(guest.Backpack, Water.Empty, Water.Liquid, 3) == 0,
            "the first occupant uses the remaining liter");
    }

    private static void CheckAWholeLiterAtDawnServesEachOccupant(GameData data) {
        var world = EmptyWorld(data);
        var guest = world.Arrive("first in", 2);
        Drop(guest, Water.Empty);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 2);
        StepInside(world, cabin, guest);
        StepInside(world, cabin);
        world.Doorways.TurnKey();
        // Eight game hours cost one liter per person.
        RunUntil(world, data.DaySeconds - data.DaySeconds / 3 + 1);
        ShutDens(world);
        SetNeeds(world, hunger: 100, thirst: 100);
        SetNeeds(guest, hunger: 100, thirst: 100);
        Require(world.Sleep.Preview(cabin).Thirst == 100 && world.As(guest, () => world.Sleep.Preview(cabin).Thirst) == 100,
            "two liters for two people should forecast full thirst after an eight-hour sleep");
        world.Sleep.Start(cabin);
        world.As(guest, () => world.Sleep.Start(cabin));
        UntilAllAwake(world);
        Require(guest.Thirst == 100 && world.Thirst == 100 && cabin.Inventory[Barrel.Item] == 1,
            "the player who wakes first should use only one liter despite the rounding of the needs during sleep");
    }

    private static void CheckPlayerOutsideCannotDrinkFromTheCabin(GameData data) {
        var world = EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + Vector2.Down * 2);
        StockVessel(world, cabin.Inventory, Barrel.Item, Water.Liquid, 20);
        RunUntilPhase(world, .75f);
        SetNeeds(world, hunger: 100, thirst: 5);
        float time = world.Time;
        world.Sleep.Start(cabin);
        Require(world.Time == time && world.Thirst == 5 && Vessels.Count(cabin.Inventory, Barrel.Item, Water.Liquid, 20) == 1,
            "a player outside the cabin cannot use its roof water to sleep");
    }

    private static void CheckRemovingCabinReturnsItsBarrelWithWater(GameData data) {
        var world = EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + Vector2.Left * 3);
        Give(world, Barrel.Item, 4);
        string barrel = GiveVessel(world, Barrel.Item, Water.Liquid, 13);
        Require(world.Transfer(world.Backpack, cabin.Inventory, barrel, 1) == 1, "the dedicated slot mounts a barrel");
        Require(world.Construction.Dismantle(cabin) && Vessels.Count(world.Backpack, Barrel.Item, Water.Liquid, 13) == 1 && world.Backpack[Barrel.Item] == 4,
            "taking down the cabin returns its barrel and water alongside the carried empties");
    }
}
