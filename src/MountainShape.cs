using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The mountain height used by both the landscape mesh and rock outcrops embedded into it.</summary>
internal static class MountainShape {
    private static readonly FastNoiseLite Ridges = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = .018f, FractalType = FastNoiseLite.FractalTypeEnum.Ridged, FractalOctaves = 4, Seed = 31 };
    public static float Height(Vector2 p) {
        float depth = -Terrain.FromMountains(p);
        if (depth <= 0) return 0;
        float h = 26 * (1 - MathF.Exp(-depth / 16)) + (Ridges.GetNoise2D(p.X, p.Y) + 1) * 13 * Math.Clamp(depth / 18, 0, 1);
        float seaward = Math.Clamp((Terrain.Coast(Terrain.Across(p)) + 14 - Terrain.Along(p)) / 14, 0, 1);
        return h * seaward;
    }

    /// <summary>Interpolate the actual two-unit landscape triangles rather than a different smooth height.</summary>
    public static float Surface(Vector2 p) {
        float x = MathF.Floor((p.X + 110) / 2) * 2 - 110, y = MathF.Floor((p.Y + 110) / 2) * 2 - 110;
        float u = (p.X - x) / 2, v = (p.Y - y) / 2;
        float a = Height(new(x, y)), b = Height(new(x + 2, y)), c = Height(new(x, y + 2)), d = Height(new(x + 2, y + 2));
        float h = u >= v ? a * (1 - u) + b * (u - v) + d * v : a * (1 - v) + d * u + c * (v - u);
        return Math.Max(0, h - .03f);
    }
    /// <summary>Subtract the tunnel volume from low mountain triangles; the rock shell hides its cut edges.</summary>
    public static void AddCarvedTriangle(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c) {
        void Emit(List<Vector3> polygon) {
            for (int i = 1; i + 1 < polygon.Count; i++) {
                tool.AddVertex(polygon[0]); tool.AddVertex(polygon[i]); tool.AddVertex(polygon[i + 1]);
            }
        }
        if (Math.Min(a.Y, Math.Min(b.Y, c.Y)) < 1.83f)
            foreach (var cliff in World.Cliffs) {
                var spot = World.MineSpot(cliff); var across = new Vector2(cliff.Face.Y, -cliff.Face.X);
                Vector3 Local(Vector3 v) {
                    var offset = new Vector2(v.X, v.Z) - spot;
                    return new(offset.Dot(across), v.Y, offset.Dot(cliff.Face));
                }
                var x = Local(a); var y = Local(b); var z = Local(c);
                if (Math.Min(x.X, Math.Min(y.X, z.X)) > .77f || Math.Max(x.X, Math.Max(y.X, z.X)) < -.77f ||
                    Math.Min(x.Z, Math.Min(y.Z, z.Z)) > -2.15f || Math.Max(x.Z, Math.Max(y.Z, z.Z)) < -5.5f) continue;
                var remaining = new List<Vector3> { a, b, c };
                // Partition the triangle against a convex tunnel box, retaining every outside piece.
                for (int plane = 0; plane < 6 && remaining.Count > 0; plane++) {
                    float Distance(Vector3 v) {
                        var p = Local(v);
                        return plane switch { 0 => p.X + .77f, 1 => .77f - p.X, 2 => p.Y + .1f, 3 => 1.83f - p.Y, 4 => p.Z + 5.5f, _ => -2.15f - p.Z };
                    }
                    var inside = new List<Vector3>(); var outside = new List<Vector3>();
                    var previous = remaining[^1]; float before = Distance(previous);
                    foreach (var current in remaining) {
                        float after = Distance(current);
                        if ((before >= 0) != (after >= 0)) {
                            var cut = previous.Lerp(current, before / (before - after));
                            inside.Add(cut); outside.Add(cut);
                        }
                        if (after >= 0) inside.Add(current); else outside.Add(current);
                        previous = current; before = after;
                    }
                    Emit(outside); remaining = inside;
                }
                return;
            }
        tool.AddVertex(a); tool.AddVertex(b); tool.AddVertex(c);
    }

}
