using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The mountain height used by Terrain3D and the rock outcrops embedded into it.</summary>
internal static class MountainShape {
    private static readonly FastNoiseLite Ridges = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = .018f, FractalType = FastNoiseLite.FractalTypeEnum.Ridged, FractalOctaves = 4, Seed = 31 };
    public static float Height(Vector2 p) {
        float depth = -Terrain.FromMountains(p);
        if (depth <= 0) return 0;
        float h = 26 * (1 - MathF.Exp(-depth / 16)) + (Ridges.GetNoise2D(p.X, p.Y) + 1) * 13 * Math.Clamp(depth / 18, 0, 1);
        float seaward = Math.Clamp((Terrain.Coast(Terrain.Across(p)) + 14 - Terrain.Along(p)) / 14, 0, 1);
        return h * seaward;
    }

    /// <summary>Interpolate a stable two-unit height proxy used to fit mine rock and talus to the mountains.</summary>
    public static float Surface(Vector2 p) {
        float x = MathF.Floor((p.X + 110) / 2) * 2 - 110, y = MathF.Floor((p.Y + 110) / 2) * 2 - 110;
        float u = (p.X - x) / 2, v = (p.Y - y) / 2;
        float a = Height(new(x, y)), b = Height(new(x + 2, y)), c = Height(new(x, y + 2)), d = Height(new(x + 2, y + 2));
        float h = u >= v ? a * (1 - u) + b * (u - v) + d * v : a * (1 - v) + d * u + c * (v - u);
        return Math.Max(0, h - .03f);
    }
}
