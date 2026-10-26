using Godot;

namespace ChopChopMeow;

/// <summary>Shared hollow slatted shipping crate; the lid uses the existing rear hinge frame.</summary>
internal static class CrateShape {
    private static ArrayMesh? body, lid;
    public static Mesh Body => body ??= BuildBody();
    public static Mesh Lid => lid ??= BuildLid();
    public static readonly StandardMaterial3D Timber = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .9f };

    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }
    private static ArrayMesh BuildBody() {
        var s = Begin();
        // Separate floor planks and two low runners keep the open box hollow all the way down.
        for (int i = -1; i <= 1; i++) Box(s, new(.54f, .028f, .132f), new(0, .045f, i * .136f), i == 0 ? "a57c49" : "b48b54");
        foreach (float x in new[] { -.2f, .2f }) Box(s, new(.055f, .032f, .43f), new(x, .016f, 0), "84603c");
        string[] shades = { "c29a62", "c9a46e", "bf965d" };
        for (int i = 0; i < 3; i++)
            foreach (float z in new[] { -.22f, .22f })
                Board(s, .57f, .109f, .027f, new(0, .091f + i * .132f, z), shades[i]);
        for (int i = 0; i < 2; i++)
            foreach (float x in new[] { -.287f, .287f })
                Board(s, .417f, .109f, .027f, new(x, .091f + i * .132f, 0), shades[i], turn: true);
        // A real opening in each upper end slat forms the grip, with a thick lip above it.
        foreach (float x in new[] { -.287f, .287f }) {
            Box(s, new(.027f, .032f, .417f), new(x, .318f, 0), "c29a62");
            Box(s, new(.027f, .027f, .417f), new(x, .4085f, 0), "d0ad79");
            foreach (float z in new[] { -.151f, .151f })
                Box(s, new(.027f, .061f, .115f), new(x, .3645f, z), "c9a46e");
        }
        foreach (float x in new[] { -.274f, .274f })
            foreach (float z in new[] { -.207f, .207f })
                Box(s, new(.047f, .389f, .047f), new(x, .2245f, z), "8f6a3f");
        // Flat nail heads need only one visible face and remain part of the same cached mesh.
        foreach (float z in new[] { -.234f, .234f })
            foreach (float x in new[] { -.274f, .274f })
                foreach (float y in new[] { .09f, .354f })
                    Quad(s, new(x - .006f, y - .006f, z), new(x + .006f, y - .006f, z), new(x + .006f, y + .006f, z), new(x - .006f, y + .006f, z), new(0, 0, z), "5f5c53");
        return s.Commit();
    }

    private static ArrayMesh BuildLid() {
        var s = Begin();
        for (int i = 0; i < 3; i++)
            Board(s, .63f, .032f, .143f, new(0, .018f, .0765f + i * .153f), i == 1 ? "c9a46e" : "c19a63", caps: true);
        foreach (float x in new[] { -.25f, .25f })
            Box(s, new(.046f, .025f, .475f), new(x, .046f, .23f), "8f6a3f");
        return s.Commit();
    }

    /// <summary>A plank with a chamfered cross-section; hidden end caps are omitted behind the corner battens.</summary>
    private static void Board(SurfaceTool s, float length, float height, float thickness, Vector3 center, string tint, bool turn = false, bool caps = false) {
        float y = height / 2, z = thickness / 2, bevel = .004f;
        Vector2[] profile = { new(-y + bevel, -z), new(y - bevel, -z), new(y, -z + bevel), new(y, z - bevel), new(y - bevel, z), new(-y + bevel, z), new(-y, z - bevel), new(-y, -z + bevel) };
        Vector3 Point(float x, Vector2 p) => center + (turn ? new Vector3(p.Y, p.X, -x) : new Vector3(x, p.X, p.Y));
        var axis = turn ? Vector3.Forward : Vector3.Right;
        for (int i = 0; i < profile.Length; i++) {
            int next = (i + 1) % profile.Length;
            var a = Point(-length / 2, profile[i]); var b = Point(-length / 2, profile[next]);
            var c = Point(length / 2, profile[next]); var d = Point(length / 2, profile[i]);
            Quad(s, a, b, c, d, (a + b + c + d) / 4 - center, tint);
        }
        if (caps)
            for (int i = 1; i + 1 < profile.Length; i++) {
                Triangle(s, Point(-length / 2, profile[0]), Point(-length / 2, profile[i]), Point(-length / 2, profile[i + 1]), -axis, tint);
                Triangle(s, Point(length / 2, profile[0]), Point(length / 2, profile[i]), Point(length / 2, profile[i + 1]), axis, tint);
            }
    }
    private static void Box(SurfaceTool s, Vector3 size, Vector3 center, string tint) {
        var half = size / 2;
        var a = center + new Vector3(-half.X, -half.Y, -half.Z); var b = center + new Vector3(half.X, -half.Y, -half.Z);
        var c = center + new Vector3(half.X, -half.Y, half.Z); var d = center + new Vector3(-half.X, -half.Y, half.Z);
        var up = Vector3.Up * size.Y;
        Quad(s, a, b, c, d, Vector3.Down, tint); Quad(s, a + up, d + up, c + up, b + up, Vector3.Up, tint);
        Quad(s, a, a + up, b + up, b, Vector3.Forward, tint); Quad(s, d, c, c + up, d + up, Vector3.Back, tint);
        Quad(s, a, d, d + up, a + up, Vector3.Left, tint); Quad(s, b, b + up, c + up, c, Vector3.Right, tint);
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
