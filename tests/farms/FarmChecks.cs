using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static CabinSteps;
using static Checks;
using static FarmSteps;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

public static class FarmChecks {
    public static void Run(GameData data) {
        Test(CheckFarmPlotBuildsBesidePileAndTakesOnlyFarmGoods, data);
        Test(CheckDryWheatWaitsForWater, data);
        Test(CheckSpareSeedCannotTakeTheWaterSlot, data);
        Test(CheckSownWheatGrowsToRipeInItsDays, data);
        Test(CheckPlotHarvestedAloneKeepsSeedInItsOutput, data);
        Test(CheckRipeWheatKeepsStoredWater, data);
        Test(CheckWorkerFinishesHarvestHaulsCropAndSowsAgain, data);
        Test(CheckPlotHoldsTenLitersAndKeepsWaterOnLoad, data);
        Test(CheckHaulerPoursABucketAtFourCorners, data);
        Test(CheckWateredGrowthAllocatesNothing, data);
    }

    private static void CheckFarmPlotBuildsBesidePileAndTakesOnlyFarmGoods(GameData data) {
        var (world, compost, farm) = FarmBesideCompost(data);
        Require(farm.Pos == compost.Pos + new Vector2(4.5f, 1.5f), "a farm plot should snap edge to edge with a pile");
        Require(world.CanStand(farm.Pos), "a plot should be walkable");
        var next = world.Construction.Snap(ThingKind.Farm, farm.Pos + new Vector2(6.3f, .2f));
        Require(next == farm.Pos + new Vector2(6, 0), "farm plots six meters wide should snap edge to edge");
        world.Player = farm.Pos;
        Require(world.Construction.PlacementBlock(ThingKind.Chest, farm.Pos + new Vector2(2.8f, 2.8f)) == "Leave room around resources and buildings.",
            "a building should not fit inside a farm's corner");
        Require(!world.Holds(farm, "stone"), "a plot should take only farm goods");
        Require(world.GatherBlock(farm) != null, "a plot should have nothing to harvest before sowing");
    }

    private static void CheckDryWheatWaitsForWater(GameData data) {
        var (world, _, farm) = FarmBesideCompost(data);
        world.Transfer(world.Backpack, farm.Inventory, "wheat_seeds", 1);
        world.Transfer(world.Backpack, farm.Inventory, "fertilizer", 1);
        SafeTick(world, data.DaySeconds);
        Require(farm.FarmStage == FarmStage.Growing && farm.Progress == 0, "a dry plot sows but makes no progress");
    }

    private static void CheckSpareSeedCannotTakeTheWaterSlot(GameData data) {
        var (world, _, farm) = FarmBesideCompost(data);
        farm.FarmStage = FarmStage.Growing;
        farm.Inventory.Add("wheat_seeds", 63);
        farm.Inventory.Add("fertilizer", 1);
        farm.Output!.Add("wheat_seeds", 2);
        var worker = AddWorker(world, farm.Pos + new Vector2(0, 5));
        world.SetDuty(worker, farm, JobKind.Haul, true);
        Work(world, .25f);
        Require(worker.Status.StartsWith("Sowing back "), "the hauler should be on the way to move the spare seed");
        world.Transfer(world.Backpack, farm.Inventory, "wheat_seeds", 1);
        Work(world, 8);
        Give(world, Bucket.AtFill(10));
        Require(farm.Inventory["wheat_seeds"] == 64 && world.Transfer(world.Backpack, farm.Inventory, Bucket.AtFill(10), 1) == 1,
            "a seed stack filled while the hauler walks must still leave a slot for water");
    }

