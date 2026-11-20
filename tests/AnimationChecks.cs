using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Focused checks for deterministic actor animation selection and timing.</summary>
public static class AnimationChecks {
    /// <summary>The game's tables, which say what a station's recipe needs done at it.</summary>
    private static GameData data = null!;

    public static void Run(GameData gameData) {
        data = gameData;
        CheckActionsLoopAndWrapByTheirDuration();
        CheckActorsTurnToFaceTheirMovement();
        CheckPlayerHeadTurnsBeforeTheBody();
        CheckPlayerTurnsHalfwayRoundOneWay();
        CheckPlayerLeansIntoATurn();
        CheckPlayerActionFollowsToolAndTarget();
        CheckWorkerHoldingALoadCarriesIt();
        CheckStationWorkFollowsWorkStateNotItsWording();
        CheckStrikingWorkerProtestsUntilWalking();
        CheckGatheringWorkerSwingsTheToolOfItsTarget();
        CheckWorkerAtLunchSitsOnceThere();
        CheckEveryActionMovesThroughDistinctPoses();
        CheckCraftingSquatsOnlyWhileStandingStill();
        CheckChangingActionBlendsBetweenPoses();
        CheckEveryGaitPlantsItsFeetAndLeavesTheGroundOnlyWhenRunning();
        CheckEveryMomentOfAStrideHasAPose();
        CheckDiggingKeepsBothFeetWhereTheyStand();
        CheckAHandReachesItsPointWithTheElbowTheWayAsked();
        CheckABlowLandsItsToolOnItsMarkSmoothlyWithinReach();
        CheckAHandReachesItsPointFromAShruggedShoulder();
        CheckPosingAllocatesNothing();
    }

    /// <summary>Every worker and player in the world is posed every frame, so working out a pose makes no garbage.</summary>
    private static void CheckPosingAllocatesNothing() {
        var actions = Enum.GetValues<ActorAction>();
        float time = 0;
        var mark = new Vector3(.1f, .5f, .9f);
        AllocationChecks.Check("pose: every action, blending out of the last", () => {
            time += 1 / 60f;
            foreach (var action in actions) ActorAnimation.Blended(ActorAction.Idle, time, false, action, time, false, .5f);
        });
        AllocationChecks.Check("pose: every action aimed at a mark", () => {
            time += 1 / 60f;
            foreach (var action in actions) ActorAnimation.PoseAt(action, time, false, mark);
        });
    }

    /// <summary>Through a dig the front boot stays flat on the ground and the back one on its toe, neither sliding nor sinking, while the hips go down and up.</summary>
    private static void CheckDiggingKeepsBothFeetWhereTheyStand() {
        const int samples = 200;
        float lowest = float.MaxValue, highest = 0;
        Vector2? frontToe = null, backToe = null;
        for (int i = 0; i < samples; i++) {
            var pose = ActorAnimation.PoseAt(ActorAction.Dig, ActorAnimation.Duration(ActorAction.Dig) * i / samples, false);
            var front = ActorAnimation.SoleOf(pose.SwingL, pose.KneeL, pose.FootL);
            var back = ActorAnimation.SoleOf(pose.SwingR, pose.KneeR, pose.FootR);
            frontToe ??= front.Toe; backToe ??= back.Toe;
            Require(Math.Abs(pose.Hips - front.Toe.Y) < .003f && Math.Abs(pose.Hips - front.Heel.Y) < .003f, $"the front boot should stay flat on the ground, sample {i}: toe {pose.Hips - front.Toe.Y:0.000} m, heel {pose.Hips - front.Heel.Y:0.000} m up");
            Require(Math.Abs(pose.Hips - back.Toe.Y) < .003f && pose.Hips - back.Heel.Y > -.003f, $"the back boot should stay on its toe with the heel off the ground, sample {i}: toe {pose.Hips - back.Toe.Y:0.000} m, heel {pose.Hips - back.Heel.Y:0.000} m up");
            Require(Math.Abs(front.Toe.X - frontToe.Value.X) < .005f && Math.Abs(back.Toe.X - backToe.Value.X) < .005f, $"neither boot should slide, sample {i}");
            lowest = Math.Min(lowest, pose.Hips); highest = Math.Max(highest, pose.Hips);
        }
        Require(highest - lowest > .1f, $"the digger should bend down onto the shovel and straighten to throw: the hips moved {highest - lowest:0.00} m");
    }

