using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Ground-resting webbing and a flexible access flap for the shared actor backpack.</summary>
internal static class BackpackShape {
    private static ArrayMesh? details, flap;
    public static Mesh Details => details ??= BuildDetails();
    public static Mesh Flap => flap ??= BuildFlap();
    public static readonly StandardMaterial3D Canvas = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .95f };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildDetails() {
        var s = Begin();
        // Shoulder straps have fallen loosely to either side; their ends disappear beneath the body.
        foreach (float side in new[] { -1f, 1f })
            Strap(s, new[] { new Vector3(side * .1f, .075f, -.19f), new(side * .24f, .025f, -.14f), new(side * .28f, .018f, .08f), new(side * .14f, .06f, .19f) }, .026f);
        Strap(s, new[] { new Vector3(-.055f, .105f, .235f), new(-.06f, .07f, .31f), new(.06f, .07f, .31f), new(.055f, .105f, .235f) }, .022f);
        // A dark, fitted opening remains under the flap when the inventory is open.
        for (int i = 0; i < 4; i++) {
            Vector3 Point(int column, float end) {
                float x = (column - 2) * .06f;
                float z = end * .065f * MathF.Sqrt(1 - x * x / (.135f * .135f));
                float width = 1.5f * (.14f + .005f * (z / 1.5f + .1f) / .21f);
                return new(x, .151f + .135f * MathF.Sqrt(1 - x * x / (width * width)), z);
            }
            Quad(s, Point(i, -1), Point(i + 1, -1), Point(i + 1, 1), Point(i, 1), Vector3.Up, "323b2b");
        }
        return s.Commit();
    }

    private static ArrayMesh BuildFlap() {
        var s = Begin();
        // The canvas follows the rounded body instead of forming a rigid box lid.
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 4; col++) {
                Vector3 Point(int x, int z, float drop = 0) {
                    float across = (x - 2) / 2f;
                    float width = z == 3 ? .11f : .145f;
                    return new(across * width, .022f + .035f * (1 - across * across) - drop, z * .095f);
                }
                Quad(s, Point(col, row), Point(col + 1, row), Point(col + 1, row + 1), Point(col, row + 1), Vector3.Up, row == 2 ? "788253" : "7d8858");
                Quad(s, Point(col, row, .008f), Point(col, row + 1, .008f), Point(col + 1, row + 1, .008f), Point(col + 1, row, .008f), Vector3.Down, "525f39");
                if (row == 2) Quad(s, Point(col, 3), Point(col + 1, 3), Point(col + 1, 3, .008f), Point(col, 3, .008f), Vector3.Back, "465332");
                if (col == 0 || col == 3) {
                    int x = col == 0 ? 0 : 4;
                    Quad(s, Point(x, row), Point(x, row + 1), Point(x, row + 1, .008f), Point(x, row, .008f), col == 0 ? Vector3.Left : Vector3.Right, "56653b");
                }
            }
        // Sewn webbing tab with a small contrasting buckle, folded over the flap's nose.
        Strap(s, new[] { new Vector3(0, .061f, .21f), new(0, .061f, .27f), new(0, .035f, .293f) }, .028f);
        Quad(s, new(-.023f, .065f, .245f), new(.023f, .065f, .245f), new(.023f, .065f, .27f), new(-.023f, .065f, .27f), Vector3.Up, "a8a48b");
        return s.Commit();
    }

    private static void Strap(SurfaceTool s, Vector3[] path, float width) {
        for (int i = 0; i < path.Length - 1; i++) {
            var a = path[i]; var b = path[i + 1];
            var across = (b - a).Cross(Vector3.Up).Normalized() * width / 2;
            var down = Vector3.Down * .008f;
            Quad(s, a - across, a + across, b + across, b - across, Vector3.Up, "485335");
            Quad(s, a - across + down, b - across + down, b + across + down, a + across + down, Vector3.Down, "3b452d");
            foreach (float side in new[] { -1f, 1f }) Quad(s, a + across * side, b + across * side, b + across * side + down, a + across * side + down, across * side, "3b452d");
        }
    }

    private static void Quad(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, string tint) {
        Triangle(s, a, b, c, outward, tint); Triangle(s, a, c, d, outward, tint);
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, string tint) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        s.SetColor(new Color(tint)); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
