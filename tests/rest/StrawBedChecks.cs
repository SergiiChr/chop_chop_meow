using System;
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
        Test(CheckSleepingOnStrawNeedsThePlayerBesideTheBed, data);
        Test(CheckSoloStrawBedSleeperHasTimeToLieDownBeforeDawn, data);
        Test(CheckCabinAndStrawBedSleepTogetherSkipToDawn, data);
        Test(CheckBootsStayPlantedAndClearTheMat);
        Test(CheckSupportHandStaysPlantedThroughTheWeightShift);
    }

    private static void CheckTakingApartAFreshBedGivesBackExactlyItsCost(GameData data) {
        var world = EmptyWorld(data);
        var bed = StrawBedSteps.Build(world);
        Require(world.Backpack["grass"] == 0 && world.Backpack["leaves"] == 0, "building a straw bed spends two grass and twenty leaves");
        Require(world.CanStand(bed.Pos), "the low mat should not block walking");
        world.Tick(data.HourSeconds - 1);
        Require(world.Construction.Dismantle(bed) && world.Find(bed.Id) == null, "taking down a fresh bed should remove it");
        Require(world.Backpack["grass"] == 2 && world.Backpack["leaves"] == 20, "a bed less than an hour old returns its cost");
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
            "a solo sleeper should start healing with time to lie down before the clock skips.");
        world.Tick(StrawBed.LieDownSeconds - .5f);
        Require(world.Day == day && world.Me.SleepingAt == bed.Id, "dawn should leave time for the last frame to reach clients");
        world.Tick(.25f);
        Require(world.Day == day + 1 && world.Me.SleepingAt == 0, "the clock should skip automatically once the sleeper lies down.");
    }

    private static void CheckCabinAndStrawBedSleepTogetherSkipToDawn(GameData data) {
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
                Check(Sole(true), left, down); Check(Sole(false), right, down);
            }
        } finally { rig.Root.Free(); }

        (Vector3 Toe, Vector3 Heel) Sole(bool left) {
            var (toe, heel) = rig.BootSole(left);
            return (rig.Root.Transform * toe, rig.Root.Transform * heel);
        }

        static void Check((Vector3 Toe, Vector3 Heel) actual, (Vector3 Toe, Vector3 Heel) start, float down) {
            const float tolerance = .008f;
            Require(actual.Toe.IsFinite() && actual.Heel.IsFinite() && Math.Min(actual.Toe.Y, actual.Heel.Y) >= StrawBedModel.Top - tolerance,
                $"boot must not go through the straw at phase {down:F3}, got {actual}");
            if (down <= StrawBedView.BothBootsOnUntil) {
                Require(Math.Abs(actual.Toe.Y - StrawBedModel.Top) < tolerance && Math.Abs(actual.Heel.Y - StrawBedModel.Top) < tolerance,
                    $"both boots should stand flat while bearing weight at phase {down:F3}, got {actual}");
                Require(actual.Toe.DistanceTo(start.Toe) < tolerance && actual.Heel.DistanceTo(start.Heel) < tolerance,
                    $"both boots should stay in place while bearing weight at phase {down:F3}, got {actual} from {start}");
            }
        }
    }

    private static void CheckSupportHandStaysPlantedThroughTheWeightShift() {
        var rig = new ActorRig(ActorSkin.Player);
        var mat = new Thing { Kind = ThingKind.StrawBed };
        try {
            var contact = Palm(StrawBedView.HandOn);
            int frames = Mathf.CeilToInt((StrawBedView.HandOff - StrawBedView.HandOn) * StrawBed.LieDownSeconds * 60);
            for (int frame = 0; frame <= frames; frame++) {
                float down = Mathf.Lerp(StrawBedView.HandOn, StrawBedView.HandOff, (float)frame / frames);
                var planted = Palm(down);
                Require(Math.Abs(planted.Y - StrawBedModel.Top) < .012f && planted.DistanceTo(contact) < .012f,
                    $"the hand should stay on the straw while it supports the weight shift at phase {down:F3}, got {planted} from {contact}");
            }
        } finally { rig.Root.Free(); }

        Vector3 Palm(float down) {
            StrawBedView.Draw(rig, mat, down);
            return rig.Root.Transform * rig.HandPad(true);
        }
    }
}
