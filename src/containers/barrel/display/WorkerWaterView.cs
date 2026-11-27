using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Fits a worker's feet, hands and vessel to the ladder, roof and water source.</summary>
internal static class WorkerWaterView {
    private const float LadderShare = .69f, RoofEdgeShare = .82f;
    private const float RungTop = .02f, StandingToe = .155f;
    private static readonly Basis TowardRoof = new(Vector3.Up, -Mathf.Pi / 2);
    private static readonly Vector3 TravelGrip = new(-.34f, .80f, .13f);

    public static void Show(ActorRig rig, Vector2 at, Vector3 target, WorkerWaterAction action, bool roof, float progress, string vessel, bool filled, bool ground = false, float? facing = null, float rotation = 0) {
        var origin = new Vector3(at.X, 0, at.Y);
        if (roof) {
            ShowOnCabin(rig, action, progress, vessel, filled);
            rig.Root.Transform = new Transform3D(new Basis(Vector3.Up, -rotation), target) * rig.Root.Transform;
            return;
        }
        var toward = target - origin;
        float yaw = facing ?? ActorAnimation.Yaw(new Vector2(toward.X, toward.Z), 0);
        rig.Root.Transform = new Transform3D(new Basis(Vector3.Up, yaw), origin);
        var opening = rig.Root.Transform.AffineInverse() * target;
        WorkAtOpening(rig, vessel, filled, opening, progress, action == WorkerWaterAction.Fill, roof: false, ground);
    }

    public static void Carry(ActorRig rig, Pose pose, string vessel, bool filled, bool lit = false) =>
        rig.HoldWater(pose, vessel, VesselAtGrip(vessel, TravelGrip), TravelGrip, lit: lit, filled: filled);

    private static void ShowOnCabin(ActorRig rig, WorkerWaterAction action, float progress, string vessel, bool filled) {
        if (action == WorkerWaterAction.Pour) {
            rig.Root.Transform = new Transform3D(TowardRoof, CabinWaterModel.PourStand);
            var opening = TowardRoof.Inverse() * (CabinWaterModel.FillOpening - CabinWaterModel.PourStand);
            WorkAtOpening(rig, vessel, filled, opening, progress, fill: false, roof: true);
            return;
        }

        float climb = action == WorkerWaterAction.ClimbDown ? 1 - progress : progress;
        if (climb > RoofEdgeShare) {
            // Take two short steps across the roof.
            // Going back, the worker feels for the ladder without turning away.
            float across = (climb - RoofEdgeShare) / (1 - RoofEdgeShare);
            var entry = CabinWaterModel.RoofEntry;
            var stand = CabinWaterModel.PourStand;
            var onRoof = entry.Lerp(stand, across);
            onRoof.Y = CabinShape.RoofSurface(onRoof.Z);
            rig.Root.Transform = new Transform3D(TowardRoof, onRoof);
            float walk = across * ActorAnimation.Duration(ActorAction.Walk);
            var pose = ActorAnimation.PoseAt(ActorAction.Walk, walk, true);
            float ease = Math.Min(Mathf.SmoothStep(0, .15f, across), 1 - Mathf.SmoothStep(.85f, 1, across));
            Carry(rig, Pose.Lerp(new Pose(), pose, ease), vessel, filled);
            return;
        }

        var foot = CabinWaterModel.LadderApproach;
        if (climb > LadderShare) {
            float step = (climb - LadderShare) / (RoofEdgeShare - LadderShare);
            float move = Mathf.SmoothStep(0, 1, step);
            float top = Rung(CabinWaterModel.RungCount - 1);
            var entry = CabinWaterModel.RoofEntry;
            var atEdge = new Vector3(foot.X, top - .20f, foot.Z).Lerp(entry, move);
            rig.Root.Transform = new Transform3D(TowardRoof, atEdge);
            float lead = Mathf.SmoothStep(0, .63f, step), follow = Mathf.SmoothStep(.48f, 1, step);
            float toeL = atEdge.X - Mathf.Lerp(CabinWaterModel.LadderX, entry.X - .15f, lead);
            float toeR = atEdge.X - Mathf.Lerp(CabinWaterModel.LadderX, entry.X - .15f, follow);
            float leftY = Mathf.Lerp(Rung(CabinWaterModel.RungCount - 2), entry.Y, lead) + .14f * MathF.Sin(Mathf.Pi * lead);
            float rightY = Mathf.Lerp(top, entry.Y, follow) + .10f * MathF.Sin(Mathf.Pi * follow);
            var mount = LadderFeet(Mathf.Lerp(.62f, .82f, move), toeL, leftY - atEdge.Y,
                rightY - atEdge.Y, toeR, lean: 18 * (1 - move));
            var rail = RailGrip() - atEdge;
            var handOff = (TowardRoof.Inverse() * rail).Lerp(new Vector3(.22f, .73f, .07f), Mathf.SmoothStep(.42f, 1, step));
            rig.HoldWater(mount, vessel, VesselAtGrip(vessel, TravelGrip), TravelGrip, handOff, filled: filled);
            return;
        }

        float rungs = climb / LadderShare * CabinWaterModel.RungCount;
        int stepIndex = Math.Min((int)rungs, CabinWaterModel.RungCount - 1);
        float phase = rungs - stepIndex, shift = Mathf.SmoothStep(0, 1, phase);
        float fixedRung = Rung(stepIndex - 1), nextRung = Rung(stepIndex);
        float swinging = Mathf.Lerp(Rung(stepIndex - 2), nextRung, shift) + .10f * MathF.Sin(Mathf.Pi * phase);
        float nearRoof = Mathf.SmoothStep(CabinWaterModel.RungCount - 2, CabinWaterModel.RungCount, rungs);
        float height = Math.Max(0, Mathf.Lerp(fixedRung, nextRung, phase) - .10f - .10f * nearRoof);
        rig.Root.Transform = new Transform3D(TowardRoof, foot + Vector3.Up * height);
        float footAhead = foot.X - CabinWaterModel.LadderX;
        bool leftSteps = stepIndex % 2 == 0;
        float raised = swinging - height, planted = fixedRung - height;
        float lifting = .065f * MathF.Sin(Mathf.Pi * phase);
        float toeSwing = Mathf.Lerp(stepIndex < 2 ? StandingToe : footAhead, footAhead, shift) - lifting;
        float toePlant = stepIndex == 0 ? StandingToe : footAhead;
        var climbing = LadderFeet(Mathf.Lerp(.73f, .62f, nearRoof), leftSteps ? toeSwing : toePlant,
            leftSteps ? raised : planted, leftSteps ? planted : raised, leftSteps ? toePlant : toeSwing,
            lean: Mathf.Lerp(11, 18, nearRoof));

        // One hand moves to the next rung and finally onto the rail above the roof.
        // The other carries the vessel.
        float reach = Mathf.SmoothStep(.50f, .92f, phase);
        var handPoint = HandRung(stepIndex + 2).Lerp(HandRung(stepIndex + 3), reach);
        var hand = TowardRoof.Inverse() * (handPoint - foot - Vector3.Up * height);
        hand.Z -= .075f * MathF.Sin(Mathf.Pi * reach);
        rig.HoldWater(climbing, vessel, VesselAtGrip(vessel, TravelGrip), TravelGrip, hand, filled: filled);
    }

