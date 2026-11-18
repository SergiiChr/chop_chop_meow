using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The ground a swing's blade, flame or fist passes over as it lands, seen from above: from <paramref name="Near"/> to
/// <paramref name="Far"/> metres from the swinger, from <paramref name="Left"/> degrees to the left of the way they aim
/// round to <paramref name="Right"/> degrees to its right.
/// A negative angle lies on the other side: a punch runs from 10 degrees right of the aim to 35, so its left is -10.
/// It comes no lower than <paramref name="Low"/> metres off the ground, so it passes over a body that does not stand that tall.
/// </summary>
public readonly record struct SwingShape(float Near, float Far, float Left, float Right, float Low = 0) {
    /// <summary>
    /// Whether a swing from <paramref name="from"/> aimed along <paramref name="aim"/>, a unit vector, touches a round
    /// body <paramref name="radius"/> metres across its half at <paramref name="spot"/> that stands <paramref name="top"/> metres tall.
    /// </summary>
    public bool Touches(Vector2 from, Vector2 aim, Vector2 spot, float radius, float top) {
        if (top < Low) return false;
        var to = spot - from;
        float distance = to.Length();
        if (distance + radius < Near || distance - radius > Far) return false;
        if (distance <= radius) return true;
        float angle = Mathf.RadToDeg(MathF.Atan2(to.Dot(BodySide.Out(aim, BodySide.Left)), to.Dot(aim)));
        if (angle <= Left && angle >= -Right) return true;
        // Outside the swing's sweep, a body still touches it when it reaches across the nearer edge.
        float past = Math.Min(Math.Abs(angle - Left), Math.Abs(angle + Right));
        return past < 90 && distance * MathF.Sin(Mathf.DegToRad(past)) <= radius;
    }
}
