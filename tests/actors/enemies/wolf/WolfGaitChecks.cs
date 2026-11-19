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

    /// <summary>At each gait's pace a paw on the ground holds its place while the body moves on over it, and no paw sinks into the ground.</summary>
    private static void CheckWolfGaitsPlantEachPaw() {
        var wolf = new WolfModel();
        foreach (var (gait, speed, chasing) in new[] { ("walk", 1.1f, false), ("patrol walk", 1.5f, false), ("trot", 3f, false), ("gallop", 6.3f, true) }) {
            const float dt = 1f / 240;
            // A second at the pace lets the speed settle and the blend from the last gait finish.
            for (int i = 0; i < 240; i++) wolf.Move(speed * dt, dt, chasing, false);
            float travelled = 0, slide = 0, deepest = 0;
            var landed = new float?[4];
            for (int i = 0; i < 480; i++) {
                wolf.Move(speed * dt, dt, chasing, false); travelled += speed * dt;
                for (int leg = 0; leg < 4; leg++) {
                    var paw = wolf.Paw(leg);
                    deepest = Math.Min(deepest, paw.Y - WolfShape.Sole);
                    // The model faces +Z, so a paw's place on the ground is its offset plus the distance travelled.
                    if (paw.Y < WolfShape.Sole + .004f) {
                        landed[leg] ??= paw.Z + travelled;
                        slide = Math.Max(slide, Math.Abs(paw.Z + travelled - landed[leg]!.Value));
                    } else landed[leg] = null;
                }
            }
            Require(slide < .02f, $"a planted paw should not slide in the {gait}: {slide:0.000} tiles");
            Require(deepest > -.005f, $"no paw should sink into the ground in the {gait}: {deepest:0.000} tiles");
        }
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
