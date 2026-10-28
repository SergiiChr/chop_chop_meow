using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Shared nature meshes: stumps, decorative shrubs, fruit, faceted boulders and resource lumps.
/// Each variant is built once from a fixed seed and colored by vertex. Standing trees and berry bushes use baked Tree3D meshes.
/// </summary>
internal static class NatureShape {
    internal readonly record struct Step(float Y, float Radius, Color Color);

    public const int Variants = 3;
    public static readonly StandardMaterial3D Natural = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .92f };
    public static readonly StandardMaterial3D Glossy = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .4f };
    private static readonly Dictionary<string, ArrayMesh> meshes = new();

    private static readonly Dictionary<(string Color, float Roughness, float Metallic), StandardMaterial3D> oreMaterials = new();

    /// <summary>Cached ore palettes tint shared neutral geometry without copying meshes between resource kinds.</summary>
    public static StandardMaterial3D OreMaterial(string color, float roughness, float metallic) {
        var key = (color, roughness, metallic);
        if (!oreMaterials.TryGetValue(key, out var material))
            oreMaterials[key] = material = new StandardMaterial3D {
                AlbedoColor = new Color(color),
                VertexColorUseAsAlbedo = true,
                VertexColorIsSrgb = true,
                Roughness = roughness,
                Metallic = metallic
            };
        return material;
    }

    private static Mesh Cached(string key, Action<SurfaceTool> build) {
        if (meshes.TryGetValue(key, out var mesh)) return mesh;
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        build(surface);
        return meshes[key] = surface.Commit();
    }

    /// <summary>Unit icosphere positions and triangles; detail 0 is the bare 20-face icosahedron.</summary>
    private static (List<Vector3> Points, List<int> Triangles) Icosphere(int detail) {
        float t = (1 + MathF.Sqrt(5)) / 2;
        var points = new List<Vector3> {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < points.Count; i++) points[i] = points[i].Normalized();
        var triangles = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        for (int level = 0; level < detail; level++) {
            var middles = new Dictionary<long, int>();
            int Middle(int a, int b) {
                long key = Math.Min(a, b) * 100000L + Math.Max(a, b);
                if (middles.TryGetValue(key, out int index)) return index;
                points.Add((points[a] + points[b]).Normalized());
                return middles[key] = points.Count - 1;
            }
            var next = new List<int>();
            for (int i = 0; i < triangles.Count; i += 3) {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            triangles = next;
        }
        return (points, triangles);
    }

    /// <summary>Stable value in [-1, 1] for a point, so lumps and color mottling repeat per variant.</summary>
    private static float Noise(Vector3 p, int seed) {
        uint h = (uint)MathF.Round(p.X * 977) * 73856093u ^ (uint)MathF.Round(p.Y * 977) * 19349663u ^ (uint)MathF.Round(p.Z * 977) * 83492791u ^ (uint)seed * 2654435761u;
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xffff) / 32768f - 1;
    }

    /// <summary>
    /// A closed lumpy ellipsoid: broad bulges from <paramref name="lumps"/> random directions plus fine jitter.
    /// Points below <paramref name="floor"/> are pressed flat so the blob rests on, or hangs from, a level base.
    /// </summary>
    private static (Vector3[] Points, List<int> Triangles) Shape(Vector3 center, Vector3 radii, int detail, int seed, int lumps, float bulge, float jitter,
        float floor, int cuts) {
        var (points, triangles) = Icosphere(detail);
        // Bulges spread evenly over the upper sphere, then jittered, so a crown is clustered all round instead of lopsided.
        var random = new Random(seed);
        var bumps = new (Vector3 Direction, float Height)[lumps];
        for (int i = 0; i < lumps; i++) {
            float y = 1 - 1.6f * (i + .5f) / lumps, ring = MathF.Sqrt(1 - y * y), angle = i * 2.39996f + (float)random.NextDouble();
            var direction = new Vector3(MathF.Cos(angle) * ring, y, MathF.Sin(angle) * ring) + new Vector3((float)random.NextDouble() - .5f, 0, (float)random.NextDouble() - .5f) * .3f;
            bumps[i] = (direction.Normalized(), bulge * (.7f + (float)random.NextDouble() * .3f));
        }
        // Cutting planes shave broad flat facets, like split or weathered stone.
        var planes = new (Vector3 Normal, float Depth)[cuts];
        for (int i = 0; i < cuts; i++)
            planes[i] = (new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 1.4f - .4f, (float)random.NextDouble() * 2 - 1).Normalized(),
                .62f + (float)random.NextDouble() * .2f);
        var shaped = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++) {
            var unit = points[i];
            float radius = 1 - bulge * .25f + Noise(unit, seed) * jitter;
            foreach (var (direction, height) in bumps) radius += height * MathF.Pow(Math.Max(0, unit.Dot(direction)), 6);
            var local = unit * radius;
            foreach (var (normal, depth) in planes) local -= normal * Math.Max(0, local.Dot(normal) - depth);
            var p = center + local * radii;
            if (p.Y < floor) p.Y = floor + (p.Y - floor) * .12f;
            shaped[i] = p;
        }
        return (shaped, triangles);
    }

    /// <summary>Emits a <see cref="Shape"/>: smooth blobs share normals across faces; faceted ones keep a flat normal per face, like split stone.</summary>
    private static void Blob(SurfaceTool surface, Vector3 center, Vector3 radii, int detail, int seed, int lumps, float bulge, float jitter,
        float floor, bool faceted, Func<Vector3, Vector3, Color> tint) {
        var (shaped, triangles) = Shape(center, radii, detail, seed, lumps, bulge, jitter, floor, 0);
        Blob(surface, center, shaped, triangles, faceted, tint);
    }

    private static void Blob(SurfaceTool surface, Vector3 center, Vector3[] shaped, List<int> triangles,
        bool faceted, Func<Vector3, Vector3, Color> tint) {
        if (faceted) { Faces(surface, shaped, triangles, center, tint); return; }
        var normals = new Vector3[shaped.Length];
        for (int i = 0; i < triangles.Count; i += 3) {
            var face = (shaped[triangles[i + 2]] - shaped[triangles[i]]).Cross(shaped[triangles[i + 1]] - shaped[triangles[i]]);
            if (face.Dot(shaped[triangles[i]] - center) < 0) face = -face;
            for (int k = 0; k < 3; k++) normals[triangles[i + k]] += face;
        }
        for (int i = 0; i < triangles.Count; i += 3) {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if ((shaped[c] - shaped[a]).Cross(shaped[b] - shaped[a]).Dot(shaped[a] - center) < 0) (b, c) = (c, b);
            foreach (int k in new[] { a, b, c }) {
                var normal = normals[k].Normalized();
                surface.SetNormal(normal); surface.SetColor(tint(shaped[k], normal)); surface.AddVertex(shaped[k]);
            }
        }
    }

    /// <summary>Flat-shaded faces of a shape, optionally only those whose middle passes <paramref name="keep"/>.</summary>
    private static void Faces(SurfaceTool surface, Vector3[] shaped, List<int> triangles, Vector3 center, Func<Vector3, Vector3, Color> tint,
        Func<Vector3, bool>? keep = null) {
        for (int i = 0; i < triangles.Count; i += 3) {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            var face = (shaped[c] - shaped[a]).Cross(shaped[b] - shaped[a]).Normalized();
            var middle = (shaped[a] + shaped[b] + shaped[c]) / 3;
            if (face.Dot(middle - center) < 0) { (b, c) = (c, b); face = -face; }
            if (keep != null && !keep(middle)) continue;
            var color = tint(middle, face);
            foreach (int k in new[] { a, b, c }) {
                surface.SetNormal(face); surface.SetColor(color); surface.AddVertex(shaped[k]);
            }
        }
    }

    /// <summary>
    /// A wobbly surface of revolution placed by <paramref name="at"/>; the profile may fold back down to make skirts.
    /// Normals are smooth around the axis but break between steps, so each tier and flare reads as its own facet band.
    /// </summary>
    private static void Lathe(SurfaceTool surface, Transform3D at, Step[] profile, int sides, int seed = 0, float wobble = 0,
        Func<int, int, Color?>? mark = null) {
        Vector3 Point(int row, int side) {
            float angle = Mathf.Tau * side / sides;
            float radius = profile[row].Radius * (1 + wobble * Noise(new Vector3(row, side % sides, 0), seed));
            return new Vector3(MathF.Cos(angle) * radius, profile[row].Y, MathF.Sin(angle) * radius);
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc, Vector3 outward) {
            if ((c - a).Cross(b - a).Dot(outward) < 0) { (b, c) = (c, b); (nb, nc) = (nc, nb); (cb, cc) = (cc, cb); }
            foreach (var (p, n, color) in new[] { (a, na, ca), (b, nb, cb), (c, nc, cc) }) {
                surface.SetNormal((at.Basis * n).Normalized()); surface.SetColor(color); surface.AddVertex(at * p);
            }
        }
        for (int row = 0; row < profile.Length - 1; row++)
            for (int side = 0; side < sides; side++) {
                var a = Point(row, side); var b = Point(row, side + 1); var c = Point(row + 1, side + 1); var d = Point(row + 1, side);
                // Around tangent crossed with the step's slope; the step's own slope decides which way is out.
                var along = (c + d - a - b) / 2;
                if (along.LengthSquared() < 1e-8f) continue;
                Vector3 Normal(int r, int s) {
                    var around = Point(r, s + 1) - Point(r, s - 1 + sides);
                    var radial = Point(r, s) with { Y = 0 };
                    var normal = around.Cross(along).Normalized();
                    var slope = radial.Normalized() * along.Y - Vector3.Up * (along with { Y = 0 }).Dot(radial.Normalized());
                    return normal.Dot(slope) < 0 ? -normal : normal;
                }
                var outward = ((a + b) / 2) with { Y = 0 };
                outward = outward.Normalized() * along.Y - Vector3.Up * (along with { Y = 0 }).Dot(outward.Normalized());
                Color low = mark?.Invoke(row, side) ?? profile[row].Color, high = mark?.Invoke(row, side) ?? profile[row + 1].Color;
                Vector3 na = Normal(row, side), nb = Normal(row, side + 1), nc = Normal(row + 1, side + 1), nd = Normal(row + 1, side);
                Triangle(a, b, c, na, nb, nc, low, low, high, outward);
                Triangle(a, c, d, na, nc, nd, low, high, high, outward);
            }
        for (int end = 0; end < 2; end++) {
            int row = end == 0 ? 0 : profile.Length - 1;
            if (profile[row].Radius <= 0) continue;
            var normal = end == 0 ? Vector3.Down : Vector3.Up;
            var hub = new Vector3(0, profile[row].Y, 0);
            var color = profile[row].Color;
            for (int side = 0; side < sides; side++)
                Triangle(hub, Point(row, side), Point(row, side + 1), normal, normal, normal, color, color, color, normal);
        }
    }

    /// <summary>Darker underneath and toward the core, lighter on sunlit tops, with a little mottling.</summary>
    private static Func<Vector3, Vector3, Color> Leafy(Color shade, Color mid, Color light, int seed) => (p, normal) => {
        float lit = Math.Clamp(normal.Y * .6f + .45f, 0, 1);
        var color = lit < .5f ? shade.Lerp(mid, lit * 2) : mid.Lerp(light, (lit - .5f) * 2);
        return color.Lightened(Noise(p * 3, seed) * .05f);
    };

    /// <summary>A felled tree's stump: bark sides with root flare and a pale cut face.</summary>
    public static Mesh Stump => Cached("stump", s => {
        Color bark = new("6b4a32"), wood = new("c9a477");
        Lathe(s, Transform3D.Identity, new Step[] { new(0, .36f, new Color("4f3624")), new(.1f, .28f, bark), new(.32f, .26f, wood) }, 9, 5, .06f);
    });

    /// <summary>The same folded leaf construction is used for resource bushes and lightweight scenery lobes.</summary>
    private static void BushLeaves(SurfaceTool s, Vector3 at, Vector3 radii, Vector3[] points,
        Func<Vector3, Vector3, Color> tint, float phase, int count = 8, float size = 1) {
        for (int leaf = 0; leaf < count; leaf++) {
            float angle = leaf * 2.39996f + phase;
            var outward = new Vector3(MathF.Cos(angle), .35f + (leaf % 3) * .3f, MathF.Sin(angle)).Normalized();
            var anchor = points[0]; float best = float.NegativeInfinity;
            foreach (var point in points) {
                float score = ((point - at) / radii).Normalized().Dot(outward);
                if (score > best) { best = score; anchor = point; }
            }
            var leafNormal = ((anchor - at) / (radii * radii)).Normalized();
            var along = (Vector3.Up - leafNormal * leafNormal.Y).Normalized();
            along = along.Rotated(leafNormal, angle * .7f);
            var side = leafNormal.Cross(along).Normalized();
            var basePoint = anchor + leafNormal * (.002f * size);
            var tip = anchor + along * (.18f * size) + leafNormal * (.025f * size);
            var middle = basePoint.Lerp(tip, .45f) - leafNormal * (.012f * size);
            var left = middle + side * (.06f * size); var right = middle - side * (.06f * size);
            void LeafFace(Vector3 a, Vector3 b, Vector3 c) {
                var normal = (c - a).Cross(b - a).Normalized();
                if (normal.Dot(leafNormal) < 0) { (b, c) = (c, b); normal = -normal; }
                var color = tint(anchor, normal).Lightened((leaf % 3) * .035f);
                s.SetNormal(normal); s.SetColor(color); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
                s.SetNormal(-normal); s.SetColor(color.Darkened(.15f)); s.AddVertex(a); s.AddVertex(c); s.AddVertex(b);
            }
            LeafFace(basePoint, left, tip); LeafFace(basePoint, tip, right);
        }
    }

    /// <summary>A lightweight neutral shrub lobe tinted by the scenery MultiMesh's existing per-instance colors.</summary>
    public static Mesh Scrub => Cached("scrub", s => {
        var tint = Leafy(new Color("a0a0a0"), new Color("d5d5d5"), Colors.White, 391);
        var (points, triangles) = Shape(Vector3.Zero, Vector3.One, 0, 391, 4, .18f, .04f, -.46f, 0);
        Blob(s, Vector3.Zero, points, triangles, false, tint);
        BushLeaves(s, Vector3.Zero, Vector3.One, points, tint, .4f, 7, 2.1f);
    });

    /// <summary>Fruit clusters attached to the stems at the centers of actual Tree3D foliage cards, rather than their transparent edges.</summary>
    public static Mesh CardBerries(Mesh foliage, int variant) => Cached($"cardberries{variant}", s => {
        var anchors = new List<Vector3>();
        // The pinned Tree3D baker preserves its eight-vertex, two-sided card layout.
        // UV (0.5, 0.5) meets the central twig in our broadleaf cutout.
        for (int surface = 0; surface < foliage.GetSurfaceCount(); surface++) {
            var vertices = foliage.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            for (int i = 0; i + 7 < vertices.Length; i += 8) {
                var center = (vertices[i] + vertices[i + 1] + vertices[i + 2] + vertices[i + 3]) * .25f;
                if (center.Y > .2f) anchors.Add(center);
            }
        }
        Color[] reds = { new("ce3543"), new("a8263c"), new("e14b50") };
        for (int i = 0; i < 16 && anchors.Count > 0; i++) {
            float angle = i * 2.39996f + variant, y = .7f - (i % 5) * .25f;
            var direction = new Vector3(MathF.Cos(angle), y, MathF.Sin(angle)).Normalized();
            int best = 0; float score = float.NegativeInfinity;
            for (int candidate = 0; candidate < anchors.Count; candidate++) {
                float reach = (anchors[candidate] - Vector3.Up * .4f).Dot(direction);
                if (reach > score) { score = reach; best = candidate; }
            }
            var at = anchors[best];
            // Spacing prevents several clusters piling onto the same exposed twig.
            for (int candidate = anchors.Count - 1; candidate >= 0; candidate--)
                if (anchors[candidate].DistanceSquaredTo(at) < .012f) anchors.RemoveAt(candidate);
            for (int berry = 0; berry < 3; berry++) {
                var offset = new Vector3(MathF.Cos(berry * Mathf.Tau / 3) * .025f, -.012f * berry, MathF.Sin(berry * Mathf.Tau / 3) * .025f);
                var color = reds[(i + berry) % reds.Length];
                Blob(s, at + offset, Vector3.One * .032f, 0, 0, 0, 0, 0, -1, false, (_, _) => color);
            }
        }
    });

    /// <summary>A small heap of the bush's berries, for the berries icon, rounder than on the bush since they are seen up close: a ring of seven around one with three more on top.</summary>
    public static Mesh BerryHeap => Cached("berry_heap", s => {
        Color[] reds = { new("c0283a"), new("a3202f"), new("d23a45") };
        var berries = new List<Vector3> { new(0, .05f, 0) };
        for (int i = 0; i < 7; i++) berries.Add(new(MathF.Cos(i * Mathf.Tau / 7) * .1f, .05f, MathF.Sin(i * Mathf.Tau / 7) * .1f));
        for (int i = 0; i < 3; i++) berries.Add(new(MathF.Cos(i * Mathf.Tau / 3 + .5f) * .055f, .13f, MathF.Sin(i * Mathf.Tau / 3 + .5f) * .055f));
        for (int i = 0; i < berries.Count; i++) {
            var color = reds[i % reds.Length];
            Blob(s, berries[i], Vector3.One * .05f, 1, 0, 0, 0, 0, -1, false, (_, _) => color);
        }
    });

    private static (Vector3 At, Vector3 Radii, int Seed)[] Boulders(int variant) {
        var random = new Random(501 + variant);
        float R(float spread) => ((float)random.NextDouble() * 2 - 1) * spread;
        var stones = new List<(Vector3, Vector3, int)> { (new(R(.05f), .28f, R(.05f)), new(.62f + R(.05f), .58f, .55f + R(.05f)), 511 + variant) };
        float turn = R(3);
        for (int i = 0; i < 3; i++) {
            float angle = turn + i * 2.1f + R(.3f), radius = .32f - i * .03f + R(.03f), reach = .5f + radius * .3f;
            stones.Add((new(MathF.Cos(angle) * reach, radius * .3f, MathF.Sin(angle) * reach), new(radius * 1.15f, radius, radius), 521 + variant * 5 + i));
        }
        return stones.ToArray();
    }

    private static (Vector3[] Points, List<int> Triangles) Boulder(Vector3 at, Vector3 radii, int seed) => Shape(at, radii, 1, seed, 3, .2f, .06f, 0, 11);

    /// <summary>A faceted stone group pressed into the ground, with lighter tops and per-face color variation.</summary>
    public static Mesh Rocks(string kind, Color color, int variant) => Cached($"rocks{kind}{variant}", s => {
        foreach (var (at, radii, seed) in Boulders(variant)) {
            var (points, triangles) = Boulder(at, radii, seed);
            Faces(s, points, triangles, at, (p, normal) => {
                var shade = normal.Y > 0 ? color.Lightened(normal.Y * .12f) : color.Darkened(-normal.Y * .3f);
                return shade.Lightened(Noise(p * 5, seed) * .03f);
            });
        }
    });

    /// <summary>Neutral host rock or seam faces shared across all ore palettes for one seeded variant.</summary>
    public static Mesh Ore(int variant, bool seam) => Cached($"ore{variant}{seam}", s => {
        foreach (var (at, radii, seed) in Boulders(variant)) {
            var (points, triangles) = Boulder(at, radii, seed);
            OreFaces(s, points, triangles, at, seam, seed);
        }
    });

    /// <summary>Split existing boulder faces at continuous bedding planes, so ore seams wrap flush around the rock.</summary>
    private static void OreFaces(SurfaceTool surface, Vector3[] points, List<int> triangles, Vector3 center, bool seam, int seed) {
        float Bed(Vector3 p) => p.Y + p.X * .16f - p.Z * .08f;
        List<Vector3> Clip(List<Vector3> polygon, float plane, bool above) {
            var result = new List<Vector3>();
            for (int i = 0; i < polygon.Count; i++) {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                float da = Bed(a) - plane, db = Bed(b) - plane;
                bool insideA = above ? da >= 0 : da <= 0, insideB = above ? db >= 0 : db <= 0;
                if (insideA) result.Add(a);
                if (insideA != insideB) result.Add(a.Lerp(b, da / (da - db)));
            }
            return result;
        }
        var bands = seam ? new[] { (.08f, .24f), (.34f, .53f) } : new[] { (-2f, .08f), (.24f, .34f), (.53f, 2f) };
        for (int i = 0; i < triangles.Count; i += 3) {
            var a = points[triangles[i]]; var b = points[triangles[i + 1]]; var c = points[triangles[i + 2]];
            foreach (var (low, high) in bands) {
                var polygon = Clip(Clip(new List<Vector3> { a, b, c }, low, true), high, false);
                for (int corner = 1; corner < polygon.Count - 1; corner++) {
                    var p = polygon[0]; var q = polygon[corner]; var r = polygon[corner + 1];
                    var face = (r - p).Cross(q - p);
                    if (face.LengthSquared() < 1e-12f) continue;
                    face = face.Normalized();
                    if (face.Dot((p + q + r) / 3 - center) < 0) { (q, r) = (r, q); face = -face; }
                    surface.SetNormal(face);
                    var shade = Colors.White.Darkened(.04f + Math.Max(0, -face.Y) * .3f);
                    surface.SetColor(shade.Lightened(Noise((p + q + r) * 2, seed) * .025f));
                    surface.AddVertex(p); surface.AddVertex(q); surface.AddVertex(r);
                }
            }
        }
    }

    /// <summary>
    /// Angular ore chunks set into the boulder group, half buried where a ray from inside leaves the outermost rock face.
    /// Rays are cast against the real faces rather than the boulder ellipsoids, because the cutting planes shave the rock inside them.
    /// </summary>
    public static Mesh OreChips(int variant) => Cached($"orechips{variant}", s => {
        var faces = new List<(Vector3, Vector3, Vector3)>();
        foreach (var (at, radii, seed) in Boulders(variant)) {
            var (points, triangles) = Boulder(at, radii, seed);
            for (int i = 0; i < triangles.Count; i += 3) faces.Add((points[triangles[i]], points[triangles[i + 1]], points[triangles[i + 2]]));
        }
        var random = new Random(601 + variant);
        var origin = new Vector3(0, .2f, 0);
        const int count = 7;
        for (int i = 0; i < count; i++) {
            float y = .75f - .9f * i / count, ring = MathF.Sqrt(1 - y * y), angle = i * 2.39996f + (float)random.NextDouble() * .5f;
            var direction = new Vector3(MathF.Cos(angle) * ring, y, MathF.Sin(angle) * ring);
            float reach = 0;
            foreach (var (a, b, c) in faces) if (Hit(origin, direction, a, b, c) is float t) reach = Math.Max(reach, t);
            if (reach <= 0) continue;
            var basis = Basis.FromEuler(new Vector3((float)random.NextDouble() * 1.2f - .6f, (float)random.NextDouble() * Mathf.Tau, (float)random.NextDouble() * 1.2f - .6f));
            var size = new Vector3(.13f, .1f, .12f) * (.85f + (float)random.NextDouble() * .4f);
            var transform = new Transform3D(basis, origin + direction * (reach - .01f));
            var (points, triangles) = Shape(Vector3.Zero, size * .7f, 0, 631 + variant * 7 + i, 0, 0, .1f, -1, 3);
            for (int point = 0; point < points.Length; point++) points[point] = transform * points[point];
            Faces(s, points, triangles, transform.Origin, (_, normal) => Colors.White.Darkened(.025f + Math.Max(0, -normal.Y) * .2f));
        }
    });

    /// <summary>Distance along a ray to a triangle, or null when it misses (Möller–Trumbore).</summary>
    private static float? Hit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c) {
        Vector3 ab = b - a, ac = c - a, p = direction.Cross(ac);
        float det = ab.Dot(p);
        if (MathF.Abs(det) < 1e-7f) return null;
        var offset = origin - a;
        float u = offset.Dot(p) / det;
        if (u < 0 || u > 1) return null;
        var q = offset.Cross(ab);
        float v = direction.Dot(q) / det, t = ac.Dot(q) / det;
        return v < 0 || u + v > 1 || t <= 0 ? null : t;
    }

    /// <summary>A few loose lumps lying on the ground, each a faceted nugget seated flat.</summary>
    public static Mesh Loose(string kind, Color light, Color dark, int variant) => Cached($"loose{kind}{variant}", s => {
        var random = new Random(701 + variant);
        for (int i = 0; i < 5; i++) {
            float angle = i * 1.4f + (float)random.NextDouble() * .5f, reach = i == 0 ? 0 : .24f + (float)random.NextDouble() * .06f;
            float size = i == 0 ? .18f : .13f + (float)random.NextDouble() * .03f;
            var color = i % 2 == 0 ? light : dark;
            Blob(s, new Vector3(MathF.Cos(angle) * reach, size * .45f, MathF.Sin(angle) * reach), new Vector3(1.15f, .75f, 1) * size,
                0, 711 + variant * 7 + i, 2, .3f, .1f, 0, true, (_, normal) => color.Lightened(normal.Y * .1f).Darkened(Math.Max(0, -normal.Y) * .3f));
        }
    });
}
