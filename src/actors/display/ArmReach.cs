using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Putting an actor's hand on a point: the arm's joint angles for it, and a pose with the arm moved there.</summary>
public static class ArmReach {
    /// <summary>
    /// The arm raise, outward swing and elbow bend, in degrees, that put a fist at <paramref name="grip"/>, measured from the hips in the trunk's own frame.
    /// With <paramref name="palm"/> it is what the fist holds that goes there, that far ahead of the forearm's line; with
    /// <paramref name="along"/> that point is that far from the elbow along the forearm's line, as the end of a tool held in line with it is.
    /// </summary>
    public static (float Raise, float Out, float Elbow) ArmTo(Vector3 grip, bool left, float palm = 0, float along = ActorRig.Forearm) {
        var reach = grip - HumanShape.Standard.Shoulder(left);
        // A held thing ahead of the forearm's line is reached by a slightly longer forearm bent a little further.
        const float upper = ActorRig.UpperArm;
        float fore = MathF.Sqrt(along * along + palm * palm);
        // The elbow folds until the fist is that far from the shoulder, then the shoulder swings the bent arm onto the point.
        float bend = MathF.Acos(Math.Clamp((reach.LengthSquared() - upper * upper - fore * fore) / (2 * upper * fore), -1, 1));
        float straight = upper + fore * MathF.Cos(bend), ahead = fore * MathF.Sin(bend);
        float outward = MathF.Asin(Math.Clamp((left ? reach.X : -reach.X) / straight, -1, 1));
        float raise = Mathf.RadToDeg(MathF.Atan2(reach.Z, -reach.Y) - MathF.Atan2(ahead, straight * MathF.Cos(outward)));
        return (Mathf.PosMod(raise + 180, 360) - 180, Mathf.RadToDeg(outward), Mathf.RadToDeg(bend - MathF.Atan2(palm, along)));
    }

    /// <summary>
    /// The arm raise, outward swing, elbow bend and roll of the arm about its own length, in degrees, that put a fist at
    /// <paramref name="grip"/>, from the hips in the trunk's own frame, with the elbow as far toward <paramref name="elbowToward"/>
    /// as the arm allows; <see cref="ActorRig.RollArm"/> applies the roll after the pose.
    /// <paramref name="shoulder"/> is where the shoulder is in that frame, the game's build at rest unless given.
    /// </summary>
    public static (float Raise, float Out, float Elbow, float Roll) ArmTo(Vector3 grip, bool left, Vector3 elbowToward, float palm = 0, Vector3? shoulder = null) {
        var from = shoulder ?? HumanShape.Standard.Shoulder(left);
        const float upper = ActorRig.UpperArm;
        float fore = MathF.Sqrt(ActorRig.Forearm * ActorRig.Forearm + palm * palm);
        var reach = grip - from;
        float d = Math.Clamp(reach.Length(), Math.Abs(upper - fore) + 1e-3f, upper + fore - 1e-3f);
        var along = reach.Normalized();
        // The elbow lies on a circle round the line to the hand; it takes the point of it nearest the way asked.
        float toCentre = (upper * upper - fore * fore + d * d) / (2 * d), radius = MathF.Sqrt(Math.Max(0, upper * upper - toCentre * toCentre));
        var toward = elbowToward - along * elbowToward.Dot(along);
        if (toward.LengthSquared() < 1e-8f) toward = Vector3.Down - along * Vector3.Down.Dot(along);
        var elbowAt = from + along * toCentre + toward.Normalized() * radius;
        var hand = from + along * d;
        Vector3 down = (elbowAt - from).Normalized(), forearm = (hand - elbowAt).Normalized();
        // The forearm bends toward the shoulder's +Z; the fist sits a palm ahead of the forearm's line.
        var bend = forearm - down * forearm.Dot(down);
        if (bend.LengthSquared() < 1e-8f) bend = Vector3.Back - down * Vector3.Back.Dot(down);
        var z = bend.Normalized();
        var y = -down;
        var basis = new Basis(y.Cross(z), y, z);
        // The shoulder turns as Rx(-raise) Rz(out) and RollArm then turns it about its length.
        var euler = basis.GetEuler(EulerOrder.Xzy);
        float elbow = Mathf.RadToDeg(MathF.Acos(Math.Clamp(down.Dot(forearm), -1, 1)) - MathF.Atan2(palm, ActorRig.Forearm));
        float outward = Mathf.RadToDeg(euler.Z);
        return (-Mathf.RadToDeg(euler.X), left ? outward : -outward, elbow, Mathf.RadToDeg(euler.Y));
    }

    /// <summary>
    /// A pose with a fist, the right one unless <paramref name="left"/>, <paramref name="weight"/> of the way to <paramref name="aim"/>
    /// in the world, for a rig placed at <paramref name="root"/>; with <paramref name="elbowToward"/>, in the trunk's frame, the
    /// elbow points that way as far as the arm allows, and the roll of the arm to apply after the pose is returned too.
    /// </summary>
    public static (Pose Pose, float Roll) Reaching(Pose pose, Vector3 aim, Transform3D root, bool left = false, float weight = 1, Vector3? elbowToward = null) {
        var inTrunk = ToTrunk(pose, root.AffineInverse() * aim);
        var (raise, outward, elbow, roll) = elbowToward is Vector3 way ? ArmTo(inTrunk, left, way, ActorRig.Palm) : ArmTo(inTrunk, left, ActorRig.Palm) is var (r, o, e) ? (r, o, e, 0f) : default;
        return (WithArm(pose, left, raise, outward, elbow, weight), roll * weight);
    }

