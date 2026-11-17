using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// An actor's turn, a person's or a wolf's: the head turns toward a new heading first and the body follows with weight,
/// easing into and out of the turn and leaning into it.
/// </summary>
public struct Turning {
    /// <summary>The head's turn from the body, and the body's roll about its forward line, in radians.</summary>
    public float Look, Bank;
    // How fast the body and the head turn, in radians a second.
    private float speed, head, headSpeed;
    /// <summary>
    /// How quickly a body and head turn to a new heading, as the rate of a critically damped spring a second:
    /// the body covers 90% of a turn in 0.3 s, and the head about 0.2 s before it, as people turning while walking or running do.
    /// </summary>
    public const float BodyRate = 13, HeadRate = 30;
    /// <summary>How far, in radians, the head turns from the body, and the most a turning body leans into the turn at a sprint and at a jog or walk.</summary>
    public static readonly float MaxLook = Mathf.DegToRad(70), MaxBank = Mathf.DegToRad(8), MaxJogBank = Mathf.DegToRad(1);

    /// <summary>
    /// Moves <paramref name="angle"/> in radians toward <paramref name="target"/> as a critically damped spring of <paramref name="rate"/>
    /// for <paramref name="dt"/> seconds, exact for any step, carrying its turning <paramref name="speed"/>.
    /// A target half a turn away is turned toward the way the angle already turns, or the way <paramref name="side"/> points when it is still.
    /// </summary>
    private static float Spring(float angle, float target, float rate, float dt, ref float speed, float side) {
        float gap = Mathf.AngleDifference(angle, target);
        float way = speed != 0 ? speed : side;
        if (MathF.Abs(gap) > MathF.PI - .05f && way != 0) gap = MathF.CopySign(MathF.Abs(gap), way);
        float behind = -gap, push = speed + rate * behind, fade = MathF.Exp(-rate * dt);
        speed = (speed - rate * push * dt) * fade;
        return Mathf.Wrap(angle + gap + (behind + push * dt) * fade, -MathF.PI, MathF.PI);
    }

    /// <summary>
    /// Eases a body's <paramref name="yaw"/> toward <paramref name="heading"/>, both in radians, with the head ahead of it on a faster spring,
    /// and leans the body into the turn by the angle that balances turning at <paramref name="pace"/> metres a second against gravity,
    /// up to <see cref="MaxBank"/> when <paramref name="running"/> and <see cref="MaxJogBank"/> otherwise.
    /// </summary>
    public void Step(ref float yaw, float heading, float pace, float dt, bool running) {
        head = Spring(head, heading, HeadRate, dt, ref headSpeed, speed);
        yaw = Spring(yaw, heading, BodyRate, dt, ref speed, Look);
        Look = Math.Clamp(Mathf.AngleDifference(yaw, head), -MaxLook, MaxLook);
        // Turning toward +X, the rig's left, tips it that way: a negative roll about its forward line.
        float most = running ? MaxBank : MaxJogBank, lean = Math.Clamp(-MathF.Atan(pace * speed / 9.8f), -most, most);
        Bank = Mathf.Lerp(Bank, lean, Math.Min(1, dt * 12));
    }

    /// <summary>Keeps the head with the body at <paramref name="yaw"/>, still and upright, while something else turns the body.</summary>
    public void Hold(float yaw) {
        head = yaw; speed = headSpeed = 0; Look = Bank = 0;
    }
}
