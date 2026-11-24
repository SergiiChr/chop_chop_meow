using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;
using System;

/// <summary>Checks of where a worker with no job that can be done waits, and the campfire they sit at.</summary>
internal static class IdleChecks {
    public static void Run(GameData data) {
        Test(CheckWorkerWithNothingToDoSitsAtTheNearestCampfire, data);
        Test(CheckIdleWorkersPastSixSitOnTheGroundByTheFire, data);
        Test(CheckCampfireBurnsWithNoWoodWhileWorkersSitAndGoesOutAfter, data);
        Test(CheckCampfireBurnsItsWoodAsUsualWhileWorkersSit, data);
        Test(CheckWorkerWithNothingToDoWaitsAtTheTradingPost, data);
    }

    private static bool Seated(Worker w) => w.WorkState is WorkerWorkState.Resting or WorkerWorkState.Perching;

    private static void CheckWorkerWithNothingToDoSitsAtTheNearestCampfire(GameData data) {
        var world = FundedWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks", "steel_chisel");
        world.Add(ThingKind.Signpost, new Vector2(24, 10));
        var near = world.Add(ThingKind.Campfire, new Vector2(10, 18));
        world.Add(ThingKind.Campfire, new Vector2(30, 30));
        var crafter = Staff(world, bench, JobKind.Craft);
        Work(world, 60, () => Seated(crafter));
        Require(Seated(crafter) && crafter.Pos.DistanceTo(near.Pos) < 1.6f,
            $"a worker whose jobs cannot be done should sit at the nearest campfire, not {crafter.Pos.DistanceTo(near.Pos):0.0} m from it");
        Require(crafter.Status.Contains("wood", System.StringComparison.OrdinalIgnoreCase), $"a seated idle worker's status should say what their first job lacks, not \"{crafter.Status}\"");
        Require(crafter.Assignment == bench.Id, "a worker waiting at the fire should keep the job's place");
    }

    private static void CheckIdleWorkersPastSixSitOnTheGroundByTheFire(GameData data) {
        var world = FundedWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(30, 30));
        for (int i = 0; i < 7; i++) AddWorker(world, new Vector2(12 + i, 12));
        Work(world, 60, () => world.Workers.All(Seated));
        Require(world.Workers.All(Seated), "every idle worker should sit down");
        int atFire = world.Workers.Count(w => w.Pos.DistanceTo(fire.Pos) < 1.6f);
        Require(atFire == 6, $"six idle workers should take the fire's seats, not {atFire}");
        var ground = world.Workers.Single(w => w.Pos.DistanceTo(fire.Pos) >= 1.6f);
        Require(ground.Pos.DistanceTo(fire.Pos) < 12, $"the seventh should sit on the ground near the fire, not {ground.Pos.DistanceTo(fire.Pos):0} m from it");
    }

    private static void CheckCampfireBurnsWithNoWoodWhileWorkersSitAndGoesOutAfter(GameData data) {
        var world = FundedWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var bench = Bench(world, new Vector2(10, 10), "planks", "steel_chisel");
        var crafter = Staff(world, bench, JobKind.Craft);
        Require(fire.Burn == 0 && fire.Inventory["wood"] == 0, "the campfire should start out with no wood and unlit");
        Work(world, 60, () => Seated(crafter));
        Work(world, 1);
        Require(fire.Burn > 0 && world.Protected(fire.Pos + Vector2.Right * 3), "a campfire with an idle worker sitting at it should burn and keep wolves off");
        bench.Inventory.Add("wood", 10);
        Work(world, 30, () => !Seated(crafter) && fire.Burn == 0);
        Require(!Seated(crafter), "the worker should get up once their job can be done");
        Require(fire.Burn == 0 && !world.Protected(fire.Pos + Vector2.Right * 3), "with nobody left sitting at it, a fire with no wood should go out");
    }

    private static void CheckCampfireBurnsItsWoodAsUsualWhileWorkersSit(GameData data) {
        var world = FundedWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        fire.Burn = 100; fire.Inventory.Add("wood", 2);
        var worker = AddWorker(world, new Vector2(16, 20));
        Work(world, 30, () => Seated(worker));
        float burn = fire.Burn;
        Work(world, 60);
        Require(Seated(worker), "the idle worker should still be sitting at the fire");
        Require(fire.Burn < burn - 50, $"a fire with wood burning should burn down as usual while workers sit at it: {fire.Burn:0} of {burn:0} s left");
        Work(world, burn);
        Require(fire.Inventory["wood"] == 1, $"a fire running low should take a log from its slot while workers sit at it, not leave {fire.Inventory["wood"]}");
    }

    private static void CheckWorkerWithNothingToDoWaitsAtTheTradingPost(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks", "steel_chisel");
        var post = world.Add(ThingKind.Signpost, new Vector2(24, 10));
        var crafter = Staff(world, bench, JobKind.Craft);
        Work(world, 60, () => crafter.Pos.DistanceTo(post.Pos) < 3);
        Require(crafter.Pos.DistanceTo(post.Pos) < 3, $"a worker whose jobs cannot be done should wait at the trading post, not {crafter.Pos.DistanceTo(post.Pos):0} m from it");
        Require(crafter.Status.Contains("wood", System.StringComparison.OrdinalIgnoreCase), $"a waiting worker's status should say what their first job lacks, not \"{crafter.Status}\"");
    }
}