    /// <summary>
    /// A pose with one arm bent so its fist lies on <paramref name="grip"/>, from the hips in the trunk's own frame.
    /// The elbow points toward <paramref name="elbowToward"/>; the roll of the arm to apply after it comes too.
    /// <paramref name="shoulder"/> is where the shoulder is in that frame, the game's build at rest unless given.
    /// </summary>
    public static (Pose Pose, float Roll) WithHandAt(Pose pose, bool left, Vector3 grip, Vector3 elbowToward, Vector3? shoulder = null) {
        var (raise, outward, elbow, roll) = ArmTo(grip, left, elbowToward, ActorRig.Palm, shoulder);
        return (WithArm(pose, left, raise, outward, elbow, 1), roll);
    }

    /// <summary>A pose with one arm's raise, outward swing and elbow bend moved <paramref name="weight"/> of the way to these.</summary>
    public static Pose WithArm(Pose pose, bool left, float raise, float outward, float elbow, float weight) => left
        ? pose with { RaiseL = Mathf.Lerp(pose.RaiseL, raise, weight), OutL = Mathf.Lerp(pose.OutL, outward, weight), ElbowL = Mathf.Lerp(pose.ElbowL, elbow, weight) }
        : pose with { RaiseR = Mathf.Lerp(pose.RaiseR, raise, weight), OutR = Mathf.Lerp(pose.OutR, outward, weight), ElbowR = Mathf.Lerp(pose.ElbowR, elbow, weight) };

    /// <summary>How a pose turns the trunk on the hips, as <see cref="ActorRig.Show"/> applies it.</summary>
    public static Basis TrunkBasis(Pose pose) => Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Lean), Mathf.DegToRad(pose.Twist), Mathf.DegToRad(pose.Sway)));

    /// <summary>A point given from the rig's root, measured instead from the hips in a pose's trunk frame.</summary>
    public static Vector3 ToTrunk(Pose pose, Vector3 point) =>
        TrunkBasis(pose).Inverse() * (point - Vector3.Up * pose.HipHeight);

    /// <summary>A point given from the hips in a pose's trunk frame, measured instead from the rig's root.</summary>
    public static Vector3 FromTrunk(Pose pose, Vector3 point) => Vector3.Up * pose.HipHeight + TrunkBasis(pose) * point;

    /// <summary>How a pose turns an arm at the shoulder and how far that arm's elbow folds, as <see cref="ActorRig.Show"/> applies them, from the trunk's frame.</summary>
    private static (Basis Shoulder, Basis Forearm) ArmBases(Pose pose, bool left) {
        var shoulder = left
            ? Basis.FromEuler(new Vector3(Mathf.DegToRad(-pose.RaiseL), 0, Mathf.DegToRad(pose.OutL)))
            : Basis.FromEuler(new Vector3(Mathf.DegToRad(-pose.RaiseR), 0, Mathf.DegToRad(-pose.OutR)));
        return (shoulder, shoulder * new Basis(Vector3.Right, Mathf.DegToRad(-(left ? pose.ElbowL : pose.ElbowR))));
    }

    /// <summary>
    /// Where a fist is for an arm turned at the shoulder and bent at the elbow by these (<see cref="ArmBases"/>), from
    /// the hips in the trunk's own frame, as <see cref="ActorRig.Fist"/> has it on a rig shown in them.
    /// </summary>
    private static Vector3 FistInTrunk(Basis shoulder, Basis forearm, bool left) => HumanShape.Standard.Shoulder(left)
        + shoulder * new Vector3(0, -ActorRig.UpperArm, 0) + forearm * new Vector3(0, -ActorRig.Forearm, ActorRig.Palm);

    /// <summary>
    /// Where a point <paramref name="past"/> metres beyond a pose's fist along its forearm's line is, from the hips in
    /// the trunk's own frame.
    /// </summary>
    public static Vector3 PastFist(Pose pose, bool left, float past) {
        var (shoulder, forearm) = ArmBases(pose, left);
        return FistInTrunk(shoulder, forearm, left) + forearm * Vector3.Down * past;
    }

    /// <summary>
    /// Where a point of something in the right fist is for a pose, from the rig's root: <paramref name="held"/> is in the grip's
    /// own frame, the tool's handle running down along -Y from the fist, turned about its length by <see cref="Pose.Grip"/>.
    /// </summary>
    public static Vector3 InRightHand(Pose pose, Vector3 held) {
        var (shoulder, forearm) = ArmBases(pose, false);
        var trunk = TrunkBasis(pose);
        var grip = new Basis(Vector3.Up, Mathf.DegToRad(pose.Grip));
        return Vector3.Up * pose.HipHeight + trunk * FistInTrunk(shoulder, forearm, false)
            + trunk * (forearm * (grip * held));
    }

    /// <summary>Where a shoulder is in the world for a pose, on a rig placed at <paramref name="root"/>.</summary>
    public static Vector3 Shoulder(Pose pose, Transform3D root, bool left) =>
        root * FromTrunk(pose, HumanShape.Standard.Shoulder(left));
}
