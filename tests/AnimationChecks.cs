using System;
using System.Collections.Generic;
using ChopChopMeow;
using Godot;

/// <summary>Focused checks for deterministic actor animation selection and timing.</summary>
public static class AnimationChecks {
    public static void Run() {
        CheckTiming();
        CheckFacing();
        CheckPlayerActions();
        CheckWorkerActions();
        CheckPoses();
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException($"Animation check failed: {message}");
    }

    private static void CheckTiming() {
        Require(ActorAnimation.Phase(ActorAction.Chop, .25f) == .25f, "a one-second chop should be a quarter through after .25s");
        Require(ActorAnimation.Phase(ActorAction.Chop, 1) == 0, "action loops should wrap after their duration");
        Require(Math.Abs(ActorAnimation.Phase(ActorAction.Chop, -.125f) - .875f) < 1e-5f, "negative elapsed time should wrap safely");
        Require(Math.Abs(ActorAnimation.Phase(ActorAction.Walk, .4f) - .5f) < 1e-5f, "walking should loop every .8 seconds");
        Require(ActorAnimation.Phase(ActorAction.Attack, ActorAnimation.Duration(ActorAction.Attack)) == 0, "punches should wrap at their own duration");
        Require(ActorAnimation.Phase(ActorAction.Idle, float.NaN) == 0, "invalid time should fall back to the first pose");
    }

    private static void CheckFacing() {
        Require(Math.Abs(ActorAnimation.Yaw(new Vector2(0, 1), 1) - 0) < 1e-5f, "world +Y movement should face the rig's +Z forward");
        Require(Math.Abs(ActorAnimation.Yaw(new Vector2(1, 0), 0) - Mathf.Pi / 2) < 1e-5f, "eastward movement should turn a quarter turn");
        Require(Math.Abs(Math.Abs(ActorAnimation.Yaw(new Vector2(0, -1), 0)) - Mathf.Pi) < 1e-5f, "northward movement should face backward");
        Require(ActorAnimation.Yaw(Vector2.Zero, 2) == 2, "zero movement should retain the previous facing");
    }

    private static void CheckPlayerActions() {
        var bag = new Inventory();
        bag.Add("steel_axe", 1);
        bag.Add("steel_pick", 1);

        Require(ActorAnimation.PlayerAction("tree", true, true, bag, "steel_axe") == ActorAction.Walk,
            "movement toward a resource should animate as walking");
        Require(ActorAnimation.PlayerAction("tree", true, false, bag, "steel_axe") == ActorAction.SteelChop,
            "a stationary player with a steel axe should chop a tree");
        Require(ActorAnimation.PlayerAction("iron", true, false, bag, "steel_pick") == ActorAction.SteelMine,
            "a carried steel pick should mine iron deposits");
        Require(ActorAnimation.PlayerAction("iron", true, false, bag, "steel_axe") == ActorAction.SteelMine,
            "an irrelevant axe selection should fall back to a carried pick for iron");
        Require(ActorAnimation.PlayerAction("stone", true, false, bag, "steel_axe") == ActorAction.SteelMine,
            "a carried pick should mine stone");
        Require(ActorAnimation.PlayerAction("tree", true, false, bag, "steel_pick") == ActorAction.SteelChop,
            "an irrelevant selected pick should fall back to the carried axe for trees");

        var empty = new Inventory();
        Require(ActorAnimation.PlayerAction("tree", true, false, empty, "steel_axe") == ActorAction.Gather,
            "selected tools not carried should not animate as tool use");
        var axeOnly = new Inventory();
        axeOnly.Add("steel_axe", 1);
        Require(ActorAnimation.PlayerAction("coal", true, false, axeOnly, "steel_axe") == ActorAction.Gather,
            "a carried but unsuitable axe should not animate as mining");
        Require(ActorAnimation.PlayerAction("berries", true, false, empty, null) == ActorAction.Gather,
            "hand gathering berries should use the gather action");
        Require(ActorAnimation.PlayerAction("wolf_corpse", true, false, empty, null) == ActorAction.Gather,
            "butchering a carcass should use the crouched gather action");
        Require(ActorAnimation.PlayerAction("tree", false, false, bag, "steel_axe") == ActorAction.Idle,
            "inactive gathering should leave the player idle");
    }

