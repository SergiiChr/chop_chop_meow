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
        var spot = World.MineSpot(cliff); var outwards = cliff.Face; var across = new Vector2(outwards.Y, -outwards.X);
        Vector2 WorldPoint(float x, float z) => spot + across * x + outwards * z;
        float root = -7;
        while (root > -32 && (Terrain.FromMountains(WorldPoint(-4.6f, root)) > -6 || Terrain.FromMountains(WorldPoint(0, root)) > -6 || Terrain.FromMountains(WorldPoint(4.6f, root)) > -6)) root -= 1;
        float[] depth = { WorldModels.MineDoor - .08f, -4.2f, -6, root };
        float[] width = { 2.55f, 2.8f, 3.35f, 4.6f };
        float[] crown = { 3.05f, 3.6f, 4.3f, 0 };
        // More angular stations break up the long straight shoulders while retaining a broad, clear adit.
        Vector2[] section = {
            new(-1, 0), new(-.99f, .2f), new(-.96f, .42f), new(-.88f, .63f), new(-.74f, .82f),
            new(-.56f, .93f), new(-.34f, .98f), new(-.1f, 1), new(.15f, .98f), new(.39f, .96f),
            new(.61f, .89f), new(.79f, .76f), new(.91f, .57f), new(.98f, .32f), new(1, 0)
        };
        var rings = new Vector3[4][];
        for (int row = 0; row < rings.Length; row++) {
            rings[row] = new Vector3[section.Length];
            for (int i = 0; i < section.Length; i++) {
                float edge = i == 0 || i == section.Length - 1 ? 0 : Hash(cliff, row, i);
                float x = section[i].X * width[row] + edge * (row == 0 ? .08f : .22f);
                float ground = MountainShape.Surface(WorldPoint(x, depth[row]));
                float y = row == 3 ? Math.Min(section[i].Y * 4.3f, ground - .8f) : i == 0 || i == section.Length - 1 ? -.12f : section[i].Y * crown[row] + edge * (row == 0 ? .08f : .34f);
                y = Math.Min(4.3f, y);
                float z = depth[row] + (row == 3 ? 0 : edge * (row == 0 ? .22f : .3f));
                rings[row][i] = new(x, y, z);
            }
        }
        var s = new SurfaceTool(); s.Begin(Godot.Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int tint, float blend) {
            var normal = (c - a).Cross(b - a).Normalized();
            if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
            var ore = new Color(palette[Math.Abs(tint) % palette.Length]);
            var host = tint % 3 == 0 ? new Color("59604c") : new Color("55524c");
            s.SetNormal(normal);
            foreach (var vertex in new[] { a, b, c }) {
                float shoulder = Math.Max(Math.Abs(vertex.X) - .72f, vertex.Y - 1.76f);
                float edgeBlend = Math.Clamp(shoulder / 1.5f, 0, 1) * .78f;
                s.SetColor(ore.Lerp(host, Math.Max(blend, edgeBlend)));
                s.AddVertex(vertex);
            }
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int tint, float blend) {
            Triangle(a, b, c, outward, tint, blend); Triangle(a, c, d, outward, tint, blend);
        }
        void AddAngularRock(Vector3 center, float rx, float ry, float rz, int seed, int tint, float blend) {
            const int sides = 8;
            var ring = new Vector3[sides];
            float phase = Hash(cliff, 9, seed) * .32f;
            for (int i = 0; i < sides; i++) {
                float angle = phase + i * MathF.Tau / sides;
                float wobble = 1 + Hash(cliff, seed, i) * .16f;
                ring[i] = center + new Vector3(MathF.Cos(angle) * rx * wobble, Hash(cliff, seed + 1, i) * ry * .18f, MathF.Sin(angle) * rz * wobble);
            }
            var top = center + new Vector3(Hash(cliff, seed + 2, 0) * rx * .18f, ry, Hash(cliff, seed + 2, 1) * rz * .18f);
            var bottom = center - new Vector3(Hash(cliff, seed + 3, 0) * rx * .15f, ry * .8f, Hash(cliff, seed + 3, 1) * rz * .15f);
            for (int i = 0; i < sides; i++) {
                int next = (i + 1) % sides;
                Triangle(top, ring[i], ring[next], Vector3.Up, tint + i, blend);
                Triangle(ring[i], bottom, ring[next], Vector3.Down, tint + i, blend);
            }
        }
        // Open-backed skin: the whole rear edge is buried below the same mountain triangles it joins.
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < section.Length; i++) {
                int next = (i + 1) % section.Length;
                var outward = new Vector3(section[i].X + section[next].X, i == section.Length - 1 ? -1 : .8f, 0);
                float blend = row switch { 0 => .08f, 1 => .34f, _ => .78f };
                Quad(rings[row][i], rings[row][next], rings[row + 1][next], rings[row + 1][i], outward, row + i / 2, blend);
            }
        // The mouth is a deliberate aperture; nothing is drawn across the tunnel opening.
        Vector3[] corners = { new(-.72f, .02f, depth[0]), new(-.72f, .9f, depth[0]), new(-.72f, 1.66f, depth[0]), new(-.4f, 1.76f, depth[0]), new(.4f, 1.76f, depth[0]), new(.72f, 1.66f, depth[0]), new(.72f, .9f, depth[0]), new(.72f, .02f, depth[0]) };
        var mouth = new Vector3[15];
        for (int i = 0; i < corners.Length - 1; i++) {
            mouth[i * 2] = corners[i]; mouth[i * 2 + 1] = corners[i].Lerp(corners[i + 1], .5f);
        }
        mouth[^1] = corners[^1];
        for (int i = 0; i < mouth.Length; i++) {
            int next = (i + 1) % mouth.Length;
            Quad(rings[0][i], rings[0][next], mouth[next], mouth[i], Vector3.Back, i / 2, .03f);
        }

        // Faceted outcrops cross the side and crown seams; their lower halves sink into the host skin.
        AddAngularRock(new(-2.4f, .75f, -2.45f), .65f, .82f, .48f, 21, 1, .85f);
        AddAngularRock(new(2.42f, .82f, -2.48f), .62f, .9f, .5f, 22, 2, .85f);
        AddAngularRock(new(-2.15f, 1.92f, -2.46f), .62f, .7f, .48f, 23, 3, .82f);
        AddAngularRock(new(2.12f, 1.95f, -2.5f), .65f, .72f, .5f, 24, 4, .85f);
        AddAngularRock(new(-1.12f, 2.78f, -2.5f), .72f, .48f, .48f, 25, 5, .85f);
        AddAngularRock(new(1.06f, 2.7f, -2.46f), .68f, .48f, .5f, 26, 6, .85f);
        AddAngularRock(new(-2.65f, .68f, -4.25f), .78f, .72f, .52f, 1, 1, .72f);
        AddAngularRock(new(2.72f, .82f, -4.55f), .86f, .7f, .64f, 2, 2, .76f);
        AddAngularRock(new(-3.05f, .45f, -5.75f), .72f, .58f, .55f, 3, 3, .82f);
        AddAngularRock(new(3.15f, .52f, -5.95f), .8f, .58f, .62f, 4, 4, .84f);
        AddAngularRock(new(-.92f, 3.18f, -4.45f), .74f, .48f, .56f, 5, 5, .82f);
        AddAngularRock(new(1.02f, 3.16f, -4.7f), .68f, .42f, .62f, 6, 6, .84f);

        // Sparse talus sits at the exposed feet, clear of the central rails and the buried rear seam.
        for (int i = 0; i < 7; i++) {
            float side = (i % 2 == 0 ? -1 : 1), x = side * (3.1f + (i % 3) * .58f);
            float z = depth[0] + .35f + (i / 2) * .28f;
            float rx = .22f + Math.Abs(Hash(cliff, 7, i)) * .2f, ry = .2f + Math.Abs(Hash(cliff, 8, i)) * .16f;
            float y = MountainShape.Surface(WorldPoint(x, z)) - ry * .28f;
            if (y + ry > 4.3f) continue;
            AddAngularRock(new(x, y, z), rx, ry, rx * .78f, 10 + i, 10 + i, .9f);
        }
        return meshes[cliff] = s.Commit();
    }

    private static float Hash(Cliff cliff, int row, int point) {
        int cliffSeed = (int)MathF.Round(cliff.Center.X * 31 + cliff.Center.Y * 67 + cliff.Face.X * 101 + cliff.Face.Y * 137);
        uint value = unchecked((uint)cliffSeed * 0x9e3779b9u + (uint)(row * 97 + point * 7919));
        value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
        return ((value & 0xffff) / 32767.5f) - 1;
    }
}
