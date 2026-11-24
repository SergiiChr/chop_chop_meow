using Godot;

namespace ChopChopMeow;

/// <summary>A two-handed pull through a leaf spot with the boots planted.</summary>
internal static class RakeStroke {
    public const float Seconds = 2;
    public const float Contact = .27f;
    public const float Scale = 1.4f;
    private const float LowerGrip = .335f, Reach = 1.29f, FarReach = 1.35f;
    // Even with leaves underfoot, the teeth stay ahead of the boots.
    private const float NearReach = .43f, BootClearance = .32f, FullStrokeReach = .95f;

    // The bottom corner of a tine in RackShape.Rake, at the centre of the row.
    public static readonly Vector3 Tines = new Vector3(0, -.855f, .0075f) * Scale;

    /// <summary>The rake's teeth, forward slope of the shaft and weight over the feet at a moment in the stroke.</summary>
    private readonly record struct Key(
        float At, float Height, float Forward, float Slope, float Lean, float Twist, float Hips, float Shift, float Aim);

    private static readonly Key[] Keys = [
        new(0, .12f, .87f, .41f, 16, -10, .77f, .02f, .75f),
        new(Contact, .02f, Reach, .62f, 45, -17, .72f, .065f, 1),
        new(.64f, .02f, .64f, .31f, 15, -10, .77f, 0, .53f),
        new(.76f, .15f, .66f, .31f, 15, -10, .77f, 0, .53f),
    ];

    /// <summary>Lowers at the far edge, pulls along the ground and lifts clear to reach forward again.</summary>
    public static Pose PoseAt(float phase, Vector3? patch, Pose? blending = null) {
        int from = Keys.Length - 1;
        while (from > 0 && phase < Keys[from].At) from--;
        var a = Keys[from];
        bool returning = from == Keys.Length - 1;
        var b = returning ? Keys[0] : Keys[from + 1];
        float t = Mathf.SmoothStep(a.At, returning ? 1 : b.At, phase);

        float distance = patch is Vector3 centre ? new Vector2(centre.X, centre.Z).Length() : Reach;
        float fullStroke = Mathf.SmoothStep(NearReach, FullStrokeReach, distance);
        float reach = (Mathf.Clamp(distance, NearReach, FarReach) - Reach) * Mathf.Lerp(a.Aim, b.Aim, t);
        float forward = Mathf.Lerp(a.Forward, b.Forward, t);
        float teethForward = Mathf.Max(BootClearance, forward + reach);
        float slope = Mathf.Lerp(a.Slope, b.Slope, t) + .55f * (teethForward - forward);
        float across = Mathf.Lerp(.12f, .225f, fullStroke);
        var along = new Vector3(across, -Mathf.Sqrt(1 - across * across - slope * slope), slope);
        var teeth = new Vector3(Mathf.Lerp(.045f, .13f, fullStroke), Mathf.Lerp(a.Height, b.Height, t), teethForward);
        var top = teeth - Shaft(along) * Tines;
        float lean = Mathf.Lerp(8, Mathf.Lerp(a.Lean, b.Lean, t), fullStroke);
        var pose = blending ?? new Pose(Lean: lean, Twist: Mathf.Lerp(a.Twist, b.Twist, t),
            Head: 22, Hips: Mathf.Lerp(a.Hips, b.Hips, t)) { HipShift = Mathf.Lerp(a.Shift, b.Shift, t) * fullStroke };
        (pose, float rightRoll) = ArmReach.WithHandAt(pose, false, ArmReach.ToTrunk(pose, top), new Vector3(-1, -1, 0));
        (pose, float leftRoll) = ArmReach.WithHandAt(pose, true, ArmReach.ToTrunk(pose, top + along * LowerGrip), new Vector3(1, -1, 0));
        return pose with { ArmRollL = leftRoll, ArmRollR = rightRoll };
    }

    /// <summary>Runs -Y down the shaft and keeps the row of tines level across the ground.</summary>
    public static Basis Shaft(Vector3 along) {
        var across = Vector3.Up.Cross(along).Normalized();
        return new Basis(across, -along, across.Cross(-along));
    }
}