    private static void CheckWorkerActions() {
        var worker = new Worker();
        worker.Bag.Add("wood", 2);
        worker.Status = "Carrying wood → chest";
        Require(ActorAnimation.WorkerAction(worker, null, true) == ActorAction.Carry,
            "a worker hauling a load while moving should retain the carry action");
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Carry,
            "a worker holding a load while stationary should use carry pose");

        worker.Bag.Remove("wood", 2);
        worker.Status = "Text can vary";
        worker.WorkState = WorkerWorkState.Pumping;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "reinforced" }, false) == ActorAction.Pump,
            "bellows work state should select pumping animation");
        worker.Status = "Different text";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "reinforced" }, false) == ActorAction.Pump,
            "rewording status should not change the pumping animation");

        worker.Status = "Station status text";
        worker.WorkState = WorkerWorkState.Hammering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "anvil" }, false) == ActorAction.Hammer,
            "anvil work should select hammering animation");
        worker.Status = "On strike · wages owed";
        worker.WorkState = WorkerWorkState.Idle;
        worker.WageDebt = worker.Wage;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "anvil" }, false) == ActorAction.Protest
                && ActorAnimation.Tool(ActorAction.Protest) == "protest_sign",
            "striking workers should stand holding the protest sign instead of working");
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "anvil" }, true) == ActorAction.Walk,
            "a striking worker heading home should walk, not protest");
        worker.WageDebt = 0;
        worker.Status = "Gathering wood";
        worker.WorkState = WorkerWorkState.Blocked;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "tree" }, false) == ActorAction.Idle,
            "waiting workers should not swing tools just because they have an assignment");

        worker.Status = "Arbitrary status";
        worker.WorkState = WorkerWorkState.Gathering;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "tree" }, false) == ActorAction.Chop,
            "workers gathering trees should chop");
        worker.Status = "Arbitrary status";
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "iron" }, false) == ActorAction.Mine,
            "workers gathering iron should mine");
        worker.WorkState = WorkerWorkState.Resting;
        worker.Bag.Add("wood", 2);
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "anvil" }, false) == ActorAction.Perch,
            "a worker at lunch should sit, even holding a load");
        worker.Id = 1;
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Sit,
            "every other worker should sit cross-legged on the ground rather than on a rock");
        worker.Id = 0;
        Require(ActorAnimation.WorkerAction(worker, new Thing { Kind = "anvil" }, true) == ActorAction.Carry,
            "a worker still walking to lunch should not sit");
        worker.Bag.Remove("wood", 2);
        worker.Status = "Available";
        worker.WorkState = WorkerWorkState.Idle;
        Require(ActorAnimation.WorkerAction(worker, null, false) == ActorAction.Idle,
            "available workers should remain idle");
    }

    private static void CheckPoses() {
        foreach (var action in Enum.GetValues<ActorAction>()) {
            var poses = new HashSet<Pose>();
            for (int step = 0; step < 8; step++)
                poses.Add(ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) * step / 8, action == ActorAction.Carry));
            Require(poses.Count >= 6, $"{action} should move through at least six distinct poses per loop, found {poses.Count}");
            var start = ActorAnimation.PoseAt(action, 0, false);
            var end = ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) - .001f, false);
            Require(Math.Abs(start.RaiseR - end.RaiseR) < 2 && Math.Abs(start.Lean - end.Lean) < 2 && Math.Abs(start.SwingL - end.SwingL) < 2,
                $"{action} should loop without a visible jump");
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
        Require(ActorAnimation.PoseAt(ActorAction.Sit, 0, false).Seat is > 0 and < .2f && ActorAnimation.PoseAt(ActorAction.Perch, 0, false).Seat == 0,
            "a cross-legged worker should sit on the ground, and one on a rock should not");
        Require(ActorAnimation.Tool(ActorAction.SteelMine) == "steel_pick" && ActorAnimation.Tool(ActorAction.Hammer) == "hammer" && ActorAnimation.Tool(ActorAction.Gather) == null,
            "each action should show its own tool, and hand gathering none");
    }
}
