using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Three cached, vertex-coloured autumn leaf scatters for rakeable leaf patches.</summary>
internal static class LeafShape {
    public const int Variants = 3;
    private static readonly ArrayMesh?[] variants = new ArrayMesh[Variants];
    private static readonly Color[] palette = {
        new("c08a3a"), new("a8652c"), new("d0a347"), new("8e5a2a"), new("b8753a")
    };

    public static Mesh For(int id) {
        int variant = ((id % Variants) + Variants) % Variants;
        return variants[variant] ??= Build(variant);
    }

    private static ArrayMesh Build(int variant) {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var random = new Random(731 + variant * 997);
        ReadOnlySpan<Vector2> outline = [
            new(0, -.11f), new(.035f, -.06f), new(.02f, -.03f), new(.055f, .01f), new(.03f, .07f),
            new(0, .10f), new(-.03f, .07f), new(-.055f, .01f), new(-.02f, -.03f), new(-.035f, -.06f)
        ];
        Span<Vector3> points = stackalloc Vector3[10];

        for (int leaf = 0; leaf < 24; leaf++) {
            float angle = (float)random.NextDouble() * Mathf.Tau;
            float radius = .46f * MathF.Pow((float)random.NextDouble(), .75f);
            float yaw = (float)random.NextDouble() * Mathf.Tau;
            float size = .82f + (float)random.NextDouble() * .18f;
            float baseY = .008f + (float)random.NextDouble() * .035f;
            float sin = MathF.Sin(yaw), cos = MathF.Cos(yaw);
            var center = new Vector3(MathF.Cos(angle) * radius, baseY, MathF.Sin(angle) * radius);
            Vector3 Point(Vector2 p, float y) {
                float x = p.X * size, z = p.Y * size;
                return center + new Vector3(x * cos + z * sin, y, -x * sin + z * cos);
            }

            var ridge = Point(Vector2.Zero, .005f);
            var leafColor = palette[random.Next(palette.Length)];
            var ridgeColor = leafColor.Lightened(.08f);
            for (int i = 0; i < outline.Length; i++) {
                float curl = i is 0 or 5 ? .002f : 0;
                points[i] = Point(outline[i], curl);
            }
            for (int i = 0; i < points.Length; i++) {
                int next = (i + 1) % points.Length;
                Color sideColor = i < 5 ? leafColor.Lightened(.035f) : leafColor.Darkened(.035f);
                Triangle(surface, ridge, points[next], points[i], ridgeColor, sideColor, sideColor);
            }

            // A short tapered petiole continues from the leaf base.
            var stemLeft = Point(new Vector2(-.008f, .103f), .001f);
            var stemRight = Point(new Vector2(.008f, .103f), .001f);
            var stemTip = Point(new Vector2(0, .14f), .001f);
            Triangle(surface, stemLeft, stemRight, stemTip, leafColor.Darkened(.08f), leafColor.Darkened(.08f), leafColor.Darkened(.08f));
        }
        return surface.Commit();
    }

    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color colorA, Color colorB, Color colorC) {
        if ((c - a).Cross(b - a).Y < 0) (b, c, colorB, colorC) = (c, b, colorC, colorB);
        var normal = (c - a).Cross(b - a).Normalized();
        surface.SetNormal(normal); surface.SetColor(colorA); surface.AddVertex(a);
        surface.SetNormal(normal); surface.SetColor(colorB); surface.AddVertex(b);
        surface.SetNormal(normal); surface.SetColor(colorC); surface.AddVertex(c);
    }
}
