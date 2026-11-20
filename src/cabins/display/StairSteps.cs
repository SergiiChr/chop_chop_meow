using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Standing a body on a cabin's steps: how high the treads are, and a pose's boots fitted onto them so none sinks in.</summary>
public static class StairSteps {
    /// <summary>
    /// Where the treads of a cabin's step and then its floor begin, in from the door point, each this much higher than the
    /// last, as <see cref="WorldModels"/> builds the cabin.
    /// </summary>
    public static ReadOnlySpan<float> StepEdges => new[] { .4f, .62f, .8f };
    public const float StepRise = .14f;
    // A swinging boot rises onto a tread over this far ahead of its edge, so it clears the edge rather than snapping up at it.
    private const float StepClearance = .12f;
    // A boot this high over the pose's ground has left it, and rises onto a tread ahead of its edge.
    private const float OffTheGround = .03f;
    // The body heads onto the other boot's tread this many metres per metre a boot lifting off behind rises, easing out
    // over this far either side of the hips as the boot swings past them.
    private const float LiftEase = 3, PastHips = .1f;
    // How quickly the body's height over the steps follows where it heads, up and down, as the rate of a critically damped
    // spring a second (Movement.Spring). It comes down quicker, since a boot it holds up over a lower tread would show in the air.
    private const float RiseRate = 20, DropRate = 40;
    // A boot reaching down toe first to a tread lifts its heel this many degrees at most.
    private const float MostPointed = 35;
    // How far ahead of a boot in the air the body looks for the steps dropping away, to start lowering before the boot gets there.
    private const float LookAhead = .5f;
    // How far a boot bearing the weight alone raises toward the hips at most to let the body down to a lower tread, a knee
    // bend of about 100° under hips at a walk's height.
    private const float MostBend = .3f;
    // A boot this close over the pose's ground bears some of the body's weight.
    private const float Bearing = .005f;
    // How much higher than its toe reaches the body may stand over a boot lifting off, by the time it is off the ground.
    private const float Unborne = .1f;

    /// <summary>Seconds a body new to the steps is given to settle onto them in one frame, so it starts where it stands.</summary>
    public const float Settle = 1;

    /// <summary>Half the width of a cabin's step, which lies centred on its door point, and how far in from the door point the door itself is.</summary>
    public const float StepHalfWidth = .5f, DoorDepth = .925f;

    /// <summary>
    /// How high a cabin's step, or its floor past the door, stands over the ground at <paramref name="from"/>, a spot measured from
    /// the cabin's door point. A paw or boot in the air, <paramref name="lifted"/>, rises onto a tread ahead of its edge.
    /// </summary>
    public static float HeightAt(Vector2 from, bool lifted = false) {
        float depth = -from.Dot(Doorways.OutOfDoor);
        return Math.Abs(from.Dot(Doorways.AsideWay)) > StepHalfWidth && depth < DoorDepth ? 0 : StepGround(depth, StepEdges, StepRise, lifted ? StepClearance : 0);
    }

    /// <summary>How high steps rising <paramref name="rise"/> at each of <paramref name="edges"/>, depths along the way up, are at <paramref name="depth"/>.</summary>
    public static float StepGround(float depth, ReadOnlySpan<float> edges, float rise) => StepGround(depth, edges, rise, 0);

    /// <summary>The steps' height at <paramref name="depth"/>, rising onto each tread over <paramref name="ahead"/> before its edge.</summary>
    private static float StepGround(float depth, ReadOnlySpan<float> edges, float rise, float ahead) {
        float ground = 0;
        foreach (float edge in edges)
            ground += depth >= edge ? rise : ahead > 0 && depth > edge - ahead ? rise * Mathf.SmoothStep(0, 1, (depth - edge + ahead) / ahead) : 0;
        return ground;
    }

