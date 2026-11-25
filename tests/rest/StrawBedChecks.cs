using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;

public static class StrawBedChecks {
    public static void Run(GameData data) {
        Test(CheckTakingApartAFreshBedGivesBackExactlyItsCost, data);
        Test(CheckAnHourOldBedReturnsNothing, data);
        Test(CheckABedDisappearsAfterThreeDaysEvenWithoutUse, data);
        Test(CheckAWolfCanWakeAStrawBedSleeper, data);
        Test(CheckLyingDownCancelsAnUnfinishedSwing, data);
        Test(CheckSleepingOnStrawNeedsThePlayerBesideTheBed, data);
        Test(CheckSoloStrawBedSleeperHasTimeToLieDownBeforeDawn, data);
        Test(CheckANearbyWolfKeepsTimeAtNormalSpeedWithoutStoppingHealing, data);
        Test(CheckAWolfComingIntoSightRestoresNormalSpeed, data);
        Test(CheckOnlyTheSleeperNearTheWolfHearsIt, data);
        Test(CheckMovementWaitsForTheStandingHandoff, data);
        Test(CheckCabinAndStrawBedSleepTogetherSpeedUpTime, data);
        Test(CheckBootsStayPlantedAndClearTheMat);
        Test(CheckWeightPassesSmoothlyFromThePalmToTheForearm);
        Test(CheckTheBurningTorchStaysVisibleBesideTheSleeper);
    }

    private static void CheckTakingApartAFreshBedGivesBackExactlyItsCost(GameData data) {
        var world = EmptyWorld(data);
        var bed = StrawBedSteps.Build(world);
        Require(world.Backpack["leaves"] == 0, "building a straw bed spends all twenty leaves");
        Require(world.CanStand(bed.Pos), "the low mat should not block walking");
        world.Tick(data.HourSeconds - 1);
        Require(world.Construction.Dismantle(bed) && world.Find(bed.Id) == null, "taking down a fresh bed should remove it");
        Require(world.Backpack["grass"] == 0 && world.Backpack["leaves"] == 20, "a bed less than an hour old returns only twenty leaves");
    }

    private static void CheckAnHourOldBedReturnsNothing(GameData data) {
        var world = EmptyWorld(data);
        var bed = StrawBedSteps.Build(world);
        world.Tick(data.HourSeconds);
        Require(world.Construction.Dismantle(bed) && world.Find(bed.Id) == null, "an hour-old bed can still come down");
        Require(world.Backpack["grass"] == 0 && world.Backpack["leaves"] == 0, "the refund window closes at one hour");
    }

    private static void CheckABedDisappearsAfterThreeDaysEvenWithoutUse(GameData data) {
        var world = EmptyWorld(data);
        world.Tick(data.HourSeconds);
        float built = world.Time;
        int id = StrawBedSteps.Build(world).Id;
        RunUntil(world, built + data.DaySeconds * 2);
        var loaded = new World(data, false);
        loaded.Deserialize(world.Serialize());
        RunUntil(loaded, built + data.DaySeconds * 3 - 1);
        Require(loaded.Find(id) != null, "the saved bed should still be there one second before three full days");
        loaded.Tick(1);
        Require(loaded.Find(id) == null, "an unused straw bed should fall apart after exactly three days, including time before saving");
        Require(loaded.Backpack["grass"] == 0 && loaded.Backpack["leaves"] == 0, "wearing out should not refund any materials");
    }

    private static void CheckAWolfCanWakeAStrawBedSleeper(GameData data) {
        var (world, _, _) = Hosted(data);
        var bed = StrawBedSteps.Build(world);
        RunUntilNight(world);
        world.Sleep.Start(bed);
        Require(world.Me.SleepingAt == bed.Id, "the host should start sleeping on the outdoor bed while the guest is awake");
        var wolf = PlaceWolf(world, new Wolf { Pos = bed.Pos + Vector2.Right, Target = world.Me.Id });
        WolfSteps.SnapTick(world, .8f);
        Require(world.Me.SleepingAt == 0 && world.Health < 100 && wolf.Target == world.Me.Id,
            "a wolf can bite a straw bed sleeper and wake them");
    }

