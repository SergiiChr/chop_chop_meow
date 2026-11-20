using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Things thrown through the air: a stump or bush dug out of the ground and a chip knocked off a rock.
/// In the air a body's middle follows a parabola under gravity and it turns at a steady rate about its middle.
/// </summary>
public static class Thrown {
    public const float Gravity = 9.81f;

    /// <summary>
    /// How far above a straight line from launch to landing a thrown body is <paramref name="t"/> seconds into a flight of
    /// <paramref name="flight"/> seconds: highest halfway, at g·flight²/8.
    /// </summary>
    public static float Rise(float t, float flight) => Gravity / 2 * t * (flight - t);

    /// <summary>
    /// A body thrown from <paramref name="launch"/>, its frame as it left: lifted by <paramref name="rise"/>, turned
    /// <paramref name="tumble"/> radians over about its own x axis through <paramref name="middle"/> and sized
    /// <paramref name="size"/> about it.
    /// </summary>
    public static Transform3D Tumble(Transform3D launch, Vector3 middle, float rise, float tumble, float size) {
        var turn = new Basis(Vector3.Right, tumble).Scaled(Vector3.One * size);
        return (launch * new Transform3D(turn, middle - turn * middle)).Translated(Vector3.Up * rise);
    }

    /// <summary>
    /// How far along a chip's way it is <paramref name="t"/> seconds after it left the rock, from 0 to 1: it moves and
    /// spins steadily through its <paramref name="flight"/> in the air, then skids and turns to a stop over
    /// <paramref name="settle"/> seconds, slowing evenly, so neither its speed nor its spin jumps as it lands.
    /// </summary>
    public static float ChipWay(float t, float flight, float settle) {
        float whole = flight + settle / 2;
        if (t <= flight) return t / whole;
        if (t >= flight + settle) return 1;
        float on = t - flight;
        return (flight + on - on * on / (2 * settle)) / whole;
    }

    /// <summary>
    /// The height of a chip's model <paramref name="t"/> seconds into a flight of <paramref name="flight"/> seconds
    /// down from <paramref name="from"/> metres up to the ground.
    /// </summary>
    public static float ChipHeight(float t, float flight, float from) => t >= flight ? 0 : from * (1 - t / flight) + Rise(t, flight);
}
