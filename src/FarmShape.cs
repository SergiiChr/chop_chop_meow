using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Cached tilled rows and three seeded field variants with tapered leaves and grain heads.</summary>
internal static class FarmShape
{
    private static ArrayMesh? soil;
    private static readonly ArrayMesh?[,] crops = new ArrayMesh?[3, 3];
    public static Mesh Soil => soil ??= BuildSoil();
    public static Mesh Crop(int stage, int variant) => crops[stage, variant] ??= BuildField(stage, variant);
    public static readonly StandardMaterial3D Earth = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1 };
    // Thin plant leaves are deliberately two-sided; solid stems and grain heads share the same cached material.
    public static readonly StandardMaterial3D Foliage = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildSoil()
    {
        var s = Begin();
        Quad(s, new(-1.35f, .03f, -1.35f), new(1.35f, .03f, -1.35f), new(1.35f, .03f, 1.35f), new(-1.35f, .03f, 1.35f), Vector3.Up, "4f3a28");
        foreach (float side in new[] { -1.35f, 1.35f })
        {
            Quad(s, new(side, .01f, -1.35f), new(side, .03f, -1.35f), new(side, .03f, 1.35f), new(side, .01f, 1.35f), new(side, 0, 0), "4f3a28");
            Quad(s, new(-1.35f, .01f, side), new(-1.35f, .03f, side), new(1.35f, .03f, side), new(1.35f, .01f, side), new(0, 0, side), "4f3a28");
        }
        for (int row = 0; row < 5; row++)
        {
            float z = -1.08f + row * .54f;
            Vector3[] profile = { new(0, .03f, z - .11f), new(0, .08f, z - .065f), new(0, .08f, z + .065f), new(0, .03f, z + .11f) };
            Vector3 Left(int i) => profile[i] + Vector3.Left * 1.3f;
            Vector3 Right(int i) => profile[i] + Vector3.Right * 1.3f;
            for (int i = 0; i < 3; i++)
                Quad(s, Left(i), Right(i), Right(i + 1), Left(i + 1), Vector3.Up, i == 1 ? "654b32" : "5e4630");
            Quad(s, Left(0), Left(1), Left(2), Left(3), Vector3.Left, "5e4630");
            Quad(s, Right(0), Right(1), Right(2), Right(3), Vector3.Right, "5e4630");
        }
        return s.Commit();
    }

    private static ArrayMesh BuildField(int stage, int variant)
    {
        var s = Begin();
        for (int row = 0; row < 5; row++)
            for (int i = 0; i < 7; i++)
            {
                int n = row * 7 + i;
                var at = new Vector3(-1.14f + i * .38f + (WorldModels.Jitter(variant, n) - .5f) * .1f, .078f, -1.08f + row * .54f);
                float turn = n * 2.4f + variant;
                Vector3 Direction(float angle) => new(MathF.Cos(angle), 0, MathF.Sin(angle));
                if (stage < 2)
                {
                    int count = stage == 0 ? 2 : 3;
                    for (int k = 0; k < count; k++)
                    {
                        var direction = Direction(turn + k * 2.3f);
                        float height = stage == 0 ? .15f : .43f + WorldModels.Jitter(variant, n + 70) * .09f;
                        Leaf(s, at, direction, height, stage == 0 ? .04f : .15f, stage == 0 ? .018f : .035f,
                            stage == 0 ? "7fae45" : k == 0 ? "669b38" : "4f8a34");
                    }
                }
                else
                {
                    for (int k = 0; k < 2; k++)
                    {
                        float height = .59f + WorldModels.Jitter(variant, n + k * 50) * .1f - k * .045f;
                        var baseAt = at + Direction(turn + k * 3) * .045f;
                        var tip = baseAt + Vector3.Up * height + Direction(turn) * .04f;
                        Stem(s, baseAt, tip);
                        Ear(s, tip, k == 0 ? "e2c25b" : "ceb057");
                        if (k == 0) Leaf(s, baseAt.Lerp(tip, .4f), Direction(turn + 1), .23f, .13f, .022f, "b8a15b");
                    }
                }
            }
        return s.Commit();
    }

    private static void Leaf(SurfaceTool s, Vector3 at, Vector3 direction, float height, float reach, float width, string tint)
    {
        var middle = at + Vector3.Up * (height * .65f) + direction * (reach * .3f);
        var tip = at + Vector3.Up * height + direction * reach;
        var across = direction.Cross(Vector3.Up) * width;
        Quad(s, at, middle + across, tip, middle - across, Vector3.Up, tint);
    }

    private static void Stem(SurfaceTool s, Vector3 bottom, Vector3 top)
    {
        const float radius = .009f;
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Tau / 3, b = (i + 1) * Mathf.Tau / 3;
            var first = new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * radius;
            var next = new Vector3(MathF.Cos(b), 0, MathF.Sin(b)) * radius;
            Quad(s, bottom + first, bottom + next, top + next * .6f, top + first * .6f, first + next, "d6b354");
        }
    }

    private static void Ear(SurfaceTool s, Vector3 bottom, string tint)
    {
        var middle = bottom + Vector3.Up * .075f; var tip = bottom + Vector3.Up * .18f;
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Tau / 4, b = (i + 1) * Mathf.Tau / 4;
            var first = middle + new Vector3(MathF.Cos(a) * .035f, 0, MathF.Sin(a) * .035f);
            var next = middle + new Vector3(MathF.Cos(b) * .035f, 0, MathF.Sin(b) * .035f);
            Triangle(s, bottom, next, first, (first + next) / 2 - middle - Vector3.Up * .02f, tint);
            Triangle(s, first, next, tip, (first + next) / 2 - middle + Vector3.Up * .02f, tint);
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
