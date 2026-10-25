using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared cabin shell and sheet-metal geometry, fitted to the same shed-roof plane.</summary>
internal static class CabinShape
{
    private static ArrayMesh? walls, roof, interior;
    public static readonly StandardMaterial3D WallMaterial = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .95f };
    private const float Pitch = 9 * Mathf.Pi / 180;
    private const float Thickness = .06f, Ripple = .018f;

    // Seat the walls inside the sheet thickness so raised corrugations cannot leave daylight gaps.
    public static float WallTop(float z) => 2.68f + MathF.Tan(Pitch) * (z + .05f) + (Ripple - Thickness / 2) / MathF.Cos(Pitch);

    public static Mesh Walls => walls ??= BuildWalls();
    public static Mesh Roof => roof ??= BuildRoof();
    public static Mesh Interior => interior ??= BuildInterior();

    private static ArrayMesh BuildWalls()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var normal = (c - a).Cross(b - a).Normalized();
            var center = (a + b + c + d) / 4;
            bool inside = normal.Z > .9f && Mathf.IsEqualApprox(center.Z, -1.01f)
                || normal.Z < -.9f && Mathf.IsEqualApprox(center.Z, .91f)
                || normal.X > .9f && Mathf.IsEqualApprox(center.X, -1.11f)
                || normal.X < -.9f && Mathf.IsEqualApprox(center.X, 1.11f);
            surface.SetColor(new Color(inside ? "74614c" : "c8a46a"));
            Quad(surface, a, b, c, d);
        }
        void Patch(float x0, float x1, float z0, float z1, float bottom, float? top = null)
        {
            float back = top ?? WallTop(z0), front = top ?? WallTop(z1);
            var a = new Vector3(x0, bottom, z0); var b = new Vector3(x1, bottom, z0);
            var c = new Vector3(x1, bottom, z1); var d = new Vector3(x0, bottom, z1);
            var e = new Vector3(x0, back, z0); var f = new Vector3(x1, back, z0);
            var g = new Vector3(x1, front, z1); var h = new Vector3(x0, front, z1);
            Face(a, d, c, b); Face(e, f, g, h);
            Face(a, b, f, e); Face(d, h, g, c);
            Face(a, e, h, d); Face(b, c, g, f);
        }
        Patch(-1.2f, 1.2f, -1.1f, -1.01f, .42f);
        Patch(-1.2f, -1.11f, -1.01f, .91f, .42f);
        // The right wall surrounds the side window.
        Patch(1.11f, 1.2f, -1.01f, -.4f, .42f);
        Patch(1.11f, 1.2f, .4f, .91f, .42f);
        Patch(1.11f, 1.2f, -.4f, .4f, .42f, 1.3f);
        Patch(1.11f, 1.2f, -.4f, .4f, 1.9f);
        // Front patches leave real openings behind the animated door and front pane.
        Patch(-1.2f, -.76f, .91f, 1, .42f);
        Patch(-.76f, .08f, .91f, 1, 2.29f);
        Patch(.08f, .25f, .91f, 1, .42f);
        Patch(.85f, 1.2f, .91f, 1, .42f);
        Patch(.25f, .85f, .91f, 1, .42f, 1.4f);
        Patch(.25f, .85f, .91f, 1, 1.9f);
        return surface.Commit();
    }

    private static ArrayMesh BuildInterior()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Box(Vector3 size, Vector3 center, string color)
        {
            var min = center - size / 2; var max = center + size / 2;
            var a = new Vector3(min.X, min.Y, min.Z); var b = new Vector3(max.X, min.Y, min.Z);
            var c = new Vector3(max.X, min.Y, max.Z); var d = new Vector3(min.X, min.Y, max.Z);
            var e = new Vector3(min.X, max.Y, min.Z); var f = new Vector3(max.X, max.Y, min.Z);
            var g = new Vector3(max.X, max.Y, max.Z); var h = new Vector3(min.X, max.Y, max.Z);
            surface.SetColor(new Color(color));
            Quad(surface, a, d, c, b); Quad(surface, e, f, g, h);
            Quad(surface, a, b, f, e); Quad(surface, d, h, g, c);
            Quad(surface, a, e, h, d); Quad(surface, b, c, g, f);
        }
        Box(new Vector3(2.22f, .02f, 1.92f), new Vector3(0, .417f, -.05f), "a58861");
        Box(new Vector3(.035f, .1f, 1.92f), new Vector3(-1.09f, .477f, -.05f), "4b3d2d");
        Box(new Vector3(.035f, .1f, 1.92f), new Vector3(1.09f, .477f, -.05f), "4b3d2d");
        Box(new Vector3(2.18f, .1f, .035f), new Vector3(0, .477f, -.99f), "4b3d2d");
        Box(new Vector3(1.02f, .1f, .035f), new Vector3(.6f, .477f, .89f), "4b3d2d");
        Box(new Vector3(.75f, .22f, 1.4f), new Vector3(-.72f, .537f, .05f), "685137");
        // A bevelled sleeping pad keeps the small interior readable without fine furnishings.
        Vector3[] outline = {
            new(-.305f, 0, -.675f), new(.305f, 0, -.675f), new(.365f, 0, -.615f), new(.365f, 0, .615f),
            new(.305f, 0, .675f), new(-.305f, 0, .675f), new(-.365f, 0, .615f), new(-.365f, 0, -.615f) };
        surface.SetColor(new Color("8b9d95"));
        float[] levels = { .65f, .675f, .745f };
        float[] scales = { .95f, 1f, .95f };
        Vector3 Point(int row, int corner) => new Vector3(-.72f, levels[row], .05f) + outline[corner] * scales[row];
        for (int row = 0; row < levels.Length - 1; row++)
            for (int corner = 0; corner < outline.Length; corner++)
            {
                int next = (corner + 1) % outline.Length;
                Quad(surface, Point(row, corner), Point(row, next), Point(row + 1, next), Point(row + 1, corner));
            }
        for (int end = 0; end < 2; end++)
        {
            int row = end == 0 ? 0 : levels.Length - 1;
            for (int corner = 0; corner < outline.Length; corner++)
            {
                int next = (corner + 1) % outline.Length;
                surface.SetNormal(end == 0 ? Vector3.Down : Vector3.Up);
                surface.AddVertex(new Vector3(-.72f, levels[row], .05f));
                surface.AddVertex(Point(row, end == 0 ? next : corner));
                surface.AddVertex(Point(row, end == 0 ? corner : next));
            }
        }
        return surface.Commit();
    }

    private static ArrayMesh BuildRoof()
    {
        const int segments = 60;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        float Wave(float x) => Ripple * MathF.Cos((x + 1.45f) * Mathf.Tau / .29f);
        Vector3 Normal(float x) => new Vector3(Ripple * Mathf.Tau / .29f * MathF.Sin((x + 1.45f) * Mathf.Tau / .29f), 1, 0).Normalized();
        for (int i = 0; i < segments; i++)
        {
            float x0 = -1.45f + 2.9f * i / segments, x1 = -1.45f + 2.9f * (i + 1) / segments;
            float y0 = Wave(x0), y1 = Wave(x1);
            var a = new Vector3(x0, y0 + Thickness / 2, -1.4f); var b = new Vector3(x1, y1 + Thickness / 2, -1.4f);
            var c = new Vector3(x1, y1 + Thickness / 2, 1.4f); var d = new Vector3(x0, y0 + Thickness / 2, 1.4f);
            SmoothQuad(surface, a, b, c, d, Normal(x0), Normal(x1));
            Quad(surface, a - Vector3.Up * Thickness, d - Vector3.Up * Thickness, c - Vector3.Up * Thickness, b - Vector3.Up * Thickness);
            Quad(surface, a, a - Vector3.Up * Thickness, b - Vector3.Up * Thickness, b);
            Quad(surface, d, c, c - Vector3.Up * Thickness, d - Vector3.Up * Thickness);
            if (i == 0) Quad(surface, a, d, d - Vector3.Up * Thickness, a - Vector3.Up * Thickness);
            if (i == segments - 1) Quad(surface, b, b - Vector3.Up * Thickness, c - Vector3.Up * Thickness, c);
        }
        return surface.Commit();
    }

    private static void Quad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var normal = (c - a).Cross(b - a).Normalized();
        surface.SetNormal(normal);
        surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
        surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(d);
    }

    private static void SmoothQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 left, Vector3 right)
    {
        surface.SetNormal(left); surface.AddVertex(a);
        surface.SetNormal(right); surface.AddVertex(b); surface.AddVertex(c);
        surface.SetNormal(left); surface.AddVertex(a);
        surface.SetNormal(right); surface.AddVertex(c);
        surface.SetNormal(left); surface.AddVertex(d);
    }
}
