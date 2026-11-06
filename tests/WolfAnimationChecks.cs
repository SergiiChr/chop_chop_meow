using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>The wolf's keyframed motion: gaits that keep their paws on the ground, attacks that reach, and a rig that lands where the arithmetic says.</summary>
public static class WolfAnimationChecks {
    private static readonly WolfState[] Gaits = { WolfState.Stalk, WolfState.Trot, WolfState.Gallop };

    public static void Run() {
        CheckEveryStateMovesThroughDistinctPoses();
        CheckGaitsLoopAndKeepTheirPawsAtTheGround();
        CheckLeapDrivesTucksAndReachesWithOpenJaws();
        CheckBiteLungesAndSnaps();
        CheckHitThrowsTheHeadUpAndFallEndsOnItsSide();
        CheckStateChangesBlendBetweenPoses();
        CheckRigPawsMatchTheArithmetic();
    }

    private static float Cycle(WolfState gait) => Mathf.Tau / gait switch { WolfState.Stalk => WolfAnimation.StalkRate, WolfState.Trot => WolfAnimation.TrotRate, _ => WolfAnimation.GallopRate };

    private static void CheckEveryStateMovesThroughDistinctPoses() {
        foreach (var state in Enum.GetValues<WolfState>()) {
            float span = state switch { WolfState.Idle or WolfState.Feed => 6, WolfState.Stalk or WolfState.Trot or WolfState.Gallop => Cycle(state), _ => 1 };
            var poses = new HashSet<WolfPose>();
            for (int step = 0; step < 8; step++) poses.Add(WolfAnimation.PoseAt(state, span * step / 8));
            Require(poses.Count >= 6, $"{state} should move through at least six distinct poses, found {poses.Count}");
        }
    }

    private static void CheckGaitsLoopAndKeepTheirPawsAtTheGround() {
        foreach (var gait in Gaits) {
            float cycle = Cycle(gait);
            var start = WolfAnimation.PoseAt(gait, 0); var end = WolfAnimation.PoseAt(gait, cycle);
            Require(Math.Abs(start.FrontL.Swing - end.FrontL.Swing) < .5f && Math.Abs(start.HindR.Swing - end.HindR.Swing) < .5f, $"a {gait} should end a cycle where it started");
            int grounded = 0;
            for (int step = 0; step < 48; step++) {
                var pose = WolfAnimation.PoseAt(gait, cycle * step / 48);
                float lowest = float.MaxValue;
                for (int leg = 0; leg < 4; leg++) lowest = Math.Min(lowest, WolfAnimation.Paw(pose, leg).Y);
                Require(lowest > -.08f, $"a {gait} should not drive a paw more than a skim into the ground, found {lowest:0.000} at step {step}");
                if (lowest < .03f) grounded++;
                Require(WolfAnimation.HeightOf(pose) > -.2f && WolfAnimation.HeightOf(pose) < .25f, $"a {gait} should keep the body near its standing height, found {WolfAnimation.HeightOf(pose):0.00}");
            }
            // A gallop leaves the ground for part of each stride; the slower gaits always have a paw down.
            Require(grounded >= (gait == WolfState.Gallop ? 28 : 48), $"a {gait} should keep a paw on the ground through its stance, found {grounded} of 48 steps");
        }
        var stalk = WolfAnimation.PoseAt(WolfState.Stalk, .1f); var trot = WolfAnimation.PoseAt(WolfState.Trot, .1f);
        Require(stalk.Neck > trot.Neck - 1 && WolfAnimation.PoseAt(WolfState.Gallop, .1f).Ears > trot.Ears, "a stalking wolf carries its head low and a galloping one lays its ears back");
    }

    private static void CheckLeapDrivesTucksAndReachesWithOpenJaws() {
        var drive = WolfAnimation.PoseAt(WolfState.Leap, .26f); var flight = WolfAnimation.PoseAt(WolfState.Leap, .5f); var landing = WolfAnimation.PoseAt(WolfState.Leap, .98f);
        Require(drive.HindL.Swing > 40 && drive.HindL.Knee < 10, "take-off should drive the hind legs out straight behind");
        Require(drive.FrontL.Knee > 40 && drive.FrontL.Hock > 40 && drive.Pitch < -10, "take-off should tuck the forelegs and lift the nose");
        Require(flight.Lift > .5f && flight.Jaw > 30, "mid-leap the wolf should be well off the ground with its jaws open");
        Require(landing.FrontL.Swing < -40 && landing.FrontL.Knee < 20 && landing.Pitch > 5 && landing.Neck > 20, "landing should reach the forelegs out and drive the head down");
        Require(landing.Jaw < flight.Jaw - 10 && WolfAnimation.PoseAt(WolfState.Leap, 1).Lift < .01f, "the jaws should be closing as the wolf comes down onto the player");
    }

