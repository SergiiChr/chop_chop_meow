using System;
using Godot;

namespace ChopChopMeow;

/// <summary>How things move under gravity and ease from one place to another, shared by everything the view animates.</summary>
public static class Movement {
    /// <summary>How fast a falling body speeds up, in metres a second each second.</summary>
    public const float Gravity = 9.81f;

    /// <summary>
    /// How far above a straight line from launch to landing a thrown body is <paramref name="t"/> seconds into a flight of
    /// <paramref name="flight"/> seconds: highest halfway, at g·flight²/8.
    /// </summary>
    public static float Rise(float t, float flight) => Gravity / 2 * t * (flight - t);

    /// <summary>
    /// How far something has gone <paramref name="t"/> seconds after it starts from rest, speeding up evenly to a speed of
    /// one over <paramref name="ramp"/> seconds and then going on at that speed.
    /// </summary>
    public static float Ramped(float t, float ramp) => t < ramp ? t * t / (2 * ramp) : t - ramp / 2;

    /// <summary>A turn, and any sizing, made about <paramref name="pivot"/> rather than about the origin.</summary>
    public static Transform3D About(Basis turn, Vector3 pivot) => new(turn, pivot - turn * pivot);

    /// <summary>
    /// Moves <paramref name="value"/> toward <paramref name="target"/> as a critically damped spring of <paramref name="rate"/>
    /// for <paramref name="dt"/> seconds, exact for any step, carrying its <paramref name="speed"/>: it never overshoots,
    /// and is most of the way there in 1.5 / rate seconds.
    /// </summary>
    public static void Spring(ref float value, ref float speed, float target, float rate, float dt) {
        float fade = MathF.Exp(-rate * dt), off = value - target, push = speed + rate * off;
        value = target + (off + push * dt) * fade;
        speed = (speed - rate * dt * push) * fade;
    }
}
