using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Squat on planted boots, put weight on one hand and the seat, then roll onto the side.</summary>
internal sealed class StrawBedView {
    private float down;
    private Thing? bed;

    private const float RigHips = FallBackView.FallHips;
    internal const float HandOn = .43f, HandOff = .78f, BothBootsOnUntil = .57f;
    private const float BootHalfWidth = .059f;
    private static readonly Vector3 Handhold = new(.31f, StrawBedModel.Top + .005f, -.235f);
    private static readonly Basis SideBoot = new Basis(Vector3.Right, -Mathf.Pi / 2) * new Basis(Vector3.Up, Mathf.DegToRad(84));

    private readonly record struct Key(float At, Vector3 Hips, float Tip, float Roll, Pose Pose);
    private static readonly Key[] Keys = [
        new(0, new(0, new Pose().HipHeight, 0), 0, 0, new Pose()),
        new(.24f, new(-.02f, .585f, -.09f), 0, 0, new Pose(Lean: 29, Head: 15, Sway: -8, RaiseR: 22)),
        new(.43f, new(.018f, .285f, -.22f), -3, 0, new Pose(Lean: 39, Head: 20, Sway: -28, RaiseR: 34, ElbowR: 26)),
        new(.53f, new(.03f, .117f, -.29f), -9, 7, new Pose(Lean: 25, Head: 18, Sway: -25, RaiseR: 42, ElbowR: 51)),
        new(.65f, new(.055f, .128f, -.22f), -33, 30, new Pose(Lean: 13, Head: 18, Sway: -13, RaiseR: 48, ElbowR: 66)),
        new(.81f, new(.04f, .152f, -.055f), -70, 70, new Pose(Lean: 3, Head: 11, RaiseL: 93, OutL: 22, ElbowL: 65,
            RaiseR: 48, OutR: -22, ElbowR: 65)),
        new(1, new(0, .158f, .065f), -90, 84, new Pose(Head: 7, RaiseL: 115, OutL: 24, ElbowL: 55,
            RaiseR: 40, OutR: -30, ElbowR: 67))
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

    /// <summary>Draws the same support path in either direction on a mat at any allowed rotation.</summary>
    internal static void Draw(ActorRig rig, Thing mat, float u) {
        int next = 1;
        while (Keys[next].At < u) next++;
        var a = Keys[next - 1]; var b = Keys[next];
        var before = Keys[Math.Max(0, next - 2)]; var after = Keys[Math.Min(Keys.Length - 1, next + 1)];
        float h = b.At - a.At, t = (u - a.At) / h;
        var hips = new Vector3(Curve(before.Hips.X, a.Hips.X, b.Hips.X, after.Hips.X),
            StrawBedModel.Top + Curve(before.Hips.Y, a.Hips.Y, b.Hips.Y, after.Hips.Y), Curve(before.Hips.Z, a.Hips.Z, b.Hips.Z, after.Hips.Z));
        float tip = Curve(before.Tip, a.Tip, b.Tip, after.Tip), roll = Curve(before.Roll, a.Roll, b.Roll, after.Roll);
        var tilt = new Basis(Vector3.Right, Mathf.DegToRad(tip)) * new Basis(Vector3.Up, Mathf.DegToRad(roll));
        var bedFrame = new Transform3D(new Basis(Vector3.Up, -mat.Rotation), WorldView.To3(mat.Pos));
        var root = new Transform3D(tilt, hips - tilt * (Vector3.Up * RigHips));
        var pose = Pose.Lerp(a.Pose, b.Pose, Mathf.SmoothStep(0, 1, t)) with {
            Hips = RigHips,
            Lean = Curve(before.Pose.Lean, a.Pose.Lean, b.Pose.Lean, after.Pose.Lean),
            Sway = Curve(before.Pose.Sway, a.Pose.Sway, b.Pose.Sway, after.Pose.Sway),
            Head = Curve(before.Pose.Head, a.Pose.Head, b.Pose.Head, after.Pose.Head)
        };
        var fromBed = root.AffineInverse();
        float hand = Mathf.SmoothStep(.23f, HandOn, u) * (1 - Mathf.SmoothStep(HandOff, .96f, u));
        if (hand > 0) {
            var grip = ArmReach.ToTrunk(pose, fromBed * Handhold);
            var (raise, outward, elbow, armRoll) = ArmReach.ArmTo(grip, true, new Vector3(1, -.3f, -.8f),
                ActorRig.Palm, forearm: ActorRig.Forearm + ActorRig.HandPadReach);
            raise = pose.RaiseL + Mathf.RadToDeg(Mathf.AngleDifference(Mathf.DegToRad(pose.RaiseL), Mathf.DegToRad(raise)));
            pose = ArmReach.WithArm(pose, true, raise, outward, elbow, hand) with { ArmRollL = armRoll * hand };
        }
        rig.Root.Transform = bedFrame * root;
        rig.Show(pose, null, false);
        Boot(true, .64f, 1, .15f, .012f, .73f, 0);
        Boot(false, BothBootsOnUntil, .95f, .17f, -.06f, .63f, .089f);

        // Preserve velocity at passing keys, without overshooting the low point that takes the player's weight.
        float Curve(float prev, float start, float end, float following) {
            float gradient = (end - start) / h;
            float enter = a.At == before.At ? 0 : Slope((start - prev) / (a.At - before.At), gradient) * h;
            float exit = b.At == after.At ? 0 : Slope(gradient, (following - end) / (after.At - b.At)) * h;
            return Mathf.CubicInterpolate(start, end, end - 2 * enter, start + 2 * exit, t);
        }

        // Each boot stays where it bears weight, then lifts and settles independently as the hips turn.
        void Boot(bool left, float release, float land, float lift, float restX, float restZ, float stack) {
            float q = Math.Clamp((u - release) / (land - release), 0, 1);
            float toePath = Mathf.SmoothStep(0, 1, q);
            var boot = Basis.Identity.Slerp(SideBoot, toePath);
            float bottom = ActorAnimation.AboveSole * boot.Y.Y - Math.Min(ActorAnimation.Toe * boot.Z.Y, -ActorAnimation.Heel * boot.Z.Y)
                + BootHalfWidth * Math.Abs(boot.X.Y);
            float arc = 4 * q * (1 - q);
            var ankle = new Vector3(Mathf.Lerp(left ? ActorAnimation.LegSide : -ActorAnimation.LegSide, restX, toePath),
                StrawBedModel.Top + bottom + lift * arc * arc + stack * toePath, restZ * toePath);
            rig.PlaceBoot(left, fromBed * ankle, fromBed.Basis * boot);
        }
    }

    private static float Slope(float left, float right) => left * right <= 0 ? 0 : 2 * left * right / (left + right);
}
