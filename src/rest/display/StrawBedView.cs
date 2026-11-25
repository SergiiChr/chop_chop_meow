using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shift from a crouch to one hip, then lower through a palm and forearm onto the side.</summary>
internal sealed class StrawBedView {
    private float down;
    private Thing? bed;

    private const float RigHips = FallBackView.FallHips;
    internal const float HandOn = .43f, ForearmOn = .72f, SecondHandOn = .79f, SecondHandOff = .89f, BothBootsOnUntil = .57f;
    private const float BootHalfWidth = .059f, ElbowRadius = .043f;
    private static readonly Vector3 Handhold = new(.275f, StrawBedModel.Top + .009f, -.255f);
    private static readonly Vector3 SecondHandhold = new(.20f, StrawBedModel.Top + .01f, -.15f);
    private static readonly Vector3 RestingHand = new(.16f, StrawBedModel.Top + .03f, -.36f);
    private static readonly Transform3D TorchStand = new(new Basis(Vector3.Right, -Mathf.Pi / 2), new Vector3(-.51f, .14f, -.2f));
    private static readonly Basis SideBoot = new Basis(Vector3.Right, -Mathf.Pi / 2) * new Basis(Vector3.Up, Mathf.DegToRad(84));

    private readonly record struct Key(float At, Vector3 Hips, float Recline, float Turn, Pose Pose);
    private static readonly Key[] Keys = [
        new(0, new(0, new Pose().HipHeight, 0), 0, 0, new Pose()),
        new(.23f, new(-.03f, .59f, -.065f), 0, 0, new Pose(Lean: 23, Head: 12, Sway: -7, RaiseR: 13, ElbowR: 24)),
        new(HandOn, new(.02f, .282f, -.24f), -4, 3, new Pose(Lean: 41, Head: 17, Sway: -25, RaiseR: 19, ElbowR: 41)),
        new(.52f, new(.045f, .12f, -.315f), -13, 25, new Pose(Lean: 20, Head: 21, Sway: -21, RaiseR: 18, ElbowR: 54)),
        new(.60f, new(.06f, .124f, -.296f), -32, 55, new Pose(Lean: 12, Head: 19, Sway: -9, RaiseR: 26, ElbowR: 60)),
        new(.69f, new(.052f, .132f, -.20f), -51, 75, new Pose(Lean: 5, Head: 15, Sway: -4, RaiseR: 36, ElbowR: 62)),
        new(.79f, new(.04f, .142f, -.09f), -69, 82, new Pose(Lean: 1, Head: 11, Sway: 5, RaiseR: 40, ElbowR: 67)),
        new(.88f, new(.016f, .151f, .008f), -82, 84, new Pose(Twist: 9, Head: 14, Sway: 6, RaiseR: 40, ElbowR: 67)),
        new(1, new(0, .155f, .065f), -90, 84, new Pose(Twist: 36, Head: 25, Sway: 2, RaiseR: 40, ElbowR: 67))
    ];

    internal void Clear() { down = 0; bed = null; }

    /// <summary>Walking waits for the last supported pose, but a forced move or fall may interrupt it.</summary>
    internal bool Rising(PlayerState who) => who.SleepingAt == 0 && down > 0 && bed is Thing shown &&
        CanPose(who) && who.Pos.DistanceSquaredTo(shown.Pos) <= .18f * .18f;

    private static bool CanPose(PlayerState who) => !who.Mauled && who.RespawnIn <= 0 && who.Sheltered == 0 && who.Door == Doorway.None;

    /// <summary>Draws a sleeper, then gives the standing pose the straw bed's bearing when the rise finishes.</summary>
    internal bool Show(ActorRig rig, ActorState actor, PlayerState who, Thing? sleepingPlace, float dt) {
        if (!CanPose(who)) { Clear(); return false; }
        if (sleepingPlace is { Kind: ThingKind.StrawBed } place) {
            if (bed != place) { bed = place; down = 0; }
            down = Math.Min(1, down + dt / StrawBed.LieDownSeconds);
        } else {
            if (bed == null || !Rising(who)) { Clear(); return false; }
            down = Math.Max(0, down - dt / StrawBed.LieDownSeconds);
            if (down == 0) {
                actor.Yaw = -bed.Rotation;
                actor.Turning.Hold(actor.Yaw);
                Clear();
                return false;
            }
        }
        Draw(rig, bed, down, who.TorchLit);
        return true;
    }

