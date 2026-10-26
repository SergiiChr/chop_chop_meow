using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Cached outdoor cook station: open masonry hearth, fitted counter and hollow cookware.</summary>
internal static class CookShape {
    private static ArrayMesh? masonry, timber, metal, mortar, fire;
    public static Mesh Masonry => masonry ??= BuildMasonry();
    public static Mesh Timber => timber ??= BuildTimber();
    public static Mesh Metal => metal ??= BuildMetal();
    public static Mesh Mortar => mortar ??= BuildMortar();
    public static Mesh Fire => fire ??= BuildFire();
    public static readonly StandardMaterial3D Stone = Material(.95f);
    public static readonly StandardMaterial3D Wood = Material(.85f);
    public static readonly StandardMaterial3D Steel = Material(.5f, .35f);
    public static readonly StandardMaterial3D Ceramic = Material(.9f);
    public static readonly StandardMaterial3D Flame = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    private static StandardMaterial3D Material(float roughness, float metallic = 0) => new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = roughness, Metallic = metallic };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildMasonry() {
        var s = Begin();
        Bevel(s, new(.62f, .07f, .62f), new(-.33f, .035f, 0), .009f, "7b7c78");
        // The front opens into a soot-lined cavity. Staggered side blocks carry the grill.
        for (int row = 0; row < 3; row++) {
            float y = .15f + row * .18f;
            foreach (float x in new[] { -.575f, -.085f })
                for (int block = 0; block < 2; block++)
                    Bevel(s, new(.13f, .174f, .302f), new(x, y, (block == 0 ? -.155f : .155f) + (row == 1 ? .004f : 0)), .008f, (row + block) % 2 == 0 ? "9b9c98" : "92958f");
            Bevel(s, new(.35f, .174f, .12f), new(-.33f, y, -.25f), .008f, "8b8e88");
        }
        Bevel(s, new(.36f, .14f, .13f), new(-.33f, .53f, .245f), .009f, "a3a49d");
        Box(s, new(.35f, .02f, .43f), new(-.33f, .081f, .03f), "302b26");
        Box(s, new(.35f, .37f, .008f), new(-.33f, .26f, -.184f), "494139");
        return s.Commit();
    }

    private static ArrayMesh BuildTimber() {
        var s = Begin();
        foreach (float x in new[] { .065f, .595f })
            foreach (float z in new[] { -.235f, .235f })
                Bevel(s, new(.075f, .595f, .075f), new(x, .2975f, z), .006f, "8a6a48");
        foreach (float z in new[] { -.24f, .24f })
            Box(s, new(.59f, .1f, .045f), new(.33f, .535f, z), "99744d");
        foreach (float x in new[] { .065f, .595f }) {
            Box(s, new(.05f, .1f, .46f), new(x, .535f, 0), "99744d");
            Box(s, new(.05f, .055f, .46f), new(x, .18f, 0), "8a6a48");
        }
        for (int i = -1; i <= 1; i++)
            Bevel(s, new(.64f, .055f, .196f), new(.33f, .6175f, i * .201f), .007f, i == 0 ? "d2a26a" : "c69862");
        return s.Commit();
    }

    private static ArrayMesh BuildMetal() {
        var s = Begin();
        foreach (float z in new[] { -.3f, .3f }) Box(s, new(.66f, .035f, .035f), new(-.33f, .623f, z), "a2adae");
        foreach (float x in new[] { -.64f, -.02f }) Box(s, new(.035f, .035f, .6f), new(x, .623f, 0), "a2adae");
        for (int i = 0; i < 7; i++) Box(s, new(.018f, .02f, .58f), new(-.57f + i * .08f, .635f, 0), "aeb9ba");
        // Rolled rim folds into a real inner wall; the stew sits below the lip.
        Lathe(s, new(-.36f, 0, .025f), new[] { new Vector2(0, .647f), new(.12f, .647f), new(.146f, .67f), new(.16f, .775f), new(.162f, .8f), new(.148f, .808f), new(.138f, .79f), new(.13f, .69f), new(0, .69f) }, "45494c", 16);
        Lathe(s, new(-.36f, 0, .025f), new[] { new Vector2(.135f, .768f), new(0, .768f) }, "875832", 16);
        // A connected handle grows from a socket on the pot's side.
        Bevel(s, new(.06f, .045f, .07f), new(-.205f, .758f, .025f), .006f, "45494c");
        Bevel(s, new(.175f, .035f, .055f), new(-.105f, .758f, .025f), .009f, "303538");
        Lathe(s, new(-.56f, 0, -.24f), new[] { new Vector2(.045f, .59f), new(.045f, 1.35f), new(.052f, 1.35f), new(.052f, 1.375f), new(.036f, 1.375f), new(.036f, 1.3f) }, "45494c", 10);
        return s.Commit();
    }

    private static ArrayMesh BuildMortar() {
        var s = Begin();
        Lathe(s, Vector3.Zero, new[] { new Vector2(0, 0), new(.062f, 0), new(.078f, .013f), new(.1f, .08f), new(.1f, .105f), new(.085f, .111f), new(.077f, .092f), new(.045f, .035f), new(0, .03f) }, "9aa29f", 12);
        // Rounded pestle leans into the bowl, with its foot below the rim.
        Lathe(s, new(.025f, .025f, 0), new[] { new Vector2(0, 0), new(.027f, .008f), new(.029f, .03f), new(.022f, .06f), new(.019f, .19f), new(.015f, .2f), new(0, .203f) }, "c4c8c3", 8, -.38f);
        return s.Commit();
    }

    private static ArrayMesh BuildFire() {
        var s = Begin();
        foreach (var (x, h) in new[] { (-.43f, .18f), (-.33f, .24f), (-.23f, .15f) }) {
            var baseAt = new Vector3(x, .09f, .17f);
            for (int i = 0; i < 5; i++) {
                float a = i * Mathf.Tau / 5, b = (i + 1) * Mathf.Tau / 5;
                Triangle(s, baseAt + new Vector3(MathF.Cos(a) * .055f, 0, MathF.Sin(a) * .065f), baseAt + new Vector3(MathF.Cos(b) * .055f, 0, MathF.Sin(b) * .065f), baseAt + new Vector3(.015f, h, 0), new Vector3(MathF.Cos(a), .2f, MathF.Sin(a)), i % 2 == 0 ? "ffb447" : "ef7024");
            }
        }
        return s.Commit();
    }

    private static void Lathe(SurfaceTool s, Vector3 center, Vector2[] profile, string tint, int sides, float lean = 0) {
        Vector3 Point(Vector2 p, float angle) => center + new Vector3(MathF.Cos(angle) * p.X - MathF.Sin(lean) * p.Y, MathF.Cos(lean) * p.Y, MathF.Sin(angle) * p.X);
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
        float x = size.X / 2, z = size.Z / 2, y = size.Y / 2;
        Vector3[] Ring(float width, float depth, float height, float corner) => new[] {
            center + new Vector3(-width + corner, height, -depth), center + new Vector3(width - corner, height, -depth),
            center + new Vector3(width, height, -depth + corner), center + new Vector3(width, height, depth - corner),
            center + new Vector3(width - corner, height, depth), center + new Vector3(-width + corner, height, depth),
            center + new Vector3(-width, height, depth - corner), center + new Vector3(-width, height, -depth + corner) };
        Vector3[][] rings = { Ring(x - bevel, z - bevel, -y, bevel * .4f), Ring(x, z, -y + bevel, bevel),
            Ring(x, z, y - bevel, bevel), Ring(x - bevel, z - bevel, y, bevel * .4f) };
        for (int row = 0; row < rings.Length - 1; row++)
            for (int i = 0; i < 8; i++) {
                int next = (i + 1) % 8;
                var outward = (rings[row][i] + rings[row][next]) / 2 - center; outward.Y = 0;
                Triangle(s, rings[row][i], rings[row][next], rings[row + 1][next], outward, tint);
                Triangle(s, rings[row][i], rings[row + 1][next], rings[row + 1][i], outward, tint);
            }
        for (int i = 0; i < 8; i++) {
            int next = (i + 1) % 8;
            Triangle(s, center - Vector3.Up * y, rings[0][i], rings[0][next], Vector3.Down, tint);
            Triangle(s, center + Vector3.Up * y, rings[3][i], rings[3][next], Vector3.Up, tint);
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
