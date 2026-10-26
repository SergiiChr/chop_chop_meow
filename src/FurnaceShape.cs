using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared concrete faces, recessed firebox and steel coping for the cinder-block furnace.</summary>
internal static class FurnaceShape {
    private static ArrayMesh? masonry, plate, fire, coalPile;
    public static readonly StandardMaterial3D Concrete = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .95f };
    public static readonly StandardMaterial3D Steel = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .45f, Metallic = .7f };
    public static readonly StandardMaterial3D Flame = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    public static Mesh Masonry => masonry ??= BuildMasonry();
    public static Mesh Plate => plate ??= BuildPlate();
    public static Mesh Fire => fire ??= BuildFire();
    public static Mesh CoalPile => coalPile ??= BuildCoalPile();

    private static ArrayMesh BuildCoalPile() {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var (x, y, z, radius, tint) in new[] {
            (-.08f, .07f, -.16f, .09f, "292c30"), (.07f, .065f, -.13f, .085f, "3a3c40"),
            (-.09f, .06f, .015f, .085f, "34373b"), (.075f, .07f, .045f, .095f, "26282b"),
            (-.04f, .07f, .19f, .09f, "303338"), (0f, .16f, -.015f, .095f, "3a3c40") }) {
            surface.SetColor(new Color(tint));
            var center = new Vector3(x, y, z);
            for (int i = 0; i < 6; i++) {
                float a = i * Mathf.Tau / 6, b = (i + 1) * Mathf.Tau / 6;
                var first = center + new Vector3(MathF.Cos(a) * radius, 0, MathF.Sin(a) * radius);
                var second = center + new Vector3(MathF.Cos(b) * radius, 0, MathF.Sin(b) * radius);
                Triangle(surface, first, second, center + new Vector3(.015f, radius * .8f, -.01f), (first + second) / 2 - center + Vector3.Up * radius);
                Triangle(surface, first, center - Vector3.Up * radius * .65f, second, (first + second) / 2 - center - Vector3.Up * radius);
            }
        }
        return surface.Commit();
    }

    private static ArrayMesh BuildMasonry() {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        // The structural core leaves a cavity, rather than placing a painted opening over a solid box.
        Box(surface, new Vector3(-.488f, 0, -.438f), new Vector3(.488f, .06f, .438f), "7b7c78");
        Box(surface, new Vector3(-.488f, .06f, -.438f), new Vector3(-.24f, .8f, .438f), "7b7c78");
        Box(surface, new Vector3(.24f, .06f, -.438f), new Vector3(.488f, .8f, .438f), "7b7c78");
        Box(surface, new Vector3(-.24f, .42f, -.438f), new Vector3(.24f, .8f, .438f), "7b7c78");
        Box(surface, new Vector3(-.24f, .06f, -.438f), new Vector3(.24f, .42f, -.32f), "7b7c78");
        for (int row = 0; row < 4; row++) {
            float y0 = row * .2f, y1 = y0 + .2f;
            // Staggered half blocks wrap all four sides; their bevels expose the recessed mortar.
            for (int tile = 0; tile < 3; tile++) {
                float offset = row % 2 == 0 ? 0 : -.25f;
                float x0 = Math.Max(-.5f, -.5f + tile * .5f + offset), x1 = Math.Min(.5f, x0 + .5f);
                if (row % 2 != 0 && tile == 0) x1 = -.25f;
                if (x0 >= .5f) continue;
                string tint = (row + tile) % 3 == 0 ? "a0a19d" : "969994";
                void Front(float left, float right, float bottom, float top) =>
                    Face(surface, left, right, bottom, top, new Vector3(0, 0, .45f), Vector3.Right, Vector3.Up, Vector3.Back, tint);
                if (y0 >= .42f) Front(x0, x1, y0, y1);
                else {
                    Front(x0, Math.Min(x1, -.24f), y0, y1);
                    Front(Math.Max(x0, .24f), x1, y0, y1);
                    Front(Math.Max(x0, -.24f), Math.Min(x1, .24f), y0, Math.Min(y1, .06f));
                    Front(Math.Max(x0, -.24f), Math.Min(x1, .24f), Math.Max(y0, .42f), y1);
                }
                Face(surface, x0, x1, y0, y1, new Vector3(0, 0, -.45f), Vector3.Right, Vector3.Up, Vector3.Forward, tint);
                float z0 = x0 * .9f, z1 = x1 * .9f;
                Face(surface, z0, z1, y0, y1, new Vector3(.5f, 0, 0), Vector3.Back, Vector3.Up, Vector3.Right, tint);
                Face(surface, z0, z1, y0, y1, new Vector3(-.5f, 0, 0), Vector3.Back, Vector3.Up, Vector3.Left, tint);
            }
        }
        // Soot-dark lining and an unlit coal bed remain visible when processing stops.
        surface.SetColor(new Color("37322b"));
        Quad(surface, new(-.238f, .062f, .438f), new(.238f, .062f, .438f), new(.238f, .062f, -.318f), new(-.238f, .062f, -.318f), Vector3.Up);
        Quad(surface, new(-.238f, .418f, .438f), new(-.238f, .418f, -.318f), new(.238f, .418f, -.318f), new(.238f, .418f, .438f), Vector3.Down);
        Quad(surface, new(-.238f, .062f, .438f), new(-.238f, .062f, -.318f), new(-.238f, .418f, -.318f), new(-.238f, .418f, .438f), Vector3.Right);
        Quad(surface, new(.238f, .062f, .438f), new(.238f, .418f, .438f), new(.238f, .418f, -.318f), new(.238f, .062f, -.318f), Vector3.Left);
        Quad(surface, new(-.238f, .062f, -.318f), new(.238f, .062f, -.318f), new(.238f, .418f, -.318f), new(-.238f, .418f, -.318f), Vector3.Back);
        Box(surface, new Vector3(-.19f, .062f, .1f), new Vector3(.19f, .107f, .38f), "262522");
        return surface.Commit();
    }

    private static void Face(SurfaceTool surface, float x0, float x1, float y0, float y1, Vector3 origin, Vector3 across, Vector3 up, Vector3 outward, string tint) {
        if (x1 - x0 < .025f || y1 - y0 < .025f) return;
        x0 += .005f; x1 -= .005f; y0 += .005f; y1 -= .005f;
        float bevel = Math.Min(.012f, Math.Min(x1 - x0, y1 - y0) * .18f), corner = bevel * .5f;
        Vector2[] outer = { new(x0 + corner, y0), new(x1 - corner, y0), new(x1, y0 + corner), new(x1, y1 - corner),
            new(x1 - corner, y1), new(x0 + corner, y1), new(x0, y1 - corner), new(x0, y0 + corner) };
        Vector2[] inner = { new(x0 + bevel, y0 + bevel), new(x1 - bevel, y0 + bevel), new(x1 - bevel, y1 - bevel), new(x0 + bevel, y1 - bevel) };
        Vector3 Outer(int index) => origin + across * outer[index].X + up * outer[index].Y - outward * .01f;
        Vector3 Inner(int index) => origin + across * inner[index].X + up * inner[index].Y;
        surface.SetColor(new Color(tint));
        Quad(surface, Inner(0), Inner(1), Inner(2), Inner(3), outward);
        for (int side = 0; side < 4; side++) {
            int next = (side + 1) % 4;
            Quad(surface, Outer(side * 2), Outer(side * 2 + 1), Inner(next), Inner(side), outward);
            Triangle(surface, Outer(side * 2 + 1), Outer((side * 2 + 2) % 8), Inner(next), outward);
        }
    }

    private static ArrayMesh BuildPlate() {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] outline = { new(-.545f, 0, -.5f), new(.545f, 0, -.5f), new(.57f, 0, -.475f), new(.57f, 0, .475f),
            new(.545f, 0, .5f), new(-.545f, 0, .5f), new(-.57f, 0, .475f), new(-.57f, 0, -.475f) };
        surface.SetColor(new Color("45494c"));
        for (int i = 0; i < 8; i++) {
            int next = (i + 1) % 8;
            var a = outline[i] + Vector3.Up * .8f; var b = outline[next] + Vector3.Up * .8f;
            Quad(surface, a, b, b + Vector3.Up * .05f, a + Vector3.Up * .05f, (outline[i] + outline[next]).Normalized());
            Triangle(surface, Vector3.Up * .85f, a + Vector3.Up * .05f, b + Vector3.Up * .05f, Vector3.Up);
            Triangle(surface, Vector3.Up * .8f, a, b, Vector3.Down);
        }
        return surface.Commit();
    }

    private static ArrayMesh BuildFire() {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetColor(new Color("e95c1d"));
        for (int i = 0; i < 8; i++) {
            float a = i * Mathf.Tau / 8, b = (i + 1) * Mathf.Tau / 8;
            Triangle(surface, new Vector3(0, .117f, .25f), new Vector3(MathF.Cos(a) * .18f, .11f, .25f + MathF.Sin(a) * .12f),
                new Vector3(MathF.Cos(b) * .18f, .11f, .25f + MathF.Sin(b) * .12f), Vector3.Up);
        }
        foreach (var (x, z, height, tint) in new[] { (-.1f, .27f, .26f, "ff9b30"), (.025f, .24f, .34f, "ffc15b"), (.12f, .3f, .23f, "ff7b25") }) {
            surface.SetColor(new Color(tint));
            for (int i = 0; i < 4; i++) {
                float a = i * Mathf.Tau / 4, b = (i + 1) * Mathf.Tau / 4;
                var first = new Vector3(x + MathF.Cos(a) * .05f, .115f, z + MathF.Sin(a) * .05f);
                var second = new Vector3(x + MathF.Cos(b) * .05f, .115f, z + MathF.Sin(b) * .05f);
                Triangle(surface, first, second, new Vector3(x + .015f, height, z), (first + second) / 2 - new Vector3(x, .115f, z));
            }
        }
        return surface.Commit();
    }

    private static void Box(SurfaceTool surface, Vector3 min, Vector3 max, string color) {
        var a = new Vector3(min.X, min.Y, min.Z); var b = new Vector3(max.X, min.Y, min.Z);
        var c = new Vector3(max.X, min.Y, max.Z); var d = new Vector3(min.X, min.Y, max.Z);
        var e = new Vector3(min.X, max.Y, min.Z); var f = new Vector3(max.X, max.Y, min.Z);
        var g = new Vector3(max.X, max.Y, max.Z); var h = new Vector3(min.X, max.Y, max.Z);
        surface.SetColor(new Color(color));
        Quad(surface, a, d, c, b, Vector3.Down); Quad(surface, e, f, g, h, Vector3.Up);
        Quad(surface, a, b, f, e, Vector3.Forward); Quad(surface, d, h, g, c, Vector3.Back);
        Quad(surface, a, e, h, d, Vector3.Left); Quad(surface, b, c, g, f, Vector3.Right);
    }

    private static void Quad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) {
        Triangle(surface, a, b, c, outward); Triangle(surface, a, c, d, outward);
    }

    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 outward) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        surface.SetNormal(normal); surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
    }
}
