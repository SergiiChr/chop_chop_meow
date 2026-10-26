using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared fitted canning stove, stockpot and hand-operated can seamer beside a timber counter.</summary>
internal static class CanneryShape {
    private static ArrayMesh? stove, timber, pot, press, fire;
    public static Mesh Stove => stove ??= BuildStove();
    public static Mesh Timber => timber ??= BuildTimber();
    public static Mesh Pot => pot ??= BuildPot();
    public static Mesh Press => press ??= BuildPress();
    public static Mesh Fire => fire ??= BuildFire();
    public static readonly StandardMaterial3D Iron = Material(.6f, .55f);
    public static readonly StandardMaterial3D Wood = Material(.85f);
    public static readonly StandardMaterial3D Steel = Material(.4f, .4f);
    public static readonly StandardMaterial3D Flame = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    private static StandardMaterial3D Material(float roughness, float metallic = 0) => new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = roughness, Metallic = metallic };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildStove() {
        var s = Begin();
        // Folded steel sides and a real firebox opening, with a soot-dark inner back.
        foreach (float x in new[] { -.485f, -.055f })
            Box(s, new(.03f, .44f, .48f), new(x, .25f, 0), "45494c");
        Box(s, new(.44f, .44f, .03f), new(-.27f, .25f, -.225f), "3d4245");
        Bevel(s, new(.48f, .045f, .52f), new(-.27f, .4775f, 0), .008f, "52585b");
        Bevel(s, new(.46f, .045f, .5f), new(-.27f, .0325f, 0), .007f, "393e41");
        foreach (float x in new[] { -.4525f, -.0875f })
            Box(s, new(.065f, .22f, .03f), new(x, .165f, .225f), "45494c");
        Box(s, new(.4f, .195f, .03f), new(-.27f, .3725f, .225f), "45494c");
        Box(s, new(.34f, .018f, .4f), new(-.27f, .064f, .015f), "292724");
        Box(s, new(.39f, .37f, .005f), new(-.27f, .25f, -.207f), "302d29");
        // Hollow flue outlet meets the existing smoke vent.
        Lathe(s, new(-.44f, 0, -.18f), new[] { new Vector2(.036f, .49f), new(.036f, 1.085f), new(.027f, 1.085f), new(.027f, 1.02f) }, "45494c", 8);
        return s.Commit();
    }

    private static ArrayMesh BuildTimber() {
        var s = Begin();
        foreach (float x in new[] { .025f, .495f })
            foreach (float z in new[] { -.215f, .215f })
                Box(s, new(.07f, .55f, .07f), new(x, .275f, z), "8a6a48");
        foreach (float z in new[] { -.22f, .22f })
            Box(s, new(.52f, .1f, .045f), new(.26f, .5f, z), "99744d");
        foreach (float x in new[] { .025f, .495f }) {
            Box(s, new(.045f, .1f, .43f), new(x, .5f, 0), "99744d");
            Box(s, new(.045f, .055f, .43f), new(x, .17f, 0), "8a6a48");
        }
        for (int i = -1; i <= 1; i++)
            Bevel(s, new(.56f, .055f, .176f), new(.26f, .5775f, i * .181f), .007f, i == 0 ? "d2a26a" : "c69862");
        return s.Commit();
    }

    private static ArrayMesh BuildPot() {
        var s = Begin();
        Lathe(s, new(-.27f, 0, 0), new[] { new Vector2(.155f, .5f), new(.19f, .525f), new(.19f, .832f), new(.197f, .845f) }, "b9c3c6", 16);
        // A shallow domed lid nests on the rolled rim instead of overhanging as a flat disc.
        Lathe(s, new(-.27f, 0, 0), new[] { new Vector2(.2f, .847f), new(.2f, .86f), new(.065f, .893f), new(0, .893f) }, "777f83", 16);
        Lathe(s, new(-.27f, 0, 0), new[] { new Vector2(.026f, .89f), new(.032f, .9f), new(.032f, .921f), new(0, .934f) }, "343a3e", 6);
        // Side handles return to sockets on the shell, leaving an open grip.
        foreach (float x in new[] { -.475f, -.065f }) {
            foreach (float z in new[] { -.055f, .055f })
                Box(s, new(.065f, .028f, .026f), new(x, .772f, z), "61696d");
            Box(s, new(.025f, .028f, .135f), new(x + (x < -.27f ? -.03f : .03f), .772f, 0), "454b4f");
        }
        return s.Commit();
    }

    private static ArrayMesh BuildPress() {
        var s = Begin();
        Bevel(s, new(.18f, .025f, .19f), new(.39f, .6175f, -.14f), .006f, "7b888d");
        Box(s, new(.045f, .34f, .055f), new(.475f, .795f, -.18f), "808d92");
        Box(s, new(.18f, .04f, .07f), new(.4125f, .95f, -.18f), "939fa3");
        Lathe(s, new(.355f, 0, -.14f), new[] { new Vector2(.019f, .762f), new(.019f, .955f) }, "abb6b8", 6);
        Lathe(s, new(.355f, 0, -.14f), new[] { new Vector2(.055f, .757f), new(.055f, .78f), new(.019f, .788f) }, "7b888d", 8);
        // Lever seats on the head and ends in a fitted dark grip.
        Box(s, new(.26f, .03f, .035f), new(.35f, .984f, -.18f), "939fa3");
        Bevel(s, new(.08f, .039f, .045f), new(.2f, .984f, -.18f), .008f, "414a4d");
        // A single rolled upper rim preserves the small cans' silhouette; contact bottoms are omitted.
        foreach (var (x, z, bottom) in new[] { (.13f, .12f, .605f), (.255f, .14f, .605f), (.19f, .13f, .72f), (.355f, -.14f, .63f) })
            Lathe(s, new(x, 0, z), new[] { new Vector2(.048f, bottom), new(.048f, bottom + .1f), new(.052f, bottom + .107f), new(.043f, bottom + .115f), new(0, bottom + .109f) }, "c0cac8", 8);
        return s.Commit();
    }

    private static ArrayMesh BuildFire() {
        var s = Begin();
        foreach (var (x, height) in new[] { (-.35f, .13f), (-.26f, .18f), (-.18f, .115f) })
            for (int i = 0; i < 5; i++) {
                float a = i * Mathf.Tau / 5, b = (i + 1) * Mathf.Tau / 5;
                var baseAt = new Vector3(x, .075f, .13f);
                Triangle(s, baseAt + new Vector3(MathF.Cos(a) * .045f, 0, MathF.Sin(a) * .055f), baseAt + new Vector3(MathF.Cos(b) * .045f, 0, MathF.Sin(b) * .055f), baseAt + new Vector3(.01f, height, 0), new Vector3(MathF.Cos(a), .2f, MathF.Sin(a)), i % 2 == 0 ? "ffb447" : "ef7024");
            }
        return s.Commit();
    }

    private static void Lathe(SurfaceTool s, Vector3 center, Vector2[] profile, string tint, int sides) {
        Vector3 Point(Vector2 p, float angle) => center + new Vector3(MathF.Cos(angle) * p.X, p.Y, MathF.Sin(angle) * p.X);
        for (int row = 0; row < profile.Length - 1; row++)
            for (int i = 0; i < sides; i++) {
                float a = i * Mathf.Tau / sides, b = (i + 1) * Mathf.Tau / sides;
                var p = profile[row]; var q = profile[row + 1];
                var outward = new Vector3(MathF.Cos((a + b) / 2) * (q.Y - p.Y), p.X - q.X, MathF.Sin((a + b) / 2) * (q.Y - p.Y));
                if (p.X > 0) Triangle(s, Point(p, a), Point(p, b), Point(q, b), outward, tint);
                if (q.X > 0) Triangle(s, Point(p, a), Point(q, b), Point(q, a), outward, tint);
            }
    }

    private static void Bevel(SurfaceTool s, Vector3 size, Vector3 center, float bevel, string tint) {
        // Four-corner rings chamfer the horizontal edges without subdividing every vertical corner.
        float x = size.X / 2, z = size.Z / 2, y = size.Y / 2;
        Vector3[] Ring(float width, float depth, float height) => new[] {
            center + new Vector3(-width, height, -depth), center + new Vector3(width, height, -depth),
            center + new Vector3(width, height, depth), center + new Vector3(-width, height, depth) };
        Vector3[][] rings = { Ring(x - bevel, z - bevel, -y), Ring(x, z, -y + bevel),
            Ring(x, z, y - bevel), Ring(x - bevel, z - bevel, y) };
        for (int row = 0; row < rings.Length - 1; row++)
            for (int i = 0; i < 4; i++) {
                int next = (i + 1) % 4;
                var outward = (rings[row][i] + rings[row][next]) / 2 - center; outward.Y = 0;
                Quad(s, rings[row][i], rings[row][next], rings[row + 1][next], rings[row + 1][i], outward, tint);
            }
        Quad(s, rings[0][0], rings[0][1], rings[0][2], rings[0][3], Vector3.Down, tint);
        Quad(s, rings[3][0], rings[3][1], rings[3][2], rings[3][3], Vector3.Up, tint);
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
