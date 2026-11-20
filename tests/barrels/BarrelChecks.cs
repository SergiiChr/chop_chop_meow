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
        Test(CheckCabinServesThirstBeforeBottlesInEntryOrder, data);
        Test(CheckRemovingCabinReturnsItsBarrelWithWater, data);
    }

    private static void CheckBenchStacksBarrelsMadeWithSteelHammer(GameData data) {
        var world = new World(data, false);
        var bench = world.Add(ThingKind.Bench, world.Player + Vector2.Right);
        Require(Give(world, "wood_hammer") == 1 && world.SlotTool(bench, "wood_hammer"), "bench should accept a wooden mallet");
        Give(world, "steel_bar", 13);
        world.QueueBatch(bench, Barrel.Item, 2);
        Work(world, 12);
        Require(bench.Inventory[Barrel.Item] == 0 && !bench.Processing, "the barrel recipe should wait for the steel hammer");
        world.UnslotTool(bench, "wood_hammer");
        Require(Give(world, "steel_hammer") == 1 && world.SlotTool(bench, "steel_hammer"), "bench should accept a steel hammer");
        CompleteBatches(world, bench, Barrel.Item, 2);
        Require(bench.Inventory.UsedSlots == 1 && world.Backpack["steel_bar"] == 3, "two barrels stack and cost ten steel bars");
        Require(world.Transfer(bench.Inventory, world.Backpack, Barrel.Item, 2) == 1, "a backpack can take only one barrel");
        Require(world.Transfer(bench.Inventory, world.Backpack, Barrel.Item, 1) == 0, "a second barrel stays in the output");
        var pile = world.Add(ThingKind.Pile, world.Player + Vector2.Down * 3);
        Require(world.Transfer(world.Backpack, pile.Inventory, Barrel.Item, 1) == 1, "the pile should take the barrel");
        Require(world.Transfer(bench.Inventory, pile.Inventory, Barrel.Item, 1) == 1, "the pile accepts the second barrel from the bench");
        world.QueueBatch(bench, Bucket.Item);
        CompleteBatches(world, bench, Bucket.Item, 1);
        Require(world.Backpack["steel_bar"] == 0, "one bucket costs three steel bars");
    }

    private static void CheckPileStacksEmptyBarrelsAndKeepsFullBarrelsSeparate(GameData data) {
        var world = new World(data, false);
        var pile = world.Add(ThingKind.Pile, world.Player + Vector2.Down * 3);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.Right);
        Give(world, Barrel.Item);
        var said = new System.Collections.Generic.List<string>(); world.Notify += said.Add;
        Require(world.Transfer(world.Backpack, chest.Inventory, Barrel.Item, 1) == 0 && said.Contains("Barrel does not fit in storage chest."),
            "a chest must say why it refuses a barrel");
        world.Transfer(world.Backpack, pile.Inventory, Barrel.Item, 1);
        Give(world, Barrel.Item);
        world.Transfer(world.Backpack, pile.Inventory, Barrel.Item, 1);
        for (int i = 0; i < 2; i++) {
            Give(world, Barrel.AtFill(50));
            Require(world.Transfer(world.Backpack, pile.Inventory, Barrel.AtFill(50), 1) == 1, "the pile takes filled barrels");
        }
        Require(pile.Inventory.UsedSlots == 3 && pile.Inventory[Barrel.Item] == 2, "empty barrels share one cell and each full barrel needs its own");
    }

    private static void CheckBarrelKeepsWaterWhenPlacedAndLifted(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Barrel.Item);
        for (int i = 0; i < 5; i++) {
            Give(world, Bucket.AtFill(10));
            Require(world.MoveInBarrel(Barrel.AtFill(i * 10), Bucket.AtFill(10), 1, true) == 10, "each bucket pours ten liters");
        }
        Require(!world.CanSprint && System.Math.Abs(world.PlayerSpeed - data.WalkSpeed / 2) < .01f, "a full barrel halves walk speed and prevents sprinting");
        var barrel = world.PlaceBarrel(Barrel.AtFill(50), world.Player + Vector2.Left * 3);
        Require(barrel != null && barrel.Inventory[Water.Liquid] == 50 && world.PileFilter(barrel) == Water.Liquid, "placement keeps its water and sets the water filter");
        Require(world.CanSprint && world.PickUpBarrel(barrel!), "placing frees the hands, and the barrel can be lifted again");
        Require(world.Backpack[Barrel.AtFill(50)] == 1, "lifting keeps all fifty liters");
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
        Give(world, Barrel.AtFill(13));
        Give(world, Bucket.AtFill(7));
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Backpack[Barrel.AtFill(13)] == 1 && loaded.Backpack[Bucket.AtFill(7)] == 1 && !loaded.CanSprint,
            "the barrel and bucket keep their fill and carry rules after a save loads");
        world.Die();
        loaded.Deserialize(world.Serialize());
        Require(loaded.Dead && loaded.CanSprint && loaded.Nearest(ThingKind.Grave, world.Player)?.Inventory[Barrel.AtFill(13)] == 1,
            "the new backpack and the recovery box both load after death");
    }

    private static void CheckRecoveryKeepsWorkerBarrelAndWaterVessel(GameData data) {
        var world = EmptyWorld(data);
        var worker = AddWorker(world, world.Player + Vector2.Right);
        Give(world, Water.OneLiter);
        Require(world.Transfer(world.Backpack, worker.ToolSlot, Water.OneLiter, 1) == 1, "worker tool slots take a bottle");
        Give(world, Barrel.AtFill(17));
        Require(world.Transfer(world.Backpack, worker.Bag, Barrel.AtFill(17), 1) == 1, "worker can carry a barrel with a bottle in the tool slot");
        world.Fire(worker);
        var grave = world.Nearest(ThingKind.Grave, worker.Pos);
        Require(grave != null && grave.Inventory[Barrel.AtFill(17)] == 1 && grave.Inventory[Water.OneLiter] == 1,
            "dismissing the worker leaves the barrel with its water and their tool");
    }

    private static void CheckFullBucketsDoNotPreventRefillingABottle(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Bucket.AtFill(10));
        Give(world, Water.Empty);
        var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(world.Player), 10);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.RefilledBottle && world.Backpack[Water.Full] == 1,
            "a bottle can fill at the lake even when the bucket is already full");
    }

    private static void CheckAFullBucketDoesNotPreventDrinkingAtTheLake(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Bucket.AtFill(10));
        SetNeeds(world, thirst: 0);
        var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(world.Player), 10);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceLiter && world.Thirst > 33
            && world.Backpack[Bucket.AtFill(10)] == 1, "without bottles a player can drink at the lake while keeping a full bucket");
    }

    private static void CheckHotbarBoundBarrelAcceptsWaterFromSpareBottles(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Barrel.Item);
        Give(world, Water.Full, 2);
        PutOnHotbar(world, SlotOf(world.Backpack, Barrel.Item), 0);
        PutOnHotbar(world, SlotOf(world.Backpack, Water.Full), 1);
        world.FromSpares(() => world.MoveInBarrel(Barrel.Item, Water.Full, 2, true));
        Require(world.HotbarItem(0) == Barrel.AtFill(3) && world.HotbarItem(1) == Water.Full && world.Backpack[Water.Empty] == 1,
            "Ctrl pours the unbound bottle into the barrel and preserves both hotbar bindings");
    }

    private static void CheckBucketsRefillAndPourWithoutLosingTheirStack(GameData data) {
        var world = new World(data, false);
        Give(world, Bucket.Item, 3);
        Require(world.Backpack.UsedSlots == 1, "empty buckets stack");
        var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(world.Player), 10);
        Water.CollectWater(world, lake);
        Water.CollectWater(world, lake);
        Require(world.Backpack[Bucket.AtFill(10)] == 2 && world.Backpack[Bucket.Item] == 1 && world.Backpack.UsedSlots == 3,
            "filling two buckets gives each its own cell");
        var barrel = world.Add(ThingKind.Barrel, world.Player + Vector2.Left);
        barrel.Inventory.Add(Water.Liquid, 45);
        Require(world.Transfer(world.Backpack, barrel.Inventory, Bucket.AtFill(10), 2) == 1, "only one bucket starts pouring into five liters of room");
        Require(barrel.Inventory[Water.Liquid] == 50 && world.Backpack[Bucket.AtFill(5)] == 1 && world.Backpack[Bucket.AtFill(10)] == 1,
            "partial pouring preserves the excess and the other bucket");
    }

    private static void CheckFillingABucketInAFullCrateKeepsItsItemCount(GameData data) {
        var world = EmptyWorld(data);
        var crate = world.Add(ThingKind.Crate, world.Player + Vector2.Right);
        var barrel = world.Add(ThingKind.Barrel, world.Player + Vector2.Left);
        Give(world, Bucket.Item, 12);
        Require(world.Transfer(world.Backpack, crate.Inventory, Bucket.Item, 12) == 12, "a crate takes twelve empty buckets");
        barrel.Inventory.Add(Water.Liquid, 10);
        Require(world.Transfer(barrel.Inventory, crate.Inventory, Water.Liquid, 10) == 10,
            "a bucket can take water even when the crate already holds its limit of items");
        Require(crate.Inventory[Bucket.Item] == 11 && crate.Inventory[Bucket.AtFill(10)] == 1 && barrel.Inventory.Count == 0,
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
        var rack = world.Add(ThingKind.Toolstation, source.Pos + new Vector2(2, 1));
        rack.Inventory.Add(Bucket.Item, 1);
        bool climbed = false;
        for (int i = 0; i < 500 && cabin.Inventory[Barrel.AtFill(10)] == 0; i++) {
            SafeTick(world, .05f);
            climbed |= worker.WaterAction == WorkerWaterAction.ClimbUp;
        }
        Require(climbed && cabin.Inventory[Barrel.AtFill(10)] == 1, $"worker delivers ten liters by ladder, status was {worker.Status}");
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
        for (int i = 0; i < 600 && cabin.Inventory[Barrel.AtFill(3)] == 0; i++) SafeTick(world, .05f);
        Require(cabin.Inventory[Barrel.AtFill(3)] == 1, $"worker should go around the obstruction to fill the barrel: {worker.Status}");
    }

    private static void CheckBlockedToolSwapLetsTheWorkerDoTheirNextJob(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn + new Vector2(0, 6);
        var barrel = world.Add(ThingKind.Barrel, at);
        var rack = world.Add(ThingKind.Toolstation, at + Vector2.Right * 4);
        rack.Inventory.Add(Bucket.Item, 1);
        rack.Inventory.InputFilter.Add(Bucket.Item);
        var tree = world.Add(ThingKind.Tree, at + Vector2.Left * 4, 10);
        world.Add(ThingKind.Chest, at + Vector2.Down * 4);
        var worker = AddWorker(world, tree.Pos + Vector2.Down);
        worker.Tool = "steel_axe";
        world.SetDuty(worker, barrel, JobKind.Haul, true);
        world.SetDuty(worker, tree, JobKind.Gather, true);
        Work(world, 10, () => tree.Remaining < 10);
        Require(tree.Remaining < 10, "a worker who cannot put back their axe moves on to the woodcutting job");
    }

    private static void CheckCabinServesThirstBeforeBottlesInEntryOrder(GameData data) {
        var world = EmptyWorld(data);
        var guest = world.Arrive("first in", 2);
        Give(world, Water.Empty);
        var cabin = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -7));
        cabin.Inventory.Add(Barrel.AtFill(3), 1);
        StepInside(world, cabin, guest);
        StepInside(world, cabin);
        RunUntilNight(world);
        SetNeeds(world, hunger: 100, thirst: 55);
        SetNeeds(guest, hunger: 100, thirst: 55);
        world.Sleep(cabin);
        Require(cabin.Inventory[Barrel.Item] == 1 && world.Backpack[Water.Empty] == 1 && guest.Backpack[Water.Empty] == 1,
            "both players' thirst takes all available water before either bottle");
        Require(guest.Thirst - world.Thirst > 11 && guest.Thirst - world.Thirst < 12, "the first player inside received two liters and the next received one");
    }

    private static void CheckRemovingCabinReturnsItsBarrelWithWater(GameData data) {
        var world = EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + Vector2.Left * 3);
        Give(world, Barrel.AtFill(13));
        Require(world.Transfer(world.Backpack, cabin.Inventory, Barrel.AtFill(13), 1) == 1, "the dedicated slot mounts a barrel");
        Require(world.Construction.Dismantle(cabin) && world.Backpack[Barrel.AtFill(13)] == 1, "taking down the cabin returns its barrel and water");
    }
}
