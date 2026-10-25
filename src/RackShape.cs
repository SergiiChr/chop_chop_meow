using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Cached plank rack with fitted concrete feet and separately removable shaped hand tools.</summary>
internal static class RackShape
{
    private static ArrayMesh? feet, frame, axe, pick, rake;
    public static Mesh Feet => feet ??= BuildFeet();
    public static Mesh Frame => frame ??= BuildFrame();
    public static Mesh Axe => axe ??= BuildTool("axe");
    public static Mesh Pick => pick ??= BuildTool("pickaxe");
    public static Mesh Rake => rake ??= BuildTool("rake");
    public static readonly StandardMaterial3D Concrete = Material(.95f);
    public static readonly StandardMaterial3D Timber = Material(.85f);
    public static readonly StandardMaterial3D Tool = Material(.65f, .15f);
    private static StandardMaterial3D Material(float roughness, float metallic = 0) => new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = roughness, Metallic = metallic };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildFeet()
    {
        var s = Begin();
        foreach (float x in new[] { -.43f, .43f })
            Bevel(s, new(.26f, .22f, .36f), new(x, .11f, 0), .012f, "9a9d99");
        return s.Commit();
    }

    private static ArrayMesh BuildFrame()
    {
        var s = Begin();
        foreach (float x in new[] { -.48f, .48f })
            Box(s, new(.1f, 1.31f, .1f), new(x, .855f, -.04f), "8a6a48");
        for (int row = 0; row < 4; row++)
            Bevel(s, new(1.05f, .218f, .045f), new(0, .6625f + row * .225f, -.025f), .004f, row % 2 == 0 ? "d2a26a" : "c69862");
        Bevel(s, new(1.15f, .06f, .2f), new(0, 1.5f, -.025f), .009f, "99734d");
        Box(s, new(.98f, .07f, .06f), new(0, .48f, -.04f), "8a6a48");
        // The protruding peg ends remain when tools are taken from their slots.
        foreach (float x in new[] { -.3f, 0, .3f })
            Box(s, new(.025f, .025f, .115f), new(x, 1.32f, .05f), "8a6a48");
        return s.Commit();
    }

    private static ArrayMesh BuildTool(string kind)
    {
        var s = Begin();
        Handle(s, kind == "rake" ? .75f : .72f);
        switch (kind)
        {
            case "axe":
                // A narrow eye flares towards a broad, tapered cutting edge.
                Prism(s, new[] { new Vector2(.045f, .025f), new(-.065f, .025f), new(-.18f, .065f), new(-.18f, -.115f), new(-.065f, -.065f), new(.045f, -.065f) }, .035f, "aebabc");
                break;
            case "pickaxe":
                Prism(s, new[] { new Vector2(-.22f, -.08f), new(-.13f, -.005f), new(-.035f, .025f), new(.035f, .025f), new(.13f, -.005f), new(.22f, -.08f), new(.12f, -.04f), new(.025f, -.025f), new(-.025f, -.025f), new(-.12f, -.04f) }, .035f, "aebabc");
                break;
            case "rake":
                Box(s, new(.3f, .04f, .04f), new(0, -.74f, 0), "aebabc");
                for (int i = 0; i < 5; i++)
                {
                    float x = -.12f + i * .06f;
                    // Tines project out from the backing planks.
                    Prism(s, new[] { new Vector2(-.008f, -.755f), new(.008f, -.755f), new(.008f, -.855f), new(-.008f, -.855f) }, .025f, "aebabc", new Vector3(x, 0, .02f));
                }
                break;
        }
        return s.Commit();
    }

    private static void Handle(SurfaceTool s, float length)
    {
        const int sides = 6;
        Vector2[] profile = { new(.017f, -length), new(.022f, -length + .035f), new(.018f, .005f) };
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.Tau / sides, b = (i + 1) * Mathf.Tau / sides;
                Vector3 Point(int ring, float angle) => new(MathF.Cos(angle) * profile[ring].X, profile[ring].Y, MathF.Sin(angle) * profile[ring].X);
                Quad(s, Point(row, a), Point(row, b), Point(row + 1, b), Point(row + 1, a), new(MathF.Cos(a), 0, MathF.Sin(a)), "e0b07a");
                if (row == 0) Triangle(s, new(0, -length, 0), Point(0, a), Point(0, b), Vector3.Down, "c99a61");
                else Triangle(s, new(0, .005f, 0), Point(2, a), Point(2, b), Vector3.Up, "e0b07a");
            }
    }

    private static void Prism(SurfaceTool s, Vector2[] outline, float depth, string tint, Vector3 offset = default)
    {
        Vector3 Point(int i, float z) => offset + new Vector3(outline[i].X, outline[i].Y, z);
        float half = depth / 2;
        // Triangulate the concave pick arch and axe shoulder without adding centre vertices.
        var faces = Geometry2D.TriangulatePolygon(outline);
        for (int i = 0; i < faces.Length; i += 3)
        {
            Triangle(s, Point(faces[i], -half), Point(faces[i + 1], -half), Point(faces[i + 2], -half), Vector3.Forward, tint);
            Triangle(s, Point(faces[i], half), Point(faces[i + 1], half), Point(faces[i + 2], half), Vector3.Back, tint);
        }
        float winding = Geometry2D.IsPolygonClockwise(outline) ? 1 : -1;
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            var edge = outline[next] - outline[i];
            Quad(s, Point(i, -half), Point(next, -half), Point(next, half), Point(i, half), new Vector3(-edge.Y, edge.X, 0) * winding, tint);
        }
    }

    private static void Bevel(SurfaceTool s, Vector3 size, Vector3 center, float bevel, string tint)
    {
        // Four-corner rings chamfer the horizontal edges without subdividing every vertical corner.
        float x = size.X / 2, z = size.Z / 2, y = size.Y / 2;
        Vector3[] Ring(float width, float depth, float height) => new[] {
            center + new Vector3(-width, height, -depth), center + new Vector3(width, height, -depth),
            center + new Vector3(width, height, depth), center + new Vector3(-width, height, depth) };
        Vector3[][] rings = { Ring(x - bevel, z - bevel, -y), Ring(x, z, -y + bevel),
            Ring(x, z, y - bevel), Ring(x - bevel, z - bevel, y) };
        for (int row = 0; row < rings.Length - 1; row++)
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                var outward = (rings[row][i] + rings[row][next]) / 2 - center; outward.Y = 0;
                Quad(s, rings[row][i], rings[row][next], rings[row + 1][next], rings[row + 1][i], outward, tint);
            }
        Quad(s, rings[0][0], rings[0][1], rings[0][2], rings[0][3], Vector3.Down, tint);
        Quad(s, rings[3][0], rings[3][1], rings[3][2], rings[3][3], Vector3.Up, tint);
    }

    private static void Box(SurfaceTool s, Vector3 size, Vector3 center, string tint)
    {
        var half = size / 2;
        var a = center + new Vector3(-half.X, -half.Y, -half.Z); var b = center + new Vector3(half.X, -half.Y, -half.Z);
        var c = center + new Vector3(half.X, -half.Y, half.Z); var d = center + new Vector3(-half.X, -half.Y, half.Z);
        var up = Vector3.Up * size.Y;
        Quad(s, a, b, c, d, Vector3.Down, tint); Quad(s, a + up, d + up, c + up, b + up, Vector3.Up, tint);
        Quad(s, a, a + up, b + up, b, Vector3.Forward, tint); Quad(s, d, c, c + up, d + up, Vector3.Back, tint);
        Quad(s, a, d, d + up, a + up, Vector3.Left, tint); Quad(s, b, b + up, c + up, c, Vector3.Right, tint);
    }

    private static void Quad(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, string tint)
    {
        Triangle(s, a, b, c, outward, tint); Triangle(s, a, c, d, outward, tint);
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, string tint)
    {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        s.SetColor(new Color(tint)); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
