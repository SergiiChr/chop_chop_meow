using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Things thrown through the air: a stump or bush dug out of the ground and a chip knocked off a rock.
/// In the air a body's middle follows a parabola under gravity and it turns at a steady rate about its middle.
/// </summary>
public static class Thrown {
    /// <summary>
    /// Seconds a dug out stump or bush takes to come out of the ground and be gone: thrown up, it rises and falls back in
    /// that time.
    /// </summary>
    public const float DugOutSeconds = .6f;
    /// <summary>How far over a dug out stump or bush tumbles in the air, in radians.</summary>
    private static readonly float DugTumble = Mathf.DegToRad(150);

    /// <summary>
    /// A stump or bush <paramref name="t"/> seconds after it was dug out from <paramref name="launch"/>, its frame as it
    /// left: it rises and falls under gravity, tumbles steadily over about <paramref name="middle"/> and shrinks away as it
    /// comes down.
    /// </summary>
    public static Transform3D DugOut(Transform3D launch, Vector3 middle, float t) {
        float share = t / DugOutSeconds;
        return Tumble(launch, middle, Movement.Rise(t, DugOutSeconds), DugTumble * t / DugOutSeconds, 1 - Mathf.SmoothStep(.45f, 1, share));
    }

    /// <summary>
    /// A body thrown from <paramref name="launch"/>, its frame as it left: lifted by <paramref name="rise"/>, turned
    /// <paramref name="tumble"/> radians over about its own x axis through <paramref name="middle"/> and sized
    /// <paramref name="size"/> about it.
    /// </summary>
    private static Transform3D Tumble(Transform3D launch, Vector3 middle, float rise, float tumble, float size) {
        var turn = new Basis(Vector3.Right, tumble).Scaled(Vector3.One * size);
        return (launch * Movement.About(turn, middle)).Translated(Vector3.Up * rise);
    }

    /// <summary>
    /// How far along a chip's way it is <paramref name="t"/> seconds after it left the rock, from 0 to 1: it moves and
    /// spins steadily through its <paramref name="flight"/> in the air, then skids and turns to a stop over
    /// <paramref name="settle"/> seconds, slowing evenly, so neither its speed nor its spin jumps as it lands.
    /// </summary>
    public static float ChipWay(float t, float flight, float settle) {
        // Slowing evenly to a stop is speeding up evenly from one played backward, from the end of the way.
        float whole = flight + settle / 2;
        return (whole - Movement.Ramped(Math.Max(0, flight + settle - t), settle)) / whole;
    }

    /// <summary>
    /// The height of a chip's model <paramref name="t"/> seconds into a flight of <paramref name="flight"/> seconds
    /// down from <paramref name="from"/> metres up to the ground.
    /// </summary>
    public static float ChipHeight(float t, float flight, float from) => t >= flight ? 0 : from * (1 - t / flight) + Movement.Rise(t, flight);
}
