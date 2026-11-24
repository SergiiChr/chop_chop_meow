using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static WorkerSteps;

/// <summary>Checks of a worker's job list: the order jobs are done in, rounds, and what a worker does with no way to their job.</summary>
public static class WorkerJobChecks {
    public static void Run(GameData data) {
        Test(CheckWorkerKeepsToTheFirstJobWhileItCanBeDone, data);
        Test(CheckWorkerGoesBackToTheFirstJobAfterOneDeliveryOfTheNext, data);
        Test(CheckBellowsWorkerStopsWhenThereIsNothingToSmelt, data);
        Test(CheckAJobMovedUpComesFirst, data);
        Test(CheckACrafterLowerInTheListGivesWayAfterOneBatch, data);
        Test(CheckASaveKeepsTheJobOrderGroupsAndTool, data);
        Test(CheckWorkerOutOfPatchesMovesToTheirOtherJob, data);
        Test(CheckWorkerWhoseBuildingGoesSaysTheyHaveNoJob, data);
        Test(CheckWorkerWithoutRouteIsBlocked, data);
        Test(CheckSavedWorkerRestartsWithDefaultWorkState, data);
        Test(CheckWorkerWithTwoJobsTakesTurns, data);
        Test(CheckWorkerDetoursAroundLake, data);
    }

