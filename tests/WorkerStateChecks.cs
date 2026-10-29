using System;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>Simulation checks for worker animation state transitions.</summary>
public static class WorkerStateChecks {
    public static void Run(GameData data) {
        CheckGatherAndReassign(data);
        CheckStrikeResetsWorkState(data);
        CheckBlockedPath(data);
        CheckFurnacePumping(data);
        CheckSaveRoundTrip(data);
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException($"Worker state check failed: {message}");
    }

    private static World EmptyWorld(GameData data) => new(data, false);

    private static Worker AddWorker(World world, Vector2 position) {
        var worker = new Worker { Id = world.S.NextId++, Name = "State check worker", Pos = position };
        world.S.Workers.Add(worker);
        return worker;
    }

    private static void CheckGatherAndReassign(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add("tree", new Vector2(10, 10), 12);
        world.Add("chest", new Vector2(12, 10));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Gathering,
                "a worker at its resource should enter gathering state through World.Tick");

        world.Assign(worker, null);
        Require(worker.WorkState == WorkerWorkState.Idle,
                "reassigning a working worker to idle should clear its transient work state");
        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Idle,
                "an available worker should remain idle after its next simulation tick");
    }

    private static void CheckStrikeResetsWorkState(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add("tree", new Vector2(10, 10), 12);
        world.Add("chest", new Vector2(12, 10));
        var worker = AddWorker(world, tree.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);
        worker.WorkState = WorkerWorkState.Gathering;
        worker.WageDebt = worker.Wage;

        world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Idle,
                "a worker on strike should have no active work animation state");
    }

    private static void CheckBlockedPath(GameData data) {
        var world = EmptyWorld(data);
        // Off the map's edge: the tree is out of reach.
        var tree = world.Add("tree", Vector2.One * (Terrain.Size - 1), 12);
        world.Add("chest", Vector2.One * (Terrain.Size - 6));
        var worker = AddWorker(world, Vector2.One * (Terrain.Size - 8));
        worker.Tool = "steel_axe";
        world.Assign(worker, tree);

        for (int i = 0; i < 80 && worker.WorkState != WorkerWorkState.Blocked; i++) world.Tick(.25f);
        Require(worker.WorkState == WorkerWorkState.Blocked,
                "a worker with no valid route to its destination should enter blocked state through Walk");
    }

    private static void CheckFurnacePumping(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add("reinforced", new Vector2(20, 20));
        var bellowsWorker = AddWorker(world, furnace.Pos + World.BellowsStand);
        var secondWorker = AddWorker(world, furnace.Pos);
        world.Assign(bellowsWorker, furnace);
        world.Assign(secondWorker, furnace);

        world.Tick(.25f);
        Require(bellowsWorker.WorkState == WorkerWorkState.Pumping,
                "the assigned furnace bellows operator should enter pumping state through World.Tick");

        // Idle haulers with nothing to fetch used to pump too; extra crew must never join the operator.
        var thirdWorker = AddWorker(world, furnace.Pos);
        world.Assign(thirdWorker, furnace);
        for (int i = 0; i < 40; i++) {
            world.Tick(.25f);
            Require(world.S.Workers.Count(w => w.WorkState == WorkerWorkState.Pumping) <= 1,
                    "at most one worker should pump the bellows at a time");
        }
        Require(bellowsWorker.WorkState == WorkerWorkState.Pumping,
                "the first assigned worker should keep operating the bellows with extra crew assigned");
    }

    private static void CheckSaveRoundTrip(GameData data) {
        var world = EmptyWorld(data);
        var tree = world.Add("tree", new Vector2(10, 10), 12);
        world.Add("chest", new Vector2(12, 10));
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
        var loadedWorker = loaded.S.Workers[0];
        Require(loadedWorker.WorkState == WorkerWorkState.Idle,
                "a loaded worker should start with the default transient state");
        loaded.Tick(.25f);
        Require(loadedWorker.WorkState == WorkerWorkState.Gathering,
                "the next world tick should recompute a loaded worker's active state");
    }
}
