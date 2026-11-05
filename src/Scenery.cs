using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The baked scenery candidates ProtonScatter made offline, and the ground they may stand on.
/// The view dresses the valley with most of them; the world places the loose stones and sticks among them as things to pick up.
/// </summary>
public static class Scenery {
    /// <summary>A candidate's spot, its turn about the vertical and its size.</summary>
    public readonly record struct Point(Vector2 At, float Yaw, float Size);

    /// <summary>Every candidate in its baked order, read once; its index picks what grows there.</summary>
    public static IReadOnlyList<Point> Points => points ??= Load();
    private static Point[]? points;

    // Patches where scenery thins out, the same for every machine.
    private static readonly FastNoiseLite Patches = new() { Seed = 671, Frequency = .18f, FractalOctaves = 2 };

    private static Point[] Load() {
        using var document = JsonDocument.Parse(FileAccess.GetFileAsString("res://assets/environment/proton_scatter/placements.json"));
        var list = new List<Point>();
        foreach (var p in document.RootElement.GetProperty("points").EnumerateArray())
            list.Add(new Point(new Vector2(p[0].GetSingle(), p[1].GetSingle()), p[2].GetSingle(), p[3].GetSingle()));
        return list.ToArray();
    }

    /// <summary>How thick scenery grows at a spot, from -1 to 1; below <see cref="Bare"/> nothing grows.</summary>
    public static float Density(Vector2 p) => Patches.GetNoise2D(p.X, p.Y);
    public const float Bare = -.32f;

    /// <summary>Whether scenery may stand at a spot: dry open ground off the beach, the lake shore, the mountain feet, the clearing, its path and the mine approaches.</summary>
    public static bool Dressable(Vector2 p) {
        if (!Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, 1.1f) || Terrain.FromMountains(p) < 2) return false;
        float along = Terrain.Along(p), across = Terrain.Across(p), path = Terrain.Across(Terrain.Spawn) + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f;
        if (p.DistanceTo(Terrain.Spawn) < 4.5f || along > Terrain.Along(Terrain.Spawn) - 1 && Math.Abs(across - path) < 1.1f) return false;
        foreach (var cliff in World.Cliffs) {
            var offset = p - World.MineSpot(cliff); float depth = offset.Dot(cliff.Face);
            if (Math.Abs(offset.Dot(cliff.Along)) < 3 && depth > -6 && depth < 2) return false;
        }
        return Density(p) >= Bare;
    }

    /// <summary>The loose piece a candidate is, a stone or a stick, or null for one the view dresses with plants.</summary>
    public static ThingKind? Loose(int index) => index % 127 == 0 ? ThingKind.LooseStone : index % 293 == 0 ? ThingKind.Stick : null;
}
