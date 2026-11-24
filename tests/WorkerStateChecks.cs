using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

/// <summary>Simulation checks for worker animation state transitions.</summary>
public static class WorkerStateChecks {
    public static void Run(GameData data) {
        CheckWaitingWorkerTakesAToolOnceOneIsHung(data);
        CheckWorkerOutOfPatchesMovesToTheirOtherJob(data);
        CheckWorkerWhoseBuildingGoesSaysTheyHaveNoJob(data);
        CheckStrikeResetsWorkState(data);
        CheckWorkerWithoutRouteIsBlocked(data);
        CheckSavedWorkerRestartsWithDefaultWorkState(data);
    }

    /// <summary>A worker waiting for a tool takes one as soon as it hangs in a tool station that gives it out, and not while its filter keeps it in.</summary>
    private static void CheckWaitingWorkerTakesAToolOnceOneIsHung(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn;
        TakeEverything(world, world.Add(ThingKind.Chest, at + new Vector2(2, 2)));
        var rack = TakeEverything(world, world.Add(ThingKind.Toolstation, at + new Vector2(-2, 2)));
        var tree = world.Add(ThingKind.Tree, at + new Vector2(1, 0), 40);
        world.Player = at; world.Look();
        var cutter = AddWorker(world, tree.Pos);
        world.Assign(cutter, tree);
        for (int i = 0; i < 20; i++) world.Tick(.1f);
        Require(cutter.Missing == "axe", "a woodcutter with no axe anywhere should wait for one");
        // An output filter naming only the pickaxe keeps the axe in.
        world.SetFilter(rack, "steel_pick", true, true);
        Give(world, "steel_axe", 2); world.Transfer(world.Backpack, rack.Inventory, "steel_axe", 1);
        for (int i = 0; i < 20; i++) world.Tick(.1f);
        Require(cutter.Tool == "" && rack.Inventory["steel_axe"] == 1, "a tool station whose filter keeps its axe in should not give it out");
        world.SetFilter(rack, Inventory.Everything, true, true);
        for (int i = 0; i < 100 && cutter.Tool == ""; i++) world.Tick(.1f);
        Require(cutter.Tool == "steel_axe" && rack.Inventory["steel_axe"] == 0, "a waiting woodcutter should take the axe once the tool station gives it out");
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

    private static void CheckStrikeResetsWorkState(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add(ThingKind.Tree, new Vector2(10, 10), 12);
        TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(12, 10)));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        worker.WorkState = WorkerWorkState.Gathering;
        worker.WageDebt = worker.Wage;

        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Idle,
                "a worker on strike should have no active work animation state");
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
}