    private static void CheckCraftingSquatsOnlyWhileStandingStill() {
        var empty = new Inventory();
        Require(ActorAnimation.PlayerAction(null, false, false, empty, crafting: true) == ActorAction.Craft, "a player crafting by hand should squat over the work while standing still");
        Require(ActorAnimation.PlayerAction(null, false, true, empty, crafting: true) == ActorAction.Jog, "moving should stand the crafting player up to jog");
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
    /// Each way of moving keeps every foot on the ground where it landed while the body passes over it, never sinks one into the ground and carries the hips up and down smoothly.
    /// A sprint is in the air for most of each step, a jog for some of it, and a walk never, with both feet down for a moment at each step.
    /// </summary>
    private static void CheckEveryGaitPlantsItsFeetAndLeavesTheGroundOnlyWhenRunning() {
        const int samples = 400;
        // Shares of the cycle with both feet off the ground: sprint and jog figures from running studies, scaled to these legs.
        foreach (var (gait, minAirborne, maxAirborne) in new[] { (ActorAction.Run, .5f, .8f), (ActorAction.Jog, .2f, .55f), (ActorAction.Walk, 0f, 0f), (ActorAction.Carry, 0f, 0f) }) {
            int airborne = 0, doubleSupport = 0;
            float lastHips = 0, steepest = 0, deepest = 0, slide = 0;
            // Where each foot's toe and heel touched down, while they stay down.
            var planted = new float?[4];
            for (int i = 0; i <= samples; i++) {
                // Two steps carry the body one stride along the ground.
                float travelled = ActorAnimation.Stride(gait) * i / samples;
                var pose = ActorAnimation.PoseAt(gait, ActorAnimation.Advance(gait, 0, travelled), true);
                int down = 0;
                for (int side = 0; side < 2; side++) {
                    var (toe, heel) = side == 0 ? ActorAnimation.SoleOf(pose.SwingL, pose.KneeL, pose.FootL) : ActorAnimation.SoleOf(pose.SwingR, pose.KneeR, pose.FootR);
                    bool touching = false;
                    for (int end = 0; end < 2; end++) {
                        var point = end == 0 ? toe : heel;
                        float clear = pose.Hips - point.Y;
                        deepest = Math.Min(deepest, clear);
                        if (clear > .002f) { planted[side * 2 + end] = null; continue; }
                        touching = true;
                        float at = travelled + point.X;
                        planted[side * 2 + end] ??= at;
                        slide = Math.Max(slide, Math.Abs(at - planted[side * 2 + end]!.Value));
                    }
                    if (touching) down++;
                }
                if (i < samples && down == 0) airborne++;
                if (i < samples && down == 2) doubleSupport++;
                if (i > 0) steepest = Math.Max(steepest, Math.Abs(pose.Hips - lastHips));
                lastHips = pose.Hips;
            }
            Require(airborne >= samples * minAirborne && airborne <= samples * maxAirborne,
                $"{gait} should be off the ground for {minAirborne:0%} to {maxAirborne:0%} of its cycle: {airborne} of {samples} samples");
            if (maxAirborne == 0) Require(doubleSupport > samples * .05f, $"{gait} should have both feet down for a moment at each step: {doubleSupport} of {samples} samples");
            Require(deepest > -.002f, $"{gait} should never sink a foot into the ground: {deepest:0.000} m");
            Require(slide < .01f, $"{gait} should keep a planted foot where it is while the body passes over it: it slid {slide:0.000} m");
            // Several centimetres up and down twice a stride moves the hips under 3 mm between these samples.
            Require(steepest < .003f, $"{gait} should carry the hips up and down smoothly, not jump: {steepest:0.0000} m between samples");
        }
        // The bounces and the time on the ground are timed for a cycle at the speed each gait moves at.
        var data = GameData.Create();
        foreach (var (gait, speed) in new[] { (ActorAction.Run, data.MoveSpeed(sprinting: true)), (ActorAction.Jog, data.MoveSpeed(sprinting: false)), (ActorAction.Walk, World.WorkerSpeed) }) {
            float cycle = ActorAnimation.Stride(gait) / speed;
            Require(Math.Abs(cycle - ActorAnimation.Duration(gait)) < .02f,
                $"{gait}'s stride should take as long at its speed as its timing assumes: {cycle:0.000} s against {ActorAnimation.Duration(gait):0.000} s");
        }
    }

    /// <summary>
    /// A blow lands its tool's working end on a mark anywhere a worker stands from what they work: the axe on a trunk 1 to 1.4 m
    /// ahead at chest height, the pick on a rock 0.9 to 1.4 m ahead low down and 1.1 to 1.4 m higher up, the hammer on a work top
    /// 0.7 to 1 m ahead and the shovel's blade into the ground 0.8 to 1.1 m ahead, up to 0.3 m to either side.
    /// Nearer than that a straight-armed swing passes over its mark, and the game finds where the swing meets what is worked instead.
    /// Every frame of the aimed loop follows the last without a jump, at 60 frames a second.
    /// </summary>
    private static void CheckABlowLandsItsToolOnItsMarkSmoothlyWithinReach() {
        var rig = new ActorRig(ActorSkin.Player);
        foreach (var (action, end, heights, nearest, farthest, aside) in new[] {
            (ActorAction.SteelChop, ActorRig.AxeHeadPoint, new[] { 1.13f }, 1, 1.4f, .3f), (ActorAction.SteelMine, ActorRig.PickPoint, new[] { .45f }, .9f, 1.4f, .3f),
            (ActorAction.SteelMine, ActorRig.PickPoint, new[] { .7f }, 1.1f, 1.4f, .3f), (ActorAction.Hammer, ActorRig.HammerFace, new[] { .84f }, .7f, 1, .3f), (ActorAction.Dig, Vector3.Zero, new[] { 0f }, .8f, 1.1f, .2f) }) {
            float strike = action switch { ActorAction.SteelChop => ActorAnimation.ChopStrike, ActorAction.SteelMine => ActorAnimation.MineStrike, ActorAction.Hammer => ActorAnimation.HammerStrike, _ => ActorAnimation.DigStrike };
            float loop = ActorAnimation.Duration(action);
            foreach (float height in heights)
                foreach (float side in new[] { -aside, 0, aside })
                    for (float ahead = nearest; ahead <= farthest + .001f; ahead += .1f) {
                        var mark = new Vector3(side, height, ahead);
                        rig.Show(ActorAnimation.PoseAt(action, strike * loop, false, mark), ActorAnimation.Tool(action), false);
                        // A shovel's blade goes into the ground there, its tip a little under it.
                        var lands = action == ActorAction.Dig ? rig.ShovelTip with { Y = 0 } : rig.Holds(end);
                        Require(lands.DistanceTo(mark) < .03f, $"{action} should land on a mark {side:0.0} m aside, {height:0.00} m up and {ahead:0.0} m ahead: it lands {lands.DistanceTo(mark):0.00} m off it");
                        var last = ActorAnimation.PoseAt(action, 0, false, mark);
                        for (int frame = 1; frame <= loop * 60; frame++) {
                            var pose = ActorAnimation.PoseAt(action, frame / 60f, false, mark);
                            var (joint, step) = new[] { ("right raise", pose.RaiseR - last.RaiseR), ("right swing out", pose.OutR - last.OutR), ("right elbow", pose.ElbowR - last.ElbowR),
                                ("left raise", pose.RaiseL - last.RaiseL), ("left swing out", pose.OutL - last.OutL), ("left elbow", pose.ElbowL - last.ElbowL), ("lean", pose.Lean - last.Lean), ("turn", pose.Twist - last.Twist) }.MaxBy(j => Math.Abs(j.Item2));
                            Require(Math.Abs(step) < 30, $"{action} aimed {side:0.0} m aside and {ahead:0.0} m ahead should move each joint under 30° a frame, not its {joint} {step:0}° at frame {frame}");
                            last = pose;
                        }
                    }
        }
        rig.Root.Free();
    }

    /// <summary>Hands pushed at points in front of the chest and face reach them, the elbow on its own side and down, outside the trunk rather than across or in it.</summary>
    private static void CheckAHandReachesItsPointWithTheElbowTheWayAsked() {
        var rig = new ActorRig(ActorSkin.Player);
        foreach (bool left in new[] { true, false })
            foreach (var target in new[] { new Vector3(.05f, .5f, .3f), new Vector3(-.05f, .6f, .25f), new Vector3(.1f, .35f, .35f), new Vector3(0, .55f, .4f) }) {
                float side = left ? 1 : -1;
                var aim = target with { X = target.X * side };
                var (raise, outward, elbow, roll) = ArmReach.ArmTo(aim, left, new Vector3(side, -1, 0), ActorRig.Palm);
                var pose = left ? new Pose(RaiseL: raise, OutL: outward, ElbowL: elbow) : new Pose(RaiseR: raise, OutR: outward, ElbowR: elbow);
                rig.Show(pose, null, false);
                rig.RollArm(left, roll);
                var hips = Vector3.Up * pose.HipHeight;
                float miss = (rig.Fist(left) - hips).DistanceTo(aim);
                var at = rig.Elbow(left) - hips;
                Require(miss < .005f, $"a {(left ? "left" : "right")} hand should reach {aim}: it missed by {miss:0.000} m");
                // Its own side of the body and below the shoulder, beside the trunk or in front of the chest, never in it.
                bool outside = at.X * side > ActorRig.ShoulderOut || at.Z > .2f;
                Require(at.X * side > .05f && at.Y < ActorRig.ShoulderUp && outside, $"a {(left ? "left" : "right")} elbow should stay out and down, reaching {aim}: it is at {at}");
            }
        rig.Root.Free();
    }

    /// <summary>
    /// On the slim build, a fist that <c>ArmReach.WithHandAt</c> places from a shrugged shoulder reaches its point.
    /// </summary>
    private static void CheckAHandReachesItsPointFromAShruggedShoulder() {
        const float up = .03f, forward = .02f;
        var build = HumanShape.Slim;
        var rig = new ActorRig(ActorRig.OutfitOf(ActorSkin.Player), build);
        foreach (bool left in new[] { true, false }) {
            float side = left ? 1 : -1;
            var aim = new Vector3(.05f * side, .55f, .3f);
            var (pose, roll) = ArmReach.WithHandAt(new Pose(), left, aim, new Vector3(side, -1, 0), build.Shoulder(left, up, forward));
            rig.Show(pose, null, false);
            rig.MoveShoulder(left, up, forward);
            rig.RollArm(left, roll);
            float miss = (rig.Fist(left) - Vector3.Up * pose.HipHeight).DistanceTo(aim);
            Require(miss < .005f, $"a {(left ? "left" : "right")} fist should reach {aim} from a shrugged shoulder: it missed by {miss:0.000} m");
        }
        rig.Root.Free();
    }

    private static void CheckActionsLoopAndWrapByTheirDuration() {
        Require(ActorAnimation.Phase(ActorAction.Chop, .25f) == .25f, "a one-second chop should be a quarter through after .25s");
        Require(ActorAnimation.Phase(ActorAction.Chop, 1) == 0, "action loops should wrap after their duration");
        Require(Math.Abs(ActorAnimation.Phase(ActorAction.Chop, -.125f) - .875f) < 1e-5f, "negative elapsed time should wrap safely");
        Require(ActorAnimation.Phase(ActorAction.Attack, ActorAnimation.Duration(ActorAction.Attack)) == 0, "punches should wrap at their own duration");
        Require(ActorAnimation.Phase(ActorAction.Idle, float.NaN) == 0, "invalid time should fall back to the first pose");
    }

    private static void CheckActorsTurnToFaceTheirMovement() {
        Require(Math.Abs(ActorAnimation.Yaw(new Vector2(0, 1), 1) - 0) < 1e-5f, "world +Y movement should face the rig's +Z forward");
        Require(Math.Abs(ActorAnimation.Yaw(new Vector2(1, 0), 0) - Mathf.Pi / 2) < 1e-5f, "eastward movement should turn a quarter turn");
        Require(Math.Abs(Math.Abs(ActorAnimation.Yaw(new Vector2(0, -1), 0)) - Mathf.Pi) < 1e-5f, "northward movement should face backward");
        Require(ActorAnimation.Yaw(Vector2.Zero, 2) == 2, "zero movement should retain the previous facing");
    }

    /// <summary>People turning while walking or running turn the head about 0.2 s before the trunk, which follows within about 0.3 s.</summary>
    private static void CheckPlayerHeadTurnsBeforeTheBody() {
        var turning = new Turning();
        float yaw = 0, quarter = Mathf.Pi / 2;
        for (int i = 0; i < 6; i++) turning.Step(ref yaw, quarter, 3, 1 / 60f, running: false);
        Require(Mathf.RadToDeg(turning.Look) > 20 && Mathf.RadToDeg(yaw) < 45, $"0.1 s into a quarter turn the head should look well ahead of a body still under half way round: look {Mathf.RadToDeg(turning.Look):0}°, body {Mathf.RadToDeg(yaw):0}°");
        for (int i = 0; i < 15; i++) turning.Step(ref yaw, quarter, 3, 1 / 60f, running: false);
        Require(Mathf.RadToDeg(yaw) > 81, $"0.35 s into a quarter turn the body should face nearly the new way: {Mathf.RadToDeg(yaw):0}°");
    }

    /// <summary>A key for straight back turns the body round through one side, never stalling or swinging between the two.</summary>
    private static void CheckPlayerTurnsHalfwayRoundOneWay() {
        var turning = new Turning();
        float yaw = 0, turned = 0;
        for (int i = 0; i < 30; i++) {
            float before = yaw;
            turning.Step(ref yaw, Mathf.Pi, 3, 1 / 60f, running: false);
            float step = Mathf.AngleDifference(before, yaw);
            Require(i == 0 || step * turned >= 0, $"a half turn should keep turning the same way, step {i}: {Mathf.RadToDeg(step):0.0}°");
            turned += step;
        }
        Require(Math.Abs(turned) > Mathf.DegToRad(170), $"0.5 s into a half turn the body should face nearly back: turned {Mathf.RadToDeg(turned):0}°");
    }

    /// <summary>A sprinter leans toward the inside of a turn, a jogger barely leans, and someone standing still turns upright.</summary>
    private static void CheckPlayerLeansIntoATurn() {
        var turning = new Turning();
        float yaw = 0;
        for (int i = 0; i < 6; i++) turning.Step(ref yaw, Mathf.Pi / 2, 6.7f, 1 / 60f, running: true);
        // Turning from +Z toward +X, the inside of the turn is the way the forward line swings.
        var inside = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
        float tilt = (Basis.FromEuler(new Vector3(0, yaw, turning.Bank)) * Vector3.Up).Dot(inside);
        Require(tilt > MathF.Sin(Mathf.DegToRad(5)), $"a sprinter turning should lean over 5° into the turn: {Mathf.RadToDeg(MathF.Asin(tilt)):0.0}°");
        var jogging = new Turning();
        yaw = 0;
        for (int i = 0; i < 30; i++) {
            jogging.Step(ref yaw, Mathf.Pi / 2, 4.2f, 1 / 60f, running: false);
            Require(Math.Abs(jogging.Bank) <= Mathf.DegToRad(1.01f), $"a jogger turning should lean at most 1°, step {i}: {Mathf.RadToDeg(jogging.Bank):0.0}°");
        }
        var standing = new Turning();
        yaw = 0;
        for (int i = 0; i < 30; i++) standing.Step(ref yaw, Mathf.Pi / 2, 0, 1 / 60f, running: false);
        Require(standing.Bank == 0, "someone turning on the spot should stay upright");
    }

    private static void CheckPlayerActionFollowsToolAndTarget() {
        var bag = new Inventory();
        bag.Add("steel_axe", 1);
        bag.Add("steel_pick", 1);

        Require(ActorAnimation.PlayerAction(ThingKind.Tree, true, true, bag) == ActorAction.Jog,
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
        Require(ActorAnimation.PlayerAction(ThingKind.Stump, true, false, bag) == ActorAction.Idle && ActorAnimation.PlayerAction(ThingKind.Bush, true, false, bag) == ActorAction.Idle,
            "a stump or a bush should not be worked at without a shovel");
        bag.Add("steel_shovel", 1);
        Require(ActorAnimation.PlayerAction(ThingKind.Stump, true, false, bag) == ActorAction.Dig && ActorAnimation.PlayerAction(ThingKind.Bush, true, false, bag) == ActorAction.Dig,
            "a carried shovel should dig at a stump and at a bush");
    }

    private static void CheckWorkerHoldingALoadCarriesIt() {
        var worker = new Worker();
        worker.Bag.Add("wood", 2);
        worker.Status = "Carrying wood → chest";
        Require(ActorAnimation.WorkerAction(worker, null, true, data) == ActorAction.Carry,
            "a worker hauling a load while moving should retain the carry action");
        Require(ActorAnimation.WorkerAction(worker, null, false, data) == ActorAction.Carry,
            "a worker holding a load while stationary should use carry pose");
    }

    private static void CheckStationWorkFollowsWorkStateNotItsWording() {
        var worker = new Worker();
        worker.Status = "Text can vary";
        worker.WorkState = WorkerWorkState.Pumping;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Reinforced }, false, data) == ActorAction.Pump,
            "bellows work state should select pumping animation");
        worker.Status = "Different text";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Reinforced }, false, data) == ActorAction.Pump,
            "rewording status should not change the pumping animation");

        worker.Status = "Station status text";
        worker.WorkState = WorkerWorkState.Hammering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, false, data) == ActorAction.Hammer,
            "anvil work should select hammering animation");
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Cooktable, Keep = "flour" }, false, data) == ActorAction.Grind,
            "grinding flour at the cooking table should select grinding with the mortar, not hammering");
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Cooktable, Keep = "dough" }, false, data) == ActorAction.Pack,
            "kneading dough at the cooking table should work it by hand as goods are packed");
    }

    private static void CheckStrikingWorkerProtestsUntilWalking() {
        var worker = new Worker();
        worker.Status = "On strike · wages owed";
        worker.WorkState = WorkerWorkState.Idle;
        worker.WageDebt = worker.Wage;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, false, data) == ActorAction.Protest
                && ActorAnimation.Tool(ActorAction.Protest) == "protest_sign",
            "striking workers should stand holding the protest sign instead of working");
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, true, data) == ActorAction.Walk,
            "a striking worker heading home should walk, not protest");
    }

    private static void CheckGatheringWorkerSwingsTheToolOfItsTarget() {
        var worker = new Worker();
        worker.Status = "Gathering wood";
        worker.WorkState = WorkerWorkState.Blocked;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Tree }, false, data) == ActorAction.Idle,
            "waiting workers should not swing tools just because they have an assignment");

        worker.Status = "Arbitrary status";
        worker.WorkState = WorkerWorkState.Gathering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Tree }, false, data) == ActorAction.Chop,
            "workers gathering trees should chop");
        worker.Status = "Arbitrary status";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Iron }, false, data) == ActorAction.Mine,
            "workers gathering iron should mine");
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Stump }, false, data) == ActorAction.Dig && ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Bush }, false, data) == ActorAction.Dig,
            "workers at a stump or a bush should dig");
        worker.Status = "Available";
        worker.WorkState = WorkerWorkState.Idle;
        Require(ActorAnimation.WorkerAction(worker, null, false, data) == ActorAction.Idle,
            "available workers should remain idle");
    }

    private static void CheckWorkerAtLunchSitsOnceThere() {
        var worker = new Worker();
        worker.WorkState = WorkerWorkState.Resting;
        worker.Bag.Add("wood", 2);
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, false, data) == ActorAction.Perch,
            "a worker at lunch should sit, even holding a load");
        worker.Id = 1;
        Require(ActorAnimation.WorkerAction(worker, null, false, data) == ActorAction.Sit,
            "every other worker should sit cross-legged on the ground rather than on a rock");
        worker.Id = 0;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = ThingKind.Anvil }, true, data) == ActorAction.Carry,
            "a worker still walking to lunch should not sit");
    }

    private static void CheckEveryActionMovesThroughDistinctPoses() {
        foreach (var action in Enum.GetValues<ActorAction>()) {
            var poses = new HashSet<Pose>();
            for (int step = 0; step < 8; step++)
                poses.Add(ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) * step / 8, action == ActorAction.Carry));
            Require(poses.Count >= 6, $"{action} should move through at least six distinct poses per loop, found {poses.Count}");
            // A fall happens once, from standing to lying down, and stays down after.
            if (action == ActorAction.FallBack) {
                Require(ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) * 1.5f, false) == ActorAnimation.PoseAt(action, ActorAnimation.Duration(action), false),
                    "a fall backward should stay lying down after it ends, as it blends into the next action");
                continue;
            }
            var start = ActorAnimation.PoseAt(action, 0, false);
            var end = ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) - .001f, false);
            Require(Math.Abs(start.RaiseR - end.RaiseR) < 2,
                $"{action} should end with its right arm where it started");
            Require(Math.Abs(start.Lean - end.Lean) < 2, $"{action} should end with its lean where it started");
            Require(Math.Abs(start.SwingL - end.SwingL) < 2,
                $"{action} should end with its left swing where it started");
        }
        var windup = ActorAnimation.PoseAt(ActorAction.AxeSwing, .38f, false);
        var strike = ActorAnimation.PoseAt(ActorAction.AxeSwing, .52f, false);
        Require(windup.RaiseR > 150 && strike.RaiseR < 100, "an axe swung in the air should rise overhead and come down");
        var back = ActorAnimation.PoseAt(ActorAction.Chop, .38f, false);
        var cut = ActorAnimation.PoseAt(ActorAction.Chop, .52f, false);
        Require(back.Twist > 30 && cut.Twist < 0 && back.RaiseR < 100, "felling should wind the axe round to the side and swing it across, not overhead");
        Require(cut.Grip == 90, "felling should turn the blade to cut sideways into the trunk");
        Require(ActorAnimation.PoseAt(ActorAction.Gather, .3f, false).KneeL > 80, "hand gathering should crouch");
        Require(ActorAnimation.PoseAt(ActorAction.PickUp, .32f * ActorAnimation.Duration(ActorAction.PickUp), false).KneeL > 80, "picking up a loose piece should crouch for it");
        var swingWalking = ActorAnimation.PoseAt(ActorAction.TorchSwing, .27f, true);
        var swingStanding = ActorAnimation.PoseAt(ActorAction.TorchSwing, .27f, false);
        Require(swingWalking.RaiseL == swingStanding.RaiseL && swingWalking.SwingL == ActorAnimation.PoseAt(ActorAction.Jog, .27f, true).SwingL,
            "a torch swing on the move should keep the swinging arm while the legs keep walking");
        Require(ActorAnimation.PoseAt(ActorAction.Sit, 0, false).Hips is > 0 and < .2f && ActorAnimation.PoseAt(ActorAction.Perch, 0, false).Hips > .25f,
            "a cross-legged worker should sit on the ground, and one on a rock should sit up on it");
        Require(ActorAnimation.Tool(ActorAction.SteelMine) == "steel_pick", "steel mining should show the steel pick");
        Require(ActorAnimation.Tool(ActorAction.Hammer) == "hammer", "hammering should show the hammer");
        Require(ActorAnimation.Tool(ActorAction.Grind) == "mortar", "grinding should show the mortar in hand");
        Require(ActorAnimation.Tool(ActorAction.Dig) == "steel_shovel", "digging should show the shovel");
        Require(ActorAnimation.Tool(ActorAction.Gather) == null, "hand gathering should show no tool");
    }

    /// <summary>
    /// A walk, a jog and a run have a pose at every moment, also the ones a hair either side of a foot landing, a quarter
    /// and three quarters into the stride, where rounding puts a leg a whole stride after its own landing.
    /// </summary>
    private static void CheckEveryMomentOfAStrideHasAPose() {
        foreach (var gait in new[] { ActorAction.Walk, ActorAction.Jog, ActorAction.Run })
            foreach (float landing in new[] { .25f, .75f }) {
                float early = landing * ActorAnimation.Duration(gait), late = early;
                for (int i = 0; i < 4000; i++) {
                    early = MathF.BitDecrement(early); late = MathF.BitIncrement(late);
                    ActorAnimation.PoseAt(gait, early, true); ActorAnimation.PoseAt(gait, late, true);
                }
            }
    }
}
