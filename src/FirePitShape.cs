using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A salvaged wheel rim with open ventilation slots and fitted split firewood.</summary>
internal static class FirePitShape {
    private static ArrayMesh? rim, fuel, flames;
    public static Mesh Rim => rim ??= BuildRim();
    public static Mesh Fuel => fuel ??= BuildFuel();
    public static Mesh Flames => flames ??= BuildFlames();
    public static readonly StandardMaterial3D Steel = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .65f, Metallic = .55f };
    public static readonly StandardMaterial3D Wood = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1 };
    public static readonly StandardMaterial3D Flame = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }
    private static Vector3 Ring(float radius, float y, float angle) => new(MathF.Cos(angle) * radius, y, MathF.Sin(angle) * radius);

    private static ArrayMesh BuildRim() {
        var s = Begin();
        // Closed cross-section: lower bead, recessed barrel, flared top bead and soot-dark inner bowl.
        Vector2[] profile = { new(.46f, .025f), new(.48f, .045f), new(.48f, .066f), new(.435f, .09f), new(.435f, .12f), new(.435f, .175f), new(.435f, .2f), new(.48f, .235f), new(.48f, .26f), new(.451f, .26f), new(.407f, .207f), new(.407f, .175f), new(.407f, .12f), new(.407f, .085f), new(.435f, .047f), new(.46f, .025f) };
        for (int i = 0; i < 16; i++) {
            float a = i * MathF.Tau / 16, b = (i + 1) * MathF.Tau / 16;
            for (int j = 0; j < profile.Length - 1; j++) {
                // Eight actual air slots cross the barrel, bounded by the same inner and outer surfaces.
                if ((j == 4 || j == 11) && i % 2 == 0) continue;
                var p = profile[j]; var q = profile[j + 1];
                var outward = new Vector3(MathF.Cos((a + b) / 2) * (q.Y - p.Y), p.X - q.X, MathF.Sin((a + b) / 2) * (q.Y - p.Y));
                Quad(s, Ring(p.X, p.Y, a), Ring(p.X, p.Y, b), Ring(q.X, q.Y, b), Ring(q.X, q.Y, a), outward, j >= 9 ? "303336" : j == 7 || j == 8 ? "777d80" : i % 5 == 0 ? "63534a" : "4c5358");
            }
            if (i % 2 != 0) continue;
            foreach (float angle in new[] { a, b })
                Quad(s, Ring(.435f, .12f, angle), Ring(.435f, .175f, angle), Ring(.407f, .175f, angle), Ring(.407f, .12f, angle), new Vector3(-MathF.Sin(angle), 0, MathF.Cos(angle)) * (angle == a ? 1 : -1), "45494b");
            Quad(s, Ring(.435f, .12f, a), Ring(.407f, .12f, a), Ring(.407f, .12f, b), Ring(.435f, .12f, b), Vector3.Up, "64696b");
            Quad(s, Ring(.435f, .175f, a), Ring(.435f, .175f, b), Ring(.407f, .175f, b), Ring(.407f, .175f, a), Vector3.Down, "33383b");
        }
        return s.Commit();
    }

    private static ArrayMesh BuildFuel() {
        var s = Begin();
        // Ash settles below the ventilation slots; logs rest inside the rim rather than on a lid.
        for (int i = 0; i < 16; i++) Triangle(s, Vector3.Up * .07f, Ring(.405f, .07f, i * MathF.Tau / 16), Ring(.405f, .07f, (i + 1) * MathF.Tau / 16), Vector3.Up, i % 3 == 0 ? "55514a" : "393732");
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
