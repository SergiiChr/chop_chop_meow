using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared cast anvil profile, bevelled working face and fitted swept horn.</summary>
internal static class AnvilShape {
    private static ArrayMesh? casting;
    public static Mesh Casting => casting ??= Build();
    public static readonly StandardMaterial3D Iron = new() { VertexColorUseAsAlbedo = true, Roughness = .45f, Metallic = .7f };

    private static ArrayMesh Build() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        (float Y, float X, float Z, float Center)[] rows = {
            (.495f, .165f, .115f, 0), (.505f, .18f, .125f, 0), (.535f, .18f, .125f, 0),
            (.56f, .16f, .105f, 0), (.59f, .125f, .083f, 0), (.625f, .10f, .07f, 0),
            (.665f, .095f, .066f, 0), (.695f, .115f, .077f, -.01f),
            (.72f, .16f, .09f, -.02f), (.74f, .23f, .10f, -.03f),
            (.825f, .23f, .10f, -.03f), (.84f, .22f, .09f, -.03f) };
        Vector3 Corner(int row, int corner) {
            var r = rows[row]; const float bevel = .018f;
            Vector2[] outline = { new(-r.X + bevel, -r.Z), new(r.X - bevel, -r.Z), new(r.X, -r.Z + bevel), new(r.X, r.Z - bevel),
                new(r.X - bevel, r.Z), new(-r.X + bevel, r.Z), new(-r.X, r.Z - bevel), new(-r.X, -r.Z + bevel) };
            return new Vector3(outline[corner].X + r.Center, r.Y, outline[corner].Y);
        }
        for (int row = 0; row < rows.Length - 1; row++)
            for (int side = 0; side < 8; side++) {
                int next = (side + 1) % 8;
                var a = Corner(row, side); var b = Corner(row, next); var c = Corner(row + 1, next); var d = Corner(row + 1, side);
                var outward = new Vector3((a.X + b.X) / 2 - rows[row].Center, 0, (a.Z + b.Z) / 2);
                var color = row == rows.Length - 2 ? new Color("7e8587") : new Color("41474a");
                if (row >= 2 && row < 9) {
                    void Write(int r, int corner) {
                        var across = Corner(r, next) - Corner(r, side);
                        var along = Corner(Math.Min(rows.Length - 1, r + 1), corner) - Corner(Math.Max(0, r - 1), corner);
                        var normal = across.Cross(along).Normalized();
                        if (normal.Dot(outward) < 0) normal = -normal;
                        s.SetColor(color); s.SetNormal(normal); s.AddVertex(Corner(r, corner));
                    }
                    Write(row, side); Write(row, next); Write(row + 1, next);
                    Write(row, side); Write(row + 1, next); Write(row + 1, side);
                } else { Triangle(s, a, b, c, outward, color); Triangle(s, a, c, d, outward, color); }
            }
        for (int side = 0; side < 8; side++) {
            int next = (side + 1) % 8;
            Triangle(s, new Vector3(0, rows[0].Y, 0), Corner(0, side), Corner(0, next), Vector3.Down, new Color("41474a"));
            Triangle(s, new Vector3(-.03f, .84f, 0), Corner(rows.Length - 1, side), Corner(rows.Length - 1, next), Vector3.Up, new Color("939b9e"));
        }
        // Elliptical sections fit inside the head and taper upward into the horn's rounded tip.
        (float X, float Y, float Height, float Depth)[] horn = {
            (.185f, .78f, .06f, .094f), (.24f, .782f, .057f, .086f),
            (.31f, .792f, .045f, .063f), (.38f, .806f, .029f, .04f),
            (.435f, .817f, .012f, .018f), (.46f, .82f, .002f, .003f) };
        for (int row = 0; row < horn.Length - 1; row++)
            for (int side = 0; side < 12; side++) {
                Vector3 P(int r, int i) { float t = i * Mathf.Tau / 12; var h = horn[r]; return new Vector3(h.X, h.Y + MathF.Cos(t) * h.Height, MathF.Sin(t) * h.Depth); }
                Vector3 N(int r, int i) {
                    var a = horn[Math.Max(0, r - 1)]; var b = horn[Math.Min(horn.Length - 1, r + 1)];
                    float t = i * Mathf.Tau / 12;
                    var around = new Vector3(0, -MathF.Sin(t) * a.Height, MathF.Cos(t) * a.Depth);
                    var along = new Vector3(b.X - a.X, b.Y - a.Y + MathF.Cos(t) * (b.Height - a.Height), MathF.Sin(t) * (b.Depth - a.Depth));
                    return around.Cross(along).Normalized();
                }
                int next = side + 1;
                Vertex(s, P(row, side), N(row, side)); Vertex(s, P(row + 1, side), N(row + 1, side)); Vertex(s, P(row, next), N(row, next));
                Vertex(s, P(row, next), N(row, next)); Vertex(s, P(row + 1, side), N(row + 1, side)); Vertex(s, P(row + 1, next), N(row + 1, next));
            }
        for (int i = 0; i < 12; i++) {
            float a = i * Mathf.Tau / 12, b = (i + 1) * Mathf.Tau / 12;
            Triangle(s, new Vector3(.461f, .82f, 0), new Vector3(.46f, .82f + MathF.Cos(a) * .002f, MathF.Sin(a) * .003f),
                new Vector3(.46f, .82f + MathF.Cos(b) * .002f, MathF.Sin(b) * .003f), Vector3.Right, new Color("7e8587"));
        }
        return s.Commit();
    }

    private static void Vertex(SurfaceTool s, Vector3 p, Vector3 n) {
        s.SetColor(new Color("41474a").Lerp(new Color("939b9e"), Math.Max(0, n.Y))); s.SetNormal(n); s.AddVertex(p);
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color color) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        s.SetColor(color); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}
