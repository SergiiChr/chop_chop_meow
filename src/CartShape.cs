using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A hollow pressed-steel garden cart, continuous pull frame and dished wheels.</summary>
internal static class CartShape {
    private static ArrayMesh? frame, wheel, payment;
    public static Mesh Frame => frame ??= BuildFrame();
    public static Mesh Wheel => wheel ??= BuildWheel();
    public static Mesh Payment => payment ??= BuildPayment();
    public static readonly StandardMaterial3D Steel = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .65f, Metallic = .35f };
    public static readonly StandardMaterial3D WheelSurface = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .9f };

    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static Vector3[] Ring(float x, float z, float y, float bevel) => new[] {
        new Vector3(-x+bevel,y,-z),new(x-bevel,y,-z),new(x,y,-z+bevel),new(x,y,z-bevel),
        new(x-bevel,y,z),new(-x+bevel,y,z),new(-x,y,z-bevel),new(-x,y,-z+bevel) };

    private static ArrayMesh BuildFrame() {
        var s = Begin();
        // Continuous tapered outer shell, folded bead and inner lining meet the inset bed.
        var bottom = Ring(.3f, .36f, .385f, .045f);
        var shoulder = Ring(.35f, .425f, .62f, .05f);
        var bead = Ring(.365f, .44f, .64f, .055f);
        var lip = Ring(.365f, .44f, .662f, .055f);
        var inner = Ring(.33f, .405f, .65f, .045f);
        var floor = Ring(.277f, .337f, .415f, .035f);
        Join(s, bottom, shoulder, "497541"); Join(s, shoulder, bead, "58854b"); Join(s, bead, lip, "6b9659");
        Join(s, lip, inner, "426c39", Vector3.Up); Join(s, inner, floor, "34592e", inward: true);
        Cap(s, floor, Vector3.Up, "3b6334"); Cap(s, bottom, Vector3.Down, "314a2d");
        // Two fitted bed supports and a full axle tie the wheels to the tray.
        foreach (float x in new[] { -.235f, .235f }) {
            Box(s, new(.045f, .045f, .69f), new(x, .353f, 0), "535c5d");
            Box(s, new(.045f, .095f, .055f), new(x, .3f, 0), "535c5d");
        }
        Tube(s, new[] { new Vector3(-.465f, .26f, 0), new(.465f, .26f, 0) }, .027f, "747d7f");
        // One bent U-shaped tube reaches the courier's existing hand position.
        Tube(s, new[] { new Vector3(-.235f, .355f, -.26f), new(-.235f, .355f, .28f), new(-.265f, .48f, .55f), new(-.265f, .74f, 1.02f), new(.265f, .74f, 1.02f), new(.265f, .48f, .55f), new(.235f, .355f, .28f), new(.235f, .355f, -.26f) }, .021f, "8c9697", grip: 3);
        return s.Commit();
    }

    private static ArrayMesh BuildWheel() {
        var s = Begin();
        // A single continuous section joins recessed hubs, dished steel rims and the rounded tire.
        Vector2[] profile = { new(0, -.06f), new(.075f, -.06f), new(.2f, -.035f), new(.26f, -.035f), new(.26f, .035f), new(.2f, .035f), new(.075f, .06f), new(0, .06f) };
        for (int i = 0; i < 12; i++) {
            float a = i * MathF.Tau / 12, b = (i + 1) * MathF.Tau / 12;
            Vector3 Point(Vector2 p, float angle) => new(p.Y, MathF.Cos(angle) * p.X, MathF.Sin(angle) * p.X);
            for (int j = 0; j < profile.Length - 1; j++) {
                var p = profile[j]; var q = profile[j + 1];
                var outward = new Vector3(p.X - q.X, MathF.Cos((a + b) / 2) * (q.Y - p.Y), MathF.Sin((a + b) / 2) * (q.Y - p.Y));
                string tint = j >= 2 && j <= 4 ? (j == 3 ? "292c2b" : "343735") : j == 0 || j == 6 ? "bcc4c3" : "788780";
                if (p.X > 0) Triangle(s, Point(p, a), Point(p, b), Point(q, b), outward, tint);
                if (q.X > 0) Triangle(s, Point(p, a), Point(q, b), Point(q, a), outward, tint);
            }
        }
        return s.Commit();
    }

    private static ArrayMesh BuildPayment() {
        var s = Begin();
        var bottom = Ring(.18f, .13f, .015f, .018f); var upper = Ring(.18f, .13f, .185f, .018f);
        Join(s, bottom, upper, "596467"); Cap(s, bottom, Vector3.Down, "394448");
        var seam = Ring(.184f, .134f, .19f, .019f); var lid = Ring(.18f, .13f, .22f, .018f);
        Join(s, upper, seam, "283335"); Join(s, seam, lid, "7a8588"); Cap(s, lid, Vector3.Up, "6a767a");
        Tube(s, new[] { new Vector3(-.065f, .224f, 0), new(-.065f, .265f, 0), new(.065f, .265f, 0), new(.065f, .224f, 0) }, .009f, "293638");
        Box(s, new(.045f, .035f, .012f), new(0, .18f, .137f), "c2b688");
        return s.Commit();
    }

    private static void Join(SurfaceTool s, Vector3[] a, Vector3[] b, string tint, Vector3? normal = null, bool inward = false) {
        for (int i = 0; i < a.Length; i++) {
            int next = (i + 1) % a.Length;
            var outward = normal ?? new Vector3((a[i].X + a[next].X) / 2, 0, (a[i].Z + a[next].Z) / 2) * (inward ? -1 : 1);
            Quad(s, a[i], a[next], b[next], b[i], outward, tint);
        }
    }
    private static void Cap(SurfaceTool s, Vector3[] ring, Vector3 normal, string tint) {
        for (int i = 1; i < ring.Length - 1; i++) Triangle(s, ring[0], ring[i], ring[i + 1], normal, tint);
    }
    private static void Tube(SurfaceTool s, Vector3[] path, float radius, string tint, int grip = -1) {
        var rings = new Vector3[path.Length, 6];
        for (int i = 0; i < path.Length; i++) {
            var axis = (path[Math.Min(i + 1, path.Length - 1)] - path[Math.Max(0, i - 1)]).Normalized();
            var u = axis.Cross(Vector3.Up).Normalized(); if (u.LengthSquared() < .1f) u = Vector3.Right;
            var v = axis.Cross(u);
            for (int side = 0; side < 6; side++) rings[i, side] = path[i] + (u * MathF.Cos(side * MathF.Tau / 6) + v * MathF.Sin(side * MathF.Tau / 6)) * radius;
        }
        for (int i = 0; i < path.Length - 1; i++)
            for (int side = 0; side < 6; side++) {
                int next = (side + 1) % 6;
                Quad(s, rings[i, side], rings[i, next], rings[i + 1, next], rings[i + 1, side], (rings[i, side] + rings[i, next]) / 2 - path[i], i == grip ? "262e2b" : tint);
            }
        for (int side = 1; side < 5; side++) {
            Triangle(s, rings[0, 0], rings[0, side], rings[0, side + 1], path[0] - path[1], tint);
            int end = path.Length - 1;
            Triangle(s, rings[end, 0], rings[end, side], rings[end, side + 1], path[end] - path[end - 1], tint);
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
