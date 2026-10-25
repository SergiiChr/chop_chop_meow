using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Shared nature meshes: lumpy leaf crowns, lathed trunks and tiers, and faceted boulders and lumps pressed into the ground.
/// Each variant is built once from a fixed seed and colored by vertex, so a forest shares a handful of meshes and one material.
/// </summary>
internal static class NatureShape
{
    internal readonly record struct Step(float Y, float Radius, Color Color);

    public const int Variants = 3;
    public static readonly StandardMaterial3D Natural = new() { VertexColorUseAsAlbedo = true, Roughness = .92f };
    public static readonly StandardMaterial3D Glossy = new() { VertexColorUseAsAlbedo = true, Roughness = .4f };
    public static readonly StandardMaterial3D Mineral = new() { VertexColorUseAsAlbedo = true, Roughness = .4f, Metallic = .3f };
    private static readonly Dictionary<string, ArrayMesh> meshes = new();

    private static Mesh Cached(string key, Action<SurfaceTool> build)
    {
        if (meshes.TryGetValue(key, out var mesh)) return mesh;
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        build(surface);
        return meshes[key] = surface.Commit();
    }

    /// <summary>Unit icosphere positions and triangles; detail 0 is the bare 20-face icosahedron.</summary>
    private static (List<Vector3> Points, List<int> Triangles) Icosphere(int detail)
    {
        float t = (1 + MathF.Sqrt(5)) / 2;
        var points = new List<Vector3> {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < points.Count; i++) points[i] = points[i].Normalized();
        var triangles = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        for (int level = 0; level < detail; level++)
        {
            var middles = new Dictionary<long, int>();
            int Middle(int a, int b)
            {
                long key = Math.Min(a, b) * 100000L + Math.Max(a, b);
                if (middles.TryGetValue(key, out int index)) return index;
                points.Add((points[a] + points[b]).Normalized());
                return middles[key] = points.Count - 1;
            }
            var next = new List<int>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            triangles = next;
        }
        return (points, triangles);
    }

