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
                // Slightly broken strata avoid a regular extruded arch while the aperture stays precisely aligned.
                float stagger = row < 3 ? MathF.Sin(i * 2.31f + row * 1.7f) * .18f : 0;
                if (row < 3 && i != 0 && i != 7) {
                    x += MathF.Sin(i * 1.73f + row * 2.41f) * .12f;
                    y = Math.Min(4.3f, y + MathF.Sin(i * 2.07f + row) * .12f);
                }
                rings[row][i] = new(x, y, depth[row] + stagger);
            }
        }
        var s = new SurfaceTool(); s.Begin(Godot.Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int tint) {
            var normal = (c - a).Cross(b - a).Normalized();
            if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
            var ore = new Color(palette[Math.Abs(tint) % palette.Length]);
            void Vertex(Vector3 point) {
                var at = WorldPoint(point.X, point.Z);
                float blend = Math.Clamp((-point.Z - 3.4f) / (-root - 3.4f), 0, 1);
                blend = blend * blend * (3 - 2 * blend);
                var hillside = MountainShape.ColorAt(at, normal, MountainShape.Surface(at));
                s.SetColor(ore.Lerp(hillside, blend)); s.SetNormal(normal); s.AddVertex(point);
            }
            Vertex(a); Vertex(b); Vertex(c);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int tint) {
            var middle = (a + b + c + d) / 4 + outward.Normalized() * .055f;
            Triangle(a, b, middle, outward, tint); Triangle(b, c, middle, outward, tint);
            Triangle(c, d, middle, outward, tint); Triangle(d, a, middle, outward, tint);
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
        // Small, subdued talus stones sit on the sampled mountain triangles and soften the spur's lower seam.
        // They stay outside the aperture and are part of this cached mesh, so they add no scene nodes or collisions.
        void TalusTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color color) {
            var normal = (c - a).Cross(b - a).Normalized();
            if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
            s.SetColor(color); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        }
        Color[] talus = { new("62614f"), new("696653"), new("58584b") };
        Span<Vector3> baseRing = stackalloc Vector3[5];
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 6; i++) {
                float x = side * (2.95f + i * .34f), z = -2.45f - i * .48f + (i % 2) * .22f;
                float radius = .34f + (i % 3) * .07f, peak = .24f + (i % 2) * .11f;
                Vector3 Ground(float px, float pz) => new(px, MountainShape.Surface(WorldPoint(px, pz)) + .025f, pz);
                var center = Ground(x, z); var tip = center + Vector3.Up * peak;
                if (tip.Y > 4.3f) continue;
                for (int j = 0; j < baseRing.Length; j++) {
                    float angle = j * Mathf.Tau / baseRing.Length + i * .73f + side * .19f;
                    float scale = radius * (j % 2 == 0 ? 1 : .76f);
                    baseRing[j] = Ground(x + MathF.Cos(angle) * scale, z + MathF.Sin(angle) * scale);
                }
                if (baseRing[0].Y > 4.3f || baseRing[1].Y > 4.3f || baseRing[2].Y > 4.3f || baseRing[3].Y > 4.3f || baseRing[4].Y > 4.3f) continue;
                var shade = talus[(i + (side > 0 ? 1 : 0)) % talus.Length];
                for (int j = 0; j < baseRing.Length; j++) {
                    var a = baseRing[j]; var b = baseRing[(j + 1) % baseRing.Length];
                    TalusTriangle(a, b, tip, new Vector3(a.X + b.X - 2 * x, .8f, a.Z + b.Z - 2 * z), shade);
                }
            }
        return meshes[cliff] = s.Commit();
    }
}