    private static void CheckLyingDownCancelsAnUnfinishedSwing(GameData data) {
        var world = WolfSteps.AxeNight(data);
        world.Player = Terrain.Spawn;
        var bed = StrawBedSteps.Build(world);
        var wolf = PlaceWolf(world, new Wolf {
            Pos = bed.Pos + Vector2.Right * 1.1f, Heading = Mathf.Pi, Howl = 5, HowlLength = 5
        });
        world.Attack(Vector2.Right);
        world.Sleep.Start(bed);
        world.Tick(.5f);
        Require(world.Sleep.Place == bed.Id && wolf.Health == data.WolfHealth,
            "A swing started before lying down must not hit anything from the bed.");
        world.Sleep.Wake();
        world.Tick(.2f);
        world.Attack(Vector2.Right);
        world.Tick(.5f);
        Require(wolf.Health < data.WolfHealth, "The same swing while awake should reach the wolf from here.");
    }

    private static void CheckSleepingOnStrawNeedsThePlayerBesideTheBed(GameData data) {
        var (world, _, _) = Hosted(data);
        var bed = StrawBedSteps.Build(world);
        world.Player = bed.Pos + Vector2.Right * 6;
        var far = world.Player;
        world.Sleep.Start(bed);
        Require(world.Me.SleepingAt == 0 && world.Player == far, "Sleep should not jump the player across a clearing.");
        world.Player = bed.Pos + Vector2.Right;
        world.Sleep.Start(bed);
        Require(world.Me.SleepingAt == bed.Id, "a player just beside the mat should be able to lie down.");
    }

    private static void CheckSoloStrawBedSleeperHasTimeToLieDownBeforeDawn(GameData data) {
        var world = EmptyWorld(data);
        var bed = StrawBedSteps.Build(world);
        RunUntilPhase(world, .72f);
        RemoveWolves(world, _ => true);
        ShutDens(world);
        SetNeeds(world, 100, 100, 60);
        float time = world.Time;
        int day = world.Day;
        world.Sleep.Start(bed);
        world.Tick(.5f);
        Require(world.Me.SleepingAt == bed.Id && world.Time == time + .5f && world.Health > 60,
            "a solo sleeper should start healing with time to lie down before the clock speeds up.");
        world.Tick(StrawBed.LieDownSeconds - .5f);
        Require(world.Day == day && world.Me.SleepingAt == bed.Id, "dawn should leave time for the last frame to reach clients");
        world.Tick(.25f);
        Require(world.Day == day && world.Time > time + StrawBed.LieDownSeconds + .25f,
            "after lying down the clock should run faster, with intermediate frames still shown");
        UntilAllAwake(world);
        Require(world.Day == day + 1 && world.Me.SleepingAt == 0, "the clock should run faster to dawn once the sleeper lies down.");
    }

    private static void CheckANearbyWolfKeepsTimeAtNormalSpeedWithoutStoppingHealing(GameData data) {
        foreach (bool lit in new[] { false, true }) {
            var world = NightWorld(data);
            world.Player = Terrain.Spawn;
            var bed = StrawBedSteps.Build(world);
            RemoveWolves(world, _ => true);
            if (lit) world.Add(ThingKind.Campfire, bed.Pos).Burn = 90;
            PlaceWolf(world, new Wolf { Pos = bed.Pos + Vector2.Right * (data.SightRadius - .5f), Howl = 10 });
            SetNeeds(world, 100, 100, 60);
            var messages = new List<string>();
            world.Notify += messages.Add;
            float time = world.Time;
            world.Sleep.Start(bed);
            world.Tick(3);
            world.Tick(.25f);
            Require(world.Time == time + 3.25f && world.Sleep.Place == bed.Id,
                "a wolf in sight should keep a straw-bed sleeper at normal speed, even by firelight");
            Require(Mathf.IsEqualApprox(world.Health, 60.39f), "the sleeper should keep healing at the normal rate");
            Require(messages.FindAll(message => message == "You hear something").Count == 1,
                "a nearby wolf should warn the sleeper once, not on every tick");
        }
    }