    private static void CheckBiteLungesAndSnaps() {
        var snap = WolfAnimation.PoseAt(WolfState.Bite, .22f); var shut = WolfAnimation.PoseAt(WolfState.Bite, .5f); var done = WolfAnimation.PoseAt(WolfState.Bite, 1);
        Require(snap.Jaw > 40 && snap.Lunge > .12f, "a bite should open wide as the body lunges forward");
        Require(shut.Jaw < 8 && shut.Lunge > .1f, "the jaws should snap shut while the wolf is still forward");
        Require(Math.Abs(done.Lunge) < .01f && done.Jaw < 8, "a bite should settle back where it stood");
    }

    private static void CheckHitThrowsTheHeadUpAndFallEndsOnItsSide() {
        var flinch = WolfAnimation.PoseAt(WolfState.Recoil, .14f); var over = WolfAnimation.PoseAt(WolfState.Recoil, 1);
        Require(flinch.Neck < -10 && flinch.Ears > 50 && flinch.Tail < 20 && flinch.HindL.Knee > 40, "a hit should throw the head up with the ears flat, the tail tucked and the legs bent");
        Require(flinch.Jaw > 20 && over.Jaw < 1 && over.Ears < flinch.Ears / 2, "the yelp should close and the ears come up as the wolf straightens");
        var dead = WolfAnimation.PoseAt(WolfState.Fall, 1); var buckled = WolfAnimation.PoseAt(WolfState.Fall, .28f);
        Require(buckled.FrontL.Knee > 60 && buckled.Pitch > 10 && buckled.Height == null, "a killed wolf should buckle at the forelegs and drop its chest before it rolls");
        Require(Math.Abs(dead.Roll - 90) < .5f && dead.Height is < .3f and > .1f, "a dead wolf should lie on its side at the ground");
        Require(WolfAnimation.PoseAt(WolfState.Fall, .8f).FrontL.Swing != dead.FrontL.Swing, "the legs should still twitch before they go still");
    }

    private static void CheckStateChangesBlendBetweenPoses() {
        var trot = WolfAnimation.PoseAt(WolfState.Trot, .2f); var feed = WolfAnimation.PoseAt(WolfState.Feed, 1);
        var half = WolfAnimation.Lerp(trot, feed, .5f);
        Require(Math.Abs(half.Neck - (trot.Neck + feed.Neck) / 2) < .01f && Math.Abs(half.HindL.Knee - (trot.HindL.Knee + feed.HindL.Knee) / 2) < .01f, "a blend should sit between the two poses");
        Require(half.Height == null, "a blend of two standing poses stands on its paws");
        var fallen = WolfAnimation.PoseAt(WolfState.Fall, 1);
        var rising = WolfAnimation.Lerp(fallen, trot, .999f);
        Require(rising.Height is float h && Math.Abs(h - WolfAnimation.HeightOf(trot)) < .01f, "a blend out of a set height should arrive at the standing height");
        Require(WolfAnimation.BlendSeconds(WolfState.Leap) < WolfAnimation.BlendSeconds(WolfState.Trot), "a leap should snap in faster than a change of gait");
    }

    private static void CheckRigPawsMatchTheArithmetic() {
        var model = new WorldModels.WolfModel();
        try {
            foreach (var (state, t) in new[] { (WolfState.Idle, 1f), (WolfState.Trot, .13f), (WolfState.Gallop, .4f), (WolfState.Leap, .5f), (WolfState.Feed, .7f), (WolfState.Recoil, .14f), (WolfState.Fall, 1f) }) {
                var pose = WolfAnimation.PoseAt(state, t);
                model.Pose(pose);
                for (int leg = 0; leg < 4; leg++) {
                    var expected = WolfAnimation.Paw(pose, leg); var actual = model.PawOf(leg);
                    Require(expected.DistanceTo(actual) < .005f, $"the rig's paw {leg} in {state} should land where the arithmetic puts it: {actual} against {expected}");
                }
                if (pose.Height == null) {
                    float lowest = float.MaxValue;
                    for (int leg = 0; leg < 4; leg++) lowest = Math.Min(lowest, model.PawOf(leg).Y);
                    Require(Math.Abs(lowest - pose.Lift) < .005f, $"the lowest paw in {state} should stand on the ground plus its lift, found {lowest:0.000}");
                }
            }
        } finally { model.Root.Free(); }
    }
}
