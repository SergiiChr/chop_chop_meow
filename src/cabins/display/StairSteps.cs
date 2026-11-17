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
    // A boot this much higher than the other has left the ground: the body stands on the other one.
    private const float OffTheGround = .03f;
    // The body eases onto a lifting boot's tread this many metres per metre the boot rises, so the hips never jump.
    private const float LiftEase = 3;

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
    /// For each leg of a pose, how high its boot sole's lower end is above the ground the pose stands on, and how high that
    /// ground would have to be for the boot to rest on the steps with neither end in them.
    /// Once the boot is off the ground, each tread's rise is counted over <see cref="StepClearance"/> ahead of its edge.
    /// </summary>
    private static ((float Clear, float Rests) Left, (float Clear, float Rests) Right) Boots(Pose pose, float depth, Vector2 up, ReadOnlySpan<float> edges, float rise) {
        float hips = pose.HipHeight;
        var (toeL, heelL) = ActorAnimation.SoleOf(pose.SwingL, pose.KneeL, pose.FootL);
        var (toeR, heelR) = ActorAnimation.SoleOf(pose.SwingR, pose.KneeR, pose.FootR);
        float clearL = hips - Math.Max(toeL.Y, heelL.Y), clearR = hips - Math.Max(toeR.Y, heelR.Y), lower = Math.Min(clearL, clearR);
        float aheadL = StepClearance * Math.Clamp((clearL - lower) / OffTheGround, 0, 1), aheadR = StepClearance * Math.Clamp((clearR - lower) / OffTheGround, 0, 1);
        float restsL = Math.Max(StepGround(depth + up.Y * toeL.X + up.X * ActorAnimation.LegSide, edges, rise, aheadL) - hips + toeL.Y, StepGround(depth + up.Y * heelL.X + up.X * ActorAnimation.LegSide, edges, rise, aheadL) - hips + heelL.Y);
        float restsR = Math.Max(StepGround(depth + up.Y * toeR.X - up.X * ActorAnimation.LegSide, edges, rise, aheadR) - hips + toeR.Y, StepGround(depth + up.Y * heelR.X - up.X * ActorAnimation.LegSide, edges, rise, aheadR) - hips + heelR.Y);
        return ((clearL, restsL), (clearR, restsR));
    }

    /// <summary>
    /// A pose stood on steps, and how high above the ground under it the body stands: on the boot that bears the weight,
    /// easing onto the other boot's tread as the first one lifts off.
    /// A boot that would sink into a tread bends its knee to stand on it instead, keeping its angle to the ground.
    /// <paramref name="depth"/> is how far along the way up the hips are and <paramref name="up"/> that way in the body's
    /// own frame, X toward its left and Y ahead; the steps rise <paramref name="rise"/> at each of <paramref name="edges"/>.
    /// </summary>
    public static (Pose Pose, float Lift) OnSteps(Pose pose, float depth, Vector2 up, ReadOnlySpan<float> edges, float rise) {
        var (left, right) = Boots(pose, depth, up, edges, rise);
        float lower = Math.Min(left.Clear, right.Clear);
        float lift = Math.Min(left.Rests + LiftEase * (left.Clear - lower), right.Rests + LiftEase * (right.Clear - lower));
        var fitted = pose with { Hips = pose.HipHeight };
        if (left.Rests > lift) {
            var leg = Raised(new ActorAnimation.LegAngles(pose.SwingL, pose.KneeL, pose.FootL), left.Rests - lift);
            fitted = fitted with { SwingL = leg.Swing, KneeL = leg.Knee, FootL = leg.Foot };
        }
        if (right.Rests > lift) {
            var leg = Raised(new ActorAnimation.LegAngles(pose.SwingR, pose.KneeR, pose.FootR), right.Rests - lift);
            fitted = fitted with { SwingR = leg.Swing, KneeR = leg.Knee, FootR = leg.Foot };
        }
        return (fitted, lift);
    }

    /// <summary>A leg with its ankle drawn <paramref name="by"/> metres straight up toward the hip, the boot keeping its angle to the ground.</summary>
    private static ActorAnimation.LegAngles Raised(ActorAnimation.LegAngles leg, float by) {
        var ankle = ActorAnimation.Ankle(leg.Swing, leg.Knee);
        var (swing, knee) = ActorAnimation.LegTo(ankle.X, ankle.Y - by);
        return new ActorAnimation.LegAngles(swing, knee, leg.Foot - leg.Swing + leg.Knee + swing - knee);
    }
}