    private static void CheckAWolfComingIntoSightRestoresNormalSpeed(GameData data) {
        var world = NightWorld(data);
        world.Player = Terrain.Spawn;
        var bed = StrawBedSteps.Build(world);
        RemoveWolves(world, _ => true);
        SetNeeds(world, 100, 100, 60);
        world.Sleep.Start(bed);
        var wolf = PlaceWolf(world, new Wolf { Pos = bed.Pos + Vector2.Left * (data.SightRadius + 7) });
        float time = world.Time;
        int frames = 0;
        while (!world.Sees(wolf.Pos) && frames++ < 240) world.Tick(1f / 60);
        Require(world.Time > time + frames / 60f && world.Time < time + 9 && world.Sees(wolf.Pos),
            "time should run faster until the wolf comes into sight, then return to normal speed while it is still far away");
        Require(world.Sleep.Place == bed.Id && world.Health > 60, "a wolf interrupting the speed-up should leave the player asleep and healing");
    }

    private static void CheckOnlyTheSleeperNearTheWolfHearsIt(GameData data) {
        var (world, guest, heard) = Hosted(data);
        var bed = StrawBedSteps.Build(world);
        var hut = world.Add(ThingKind.Hut, Terrain.Point(135, 0));
        StepInside(world, hut, guest);
        RunUntilNight(world);
        ShutDens(world);
        SetNeeds(world, 100, 100, 60); SetNeeds(guest, 100, 100, 60);
        world.Sleep.Start(bed);
        world.Tick(2.25f);
        var wolf = PatrolWolf(world, new Wolf { Pos = guest.Pos + Vector2.Right * 7, Stagger = 1 });
        var hostMessages = new List<string>();
        world.Notify += hostMessages.Add;
        float time = world.Time;
        world.As(guest, () => world.Sleep.Start(hut));
        world.Tick(.25f);
        Require(world.Sleep.Place == bed.Id && guest.SleepingAt == hut.Id && world.Time == time + .25f,
            "one sleeper hearing a wolf should hold the shared clock at normal speed without waking either sleeper");
        Require(heard.Contains("You hear something") && !hostMessages.Contains("You hear something"),
            "only the sleeper within sight radius of the wolf should see its warning");
        int day = world.Day;
        RemoveWolves(world, w => w == wolf);
        UntilAllAwake(world);
        Require(world.Day == day + 1 && world.Sleep.Place == 0 && guest.SleepingAt == 0,
            "once the wolf is gone, time should speed up to dawn for the two players who stayed asleep");
    }

    private static void CheckMovementWaitsForTheStandingHandoff(GameData data) {
        var (world, _, _) = Hosted(data);
        var bed = StrawBedSteps.Build(world, Mathf.Pi / 2);
        var rig = new ActorRig(ActorSkin.Player);
        var view = new StrawBedView();
        var actor = new ActorState();
        try {
            world.Sleep.Start(bed);
            Require(view.Show(rig, actor, world.Me, bed, StrawBed.LieDownSeconds), "a sleeper should lie down on the mat");
            world.Sleep.Wake();
            Require(view.Rising(world.Me), "movement must wait as soon as the sleeper wakes");
            float stride = 1f / 60;
            int frames = Mathf.CeilToInt(StrawBed.LieDownSeconds / stride);
            for (int frame = 1; frame < frames - 1; frame++)
                Require(view.Show(rig, actor, world.Me, null, stride) && view.Rising(world.Me), "movement must wait while either boot is still getting up");
            Require(!view.Show(rig, actor, world.Me, null, 3 * stride) && !view.Rising(world.Me),
                "walking should be available once the standing pose takes over");
            Require(Mathf.IsEqualApprox(actor.Yaw, -bed.Rotation), "the standing pose should take over facing the same way as the rise");
        } finally { rig.Root.Free(); }
    }