    private static void CheckSownWheatGrowsToRipeInItsDays(GameData data) {
        var (world, _, farm) = FarmBesideCompost(data);
        world.Transfer(world.Backpack, farm.Inventory, "wheat_seeds", 1); world.Transfer(world.Backpack, farm.Inventory, "fertilizer", 1);
        world.Tick(.1f);
        Require(farm.FarmStage == FarmStage.Growing, "seeds and fertilizer should sow the plot");
        Require(farm.Inventory.Count == 0, "sowing should use up the seeds and fertilizer");
        Require(world.Farms.CropLook(farm) == 1, "a sown plot should show soil");
        var looks = new List<int>();
        for (int day = 0; day < 3; day++) {
            Give(world, Bucket.AtFill(10));
            Require(world.Transfer(world.Backpack, farm.Inventory, Bucket.AtFill(10), 1) == 1,
                $"a plot takes a full bucket on day {day + 1}: {world.Backpack[Bucket.AtFill(10)]} buckets held, {world.Farms.WaterLevel(farm):0.###} L left, in reach: {world.Reaches(farm)}");
            Work(world, data.DaySeconds - 1);
            looks.Add(world.Farms.CropLook(farm));
            Work(world, 1);
            Require(farm.Inventory[Water.Liquid] == 0 && world.Farms.WaterLevel(farm) == 0, "one day of growth consumes ten liters");
        }
        Require(looks.SequenceEqual(new[] { 1, 2, 3 }),
            $"wheat should grow through soil, straws and green plants: {string.Join(",", looks)}");
        Require(farm.FarmStage == FarmStage.Ripe, $"wheat should be ripe in {data.Farm.GrowDays} days");
        Require(world.Farms.CropLook(farm) == 4, "ripe wheat should look ripe");
    }

    /// <summary>A plot harvested with nobody to haul keeps its seed in the output: nothing sows it back on its own.</summary>
    private static void CheckPlotHarvestedAloneKeepsSeedInItsOutput(GameData data) {
        var world = EmptyWorld(data);
        var lone = world.Add(ThingKind.Farm, new Vector2(20, 14));
        lone.FarmStage = FarmStage.Ripe; world.Player = lone.Pos;
        world.Gather(lone, world.Farms.HarvestSeconds); world.Tick(1);
        Require(lone.FarmStage == FarmStage.Fallow, "a harvested plot should lie fallow");
        Require(lone.Output!["wheat_seeds"] == 2, "harvested seed should wait in the output");
        Require(lone.Inventory["wheat_seeds"] == 0, "harvested seed should not go into the plot by itself");
    }

    private static void CheckRipeWheatKeepsStoredWater(GameData data) {
        var world = EmptyWorld(data);
        var farm = world.Add(ThingKind.Farm, new Vector2(20, 14));
        farm.FarmStage = FarmStage.Ripe;
        farm.Inventory.Add(Water.Liquid, 4);
        Work(world, data.DaySeconds);
        Require(farm.FarmStage == FarmStage.Ripe && world.Farms.WaterLevel(farm) == 4,
            "ripe wheat should keep its water for the next crop");
    }

