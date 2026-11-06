using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Focused checks for deterministic actor animation selection and timing.</summary>
public static class AnimationChecks {
    public static void Run() {
        CheckActionsLoopAndWrapByTheirDuration();
        CheckActorsTurnToFaceTheirMovement();
        CheckPlayerActionFollowsToolAndTarget();
        CheckWorkerHoldingALoadCarriesIt();
        CheckStationWorkFollowsWorkStateNotItsWording();
        CheckStrikingWorkerProtestsUntilWalking();
        CheckGatheringWorkerSwingsTheToolOfItsTarget();
        CheckWorkerAtLunchSitsOnceThere();
        CheckEveryActionMovesThroughDistinctPoses();
        CheckCraftingSquatsOnlyWhileStandingStill();
        CheckChangingActionBlendsBetweenPoses();
        CheckRunPlantsEachFootAndLeavesTheGroundAfterPushOff();
        CheckWolfGaitsPlantEachPaw();
    }

    /// <summary>At each gait's pace a paw on the ground holds its place while the body moves on over it, and no paw sinks into the ground.</summary>
    private static void CheckWolfGaitsPlantEachPaw() {
        var wolf = new WolfModel();
        foreach (var (gait, speed, chasing) in new[] { ("walk", 1.1f, false), ("trot", 3f, false), ("gallop", 6.3f, true) }) {
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

    private static void CheckCraftingSquatsOnlyWhileStandingStill() {
        var empty = new Inventory();
        Require(ActorAnimation.PlayerAction(null, false, false, empty, crafting: true) == ActorAction.Craft, "a player crafting by hand should squat over the work while standing still");
        Require(ActorAnimation.PlayerAction(null, false, true, empty, crafting: true) == ActorAction.Walk, "moving should stand the crafting player up to walk");
        Require(ActorAnimation.PlayerAction(ThingKind.Berries, true, false, empty, crafting: true) == ActorAction.Gather, "gathering should come before crafting");
        var squat = ActorAnimation.PoseAt(ActorAction.Craft, .3f, false);
        Require(squat.KneeL > 120 && squat.KneeR > 120 && squat.SwingL > 80, "the crafting pose should be a deep squat");
        Require(Math.Abs(squat.ElbowL - ActorAnimation.PoseAt(ActorAction.Craft, 1.1f, false).ElbowL) > 1, "the hands should keep working while squatting");
    }

    private static void CheckChangingActionBlendsBetweenPoses() {
        var squat = ActorAnimation.PoseAt(ActorAction.Craft, .5f, false);
        var walk = ActorAnimation.PoseAt(ActorAction.Walk, 0, true);
        Require(ActorAnimation.Blended(ActorAction.Craft, .5f, false, ActorAction.Walk, 0, true, 0) == squat, "a change of action should start from the old pose");
        Require(ActorAnimation.Blended(ActorAction.Craft, .5f, false, ActorAction.Walk, 0, true, 1) == walk, "a finished blend should be the new pose");
        var halfway = ActorAnimation.Blended(ActorAction.Craft, .5f, false, ActorAction.Walk, 0, true, .5f);
        Require(halfway.KneeL < squat.KneeL && halfway.KneeL > walk.KneeL, "halfway through, the knees should be rising out of the squat");
        // Stopping from a run rises out of its crouch onto the feet: no step of the blend sinks a foot into the ground.
        for (int i = 0; i <= 10; i++) {
            var stopping = ActorAnimation.Blended(ActorAction.Run, .3f, true, ActorAction.Idle, 0, false, i / 10f);
            float lowest = stopping.HipHeight - ActorAnimation.Sole - Math.Max(ActorAnimation.Reach(stopping.SwingL, stopping.KneeL), ActorAnimation.Reach(stopping.SwingR, stopping.KneeR));
            Require(lowest > -.001f, $"stopping from a run should not sink the feet into the ground: {lowest:0.000} m at {i / 10f:0.0} of the blend");
        }
    }

    /// <summary>
    /// A run leaves the ground for a while after each push-off, never sinks a foot into it, and carries the hips up and down smoothly.
    /// A foot on the ground holds its place there while the body runs on over it.
    /// </summary>
    private static void CheckRunPlantsEachFootAndLeavesTheGroundAfterPushOff() {
        const int samples = 400;
        int airborne = 0;
        float lastHips = 0, steepest = 0, deepest = 0, slide = 0;
        var planted = new float?[2];
        for (int i = 0; i <= samples; i++) {
            // Two steps carry the body a run's stride along the ground.
            float travelled = ActorAnimation.RunStride * i / samples;
            var run = ActorAnimation.PoseAt(ActorAction.Run, ActorAnimation.Advance(ActorAction.Run, 0, travelled), true);
            bool grounded = false;
            for (int side = 0; side < 2; side++) {
                var (toe, heel) = side == 0 ? ActorAnimation.SoleOf(run.SwingL, run.KneeL, run.FootL) : ActorAnimation.SoleOf(run.SwingR, run.KneeR, run.FootR);
                float toeClear = run.Hips - toe.Y, heelClear = run.Hips - heel.Y;
                deepest = Math.Min(deepest, Math.Min(toeClear, heelClear));
                if (toeClear > .002f) { planted[side] = null; continue; }
                grounded = true;
                float at = travelled + toe.X;
                planted[side] ??= at;
                slide = Math.Max(slide, Math.Abs(at - planted[side]!.Value));
            }
            if (i < samples && !grounded) airborne++;
            if (i > 0) steepest = Math.Max(steepest, Math.Abs(run.Hips - lastHips));
            lastHips = run.Hips;
        }
        // A sprint is in the air for over half of each step; a jog or a walk would be on the ground most of it.
        Require(airborne > samples * .5f && airborne < samples * .8f, $"a run should leave the ground for most of each step, not walk or bound: airborne {airborne} of {samples} samples");
        Require(deepest > -.002f, $"a running foot should never sink into the ground: {deepest:0.000} m");
        Require(slide < .01f, $"a planted foot should stay where it landed while the body runs over it: it slid {slide:0.000} m");
        // Six centimetres up and down twice a stride moves the hips under 3 mm between these samples.
        Require(steepest < .003f, $"the hips should rise and fall smoothly through a run, not jump: {steepest:0.0000} m between samples");
        Require(ActorAnimation.PoseAt(ActorAction.Walk, .3f, true).Hips == 0, "a walk should keep a foot on the ground");
        // The bounce under gravity and the tenth of a second on the ground are timed for a cycle at sprint speed.
        var data = GameData.Create();
        float sprintCycle = ActorAnimation.RunStride / data.MoveSpeed(sprinting: true);
        Require(Math.Abs(sprintCycle - ActorAnimation.Duration(ActorAction.Run)) < .02f,
            $"a run's stride should take as long at sprint speed as its timing assumes: {sprintCycle:0.000} s against {ActorAnimation.Duration(ActorAction.Run):0.000} s");
    }

    private static void CheckActionsLoopAndWrapByTheirDuration() {
        Require(ActorAnimation.Phase(ActorAction.Chop, .25f) == .25f, "a one-second chop should be a quarter through after .25s");
        Require(ActorAnimation.Phase(ActorAction.Chop, 1) == 0, "action loops should wrap after their duration");
        Require(Math.Abs(ActorAnimation.Phase(ActorAction.Chop, -.125f) - .875f) < 1e-5f, "negative elapsed time should wrap safely");
        Require(Math.Abs(ActorAnimation.Phase(ActorAction.Walk, .4f) - .5f) < 1e-5f, "walking should loop every .8 seconds");
        Require(ActorAnimation.Phase(ActorAction.Attack, ActorAnimation.Duration(ActorAction.Attack)) == 0, "punches should wrap at their own duration");
        Require(ActorAnimation.Phase(ActorAction.Idle, float.NaN) == 0, "invalid time should fall back to the first pose");
    }

    private static void CheckActorsTurnToFaceTheirMovement() {
        Require(Math.Abs(ActorAnimation.Yaw(new Vector2(0, 1), 1) - 0) < 1e-5f, "world +Y movement should face the rig's +Z forward");
        Require(Math.Abs(ActorAnimation.Yaw(new Vector2(1, 0), 0) - Mathf.Pi / 2) < 1e-5f, "eastward movement should turn a quarter turn");
        Require(Math.Abs(Math.Abs(ActorAnimation.Yaw(new Vector2(0, -1), 0)) - Mathf.Pi) < 1e-5f, "northward movement should face backward");
        Require(ActorAnimation.Yaw(Vector2.Zero, 2) == 2, "zero movement should retain the previous facing");
    }

    private static void CheckPlayerActionFollowsToolAndTarget() {
        var bag = new Inventory();
        bag.Add("steel_axe", 1);
        bag.Add("steel_pick", 1);

        Require(ActorAnimation.PlayerAction(ThingKind.Tree, true, true, bag) == ActorAction.Walk,
            "movement toward a resource should animate as walking");
        Require(ActorAnimation.PlayerAction(ThingKind.Tree, true, false, bag) == ActorAction.SteelChop,
            "a stationary player with a steel axe should chop a tree");
        Require(ActorAnimation.PlayerAction(ThingKind.Iron, true, false, bag) == ActorAction.SteelMine,
            "a carried steel pick should mine iron deposits");
        Require(ActorAnimation.PlayerAction(ThingKind.Stone, true, false, bag) == ActorAction.SteelMine,
            "a carried pick should mine stone");

        var empty = new Inventory();
        Require(ActorAnimation.PlayerAction(ThingKind.Tree, true, false, empty) == ActorAction.Gather,
            "tools not carried should not animate as tool use");
        var axeOnly = new Inventory();
        axeOnly.Add("steel_axe", 1);
        Require(ActorAnimation.PlayerAction(ThingKind.Coal, true, false, axeOnly) == ActorAction.Gather,
            "a carried but unsuitable axe should not animate as mining");
        Require(ActorAnimation.PlayerAction(ThingKind.Berries, true, false, empty) == ActorAction.Gather,
            "hand gathering berries should use the gather action");
        Require(ActorAnimation.PlayerAction(ThingKind.WolfCorpse, true, false, empty) == ActorAction.Gather,
            "butchering a carcass should use the crouched gather action");
        Require(ActorAnimation.PlayerAction(ThingKind.Tree, false, false, bag) == ActorAction.Idle,
            "inactive gathering should leave the player idle");
    }

    private static void CheckWorkerHoldingALoadCarriesIt() {
        var worker = new Worker();
        worker.Bag.Add("wood", 2);
        worker.Status = "Carrying wood → chest";
        Require(ActorAnimation.WorkerAction(worker, null, true) == ActorAction.Carry,
            "a worker hauling a load while moving should retain the carry action");
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Carry,
            "a worker holding a load while stationary should use carry pose");
    }

    private static void CheckStationWorkFollowsWorkStateNotItsWording() {
        var worker = new Worker();
        worker.Status = "Text can vary";
        worker.WorkState = WorkerWorkState.Pumping;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Reinforced }, false) == ActorAction.Pump,
            "bellows work state should select pumping animation");
        worker.Status = "Different text";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Reinforced }, false) == ActorAction.Pump,
            "rewording status should not change the pumping animation");

        worker.Status = "Station status text";
        worker.WorkState = WorkerWorkState.Hammering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, false) == ActorAction.Hammer,
            "anvil work should select hammering animation");
    }

    private static void CheckStrikingWorkerProtestsUntilWalking() {
        var worker = new Worker();
        worker.Status = "On strike · wages owed";
        worker.WorkState = WorkerWorkState.Idle;
        worker.WageDebt = worker.Wage;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, false) == ActorAction.Protest
                && ActorAnimation.Tool(ActorAction.Protest) == "protest_sign",
            "striking workers should stand holding the protest sign instead of working");
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, true) == ActorAction.Walk,
            "a striking worker heading home should walk, not protest");
    }

    private static void CheckGatheringWorkerSwingsTheToolOfItsTarget() {
        var worker = new Worker();
        worker.Status = "Gathering wood";
        worker.WorkState = WorkerWorkState.Blocked;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Tree }, false) == ActorAction.Idle,
            "waiting workers should not swing tools just because they have an assignment");

        worker.Status = "Arbitrary status";
        worker.WorkState = WorkerWorkState.Gathering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Tree }, false) == ActorAction.Chop,
            "workers gathering trees should chop");
        worker.Status = "Arbitrary status";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Iron }, false) == ActorAction.Mine,
            "workers gathering iron should mine");
        worker.Status = "Available";
        worker.WorkState = WorkerWorkState.Idle;
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Idle,
            "available workers should remain idle");
    }

    private static void CheckWorkerAtLunchSitsOnceThere() {
        var worker = new Worker();
        worker.WorkState = WorkerWorkState.Resting;
        worker.Bag.Add("wood", 2);
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, false) == ActorAction.Perch,
            "a worker at lunch should sit, even holding a load");
        worker.Id = 1;
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Sit,
            "every other worker should sit cross-legged on the ground rather than on a rock");
        worker.Id = 0;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, true) == ActorAction.Carry,
            "a worker still walking to lunch should not sit");
    }

    private static void CheckEveryActionMovesThroughDistinctPoses() {
        foreach (var action in Enum.GetValues<ActorAction>()) {
            var poses = new HashSet<Pose>();
            for (int step = 0; step < 8; step++)
                poses.Add(ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) * step / 8, action == ActorAction.Carry));
            Require(poses.Count >= 6, $"{action} should move through at least six distinct poses per loop, found {poses.Count}");
            var start = ActorAnimation.PoseAt(action, 0, false);
            var end = ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) - .001f, false);
            Require(Math.Abs(start.RaiseR - end.RaiseR) < 2,
                $"{action} should end with its right arm where it started");
            Require(Math.Abs(start.Lean - end.Lean) < 2, $"{action} should end with its lean where it started");
            Require(Math.Abs(start.SwingL - end.SwingL) < 2,
                $"{action} should end with its left swing where it started");
        }
        var windup = ActorAnimation.PoseAt(ActorAction.Chop, .38f, false);
        var strike = ActorAnimation.PoseAt(ActorAction.Chop, .52f, false);
        Require(windup.RaiseR > 150 && strike.RaiseR < 100, "the axe stroke should rise overhead and come down to the trunk");
        Require(ActorAnimation.PoseAt(ActorAction.Gather, .3f, false).KneeL > 80, "hand gathering should crouch");
        Require(ActorAnimation.PoseAt(ActorAction.PickUp, .32f * ActorAnimation.Duration(ActorAction.PickUp), false).KneeL > 80, "picking up a loose piece should crouch for it");
        var swingWalking = ActorAnimation.PoseAt(ActorAction.TorchSwing, .27f, true);
        var swingStanding = ActorAnimation.PoseAt(ActorAction.TorchSwing, .27f, false);
        Require(swingWalking.RaiseL == swingStanding.RaiseL && swingWalking.SwingL == ActorAnimation.PoseAt(ActorAction.Walk, .27f, true).SwingL,
            "a torch swing on the move should keep the swinging arm while the legs keep walking");
        Require(ActorAnimation.PoseAt(ActorAction.Sit, 0, false).Hips is > 0 and < .2f && ActorAnimation.PoseAt(ActorAction.Perch, 0, false).Hips == 0,
            "a cross-legged worker should sit on the ground, and one on a rock should not");
        Require(ActorAnimation.Tool(ActorAction.SteelMine) == "steel_pick", "steel mining should show the steel pick");
        Require(ActorAnimation.Tool(ActorAction.Hammer) == "hammer", "hammering should show the hammer");
        Require(ActorAnimation.Tool(ActorAction.Gather) == null, "hand gathering should show no tool");
    }
}
