using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Static ground dressing, short-lived particle effects and the developer animation review.</summary>
public partial class WorldView {
    private readonly Random effectRandom = new(1337);
    private float Jitter() => (float)effectRandom.NextDouble();

    private void BuildGround(Node3D parent) {
        InitializeDressing();
        // Fine grass grain with occasional bare-earth flecks at the top of the ramp.
        var ramp = new Gradient { Offsets = new[] { 0f, .35f, .6f, .84f, .9f, 1f }, Colors = new[] { new Color("72895a"), new Color("7e9563"), new Color("889e6b"), new Color("91a573"), new Color("9a9467"), new Color("a39a6c") } };
        var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = .045f, FractalOctaves = 4, FractalGain = .6f, Seed = 4271 };
        var grass = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = noise, ColorRamp = ramp };
        BuildTerrain3D(parent, grass);
        // The arrival clearing: overlapping translucent discs soften into the grass.
        for (int ring = 0; ring < 4; ring++)
            Cylinder(parent, 2.4f + ring * .7f, 2.4f + ring * .7f, .002f, new Vector3(World.Spawn.X, .003f + ring * .0005f, World.Spawn.Y), Flat(new Color(.66f, .6f, .42f, .1f), true), null, 32);
        // A narrow, worn trail from the clearing down the valley to the shore, the way the carts go.
        float from = Terrain.Along(World.Spawn), across = Terrain.Across(World.Spawn);
        for (int layer = 3; layer > 0; layer--) {
            float width = .33f + layer * .12f;
            var strip = new SurfaceTool();
            strip.Begin(Mesh.PrimitiveType.TriangleStrip);
            for (float along = from - 1; along <= Terrain.Coast(across) + 2; along += 1) {
                var mid = Terrain.Point(along, across + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f);
                float lift = .006f + (3 - layer) * .001f;
                strip.SetNormal(Vector3.Up); strip.AddVertex(To3(mid - Terrain.Sideways * width, lift));
                strip.SetNormal(Vector3.Up); strip.AddVertex(To3(mid + Terrain.Sideways * width, lift));
            }
            Add(parent, strip.Commit(), Flat(new Color(.67f, .6f, .43f, layer == 1 ? .3f : .16f), true), Vector3.Zero);
        }
        // Sea and its feathered bank follow the coastline at the valley's mouth.
        ShorelineAppearance.BuildCoast(parent, StillWater);
        BuildLake(parent);
        foreach (var cliff in World.Cliffs) BuildCliff(parent, cliff);
        BuildPlacedEnvironment(parent);
    }

    /// <summary>A jagged wall of faceted columns with ore seams on the face and scree at its foot.</summary>
    private static void BuildCliff(Node3D parent, Cliff cliff) {
        // Host rock follows the mountain; distinctive ore colors remain in the seams.
        (string[] rock, var seam, int seed) = cliff.Ore switch {
            "iron" => (new[] { "686650", "716b56", "5b5e4c" }, Mat("b4622a", .5f), 11),
            "tin" => (new[] { "656d5d", "717665", "586252" }, Mat("e9ebe4", .35f), 37),
            _ => (new[] { "535d4b", "606753", "495440" }, Mat("15161a", .5f, .3f), 23),
        };
        // Local +Z is the face, so everything toward the camera and the deposits is built on the positive side.
        var wall = Pivot(parent, new Vector3(cliff.Center.X, 0, cliff.Center.Y), new Vector3(0, Mathf.RadToDeg(MathF.Atan2(cliff.Face.X, cliff.Face.Y)), 0));
        WorldModels.MineSpur(parent, cliff, rock);
        var rng = new Random(seed);
        float R() => (float)rng.NextDouble();
        for (float x = -cliff.HalfLength - .4f; x <= cliff.HalfLength + .4f; x += .8f) {
            // The mine's outcrop replaces the middle of the wall and keeps the tunnel clear.
            if (Math.Abs(x) < 2.6f) continue;
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

    private void BuildLake(Node3D parent) {
        var at = To3(Terrain.Lake);
        // The bank and water follow the same shore line that bounds walking.
        ShorelineAppearance.BuildLakeBank(parent);
        LakeAppearance.BuildLakeAppearance(parent, StillWater);
        var rng = new Random(77);
        const int stones = 150;
        for (int i = 0; i < stones; i++) {
            float angle = i * Mathf.Tau / stones + (float)rng.NextDouble() * .03f;
            // Stones collect in irregular patches instead of forming an evenly spaced border.
            if (MathF.Sin(angle * 7 + 1.2f) + .55f * MathF.Sin(angle * 13) < -.05f) continue;
            float r = Terrain.Shore(angle) + .12f + (float)rng.NextDouble() * .95f;
            var stone = Sphere(parent, .1f + (float)rng.NextDouble() * .14f, at + new Vector3(MathF.Cos(angle) * r, .03f, MathF.Sin(angle) * r), Mat(i % 3 == 0 ? "a3a7a3" : "8d918e", .9f), new Vector3(1.2f, .5f, 1), 6);
            stone.RotationDegrees = new Vector3(0, i * 47, 0);
        }
        // Reed clumps with cattails along the banks, lily pads near the shore.
        for (float angle = .2f; angle < Mathf.Tau; angle += .45f + (float)rng.NextDouble() * .5f) {
            var clump = at + new Vector3(MathF.Cos(angle) * (Terrain.Shore(angle) - .1f), 0, MathF.Sin(angle) * (Terrain.Shore(angle) - .1f));
            for (int i = 0; i < 7; i++) {
                var offset = new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f) * .6f;
                float height = .7f + (float)rng.NextDouble() * .6f;
                Cylinder(parent, .02f, .01f, height, clump + offset + new Vector3(0, height / 2, 0), Mat("6b8a3e"), new Vector3(((float)rng.NextDouble() - .5f) * 14, 0, ((float)rng.NextDouble() - .5f) * 14), 4);
                if (i % 3 == 0) Cylinder(parent, .035f, .035f, .16f, clump + offset + new Vector3(0, height - .05f, 0), Mat("6e4a2e"), null, 6);
            }
        }
        for (int i = 0; i < 24; i++) {
            float angle = (float)rng.NextDouble() * Mathf.Tau, r = Terrain.Shore(angle) - 1 - (float)rng.NextDouble() * 3;
            Cylinder(parent, .28f, .28f, .01f, at + new Vector3(MathF.Cos(angle) * r, .035f, MathF.Sin(angle) * r), Mat("5b8a3e", .7f), null, 9);
        }
    }

    private static void Scatter(Node3D parent, Mesh mesh, List<(Transform3D At, Color Color)> items, ShaderMaterial material, bool castShadow = false) {
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = items.Count };
        for (int i = 0; i < items.Count; i++) { multi.SetInstanceTransform(i, items[i].At); multi.SetInstanceColor(i, items[i].Color); }
        var shadow = castShadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        var instance = new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = material, CastShadow = shadow };
        parent.AddChild(instance);
    }

    // ---------- Particles: chips, leaves, sparks, splashes, embers and smoke ----------

    private sealed class Particle {
        public MeshInstance3D Node = null!;
        public StandardMaterial3D Material = null!;
        public bool Live;
        public Vector3 Velocity;
        public float Age, Life, Gravity, Size, Grow;
    }

    private Node3D effects = null!;
    private readonly List<Particle> particles = new();
    private static readonly SphereMesh Mote = new() { Radius = .5f, Height = 1, RadialSegments = 6, Rings = 3 };

    private const int MaxParticles = 400;
    private void BuildEffects(Node3D parent) {
        effects = new Node3D(); parent.AddChild(effects);
        // The whole pool and the dust's faded copies are made while loading, so a burst instantiates nothing mid-game.
        WarmFades(plopDust);
        for (int i = 0; i < MaxParticles; i++) {
            var particle = new Particle { Node = new MeshInstance3D { Mesh = Mote, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false } };
            effects.AddChild(particle.Node); particles.Add(particle);
        }
    }

    private Particle? FreeParticle() {
        foreach (var p in particles) if (!p.Live) return p;
        return null;
    }

    /// <summary>Spawns short-lived motes; negative gravity makes smoke and embers rise.</summary>
    private void Burst(Vector3 at, StandardMaterial3D material, int count, float speed, float life, float gravity, float size, float grow = 0) {
        for (int i = 0; i < count; i++) {
            if (FreeParticle() is not Particle particle) return;
            var direction = new Vector3(Jitter() - .5f, Jitter() * .8f + .4f, Jitter() - .5f).Normalized();
            particle.Node.MaterialOverride = particle.Material = material;
            particle.Node.Position = at;
            particle.Node.Visible = particle.Live = true;
            particle.Velocity = direction * speed * (.6f + Jitter() * .6f);
            particle.Age = 0; particle.Life = life * (.7f + Jitter() * .6f); particle.Gravity = gravity; particle.Size = size; particle.Grow = grow;
            particle.Node.Scale = Vector3.One * size;
        }
    }

    private readonly StandardMaterial3D plopDust = Flat(new Color(.8f, .76f, .68f, .7f));

    /// <summary>A ring of dust puffing out along the ground from the base of something just put down, <paramref name="radius"/> from its centre.</summary>
    private void Plop(Vector2 at, float radius) {
        const int count = 18;
        for (int i = 0; i < count; i++) {
            if (FreeParticle() is not Particle particle) return;
            float angle = (i + Jitter() * .6f) / count * Mathf.Tau;
            var outward = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
            particle.Node.MaterialOverride = particle.Material = plopDust;
            particle.Node.Position = To3(at, .12f) + outward * radius * .9f;
            particle.Node.Visible = particle.Live = true;
            particle.Velocity = outward * (1 + Jitter() * .6f) + Vector3.Up * (.35f + Jitter() * .35f);
            particle.Age = 0; particle.Life = .7f + Jitter() * .4f; particle.Gravity = -.15f; particle.Size = .3f + Jitter() * .12f; particle.Grow = 1.8f;
            particle.Node.Scale = Vector3.One * particle.Size;
        }
    }

    private void UpdateEffects(float dt) {
        if (dt <= 0) return;
        foreach (var p in particles) {
            if (!p.Live) continue;
            p.Age += dt;
            if (p.Age >= p.Life) { p.Node.Visible = p.Live = false; continue; }
            p.Velocity += Vector3.Down * p.Gravity * dt;
            var position = p.Node.Position + p.Velocity * dt;
            // Chips and droplets settle on the ground instead of sinking through it.
            if (position.Y < .02f && p.Gravity > 0) { position.Y = .02f; p.Velocity = Vector3.Zero; }
            p.Node.Position = position;
            float t = p.Age / p.Life;
            p.Node.Scale = Vector3.One * p.Size * (1 + p.Grow * t) * (p.Grow > 0 ? 1 : 1 - t * t);
            if (p.Grow > 0) SetFade(p.Node, p.Material, t);
        }
    }

    // ---------- Developer animation review ----------

    private static readonly (string Name, ActorSkin Skin, ActorAction Action)[] ReviewClips =
    {
        ("player walk", ActorSkin.Player, ActorAction.Walk), ("player run", ActorSkin.Player, ActorAction.Run), ("player axe", ActorSkin.Player, ActorAction.Chop), ("player steel axe", ActorSkin.Player, ActorAction.SteelChop),
        ("player pick", ActorSkin.Player, ActorAction.Mine), ("player steel pick", ActorSkin.Player, ActorAction.SteelMine), ("player gather", ActorSkin.Player, ActorAction.Gather),
        ("player hammer", ActorSkin.Player, ActorAction.Hammer), ("player pump", ActorSkin.Player, ActorAction.Pump), ("player punch", ActorSkin.Player, ActorAction.Attack),
        ("player idle", ActorSkin.Player, ActorAction.Idle), ("worker walk", ActorSkin.Worker, ActorAction.Walk), ("worker axe", ActorSkin.Worker, ActorAction.Chop),
        ("worker pick", ActorSkin.Worker, ActorAction.Mine), ("worker carry", ActorSkin.Worker, ActorAction.Carry), ("worker gather", ActorSkin.Worker, ActorAction.Gather),
        ("worker hammer", ActorSkin.Worker, ActorAction.Hammer), ("worker pump", ActorSkin.Worker, ActorAction.Pump), ("worker idle", ActorSkin.Worker, ActorAction.Idle),
        ("worker sit", ActorSkin.Worker, ActorAction.Sit), ("worker sit on rock", ActorSkin.Worker, ActorAction.Perch), ("player pick up", ActorSkin.Player, ActorAction.PickUp),
    };
    private static readonly string[] ReviewWolves = { "wolf idle", "wolf trot", "wolf gallop", "wolf leap", "wolf feed", "wolf fall", "wolf hit" };
    private readonly List<ActorRig> reviewRigs = new();
    private readonly List<WorldModels.WolfModel> reviewWolves = new();
    private Node3D? review;
    private static readonly Vector3 ScreenRight = new Vector3(1, 0, -1).Normalized(), ScreenDown = new Vector3(1, 0, 1).Normalized();
    private static Vector3 ReviewCenter => new(0, 1, 0);
    private static Vector3 ReviewSpot(int index) => ScreenRight * ((index % 7 - 3) * 2f) + ScreenDown * ((index / 7 - 2f) * 3.3f);

    private void UpdateReview() {
        if (review == null) {
            scene.Visible = false;
            review = new Node3D(); AddChild(review);
            Cylinder(review, 40, 40, .02f, new Vector3(0, -.01f, 0), Mat("4b5e45", 1), null, 32);
            for (int i = 0; i < ReviewClips.Length; i++) {
                var rig = new ActorRig(ReviewClips[i].Skin);
                review.AddChild(rig.Root); rig.Root.Position = ReviewSpot(i);
                reviewRigs.Add(rig);
            }
            for (int i = 0; i < ReviewWolves.Length; i++) {
                var wolf = new WorldModels.WolfModel();
                review.AddChild(wolf.Root); wolf.Root.Position = ReviewSpot(ReviewClips.Length + i);
                reviewWolves.Add(wolf);
            }
        }
        // Moving wolves stride as they would at their speed in the world: a trot at 3 tiles a second and a gallop at a chase's 6.3.
        reviewWolves[0].Idle(Elapsed); reviewWolves[1].Trot(Elapsed * 3 * 5); reviewWolves[2].Gallop(Elapsed * 6.3f * 2.6f);
        reviewWolves[3].Leap(Elapsed % 1.2f / World.LeapSeconds); reviewWolves[4].Feed(Elapsed); reviewWolves[5].Fall(Elapsed % 3 / 1.5f);
        reviewWolves[6].Recoil(Elapsed % 1.2f / World.StaggerSeconds);
        foreach (var wolf in reviewWolves) wolf.Root.Rotation = new Vector3(0, Elapsed * .7f + Mathf.Pi / 4, 0);
        for (int i = 0; i < ReviewClips.Length; i++) {
            var action = ReviewClips[i].Action;
            reviewRigs[i].Root.Rotation = new Vector3(0, Elapsed * .7f + Mathf.Pi / 4, 0);
            reviewRigs[i].Show(ActorAnimation.PoseAt(action, Elapsed, action is ActorAction.Walk or ActorAction.Carry), ActorAnimation.Tool(action), action == ActorAction.Carry, action == ActorAction.Perch);
        }
    }

    private void PaintReview() {
        overlay.DrawString(font, new Vector2(28, 40), "ANIMATION REVIEW · keyframed 3D rig, rotating to show every side", HorizontalAlignment.Left, -1, 23, new Color("f2e8cf"));
        for (int i = 0; i < ReviewClips.Length; i++) {
            var at = camera.UnprojectPosition(ReviewSpot(i));
            var size = font.GetStringSize(ReviewClips[i].Name, HorizontalAlignment.Left, -1, 17);
            overlay.DrawString(font, at + new Vector2(-size.X / 2, 30), ReviewClips[i].Name, HorizontalAlignment.Left, -1, 17, new Color("f2e8cf"));
        }
        for (int i = 0; i < ReviewWolves.Length; i++) {
            var at = camera.UnprojectPosition(ReviewSpot(ReviewClips.Length + i));
            var size = font.GetStringSize(ReviewWolves[i], HorizontalAlignment.Left, -1, 17);
            overlay.DrawString(font, at + new Vector2(-size.X / 2, 30), ReviewWolves[i], HorizontalAlignment.Left, -1, 17, new Color("f2e8cf"));
        }
    }
}
