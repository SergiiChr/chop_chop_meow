using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared fitted bench joinery, bevelled planks and a mounted bench vise.</summary>
internal static class BenchShape
{
    private static ArrayMesh? wood, vise;
    public static Mesh Wood => wood ??= BuildWood();
    public static Mesh Vise => vise ??= BuildVise();
    public static readonly StandardMaterial3D Timber = new() { VertexColorUseAsAlbedo = true, Roughness = .85f };
    public static readonly StandardMaterial3D Steel = new() { VertexColorUseAsAlbedo = true, Roughness = .45f, Metallic = .6f };

    private static ArrayMesh BuildWood()
    {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        foreach (float x in new[] { -.49f, .49f })
            foreach (float z in new[] { -.24f, .24f })
                Box(s, new Vector3(.09f, .745f, .09f), new Vector3(x, .3725f, z), "7a5432");
        foreach (float z in new[] { -.25f, .25f })
        {
            Box(s, new Vector3(1, .15f, .06f), new Vector3(0, .67f, z), "8a603c");
            Box(s, new Vector3(1, .055f, .045f), new Vector3(0, .155f, z * .9f), "7a5432");
        }
        foreach (float x in new[] { -.49f, .49f })
        {
            Box(s, new Vector3(.06f, .15f, .45f), new Vector3(x, .67f, 0), "8a603c");
            Box(s, new Vector3(.045f, .055f, .46f), new Vector3(x, .155f, 0), "7a5432");
        }
        Bevel(s, new Vector3(.96f, .035f, .46f), new Vector3(0, .20f, 0), .006f, "967045");
        for (int i = -1; i <= 1; i++)
            Bevel(s, new Vector3(1.12f, .07f, .202f), new Vector3(0, .78f, i * .206f), .012f, i == 0 ? "b98552" : "c28e59");
        return s.Commit();
    }

    private static ArrayMesh BuildVise()
    {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        Bevel(s, new Vector3(.28f, .035f, .22f), new Vector3(.35f, .8325f, .22f), .008f, "4a4d50");
        Box(s, new Vector3(.14f, .035f, .14f), new Vector3(.35f, .8375f, .33f), "45494c");
        foreach (float z in new[] { .2575f, .3775f })
            Bevel(s, new Vector3(.26f, .09f, .055f), new Vector3(.35f, .895f, z), .008f, "4a4d50");
        // The two flat jaw plates face each other across the screw's travel.
        Box(s, new Vector3(.235f, .025f, .008f), new Vector3(.35f, .9225f, .289f), "939b9e");
        Box(s, new Vector3(.235f, .025f, .008f), new Vector3(.35f, .9225f, .346f), "939b9e");
        return s.Commit();
    }

    private static void Bevel(SurfaceTool s, Vector3 size, Vector3 center, float bevel, string tint)
    {
        float x = size.X / 2, z = size.Z / 2, y = size.Y / 2;
        Vector3[] Ring(float width, float depth, float height, float corner) => new[] {
            center + new Vector3(-width + corner, height, -depth), center + new Vector3(width - corner, height, -depth),
            center + new Vector3(width, height, -depth + corner), center + new Vector3(width, height, depth - corner),
            center + new Vector3(width - corner, height, depth), center + new Vector3(-width + corner, height, depth),
            center + new Vector3(-width, height, depth - corner), center + new Vector3(-width, height, -depth + corner) };
        Vector3[][] rings = { Ring(x - bevel, z - bevel, -y, bevel * .4f), Ring(x, z, -y + bevel, bevel),
            Ring(x, z, y - bevel, bevel), Ring(x - bevel, z - bevel, y, bevel * .4f) };
        for (int row = 0; row < rings.Length - 1; row++)
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                var outward = (rings[row][i] + rings[row][next]) / 2 - center; outward.Y = 0;
                Triangle(s, rings[row][i], rings[row][next], rings[row + 1][next], outward, tint);
                Triangle(s, rings[row][i], rings[row + 1][next], rings[row + 1][i], outward, tint);
            }
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            Triangle(s, center - Vector3.Up * y, rings[0][i], rings[0][next], Vector3.Down, tint);
            Triangle(s, center + Vector3.Up * y, rings[3][i], rings[3][next], Vector3.Up, tint);
        }
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
