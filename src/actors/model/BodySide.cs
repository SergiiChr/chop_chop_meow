using Godot;

namespace ChopChopMeow;

/// <summary>
/// The left and right of a body on the ground, worked out from the way it faces: 1 is its left and -1 its right, the same
/// for a body standing and for one fallen onto its back from facing that way.
/// Everything that comes at a body from a side finds the side here: a wolf taking an arm, tugging beside another, pinning
/// from beside a fallen player or feeding at one. Each takes the side it is already on, so none crosses the body to the other.
/// </summary>
public static class BodySide {
    public const int Left = 1, Right = -1;

    /// <summary>The way out from a body facing <paramref name="facing"/> to its <paramref name="side"/>.</summary>
    public static Vector2 Out(Vector2 facing, int side) => new Vector2(facing.Y, -facing.X).Normalized() * side;

    /// <summary>
    /// The side of a body at <paramref name="at"/> facing <paramref name="facing"/> that <paramref name="spot"/> is on;
    /// its left for a spot dead ahead of it or behind it.
    /// </summary>
    public static int Of(Vector2 at, Vector2 facing, Vector2 spot) => (spot - at).Dot(Out(facing, Left)) >= 0 ? Left : Right;

    /// <summary>
    /// The side of a body that something at <paramref name="spot"/> takes when its left or its right may be taken:
    /// the side it is on while that is free, else the other, else 0.
    /// </summary>
    public static int Free(Vector2 at, Vector2 facing, Vector2 spot, bool leftTaken, bool rightTaken) {
        int near = Of(at, facing, spot);
        bool nearTaken = near == Left ? leftTaken : rightTaken, farTaken = near == Left ? rightTaken : leftTaken;
        return !nearTaken ? near : !farTaken ? -near : 0;
    }
}
