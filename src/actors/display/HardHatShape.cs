using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A worker's fitted hard hat with a rounded crown, a low ridge and a short peak over the face.</summary>
internal static class HardHatShape {
    private const int Sides = 24;
    private const float Seat = .184f, HalfWidth = .135f, HalfDepth = .15f, Rise = .139f;
    private static ArrayMesh? crown, brim;

    public static void Wear(Node3D head) {
        Add(head, crown ??= BuildCrown(), Mat("e8b323", .53f), Vector3.Zero);
        Add(head, brim ??= BuildBrim(), Mat("dcaa22", .55f), Vector3.Zero);
    }

    private static SurfaceTool Begin() {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        return surface;
    }

    private static ArrayMesh BuildCrown() {
        var surface = Begin();
        const int bands = 7;
        for (int side = 0; side < Sides; side++) {
            float a = side * Mathf.Tau / Sides, b = (side + 1) * Mathf.Tau / Sides;
            for (int band = 0; band < bands; band++) {
                float bottom = band * Mathf.Pi / (bands * 2), top = (band + 1) * Mathf.Pi / (bands * 2);
                var p0 = Point(bottom, a); var p1 = Point(bottom, b);
                if (band == bands - 1) {
                    var peak = new Vector3(0, Seat + Rise, 0);
                    Triangle(surface, p0, p1, peak, Normal(p0), Normal(p1), Vector3.Up);
                    continue;
                }
                var p2 = Point(top, b); var p3 = Point(top, a);
                Triangle(surface, p0, p1, p2, Normal(p0), Normal(p1), Normal(p2));
                Triangle(surface, p0, p2, p3, Normal(p0), Normal(p2), Normal(p3));
            }
        }

        const int ridgeSteps = 14;
        for (int step = 0; step < ridgeSteps; step++) {
            float a = Mathf.Lerp(-1.28f, 1.28f, (float)step / ridgeSteps);
            float b = Mathf.Lerp(-1.28f, 1.28f, (float)(step + 1) / ridgeSteps);
            float middle = (a + b) / 2;
            var top = new Vector3(0, MathF.Cos(middle), MathF.Sin(middle));
            for (int edge = 0; edge < 3; edge++) {
                Vector3 outward = edge == 0 ? Vector3.Left : edge == 2 ? Vector3.Right : top;
                Quad(surface, Ridge(a, edge), Ridge(b, edge), Ridge(b, edge + 1), Ridge(a, edge + 1), outward);
            }
        }
        return surface.Commit();

        static Vector3 Point(float level, float angle) => new(
            HalfWidth * MathF.Cos(level) * MathF.Cos(angle), Seat + Rise * MathF.Sin(level),
            HalfDepth * MathF.Cos(level) * MathF.Sin(angle));
        static Vector3 Normal(Vector3 point) => new Vector3(
            point.X / (HalfWidth * HalfWidth), (point.Y - Seat) / (Rise * Rise), point.Z / (HalfDepth * HalfDepth)).Normalized();
        static Vector3 Ridge(float angle, int edge) {
            float x = edge switch { 0 => -.015f, 1 => -.009f, 2 => .009f, _ => .015f };
            float taper = Mathf.SmoothStep(0, 1, (1.28f - MathF.Abs(angle)) / .43f);
            float swell = MathF.Sqrt(1 - x * x / (HalfWidth * HalfWidth));
            var basePoint = new Vector3(x, Seat + Rise * swell * MathF.Cos(angle), HalfDepth * swell * MathF.Sin(angle));
            return basePoint + Normal(basePoint) * (edge is 0 or 3 ? -.001f : .01f * taper - .0005f);
        }
    }

    private static ArrayMesh BuildBrim() {
        var surface = Begin();
        var top = new Vector3(0, Seat, 0);
        var bottom = top + Vector3.Down * .011f;
        for (int side = 0; side < Sides; side++) {
            var a = Edge(side * Mathf.Tau / Sides);
            var b = Edge((side + 1) * Mathf.Tau / Sides);
            var c = b + Vector3.Down * .011f;
            var d = a + Vector3.Down * .011f;
            Triangle(surface, top, a, b, Vector3.Up, Vector3.Up, Vector3.Up);
            Triangle(surface, bottom, d, c, Vector3.Down, Vector3.Down, Vector3.Down);
            Quad(surface, a, b, c, d, new Vector3(a.X + b.X, 0, a.Z + b.Z));
        }
        return surface.Commit();

        static Vector3 Edge(float angle) {
            float forward = MathF.Sin(angle);
            float peak = MathF.Pow(MathF.Max(0, forward), 3);
            return new Vector3(MathF.Cos(angle) * .151f, Seat - .009f * peak, .169f * forward + .041f * peak);
        }
    }

    private static void Quad(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) normal = -normal;
        Triangle(s, a, b, c, normal, normal, normal);
        Triangle(s, a, c, d, normal, normal, normal);
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc) {
        if ((c - a).Cross(b - a).Dot(na + nb + nc) < 0) { (b, c) = (c, b); (nb, nc) = (nc, nb); }
        s.SetNormal(na); s.AddVertex(a);
        s.SetNormal(nb); s.AddVertex(b);
        s.SetNormal(nc); s.AddVertex(c);
    }
}