    /// <summary>Stable value in [-1, 1] for a point, so lumps and color mottling repeat per variant.</summary>
    private static float Noise(Vector3 p, int seed)
    {
        uint h = (uint)MathF.Round(p.X * 977) * 73856093u ^ (uint)MathF.Round(p.Y * 977) * 19349663u ^ (uint)MathF.Round(p.Z * 977) * 83492791u ^ (uint)seed * 2654435761u;
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xffff) / 32768f - 1;
    }

    /// <summary>
    /// A closed lumpy ellipsoid: broad bulges from <paramref name="lumps"/> random directions plus fine jitter.
    /// Points below <paramref name="floor"/> are pressed flat so the blob rests on, or hangs from, a level base.
    /// </summary>
    private static (Vector3[] Points, List<int> Triangles) Shape(Vector3 center, Vector3 radii, int detail, int seed, int lumps, float bulge, float jitter,
        float floor, int cuts)
    {
        var (points, triangles) = Icosphere(detail);
        // Bulges spread evenly over the upper sphere, then jittered, so a crown is clustered all round instead of lopsided.
        var random = new Random(seed);
        var bumps = new (Vector3 Direction, float Height)[lumps];
        for (int i = 0; i < lumps; i++)
        {
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
        for (int i = 0; i < points.Count; i++)
        {
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
        float floor, bool faceted, Func<Vector3, Vector3, Color> tint)
    {
        var (shaped, triangles) = Shape(center, radii, detail, seed, lumps, bulge, jitter, floor, 0);
        if (faceted) { Faces(surface, shaped, triangles, center, tint); return; }
        var normals = new Vector3[shaped.Length];
        for (int i = 0; i < triangles.Count; i += 3)
        {
            var face = (shaped[triangles[i + 2]] - shaped[triangles[i]]).Cross(shaped[triangles[i + 1]] - shaped[triangles[i]]);
            if (face.Dot(shaped[triangles[i]] - center) < 0) face = -face;
            for (int k = 0; k < 3; k++) normals[triangles[i + k]] += face;
        }
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if ((shaped[c] - shaped[a]).Cross(shaped[b] - shaped[a]).Dot(shaped[a] - center) < 0) (b, c) = (c, b);
            foreach (int k in new[] { a, b, c })
            {
                var normal = normals[k].Normalized();
                surface.SetNormal(normal); surface.SetColor(tint(shaped[k], normal)); surface.AddVertex(shaped[k]);
            }
        }
    }

    /// <summary>Flat-shaded faces of a shape, optionally only those whose middle passes <paramref name="keep"/>.</summary>
    private static void Faces(SurfaceTool surface, Vector3[] shaped, List<int> triangles, Vector3 center, Func<Vector3, Vector3, Color> tint,
        Func<Vector3, bool>? keep = null)
    {
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            var face = (shaped[c] - shaped[a]).Cross(shaped[b] - shaped[a]).Normalized();
            var middle = (shaped[a] + shaped[b] + shaped[c]) / 3;
            if (face.Dot(middle - center) < 0) { (b, c) = (c, b); face = -face; }
            if (keep != null && !keep(middle)) continue;
            var color = tint(middle, face);
            foreach (int k in new[] { a, b, c })
            {
                surface.SetNormal(face); surface.SetColor(color); surface.AddVertex(shaped[k]);
            }
        }
    }

    /// <summary>
    /// A wobbly surface of revolution placed by <paramref name="at"/>; the profile may fold back down to make skirts.
    /// Normals are smooth around the axis but break between steps, so each tier and flare reads as its own facet band.
    /// </summary>
    private static void Lathe(SurfaceTool surface, Transform3D at, Step[] profile, int sides, int seed = 0, float wobble = 0,
        Func<int, int, Color?>? mark = null)
    {
        Vector3 Point(int row, int side)
        {
            float angle = Mathf.Tau * side / sides;
            float radius = profile[row].Radius * (1 + wobble * Noise(new Vector3(row, side % sides, 0), seed));
            return new Vector3(MathF.Cos(angle) * radius, profile[row].Y, MathF.Sin(angle) * radius);
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc, Vector3 outward)
        {
            if ((c - a).Cross(b - a).Dot(outward) < 0) { (b, c) = (c, b); (nb, nc) = (nc, nb); (cb, cc) = (cc, cb); }
            foreach (var (p, n, color) in new[] { (a, na, ca), (b, nb, cb), (c, nc, cc) })
            {
                surface.SetNormal((at.Basis * n).Normalized()); surface.SetColor(color); surface.AddVertex(at * p);
            }
        }
        for (int row = 0; row < profile.Length - 1; row++)
            for (int side = 0; side < sides; side++)
            {
                var a = Point(row, side); var b = Point(row, side + 1); var c = Point(row + 1, side + 1); var d = Point(row + 1, side);
                // Around tangent crossed with the step's slope; the step's own slope decides which way is out.
                var along = (c + d - a - b) / 2;
                if (along.LengthSquared() < 1e-8f) continue;
                Vector3 Normal(int r, int s)
                {
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
        for (int end = 0; end < 2; end++)
        {
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
    private static Func<Vector3, Vector3, Color> Leafy(Color shade, Color mid, Color light, int seed) => (p, normal) =>
    {
        float lit = Math.Clamp(normal.Y * .6f + .45f, 0, 1);
        var color = lit < .5f ? shade.Lerp(mid, lit * 2) : mid.Lerp(light, (lit - .5f) * 2);
        return color.Lightened(Noise(p * 3, seed) * .05f);
    };

    private static Transform3D Limb(Vector3 from, float tiltDegrees, float yawDegrees) =>
        new(Basis.FromEuler(new Vector3(0, Mathf.DegToRad(yawDegrees), 0)) * Basis.FromEuler(new Vector3(0, 0, Mathf.DegToRad(-tiltDegrees))), from);

    /// <summary>Broadleaf oak: flared trunk forking into two limbs that disappear into one lumpy crown.</summary>
    public static Mesh Oak(int variant) => Cached($"oak{variant}", s =>
    {
        Color bark = new("6b4a32"), dark = new("4f3624");
        Lathe(s, Transform3D.Identity, new Step[] { new(0, .44f, dark), new(.18f, .33f, bark), new(1.4f, .26f, bark), new(2.9f, .19f, bark), new(3.3f, .1f, bark) }, 9, 11 + variant, .08f);
        Lathe(s, Limb(new Vector3(.05f, 2.2f, 0), 42, variant * 50), new Step[] { new(0, .13f, bark), new(1.25f, .06f, bark) }, 6);
        Lathe(s, Limb(new Vector3(-.05f, 2.35f, .05f), 38, 170 + variant * 40), new Step[] { new(0, .12f, bark), new(1.15f, .06f, bark) }, 6);
        var tint = Leafy(new Color("2f4d22"), new Color("4a6f2f"), new Color("6a9440"), variant);
        // A broad central mass with clumps around it; one gradient across all of them keeps the crown a single canopy.
        Blob(s, new Vector3(0, 3.9f, 0), new Vector3(1.05f, .88f, 1.05f), 2, 101 + variant, 7, .25f, .04f, 3.1f, false, tint);
        (Vector3 At, float Radius)[] clumps = { (new(1f, 3.45f, .25f), .75f), (new(-.95f, 3.55f, -.2f), .78f), (new(.1f, 3.4f, 1f), .7f),
            (new(-.15f, 3.5f, -1f), .7f), (new(.2f, 4.55f, -.1f), .68f) };
        for (int i = 0; i < clumps.Length; i++)
            Blob(s, clumps[i].At, new Vector3(1, .82f, 1) * clumps[i].Radius, 1, 111 + variant * 7 + i, 4, .3f, .05f, 3.05f, false, tint);
    });

    /// <summary>Norway spruce: one continuous lathe of drooping tiers with ragged edges over a short trunk.</summary>
    public static Mesh Spruce(int variant) => Cached($"spruce{variant}", s =>
    {
        Color bark = new("5b3f2a"), under = new("1f3d29"), needles = new("2f5a3a"), tips = new("44784e");
        Lathe(s, Transform3D.Identity, new Step[] { new(0, .26f, bark), new(.9f, .16f, bark) }, 8);
        var profile = new List<Step> { new(.75f, .14f, under) };
        (float Bottom, float Radius, float Height)[] tiers = { (.7f, 1.5f, 1.4f), (1.65f, 1.27f, 1.35f), (2.55f, 1.03f, 1.25f), (3.4f, .78f, 1.15f), (4.15f, .52f, 1.4f) };
        for (int i = 0; i < tiers.Length; i++)
        {
            var (bottom, radius, height) = tiers[i];
            profile.Add(new(bottom + .12f, radius * .92f, under));
            profile.Add(new(bottom, radius, tips));
            profile.Add(new(bottom + height * .35f, radius * .62f, needles));
            profile.Add(i == tiers.Length - 1 ? new(bottom + height, 0, tips) : new(tiers[i + 1].Bottom + .2f, radius * .34f, needles));
        }
        Lathe(s, Transform3D.Identity, profile.ToArray(), 11, 21 + variant, .09f);
    });

    /// <summary>Silver birch: slender pale trunk with dark bark marks and a light, uneven crown.</summary>
    public static Mesh Birch(int variant) => Cached($"birch{variant}", s =>
    {
        Color pale = new("e6e2d6"), shade = new("cfc9ba"), mark = new("3a3733");
        var steps = new List<Step>();
        // Thin rings between the plain bark bands carry the dark lenticels, so marks stay narrow without separate pieces.
        for (int i = 0; i <= 9; i++)
        {
            steps.Add(new(i * .5f, .16f - i * .009f, i == 0 ? shade : pale));
            if (i is > 0 and < 9) steps.Add(new(i * .5f + .07f, .16f - i * .009f, pale));
        }
        Lathe(s, Transform3D.Identity, steps.ToArray(), 7, 31 + variant, .04f,
            (row, side) => row % 2 == 1 && Noise(new Vector3(row, side, 7), 31 + variant) > .1f ? mark : null);
        Lathe(s, Limb(new Vector3(0, 2.8f, 0), 34, 30 + variant * 70), new Step[] { new(0, .06f, pale), new(1f, .03f, pale) }, 5);
        var tint = Leafy(new Color("4d6a2c"), new Color("6d8c3c"), new Color("93b257"), variant);
        Blob(s, new Vector3(0, 3.85f, 0), new Vector3(.85f, .85f, .85f), 2, 201 + variant, 6, .45f, .06f, 3.1f, false, tint);
        Blob(s, new Vector3(.6f, 3.2f, .3f), new Vector3(.55f, .48f, .55f), 1, 211 + variant, 4, .35f, .05f, 2.9f, false, tint);
        Blob(s, new Vector3(-.5f, 3.45f, -.35f), new Vector3(.52f, .48f, .52f), 1, 221 + variant, 4, .35f, .05f, 3.1f, false, tint);
        Blob(s, new Vector3(.15f, 4.55f, -.15f), new Vector3(.5f, .5f, .5f), 1, 231 + variant, 4, .35f, .05f, 4.3f, false, tint);
    });

    /// <summary>A felled tree's stump: bark sides with root flare and a pale cut face.</summary>
    public static Mesh Stump => Cached("stump", s =>
    {
        Color bark = new("6b4a32"), wood = new("c9a477");
        Lathe(s, Transform3D.Identity, new Step[] { new(0, .36f, new Color("4f3624")), new(.1f, .28f, bark), new(.32f, .26f, wood) }, 9, 5, .06f);
    });

    private static (Vector3 At, Vector3 Radii)[] BushClumps(int variant)
    {
        var random = new Random(301 + variant);
        float R(float spread) => ((float)random.NextDouble() * 2 - 1) * spread;
        var clumps = new List<(Vector3, Vector3)> { (new(R(.05f), .45f, R(.05f)), new(.42f, .36f, .42f)) };
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5 + R(.4f), reach = .38f + R(.06f), radius = .31f + R(.04f);
            clumps.Add((new(MathF.Cos(angle) * reach, radius * .85f, MathF.Sin(angle) * reach), new(radius, radius * .9f, radius)));
        }
        float top = R(3);
        clumps.Add((new(MathF.Cos(top) * .15f, .7f + R(.04f), MathF.Sin(top) * .15f), new(.27f, .23f, .27f)));
        return clumps.ToArray();
    }

    /// <summary>A low mound of leaf clumps resting on the ground under one shared light gradient.</summary>
    public static Mesh Bush(int variant) => Cached($"bush{variant}", s =>
    {
        var tint = Leafy(new Color("2c4d22"), new Color("4c7a33"), new Color("6e9c45"), variant);
        var clumps = BushClumps(variant);
        for (int i = 0; i < clumps.Length; i++)
            Blob(s, clumps[i].At, clumps[i].Radii, 1, 311 + variant * 11 + i, 4, .3f, .06f, .02f, false, tint);
    });

    /// <summary>Berries spread over the upper and outer surface of <see cref="Bush"/>, each set where its ray leaves the outermost clump.</summary>
    public static Mesh Berries(int variant) => Cached($"berries{variant}", s =>
    {
        var clumps = BushClumps(variant);
        var random = new Random(401 + variant);
        Color[] reds = { new("c0283a"), new("a3202f"), new("d23a45") };
        var origin = new Vector3(0, .3f, 0);
        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            float y = .85f - 1.05f * (i + .5f) / count, ring = MathF.Sqrt(1 - y * y), angle = i * 2.39996f + (float)random.NextDouble() * .6f;
            var direction = new Vector3(MathF.Cos(angle) * ring, y, MathF.Sin(angle) * ring);
            float reach = 0;
            foreach (var (at, radii) in clumps)
            {
                var o = (origin - at) / radii; var d = direction / radii;
                float a = d.Dot(d), b = o.Dot(d), c = o.Dot(o) - 1, root = b * b - a * c;
                if (root >= 0) reach = Math.Max(reach, (-b + MathF.Sqrt(root)) / a);
            }
            var color = reds[random.Next(reds.Length)];
            Blob(s, origin + direction * (reach + .015f), Vector3.One * .05f, 0, 0, 0, 0, 0, -1, false, (_, _) => color);
        }
    });

    private static (Vector3 At, Vector3 Radii, int Seed)[] Boulders(int variant)
    {
        var random = new Random(501 + variant);
        float R(float spread) => ((float)random.NextDouble() * 2 - 1) * spread;
        var stones = new List<(Vector3, Vector3, int)> { (new(R(.05f), .28f, R(.05f)), new(.62f + R(.05f), .58f, .55f + R(.05f)), 511 + variant) };
        float turn = R(3);
        for (int i = 0; i < 3; i++)
        {
            float angle = turn + i * 2.1f + R(.3f), radius = .32f - i * .03f + R(.03f), reach = .5f + radius * .3f;
            stones.Add((new(MathF.Cos(angle) * reach, radius * .3f, MathF.Sin(angle) * reach), new(radius * 1.15f, radius, radius), 521 + variant * 5 + i));
        }
        return stones.ToArray();
    }

    private static (Vector3[] Points, List<int> Triangles) Boulder(Vector3 at, Vector3 radii, int seed) => Shape(at, radii, 1, seed, 3, .2f, .06f, 0, 11);

    /// <summary>
    /// A faceted boulder group pressed into the ground: lighter tops, darker undersides and a little per-face variation.
    /// An ore deposit builds the same faces twice, once with <paramref name="veins"/> false for the plain rock and once true for the vein faces,
    /// so the veins can take a mineral sheen while staying flush with the rock.
    /// </summary>
    public static Mesh Rocks(string kind, Color color, int variant, bool? veins = null) => Cached($"rocks{kind}{variant}{veins}", s =>
    {
        foreach (var (at, radii, seed) in Boulders(variant))
        {
            var (points, triangles) = Boulder(at, radii, seed);
            Faces(s, points, triangles, at, (p, normal) =>
            {
                var shade = normal.Y > 0 ? color.Lightened(normal.Y * .12f) : color.Darkened(-normal.Y * .3f);
                return shade.Lightened(Noise(p * 5, seed) * .03f);
            }, veins is bool vein ? p => Noise(p * 3.1f, seed + 9) > .3f == vein : null);
        }
    });

    /// <summary>
    /// Angular ore chunks set into the boulder group, half buried where a ray from inside leaves the outermost rock face.
    /// Rays are cast against the real faces rather than the boulder ellipsoids, because the cutting planes shave the rock inside them.
    /// </summary>
    public static Mesh Insertions(string kind, Color color, int variant) => Cached($"insertions{kind}{variant}", s =>
    {
        var faces = new List<(Vector3, Vector3, Vector3)>();
        foreach (var (at, radii, seed) in Boulders(variant))
        {
            var (points, triangles) = Boulder(at, radii, seed);
            for (int i = 0; i < triangles.Count; i += 3) faces.Add((points[triangles[i]], points[triangles[i + 1]], points[triangles[i + 2]]));
        }
        var random = new Random(601 + variant);
        var origin = new Vector3(0, .2f, 0);
        const int count = 7;
        for (int i = 0; i < count; i++)
        {
            float y = .75f - .9f * i / count, ring = MathF.Sqrt(1 - y * y), angle = i * 2.39996f + (float)random.NextDouble() * .5f;
            var direction = new Vector3(MathF.Cos(angle) * ring, y, MathF.Sin(angle) * ring);
            float reach = 0;
            foreach (var (a, b, c) in faces) if (Hit(origin, direction, a, b, c) is float t) reach = Math.Max(reach, t);
            if (reach <= 0) continue;
            var basis = Basis.FromEuler(new Vector3((float)random.NextDouble() * 1.2f - .6f, (float)random.NextDouble() * Mathf.Tau, (float)random.NextDouble() * 1.2f - .6f));
            var size = new Vector3(.13f, .1f, .12f) * (.85f + (float)random.NextDouble() * .4f);
            Chunk(s, new Transform3D(basis, origin + direction * (reach - .01f)), size, color);
        }
    });

    /// <summary>Distance along a ray to a triangle, or null when it misses (Möller–Trumbore).</summary>
    private static float? Hit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
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

    /// <summary>A box placed by <paramref name="at"/>, flat-shaded with tops a little lighter.</summary>
    private static void Chunk(SurfaceTool surface, Transform3D at, Vector3 size, Color color)
    {
        var half = size / 2;
        foreach (var axis in new[] { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward })
        {
            Vector3 u = axis.X != 0 ? Vector3.Up : Vector3.Right, v = axis.Cross(u);
            Vector3 Corner(float x, float y) => at * ((axis + u * x + v * y) * half);
            var normal = (at.Basis * axis).Normalized();
            var shade = color.Lightened(Math.Max(0, normal.Y) * .12f).Darkened(Math.Max(0, -normal.Y) * .3f);
            Vector3 a = Corner(-1, -1), b = Corner(1, -1), c = Corner(1, 1), d = Corner(-1, 1);
            // Godot treats clockwise triangles as front faces; flip both halves when this face's corners run the other way.
            if ((c - a).Cross(b - a).Dot(normal) < 0) (b, d) = (d, b);
            foreach (var corner in new[] { a, b, c, a, c, d })
            {
                surface.SetNormal(normal); surface.SetColor(shade); surface.AddVertex(corner);
            }
        }
    }

    /// <summary>A few loose lumps lying on the ground, each a faceted nugget seated flat.</summary>
    public static Mesh Loose(string kind, Color light, Color dark, int variant) => Cached($"loose{kind}{variant}", s =>
    {
        var random = new Random(701 + variant);
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 1.4f + (float)random.NextDouble() * .5f, reach = i == 0 ? 0 : .24f + (float)random.NextDouble() * .06f;
            float size = i == 0 ? .18f : .13f + (float)random.NextDouble() * .03f;
            var color = i % 2 == 0 ? light : dark;
            Blob(s, new Vector3(MathF.Cos(angle) * reach, size * .45f, MathF.Sin(angle) * reach), new Vector3(1.15f, .75f, 1) * size,
                0, 711 + variant * 7 + i, 2, .3f, .1f, 0, true, (_, normal) => color.Lightened(normal.Y * .1f).Darkened(Math.Max(0, -normal.Y) * .3f));
        }
    });
}