    private static void CheckWorkerKeepsToTheFirstJobWhileItCanBeDone(GameData data) {
        var world = EmptyWorld(data);
        var berries = world.Add(ThingKind.Berries, new Vector2(10, 10), 400);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 14), 400);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 12)));
        var picker = Staff(world, berries, JobKind.Gather);
        world.SetDuty(picker, leaves, JobKind.Gather, true);
        Work(world, 90);
        Require(chest.Inventory["berries"] >= 24 && chest.Inventory["leaves"] == 0,
            $"a worker should pick berries, their first job, for as long as there are some: {chest.Inventory["berries"]} berries and {chest.Inventory["leaves"]} leaves stored");
    }

    /// <summary>With no storage at all, the wood a crafter cuts goes into the table, and they saw it all before cutting more.</summary>
    private static void CheckWorkerGoesBackToTheFirstJobAfterOneDeliveryOfTheNext(GameData data) {
        var world = EmptyWorld(data);
        var bench = Bench(world, new Vector2(10, 10), "planks", "steel_chisel");
        var tree = world.Add(ThingKind.Tree, new Vector2(14, 10), 4000);
        var crafter = Staff(world, bench, JobKind.Craft);
        world.SetDuty(crafter, tree, JobKind.Gather, true);
        TakeEverything(world, world.Add(ThingKind.Toolstation, new Vector2(12, 12))).Inventory.Add("steel_axe", 1);
        Work(world, 200, () => bench.Inventory["planks"] >= 8);
        Require(bench.Inventory["planks"] >= 8, $"a crafter who also cuts wood should saw planks from the wood they brought: {bench.Inventory["planks"]} planks");
        Require(tree.Remaining == 3988, $"a crafter should go back to sawing after one load of wood, not cut {4000 - tree.Remaining}");
    }

    private static void CheckBellowsWorkerStopsWhenThereIsNothingToSmelt(GameData data) {
        var world = EmptyWorld(data);
        var forge = world.Add(ThingKind.Reinforced, new Vector2(20, 20));
        var berries = world.Add(ThingKind.Berries, new Vector2(20, 25), 400);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(24, 22)));
        chest.Inventory.Filter("berries", false, true); chest.Inventory.Filter("steel_bar", false, true);
        var pumper = Staff(world, forge, JobKind.Bellows);
        world.SetDuty(pumper, berries, JobKind.Gather, true);
        Work(world, 60, () => chest.Inventory["berries"] > 0);
        Require(chest.Inventory["berries"] > 0 && forge.Heat == 0, $"with nothing to smelt a bellows worker should pick berries, their next job: heat {forge.Heat}");
        forge.Inventory.Add("iron_bar", 2); forge.Inventory.Add("coal", 2); forge.Burner!.Add("coal", 1);
        Work(world, 120, () => forge.Inventory["steel_bar"] > 0);
        Require(forge.Inventory["steel_bar"] == 2, "a bellows worker should pump again once the furnace holds a batch, until the steel is made");
        float berriesBefore = chest.Inventory["berries"];
        Work(world, 60, () => chest.Inventory["berries"] > berriesBefore);
        Require(chest.Inventory["berries"] > berriesBefore, "a bellows worker should go back to picking berries once the batch is done");
    }

    private static void CheckAJobMovedUpComesFirst(GameData data) {
        var world = EmptyWorld(data);
        var berries = world.Add(ThingKind.Berries, new Vector2(10, 10), 400);
        var leaves = world.Add(ThingKind.Leaves, new Vector2(10, 14), 400);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 12)));
        var picker = Staff(world, berries, JobKind.Gather);
        world.SetDuty(picker, leaves, JobKind.Gather, true);
        world.MoveJob(picker, new JobSpot(1), new JobSpot(0));
        Work(world, 90);
        Require(chest.Inventory["leaves"] >= 24 && chest.Inventory["berries"] == 0,
            $"a job dragged to the top should be the one a worker does: {chest.Inventory["leaves"]} leaves and {chest.Inventory["berries"]} berries stored");
    }

    /// <summary>Haul at the first table has nothing to do until wood reaches storage; the crafter at the second finishes the batch under way, then hauls.</summary>
    private static void CheckACrafterLowerInTheListGivesWayAfterOneBatch(GameData data) {
        var world = EmptyWorld(data);
        var first = Bench(world, new Vector2(10, 10), "planks");
        var second = Bench(world, new Vector2(10, 16), "planks", "steel_chisel");
        second.Inventory.Add("wood", 5);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(13, 13)));
        var worker = Staff(world, first, JobKind.Haul);
        world.SetDuty(worker, second, JobKind.Craft, true);
        Work(world, 10, () => second.Processing);
        Require(second.Processing, "with nothing to haul, the worker should work the second table");
        chest.Inventory.Add("wood", 40);
        FirstCarry(world, worker);
        Require(worker.Bag["wood"] > 0 && second.Inventory["planks"] == 4 && !second.Processing,
            $"the worker should finish the batch under way, one round, then haul for the first table: {second.Inventory["planks"]} planks made");
    }

    private static void CheckASaveKeepsTheJobOrderGroupsAndTool(GameData data) {
        var world = EmptyWorld(data);
        var a = Bench(world, new Vector2(10, 10), "planks");
        var b = Bench(world, new Vector2(10, 16), "planks");
        var worker = Staff(world, a, JobKind.Craft, JobKind.Haul);
        world.SetDuty(worker, b, JobKind.Haul, true);
        GroupHauls(world, worker, b, a);
        worker.Tool = "steel_axe";
        string saved = JobsText(worker);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var back = loaded.Workers.Single(w => w.Id == worker.Id);
        Require(JobsText(back) == saved && back.Tool == "steel_axe", $"a save should keep a worker's job order, haul groups and tool: {JobsText(back)} with \"{back.Tool}\"");
    }

    private static void CheckWorkerOutOfPatchesMovesToTheirOtherJob(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn;
        TakeEverything(world, world.Add(ThingKind.Chest, at + new Vector2(2, 2)));
        world.Player = at; world.Look();
        var raker = AddWorker(world, at);
        var raked = world.Add(ThingKind.Leaves, at + new Vector2(-2, -2), 0);
        var bush = world.Add(ThingKind.Berries, at + new Vector2(-4, -2), 6);
        world.Tick(0);
        raker.Jobs.Add(new Duty { Thing = raked.Id, Job = JobKind.Gather }); raker.Jobs.Add(new Duty { Thing = bush.Id, Job = JobKind.Gather }); raker.Assignment = raked.Id;
        // A worker with nothing to do reads their list again a quarter of a second later.
        for (int i = 0; i < 4; i++) world.Tick(.1f);
        Require(raker.Assignment == bush.Id && raker.Jobs.Count == 2, "a worker who knows no other raked patch should go on to their other job and keep the patch");
    }

    private static void CheckWorkerWhoseBuildingGoesSaysTheyHaveNoJob(GameData data) {
        var world = EmptyWorld(data);
        var said = new System.Collections.Generic.List<string>();
        world.Notify += said.Add;
        var at = Terrain.Spawn;
        var crafter = AddWorker(world, at);
        var bench = world.Add(ThingKind.Bench, at + new Vector2(5, 5));
        world.Assign(crafter, bench);
        world.Tick(.1f); said.Clear();
        RemoveThings(world, t => t == bench); world.Tick(.1f);
        Require(crafter.Jobs.Count == 0 && said.Contains($"{crafter.Name} without a job."), "a worker whose building is removed should be left without a job and say so");
    }

    private static void CheckWorkerWithoutRouteIsBlocked(GameData data) {
        var world = EmptyWorld(data);
        // Off the map's edge: the tree is out of reach.
        var tree = world.Add(ThingKind.Tree, Vector2.One * (Terrain.Size - 1), 12);
        TakeEverything(world, world.Add(ThingKind.Chest, Vector2.One * (Terrain.Size - 6)));
        var worker = AddWorker(world, Vector2.One * (Terrain.Size - 8));
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        for (int i = 0; i < 80 && worker.WorkState != WorkerWorkState.Blocked; i++) world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Blocked,
                "a worker with no valid route to its destination should enter blocked state through Walk");
    }

    private static void CheckSavedWorkerRestartsWithDefaultWorkState(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 12);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(12, 10)));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Gathering,
                "save fixture should contain a worker actively gathering");

        string save = world.Serialize();
        Require(!save.Contains("WorkState", StringComparison.Ordinal),
                "transient worker work state should be excluded from saved data");

        var loaded = EmptyWorld(data);
        loaded.Deserialize(save);
        var loadedWorker = loaded.Workers[0];
        Require(loadedWorker.WorkState == WorkerWorkState.Idle,
                "a loaded worker should start with the default transient state");
        loaded.Tick(.25f);
        Require(loadedWorker.WorkState == WorkerWorkState.Gathering,
                "the next world tick should recompute a loaded worker's active state");
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
}
