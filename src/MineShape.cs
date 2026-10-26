using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Cached mine portal, open tunnel surfaces and a fitted flared cart with wheels seated on the rails.</summary>
internal static class MineShape {
    private static ArrayMesh? outline, tunnel, pad, portal, supports, rails, sleepers, deepRails, cart, load;
    public static Mesh Outline => outline ??= BuildOutline();
    public static Mesh Tunnel => tunnel ??= BuildTunnel();
    public static Mesh Pad => pad ??= BuildPad();
    public static Mesh Portal => portal ??= BuildPortal();
    public static Mesh Supports => supports ??= BuildSupports();
    public static Mesh Rails => rails ??= BuildRails(false);
    public static Mesh Sleepers => sleepers ??= BuildSleepers();
    public static Mesh DeepRails => deepRails ??= BuildRails(true);
    public static Mesh Cart => cart ??= BuildCart();
    public static Mesh Load => load ??= BuildLoad();
    public static readonly StandardMaterial3D Timber = VertexMaterial(.9f);
    public static readonly StandardMaterial3D Steel = VertexMaterial(.5f, .6f);
    public static readonly StandardMaterial3D Unlit = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    private static StandardMaterial3D VertexMaterial(float roughness, float metallic = 0) => new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = roughness, Metallic = metallic };
    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }
    private static string Shade(string tint, int step) => new Color(tint).Lerp(Colors.Black, MathF.Pow((step + 1) / 6f, .45f)).ToHtml(false);

    private static ArrayMesh BuildOutline() {
        var s = Begin(); const float door = WorldModels.MineDoor;
        foreach (float x in new[] { -.8f, .8f }) Box(s, new(.06f, .03f, 3.3f), new(x, .02f, door + 1.65f), "ffffff");
        foreach (float z in new[] { door, door + 3.3f }) Box(s, new(1.66f, .03f, .06f), new(0, .02f, z), "ffffff");
        foreach (float x in new[] { -.7f, .7f }) Box(s, new(.08f, 1.75f, .08f), new(x, .88f, door), "ffffff");
        Box(s, new(1.56f, .08f, .08f), new(0, 1.75f, door), "ffffff");
        return s.Commit();
    }

    private static ArrayMesh BuildTunnel() {
        var s = Begin();
        // Only the inward faces are needed; all outer faces lie inside the mountain outcrop.
        for (int i = 0; i < 6; i++) {
            float near = -i * WorldModels.MineDepth / 6, far = -(i + 1) * WorldModels.MineDepth / 6;
            Quad(s, new(-.75f, .02f, near), new(.75f, .02f, near), new(.75f, .02f, far), new(-.75f, .02f, far), Vector3.Up, Shade("3a332c", i));
            foreach (float x in new[] { -.72f, .72f })
                Quad(s, new(x, .02f, near), new(x, 1.78f, near), new(x, 1.78f, far), new(x, .02f, far), new(-x, 0, 0), Shade("3f3830", i));
            Quad(s, new(-.76f, 1.76f, near), new(.76f, 1.76f, near), new(.76f, 1.76f, far), new(-.76f, 1.76f, far), Vector3.Down, Shade("362f28", i));
        }
        Quad(s, new(-.75f, 0, -3), new(.75f, 0, -3), new(.75f, 1.8f, -3), new(-.75f, 1.8f, -3), Vector3.Back, "000000");
        return s.Commit();
    }

    private static ArrayMesh BuildPad() {
        var s = Begin(); Bevel(s, new(1.6f, .03f, 1.4f), new(0, .015f, .7f), .01f, "5d564c"); return s.Commit();
    }

    private static ArrayMesh BuildPortal() {
        var s = Begin();
        foreach (float x in new[] { -.7f, .7f }) Bevel(s, new(.2f, 1.8f, .2f), new(x, .9f, .08f), .012f, "8a7a62");
        Bevel(s, new(1.8f, .22f, .26f), new(0, 1.89f, .08f), .015f, "8a7a62");
        foreach (int side in new[] { -1, 1 })
            Box(s, new(.1f, .42f, .12f), new(side * .55f, 1.65f, .08f), "796b57", new(0, 0, side * -45));
        // Each hood board meets the next on the same six-degree roof plane.
        for (int i = 0; i < 5; i++) {
            float z = .2f - i * .19f;
            Bevel(s, new(1.9f, .05f, .19f), new(0, 2.02f + z * .105f, z), .006f, i % 2 == 0 ? "6f6150" : "796b58", new(-6, 0, 0));
        }
        Box(s, new(.5f, .32f, .03f), new(-1.1f, 1.15f, .25f), "e8e2d2", new(0, 0, -3));
        Box(s, new(.4f, .06f, .031f), new(-1.1f, 1.22f, .25f), "b3261e", new(0, 0, -3));
        Box(s, new(.06f, .9f, .06f), new(-1.1f, .5f, .2f), "8a7a62");
        return s.Commit();
    }

    private static ArrayMesh BuildSupports() {
        var s = Begin();
        for (int i = 1; i < 6; i += 2) {
            float z = -i * WorldModels.MineDepth / 6;
            foreach (float x in new[] { -.64f, .64f }) Box(s, new(.12f, 1.72f, .12f), new(x, .86f, z), Shade("6f6150", i));
            Box(s, new(1.4f, .12f, .14f), new(0, 1.7f, z), Shade("6f6150", i));
        }
        return s.Commit();
    }

    private static void Rail(SurfaceTool s, float x, float near, float far, string tint, bool front, bool back) {
        Vector3 A(float offset, float y, float z) => new(x + offset, y, z);
        Quad(s, A(-.025f, .045f, near), A(.025f, .045f, near), A(.025f, .045f, far), A(-.025f, .045f, far), Vector3.Down, tint);
        Quad(s, A(-.025f, .095f, near), A(.025f, .095f, near), A(.025f, .095f, far), A(-.025f, .095f, far), Vector3.Up, tint);
        foreach (float offset in new[] { -.025f, .025f })
            Quad(s, A(offset, .045f, near), A(offset, .095f, near), A(offset, .095f, far), A(offset, .045f, far), new(offset, 0, 0), tint);
        if (front) Quad(s, A(-.025f, .045f, near), A(.025f, .045f, near), A(.025f, .095f, near), A(-.025f, .095f, near), Vector3.Back, tint);
        if (back) Quad(s, A(-.025f, .045f, far), A(.025f, .045f, far), A(.025f, .095f, far), A(-.025f, .095f, far), Vector3.Forward, tint);
    }

    private static ArrayMesh BuildRails(bool deep) {
        var s = Begin(); const float door = WorldModels.MineDoor;
        if (!deep) foreach (float x in new[] { -.28f, .28f }) Rail(s, x, 1, door, "6c7175", true, false);
        else
            for (int i = 0; i < 6; i++) {
                float near = door - i * WorldModels.MineDepth / 6, far = door - (i + 1) * WorldModels.MineDepth / 6;
                foreach (float x in new[] { -.28f, .28f }) Rail(s, x, near, far, Shade("8a8f93", i), false, i == 5);
                Box(s, new(.8f, .04f, .14f), new(0, .045f, (near + far) / 2), Shade("5a4030", i));
            }
        return s.Commit();
    }

    private static ArrayMesh BuildSleepers() {
        var s = Begin();
        for (int i = 0; i < 6; i++) Box(s, new(.8f, .04f, .14f), new(0, .025f, WorldModels.MineDoor + .2f + i * .55f), "6e4a2e");
        return s.Commit();
    }

    private static ArrayMesh BuildCart() {
        var s = Begin();
        Vector3[] Ring(float x, float z, float y) => new[] { new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z) };
        var bottom = Ring(.29f, .37f, .34f); var top = Ring(.39f, .48f, .73f);
        var innerBottom = Ring(.26f, .34f, .38f); var innerTop = Ring(.355f, .445f, .73f);
        for (int i = 0; i < 4; i++) {
            int next = (i + 1) % 4; var outward = top[i] + top[next]; outward.Y = 0;
            Quad(s, bottom[i], bottom[next], top[next], top[i], outward, "4f5a5e");
            Quad(s, innerBottom[i], innerBottom[next], innerTop[next], innerTop[i], -outward, "39464b");
            Quad(s, top[i], top[next], innerTop[next], innerTop[i], Vector3.Up, "879297");
        }
        Quad(s, bottom[0], bottom[1], bottom[2], bottom[3], Vector3.Down, "4f5a5e");
        Quad(s, innerBottom[0], innerBottom[1], innerBottom[2], innerBottom[3], Vector3.Up, "39464b");
        Box(s, new(.55f, .06f, .72f), new(0, .31f, 0), "343b3f");
        foreach (float z in new[] { -.28f, .28f }) {
            Cylinder(s, new(0, .215f, z), .027f, .61f, 6, "646d71");
            foreach (float x in new[] { -.3f, .3f }) Cylinder(s, new(x, .215f, z), .12f, .07f, 8, "2b2d30");
        }
        return s.Commit();
    }

    private static void Cylinder(SurfaceTool s, Vector3 at, float radius, float length, int sides, string tint) {
        Vector3 Point(int i, float x) => at + new Vector3(x, MathF.Cos(i * Mathf.Tau / sides) * radius, MathF.Sin(i * Mathf.Tau / sides) * radius);
        float half = length / 2;
        for (int i = 0; i < sides; i++) {
            int next = (i + 1) % sides;
            Quad(s, Point(i, -half), Point(next, -half), Point(next, half), Point(i, half), new(0, MathF.Cos(i * Mathf.Tau / sides), MathF.Sin(i * Mathf.Tau / sides)), tint);
        }
        for (int i = 1; i < sides - 1; i++) {
            Triangle(s, Point(0, -half), Point(i, -half), Point(i + 1, -half), Vector3.Left, tint);
            Triangle(s, Point(0, half), Point(i, half), Point(i + 1, half), Vector3.Right, tint);
        }
    }

    private static ArrayMesh BuildLoad() {
        var source = NatureShape.Loose("loose_iron", new Color("a35a2e"), new Color("8b4f2b"), 0);
        var bounds = source.GetAabb(); var scale = new Vector3(.6f, .23f, .72f) / bounds.Size;
        var points = source.GetFaces(); var s = Begin();
        for (int i = 0; i < points.Length; i += 3) {
            Vector3 At(int index) => (points[index] - bounds.GetCenter()) * scale + new Vector3(0, .66f, 0);
            var a = At(i); var b = At(i + 1); var c = At(i + 2);
            Triangle(s, a, b, c, (c - a).Cross(b - a), "ffffff");
        }
        return s.Commit();
    }

    private static void Bevel(SurfaceTool s, Vector3 size, Vector3 center, float bevel, string tint, Vector3 rotation = default) {
        // Four-corner rings chamfer the horizontal edges without subdividing every vertical corner.
        var basis = Basis.FromEuler(rotation * (Mathf.Pi / 180));
        float x = size.X / 2, z = size.Z / 2, y = size.Y / 2;
        Vector3[] Ring(float width, float depth, float height) => new[] {
            center + basis * new Vector3(-width, height, -depth), center + basis * new Vector3(width, height, -depth),
            center + basis * new Vector3(width, height, depth), center + basis * new Vector3(-width, height, depth) };
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

    private static void Box(SurfaceTool s, Vector3 size, Vector3 center, string tint, Vector3 rotation = default) {
        var basis = Basis.FromEuler(rotation * (Mathf.Pi / 180));
        foreach (var axis in new[] { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward }) {
            var u = axis.X != 0 ? Vector3.Up : Vector3.Right; var v = axis.Cross(u);
            Vector3 Corner(float x, float y) => center + basis * ((axis + u * x + v * y) * size / 2);
            Quad(s, Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1), basis * axis, tint);
        }
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