    /// <summary>
    /// A leg of a pose over the steps: its angles, where its toe is ahead of the hips and below them, how high its boot
    /// sole's lower end is above the ground the pose stands on, and how high that ground would have to be for the boot to
    /// rest on the steps with neither end in them, and how high the steps are <see cref="LookAhead"/> on from its toe the
    /// way it travels.
    /// Once the boot is off the ground, each tread's rise is counted over <see cref="StepClearance"/> ahead of its edge.
    /// </summary>
    private readonly record struct Boot(ActorAnimation.LegAngles Leg, Vector2 Toe, float Clear, float Rests, float Ahead) {
        /// <summary>How high the ground under the pose would have to be for the boot to stand as high over its tread as the pose holds it over the pose's ground.</summary>
        public float Stands => Rests + Math.Max(0, Clear);
        /// <summary>Where the boot has the body head: onto its tread at the pose's height over it, and lower as the steps drop ahead of it in the air.</summary>
        public float Heads => Stands - (1 - Bears) * Drops;
        /// <summary>How much lower the steps are ahead of the boot the way it travels than what it stands over.</summary>
        public float Drops => Math.Max(0, Stands - Ahead);
        /// <summary>How much of the body's weight the boot bears: all of it on the pose's ground, none once it is <see cref="Bearing"/> above it.</summary>
        public float Bears => 1 - Math.Clamp(Clear / Bearing, 0, 1);
    }

    /// <summary>
    /// A leg of <paramref name="pose"/> over the steps, the left one or the right, with the hips <paramref name="depth"/> along
    /// the way up, which is <paramref name="up"/> in the body's frame, and the boot travelling ahead or, <paramref name="backing"/>,
    /// behind.
    /// </summary>
    private static Boot BootOf(Pose pose, bool left, float depth, Vector2 up, bool backing, ReadOnlySpan<float> edges, float rise) {
        float hips = pose.HipHeight;
        var leg = pose.Leg(left);
        var (toe, heel) = ActorAnimation.SoleOf(leg.Swing, leg.Knee, leg.Foot);
        float clear = hips - Math.Max(toe.Y, heel.Y), ahead = StepClearance * Math.Clamp(clear / OffTheGround, 0, 1);
        // Where the toe and heel are along the way up, the leg hanging to its own side of the hips.
        float aside = (left ? up.X : -up.X) * ActorAnimation.LegSide, toeAt = depth + up.Y * toe.X + aside, heelAt = depth + up.Y * heel.X + aside;
        float rests = Math.Max(StepGround(toeAt, edges, rise, ahead) - hips + toe.Y, StepGround(heelAt, edges, rise, ahead) - hips + heel.Y);
        // A boot swings the way the body moves: ahead, or behind it backing away.
        float travel = (backing ? -up.Y : up.Y) * LookAhead;
        return new(leg, toe, clear, rests, StepGround(toeAt + travel, edges, rise));
    }

    /// <summary>
    /// A pose stood on steps, carrying the body's height over the ground under it in <paramref name="lift"/> and how fast that
    /// changes in <paramref name="speed"/> from one frame to the next, <paramref name="dt"/> seconds apart.
    /// The body heads for the tread of the boot that bears the weight, rising onto the other boot's tread as the first one
    /// lifts off behind, and lowering ahead of a boot in the air that is about to step down, moving the way the body does, or
    /// behind it <paramref name="backing"/>, as far as a leg bearing weight bends.
    /// It gets there smoothly rather than in the frame a boot passes a tread's edge.
    /// It never stands higher than a boot on the ground can reach down to its tread, unless it would have to drop faster
    /// than it falls, and a boot it holds over a lower tread reaches down toe first, as a foot stepping down a stair does.
    /// A boot that would sink into a tread bends its knee to stand on it instead, keeping its angle to the ground.
    /// <paramref name="depth"/> is how far along the way up the hips are and <paramref name="up"/> that way in the body's
    /// own frame, X toward its left and Y ahead; the steps rise <paramref name="rise"/> at each of <paramref name="edges"/>.
    /// </summary>
    public static Pose OnSteps(Pose pose, float depth, Vector2 up, bool backing, ReadOnlySpan<float> edges, float rise, ref float lift, ref float speed, float dt) {
        Boot left = BootOf(pose, true, depth, up, backing, edges, rise), right = BootOf(pose, false, depth, up, backing, edges, rise);
        float lower = Math.Min(left.Clear, right.Clear), hips = pose.HipHeight;
        float aim = Math.Min(left.Heads + Easing(left, backing, rise) * (left.Clear - lower), right.Heads + Easing(right, backing, rise) * (right.Clear - lower));
        aim = Math.Max(aim, Math.Max(Lowest(left, right), Lowest(right, left)));
        float before = lift, falls = before + Math.Min(speed, 0) * dt - Movement.Gravity * dt * dt / 2;
        Movement.Spring(ref lift, ref speed, aim, aim > lift ? RiseRate : DropRate, dt);
        // No higher than the boots on the ground reach down, but no faster down than falling: a boot that cannot reach its
        // tread in time stays in the air a moment longer.
        float held = Math.Max(Math.Min(lift, Math.Min(Highest(left), Highest(right))), falls);
        if (held != lift && dt > 0) speed = (held - before) / dt;
        lift = held;
        return (pose with { Hips = hips }).WithLegs(Fitted(left, lift), Fitted(right, lift));
    }

