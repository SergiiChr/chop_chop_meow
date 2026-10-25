using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The fixed landscape: a valley that opens onto the sea, with mountain ranges closing in from both sides toward its inland end and a lake in the middle.
/// The valley runs along the map's diagonal, so on screen the sea is at the bottom, the ranges on the left and right, and the inland end at the top.
/// </summary>
public static class Terrain
{
    public const int Size = 216;
    // Declared first: the points below are built from these directions, and static fields initialise in order.
    /// <summary>Unit step down the valley toward the sea; caravans and errand runners set off this way.</summary>
    public static readonly Vector2 Seaward = new Vector2(1, 1).Normalized();
    /// <summary>Unit step across the valley toward the right-hand range on screen.</summary>
    public static readonly Vector2 Sideways = new Vector2(1, -1).Normalized();
    public static readonly Vector2 Spawn = new(129, 146);
    public static readonly Vector2 Lake = Point(160, 14);
    public const float LakeRadius = 15;
    /// <summary>Where the shore starts, measured along the valley; beyond it is sea.</summary>
    public const float CoastLine = 240;

    /// <summary>Distance from the inland corner along the valley.</summary>
    public static float Along(Vector2 p) => p.Dot(Seaward);
    /// <summary>Signed distance from the valley's middle line; positive is the right-hand range on screen.</summary>
    public static float Across(Vector2 p) => p.Dot(Sideways);
    public static Vector2 Point(float along, float across) => Seaward * along + Sideways * across;

    /// <summary>Half the valley's width at a point along it, on one side; the valley narrows inland.</summary>
    public static float HalfWidth(float along, float side) =>
        10 + .2f * along + 5 * MathF.Sin(along * .06f + side * 2) + 2.5f * MathF.Sin(along * .17f + side);
    public static float Coast(float across) => CoastLine + 5 * MathF.Sin(across * .07f) + 3 * MathF.Sin(across * .17f + 1.3f);

    /// <summary>How far inside the valley a point is from the foot of the nearer range; negative in the mountains.</summary>
    public static float FromMountains(Vector2 p)
    {
        float along = Along(p), across = Across(p);
        return Math.Min(HalfWidth(along, Math.Sign(across)) - Math.Abs(across), along - 14);
    }
    /// <summary>Water, from just past the line where the drawn sea meets the sand.</summary>
    public static bool Sea(Vector2 p) => Along(p) > Coast(Across(p)) - 1;
    /// <summary>The sandy strip along the shore, where nothing grows.</summary>
    public static bool Beach(Vector2 p) => Along(p) > Coast(Across(p)) - 5;
    /// <summary>The lake's irregular shore as a distance from its centre; the water and the walkable edge share it.</summary>
    public static float Shore(float angle) => LakeRadius * (.9f + .05f * MathF.Sin(angle * 3 + 1) + .04f * MathF.Sin(angle * 5 + 2.3f) + .05f * MathF.Sin(angle * 2 + .4f));
    public static bool InLake(Vector2 p, float margin = 0)
    {
        var offset = p - Lake;
        return offset.Length() < Shore(MathF.Atan2(offset.Y, offset.X)) + margin;
    }
    /// <summary>Dry valley ground inside the map, ignoring anything built or growing on it.</summary>
    public static bool Open(Vector2 p) =>
        p.X >= 1 && p.Y >= 1 && p.X <= Size - 2 && p.Y <= Size - 2 && FromMountains(p) > 0 && !Sea(p) && !InLake(p);
}
