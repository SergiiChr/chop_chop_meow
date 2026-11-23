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
        terrainView = new TerrainView(parent, camera, sun, grass, disturbanceTexture, sceneryShadeTexture, objectShadeTexture, World.Player);
        // Sea and its feathered bank follow the coastline at the valley's mouth.
        ShorelineAppearance.BuildCoast(parent, EnvironmentReview);
        LakeDressing.Build(parent, EnvironmentReview);
        foreach (var cliff in Cliff.All) BuildCliff(parent, cliff);
        foreach (var den in WolfPatrols.Dens) BuildDen(parent, den);
        BuildPlacedEnvironment(parent);
    }

    /// <summary>A wolf den: a mine's rock outcrop and dark tunnel at half size, with no timber, rails or cart, so the mouth is about a wolf's height.</summary>
    private static void BuildDen(Node3D parent, Den den) {
        // The outcrop is built around a cliff's mine spot; this cliff puts that spot at the den's.
        var rock = new Cliff(ThingKind.Stone, den.Spot - den.Face * Cliff.MineOut, den.Face, 0);
        var cave = Pivot(parent, new Vector3(den.Spot.X, 0, den.Spot.Y)); cave.Rotation = MineModel.Yaw(den.Face);
        cave.Scale = Vector3.One * Den.Scale;
        Add(cave, MineRockShape.Mesh(rock, new[] { "5b5a52", "66645a", "4f4f48" }, Den.Scale), MineRockShape.Rock, Vector3.Zero);
        Add(Pivot(cave, new Vector3(0, 0, MineModel.PortalDepth)), MineShape.Tunnel, MineShape.Unlit, Vector3.Zero);
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
        var wall = Pivot(parent, new Vector3(cliff.Center.X, 0, cliff.Center.Y)); wall.Rotation = MineModel.Yaw(cliff.Face);
        MineModel.Outcrop(parent, cliff, rock);
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
        effects = new Node3D { Name = "Effects" }; parent.AddChild(effects);
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
        ("player jog", ActorSkin.Player, ActorAction.Jog), ("player run", ActorSkin.Player, ActorAction.Run), ("player axe", ActorSkin.Player, ActorAction.Chop), ("player steel axe", ActorSkin.Player, ActorAction.SteelChop), ("player axe swing", ActorSkin.Player, ActorAction.SteelAxeSwing), ("player axe sweep", ActorSkin.Player, ActorAction.SteelAxeSweep), ("player torch swing", ActorSkin.Player, ActorAction.TorchSwing),
        ("player pick", ActorSkin.Player, ActorAction.Mine), ("player steel pick", ActorSkin.Player, ActorAction.SteelMine), ("player gather", ActorSkin.Player, ActorAction.Gather),
        ("player hammer", ActorSkin.Player, ActorAction.Hammer), ("player pump", ActorSkin.Player, ActorAction.Pump), ("player punch", ActorSkin.Player, ActorAction.Attack),
        ("player idle", ActorSkin.Player, ActorAction.Idle), ("worker walk", ActorSkin.Worker, ActorAction.Walk), ("worker axe", ActorSkin.Worker, ActorAction.Chop),
        ("worker pick", ActorSkin.Worker, ActorAction.Mine), ("worker carry", ActorSkin.Worker, ActorAction.Carry), ("worker gather", ActorSkin.Worker, ActorAction.Gather),
        ("worker hammer", ActorSkin.Worker, ActorAction.Hammer), ("worker pump", ActorSkin.Worker, ActorAction.Pump), ("worker idle", ActorSkin.Worker, ActorAction.Idle),
        ("worker sit", ActorSkin.Worker, ActorAction.Sit), ("worker sit on rock", ActorSkin.Worker, ActorAction.Perch), ("player pick up", ActorSkin.Player, ActorAction.PickUp), ("player craft", ActorSkin.Player, ActorAction.Craft),
        ("player grind", ActorSkin.Player, ActorAction.Grind), ("worker grind", ActorSkin.Worker, ActorAction.Grind),
        ("player shovel", ActorSkin.Player, ActorAction.Dig), ("worker shovel", ActorSkin.Worker, ActorAction.Dig),
        ("player jog turns", ActorSkin.Player, ActorAction.Jog), ("player run turns", ActorSkin.Player, ActorAction.Run),
        ("player cabin", ActorSkin.Player, ActorAction.Walk), ("player jog back", ActorSkin.Player, ActorAction.Jog), ("player run back", ActorSkin.Player, ActorAction.Run),
        ("player barrel", ActorSkin.Player, ActorAction.Walk), ("worker barrel", ActorSkin.Worker, ActorAction.Carry),
    };
    /// <summary>Seconds each key is held in a back clip, which presses D and A in turn.</summary>
    private const float ReviewBackSeconds = 1.2f;
    /// <summary>The back clips' movement, braking at each change of key as the player's does.</summary>
    private Momentum reviewMomentum;
    /// <summary>The cabin clip's stages and when each starts, in seconds of its loop: up to the door, in at a walk, out, knock on the door, in at a run.</summary>
    private static readonly (float At, Doorway Stage)[] ReviewDoorway = { (0, Doorway.None), (2, Doorway.Entering), (4, Doorway.Leaving), (6, Doorway.Knocking), (7.5f, Doorway.Tugged) };
    private const float ReviewDoorwayLoop = 10.5f;
    private HutProp? reviewCabin;
    /// <summary>The cabin the cabin clip plays at, at the middle of the ground.</summary>
    private static readonly Thing ReviewHut = new() { Kind = ThingKind.Hut };
    /// <summary>How far from the cabin clip's door the player is still seen, before the walls hide them.</summary>
    private const float ReviewSeen = 1.75f;
    /// <summary>Seconds each key is held in a turns clip, which presses A, W, D and S in turn.</summary>
    private const float ReviewKeySeconds = .35f;
    private static readonly string[] ReviewWolves = { "wolf idle", "wolf trot", "wolf gallop", "wolf leap", "wolf feed", "wolf fall", "wolf hit" };
    private readonly List<ActorRig> reviewRigs = new();
    private readonly List<WolfModel> reviewWolves = new();
    private Node3D? review;
    private static readonly Vector3 ScreenRight = new Vector3(1, 0, -1).Normalized(), ScreenDown = new Vector3(1, 0, 1).Normalized();
    private static Vector3 ReviewCenter => new(0, 1, 0);
    private static Vector3 ReviewSpot(int index) => ScreenRight * ((index % 7 - 3) * 2f) + ScreenDown * ((index / 7 - 2f) * 3.3f);
    /// <summary>The clip <c>--animation-clip</c> names, shown alone and close up, moving across marked ground at its speed in the world; -1 shows every clip.</summary>
    private static readonly string? ReviewRequestedClip = Launch.Value("--animation-clip");
    private static readonly int ReviewClip = Array.FindIndex(ReviewClips, clip => clip.Name == ReviewRequestedClip);
    /// <summary>Wolf close-ups for <c>--animation-clip</c>; the leap, hit, feed, snap and brace play against a player the way SyncWolves sets them up.</summary>
    /// <summary>How fast, in metres a second, a wolf in the review gallops, and how many seconds the turn clip takes to go out and back.</summary>
    private const float ReviewGallop = 6.3f, ReviewTurnLoop = 4;
    private static readonly string[] WolfClips = { "wolf idle", "wolf howl", "wolf walk", "wolf trot", "wolf gallop", "wolf speeds", "wolf turn", "wolf leap", "wolf hit", "wolf feed", "wolf fall", "wolf snap", "wolf brace" };
    private static readonly int ReviewWolfClip = Array.IndexOf(WolfClips, ReviewRequestedClip);
    private static readonly string? WaterReview = ReviewRequestedClip switch {
        "worker bucket roof" => Bucket.AtFill(Bucket.Capacity),
        "worker bottle roof" => Water.Full,
        _ => null
    };
    private string? ReviewCloseUpName => WaterReview != null ? ReviewRequestedClip : ReviewClip >= 0 ? ReviewClips[ReviewClip].Name : ReviewWolfClip >= 0 ? WolfClips[ReviewWolfClip] : null;
    /// <summary>The camera's pitch in degrees for <c>--animation-pitch</c>, low for a side view; the game's otherwise.</summary>
    private static readonly float ReviewPitch = Launch.Number("--animation-pitch", Pitch);
    /// <summary>How many times closer <c>--animation-zoom</c> brings the review's camera, for a look at a face or a paw.</summary>
    private static readonly float ReviewZoom = Launch.Number("--animation-zoom", 1);
    /// <summary>Degrees <c>--animation-turn</c> turns the close-up clip's rig from where it moves, to see it from the front or behind.</summary>
    private static readonly float ReviewTurn = Launch.Number("--animation-turn", 0);
    /// <summary>
    /// How far from the close-up's actor <c>--animation-target</c> stands what a blow works, the tree, rock, anvil, bench or stump it
    /// lands on, in metres from its spot; by default as far as a player stops from it to work it.
    /// </summary>
    private static readonly float ReviewTargetAt = Launch.Number("--animation-target", 1.3f);
    /// <summary>What the close-up's blow works, and its model, built once the clip starts.</summary>
    private (Thing Thing, Prop Prop)? reviewTarget;
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
            // A blow plays against what it works, standing where it does when the player stops to work it and turned the way the views turn the actor.
            if (ReviewWorked(action) is ThingKind kind) {
                var target = new Thing { Id = ReviewTargetId, Kind = kind, Remaining = 1, Pos = new Vector2(ScreenRight.X, ScreenRight.Z).Rotated(-Mathf.DegToRad(ReviewTurn)) * ReviewTargetAt };
                var prop = WorldModels.Build(target);
                prop.Root.Position = To3(target.Pos);
                // A stump stands where its tree's trunk did, as the world view places it.
                if (prop is DugProp dug) prop.Body.Position = dug.Seat;
                // A tree's leaves would hide the swing: only its trunk and branches show, which the leaves are drawn by their own shader over.
                if (prop is TreeProp tree)
                    foreach (var part in tree.Sway.GetChildren()) if (part is MeshInstance3D mesh && mesh.GetActiveMaterial(0) is ShaderMaterial) mesh.Visible = false;
                review.AddChild(prop.Root);
                reviewTarget = (target, prop);
            }
        }
        if (ReviewClips[ReviewClip].Name == "player cabin") { ReviewCabin(dt); return; }
        if (ReviewClips[ReviewClip].Name.EndsWith(" back")) {
            // D then A, each held a while, run the actor one way, brake it, turn it round and run it back.
            var wanted = PlayerControl.KeyDirection((int)(Elapsed / ReviewBackSeconds) % 2 == 0 ? 1 : -1, 0);
            var step = reviewMomentum.Step(wanted, dt) * ReviewSpeed(action) * dt;
            var shown = reviewMomentum.Braking ? ActorAction.Brake : action;
            reviewActor.Update(reviewActor.Position + step, shown, reviewActor.Position + reviewMomentum.Heading, dt);
            Pose(reviewRigs[0], reviewActor, null);
            return;
        }
        if (ReviewClips[ReviewClip].Name.EndsWith(" turns")) {
            // A, W, D then S, each held a moment, turn the actor about a small square the way the keys turn the player.
            int key = (int)(Elapsed / ReviewKeySeconds) % 4;
            var heading = key switch { 0 => PlayerControl.KeyDirection(-1, 0), 1 => PlayerControl.KeyDirection(0, -1), 2 => PlayerControl.KeyDirection(1, 0), _ => PlayerControl.KeyDirection(0, 1) };
            reviewActor.Update(reviewActor.Position + heading * ReviewSpeed(action) * dt, action, reviewActor.Position + heading, dt);
            Pose(reviewRigs[0], reviewActor, null);
            return;
        }
        if (reviewTarget is var (worked, model)) {
            // The actor stands facing what it works and lands each blow on it, as the game has them.
            reviewActor.Update(Vector2.Zero, action, worked.Pos, dt);
            reviewActor.Mark = strikes.At(worked, reviewActor.Position, action, model);
            Pose(reviewRigs[0], reviewActor, Grade(ActorAnimation.Tool(action), null, null));
            return;
        }
        // The actor crosses 40 metres of track, then starts over; the jump back carries its last place with it, so it stays one step.
        var track = new Vector2(ScreenRight.X, ScreenRight.Z);
        float speed = ReviewClips[ReviewClip].Name == "player barrel"
            ? World.Data.WalkSpeed * Barrel.Speed(Barrel.AtFill(Barrel.Capacity)) : ReviewSpeed(action);
        float along = Mathf.PosMod(Elapsed * speed + 20, 40) - 20;
        if (reviewActor.Position.Dot(track) > along + 1) reviewActor.Position -= track * 40;
        reviewActor.Update(track * along, action, track * (along + 1), dt);
        Pose(reviewRigs[0], reviewActor, ActorAnimation.Tool(action), torch: action == ActorAction.TorchSwing,
            barrel: ReviewClips[ReviewClip].Name.EndsWith(" barrel"));
        reviewRigs[0].Flame.Visible = action == ActorAction.TorchSwing;
        reviewRigs[0].Root.Rotation += new Vector3(0, Mathf.DegToRad(ReviewTurn), 0);
    }

    /// <summary>The id the close-up's target is built with, which picks its look: a tree of middling height, and a rock and a stump like it.</summary>
    private const int ReviewTargetId = 1035;

    /// <summary>What a close-up's blow lands on, or null for an action that lands on nothing.</summary>
    private static ThingKind? ReviewWorked(ActorAction action) =>
        ActorAnimation.Works(action) is [var kind, ..] ? kind : null;

    /// <summary>
    /// A player walks up to a cabin at the middle of the ground and through each doorway stage as the game shows them:
    /// in at a walk, out, knocking on the shut door, and in at a run after a moment of being tugged back.
    /// </summary>
    private void ReviewCabin(float dt) {
        if (reviewCabin == null) { reviewCabin = (HutProp)WorldModels.Build(ReviewHut); review!.AddChild(reviewCabin.Root); }
        var door = Doorways.DoorPoint(ReviewHut);
        float time = Elapsed % ReviewDoorwayLoop;
        // Starting over puts the player back on the ground in front of the cabin.
        if (time < dt) reviewActor.OnSteps = false;
        int stage = ReviewDoorway.Length - 1;
        while (ReviewDoorway[stage].At > time) stage--;
        float start = ReviewDoorway[stage].At, end = stage + 1 < ReviewDoorway.Length ? ReviewDoorway[stage + 1].At : ReviewDoorwayLoop;
        var now = ReviewDoorway[stage].Stage;
        (Vector2 At, Vector2 Facing, ActorAction Action, bool Backing) shown;
        if (now == Doorway.None) {
            float walked = Math.Clamp((time - start) / 1.6f, 0, 1);
            var from = door - DoorwayPath.IntoCabin * 2;
            shown = (from.Lerp(door, walked), door + DoorwayPath.IntoCabin, walked < 1 ? ActorAction.Jog : ActorAction.Idle, false);
        } else if (now == Doorway.Tugged) {
            // Half way into a run in, the player is tugged back out a metre, then runs in from there as one who goes for the cabin again.
            float run = time - start, tugged = run - Doorways.RushSeconds / 2;
            var tuggedTo = door + Doorways.OutOfDoor * DoorTug.TugDistance;
            var part = run < Doorways.RushSeconds / 2 ? Doorway.Entering : tugged < DoorTug.TugSeconds ? Doorway.Tugged : Doorway.Entering;
            if (part != reviewActor.Stage) { reviewActor.Stage = part; reviewActor.StageFrom = reviewActor.Position; }
            shown = run < Doorways.RushSeconds / 2 ? DoorwayView.DoorStep(Doorway.Entering, door, door, reviewActor.StageFrom, run, Doorways.RushSeconds, true, 0)
                : tugged < DoorTug.TugSeconds ? DoorwayView.DoorStep(Doorway.Tugged, door, door, reviewActor.StageFrom, tugged, DoorTug.TugSeconds, true, DoorTug.TugDistance)
                : DoorwayView.DoorStep(Doorway.Entering, door, tuggedTo, reviewActor.StageFrom, tugged - DoorTug.TugSeconds, Doorways.RushSeconds, true, 0);
        } else {
            if (now != reviewActor.Stage) { reviewActor.Stage = now; reviewActor.StageFrom = reviewActor.Position; }
            float seconds = now switch { Doorway.Knocking => Doorways.KnockLead + Doorways.KnockSeconds, Doorway.Leaving => Doorways.LeaveSeconds, _ => Doorways.EnterSeconds };
            shown = DoorwayView.DoorStep(now, door, door, reviewActor.StageFrom, time - start, seconds, false, 0);
        }
        bool open = now is Doorway.Entering or Doorway.Leaving or Doorway.Tugged && shown.At.DistanceTo(door) < ReviewSeen;
        DoorwayView.SwingDoor(reviewCabin, open, dt, Doorways.ShutSeconds, false);
        reviewActor.Update(shown.At, shown.Action, shown.Facing, dt, shown.Backing);
        Pose(reviewRigs[0], reviewActor, null, door: new DoorShown(shown.At, shown.Facing, shown.Action, door, shown.Backing, 0, 0, 0), dt: dt);
        reviewRigs[0].Root.Visible = shown.At.DistanceTo(door) < ReviewSeen || now is Doorway.None or Doorway.Knocking;
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
            if (clip is "wolf leap" or "wolf hit" or "wolf feed" or "wolf snap" or "wolf brace") { var rig = new ActorRig(ActorSkin.Player); review.AddChild(rig.Root); reviewRigs.Add(rig); }
            // The player starts facing the wolf, rather than turning to it on the spot, or on the ground in the feeding clip.
            reviewPerson.Yaw = ActorAnimation.Yaw(track, 0);
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
                    wolf.Fall(dying); place = To3(track * (.7f * thrown - .35f));
                    break;
                }
            case "wolf feed": {
                    // Tearing at the side of the fallen player's belly.
                    wolf.Feed(Elapsed, dt, false);
                    (place, wolf.Yaw) = FeedingSpots.OnTheBody(FeedingSpots.BodyLine(reviewPerson), .4f, 1);
                    wolf.Root.Rotation = new Vector3(0, wolf.Yaw, 0);
                    person = ActorAction.Devoured; downed = 1;
                    break;
                }
            case "wolf leap": {
                    float wind = Elapsed % 3.2f - .6f, air = wind - WolfSnap.Windup;
                    float progress = Math.Clamp(air / World.LeapSeconds, 0, 1);
                    float gap = Mathf.Lerp(3, WolfSnap.ThroatJawsAhead + .12f, progress);
                    place = To3(track * gap, Movement.Rise(progress * World.LeapSeconds, World.LeapSeconds));
                    wolf.Toward = track;
                    if (wind < 0) {
                        wolf.Pinned = 0; wolf.Stand(Elapsed, dt, true);
                    } else if (air <= 0) wolf.Snap(wind / WolfSnap.Windup, 0, 0, dt);
                    else if (progress < 1) wolf.Leap(progress, dt, jawsUp: FallBackView.Throat(Vector2.Zero, Vector2.Up, 0).Y);
                    else {
                        wolf.Pinned += dt;
                        PinAt(wolf, Vector2.Zero, FeedingSpots.PawsOnChest(wolf, reviewRigs[0]), Elapsed, dt);
                        place = AtTheThroat(wolf, Vector2.Zero);
                        person = wolf.Pinned < ActorAnimation.FallBackSeconds ? ActorAction.FallBack : ActorAction.Mauled;
                        downed = wolf.Pinned;
                    }
                    break;
                }
            case "wolf hit": {
                    // A punch at a wolf at biting distance: when it lands the wolf hops KnockBackDistance clear.
                    float c = Elapsed % 2.4f, punch = c - .4f, lands = ActorAnimation.Contact(ActorAction.Attack), near = WolfBodies.StandOff, far = near + World.KnockBackDistance, gap;
                    if (punch < lands) { wolf.Stand(Elapsed, dt, true); gap = near; } else if (punch < lands + WolfModel.RecoilSeconds) {
                        float recoil = (punch - lands) / WolfModel.RecoilSeconds, hop = Math.Min(1, recoil / WolfModel.HopShare);
                        gap = near + (far - near) * hop;
                        wolf.Recoil(recoil, dt);
                    } else { wolf.Stand(Elapsed, dt, true); gap = far; }
                    place = To3(track * gap);
                    if (punch >= 0 && punch < ActorAnimation.Duration(ActorAction.Attack)) person = ActorAction.Attack;
                    break;
                }
            case "wolf snap": {
                    // A snap at a player standing still: the wolf gathers itself at biting distance, leaps the last of the way and shuts its jaws on them, then steps back.
                    float c = Elapsed % 2.4f, snap = c - .6f, landed = snap - WolfSnap.Windup - WolfSnap.Flight, near = WolfSnap.Trigger - .02f, close = WolfSnap.JawsAhead + .12f, gap;
                    if (snap < 0) { wolf.Stand(Elapsed, dt, true); gap = near; } else if (landed < 0) {
                        float drive = Math.Max(0, snap - WolfSnap.Windup) / WolfSnap.Flight;
                        gap = Mathf.Lerp(near, close, drive);
                        wolf.Snap(Math.Min(1, snap / WolfSnap.Windup), drive, 0, dt);
                    } else if (landed < WolfSnap.HitRecovery) { gap = close; wolf.Snap(1, 1, landed / WolfModel.SettleSeconds, dt); } else {
                        gap = Math.Min(near, reviewLunge + WolfBodies.BackOffSpeed * dt);
                        if (gap < near) wolf.Move(reviewLunge - gap, dt, false, true); else wolf.Stand(Elapsed, dt, true);
                    }
                    reviewLunge = gap; place = To3(track * gap);
                    break;
                }
            case "wolf brace": {
                    // A following wolf the player has walked up to: wary for three seconds, then braced where it stands.
                    if (Elapsed % 8 < 3) wolf.Stand(Elapsed, dt, true); else wolf.Brace(Elapsed, dt);
                    place = To3(track * WolfFollow.StandOrRunAt);
                    break;
                }
            case "wolf turn": {
                    // Galloping along the track, then running back the other way: the head turns first, the body follows
                    // leaning in, and it brakes while its body still faces the way it came.
                    float way = Elapsed % ReviewTurnLoop < ReviewTurnLoop / 2 ? 1 : -1;
                    var moved = track * (way * ReviewGallop * dt);
                    reviewGround += way * ReviewGallop * dt;
                    if (wolf.TurnToward(moved, moved, dt, true, true, false)) wolf.Brake(moved.Length(), dt); else wolf.Move(moved.Length(), dt, true, false);
                    wolf.Root.Rotation += new Vector3(0, Mathf.DegToRad(ReviewTurn), 0);
                    place = To3(track * reviewGround);
                    break;
                }
            default: {
                    float speed = clip switch { "wolf walk" => 1.5f, "wolf trot" => 3, "wolf gallop" => ReviewGallop, _ => ReviewSpeeds(Elapsed % 11) };
                    // The wolf crosses 40 tiles of track, then starts over.
                    reviewGround = Mathf.PosMod(reviewGround + speed * dt + 20, 40) - 20;
                    if (speed < .05f) wolf.Stand(Elapsed, dt, false); else wolf.Move(speed * dt, dt, speed > 4.5f, false);
                    place = To3(track * reviewGround);
                    break;
                }
        }
        wolf.Root.Position = place;
        wolf.Plant();
        // The camera keeps the wolf in view, and the player too while one is in the clip.
        reviewActor.Position = reviewRigs.Count > 0 ? track * 1.1f : new Vector2(place.X, place.Z);
        if (reviewRigs.Count == 0) return;
        reviewPerson.Update(Vector2.Zero, person, track, dt);
        Pose(reviewRigs[0], reviewPerson, null);
        if (downed > 0) FallBackView.FallBack(reviewRigs[0], reviewPerson, reviewPerson.Pose, downed, false, clip == "wolf leap" ? wolf : null);
    }

    private void UpdateWaterReview(string vessel) {
        if (review == null) {
            review = ReviewStage(30);
            var rig = new ActorRig(ActorSkin.Worker);
            review.AddChild(rig.Root); reviewRigs.Add(rig);
            var cabin = (HutProp)WorldModels.Build(ReviewHut);
            review.AddChild(cabin.Root);
            cabin.RoofBarrel.Show();
        }
        const float climb = BarrelHaul.ClimbSeconds, pour = BarrelHaul.PourSeconds, cycle = climb * 2 + pour + .5f;
        float time = Elapsed % cycle;
        var action = time < climb ? WorkerWaterAction.ClimbUp : time < climb + pour ? WorkerWaterAction.Pour : WorkerWaterAction.ClimbDown;
        float progress = action switch {
            WorkerWaterAction.ClimbUp => time / climb,
            WorkerWaterAction.Pour => (time - climb) / pour,
            _ => Math.Min(1, (time - climb - pour) / climb)
        };
        var rigShown = reviewRigs[0];
        if (action == WorkerWaterAction.ClimbDown || action == WorkerWaterAction.Pour && progress > .60f)
            vessel = Bucket.Is(vessel) ? Bucket.Item : Water.Empty;
        WorkerWaterView.Show(rigShown, Vector2.Zero, Vector3.Zero, action, roof: true, progress, vessel);
        var at = rigShown.Root.Position;
        reviewActor.Position = new Vector2(at.X, at.Z);
        reviewActor.Lift = at.Y;
    }

    private void UpdateReview(float dt) {
        if (WaterReview is string vessel) { UpdateWaterReview(vessel); return; }
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
        if (leap < .7f) reviewWolves[3].Leap(leap / .7f, dt); else reviewWolves[3].Feed(Elapsed, dt, true, (leap - .7f) * World.LeapSeconds / ActorAnimation.FallBackSeconds);
        reviewWolves[4].Feed(Elapsed, dt, false); reviewWolves[5].Fall(Elapsed % 2 / WolfModel.FallSeconds);
        reviewWolves[6].Recoil(Elapsed % 1.2f / WolfModel.RecoilSeconds, dt);
        foreach (var wolf in reviewWolves) wolf.Root.Rotation = new Vector3(0, Elapsed * .7f + Mathf.Pi / 4, 0);
        for (int i = 0; i < ReviewClips.Length; i++) {
            var action = ReviewClips[i].Action;
            reviewRigs[i].Root.Rotation = new Vector3(0, Elapsed * .7f + Mathf.Pi / 4, 0);
            var pose = ActorAnimation.PoseAt(action, Elapsed, action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run or ActorAction.Carry);
            if (ReviewClips[i].Name.EndsWith(" barrel")) reviewRigs[i].HugBarrel(pose);
            else reviewRigs[i].Show(pose, ActorAnimation.Tool(action), action == ActorAction.Carry, action == ActorAction.Perch);
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
