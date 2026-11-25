using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Lower onto a supporting hand, roll onto the bed, and use the same contacts in reverse when getting up.</summary>
internal sealed class StrawBedView {
    private float down;
    private Thing? bed;

    private readonly record struct Key(float At, Vector3 Hips, float Tip, float Roll, Pose Pose);
    private static readonly Key[] Keys = [
        new(0, new(0, new Pose().HipHeight + StrawBedModel.Top, 0), 0, 0, new Pose()),
        new(.30f, new(0, .57f, -.12f), 0, 0, new Pose(Lean: 23, Head: 19, RaiseL: 33, OutL: 18, RaiseR: 46)),
        new(.55f, new(0, .23f, -.31f), -8, 0, new Pose(Lean: -3, Head: 12, RaiseL: -25, OutL: 28, RaiseR: 62, ElbowR: 29,
            SwingL: 90, SwingR: 90, KneeL: 105, KneeR: 105)),
        new(.78f, new(.02f, .27f, -.03f), -61, 63, new Pose(Head: 17, RaiseL: 69, ElbowL: 75, OutL: 22, RaiseR: 63,
            ElbowR: 59, OutR: -18, SwingL: 44, SwingR: 42, KneeL: 54, KneeR: 66)),
        new(1, new(0, .255f, .06f), -90, 84, new Pose(Head: 7, RaiseL: 122, OutL: 19, ElbowL: 43, RaiseR: 40,
            OutR: -30, ElbowR: 67, SwingL: 16, SwingR: 23, KneeL: 23, KneeR: 29, FootL: 5, FootR: 12))
    ];

    /// <summary>Draws a sleeper, or a player getting up from a bed they have not walked away from.</summary>
    internal bool Show(ActorRig rig, PlayerState who, Thing? sleepingPlace, float dt) {
        if (who.Mauled || who.RespawnIn > 0 || who.Sheltered != 0) { down = 0; bed = null; return false; }
        if (sleepingPlace is { Kind: ThingKind.StrawBed } mat) {
            if (bed != mat) { bed = mat; down = 0; }
            down = Math.Min(1, down + dt / StrawBed.LieDownSeconds);
        } else {
            if (bed == null || who.Pos.DistanceSquaredTo(bed.Pos) > .18f * .18f) { down = 0; bed = null; return false; }
            down = Math.Max(0, down - dt / StrawBed.LieDownSeconds);
            if (down == 0) { bed = null; return false; }
        }
        Draw(rig, bed, down);
        return true;
    }

    private static void Draw(ActorRig rig, Thing mat, float u) {
        int next = 1;
        while (Keys[next].At < u) next++;
        var a = Keys[next - 1]; var b = Keys[next];
        float t = Mathf.SmoothStep(0, 1, (u - a.At) / (b.At - a.At));
        var hips = a.Hips.Lerp(b.Hips, t);
        float tip = Mathf.Lerp(a.Tip, b.Tip, t);
        var tilt = new Basis(Vector3.Right, Mathf.DegToRad(tip)) *
            new Basis(Vector3.Up, Mathf.DegToRad(Mathf.Lerp(a.Roll, b.Roll, t)));
        var yaw = new Basis(Vector3.Up, -mat.Rotation);
        const float rigHips = FallBackView.FallHips;
        var pose = Pose.Lerp(a.Pose, b.Pose, t) with { Hips = rigHips };
        // The boots stay planted while the knees take the body's weight. They lift once the supporting arm takes over.
        float plant = 1 - Mathf.SmoothStep(.55f, .70f, u);
        if (plant > 0) {
            float sole = ActorAnimation.Sole + StrawBedModel.Top;
            var ankle = tilt.Inverse() * (new Vector3(0, sole, 0) - hips);
            var (swing, knee) = ActorAnimation.LegTo(ankle.Z, -ankle.Y);
            float foot = tip + swing - knee;
            pose = pose with {
                SwingL = Mathf.Lerp(pose.SwingL, swing, plant),
                SwingR = Mathf.Lerp(pose.SwingR, swing, plant),
                KneeL = Mathf.Lerp(pose.KneeL, knee, plant),
                KneeR = Mathf.Lerp(pose.KneeR, knee, plant),
                FootL = Mathf.Lerp(pose.FootL, foot, plant),
                FootR = Mathf.Lerp(pose.FootR, foot, plant)
            };
        }
        var at = WorldView.To3(mat.Pos);
        var root = new Transform3D(yaw * tilt, at + yaw * (hips - tilt * (Vector3.Up * rigHips)));
        float hand = Mathf.SmoothStep(.30f, .55f, u) * (1 - Mathf.SmoothStep(.61f, .86f, u));
        if (hand > 0) {
            var contact = at + yaw * new Vector3(.27f, StrawBedModel.Top + .037f, -.48f);
            (pose, float roll) = ArmReach.Reaching(pose, contact, root, left: true, weight: hand, elbowToward: new Vector3(1, -1, -.25f));
            pose = pose with { ArmRollL = roll };
        }
        rig.Root.Transform = root;
        rig.Show(pose, null, false);
    }
}
