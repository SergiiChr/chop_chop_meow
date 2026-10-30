using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Simulation checks for worker animation state transitions.</summary>
public static class WorkerStateChecks {
    public static void Run(GameData data) {
        CheckGatherAndReassign(data);
        CheckNextKnownSpot(data);
        CheckStrikeResetsWorkState(data);
        CheckBlockedPath(data);
        CheckFurnacePumping(data);
        CheckSaveRoundTrip(data);
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

    private static void CheckNextKnownSpot(GameData data) {
        var world = EmptyWorld(data);
        var said = new System.Collections.Generic.List<string>();
        world.Notify += said.Add;
        var at = Terrain.Spawn;
        var post = world.Add("signpost", at + new Vector2(-3, 2));
        world.Add("chest", at + new Vector2(2, 2));
        var spent = world.Add("tree", at + new Vector2(1, 0), 1);
        var seen = world.Add("tree", at + new Vector2(6, 0), 2);
        var coal = world.Add("loose_coal", at + new Vector2(0, 3), 1);
        var moreCoal = world.Add("loose_coal", at + new Vector2(4, 4), 1);
        world.Player = at; world.Look();
        // Out of every player's sight, so these stay under black fog.
        world.Player = at + new Vector2(0, 40); world.Look();
        var unseen = world.Add("tree", at + new Vector2(3, 0), 5);
        world.Add("loose_coal", at + new Vector2(1, 4), 1);

        var worker = AddWorker(world, spent.Pos);
        worker.Tool = "steel_axe";
        world.Assign(worker, spent);
        for (int i = 0; i < 600 && worker.Assignment != seen.Id; i++) world.Tick(.1f);
        Require(worker.Assignment == seen.Id && worker.Duties.Count == 1 && worker.Duties[0].Thing == seen.Id && unseen.Remaining == 5,
                "a worker whose tree is felled should move on to the nearest tree a player has seen, not a closer one under black fog");
        Require(world.S.Remembered.ContainsKey(seen.Id) && !world.Sees(seen.Pos), "the next tree should be one known from memory, now out of sight");

        for (int i = 0; i < 600 && worker.Status.IndexOf("waiting at the trading post", StringComparison.Ordinal) < 0; i++) world.Tick(.1f);
        for (int i = 0; i < 100; i++) world.Tick(.1f);
        Require(seen.Remaining == 0 && worker.Assignment == seen.Id && worker.Pos.DistanceTo(post.Pos + Vector2.One) < 1.5f,
                "a worker who knows no other tree should wait at the trading post");
        Require(said.Count(m => m.Contains("knows no other tree", StringComparison.Ordinal)) == 1, "a worker who knows no other tree should say so once");

        var picker = AddWorker(world, coal.Pos);
        world.Assign(picker, coal);
        for (int i = 0; i < 20 && world.Find(coal.Id) != null; i++) world.Tick(.1f);
        Require(world.Find(coal.Id) == null && picker.Assignment == moreCoal.Id, "a worker who picks up the last of a loose coal should go to the next known one, not a closer one under black fog");
        for (int i = 0; i < 300 && world.Find(moreCoal.Id) != null; i++) world.Tick(.1f);
        for (int i = 0; i < 100; i++) world.Tick(.1f);
        Require(world.Find(moreCoal.Id) == null && picker.Duties.Count == 1 && picker.Duties[0].Kind == "loose_coal" && picker.Pos.DistanceTo(post.Pos + Vector2.One) < 1.5f,
                "a worker who picked up the last known loose coal should keep the job and wait at the trading post");

        // The look comes at the end of a tick, so the worker acts on it the tick after.
        world.Player = at; world.Tick(.1f); world.Tick(.1f);
        Require(worker.Assignment == unseen.Id, "once a player sees another tree, the waiting worker should go to it");

        var raker = AddWorker(world, at);
        var raked = world.Add("leaves", at + new Vector2(-2, -2), 0);
        var bush = world.Add("berries", at + new Vector2(-4, -2), 6);
        world.Tick(0);
        raker.Duties.Add(new Duty { Thing = raked.Id, Job = "gather" }); raker.Duties.Add(new Duty { Thing = bush.Id, Job = "gather" }); raker.Assignment = raked.Id;
        world.Tick(.1f); world.Tick(.1f);
        Require(raker.Assignment == bush.Id && raker.Duties.Count == 2, "a worker who knows no other raked patch should go on to their other job and keep the patch");

        var crafter = AddWorker(world, at);
        var bench = world.Add("bench", at + new Vector2(5, 5));
        world.Assign(crafter, bench);
        world.Tick(.1f); said.Clear();
        world.S.Things.Remove(bench); world.Tick(.1f);
        Require(crafter.Duties.Count == 0 && said.Contains($"{crafter.Name} without a job."), "a worker whose building is removed should be left without a job and say so");
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

        // Only the bellows operator pumps: extra crew with nothing to fetch never joins.
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
