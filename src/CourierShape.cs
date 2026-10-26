using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared fitted hi-vis vest and cap details for the courier.</summary>
internal static class CourierShape {
    private static ArrayMesh? vest, cap;
    public static Mesh Vest => vest ??= BuildVest();
    public static Mesh Cap => cap ??= BuildCap();
    public static readonly StandardMaterial3D VestMaterial = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .9f };
    public static readonly StandardMaterial3D CapMaterial = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .85f };

    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildVest() {
        var s = Begin();
        // The shell follows the common torso profile. Front and back panels stop short of the armpits.
        ReadOnlySpan<float> ys = [.13f, .23f, .27f, .30f, .35f, .44f, .50f];
        ReadOnlySpan<float> widths = [.14f, .145f, .153f, .157f, .17f, .177f, .12f];
        ReadOnlySpan<float> depths = [.09f, .095f, .098f, .1f, .105f, .095f, .075f];
        const int segments = 6;
        for (int side = 0; side < 2; side++)
            for (int row = 0; row < ys.Length - 1; row++) {
                var color = row == 2 ? new Color("e4e6d8") : new Color("f07a1f");
                for (int column = 0; column < segments; column++) {
                    float a = Mathf.Lerp(-1.03f, 1.03f, column / (float)segments);
                    float b = Mathf.Lerp(-1.03f, 1.03f, (column + 1) / (float)segments);
                    var p0 = VestPoint(ys[row], widths[row], depths[row], a, side == 1);
                    var p1 = VestPoint(ys[row], widths[row], depths[row], b, side == 1);
                    var p2 = VestPoint(ys[row + 1], widths[row + 1], depths[row + 1], b, side == 1);
                    var p3 = VestPoint(ys[row + 1], widths[row + 1], depths[row + 1], a, side == 1);
                    Vector3 Normal(float width, float depth, float angle) => new Vector3(MathF.Sin(angle) / width, 0, (side == 1 ? -1 : 1) * MathF.Cos(angle) / depth).Normalized();
                    var n0 = Normal(widths[row], depths[row], a); var n1 = Normal(widths[row], depths[row], b);
                    var n2 = Normal(widths[row + 1], depths[row + 1], b); var n3 = Normal(widths[row + 1], depths[row + 1], a);
                    SmoothTriangle(s, p0, p1, p2, n0, n1, n2, color);
                    SmoothTriangle(s, p0, p2, p3, n0, n2, n3, color);
                }
            }
        return s.Commit();
    }

    private static Vector3 VestPoint(float y, float width, float depth, float angle, bool back) {
        float side = MathF.Sin(angle), front = MathF.Cos(angle) * (back ? -1 : 1);
        var normal = new Vector3(side / width, 0, front / depth).Normalized();
        return new Vector3(width * side, y, depth * front) + normal * .004f;
    }

    private static ArrayMesh BuildCap() {
        var s = Begin();
        const int sides = 10;
        const float radius = .13f, centerY = .17f, vertical = .105f;
        ReadOnlySpan<float> levels = [0, .35f, .7f];
        for (int row = 0; row < levels.Length - 1; row++)
            for (int side = 0; side < sides; side++) {
                float a = side * Mathf.Tau / sides, b = (side + 1) * Mathf.Tau / sides;
                Vector3 Point(float level, float angle) {
                    float r = radius * MathF.Sqrt(1 - level * level);
                    return new(r * MathF.Cos(angle), centerY + vertical * level, r * MathF.Sin(angle));
                }
                var p0 = Point(levels[row], a); var p1 = Point(levels[row], b);
                var p2 = Point(levels[row + 1], b); var p3 = Point(levels[row + 1], a);
                SmoothTriangle(s, p0, p1, p2, CapNormal(p0), CapNormal(p1), CapNormal(p2), new Color("2b5d8a"));
                SmoothTriangle(s, p0, p2, p3, CapNormal(p0), CapNormal(p2), CapNormal(p3), new Color("2b5d8a"));
            }
        float topY = centerY + vertical;
        for (int side = 0; side < sides; side++) {
            float a = side * Mathf.Tau / sides, b = (side + 1) * Mathf.Tau / sides;
            float r = radius * MathF.Sqrt(1 - .7f * .7f);
            var p0 = new Vector3(r * MathF.Cos(a), centerY + vertical * .7f, r * MathF.Sin(a));
            var p1 = new Vector3(r * MathF.Cos(b), centerY + vertical * .7f, r * MathF.Sin(b));
            var top = new Vector3(0, topY, 0);
            SmoothTriangle(s, p0, p1, top, CapNormal(p0), CapNormal(p1), Vector3.Up, new Color("2b5d8a"));
        }
        BuildVisor(s);
        return s.Commit();
    }

    private static Vector3 CapNormal(Vector3 p) => new Vector3(p.X / (.13f * .13f), (p.Y - .17f) / (.105f * .105f), p.Z / (.13f * .13f)).Normalized();

    private static void SmoothTriangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color color) {
        if ((c - a).Cross(b - a).Dot(na + nb + nc) < 0) { (b, c) = (c, b); (nb, nc) = (nc, nb); }
        s.SetColor(color);
        s.SetNormal(na); s.AddVertex(a); s.SetNormal(nb); s.AddVertex(b); s.SetNormal(nc); s.AddVertex(c);
    }

    private static void BuildVisor(SurfaceTool s) {
        const int segments = 8;
        const float halfWidth = .105f;
        Vector3 Point(int column, float along, float thickness) {
            float x = (column / (float)segments * 2 - 1) * halfWidth;
            float across = x / halfWidth;
            float edge = MathF.Sqrt(Math.Max(0, 1 - across * across));
            float rootZ = .13f * MathF.Sqrt(Math.Max(0, 1 - x * x / (.13f * .13f)));
            float z = rootZ + (.20f - rootZ) * along * edge;
            float y = .17f + .003f * edge + .002f * along * edge + thickness;
            return new(x, y, z);
        }
        for (int column = 0; column < segments; column++) {
            Vector3 a = Point(column, 0, 0), b = Point(column + 1, 0, 0);
            Vector3 c = Point(column + 1, 1, 0), d = Point(column, 1, 0);
            Vector3 outward = new(0, 1, .35f);
            Quad(s, a, b, c, d, outward, new Color("2b5d8a"));
            Quad(s, Point(column, 0, -.006f), Point(column, 1, -.006f), Point(column + 1, 1, -.006f), Point(column + 1, 0, -.006f),
                new(0, -1, .25f), new Color("234d74"));
            Quad(s, Point(column, 1, -.006f), Point(column + 1, 1, -.006f), Point(column + 1, 1, 0), Point(column, 1, 0),
                new(0, 0, 1), new Color("234d74"));
        }
        foreach (int column in new[] { 0, segments }) {
            float side = column == 0 ? -1 : 1;
            Quad(s, Point(column, 0, -.006f), Point(column, 1, -.006f), Point(column, 1, 0), Point(column, 0, 0),
                new(side, 0, 0), new Color("234d74"));
        }
    }

    private static void Quad(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color) {
        Triangle(s, a, b, c, outward, color); Triangle(s, a, c, d, outward, color);
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color color) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        s.SetColor(color); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