    private static void CheckWorkerFinishesHarvestHaulsCropAndSowsAgain(GameData data) {
        var (world, compost, farm) = FarmBesideCompost(data);
        farm.FarmStage = FarmStage.Ripe;
        var origin = compost.Pos;
        world.Player = farm.Pos;
        world.Gather(farm, world.Farms.HarvestSeconds / 2);
        Require(farm.FarmStage == FarmStage.Ripe && Math.Abs(farm.Progress - world.Farms.HarvestSeconds / 2) < .01f, "the player should harvest by hand, without a tool");
        var chest = world.Add(ThingKind.Chest, origin + new Vector2(3, 4));
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, farm);
        Give(world, Inventory.Coins, 500);
        // The player waits out the nights in a cabin, out of the wolves' reach.
        var hut = world.Add(ThingKind.Hut, new Vector2(40, 40));
        StepInside(world, hut);
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
        Require(chest.Inventory["fertilizer"] == 0, "the worker should bring the fertilizer");
        Require(farm.Inventory["wheat_seeds"] == 1 && chest.Inventory["wheat_seeds"] == 0, "the worker should use the seeds moved back in and bring the spare seed for the next sowing");
    }

    private static void CheckPlotHoldsTenLitersAndKeepsWaterOnLoad(GameData data) {
        var (world, _, farm) = FarmBesideCompost(data);
        Give(world, Bucket.AtFill(10), 2);
        Require(world.Transfer(world.Backpack, farm.Inventory, Bucket.AtFill(10), 2) == 1, "only one full bucket fits in the plot");
        Require(world.Backpack[Bucket.AtFill(10)] == 1 && world.Backpack[Bucket.Item] == 1, "the other bucket stays full");
        Work(world, data.DaySeconds / 2);
        Require(farm.Inventory[Water.Liquid] == 10, "a fallow plot does not use water");

        world.Transfer(world.Backpack, farm.Inventory, "wheat_seeds", 1);
        world.Transfer(world.Backpack, farm.Inventory, "fertilizer", 1);
        SafeTick(world, 42);
        Require(Math.Abs(world.Farms.WaterLevel(farm) - 9.5f) < .001f, "half of the first liter should remain in use");
        Require(world.Transfer(world.Backpack, farm.Inventory, Bucket.AtFill(10), 1) == 0, "the liter in use still counts toward capacity");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var restored = loaded.Find(farm.Id)!;
        Require(Math.Abs(loaded.Farms.WaterLevel(restored) - 9.5f) < .001f, "water left in the opened liter survives a load");
        Work(loaded, data.DaySeconds / 2 - 42);
        Require(Math.Abs(loaded.Farms.Growth(restored) - data.DaySeconds / 2) < .01f && restored.Inventory[Water.Liquid] == 5,
            "a half day of growth consumes five liters");
        Require(loaded.Transfer(loaded.Backpack, restored.Inventory, Bucket.AtFill(10), 1) == 1
            && loaded.Backpack[Bucket.AtFill(5)] == 1 && restored.Inventory[Water.Liquid] == 10,
            "a partial refill should leave the other five liters in the bucket");
    }

    private static void CheckHaulerPoursABucketAtFourCorners(GameData data) {
        foreach (int liters in new[] { 0, 1 }) CheckFarmWateringTrip(data, liters);
    }

    private static void CheckFarmWateringTrip(GameData data, int liters) {
        var world = EmptyWorld(data);
        var farm = world.Add(ThingKind.Farm, new Vector2(20, 20));
        farm.FarmStage = FarmStage.Growing;
        var source = world.Add(ThingKind.Barrel, new Vector2(15, 18));
        source.Inventory.Add(Water.Liquid, 50);
        var rack = world.Add(ThingKind.Toolstation, new Vector2(14, 20));
        rack.Inventory.Add(Bucket.AtFill(liters), 1);
        var worker = AddWorker(world, new Vector2(15, 19));
        world.SetDuty(worker, farm, JobKind.Haul, true);
        var stops = new HashSet<Vector2>();
        bool walked = false;
        for (int i = 0; i < 700 && !(stops.Count == 4 && worker.Tool == Bucket.Item); i++) {
            SafeTick(world, .05f);
            if (worker.WaterAction == WorkerWaterAction.Pour && worker.WaterTarget == farm.Id) stops.Add(worker.Pos);
            walked |= worker.WaterAction == WorkerWaterAction.WalkFarm;
        }
        Require(stops.Count == 4 && walked, $"a farm hauler should walk between four pouring spots (saw {stops.Count})");
        Require(stops.All(at => Math.Abs(at.X - 20) is > 1 and < 3 && Math.Abs(at.Y - 20) is > 1 and < 3),
            "the stops should sit inside the four corners of the plot");
        Require(world.Farms.WaterLevel(farm) is > 9.8f and <= 10 && worker.Tool == Bucket.Item && source.Inventory[Water.Liquid] == 40 + liters,
            "the hauler should pour ten liters from one bucket and keep the empty bucket");
    }
    private static void CheckWateredGrowthAllocatesNothing(GameData data) {
        var (world, _, farm) = FarmBesideCompost(data);
        farm.FarmStage = FarmStage.Growing;
        farm.Inventory.Add(Water.Liquid, 10);
        AllocationChecks.Check("tick: watered farm plot", () => { world.Tick(1 / 60f); Fed(world); });
        Require(world.Farms.Growth(farm) > 10, "the measured ticks should advance growth");
    }
}
