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
        Test(CheckGroundWorkersKeepTheirSeatsWhenSomeoneJoinsLunch, data);
        Test(CheckFiringAWorkerDoesNotMakeOthersRetryBlockedFireSeats, data);
        Test(CheckWorkersCutOffFromTheirGroupDoNotSitOnOneAnother, data);
        Test(CheckTwoWorkersCannotClaimTheSameLastPatchOfGround, data);
        Test(CheckANewFireFillsSeatsReleasedByWorkersMovingOffTheGround, data);
        Test(CheckCampfireWithNoWoodBurnsWhileTheCrewLunchesAtIt, data);
    }

    private static void CheckCampfireWithNoWoodBurnsWhileTheCrewLunchesAtIt(GameData data) {
        var world = WorldAtLunch(data);
        var fire = world.Add(ThingKind.Campfire, Terrain.Spawn + Vector2.Right * 4);
        for (int i = 0; i < 3; i++) AddWorker(world, Terrain.Spawn);
        Require(fire.Burn == 0, "the campfire should start out unlit");
        Work(world, 15);
        Require(fire.Burn > 0 && world.Protected(fire.Pos + Vector2.Up * 3) && fire.Inventory["wood"] == 0,
            "a campfire with no wood should burn while the crew sits at it for lunch");
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

    private static void CheckGroundWorkersKeepTheirSeatsWhenSomeoneJoinsLunch(GameData data) {
        var world = WorldAtLunch(data);
        for (int i = 0; i < 3; i++) AddWorker(world, Terrain.Spawn);
        Work(world, 4);
        var positions = world.Workers.Select(w => w.Pos).ToArray();
        AddWorker(world, Terrain.Spawn + Vector2.Left * 5);
        for (int i = 0; i < 8; i++) {
            Work(world, .25f);
            for (int j = 0; j < positions.Length; j++) Require(world.Workers[j].Pos == positions[j]
                && world.Workers[j].WorkState == WorkerWorkState.Resting, "workers on the ground should keep their seats when someone joins");
        }
    }

    private static void CheckFiringAWorkerDoesNotMakeOthersRetryBlockedFireSeats(GameData data) {
        var world = WorldAtLunch(data);
        var at = Terrain.Spawn;
        EncloseLunchCrew(world, at);
        world.Add(ThingKind.Campfire, at + Vector2.Up * 8);
        var cutOff = AddWorker(world, at + Vector2.Up * 3);
        var other = AddWorker(world, at + Vector2.Down * 2);
        Work(world, 10);
        Require(cutOff.WorkState == WorkerWorkState.Resting && other.WorkState == WorkerWorkState.Resting,
            "both cut-off workers should be sitting on the ground after trying the fire");
        world.Fire(other);
        for (int i = 0; i < 8; i++) {
            Work(world, .25f);
            Require(cutOff.WorkState == WorkerWorkState.Resting, "the cut-off worker should keep resting while a fire seat frees");
        }
    }

    private static void CheckWorkersCutOffFromTheirGroupDoNotSitOnOneAnother(GameData data) {
        var world = WorldAtLunch(data);
        var at = Terrain.Spawn;
        for (int i = 0; i < 8; i++)
            world.Add(ThingKind.Bench, at + Vector2.FromAngle(i * Mathf.Tau / 8) * 1.05f);
        // The first step east is blocked even though the workers have some room in the other directions.
        world.Add(ThingKind.Bench, at + Vector2.Right * .61f);
        var first = AddWorker(world, at);
        var second = AddWorker(world, at);
        Work(world, 5);
        bool bothSeated = first.WorkState == WorkerWorkState.Resting && second.WorkState == WorkerWorkState.Resting;
        Require(!bothSeated || first.Pos.DistanceTo(second.Pos) >= .8f, "the two workers should never share a ground seat");
        Require(first.Pos.DistanceTo(second.Pos) > .35f, "a worker who has to stand should step away from the seated worker");
    }

    private static void CheckTwoWorkersCannotClaimTheSameLastPatchOfGround(GameData data) {
        var world = WorldAtLunch(data);
        var at = Terrain.Spawn;
        // Block every candidate group spot around the crew, apart from the small opening where they stand.
        for (int y = -10; y <= 10; y += 2)
            for (int x = -10; x <= 10; x += 2)
                if (x != 0 || y != 0) world.Add(ThingKind.Hut, at + new Vector2(x, y));
        AddWorker(world, at);
        AddWorker(world, at);
        Work(world, 3);
        Require(world.Workers.Count(w => w.WorkState == WorkerWorkState.Resting) == 1,
            "only one worker should sit when there is only one patch of ground with room");
    }

    private static void CheckANewFireFillsSeatsReleasedByWorkersMovingOffTheGround(GameData data) {
        var world = WorldAtLunch(data);
        var nearest = AddWorker(world, Terrain.Spawn);
        for (int i = 0; i < 2; i++) AddWorker(world, Terrain.Spawn);
        for (int i = 0; i < 5; i++) AddWorker(world, Terrain.Spawn + Vector2.Left * 8);
        Work(world, 5);
        var fire = world.Add(ThingKind.Campfire, nearest.Pos + Vector2.Right * 1.4f);
        Work(world, 12);
        Require(world.Workers.Count(w => w.Pos.DistanceTo(fire.Pos) < 1.6f
            && w.WorkState is WorkerWorkState.Resting or WorkerWorkState.Perching) == 6,
            "a new campfire should fill all six seats, including the spot freed by a ground worker moving to the fire");
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
