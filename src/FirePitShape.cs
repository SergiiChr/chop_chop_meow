using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A hand-packed ring of fieldstones with an ash bed and fitted split firewood.</summary>
internal static class FirePitShape {
    private static ArrayMesh? stones, fuel, flames;
    public static Mesh Stones => stones ??= BuildStones();
    public static Mesh Fuel => fuel ??= BuildFuel();
    public static Mesh Flames => flames ??= BuildFlames();
    public static readonly StandardMaterial3D Stone = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1 };
    public static readonly StandardMaterial3D Wood = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1 };
    public static readonly StandardMaterial3D Flame = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }
    private static Vector3 Ring(float radius, float y, float angle) => new(MathF.Cos(angle) * radius, y, MathF.Sin(angle) * radius);

    private static ArrayMesh BuildStones() {
        var s = Begin();
        // Eleven individually faceted fieldstones meet around the hearth, all in one shared mesh.
        string[] shades = { "85877a", "999587", "777e7b", "a39e8b", "878980" };
        for (int rock = 0; rock < 11; rock++) {
            float angle = rock * MathF.Tau / 11;
            var center = Ring(.414f + MathF.Sin(rock * 7.1f) * .009f, 0, angle);
            var radial = Ring(1, 0, angle); var tangent = Ring(1, 0, angle + MathF.PI / 2);
            float height = .205f + .025f * MathF.Sin(rock * 4.3f);
            var points = new Vector3[3, 7];
            for (int layer = 0; layer < 3; layer++)
                for (int i = 0; i < 7; i++) {
                    float a = i * MathF.Tau / 7 + rock * .17f;
                    float bulge = layer == 0 ? .84f : layer == 1 ? 1 : .66f;
                    float rough = 1 + .1f * MathF.Sin(rock * 13 + i * 7);
                    float y = layer == 0 ? .014f : layer == 1 ? height * .48f : height + .012f * MathF.Sin(i * 3 + rock);
                    points[layer, i] = center + radial * (MathF.Cos(a) * .108f * bulge * rough) + tangent * (MathF.Sin(a) * .133f * bulge * rough) + Vector3.Up * y;
                }
            string tint = shades[rock % shades.Length];
            for (int layer = 0; layer < 2; layer++)
                for (int i = 0; i < 7; i++) {
                    int next = (i + 1) % 7;
                    var outward = (points[layer, i] + points[layer, next]) / 2 - center;
                    outward.Y = layer == 0 ? -.035f : .08f;
                    Quad(s, points[layer, i], points[layer, next], points[layer + 1, next], points[layer + 1, i], outward, tint);
                }
            for (int i = 1; i < 6; i++)
                Triangle(s, points[2, 0], points[2, i], points[2, i + 1], Vector3.Up, tint);
        }
        return s.Commit();
    }

    private static ArrayMesh BuildFuel() {
        var s = Begin();
        // Ash settles inside the fieldstones; the logs rest in the hearth.
        for (int i = 0; i < 16; i++) Triangle(s, Vector3.Up * .07f, Ring(.335f, .07f, i * MathF.Tau / 16), Ring(.335f, .07f, (i + 1) * MathF.Tau / 16), Vector3.Up, i % 3 == 0 ? "55514a" : "393732");
        for (int log = 0; log < 4; log++) {
            float angle = log * MathF.Tau / 4 + .35f;
            var bottom = Ring(.28f, .135f, angle); var top = Ring(.075f, .46f + log % 2 * .035f, angle);
            var axis = (top - bottom).Normalized(); var u = axis.Cross(Vector3.Up).Normalized(); var v = axis.Cross(u);
            Vector2[] section = { new(-.065f, -.035f), new(.065f, -.035f), new(.08f, .025f), new(0, .07f), new(-.08f, .025f) };
            Vector3 Point(int i, Vector3 center) => center + u * section[i].X + v * section[i].Y;
            for (int i = 0; i < 5; i++) {
                int next = (i + 1) % 5;
                Quad(s, Point(i, bottom), Point(next, bottom), Point(next, top), Point(i, top), u * (section[i].X + section[next].X) + v * (section[i].Y + section[next].Y), i == 0 ? "b28b56" : log % 2 == 0 ? "66462f" : "795136");
            }
            for (int i = 1; i < 4; i++) {
                Triangle(s, Point(0, bottom), Point(i, bottom), Point(i + 1, bottom), -axis, "c19b63");
                Triangle(s, Point(0, top), Point(i, top), Point(i + 1, top), axis, "302b26");
            }
        }
        return s.Commit();
    }

    private static ArrayMesh BuildFlames() {
        var s = Begin();
        for (int tongue = 0; tongue < 5; tongue++) {
            float angle = tongue * MathF.Tau / 5;
            var center = Ring(tongue == 0 ? 0 : .105f, 0, angle);
            float radius = tongue == 0 ? .14f : .09f, height = tongue == 0 ? .49f : .25f + tongue * .025f;
            for (int i = 0; i < 5; i++) {
                float a = i * MathF.Tau / 5, b = (i + 1) * MathF.Tau / 5;
                var baseA = center + Ring(radius, 0, a); var baseB = center + Ring(radius, 0, b);
                var shoulderA = center + Ring(radius * .65f, height * .45f, a); var shoulderB = center + Ring(radius * .65f, height * .45f, b);
                var outward = Ring(1, .2f, (a + b) / 2);
                Quad(s, baseA, baseB, shoulderB, shoulderA, outward, tongue % 2 == 0 ? "ff9a32" : "ffc456");
                Triangle(s, shoulderA, shoulderB, center + new Vector3(.035f, height, -.02f), outward, tongue % 2 == 0 ? "f47723" : "ffdb7a");
            }
        }
        return s.Commit();
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