    private static float Rung(int index) => index < 0 ? 0 : CabinWaterModel.FirstRung + index * CabinWaterModel.RungSpacing + RungTop;

    private static Vector3 HandRung(int index) => index >= CabinWaterModel.RungCount ? RailGrip() :
        new Vector3(CabinWaterModel.LadderX, Rung(index), CabinWaterModel.LadderZ + CabinWaterModel.LadderWidth * .28f);

    private static Vector3 RailGrip() {
        float z = CabinWaterModel.LadderZ + CabinWaterModel.LadderWidth / 2;
        return new Vector3(CabinWaterModel.LadderX - .07f, CabinShape.RoofSurface(z) + .43f, z);
    }

    private static Pose LadderFeet(float hips, float toeL, float yL, float yR, float toeR, float lean) {
        var left = ActorAnimation.OnToe(toeL, hips - yL, 0);
        var right = ActorAnimation.OnToe(toeR, hips - yR, 0);
        return new Pose(Lean: lean, Head: -lean, Hips: hips, SwingL: left.Swing, KneeL: left.Knee, FootL: left.Foot,
            SwingR: right.Swing, KneeR: right.Knee, FootR: right.Foot);
    }

    private static Transform3D VesselAtGrip(string vessel, Vector3 grip) =>
        new(Basis.Identity, grip - (Bucket.Is(vessel) ? VesselShape.BucketGrip : ActorRig.BottleGrip));

    private static void WorkAtOpening(ActorRig rig, string vessel, bool filled, Vector3 opening, float progress, bool fill, bool roof, bool ground = false) {
        float extend = Mathf.SmoothStep(0, .30f, progress) * (1 - Mathf.SmoothStep(.72f, 1, progress));
        float tip = Mathf.SmoothStep(.23f, .43f, progress) * (1 - Mathf.SmoothStep(.61f, .79f, progress));
        bool bucket = Bucket.Is(vessel);
        var left = bucket ? new Vector3(.105f, .06f, -.015f) : new Vector3(.082f, .14f, -.01f);
        var mouth = bucket ? new Vector3(0, VesselShape.BucketRim, VesselShape.BucketRadius - .012f) : ActorRig.BottleMouth;
        bool tap = fill && opening.Y > .12f;
        bool scooping = fill && !tap;
        if (scooping) opening.Z = Math.Clamp(opening.Z, .25f, .48f);
        float angle = Mathf.DegToRad((tap ? bucket ? 0 : 57 : scooping ? bucket ? 42 : 84 : 112) * tip);
        var turn = new Basis(Vector3.Right, angle);
        var bail = new Basis(Vector3.Right, -.90f * angle);
        var grip = bucket ? VesselShape.BucketHinge + bail * (VesselShape.BucketGrip - VesselShape.BucketHinge) : ActorRig.BottleGrip;
        var rest = VesselAtGrip(vessel, TravelGrip);
        var spout = opening + Vector3.Up * (tap ? -.04f : fill ? .015f : ground ? .48f : .16f + .045f * (1 - tip));
        var inReach = spout - turn * mouth;
        if (tap) inReach.Y = Math.Max(inReach.Y, bucket ? .005f : .09f * MathF.Sin(angle));
        var held = new Transform3D(turn, rest.Origin.Lerp(inReach, extend));
        float crouch = roof ? .13f : fill ? .45f : .18f;
        float hips = .77f - crouch * extend;
        var squat = LadderFeet(hips, .17f, 0, 0, .12f, lean: (roof ? 31 : fill ? 34 : 29) * extend)
            with { Head = 7 + 22 * extend };
        Vector3? support = extend > .1f && (!scooping || bucket)
            ? new Vector3(.22f, .73f, .07f).Lerp(held * left, Mathf.SmoothStep(.1f, .4f, extend)) : null;
        var correction = rig.HoldWater(Pose.Lerp(new Pose(), squat, Mathf.SmoothStep(0, .25f, extend)), vessel, held, held * grip, support, bail, filled: filled);
        if (!fill && tip > .91f) rig.WaterStream(held * mouth + correction, opening, width: ground ? 1.65f : 1);
        if (tap && tip > .91f) rig.WaterStream(opening, held * mouth + correction);
    }
}