    /// <summary>
    /// How many metres the body heads up onto the other boot's tread per metre <paramref name="boot"/> rises: by
    /// <see cref="LiftEase"/> while it is behind the hips the way the body moves, lifting off, and not at all once it swings
    /// out ahead of them to land, <paramref name="backing"/> moving the body away from where it faces, nor as it heads for
    /// a tread <paramref name="rise"/> or more lower.
    /// </summary>
    private static float Easing(Boot boot, bool backing, float rise) =>
        LiftEase * (1 - Mathf.SmoothStep(-PastHips, PastHips, backing ? -boot.Toe.X : boot.Toe.X)) * (1 - Math.Clamp(boot.Drops / rise, 0, 1));

    /// <summary>
    /// How low over the ground under the pose the body heads with <paramref name="boot"/> bearing its weight alone, the
    /// <paramref name="other"/> boot in the air: the leg bends at the knee only so far to let the body down, and the hold
    /// eases off as the other boot comes down to share the weight.
    /// </summary>
    private static float Lowest(Boot boot, Boot other) {
        float alone = boot.Bears * (1 - other.Bears);
        return alone > 0 ? boot.Stands - MostBend / alone : float.MinValue;
    }

    /// <summary>
    /// How high over the ground under the pose the body can stand with <paramref name="boot"/>, as far as it bears the body's
    /// weight, still reaching its tread toe first: the limit eases off by <see cref="Unborne"/> as the boot leaves the ground.
    /// </summary>
    private static float Highest(Boot boot) {
        if (boot.Bears <= 0) return float.MaxValue;
        float tilt = Mathf.DegToRad(Math.Max(0, boot.Leg.Tilt));
        float reach = Math.Max(ActorAnimation.ToeReach(boot.Toe.X, tilt, ActorAnimation.StoodKnee), ActorAnimation.ToeReach(boot.Toe.X, Mathf.DegToRad(MostPointed), ActorAnimation.StoodKnee));
        return boot.Stands + Math.Max(0, reach - boot.Toe.Y) + (1 - boot.Bears) * Unborne;
    }

    /// <summary>
    /// A boot's leg with the body <paramref name="lift"/> over the pose's ground: raised onto a tread it would sink into, or
    /// reaching down toe first, as far as the leg goes with the toe kept where it is, to stand as high over a lower tread
    /// as the pose holds it.
    /// </summary>
    private static ActorAnimation.LegAngles Fitted(Boot boot, float lift) {
        if (boot.Rests > lift) return Raised(boot.Leg, boot.Rests - lift);
        float down = lift - boot.Stands;
        if (down <= 0) return boot.Leg;
        // The toe goes straight down and the heel lifts about it as far as the leg, keeping its knee, needs to reach; the
        // leg straightens for the rest, as far as it goes.
        float ground = boot.Toe.Y + down;
        float tilt = Mathf.DegToRad(boot.Leg.Tilt);
        tilt = Math.Clamp(ActorAnimation.HeelLift(boot.Toe.X, ground, boot.Leg.Knee), tilt, Math.Max(tilt, Mathf.DegToRad(MostPointed)));
        return ActorAnimation.OnToe(boot.Toe.X, Math.Min(ground, ActorAnimation.ToeReach(boot.Toe.X, tilt, ActorAnimation.StoodKnee)), tilt);
    }

    /// <summary>A leg with its ankle drawn <paramref name="by"/> metres straight up toward the hip, the boot keeping its angle to the ground.</summary>
    private static ActorAnimation.LegAngles Raised(ActorAnimation.LegAngles leg, float by) {
        var ankle = ActorAnimation.Ankle(leg.Swing, leg.Knee);
        var (swing, knee) = ActorAnimation.LegTo(ankle.X, ankle.Y - by);
        return new ActorAnimation.LegAngles(swing, knee, leg.Tilt + swing - knee);
    }
}