    /// <summary>Draws the same support path in either direction on a straw bed at any allowed rotation.</summary>
    internal static void Draw(ActorRig rig, Thing bed, float u, bool lit = false) {
        int next = 1;
        while (Keys[next].At < u) next++;
        var a = Keys[next - 1]; var b = Keys[next];
        var before = Keys[Math.Max(0, next - 2)]; var after = Keys[Math.Min(Keys.Length - 1, next + 1)];
        float h = b.At - a.At, t = (u - a.At) / h;
        var hips = new Vector3(Curve(before.Hips.X, a.Hips.X, b.Hips.X, after.Hips.X),
            StrawBedModel.Top + Curve(before.Hips.Y, a.Hips.Y, b.Hips.Y, after.Hips.Y), Curve(before.Hips.Z, a.Hips.Z, b.Hips.Z, after.Hips.Z));
        float recline = Curve(before.Recline, a.Recline, b.Recline, after.Recline), turn = Curve(before.Turn, a.Turn, b.Turn, after.Turn);
        var tilt = new Basis(Vector3.Right, Mathf.DegToRad(recline)) * new Basis(Vector3.Up, Mathf.DegToRad(turn));
        var bedFrame = new Transform3D(new Basis(Vector3.Up, -bed.Rotation), WorldView.To3(bed.Pos));
        var root = new Transform3D(tilt, hips - tilt * (Vector3.Up * RigHips));
        var pose = Pose.Lerp(a.Pose, b.Pose, Mathf.SmoothStep(0, 1, t)) with {
            Hips = RigHips,
            Lean = Curve(before.Pose.Lean, a.Pose.Lean, b.Pose.Lean, after.Pose.Lean),
            Sway = Curve(before.Pose.Sway, a.Pose.Sway, b.Pose.Sway, after.Pose.Sway),
            Twist = Curve(before.Pose.Twist, a.Pose.Twist, b.Pose.Twist, after.Pose.Twist),
            Head = Curve(before.Pose.Head, a.Pose.Head, b.Pose.Head, after.Pose.Head)
        };
        var fromBed = root.AffineInverse();
        var toTrunk = (tilt * ArmReach.TrunkBasis(pose)).Transposed();
        float settle = Mathf.SmoothStep(SecondHandOff, 1, u);
        var relaxed = SecondHandhold.Lerp(RestingHand, settle) + Vector3.Up * (.09f * settle * (1 - settle));
        Plant(true, Handhold, Mathf.SmoothStep(.23f, HandOn, u));
        Plant(false, relaxed, Mathf.SmoothStep(.56f, SecondHandOn, u));
        rig.Root.Transform = bedFrame * root;
        rig.Show(pose, null, false);
        rig.TipHead(-23 * settle);
        if (lit) rig.SetBedTorch(fromBed * TorchStand);
        Boot(true, BothBootsOnUntil, .88f, .025f, .012f, .66f, 0);
        Boot(false, .62f, .92f, .035f, -.045f, .60f, .095f);

        void Plant(bool left, Vector3 palm, float weight) {
            if (weight <= 0) return;
            var grip = toTrunk * (palm - hips);
            var bend = left ? new Vector3(.7f, -.18f, -1) : new Vector3(.7f, 0, .7f);
            ArmReach.ElbowSupport? floor = left ? new(toTrunk * Vector3.Up, StrawBedModel.Top + ElbowRadius - hips.Y,
                Mathf.SmoothStep(.59f, ForearmOn, u)) : null;
            var (raise, outward, elbow, roll) = ArmReach.ArmTo(grip, left, toTrunk * bend,
                ActorRig.Palm, ActorRig.Forearm + ActorRig.HandPadReach, floor);
            float idle = left ? pose.RaiseL : pose.RaiseR;
            raise = idle + Mathf.RadToDeg(Mathf.AngleDifference(Mathf.DegToRad(idle), Mathf.DegToRad(raise)));
            pose = ArmReach.WithArm(pose, left, raise, outward, elbow, weight);
            pose = left ? pose with { ArmRollL = roll * weight } : pose with { ArmRollR = roll * weight };
        }

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
