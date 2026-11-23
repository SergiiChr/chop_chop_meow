using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static LunchSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

internal static class LunchChecks {
    public static void Run(GameData data) {
        Test(CheckCrewLunchesAtTheNearestFireAndGoesBackToWork, data);
        Test(CheckLunchUsesBothFiresBeforeWorkersSitInGroupsOnTheGround, data);
        Test(CheckWorkerUsesTheNextFireWhenTheNearestIsUnreachable, data);
        Test(CheckWorkerOnTheGroundTakesAFireSeatOthersCannotReach, data);
    }

    private static void CheckCrewLunchesAtTheNearestFireAndGoesBackToWork(GameData data) {
        var (world, tree, _, near, forge, cutter, pumper, hauler) = StartDayCrew(data);
        float At(float hour) => (hour - 6) / 24 * data.DaySeconds;
        RunUntil(world, At(data.LunchHour) - 5);
        forge.Heat = 100;
        SafeTick(world, 25);
        Require(world.Lunch, "the clock should be at lunch hour");
        Require(world.Workers.All(w => (w.WorkState is WorkerWorkState.Resting or WorkerWorkState.Perching) && w.Pos.DistanceTo(near.Pos) < 2),
            "at lunch the whole crew should sit around the campfire");
        Require(cutter.Assignment == tree.Id, "lunch should keep the cutter's assignment");
        Require(pumper.Assignment == forge.Id, "lunch should keep the pumper's assignment");
        Require(hauler.Assignment == forge.Id, "lunch should keep the hauler's assignment");
        Require(forge.Heat < World.SteelHeat, "with the crew at lunch the forge should cool below the clear bar");
        SafeTick(world, At(data.LunchHour + 1) - world.Time + 10);
        // Both forge workers hold the bellows and the haul job: whichever gets to the bellows first pumps.
        Require(cutter.WorkState == WorkerWorkState.Gathering && new[] { pumper, hauler }.Count(w => w.WorkState == WorkerWorkState.Pumping) == 1,
                "after lunch workers should walk back to their jobs");
    }

    private static void CheckWorkerUsesTheNextFireWhenTheNearestIsUnreachable(GameData data) {
        var world = WorldAtLunch(data);
        var at = Terrain.Spawn;
        EncloseLunchCrew(world, at);
        world.Add(ThingKind.Campfire, at + Vector2.Up * 6.6f);
        var reachable = world.Add(ThingKind.Campfire, at + Vector2.Down * 1.5f);
        var worker = AddWorker(world, at + Vector2.Up * 3);
        Work(world, 15);
        Require(worker.Pos.DistanceTo(reachable.Pos) < 1.6f && worker.WorkState is WorkerWorkState.Resting or WorkerWorkState.Perching,
            "a worker whose nearer fire is across a wall should sit at the reachable one");
    }

    private static void CheckWorkerOnTheGroundTakesAFireSeatOthersCannotReach(GameData data) {
        var world = WorldAtLunch(data);
        var at = Terrain.Spawn;
        EncloseLunchCrew(world, at);
        var fire = world.Add(ThingKind.Campfire, at + Vector2.Up * 8);
        for (int i = 0; i < 6; i++) AddWorker(world, at + Vector2.Up * 3);
        var outside = AddWorker(world, fire.Pos + Vector2.Up * 7);
        Work(world, 15);
        Require(outside.Pos.DistanceTo(fire.Pos) < 1.6f && outside.WorkState is WorkerWorkState.Resting or WorkerWorkState.Perching,
            "a worker on the ground should take a fire seat released by a worker who cannot reach it");
    }

    private static void CheckLunchUsesBothFiresBeforeWorkersSitInGroupsOnTheGround(GameData data) {
        var world = WorldAtLunch(data);
        var at = Terrain.Spawn;
        var near = world.Add(ThingKind.Campfire, at);
        var far = world.Add(ThingKind.Campfire, at + Vector2.Right * 9);
        var secondCrew = new Worker[6];
        for (int i = 0; i < 9; i++) AddWorker(world, near.Pos + Vector2.Up * 2);
        for (int i = 0; i < secondCrew.Length; i++) secondCrew[i] = AddWorker(world, far.Pos + Vector2.Up * 2);
        Work(world, 15);
        Require(secondCrew.All(w => w.Pos.DistanceTo(far.Pos) < 1.6f),
            "workers starting by the second fire should stay by that fire");
        Require(world.Workers.Count(w => w.Pos.DistanceTo(near.Pos) < 1.6f) == 6,
            "a fire should fit six workers");
        var onGround = world.Workers.Where(w => w.Pos.DistanceTo(near.Pos) > 1.6f && w.Pos.DistanceTo(far.Pos) > 1.6f).ToArray();
        Require(onGround.Length == 3 && onGround.All(w => w.WorkState == WorkerWorkState.Resting),
            "the three workers left over should sit on the ground");
        var center = (onGround[0].Pos + onGround[1].Pos + onGround[2].Pos) / 3;
        foreach (var a in onGround) {
            Require(ActorAnimation.WorkerAction(a, null, false, data) == ActorAction.Sit,
                "a worker seated on the ground should sit cross-legged");
            Require(Vector2.FromAngle(a.LunchHeading).Dot(a.Pos.DirectionTo(center)) > .98f,
                "each worker on the ground should face the center of their group");
            foreach (var b in onGround)
                if (a != b) Require(a.Pos.DistanceTo(b.Pos) is > .8f and < 1.9f, "ground seats should form a spaced triangle");
        }
        var perched = secondCrew.First(w => w.WorkState == WorkerWorkState.Perching);
        Require(ActorAnimation.WorkerAction(perched, null, false, data) == ActorAction.Perch,
            "a worker seated on a rock should perch");
        perched.Bag.Add("wood", 2);
        Require(ActorAnimation.WorkerAction(perched, null, true, data) == ActorAction.Carry,
            "a worker still walking to lunch with a load should carry it");
        AllocationChecks.Check("tick: seated lunch groups", () => { world.Tick(1 / 60f); Fed(world); });
    }
}
