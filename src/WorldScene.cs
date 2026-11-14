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
        // The meadow's colours: fine grass grain with occasional bare-earth flecks at the top of the ramp.
        var ramp = new Gradient { Offsets = new[] { 0f, .35f, .6f, .84f, .9f, 1f }, Colors = new[] { new Color("72895a"), new Color("7e9563"), new Color("889e6b"), new Color("91a573"), new Color("9a9467"), new Color("a39a6c") } };
        var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = .045f, FractalOctaves = 4, FractalGain = .6f, Seed = 4271 };
        var grass = new NoiseTexture2D { Width = 512, Height = 512, Seamless = true, Noise = noise, ColorRamp = ramp };
        BuildTerrain3D(parent, grass);
        // Sea and its feathered bank follow the coastline at the valley's mouth.
        ShorelineAppearance.BuildCoast(parent, EnvironmentReview);
        BuildLake(parent);
        foreach (var cliff in World.Cliffs) BuildCliff(parent, cliff);
        foreach (var den in World.Dens) BuildDen(parent, den);
        BuildPlacedEnvironment(parent);
    }

    /// <summary>A wolf den: a mine's rock outcrop and dark tunnel at half size, with no timber, rails or cart, so the mouth is about a wolf's height.</summary>
    private static void BuildDen(Node3D parent, Den den) {
        // The outcrop is built around a cliff's mine spot; this cliff puts that spot at the den's.
        var rock = new Cliff(ThingKind.Stone, den.Spot - den.Face * World.MineOut, den.Face, 0);
        var cave = Pivot(parent, new Vector3(den.Spot.X, 0, den.Spot.Y), new Vector3(0, Mathf.RadToDeg(MathF.Atan2(den.Face.X, den.Face.Y)), 0));
        cave.Scale = Vector3.One * Den.Scale;
        Add(cave, MineRockShape.Mesh(rock, new[] { "5b5a52", "66645a", "4f4f48" }, Den.Scale), MineRockShape.Rock, Vector3.Zero);
        Add(Pivot(cave, new Vector3(0, 0, WorldModels.MineDoor)), MineShape.Tunnel, MineShape.Unlit, Vector3.Zero);
    }

    /// <summary>A jagged wall of faceted columns with ore seams on the face and scree at its foot.</summary>
    private static void BuildCliff(Node3D parent, Cliff cliff) {
        // Host rock follows the mountain; distinctive ore colors remain in the seams.
        (string[] rock, var seam, int seed) = cliff.Ore switch {
            ThingKind.Iron => (new[] { "686650", "716b56", "5b5e4c" }, Stone("b4622a", .5f), 11),
            ThingKind.Tin => (new[] { "656d5d", "717665", "586252" }, Stone("e9ebe4", .35f), 37),
            _ => (new[] { "535d4b", "606753", "495440" }, Stone("15161a", .5f, .3f), 23),
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
            Cylinder(wall, .95f + R() * .2f, .55f + R() * .25f, height, new Vector3(x, height / 2, z), Stone(rock[rng.Next(rock.Length)]), new Vector3((R() - .5f) * 8, R() * 360, (R() - .5f) * 8), 5);
            Sphere(wall, .55f + R() * .3f, new Vector3(x + (R() - .5f) * .4f, height + .05f, z - .1f), Stone(rock[rng.Next(rock.Length)]), new Vector3(1.2f, .6f, 1), 5);
            for (int i = 0; i < 2; i++)
                Box(wall, new Vector3(.35f + R() * .3f, .08f + R() * .06f, .12f), new Vector3(x + (R() - .5f) * .5f, .5f + R() * (height - .9f), z + .95f), seam, new Vector3(0, 0, (R() - .5f) * 40));
            for (int i = 0; i < 2; i++)
                Sphere(wall, .14f + R() * .16f, new Vector3(x + (R() - .5f) * .8f, .06f, 1.1f + R() * .6f), Stone(rock[rng.Next(rock.Length)]), new Vector3(1.2f, .6f, 1), 5);
        }
    }

    private void BuildLake(Node3D parent) {
        var at = To3(Terrain.Lake);
        // The bank and water follow the same shore line that bounds walking.
        ShorelineAppearance.BuildLakeBank(parent);
        LakeAppearance.BuildLakeAppearance(parent, EnvironmentReview);
        var rng = new Random(77);
        const int stones = 150;
        for (int i = 0; i < stones; i++) {
            float angle = i * Mathf.Tau / stones + (float)rng.NextDouble() * .03f;
            // Stones collect in irregular patches instead of forming an evenly spaced border.
            if (MathF.Sin(angle * 7 + 1.2f) + .55f * MathF.Sin(angle * 13) < -.05f) continue;
            float r = Terrain.Shore(angle) + .12f + (float)rng.NextDouble() * .95f;
            var stone = Sphere(parent, .1f + (float)rng.NextDouble() * .14f, at + new Vector3(MathF.Cos(angle) * r, .03f, MathF.Sin(angle) * r), Stone(i % 3 == 0 ? "a3a7a3" : "8d918e", .9f), new Vector3(1.2f, .5f, 1), 6);
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

    private static void Scatter(Node3D parent, Mesh mesh, List<(Transform3D At, Color Color)> items, ShaderMaterial material) {
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = items.Count };
        for (int i = 0; i < items.Count; i++) { multi.SetInstanceTransform(i, items[i].At); multi.SetInstanceColor(i, items[i].Color); }
        var instance = new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
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
        ("player jog", ActorSkin.Player, ActorAction.Jog), ("player run", ActorSkin.Player, ActorAction.Run), ("player axe", ActorSkin.Player, ActorAction.Chop), ("player steel axe", ActorSkin.Player, ActorAction.SteelChop),
        ("player pick", ActorSkin.Player, ActorAction.Mine), ("player steel pick", ActorSkin.Player, ActorAction.SteelMine), ("player gather", ActorSkin.Player, ActorAction.Gather),
        ("player hammer", ActorSkin.Player, ActorAction.Hammer), ("player pump", ActorSkin.Player, ActorAction.Pump), ("player punch", ActorSkin.Player, ActorAction.Attack),
        ("player idle", ActorSkin.Player, ActorAction.Idle), ("worker walk", ActorSkin.Worker, ActorAction.Walk), ("worker axe", ActorSkin.Worker, ActorAction.Chop),
        ("worker pick", ActorSkin.Worker, ActorAction.Mine), ("worker carry", ActorSkin.Worker, ActorAction.Carry), ("worker gather", ActorSkin.Worker, ActorAction.Gather),
        ("worker hammer", ActorSkin.Worker, ActorAction.Hammer), ("worker pump", ActorSkin.Worker, ActorAction.Pump), ("worker idle", ActorSkin.Worker, ActorAction.Idle),
        ("worker sit", ActorSkin.Worker, ActorAction.Sit), ("worker sit on rock", ActorSkin.Worker, ActorAction.Perch), ("player pick up", ActorSkin.Player, ActorAction.PickUp), ("player craft", ActorSkin.Player, ActorAction.Craft),
        ("player grind", ActorSkin.Player, ActorAction.Grind), ("worker grind", ActorSkin.Worker, ActorAction.Grind),
        ("player shovel", ActorSkin.Player, ActorAction.Dig), ("worker shovel", ActorSkin.Worker, ActorAction.Dig),
    };
    private static readonly string[] ReviewWolves = { "wolf idle", "wolf trot", "wolf gallop", "wolf leap", "wolf feed", "wolf fall", "wolf hit" };
    private readonly List<ActorRig> reviewRigs = new();
    private readonly List<WolfModel> reviewWolves = new();
    private Node3D? review;
    private static readonly Vector3 ScreenRight = new Vector3(1, 0, -1).Normalized(), ScreenDown = new Vector3(1, 0, 1).Normalized();
    private static Vector3 ReviewCenter => new(0, 1, 0);
    private static Vector3 ReviewSpot(int index) => ScreenRight * ((index % 7 - 3) * 2f) + ScreenDown * ((index / 7 - 2f) * 3.3f);
    /// <summary>The clip <c>--animation-clip</c> names, shown alone and close up, moving across marked ground at its speed in the world; -1 shows every clip.</summary>
    private static readonly int ReviewClip = Array.FindIndex(ReviewClips, clip => clip.Name == Launch.Value("--animation-clip"));
    /// <summary>Wolf close-ups for <c>--animation-clip</c>; the leap, hit and feed play against a player the way SyncWolves sets them up.</summary>
    private static readonly string[] WolfClips = { "wolf idle", "wolf howl", "wolf walk", "wolf trot", "wolf gallop", "wolf speeds", "wolf leap", "wolf hit", "wolf feed", "wolf fall" };
    private static readonly int ReviewWolfClip = Array.IndexOf(WolfClips, Launch.Value("--animation-clip"));
    private string? ReviewCloseUpName => ReviewClip >= 0 ? ReviewClips[ReviewClip].Name : ReviewWolfClip >= 0 ? WolfClips[ReviewWolfClip] : null;
    /// <summary>The camera's pitch in degrees for <c>--animation-pitch</c>, low for a side view; the game's otherwise.</summary>
    private static readonly float ReviewPitch = Launch.Number("--animation-pitch", Pitch);
    /// <summary>Degrees <c>--animation-turn</c> turns the close-up clip's rig from where it moves, to see it from the front or behind.</summary>
    private static readonly float ReviewTurn = Launch.Number("--animation-turn", 0);
    /// <summary>The close-up clip's actor, moved and posed the way the game moves and poses everyone.</summary>
    private readonly ActorState reviewActor = new();

    /// <summary>Tiles a second an action carries an actor across the ground.</summary>
    private float ReviewSpeed(ActorAction action) => action switch {
        ActorAction.Walk or ActorAction.Carry => World.WorkerSpeed,
        ActorAction.Jog => World.Data.MoveSpeed(sprinting: false),
        ActorAction.Run => World.Data.MoveSpeed(sprinting: true),
        _ => 0
    };

    /// <summary>Hides the world and lays plain ground of this radius for the review to stand on.</summary>
    private Node3D ReviewStage(float radius) {
        scene.Visible = false;
        var stage = new Node3D(); AddChild(stage);
        Cylinder(stage, radius, radius, .02f, new Vector3(0, -.01f, 0), Mat("4b5e45", 1), null, 32);
        return stage;
    }

    /// <summary>Plain ground with marks every half metre along the track, which show whether a planted foot slides.</summary>
    private Node3D ReviewTrack() {
        var stage = ReviewStage(60);
        for (int i = -60; i <= 60; i++) Box(stage, new Vector3(.03f, .005f, i % 2 == 0 ? .5f : .25f), ScreenRight * (i * .5f), Mat(i % 2 == 0 ? "d8cfb4" : "8e9478", 1), new Vector3(0, 45, 0));
        return stage;
    }

    private void UpdateClipReview(float dt) {
        var (_, skin, action) = ReviewClips[ReviewClip];
        if (review == null) {
            review = ReviewTrack();
            var rig = new ActorRig(skin);
            review.AddChild(rig.Root); reviewRigs.Add(rig);
        }
        // The actor crosses 40 metres of track, then starts over; the jump back carries its last place with it, so it stays one step.
        var track = new Vector2(ScreenRight.X, ScreenRight.Z);
        float along = Mathf.PosMod(Elapsed * ReviewSpeed(action) + 20, 40) - 20;
        if (reviewActor.Position.Dot(track) > along + 1) reviewActor.Position -= track * 40;
        reviewActor.Update(track * along, action, track * (along + 1), dt);
        Pose(reviewRigs[0], reviewActor, ActorAnimation.Tool(action));
        reviewRigs[0].Root.Rotation += new Vector3(0, Mathf.DegToRad(ReviewTurn), 0);
    }

    private WolfModel? reviewWolf;
    /// <summary>The player a wolf close-up leaps at, punches it or lies under it.</summary>
    private readonly ActorState reviewPerson = new();
    private float reviewGround, reviewLunge;

    /// <summary>The speeds clip's pace: stand, walk, trot, gallop, then slow back down to a stand over 11 seconds.</summary>
    private static float ReviewSpeeds(float time) {
        ReadOnlySpan<float> at = stackalloc float[] { 0, 1, 2, 3.5f, 4.5f, 6, 7, 8.5f, 10, 11 }, speed = stackalloc float[] { 0, 0, 1.1f, 1.1f, 3, 3, 6.3f, 6.3f, 0, 0 };
        for (int i = 1; i < at.Length; i++)
            if (time < at[i]) return Mathf.Lerp(speed[i - 1], speed[i], Ease((time - at[i - 1]) / (at[i] - at[i - 1])));
        return 0;
    }

    /// <summary>
    /// A wolf close-up: moving clips run along the marked track at their pace, and the others play against a player standing at the middle and facing the wolf, as SyncWolves times them.
    /// </summary>
    private void UpdateWolfClipReview(float dt) {
        string clip = WolfClips[ReviewWolfClip];
        var track = new Vector2(ScreenRight.X, ScreenRight.Z);
        if (review == null) {
            review = ReviewTrack();
            reviewWolf = new WolfModel(); review.AddChild(reviewWolf.Root);
            if (clip is "wolf leap" or "wolf hit" or "wolf feed") { var rig = new ActorRig(ActorSkin.Player); review.AddChild(rig.Root); reviewRigs.Add(rig); }
        }
        var wolf = reviewWolf!;
        // Moving wolves head along the track; the rest face back toward the player at the middle.
        bool moving = clip is "wolf walk" or "wolf trot" or "wolf gallop" or "wolf speeds";
        wolf.Root.Rotation = new Vector3(0, ActorAnimation.Yaw(moving ? track : -track, 0) + Mathf.DegToRad(ReviewTurn), 0);
        var place = Vector3.Zero;
        var person = ActorAction.Idle;
        float downed = 0;
        switch (clip) {
            case "wolf idle": wolf.Stand(Elapsed, dt, Elapsed % 8 > 4); break;
            case "wolf howl": wolf.Howl(Elapsed % (World.Data.LoneHowlSeconds + 1) / World.Data.LoneHowlSeconds, dt); break;
            case "wolf fall": {
                    // Thrown back by the killing blow while in the air, as a carcass is.
                    float dying = Math.Clamp((Elapsed % 2.2f - .4f) / WolfModel.FallSeconds, 0, 1), thrown = Math.Min(1, dying / WolfModel.ThrowShare);
                    wolf.Fall(dying); place = To3(track * (.7f * thrown * (2 - thrown) - .35f));
                    break;
                }
            case "wolf feed":
                wolf.Feed(Elapsed, dt, false); place = To3(track * World.FeastSeat);
                person = ActorAction.Devoured; downed = 1;
                break;
            case "wolf leap": {
                    // From 3 tiles off the leap closes on PinDistance in LeapSeconds, the jaws reaching the throat on the way.
                    float c = Elapsed % 3.2f, start = 3, progress = Math.Clamp((c - .6f) / World.LeapSeconds, 0, 1), flight = 0;
                    float gap = Mathf.Lerp(start, World.PinDistance, progress), contact = (start - WolfModel.ContactReach) / (start - World.PinDistance);
                    wolf.Toward = track;
                    if (c < .6f) { wolf.Pinned = 0; wolf.Stand(Elapsed, dt, true); } else {
                        wolf.Pinned = gap <= WolfModel.ContactReach ? wolf.Pinned + dt : 0;
                        if (wolf.Pinned > 0) wolf.Feed(Elapsed, dt, true, wolf.Pinned / KnockSeconds);
                        else { wolf.Leap(progress, contact, dt); flight = progress / contact; }
                    }
                    place = c < .6f ? To3(track * start) : AtTheThroat(wolf, Vector2.Zero, To3(track * gap), flight);
                    if (wolf.Pinned > 0) { person = ActorAction.Mauled; downed = wolf.Pinned; }
                    break;
                }
            case "wolf hit": {
                    // A punch from 1.8 tiles: the wolf lunges into it, then hops KnockBackDistance clear once it lands.
                    float c = Elapsed % 2.4f, punch = c - .4f, lands = ActorAnimation.Contact(ActorAction.Attack), near = 1.8f, far = near + World.KnockBackDistance, gap;
                    if (punch < 0) { wolf.Stand(Elapsed, dt, true); gap = near; } else if (punch < lands) { gap = Mathf.Lerp(near, WolfModel.StrikeReach, Ease(punch / lands)); wolf.Move(reviewLunge - gap, dt, false, true); } else if (punch < lands + WolfModel.RecoilSeconds) {
                        float recoil = (punch - lands) / WolfModel.RecoilSeconds, hop = Math.Min(1, recoil / WolfModel.HopShare);
                        gap = far - (far - WolfModel.StrikeReach) * (1 - hop) * (1 - hop);
                        wolf.Recoil(recoil, dt);
                    } else { wolf.Stand(Elapsed, dt, true); gap = far; }
                    reviewLunge = gap; place = To3(track * gap);
                    if (punch >= 0 && punch < ActorAnimation.Duration(ActorAction.Attack)) person = ActorAction.Attack;
                    break;
                }
            default: {
                    float speed = clip switch { "wolf walk" => 1.5f, "wolf trot" => 3, "wolf gallop" => 6.3f, _ => ReviewSpeeds(Elapsed % 11) };
                    // The wolf crosses 40 tiles of track, then starts over.
                    reviewGround = Mathf.PosMod(reviewGround + speed * dt + 20, 40) - 20;
                    if (speed < .05f) wolf.Stand(Elapsed, dt, false); else wolf.Move(speed * dt, dt, speed > 4.5f, false);
                    place = To3(track * reviewGround);
                    break;
                }
        }
        wolf.Root.Position = place;
        // The camera keeps the wolf in view, and the player too while one is in the clip.
        reviewActor.Position = reviewRigs.Count > 0 ? track * 1.1f : new Vector2(place.X, place.Z);
        if (reviewRigs.Count == 0) return;
        reviewPerson.Update(Vector2.Zero, person, track, dt);
        Pose(reviewRigs[0], reviewPerson, null);
        if (downed > 0) KnockDown(reviewRigs[0], reviewPerson, downed);
    }

    private void UpdateReview(float dt) {
        if (ReviewClip >= 0) { UpdateClipReview(dt); return; }
        if (ReviewWolfClip >= 0) { UpdateWolfClipReview(dt); return; }
        if (review == null) {
            review = ReviewStage(40);
            for (int i = 0; i < ReviewClips.Length; i++) {
                var rig = new ActorRig(ReviewClips[i].Skin);
                review.AddChild(rig.Root); rig.Root.Position = ReviewSpot(i);
                reviewRigs.Add(rig);
            }
            for (int i = 0; i < ReviewWolves.Length; i++) {
                var wolf = new WolfModel();
                review.AddChild(wolf.Root); wolf.Root.Position = ReviewSpot(ReviewClips.Length + i);
                reviewWolves.Add(wolf);
            }
        }
        // Moving wolves stride as they would at their speed in the world: a trot at 3 tiles a second and a gallop at a chase's 6.3.
        reviewWolves[0].Stand(Elapsed, dt, false); reviewWolves[1].Move(3 * dt, dt, false, false); reviewWolves[2].Move(6.3f * dt, dt, true, false);
        float leap = Elapsed % 1.2f / World.LeapSeconds;
        if (leap < .7f) reviewWolves[3].Leap(leap, .7f, dt); else reviewWolves[3].Feed(Elapsed, dt, true, (leap - .7f) * World.LeapSeconds / KnockSeconds);
        reviewWolves[4].Feed(Elapsed, dt, false); reviewWolves[5].Fall(Elapsed % 2 / WolfModel.FallSeconds);
        reviewWolves[6].Recoil(Elapsed % 1.2f / WolfModel.RecoilSeconds, dt);
        foreach (var wolf in reviewWolves) wolf.Root.Rotation = new Vector3(0, Elapsed * .7f + Mathf.Pi / 4, 0);
        for (int i = 0; i < ReviewClips.Length; i++) {
            var action = ReviewClips[i].Action;
            reviewRigs[i].Root.Rotation = new Vector3(0, Elapsed * .7f + Mathf.Pi / 4, 0);
            reviewRigs[i].Show(ActorAnimation.PoseAt(action, Elapsed, action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run or ActorAction.Carry), ActorAnimation.Tool(action), action == ActorAction.Carry, action == ActorAction.Perch);
        }
    }

    private void PaintReview() {
        if (ReviewCloseUpName is string name) {
            overlay.DrawString(font, new Vector2(28, 40), $"{name} · {Elapsed:0.000} s", HorizontalAlignment.Left, -1, 23, new Color("f2e8cf"));
            return;
        }
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
