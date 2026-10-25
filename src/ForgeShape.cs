using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared continuous tank shell, recessed refractory chamber and fitted bellows boards.</summary>
internal static class ForgeShape {
    private static ArrayMesh? shell, fire, board;
    public static Mesh Shell => shell ??= BuildShell();
    public static Mesh Fire => fire ??= BuildFire();
    public static Mesh Board => board ??= BuildBoard();

    private static ArrayMesh BuildShell() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        // A single revolved profile: domed rear, flush steel bands, rolled lip and deep lining.
        (float Z, float Radius, string Tint)[] rings = {
            (-.59f, 0, "4d6a7a"), (-.57f, .14f, "4d6a7a"), (-.52f, .27f, "4d6a7a"),
            (-.44f, .35f, "4d6a7a"), (-.35f, .38f, "4d6a7a"),
            (-.29f, .38f, "4d6a7a"), (-.28f, .393f, "3b3f42"), (-.23f, .393f, "3b3f42"), (-.22f, .38f, "4d6a7a"),
            (.17f, .38f, "4d6a7a"), (.18f, .393f, "3b3f42"), (.23f, .393f, "3b3f42"), (.24f, .38f, "4d6a7a"),
            (.38f, .38f, "4d6a7a"), (.44f, .36f, "3b3f42"), (.47f, .33f, "3b3f42"),
            (.47f, .25f, "c4b18e"), (.44f, .225f, "b9a17d"), (.30f, .215f, "7a6550"),
            (-.28f, .215f, "40352c"), (-.30f, 0, "302923") };
        for (int j = 0; j < rings.Length - 1; j++) {
            var a = rings[j]; var b = rings[j + 1];
            for (int i = 0; i < 24; i++) {
                float u = i * Mathf.Tau / 24, v = (i + 1) * Mathf.Tau / 24;
                Vector3 P(float z, float r, float t) => new(MathF.Cos(t) * r, .9f + MathF.Sin(t) * r, z);
                Vector3 N(float t) => new Vector3(MathF.Cos(t) * (b.Z - a.Z), MathF.Sin(t) * (b.Z - a.Z), a.Radius - b.Radius).Normalized();
                // Profile winds from the rear out across the nose, then back through the chamber.
                s.SetColor(new Color(b.Tint));
                if (b.Radius > 0) {
                    Vertex(s, P(a.Z, a.Radius, u), N(u)); Vertex(s, P(b.Z, b.Radius, u), N(u)); Vertex(s, P(b.Z, b.Radius, v), N(v));
                }
                if (a.Radius > 0) {
                    Vertex(s, P(a.Z, a.Radius, u), N(u)); Vertex(s, P(b.Z, b.Radius, v), N(v)); Vertex(s, P(a.Z, a.Radius, v), N(v));
                }
            }
        }
        return s.Commit();
    }

    private static ArrayMesh BuildFire() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 24; i++) {
            float a = i * Mathf.Tau / 24, b = (i + 1) * Mathf.Tau / 24;
            s.SetColor(new Color("ffb44c")); Vertex(s, new Vector3(0, .9f, .22f), Vector3.Back);
            s.SetColor(new Color("e96020")); Vertex(s, new Vector3(MathF.Cos(b) * .21f, .9f + MathF.Sin(b) * .21f, .22f), Vector3.Back);
            Vertex(s, new Vector3(MathF.Cos(a) * .21f, .9f + MathF.Sin(a) * .21f, .22f), Vector3.Back);
        }
        return s.Commit();
    }

    private static ArrayMesh BuildBoard() {
        // Tangent shoulders flow into a round heel without overlapping triangle and cylinder caps.
        var outline = new System.Collections.Generic.List<Vector3> { new(0, 0, -.045f), new(0, 0, .045f) };
        for (int i = 0; i <= 18; i++) {
            float angle = Mathf.DegToRad(120 - i * 240f / 18);
            outline.Add(new Vector3(.68f + MathF.Cos(angle) * .34f, 0, MathF.Sin(angle) * .34f));
        }
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < outline.Count; i++) {
            var a = outline[i]; var b = outline[(i + 1) % outline.Count];
            var up = Vector3.Up * .02f;
            Triangle(s, new Vector3(.5f, .02f, 0), a + up, b + up, Vector3.Up);
            Triangle(s, new Vector3(.5f, -.02f, 0), b - up, a - up, Vector3.Down);
            var normal = new Vector3(-(b - a).Z, 0, (b - a).X).Normalized();
            Triangle(s, a - up, b - up, b + up, normal); Triangle(s, a - up, b + up, a + up, normal);
        }
        return s.Commit();
    }

    private static void Vertex(SurfaceTool s, Vector3 p, Vector3 n) { s.SetNormal(n); s.AddVertex(p); }
    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        Vertex(s, a, normal); Vertex(s, b, normal); Vertex(s, c, normal);
    }
}