    private static void CheckCabinAndStrawBedSleepTogetherSpeedUpTime(GameData data) {
        var (world, guest, _) = Hosted(data);
        var bed = StrawBedSteps.Build(world);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -5));
        StepInside(world, hut, guest);
        world.As(guest, world.Doorways.TurnKey);
        RunUntilPhase(world, data.DuskStarts + .02f);
        RemoveWolves(world, _ => true);
        ShutDens(world);
        SetNeeds(world, 100, 100, 60); SetNeeds(guest, 100, 100, 60);
        int day = world.Day;
        world.Sleep.Start(bed);
        world.Tick(10);
        Require(world.Day == day && world.Me.SleepingAt == bed.Id && Math.Abs(world.Health - 61.2f) < .02f,
            "a straw bed sleeper rests and heals while the guest stays awake");
        world.As(guest, () => world.Sleep.Start(hut));
        UntilAllAwake(world);
        Require(world.Day == day + 1 && world.Me.SleepingAt == 0 && guest.SleepingAt == 0,
            "sleeping at different kinds of bed should take both players to the next dawn");
    }

    private static void CheckBootsStayPlantedAndClearTheMat() {
        var rig = new ActorRig(ActorSkin.Player);
        var mat = new Thing { Kind = ThingKind.StrawBed, Pos = new Vector2(4, -2), Rotation = Mathf.Pi / 2 };
        try {
            StrawBedView.Draw(rig, mat, 0);
            var left = Sole(true); var right = Sole(false);
            int frames = Mathf.CeilToInt(StrawBed.LieDownSeconds * 60);
            for (int frame = 0; frame <= frames; frame++) {
                float down = (float)frame / frames;
                StrawBedView.Draw(rig, mat, down);
                Check(Sole(true), left, down, .10f); Check(Sole(false), right, down, .19f);
            }
        } finally { rig.Root.Free(); }

        (Vector3 Toe, Vector3 Heel) Sole(bool left) {
            var (toe, heel) = rig.BootSole(left);
            return (rig.Root.Transform * toe, rig.Root.Transform * heel);
        }

        static void Check((Vector3 Toe, Vector3 Heel) actual, (Vector3 Toe, Vector3 Heel) start, float down, float clearance) {
            const float tolerance = .008f;
            float sole = Math.Min(actual.Toe.Y, actual.Heel.Y) - StrawBedModel.Top;
            Require(actual.Toe.IsFinite() && actual.Heel.IsFinite() && sole >= -tolerance,
                $"boot must not go through the straw at phase {down:F3}, got {actual}");
            Require(sole < clearance, $"the leg should fold close to the mat rather than kicking upward, got {sole:F3} m at {down:F3}");
            if (down <= StrawBedView.BothBootsOnUntil) {
                Require(Math.Abs(actual.Toe.Y - StrawBedModel.Top) < tolerance && Math.Abs(actual.Heel.Y - StrawBedModel.Top) < tolerance,
                    $"both boots should stand flat while bearing weight at phase {down:F3}, got {actual}");
                Require(actual.Toe.DistanceTo(start.Toe) < tolerance && actual.Heel.DistanceTo(start.Heel) < tolerance,
                    $"both boots should stay in place while bearing weight at phase {down:F3}, got {actual} from {start}");
            }
        }
    }

    private static void CheckWeightPassesSmoothlyFromThePalmToTheForearm() {
        var rig = new ActorRig(ActorSkin.Player);
        var mat = new Thing { Kind = ThingKind.StrawBed, Rotation = Mathf.Pi };
        try {
            StrawBedView.Draw(rig, mat, StrawBedView.HandOn);
            var palm = Palm(true);
            StrawBedView.Draw(rig, mat, StrawBedView.SecondHandOn);
            var secondPalm = Palm(false);
            StrawBedView.Draw(rig, mat, StrawBedView.ForearmOn);
            var brace = Elbow();
            StrawBedView.Draw(rig, mat, 0);
            var lastLeft = Palm(true); var lastRight = Palm(false);
            var lastElbow = Elbow();
            int frames = Mathf.CeilToInt(StrawBed.LieDownSeconds * 60);
            for (int frame = 1; frame <= frames; frame++) {
                float down = (float)frame / frames;
                StrawBedView.Draw(rig, mat, down);
                var left = Palm(true); var right = Palm(false); var elbow = Elbow();
                if (down >= StrawBedView.HandOn) StaysPlanted(left, palm, "leading", down);
                if (down is >= StrawBedView.SecondHandOn and <= StrawBedView.SecondHandOff) StaysPlanted(right, secondPalm, "trailing", down);
                if (down > StrawBedView.SecondHandOff) Require(right.Y >= StrawBedModel.Top,
                    $"the relaxed hand should stay above the straw at {down:F3}, got {right}");
                if (down >= StrawBedView.ForearmOn) Require(elbow.Y - StrawBedModel.Top is > .03f and < .057f,
                    $"the forearm should bear weight while the shoulder descends at {down:F3}, elbow is at {elbow}");
                if (down is >= StrawBedView.ForearmOn and <= StrawBedView.SecondHandOff) Require(elbow.DistanceTo(brace) < .05f,
                    $"the elbow should stay by its support point until the shoulder settles, moved {elbow.DistanceTo(brace):F3} m at {down:F3}");
                Require(left.DistanceTo(lastLeft) < .072f && right.DistanceTo(lastRight) < .072f && elbow.DistanceTo(lastElbow) < .072f,
                    $"neither hand nor the support elbow should jump between successive frames at phase {down:F3}, " +
                    $"steps were {left.DistanceTo(lastLeft):F3}, {right.DistanceTo(lastRight):F3}, {elbow.DistanceTo(lastElbow):F3} m");
                lastLeft = left; lastRight = right; lastElbow = elbow;
            }
        } finally { rig.Root.Free(); }

        Vector3 Palm(bool left) => rig.Root.Transform * rig.HandPad(left);
        Vector3 Elbow() => rig.Root.Transform * rig.Elbow(true);

        static void StaysPlanted(Vector3 actual, Vector3 start, string hand, float down) =>
            Require(Math.Abs(actual.Y - StrawBedModel.Top) < .012f && actual.DistanceTo(start) < .012f,
                $"the {hand} hand should stay planted at phase {down:F3}, got {actual} from {start}");
    }

    private static void CheckTheBurningTorchStaysVisibleBesideTheSleeper() {
        var rig = new ActorRig(ActorSkin.Player);
        var mat = new Thing { Kind = ThingKind.StrawBed, Rotation = Mathf.Pi / 2 };
        var torch = rig.Flame.GetParent<Node3D>();
        try {
            StrawBedView.Draw(rig, mat, .5f, lit: true);
            var braced = TorchAt();
            Require(torch.Visible && braced.Y > 0 && braced.DistanceTo(Vector3.Zero) > .42f,
                "the lit torch should show beside the straw while the player braces with both hands");
            StrawBedView.Draw(rig, mat, 1, lit: true);
            Require(torch.Visible && TorchAt().DistanceTo(braced) < .005f, "the torch should stay by the bed as the player lies down");
            rig.Show(new Pose(), null, false, lit: true);
            Require(torch.Visible && TorchAt().DistanceTo(braced) > .20f, "standing again should return the lit torch to the player's hand");
        } finally { rig.Root.Free(); }

        Vector3 TorchAt() {
            var point = Vector3.Zero;
            for (Node3D? node = torch; node != null; node = node.GetParent() as Node3D) point = node.Transform * point;
            return point;
        }
    }
}
