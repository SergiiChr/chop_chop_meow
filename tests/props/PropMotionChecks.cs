using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PropMotionSteps;

/// <summary>
/// Checks of world objects in motion, stepped frame by frame: a felled tree, a dug out stump or bush, a chip, a slammed door,
/// a cart, the window's clock and a fire going out.
/// </summary>
public static class PropMotionChecks {
    private const float Frame = 1 / 60f;

    public static void Run() {
        CheckAFelledTreeTurnsAboutItsFootAndComesToRestOnItsWood();
        CheckATallerTreeFallsLongerAndLandsFaster();
        CheckATreeFallsTheSameAtAnyFrameRate();
        CheckADugOutStumpOrBushIsThrownUnderGravityAndStaysOutOfTheGround();
        CheckAChipArcsUnderGravityAndSkidsToAStop();
        CheckASlammedDoorSpeedsUpIntoItsFrame();
        CheckACartSetsOffAndStopsWithoutAJump();
        CheckTheWindowsClockMovesEveryFrameBetweenUpdates();
        CheckAFireThatGoesOutFadesOverHalfASecond();
    }

    /// <summary>
    /// A station's fire burns fully while it has work and through the tick between one batch and the next, and once its
    /// work stops it dies down over half a second after that tick, never brightening on the way.
    /// </summary>
    private static void CheckAFireThatGoesOutFadesOverHalfASecond() {
        var fire = new CampfireProp();
        Require(fire.Burning(true, Frame) == 1, "a fire with work should burn fully");
        Require(fire.Burning(false, 1 / 30f) == 1 && fire.Burning(true, Frame) == 1, "a fire should burn fully through the tick between two batches");
        float time = 0, shown = 1, at = 0, mid = float.NaN;
        bool brightened = false;
        while (time < .8f) {
            time += Frame;
            float now = fire.Burning(false, Frame);
            brightened |= now > shown;
            if (Math.Abs(time - .1f) < Frame / 2) at = now;
            if (Math.Abs(time - .35f) < Frame / 2) mid = now;
            if (time > .6f + Frame / 2) Require(now == 0, $"a fire should be out 0.6 s after its work stops, not still at {now:0.00} at {time:0.00} s");
            shown = now;
        }
        Require(at > .99f, $"a fire should still burn fully 0.1 s after its work stops, not at {at:0.00}");
        Require(Math.Abs(mid - .5f) < .1f, $"a fire should be half out 0.35 s after its work stops, not at {mid:0.00}");
        Require(!brightened, "a fire going out should never brighten");
    }

    /// <summary>Every look of tree, felled eight ways: its foot stays where it stood, its wood never goes into the ground, and it gathers speed until it lands.</summary>
    private static void CheckAFelledTreeTurnsAboutItsFootAndComesToRestOnItsWood() {
        for (int id = 0; id < WorldModels.TreeLooks; id++)
            for (int way = 0; way < 8; way++) {
                var fallen = FellTree(id, way * MathF.PI / 4, Frame);
                Require(fallen.Lowest > -.001f, $"tree look {id} felled at {way * 45}° should keep its trunk and branches out of the ground, not {fallen.Lowest * 100:0.0} cm into it");
                Require(fallen.HingeMoved < .001f, $"tree look {id} should turn about the foot of its trunk, which moved {fallen.HingeMoved * 100:0.0} cm");
                Require(fallen.Slowed == 0, $"tree look {id} felled at {way * 45}° should gather speed all the way down, but slowed on {fallen.Slowed} frames");
                Require(fallen.Lands > MathF.PI / 6 && fallen.Lands <= MathF.PI / 2, $"tree look {id} should land on its wood, which leans it at least 30° over, or lie flat, not at {Mathf.RadToDeg(fallen.Lands):0}°");
            }
    }

    /// <summary>
    /// A pole hinged at its foot falls in a time that grows with the square root of its length, and its top lands faster
    /// the same way (https://demoweb.physics.ucla.edu/content/40-falling-chimney): the same look sized half as tall again
    /// takes and lands √1.5 times as long and as fast.
    /// </summary>
    private static void CheckATallerTreeFallsLongerAndLandsFaster() {
        var low = FellSized(1);
        var high = FellSized(1.5f);
        float ratio = MathF.Sqrt(1.5f);
        Require(Math.Abs(high.Seconds / low.Seconds - ratio) < .01f, $"a tree half as tall again should take {ratio:0.00} times as long to fall, not {high.Seconds / low.Seconds:0.00}");
        Require(Math.Abs(high.TopSpeed / low.TopSpeed - ratio) < .01f, $"its top should land {ratio:0.00} times as fast, not {high.TopSpeed / low.TopSpeed:0.00}");
    }

