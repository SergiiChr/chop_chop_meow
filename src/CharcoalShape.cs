using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Charcoal as pressed briquettes: six-sided logs with a hole through the middle, two lying and one leaning on them,
/// so charcoal reads apart from coal's rounded lumps in an icon and in a pile.
/// </summary>
internal static class CharcoalShape {
    private const float Radius = .125f, Hole = .04f;
    private const int Sides = 6;
    /// <summary>A lying briquette rests on a flat side, its middle this far above the ground.</summary>
    private static readonly float Rest = Radius * MathF.Cos(MathF.PI / Sides);
    // Each briquette from one end's middle to the other's: one running back from the game camera, one beside it pointing at the camera, one standing in front and leaning on them.
    private static readonly (Vector3 Start, Vector3 End)[] Logs = {
        (new(-.06f, Rest, .22f), new(.02f, Rest, -.30f)),
        (new(.22f, Rest, -.20f), new(.44f, Rest, .06f)),
        (new(.18f, .11f, .18f), new(0, .56f, .12f)),
    };
    private static ArrayMesh? heap, briquette;

    /// <summary>The colours the mesh carries: lighter and darker sides, the pressed ends and the dark of the hole.</summary>
    public static readonly StandardMaterial3D Material = ModelKit.Vertex(Surface.Stone, .95f);

    /// <summary>The three briquettes in their own proportions, for the icon.</summary>
    public static Mesh Heap => heap ??= Build(Logs);
    /// <summary>One briquette lying on its side, the piece a pile heaps up.</summary>
    public static Mesh Briquette => briquette ??= Build((new(-.26f, Rest, 0), new(.26f, Rest, 0)));

    /// <summary>Builds briquettes, each from one end's middle to the other's, centred on the origin.</summary>
    private static ArrayMesh Build(params (Vector3 Start, Vector3 End)[] logs) {
        var points = new List<(Vector3 A, Vector3 B, Vector3 C, Vector3 Outward, string Tint)>();
        foreach (var (start, end) in logs) Log(points, start, end);
        var min = points[0].A; var max = min;
        foreach (var (a, b, c, _, _) in points) { min = min.Min(a).Min(b).Min(c); max = max.Max(a).Max(b).Max(c); }
        var centre = (min + max) / 2;
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var (a, b, c, outward, tint) in points) {
            Vector3 p = a - centre, q = b - centre, r = c - centre;
            var normal = (r - p).Cross(q - p).Normalized();
            if (normal.Dot(outward) < 0) { (q, r) = (r, q); normal = -normal; }
            s.SetColor(new Color(tint)); s.SetNormal(normal); s.AddVertex(p); s.AddVertex(q); s.AddVertex(r);
        }
        return s.Commit();
    }

    /// <summary>One briquette: six sides in two shades, a ring at each end around the hole, and the hole's own walls.</summary>
    private static void Log(List<(Vector3, Vector3, Vector3, Vector3, string)> points, Vector3 start, Vector3 end) {
        var axis = (end - start).Normalized();
        // A lying log's first rim point lies level with its middle, which puts a flat side on the ground.
        var across = (MathF.Abs(axis.Y) > .9f ? Vector3.Right : Vector3.Up).Cross(axis).Normalized();
        var over = axis.Cross(across);
        Vector3 Spoke(int i) { float turn = i * Mathf.Tau / Sides; return across * MathF.Cos(turn) + over * MathF.Sin(turn); }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, string tint) { points.Add((a, b, c, outward, tint)); points.Add((a, c, d, outward, tint)); }
        for (int i = 0; i < Sides; i++) {
            Vector3 here = Spoke(i), next = Spoke(i + 1), facing = here + next;
            Quad(start + here * Radius, start + next * Radius, end + next * Radius, end + here * Radius, facing, i % 2 == 0 ? "625851" : "3f3833");
            Quad(start + here * Hole, start + next * Hole, end + next * Hole, end + here * Hole, -facing, "0e0d0c");
            foreach (var (middle, outward) in new[] { (start, -axis), (end, axis) })
                Quad(middle + here * Radius, middle + next * Radius, middle + next * Hole, middle + here * Hole, outward, "38322e");
        }
    }
}
