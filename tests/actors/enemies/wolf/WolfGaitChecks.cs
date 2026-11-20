using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Checks of how a wolf moves its body: its gaits and its turn at a gallop.</summary>
public static class WolfGaitChecks {
    public static void Run() {
        Test(CheckWolfGaitsPlantEachPaw);
        Test(CheckAWolfReversingAtAGallopLooksRoundFirstAndBrakesUntilItsBodyComesRound);
    }

    /// <summary>
    /// Stepped at 60 frames a second through each gait at its pace, and through every change between them from a stand to a
    /// gallop and back: no part of a sole goes into the ground, a part of a sole on the ground holds its place while the body
    /// moves on over it, no joint bends past a dog's range, and in a gallop's floats the body falls under gravity.
    /// </summary>
    private static void CheckWolfGaitsPlantEachPaw() {
        const float dt = 1f / 60;
        var wolf = new WolfModel();
        float travelled = 0, time = 0;
        foreach (var (gait, speed, chasing) in new[] { ("walk", 1.1f, false), ("patrol walk", 1.5f, false), ("trot", 3f, false), ("slow gallop", 4.2f, true), ("gallop", 6.3f, true) }) {
            // A second at the pace lets the speed settle and the change from the last gait finish.
            for (int i = 0; i < 60; i++) travelled = WolfModelSteps.RunOn(wolf, travelled, speed, chasing, dt);
            var paws = new PawWatch();
            float? lift = null, rising = null, steepest = 0;
            for (int i = 0; i < 240; i++, time += dt) {
                travelled = WolfModelSteps.RunOn(wolf, travelled, speed, chasing, dt);
                paws.See(wolf, time);
                if (!chasing) continue;
                // Off the ground the body's height changes by gravity's pull a frame squared each frame.
                if (!WolfModelSteps.InTheAir(wolf)) { lift = rising = null; continue; }
                float now = wolf.Shown.Lift;
                if (lift is float was) {
                    if (rising is float before) steepest = Math.Max(steepest!.Value, Math.Abs((now - was) - before + 9.81f * dt * dt) / (dt * dt));
                    rising = now - was;
                }
                lift = now;
            }
            Require(steepest < 1, $"a galloping wolf's body should rise and fall under gravity in the air in the {gait}: {steepest:0.0} m/s² off");
            Require(paws.Slide < .015f, $"a paw on the ground should not slide in the {gait}: leg {paws.SlideLeg} slid {paws.Slide:0.000} m, {paws.SlideAt:0.00} s in");
            Require(paws.Deepest > -.002f, $"no paw should go into the ground in the {gait}: leg {paws.DeepestLeg} is {-paws.Deepest:0.000} m in, {paws.DeepestAt:0.00} s in");
            Require(paws.PastRange < .6f, $"no joint should bend past a dog's range in the {gait}: the {paws.PastJoint} by {paws.PastRange:0.0} degrees");
        }
        // From a stand through every gait to a gallop and back down to a stand.
        var changes = new PawWatch();
        ReadOnlySpan<float> at = [0, 1, 2, 3.5f, 4.5f, 6, 7, 8.5f, 10, 11], pace = [0, 0, 1.1f, 1.1f, 3, 3, 6.3f, 6.3f, 0, 0];
        for (float t = 0; t < 11; t += dt) {
            int i = 1;
            while (at[i] <= t) i++;
            float speed = Mathf.Lerp(pace[i - 1], pace[i], Mathf.SmoothStep(0, 1, (t - at[i - 1]) / (at[i] - at[i - 1])));
            travelled = WolfModelSteps.RunOn(wolf, travelled, speed, speed > 4.5f, dt);
            changes.See(wolf, t);
        }
        Require(changes.Slide < .015f, $"a paw on the ground should not slide as a wolf changes gait: leg {changes.SlideLeg} slid {changes.Slide:0.000} m, {changes.SlideAt:0.00} s in");
        Require(changes.Deepest > -.002f, $"no paw should go into the ground as a wolf changes gait: leg {changes.DeepestLeg} is {-changes.Deepest:0.000} m in, {changes.DeepestAt:0.00} s in");
        Require(changes.PastRange < .6f, $"no joint should bend past a dog's range as a wolf changes gait: the {changes.PastJoint} by {changes.PastRange:0.0} degrees");
        wolf.Root.Free();
    }

    private static void CheckAWolfReversingAtAGallopLooksRoundFirstAndBrakesUntilItsBodyComesRound() {
        var wolf = new WolfModel();
        const float dt = 1f / 60, speed = 6.3f;
        var ahead = new Vector2(0, 1);
        for (int i = 0; i < 60; i++) {
            var moved = ahead * speed * dt;
            Require(!wolf.TurnToward(moved, moved, dt, true, true, false), "a wolf galloping on in a straight line should not brake");
            wolf.Move(moved.Length(), dt, true, false);
        }
        bool braked = false, stillBraking = true;
        float lookAhead = 0;
        // The body takes 0.3 s to come most of the way round; half a second later it gallops on.
        for (int i = 0; i < 48; i++) {
            var moved = -ahead * speed * dt;
            bool braking = wolf.TurnToward(moved, moved, dt, true, true, false);
            if (braking) wolf.Brake(moved.Length(), dt); else wolf.Move(moved.Length(), dt, true, false);
            if (i < 6) lookAhead = Math.Max(lookAhead, Math.Abs(wolf.Turning.Look));
            braked |= braking; stillBraking = braking;
        }
        Require(lookAhead > Mathf.DegToRad(20), $"a wolf turning round should turn its head ahead of its body, not {Mathf.RadToDeg(lookAhead):0} degrees");
        Require(braked, "a wolf reversing at a gallop should brake while its body still faces the way it came");
        Require(!stillBraking && wolf.Motion == WolfMotion.Gallop, $"a wolf whose body has come round should gallop on, not {wolf.Motion}");
        wolf.Root.Free();
    }
}
