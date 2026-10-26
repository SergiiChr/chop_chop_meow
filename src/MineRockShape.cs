using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>A continuous cave outcrop, rooted below the mountain surface instead of capped as a separate mound.</summary>
internal static class MineRockShape {
    private static readonly Dictionary<Cliff, ArrayMesh> meshes = new();
    public static readonly StandardMaterial3D Rock = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .95f };

    public static Mesh Mesh(Cliff cliff, string[] palette) {
        if (meshes.TryGetValue(cliff, out var mesh)) return mesh;
        var spot = World.MineSpot(cliff); var outwards = World.MineOut(cliff); var across = new Vector2(outwards.Y, -outwards.X);
        Vector2 WorldPoint(float x, float z) => spot + across * x + outwards * z;
        float root = -7;
        while (root > -32 && (Terrain.FromMountains(WorldPoint(-4.6f, root)) > -6 || Terrain.FromMountains(WorldPoint(0, root)) > -6 || Terrain.FromMountains(WorldPoint(4.6f, root)) > -6)) root -= 1;
        float[] depth = { WorldModels.MineDoor - .08f, -4.2f, -6, root };
        float[] width = { 2.55f, 2.8f, 3.35f, 4.6f };
        float[] crown = { 3.05f, 3.6f, 4.3f, 0 };
        Vector2[] section = { new(-1, 0), new(-.94f, .48f), new(-.69f, .88f), new(-.27f, 1), new(.26f, .98f), new(.7f, .87f), new(.95f, .47f), new(1, 0) };
        var rings = new Vector3[4][];
        for (int row = 0; row < rings.Length; row++) {
            rings[row] = new Vector3[8];
            for (int i = 0; i < 8; i++) {
                float x = section[i].X * width[row], ground = MountainShape.Surface(WorldPoint(x, depth[row]));
                float y = row == 3 ? Math.Min(section[i].Y * 4.3f, ground - .8f) : i == 0 || i == 7 ? -.12f : section[i].Y * crown[row];
                rings[row][i] = new(x, y, depth[row]);
            }
        }
        var s = new SurfaceTool(); s.Begin(Godot.Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int tint) {
            var normal = (c - a).Cross(b - a).Normalized();
            if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
            s.SetColor(new Color(palette[Math.Abs(tint) % palette.Length])); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int tint) {
            Triangle(a, b, c, outward, tint); Triangle(a, c, d, outward, tint);
        }
        // Open-backed skin: the whole rear edge is buried below the same mountain triangles it joins.
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < 8; i++) {
                int next = (i + 1) % 8;
                var outward = new Vector3(section[i].X + section[next].X, i == 7 ? -1 : .8f, 0);
                Quad(rings[row][i], rings[row][next], rings[row + 1][next], rings[row + 1][i], outward, row + i / 2);
            }
        // The mouth is a deliberate aperture; nothing is drawn across the tunnel opening.
        Vector3[] mouth = { new(-.72f, .02f, depth[0]), new(-.72f, .9f, depth[0]), new(-.72f, 1.66f, depth[0]), new(-.4f, 1.76f, depth[0]), new(.4f, 1.76f, depth[0]), new(.72f, 1.66f, depth[0]), new(.72f, .9f, depth[0]), new(.72f, .02f, depth[0]) };
        for (int i = 0; i < 8; i++) {
            int next = (i + 1) % 8;
            Quad(rings[0][i], rings[0][next], mouth[next], mouth[i], Vector3.Back, i / 2);
        }
        return meshes[cliff] = s.Commit();
    }
}