    private static void CheckATreeFallsTheSameAtAnyFrameRate() {
        var at30 = FellTree(0, 1, 1 / 30f);
        var at60 = FellTree(0, 1, 1 / 60f);
        var at144 = FellTree(0, 1, 1 / 144f);
        Require(Math.Abs(at30.Seconds - at144.Seconds) <= 1 / 30f && Math.Abs(at60.Seconds - at144.Seconds) <= 1 / 60f,
            $"a tree should land at the same moment at any frame rate, give or take a frame: {at30.Seconds:0.000} s at 30, {at60.Seconds:0.000} at 60, {at144.Seconds:0.000} at 144");
    }

    /// <summary>Thrown out of its hole, a stump or bush rises and falls back at g, and no part of it goes lower than it stood as it came out.</summary>
    private static void CheckADugOutStumpOrBushIsThrownUnderGravityAndStaysOutOfTheGround() {
        foreach (var kind in new[] { ThingKind.Stump, ThingKind.Bush }) {
            var thrown = ThrowDugOut(kind, Frame);
            Require(Math.Abs(thrown.Gravity - 9.81f) < .05f, $"a dug out {kind} should rise and fall at 9.81 m/s², not {thrown.Gravity:0.00}");
            Require(thrown.Lowest >= thrown.LowestAtStart - .001f, $"a dug out {kind} should not dip into the ground: its lowest point went from {thrown.LowestAtStart * 100:0.0} cm to {thrown.Lowest * 100:0.0} cm");
        }
    }

    /// <summary>A chip flies off the rock at g, lands, then skids and turns to a stop, with neither its speed along the ground nor its spin jumping between frames.</summary>
    private static void CheckAChipArcsUnderGravityAndSkidsToAStop() {
        var chip = FlyChip(.7f, Frame);
        Require(Math.Abs(chip.Gravity - 9.81f) < .05f, $"a chip should fall at 9.81 m/s² in the air, not {chip.Gravity:0.00}");
        Require(chip.Lowest >= 0, $"a chip should stay out of the ground, not reach {chip.Lowest * 100:0.0} cm into it");
        float evenly = Frame / WorldModels.ChipSettle;
        Require(chip.BiggestChange < evenly * 1.05f, $"a chip should lose its pace along the ground evenly over its skid, at most {evenly:0.00} of its speed a frame, not {chip.BiggestChange:0.00}");
        Require(chip.Stopped, "a chip should come to rest where it lies at the end of its skid");
    }

    private static void CheckASlammedDoorSpeedsUpIntoItsFrame() {
        var speeds = SlamDoor(Frame);
        for (int i = 1; i < speeds.Length - 1; i++) Require(speeds[i] > speeds[i - 1], $"a slammed door should speed up until it hits its frame, but frame {i} turned {speeds[i]:0}°/s after {speeds[i - 1]:0}°/s");
        Require(speeds.Length * Frame <= DoorwayView.SlamSeconds + Frame, $"a slam should shut the door within {DoorwayView.SlamSeconds} s, not {speeds.Length * Frame:0.000}");
    }

    private static void CheckACartSetsOffAndStopsWithoutAJump() {
        const float trip = 30;
        float first = WorldView.CartLeg(Frame, trip) * WorldView.CartDistance / Frame, last = WorldView.CartLeg(trip - Frame, trip) * WorldView.CartDistance / Frame;
        Require(first < .1f && last < .1f, $"a cart should set off from the signpost and stop back there at a walk's start, not at {first:0.00} and {last:0.00} m/s");
        Require(Math.Abs(WorldView.CartLeg(4, trip) - WorldView.CartLeg(4 - Frame, trip) - WorldView.CartSpeed * Frame / WorldView.CartDistance) < 1e-4f, "a cart should walk at its pace once it has picked up");
    }

    /// <summary>A count the server moves 30 times a second, drawn at 60 frames a second, moves on every frame, never back, and keeps up with the count.</summary>
    private static void CheckTheWindowsClockMovesEveryFrameBetweenUpdates() {
        var (steps, lead) = FollowCount(Frame, 1 / 30f, 3);
        Require(steps.All(step => step > .8f * Frame && step < 1.2f * Frame), $"a count running at one a second should move by a frame's worth each frame, give or take a fifth: {steps.Min() / Frame:0.00} to {steps.Max() / Frame:0.00}");
        Require(Math.Abs(lead) < LocalClock.MaxAhead, $"the clock should keep within {LocalClock.MaxAhead} of the count, not {lead:0.000}");
        var clock = new LocalClock(1);
        clock.Follow(5, 1, Frame); clock.Follow(5.03f, 1.03f, Frame);
        Require(clock.Follow(20, 1.06f, Frame) == 20, "a big change of the count, such as a new stage, should be taken at once");
        float after = clock.Follow(20, 1.06f, Frame);
        Require(Math.Abs(after - 20) < .01f, $"a count taken at once should hold there until the next update, not run on to {after:0.00}");
    }
}
