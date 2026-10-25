using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Static ground dressing, short-lived particle effects and the developer animation review.</summary>
public partial class WorldView
{
    private readonly Random effectRandom = new(1337);
    private float Jitter() => (float)effectRandom.NextDouble();

    private void BuildGround(Node3D parent)
    {
        // Fine grass grain with occasional bare-earth flecks at the top of the ramp.
        var ramp = new Gradient { Offsets = new[] { 0f, .35f, .6f, .84f, .9f, 1f }, Colors = new[] { new Color("72895a"), new Color("7e9563"), new Color("889e6b"), new Color("91a573"), new Color("9a9467"), new Color("a39a6c") } };
        var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = .045f, FractalOctaves = 4, FractalGain = .6f, Seed = 4271 };
        var grass = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = noise, ColorRamp = ramp };
        const float span = Terrain.Size * 4;
        var ground = new StandardMaterial3D { AlbedoTexture = grass, Roughness = 1, Uv1Scale = new Vector3(span / 9, span / 9, 1) };
        Add(parent, new PlaneMesh { Size = new Vector2(span, span) }, ground, new Vector3(Terrain.Size / 2f, 0, Terrain.Size / 2f));
        // The arrival clearing: overlapping translucent discs soften into the grass.
        for (int ring = 0; ring < 4; ring++)
            Cylinder(parent, 2.4f + ring * .7f, 2.4f + ring * .7f, .002f, new Vector3(World.Spawn.X, .003f + ring * .0005f, World.Spawn.Y), Flat(new Color(.66f, .6f, .42f, .1f), true), null, 32);
        // A narrow, worn trail from the clearing down the valley to the shore, the way the carts go.
        float from = Terrain.Along(World.Spawn), across = Terrain.Across(World.Spawn);
        for (int layer = 3; layer > 0; layer--)
        {
            float width = .33f + layer * .12f;
            var strip = new SurfaceTool();
            strip.Begin(Mesh.PrimitiveType.TriangleStrip);
            for (float along = from - 1; along <= Terrain.Coast(across) + 2; along += 1)
            {
                var mid = Terrain.Point(along, across + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f);
                float lift = .006f + (3 - layer) * .001f;
                strip.SetNormal(Vector3.Up); strip.AddVertex(To3(mid - Terrain.Sideways * width, lift));
                strip.SetNormal(Vector3.Up); strip.AddVertex(To3(mid + Terrain.Sideways * width, lift));
            }
            Add(parent, strip.Commit(), Flat(new Color(.67f, .6f, .43f, layer == 1 ? .3f : .16f), true), Vector3.Zero);
        }
        BuildSea(parent);
        BuildLake(parent);
        BuildMountains(parent);
        foreach (var cliff in World.Cliffs) BuildCliff(parent, cliff);
        BuildTufts(parent);
    }

    /// <summary>Sea beyond a sandy shore along the valley's mouth; it runs past the map so the coast is its edge.</summary>
    private static void BuildSea(Node3D parent)
    {
        var water = new StandardMaterial3D { AlbedoColor = new Color("2e5966"), Roughness = .08f, Metallic = .3f, MetallicSpecular = .9f };
        Band(parent, -1.2f, 400, .02f, water);
        Band(parent, -4.5f, .4f, .008f, Mat("bdb08a", 1));
        Band(parent, -.2f, 5, .024f, Flat(new Color(.55f, .78f, .76f, .22f), true));
    }

    /// <summary>A strip following the coastline from <paramref name="inner"/> to <paramref name="outer"/> tiles past it.</summary>
    private static void Band(Node3D parent, float inner, float outer, float height, Material material)
    {
        var strip = new SurfaceTool();
        strip.Begin(Mesh.PrimitiveType.TriangleStrip);
        for (float across = -300; across <= 300; across += 1.5f)
        {
            float coast = Terrain.Coast(across);
            strip.SetNormal(Vector3.Up); strip.AddVertex(To3(Terrain.Point(coast + outer, across), height));
            strip.SetNormal(Vector3.Up); strip.AddVertex(To3(Terrain.Point(coast + inner, across), height));
        }
        Add(parent, strip.Commit(), material, Vector3.Zero);
    }

    /// <summary>Both ranges as one flat-shaded height field that rises from the valley's edge into ridged peaks.</summary>
    private static void BuildMountains(Node3D parent)
    {
        var ridges = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = .018f, FractalType = FastNoiseLite.FractalTypeEnum.Ridged, FractalOctaves = 4, Seed = 31 };
        var tint = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = .05f, Seed = 5 };
        float Height(Vector2 p)
        {
            float depth = -Terrain.FromMountains(p);
            if (depth <= 0) return 0;
            // A steady climb from the foot that levels off, with ridges and peaks growing in higher up, easing down where the range meets the sea.
            float h = 26 * (1 - MathF.Exp(-depth / 16)) + (ridges.GetNoise2D(p.X, p.Y) + 1) * 13 * Math.Clamp(depth / 18, 0, 1);
            float seaward = Math.Clamp((Terrain.Coast(Terrain.Across(p)) + 14 - Terrain.Along(p)) / 14, 0, 1);
            return h * seaward;
        }
        const float step = 2, from = -110, to = Terrain.Size + 110;
        int count = (int)((to - from) / step) + 1;
        var heights = new float[count, count];
        for (int y = 0; y < count; y++)
            for (int x = 0; x < count; x++) heights[x, y] = Height(new Vector2(from + x * step, from + y * step));
        Color[] grass = { new("56663a"), new("5e6c3e"), new("67703f"), new("6d6a3c") }, scrub = { new("6b6a43"), new("5f5e3d"), new("74703f"), new("62603f") }, rock = { new("55524c"), new("5f5b54"), new("4b4945"), new("68635b") }, crest = { new("8a8984"), new("7c7b76") };
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int y = 0; y + 1 < count; y++)
            for (int x = 0; x + 1 < count; x++)
            {
                Vector3 V(int i, int j) => new(from + i * step, heights[i, j], from + j * step);
                var a = V(x, y); var b = V(x + 1, y); var c = V(x, y + 1); var d = V(x + 1, y + 1);
                foreach (var (p, q, r) in new[] { (a, b, d), (a, d, c) })
                {
                    if (p.Y + q.Y + r.Y <= 0) continue;
                    var normal = (r - p).Cross(q - p).Normalized();
                    if (normal.Y < 0) normal = -normal;
                    float top = Math.Max(p.Y, Math.Max(q.Y, r.Y)), shade = tint.GetNoise2D(p.X, p.Z);
                    // Gentle, low ground stays grassy and lower slopes scrubby; steep or high faces are bare rock, and the highest pale.
                    var palette = top > 40 ? crest : top < 24 && normal.Y > .6f ? grass : top < 30 && normal.Y > .35f ? scrub : rock;
                    var color = palette[(int)((shade + 1) * 1.99f * palette.Length / 2) % palette.Length];
                    foreach (var v in new[] { p, q, r }) { tool.SetColor(color); tool.SetNormal(normal); tool.AddVertex(v + Vector3.Down * .03f); }
                }
            }
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        parent.AddChild(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = material });
    }

    /// <summary>A jagged wall of faceted columns with ore seams on the face and scree at its foot.</summary>
    private static void BuildCliff(Node3D parent, Cliff cliff)
    {
        // Rust-streaked iron, black coal and pale grey tin with bright metallic seams.
        (string[] rock, var seam, int seed) = cliff.Ore switch
        {
            "iron" => (new[] { "7d5b45", "8a6650", "6e4f3c" }, Mat("b4622a", .5f), 11),
            "tin" => (new[] { "7f827d", "8b8e88", "737671" }, Mat("e9ebe4", .35f), 37),
            _ => (new[] { "3a3c40", "45474c", "2d2f33" }, Mat("15161a", .5f, .3f), 23),
        };
        // Local +Z is the face, so everything toward the camera and the deposits is built on the positive side.
        var wall = Pivot(parent, new Vector3(cliff.Center.X, 0, cliff.Center.Y), new Vector3(0, Mathf.RadToDeg(MathF.Atan2(cliff.Face.X, cliff.Face.Y)), 0));
        WorldModels.MineSpur(parent, cliff, rock);
        var rng = new Random(seed);
        float R() => (float)rng.NextDouble();
        for (float x = -cliff.HalfLength - .4f; x <= cliff.HalfLength + .4f; x += .8f)
        {
            // The ends taper down so the wall reads as a rock outcrop rather than a cut block.
            float edge = 1 - Math.Clamp((Math.Abs(x) - cliff.HalfLength + 1.5f) / 2.5f, 0, 1) * .6f;
            float height = (2.8f + R() * 1.8f) * edge, z = -.4f + R() * .5f;
            Cylinder(wall, .95f + R() * .2f, .55f + R() * .25f, height, new Vector3(x, height / 2, z), Mat(rock[rng.Next(rock.Length)], .95f), new Vector3((R() - .5f) * 8, R() * 360, (R() - .5f) * 8), 5);
            Sphere(wall, .55f + R() * .3f, new Vector3(x + (R() - .5f) * .4f, height + .05f, z - .1f), Mat(rock[rng.Next(rock.Length)], .95f), new Vector3(1.2f, .6f, 1), 5);
            for (int i = 0; i < 2; i++)
                Box(wall, new Vector3(.35f + R() * .3f, .08f + R() * .06f, .12f), new Vector3(x + (R() - .5f) * .5f, .5f + R() * (height - .9f), z + .95f), seam, new Vector3(0, 0, (R() - .5f) * 40));
            for (int i = 0; i < 2; i++)
                Sphere(wall, .14f + R() * .16f, new Vector3(x + (R() - .5f) * .8f, .06f, 1.1f + R() * .6f), Mat(rock[rng.Next(rock.Length)], .95f), new Vector3(1.2f, .6f, 1), 5);
        }
    }

    private static void BuildLake(Node3D parent)
    {
        var at = To3(Terrain.Lake);
        // The bank and water follow the same shore line that bounds walking.
        float Shore(float angle) => Terrain.Shore(angle);
        Blob(parent, at + new Vector3(0, .005f, 0), a => Shore(a) + 1.4f + .3f * MathF.Sin(a * 4), Mat("7f7556", 1));
        Blob(parent, at + new Vector3(0, .012f, 0), a => Shore(a) + .3f, Mat("5d5540", 1));
        var water = new StandardMaterial3D { AlbedoColor = new Color("3f6f72"), Roughness = .06f, Metallic = .3f, MetallicSpecular = .9f };
        Blob(parent, at + new Vector3(0, .025f, 0), Shore, water);
        Blob(parent, at + new Vector3(0, .028f, 0), a => Shore(a) * .7f, Flat(new Color(.12f, .27f, .3f, .4f), true));
        Blob(parent, at + new Vector3(0, .03f, 0), a => Shore(a) * .4f, Flat(new Color(.08f, .2f, .24f, .4f), true));
        var rng = new Random(77);
        const int stones = 150;
        for (int i = 0; i < stones; i++)
        {
            float angle = i * Mathf.Tau / stones + (float)rng.NextDouble() * .03f, r = Shore(angle) + .2f + (float)rng.NextDouble() * .5f;
            var stone = Sphere(parent, .1f + (float)rng.NextDouble() * .14f, at + new Vector3(MathF.Cos(angle) * r, .03f, MathF.Sin(angle) * r), Mat(i % 3 == 0 ? "a3a7a3" : "8d918e", .9f), new Vector3(1.2f, .5f, 1), 6);
            stone.RotationDegrees = new Vector3(0, i * 47, 0);
        }
        // Reed clumps with cattails along the banks, lily pads near the shore.
        for (float angle = .2f; angle < Mathf.Tau; angle += .45f + (float)rng.NextDouble() * .5f)
        {
            var clump = at + new Vector3(MathF.Cos(angle) * (Shore(angle) - .1f), 0, MathF.Sin(angle) * (Shore(angle) - .1f));
            for (int i = 0; i < 7; i++)
            {
                var offset = new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f) * .6f;
                float height = .7f + (float)rng.NextDouble() * .6f;
                Cylinder(parent, .02f, .01f, height, clump + offset + new Vector3(0, height / 2, 0), Mat("6b8a3e"), new Vector3(((float)rng.NextDouble() - .5f) * 14, 0, ((float)rng.NextDouble() - .5f) * 14), 4);
                if (i % 3 == 0) Cylinder(parent, .035f, .035f, .16f, clump + offset + new Vector3(0, height - .05f, 0), Mat("6e4a2e"), null, 6);
            }
        }
        for (int i = 0; i < 24; i++)
        {
            float angle = (float)rng.NextDouble() * Mathf.Tau, r = Shore(angle) - 1 - (float)rng.NextDouble() * 3;
            Cylinder(parent, .28f, .28f, .01f, at + new Vector3(MathF.Cos(angle) * r, .035f, MathF.Sin(angle) * r), Mat("5b8a3e", .7f), null, 9);
        }
    }

    /// <summary>A flat, star-shaped disc whose edge distance comes from <paramref name="radius"/>.</summary>
    private static void Blob(Node3D parent, Vector3 center, Func<float, float> radius, Material material)
    {
        var fan = new SurfaceTool();
        fan.Begin(Mesh.PrimitiveType.Triangles);
        const int segments = 72;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.Tau / segments, b = (i + 1) * Mathf.Tau / segments;
            fan.SetNormal(Vector3.Up); fan.AddVertex(Vector3.Zero);
            fan.SetNormal(Vector3.Up); fan.AddVertex(new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * radius(a));
            fan.SetNormal(Vector3.Up); fan.AddVertex(new Vector3(MathF.Cos(b), 0, MathF.Sin(b)) * radius(b));
        }
        Add(parent, fan.Commit(), material, center);
    }

    /// <summary>Scattered grass tufts and a few wildflowers, drawn as two multimeshes.</summary>
    private void BuildTufts(Node3D parent)
    {
        var rng = new Random(4271);
        var blades = new List<(Transform3D At, Color Color)>();
        var flowers = new List<(Transform3D At, Color Color)>();
        Color[] petals = { new("f2efe0"), new("e9cf5a"), new("b68ad6") };
        var bushes = new List<(Transform3D At, Color Color)>();
        Color[] leaves = { new("4f6b35"), new("5b7a3c"), new("466130") };
        while (bushes.Count < 900)
        {
            // Plain bushes dot the open valley, a few leafy lumps each; berry bushes are resources and drawn with them.
            var p = new Vector2((float)rng.NextDouble() * Terrain.Size, (float)rng.NextDouble() * Terrain.Size);
            if (!Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, 1.5f) || p.DistanceTo(World.Spawn) < 6 || Terrain.FromMountains(p) < 3) continue;
            for (int i = 0; i < 3; i++)
            {
                float size = .35f + (float)rng.NextDouble() * .3f;
                var at = new Vector3(p.X + ((float)rng.NextDouble() - .5f) * .7f, size * .35f, p.Y + ((float)rng.NextDouble() - .5f) * .7f);
                bushes.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(size, size * .75f, size)), at), leaves[rng.Next(leaves.Length)]));
            }
        }
        while (blades.Count < 32000)
        {
            var p = new Vector2((float)rng.NextDouble() * Terrain.Size, (float)rng.NextDouble() * Terrain.Size);
            if (!Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, .8f)) continue;
            var basis = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(Vector3.One * (.7f + (float)rng.NextDouble() * .7f));
            blades.Add((new Transform3D(basis, new Vector3(p.X, 0, p.Y)), new Color("5f7d43").Lerp(new Color("8ea75f"), (float)rng.NextDouble())));
            if (rng.NextDouble() < .07) flowers.Add((new Transform3D(Basis.Identity, new Vector3(p.X + .12f, .14f, p.Y + .05f)), petals[rng.Next(petals.Length)]));
        }
        var tuft = new SurfaceTool();
        tuft.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 1.7f; var lean = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
            var side = new Vector3(-lean.Z, 0, lean.X) * .035f;
            tuft.SetNormal(Vector3.Up); tuft.AddVertex(-side + lean * .02f);
            tuft.SetNormal(Vector3.Up); tuft.AddVertex(side + lean * .02f);
            tuft.SetNormal(Vector3.Up); tuft.AddVertex(lean * .1f + new Vector3(0, .22f + i * .03f, 0));
        }
        Scatter(parent, tuft.Commit(), blades);
        Scatter(parent, new SphereMesh { Radius = .045f, Height = .07f, RadialSegments = 6, Rings = 3 }, flowers);
        Scatter(parent, new SphereMesh { Radius = 1, Height = 2, RadialSegments = 7, Rings = 4 }, bushes);
    }

    private static void Scatter(Node3D parent, Mesh mesh, List<(Transform3D At, Color Color)> items)
    {
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = items.Count };
        for (int i = 0; i < items.Count; i++) { multi.SetInstanceTransform(i, items[i].At); multi.SetInstanceColor(i, items[i].Color); }
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        parent.AddChild(new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // ---------- Particles: chips, leaves, sparks, splashes, embers and smoke ----------

    private sealed class Particle
    {
        public MeshInstance3D Node = null!;
        public Vector3 Velocity;
        public float Age, Life, Gravity, Size, Grow;
    }

    private Node3D effects = null!;
    private readonly List<Particle> particles = new();
    private static readonly SphereMesh Mote = new() { Radius = .5f, Height = 1, RadialSegments = 6, Rings = 3 };

    private void BuildEffects(Node3D parent) { effects = new Node3D(); parent.AddChild(effects); }

    /// <summary>Spawns short-lived motes; negative gravity makes smoke and embers rise.</summary>
    private void Burst(Vector3 at, Material material, int count, float speed, float life, float gravity, float size, float grow = 0)
    {
        for (int i = 0; i < count; i++)
        {
            var particle = particles.Find(p => !p.Node.Visible);
            if (particle == null)
            {
                if (particles.Count >= 400) return;
                particle = new Particle { Node = new MeshInstance3D { Mesh = Mote, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off } };
                effects.AddChild(particle.Node); particles.Add(particle);
            }
            var direction = new Vector3(Jitter() - .5f, Jitter() * .8f + .4f, Jitter() - .5f).Normalized();
            particle.Node.MaterialOverride = material;
            particle.Node.SetMeta("base", material);
            particle.Node.Position = at;
            particle.Node.Visible = true;
            particle.Velocity = direction * speed * (.6f + Jitter() * .6f);
            particle.Age = 0; particle.Life = life * (.7f + Jitter() * .6f); particle.Gravity = gravity; particle.Size = size; particle.Grow = grow;
            particle.Node.Scale = Vector3.One * size;
        }
    }

    private void UpdateEffects(float dt)
    {
        if (dt <= 0) return;
        foreach (var p in particles)
        {
            if (!p.Node.Visible) continue;
            p.Age += dt;
            if (p.Age >= p.Life) { p.Node.Visible = false; continue; }
            p.Velocity += Vector3.Down * p.Gravity * dt;
            var position = p.Node.Position + p.Velocity * dt;
            // Chips and droplets settle on the ground instead of sinking through it.
            if (position.Y < .02f && p.Gravity > 0) { position.Y = .02f; p.Velocity = Vector3.Zero; }
            p.Node.Position = position;
            float t = p.Age / p.Life;
            p.Node.Scale = Vector3.One * p.Size * (1 + p.Grow * t) * (p.Grow > 0 ? 1 : 1 - t * t);
            if (p.Grow > 0) SetFade(p.Node, t);
        }
    }

    // ---------- Developer animation review ----------

    private static readonly (string Name, ActorSkin Skin, ActorAction Action)[] ReviewClips =
    {
        ("player walk", ActorSkin.Player, ActorAction.Walk), ("player axe", ActorSkin.Player, ActorAction.Chop), ("player steel axe", ActorSkin.Player, ActorAction.SteelChop),
        ("player pick", ActorSkin.Player, ActorAction.Mine), ("player steel pick", ActorSkin.Player, ActorAction.SteelMine), ("player gather", ActorSkin.Player, ActorAction.Gather),
        ("player hammer", ActorSkin.Player, ActorAction.Hammer), ("player pump", ActorSkin.Player, ActorAction.Pump), ("player punch", ActorSkin.Player, ActorAction.Attack),
        ("player idle", ActorSkin.Player, ActorAction.Idle), ("worker walk", ActorSkin.Worker, ActorAction.Walk), ("worker axe", ActorSkin.Worker, ActorAction.Chop),
        ("worker pick", ActorSkin.Worker, ActorAction.Mine), ("worker carry", ActorSkin.Worker, ActorAction.Carry), ("worker gather", ActorSkin.Worker, ActorAction.Gather),
        ("worker hammer", ActorSkin.Worker, ActorAction.Hammer), ("worker pump", ActorSkin.Worker, ActorAction.Pump), ("worker idle", ActorSkin.Worker, ActorAction.Idle),
        ("worker sit", ActorSkin.Worker, ActorAction.Sit), ("worker sit on rock", ActorSkin.Worker, ActorAction.Perch),
    };
    private readonly List<ActorRig> reviewRigs = new();
    private Node3D? review;
    private static readonly Vector3 ScreenRight = new Vector3(1, 0, -1).Normalized(), ScreenDown = new Vector3(1, 0, 1).Normalized();
    private static Vector3 ReviewCenter => new(0, 1, 0);
    private static Vector3 ReviewSpot(int index) => ScreenRight * ((index % 6 - 2.5f) * 2.2f) + ScreenDown * ((index / 6 - 1.4f) * 3.3f);

    private void UpdateReview()
    {
        if (review == null)
        {
            scene.Visible = false;
            review = new Node3D(); AddChild(review);
            Cylinder(review, 40, 40, .02f, new Vector3(0, -.01f, 0), Mat("4b5e45", 1), null, 32);
            for (int i = 0; i < ReviewClips.Length; i++)
            {
                var rig = new ActorRig(ReviewClips[i].Skin);
                review.AddChild(rig.Root); rig.Root.Position = ReviewSpot(i);
                reviewRigs.Add(rig);
            }
        }
        for (int i = 0; i < ReviewClips.Length; i++)
        {
            var action = ReviewClips[i].Action;
            reviewRigs[i].Root.Rotation = new Vector3(0, Elapsed * .7f + Mathf.Pi / 4, 0);
            reviewRigs[i].Show(ActorAnimation.PoseAt(action, Elapsed, action is ActorAction.Walk or ActorAction.Carry), ActorAnimation.Tool(action), action == ActorAction.Carry, action == ActorAction.Perch);
        }
    }

    private void PaintReview()
    {
        overlay.DrawString(font, new Vector2(28, 40), "ANIMATION REVIEW · keyframed 3D rig, rotating to show every side", HorizontalAlignment.Left, -1, 23, new Color("f2e8cf"));
        for (int i = 0; i < ReviewClips.Length; i++)
        {
            var at = camera.UnprojectPosition(ReviewSpot(i));
            var size = font.GetStringSize(ReviewClips[i].Name, HorizontalAlignment.Left, -1, 17);
            overlay.DrawString(font, at + new Vector2(-size.X / 2, 30), ReviewClips[i].Name, HorizontalAlignment.Left, -1, 17, new Color("f2e8cf"));
        }
    }
}
