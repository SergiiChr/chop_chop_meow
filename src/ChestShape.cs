using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared hollow wooden chest and fitted hardware; the lid meshes use the rear hinge's local frame.</summary>
internal static class ChestShape {
    private static ArrayMesh? body, lid, frame, lidFrame;
    public static Mesh Body => body ??= BuildWood(false);
    public static Mesh Lid => lid ??= BuildWood(true);
    public static Mesh Frame => frame ??= BuildMetal(false);
    public static Mesh LidFrame => lidFrame ??= BuildMetal(true);
    public static readonly StandardMaterial3D Wood = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .9f };
    public static readonly StandardMaterial3D Metal = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .5f, Metallic = .6f };

    private static Vector3[] Ring(float x, float z, float y, float corner, float forward = 0) {
        return new[] { new Vector3(-x + corner, y, -z + forward), new(x - corner, y, -z + forward),
            new(x, y, -z + corner + forward), new(x, y, z - corner + forward),
            new(x - corner, y, z + forward), new(-x + corner, y, z + forward),
            new(-x, y, z - corner + forward), new(-x, y, -z + corner + forward) };
    }

    private static ArrayMesh BuildWood(bool isLid) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        if (isLid) {
            var a = Ring(.435f, .285f, 0, .025f, .275f);
            var b = Ring(.445f, .295f, .012f, .03f, .275f);
            var c = Ring(.445f, .295f, .047f, .03f, .275f);
            var d = Ring(.433f, .283f, .06f, .025f, .275f);
            Join(s, a, b, "9d7a4c"); Join(s, b, c, "b38a55"); Join(s, c, d, "c9a46e");
            Cap(s, a, Vector3.Down, "8c683f"); Cap(s, d, Vector3.Up, "c9a46e");
        } else {
            var a = Ring(.415f, .265f, .04f, .025f);
            var b = Ring(.425f, .275f, .055f, .03f);
            var c = Ring(.425f, .275f, .26f, .03f);
            var d = Ring(.425f, .275f, .264f, .03f);
            var e = Ring(.425f, .275f, .475f, .03f);
            var f = Ring(.415f, .265f, .49f, .025f);
            var rim = Ring(.375f, .225f, .49f, .015f);
            var floor = Ring(.375f, .225f, .10f, .015f);
            Join(s, a, b, "9d7a4c"); Join(s, b, c, "c9a46e"); Join(s, c, d, "967447");
            Join(s, d, e, "bf985f"); Join(s, e, f, "c9a46e");
            Join(s, f, rim, "b08a55"); Join(s, rim, floor, "9f7949", inward: true);
            Cap(s, floor, Vector3.Up, "a9804e"); Cap(s, a, Vector3.Down, "89653d");
        }
        return s.Commit();
    }

    private static ArrayMesh BuildMetal(bool isLid) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        if (isLid) {
            foreach (float x in new[] { -.25f, .25f })
                Tube(s, new[] { new Vector3(x - .06f, 0, 0), new Vector3(x + .06f, 0, 0) }, .014f);
            Box(s, new Vector3(-.045f, -.05f, .565f), new Vector3(.045f, .03f, .58f));
        } else {
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f }) {
                    Vector2[] outline = { new(.38f, .29f), new(.416f, .29f), new(.44f, .266f), new(.44f, .22f),
                        new(.423f, .22f), new(.423f, .252f), new(.405f, .273f), new(.38f, .273f) };
                    for (int i = 0; i < outline.Length; i++) outline[i] *= new Vector2(x, z);
                    Extrude(s, outline, .015f, .495f);
                }
            foreach (float x in new[] { -.25f, .25f }) {
                Tube(s, new[] { new Vector3(x - .075f, .33f, .277f), new(x - .075f, .30f, .315f),
                    new(x - .055f, .27f, .33f), new(x + .055f, .27f, .33f),
                    new(x + .075f, .30f, .315f), new(x + .075f, .33f, .277f) }, .012f);
                Box(s, new Vector3(x - .06f, .42f, -.287f), new Vector3(x + .06f, .49f, -.274f));
            }
            Box(s, new Vector3(-.055f, .415f, .275f), new Vector3(.055f, .475f, .29f));
        }
        return s.Commit();
    }

    private static void Join(SurfaceTool s, Vector3[] a, Vector3[] b, string tint, bool inward = false) {
        for (int i = 0; i < a.Length; i++) {
            int next = (i + 1) % a.Length;
            var outward = new Vector3((a[i].X + a[next].X) / 2, 0, (a[i].Z + a[next].Z) / 2);
            if (inward) outward = -outward;
            // Coplanar rings form the chest's top rim.
            if (Math.Abs(a[i].Y - b[i].Y) < .001f) outward = Vector3.Up;
            Triangle(s, a[i], a[next], b[next], outward, tint); Triangle(s, a[i], b[next], b[i], outward, tint);
        }
    }

    private static void Cap(SurfaceTool s, Vector3[] ring, Vector3 normal, string tint) {
        var center = Vector3.Zero; foreach (var p in ring) center += p; center /= ring.Length;
        for (int i = 0; i < ring.Length; i++) Triangle(s, center, ring[i], ring[(i + 1) % ring.Length], normal, tint);
    }

    private static void Extrude(SurfaceTool s, Vector2[] outline, float bottom, float top) {
        var indices = Geometry2D.TriangulatePolygon(outline);
        for (int i = 0; i < indices.Length; i += 3) {
            Vector3 P(int index, float y) => new(outline[indices[index]].X, y, outline[indices[index]].Y);
            Triangle(s, P(i, top), P(i + 1, top), P(i + 2, top), Vector3.Up, "34393b");
            Triangle(s, P(i, bottom), P(i + 1, bottom), P(i + 2, bottom), Vector3.Down, "34393b");
        }
        // Polygon orientation determines the outward side, including the guard's recessed inner edges.
        float area = 0; for (int j = 0; j < outline.Length; j++) area += outline[j].Cross(outline[(j + 1) % outline.Length]);
        for (int i = 0; i < outline.Length; i++) {
            var a = outline[i]; var b = outline[(i + 1) % outline.Length];
            var normal = new Vector3((b - a).Y, 0, -(b - a).X) * Math.Sign(area);
            var p = new Vector3(a.X, bottom, a.Y); var q = new Vector3(b.X, bottom, b.Y);
            Triangle(s, p, q, q + Vector3.Up * (top - bottom), normal, "34393b");
            Triangle(s, p, q + Vector3.Up * (top - bottom), p + Vector3.Up * (top - bottom), normal, "34393b");
        }
    }

    private static void Box(SurfaceTool s, Vector3 min, Vector3 max) {
        Vector2[] outline = { new(min.X, min.Z), new(max.X, min.Z), new(max.X, max.Z), new(min.X, max.Z) };
        Extrude(s, outline, min.Y, max.Y);
    }

    private static void Tube(SurfaceTool s, Vector3[] path, float radius) {
        var points = new Vector3[path.Length, 6];
        for (int row = 0; row < path.Length; row++) {
            var tangent = (path[Math.Min(path.Length - 1, row + 1)] - path[Math.Max(0, row - 1)]).Normalized();
            var across = tangent.Cross(Vector3.Up).Normalized();
            var up = across.Cross(tangent).Normalized();
            for (int i = 0; i < 6; i++) points[row, i] = path[row] + radius * (across * MathF.Cos(i * Mathf.Tau / 6) + up * MathF.Sin(i * Mathf.Tau / 6));
        }
        for (int row = 0; row < path.Length - 1; row++)
            for (int i = 0; i < 6; i++) {
                int next = (i + 1) % 6;
                var normal = (points[row, i] + points[row, next]) / 2 - path[row];
                Triangle(s, points[row, i], points[row + 1, i], points[row + 1, next], normal, "34393b");
                Triangle(s, points[row, i], points[row + 1, next], points[row, next], normal, "34393b");
            }
        for (int end = 0; end < 2; end++) {
            int row = end == 0 ? 0 : path.Length - 1;
            for (int i = 0; i < 6; i++) Triangle(s, path[row], points[row, i], points[row, (i + 1) % 6],
                (path[row] - path[end == 0 ? 1 : row - 1]).Normalized(), "34393b");
        }
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, string tint) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        s.SetColor(new Color(tint)); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
