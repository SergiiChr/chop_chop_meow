using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Cached timber sign, framed chalkboard and curved sheet-metal mailbox.</summary>
internal static class SignpostShape
{
    private static ArrayMesh? timber, mailbox, flag;
    public static Mesh Timber => timber ??= BuildTimber();
    public static Mesh Mailbox => mailbox ??= BuildMailbox();
    public static Mesh Flag => flag ??= BuildFlag();
    public static readonly StandardMaterial3D Wood = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .9f };
    public static readonly StandardMaterial3D Steel = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .55f, Metallic = .35f };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildTimber()
    {
        var s = Begin();
        Bevel(s, new(.12f, 1.95f, .12f), new(0, .975f, 0), .012f, "8a6a48");
        Bevel(s, new(.3f, .06f, .3f), new(0, .03f, 0), .012f, "9a9b97");
        Bevel(s, new(.97f, .44f, .045f), new(.3f, 1.55f, .085f), .008f, "85633c");
        Box(s, new(.91f, .38f, .008f), new(.3f, 1.55f, .111f), "2f5d4a");
        // A painted arrow is a flat face, rather than a protruding cone.
        Vector3[] arrow = { new(-.04f, 1.51f, .116f), new(.54f, 1.51f, .116f), new(.54f, 1.45f, .116f), new(.7f, 1.55f, .116f), new(.54f, 1.65f, .116f), new(.54f, 1.59f, .116f), new(-.04f, 1.59f, .116f) };
        // The arrow's rectangular shaft and triangular head keep its concave outline clean.
        Quad(s, arrow[0], arrow[1], arrow[5], arrow[6], Vector3.Back, "eee9d9");
        Triangle(s, arrow[2], arrow[3], arrow[4], Vector3.Back, "eee9d9");
        foreach (float x in new[] { -.1f, .7f })
            Quad(s, new(x - .009f, 1.699f, .117f), new(x + .009f, 1.699f, .117f), new(x + .009f, 1.717f, .117f), new(x - .009f, 1.717f, .117f), Vector3.Back, "c1b7a2");
        // A framed blackboard hangs below the sign, supported from the post.
        foreach (float y in new[] { .6f, 1.18f }) Box(s, new(.8f, .045f, .05f), new(.37f, y, .065f), "84613d");
        Box(s, new(.66f, .7f, .035f), new(.43f, .89f, .09f), "222d28");
        foreach (float x in new[] { .087f, .773f }) Box(s, new(.036f, .756f, .05f), new(x, .89f, .101f), "9e794b");
        foreach (float y in new[] { .524f, 1.256f }) Box(s, new(.72f, .036f, .05f), new(.43f, y, .101f), "a98250");
        Box(s, new(.69f, .025f, .08f), new(.43f, .533f, .147f), "84613d");
        Box(s, new(.45f, .04f, .12f), new(-.2f, .95f, 0), "8a6a48");
        // A low pallet keeps the shipping crates off wet ground.
        foreach (float x in new[] { -.85f, -.41f }) Box(s, new(.065f, .045f, .55f), new(x, .0225f, .02f), "795838");
        for (int i = 0; i < 3; i++) Box(s, new(.65f, .025f, .164f), new(-.63f, .0575f, -.16f + i * .18f), i == 1 ? "a9804e" : "967044");
        return s.Commit();
    }

    private static ArrayMesh BuildMailbox()
    {
        var s = Begin();
        // One swept shell: square lower sides roll into an arched roof.
        Vector2[] outline = { new(-.125f, 0), new(.125f, 0), new(.125f, .13f), new(.108f, .1925f), new(.0625f, .2383f), new(0, .255f), new(-.0625f, .2383f), new(-.108f, .1925f), new(-.125f, .13f) };
        Vector3 P(int i, float z) => new(-.36f + outline[i].X, .97f + outline[i].Y, z);
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            Quad(s, P(i, -.2f), P(next, -.2f), P(next, .2f), P(i, .2f), new(outline[i].X + outline[next].X, i == 0 ? -1 : .3f, 0), "454b4d");
        }
        foreach (float z in new[] { -.2f, .204f })
            for (int i = 1; i + 1 < outline.Length; i++) Triangle(s, P(0, z), P(i, z), P(i + 1, z), new(0, 0, z), z > 0 ? "626b6b" : "3a4042");
        // Recessed mail slot and a small door pull on its fitted front panel.
        Box(s, new(.151f, .012f, .004f), new(-.36f, 1.128f, .208f), "202728");
        Box(s, new(.057f, .019f, .018f), new(-.36f, 1.054f, .217f), "a9afaa");
        Box(s, new(.12f, .012f, .005f), new(-.36f, 1.006f, .209f), "909a96");
        return s.Commit();
    }

    private static ArrayMesh BuildFlag()
    {
        var s = Begin();
        Box(s, new(.016f, .195f, .025f), new(0, .0975f, 0), "b72f2b");
        Box(s, new(.016f, .066f, .093f), new(0, .174f, .042f), "cd3b32");
        return s.Commit();
    }
    private static void Bevel(SurfaceTool s, Vector3 size, Vector3 center, float bevel, string tint, Vector3 rotation = default)
    {
        // Four-corner rings chamfer the horizontal edges without subdividing every vertical corner.
        var basis = Basis.FromEuler(rotation * (Mathf.Pi / 180));
        float x = size.X / 2, z = size.Z / 2, y = size.Y / 2;
        Vector3[] Ring(float width, float depth, float height) => new[] {
            center + basis * new Vector3(-width, height, -depth), center + basis * new Vector3(width, height, -depth),
            center + basis * new Vector3(width, height, depth), center + basis * new Vector3(-width, height, depth) };
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

    private static void Box(SurfaceTool s, Vector3 size, Vector3 center, string tint, Vector3 rotation = default)
    {
        var basis = Basis.FromEuler(rotation * (Mathf.Pi / 180));
        foreach (var axis in new[] { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward })
        {
            var u = axis.X != 0 ? Vector3.Up : Vector3.Right; var v = axis.Cross(u);
            Vector3 Corner(float x, float y) => center + basis * ((axis + u * x + v * y) * size / 2);
            Quad(s, Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1), basis * axis, tint);
        }
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
