using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Godot;
using static ChopChopMeow.ModelKit;
using RigPose = ChopChopMeow.Pose;

namespace ChopChopMeow;

/// <summary>
/// The intro film: the hero's last day at the office, the trip out, his arrival at the shore and his first night by a fire.
/// <c>--render-intro DIR</c> plays it in place of a game, under a film camera at eye level rather than the game's view from above.
/// Each frame is saved to DIR with a cue sheet of its sounds; a movie tool outside this project turns them into <c>assets/intro.ogv</c>.
/// With <c>--intro-dance</c> it draws only the catgirl's solo on the hero's screen, as tall as the picture, once.
/// </summary>
public partial class WorldView {
    public const int IntroFps = 30;
    /// <summary>The film's frame size before it is reduced to the playback size.</summary>
    public static readonly Vector2I IntroSize = new(1280, 720);
    /// <summary>The width of the film's paint filter before the frames are reduced to their playback size.</summary>
    public const int IntroPaintWidth = 21;
    private const float IntroStep = 1f / IntroFps;
    /// <summary>The cue sheet beside the frames: when each sound starts, how loud, and how it is treated.</summary>
    public const string IntroCues = "cues.tsv";

    /// <summary>Whether the film is playing, which holds the game's own camera, actors and frame updates still.</summary>
    private bool filming;
    /// <summary>True on the first frame after a cut.</summary>
    private bool cut;
    private float filmTime;
    private CameraAttributesPractical filmLens = null!;
    private readonly StringBuilder cues = new();
    private Node3D film = null!, heroTie = null!, heldBat = null!, mouseFingers = null!, indexFinger = null!;
    private ActorRig hero = null!, boss = null!;
    private readonly List<MeshInstance3D> heroTrunkMeshes = [];
    private FilmFace heroFace = null!;
    private BossFace bossFace = null!;
    private IntroLandscape landscape = null!;
    private readonly WolfModel[] nightWolves = new WolfModel[2];
    private OmniLight3D wolfRim = null!;
    private readonly ActorState heroStep = new();
    private IntroSets.Office office = null!;
    private IntroSets.Room room = null!;
    private IntroSets.Alley alley = null!;
    private IntroSets.Train train = null!;
    private Node3D skyline = null!;
    private MeshInstance3D skyDome = null!, moon = null!;
    private ShaderMaterial skyColors = null!;
    private ColorRect black = null!;
    private CanvasLayer filmLayer = null!;
    private Thing campfire = null!;
    private readonly List<ShaderMaterial> waters = new();
    private static readonly StringName SkyTop = "top", SkyHorizon = "horizon", SkyStars = "stars";

    // A dome around the camera: the game's own sky is one flat colour, which only a view from above can hide.
    private const string SkyShader = """
        shader_type spatial;
        render_mode unshaded, cull_front, fog_disabled, shadows_disabled;
        uniform vec3 top : source_color;
        uniform vec3 horizon : source_color;
        uniform float stars = 0.0;
        varying vec3 toward;
        void vertex() {
            toward = normalize(VERTEX);
        }
        void fragment() {
            vec3 ray = normalize(toward);
            ALBEDO = mix(horizon, top, pow(clamp(ray.y, 0.0, 1.0), 0.55));
            // One star in some of the cells the sky is cut into, brighter toward its middle.
            vec3 cell = ray * 150.0;
            float lit = step(0.985, fract(sin(dot(floor(cell), vec3(12.9898, 78.233, 37.719))) * 43758.5453));
            ALBEDO += stars * lit * smoothstep(0.5, 0.1, length(fract(cell) - 0.5)) * smoothstep(0.02, 0.3, ray.y);
        }
        """;

    /// <summary>The film in order: how long each shot runs and what plays it, given the seconds since its cut and since the last frame.</summary>
    private (float Seconds, Action<float, float> Play)[] IntroShots => new (float, Action<float, float>)[] {
        (DeskSeconds, DeskShot), (MouseSeconds, MouseShot), (ScreenSeconds, ScreenShot), (BossSeconds, BossShot), (StareSeconds, StareShot),
        (SmashSeconds, SmashShot), (BossImpactSeconds, BossImpactShot), (OfficeEscapeSeconds, OfficeEscapeShot),
        (5, PackingShot), (AlleySeconds, AlleyShot), (3, TrainShot), (ForestApproachSeconds, ForestApproachShot),
        (BerryPickingSeconds, BerryPickingShot), (ForestDepartureSeconds, ForestDepartureShot), (4.4f, ArrivalShot), (3.6f, ShoreShot),
        (2, (_, _) => Fade(1)), (8, NightShot), (1, (_, _) => Fade(1)),
    };

    /// <summary>
    /// Plays the film and saves every frame to <paramref name="folder"/>, or only the frames at <paramref name="stills"/> seconds when given.
    /// The film's clock moves one frame at a time, so the frames are evenly spaced however long each takes to draw and save.
    /// Each frame is drawn without waiting for the screen to refresh and saved in the background while the next one is drawn.
    /// With <paramref name="danceOnly"/> the film is the catgirl's solo alone.
    /// </summary>
    public async void RenderIntro(string folder, float[]? stills, bool danceOnly) {
        try {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            var saves = new List<Task>();
            DirAccess.MakeDirRecursiveAbsolute(folder);
            BuildFilm();
            var shots = danceOnly ? new (float Seconds, Action<float, float> Play)[] { (CatgirlDance.SoloSeconds, DanceShot) } : IntroShots;
            // Shots start on whole frames, so their lengths never drift by rounding.
            var starts = new int[shots.Length + 1];
            for (int i = 0; i < shots.Length; i++) starts[i + 1] = starts[i] + Mathf.RoundToInt(shots[i].Seconds * IntroFps);
            int shot = -1, saved = 0;
            for (int frame = 0; frame < starts[^1]; frame++) {
                cut = frame == starts[shot + 1];
                if (cut) shot++;
                filmTime = frame * IntroStep;
                Fade(0);
                shots[shot].Play((frame - starts[shot]) * IntroStep, cut ? 0 : IntroStep);
                skyDome.Position = camera.Position;
                bool still = stills != null && Array.Exists(stills, at => Mathf.RoundToInt(at * IntroFps) == frame);
                if (stills != null && !still) continue;
                // After a cut the ground's detail and the lights settle for a few frames before one is kept.
                for (int settle = cut || still ? 4 : 1; settle > 0; settle--) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string name = still ? string.Create(CultureInfo.InvariantCulture, $"still_{filmTime:00.00}.png") : $"{saved:D5}.png";
                var image = GetViewport().GetTexture().GetImage();
                saves.Add(Task.Run(() => {
                    using (image) {
                        var result = image.SavePng($"{folder}/{name}");
                        if (result != Error.Ok) throw new InvalidOperationException($"Intro frame {name} was not saved: {result}");
                    }
                }));
                saved++;
            }
            await Task.WhenAll(saves);
            using (var sheet = FileAccess.Open($"{folder}/{IntroCues}", FileAccess.ModeFlags.Write)) sheet.StoreString(cues.ToString());
            GD.Print($"INTRO RENDERED {saved} frames, {starts[^1] * IntroStep:0.00} s");
            GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    /// <summary>Notes a sound for the movie tool to lay under the film at this moment.</summary>
    /// <param name="sound">A game sound's name in <c>assets/sounds</c>, or <c>music/NAME</c> or <c>intro/NAME</c> for a file of those folders.</param>
    /// <param name="gain">Decibels over a game sound's level in the game's mix; for a file of another folder, its level outright.</param>
    /// <param name="treatment">Further <c>key=value</c> settings the tool reads, separated by tabs.</param>
    /// <param name="after">Seconds from now until it starts.</param>
    private void Cue(string sound, float gain = 0, string treatment = "", float after = 0) {
        float level = sound.Contains('/') ? gain : Sounds.Level(sound) + gain;
        cues.Append(string.Create(CultureInfo.InvariantCulture, $"{filmTime + after:0.000}\t{sound}\t{level:0.0}"));
        if (treatment != "") cues.Append('\t').Append(treatment);
        cues.Append('\n');
    }

    /// <summary>Sets how far the picture is faded to black, from 0 for clear to 1 for black.</summary>
    private void Fade(float amount) => black.Color = new Color(0, 0, 0, Math.Clamp(amount, 0, 1));

    // ---------- Building ----------

    private void BuildFilm() {
        filming = true;
        overlay.Visible = false; playerRig.Root.Visible = false;
        // No frame is blended with the one before it, so a cut leaves no ghost of the last shot.
        var viewport = GetViewport();
        viewport.UseTaa = false; viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear; viewport.Scaling3DScale = 1; viewport.Msaa3D = Viewport.Msaa.Msaa4X;
        filmLens = new CameraAttributesPractical { DofBlurAmount = .09f, DofBlurFarTransition = 3 };
        camera.Attributes = filmLens;
        camera.Projection = Camera3D.ProjectionType.Perspective; camera.Near = .05f; camera.Far = 700; camera.HOffset = 0; camera.VOffset = 0;
        sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits; sun.DirectionalShadowBlendSplits = true;
        // The film's perspective shots use their own shadow offset and softness.
        sun.ShadowBias = .1f; sun.ShadowNormalBias = 2; sun.ShadowBlur = 1.5f;
        environment.FogLightEnergy = 1; environment.FogSkyAffect = 0;
        film = new Node3D(); AddChild(film);
        skyColors = new ShaderMaterial { Shader = new Shader { Code = SkyShader } };
        skyDome = Sphere(film, 600, Vector3.Zero, skyColors, null, 24);
        skyDome.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        moon = Sphere(film, 1, Vector3.Zero, new StandardMaterial3D { AlbedoColor = new Color("e9edf2"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, DisableFog = true }, Vector3.One * 16, 16);
        moon.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        filmLayer = new CanvasLayer { Layer = 50 }; AddChild(filmLayer);
        black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); filmLayer.AddChild(black);

        hero = new ActorRig(ActorSkin.Player); film.AddChild(hero.Root);
        heroFace = new FilmFace(hero, ActorSkin.Player, showMouth: true);
        foreach (var joint in new[] { hero.Torso, hero.Chest })
            foreach (var mesh in joint.GetChildren())
                if (mesh is MeshInstance3D part) heroTrunkMeshes.Add(part);
        var heroSkin = Mat(ActorRig.OutfitOf(ActorSkin.Player).Skin);
        heroTie = ActorRig.Tie(hero.Torso, "4a5368"); heroTie.RotationDegrees = new Vector3(0, 0, 5);
        heldBat = IntroSets.Bat(); hero.Hold(heldBat);
        boss = new ActorRig(ActorSkin.Boss); film.AddChild(boss.Root);
        bossFace = new BossFace(boss);
        office = IntroSets.BuildOffice(); film.AddChild(office.Root);
        room = IntroSets.BuildRoom(); film.AddChild(room.Root);
        alley = IntroSets.BuildAlley(); film.AddChild(alley.Root);
        skyline = IntroSets.BuildSkyline(); film.AddChild(skyline);
        train = IntroSets.BuildTrain(); film.AddChild(train.Root);
        landscape = new IntroLandscape(film, Trail, TrailShore, ForestTrailStart + (ForestApproachSeconds + .18f) * World.WorkerSpeed);
        var eyesInDark = Mat("ffa337", .55f, glow: 8);
        for (int i = 0; i < nightWolves.Length; i++) {
            nightWolves[i] = new WolfModel(eyesInDark);
            film.AddChild(nightWolves[i].Root);
            nightWolves[i].Root.Hide();
        }
        wolfRim = new OmniLight3D {
            LightColor = new Color("86bfff"),
            LightEnergy = 0,
            ShadowEnabled = true,
            OmniAttenuation = 1.2f,
            OmniRange = 6.1f,
        };
        film.AddChild(wolfRim);
        // Two fingers lie from his fist over the mouse's buttons, for the one shot that sees his hand on it.
        // Each hinges at its knuckle over its button.
        mouseFingers = Pivot(office.Mouse, HandOnMouse + new Vector3(0, .012f, .02f));
        Node3D Finger(float side) {
            var finger = Pivot(mouseFingers, new Vector3(side * IntroSets.ButtonSide, 0, 0), new Vector3(FingerSlope, 0, 0));
            Sphere(finger, .021f, new Vector3(0, -.002f, .027f), heroSkin, new Vector3(.56f, .61f, 1.79f), 12);
            Sphere(finger, .013f, new Vector3(0, -.019f, .049f), heroSkin, new Vector3(.87f, .83f, 1.32f), 12);
            return finger;
        }
        Finger(-1);
        // His left is +X, so the finger on that side is the index, over the left button.
        indexFinger = Finger(1);
        BuildShaders(film);
        FindWaters(scene);
        // The fire pit of the last shot stands in the clearing from the start, unlit, so it is not dropped in with a puff of dust.
        World.S.Things.RemoveAll(thing => thing.Pos.DistanceTo(Terrain.Spawn) < 2.5f);
        campfire = World.Add(ThingKind.Campfire, Terrain.Spawn);
        // The game's fog of war is lifted for the whole film.
        UpdateFog();
    }

    /// <summary>The sea's and the lake's materials and the lily pads', whose waves the film moves by its own clock.</summary>
    private void FindWaters(Node node) {
        if (node is MeshInstance3D { MaterialOverride: ShaderMaterial material } && LakeAppearance.FollowsWaves(material) && !waters.Contains(material)) waters.Add(material);
        for (int i = 0; i < node.GetChildCount(); i++) FindWaters(node.GetChild(i));
    }

    // ---------- Shared by the shots ----------

    /// <summary>Puts the film camera at <paramref name="from"/>, looking at <paramref name="at"/> through a lens of <paramref name="fov"/> degrees.</summary>
    private void Frame(Vector3 from, Vector3 at, float fov) {
        camera.Position = from; camera.Basis = Basis.LookingAt(at - from, Vector3.Up); camera.Fov = fov;
        float distance = from.DistanceTo(at);
        filmLens.DofBlurFarEnabled = distance < 2.5f && (office.Root.Visible || room.Root.Visible);
        filmLens.DofBlurFarDistance = distance + .34f;
    }

    /// <summary>Shows one of the built sets alone, or the valley when <paramref name="set"/> is null.</summary>
    private void Stage(Node3D? set) {
        scene.Visible = set == null;
        office.Root.Visible = set == office.Root; room.Root.Visible = skyline.Visible = set == room.Root; train.Root.Visible = set == train.Root;
        alley.Root.Visible = set == alley.Root;
        // The office shots that show the boss place him explicitly.
        hero.Root.Visible = true; boss.Root.Visible = false;
        office.Page.Live = false;
        heroTie.Visible = set == office.Root;
        heroFace.Look(.92f);
        landscape.Forest.Hide();
        landscape.Shore.Hide();
        heldBat.Visible = false; moon.Visible = false; mouseFingers.Visible = false;
    }

    /// <summary>Lights a built set: one key light travelling along <paramref name="rays"/>, over a flat fill.</summary>
    private void Light(Vector3 rays, string key, float energy, string fill, float fillEnergy, float shadowReach) {
        sun.Basis = Basis.LookingAt(rays.Normalized(), Vector3.Up); sun.LightColor = new Color(key); sun.LightEnergy = energy;
        sun.DirectionalShadowMaxDistance = shadowReach;
        var ambient = new Color(fill);
        skylight.SkyTopColor = skylight.SkyHorizonColor = ambient; skylight.GroundHorizonColor = skylight.GroundBottomColor = ambient * .7f;
        // The game's lighting writes the sky's light only when it changes; this makes its next write go through.
        skyAmbient = default;
        environment.AmbientLightEnergy = fillEnergy;
        environment.FogEnabled = false;
    }

    private void PaintSky(string top, string horizon, bool stars = false) {
        skyColors.SetShaderParameter(SkyTop, new Color(top)); skyColors.SetShaderParameter(SkyHorizon, new Color(horizon));
        skyColors.SetShaderParameter(SkyStars, stars ? 1f : 0f);
    }

    /// <summary>A frame in the valley at a time of day, from 0 at dawn: the game's own things, light, wind and waves, with haze toward the horizon.</summary>
    private void Valley(float dayPhase, Vector2 around, float dt, string skyTop, string skyHorizon, float haze, bool stars = false) {
        World.S.Time = dayPhase * World.Data.DaySeconds;
        World.Player = around;
        // The game's flames and shakes go by the view's clock, which stands still while filming.
        Elapsed = filmTime;
        SyncThings(dt); UpdateEffects(dt); UpdateLighting(World.Time); UpdateLook(dt); UpdateFog();
        sun.DirectionalShadowMaxDistance = 70;
        PaintSky(skyTop, skyHorizon, stars);
        environment.FogEnabled = true; environment.FogDensity = haze; environment.FogLightColor = new Color(skyHorizon);
        foreach (var water in waters) LakeAppearance.SetWaveTime(water, filmTime);
    }

    /// <summary>Walks or runs a rig to <paramref name="at"/> the way the game does, with its footfalls on the cue sheet.</summary>
    private void Stride(ActorRig rig, ActorState state, Vector2 at, ActorAction action, Vector2 facing, float dt, string footstep = "footsteps_default", float gain = 0) {
        if (dt <= 0) { state.Position = at; state.Action = action; state.Blend = 1; state.Yaw = ActorAnimation.Yaw(facing - at, 0); }
        state.Update(at, action, facing, dt);
        Pose(rig, state, null);
        if (state.Moving && (state.Struck(.25f) || state.Struck(.75f))) Cue(footstep, gain);
    }

    /// <summary>Poses a rig standing at a spot of a set, turned by <paramref name="yaw"/> degrees from +Z toward +X.</summary>
    private static void Place(ActorRig rig, Vector3 at, float yaw, RigPose pose) {
        rig.Root.Position = at; rig.Root.RotationDegrees = new Vector3(0, yaw, 0);
        rig.Show(pose, null, false);
    }

    /// <summary>Turns a posed rig's head further: a nod down, a turn toward its left and a tilt toward its right shoulder, in degrees.</summary>
    private static void TurnHead(ActorRig rig, float nod, float turn, float tilt) => rig.Head.RotationDegrees += new Vector3(nod, turn, tilt);

    /// <summary>A number eased through keys sorted by time, held before the first and after the last.</summary>
    private static float Track(float time, params (float At, float Value)[] keys) => IntroPoses.Keyed<float>(time, Mathf.Lerp, keys);

    /// <summary>A point eased through keys sorted by time, held before the first and after the last.</summary>
    private static Vector3 Path(float time, params (float At, Vector3 Point)[] keys) => IntroPoses.Keyed<Vector3>(time, static (from, to, weight) => from.Lerp(to, weight), keys);

    /// <summary>Whether a shot's clock passed a moment on this frame, for what happens once.</summary>
    private static bool Reached(float moment, float t, float dt) => t >= moment && t - dt < moment;

    /// <summary>
    /// Bends one arm of a pose so its fist goes to a point in the rig's own space, or as near as the arm is long.
    /// <paramref name="fist"/> is where the fist ends up, for whatever it holds.
    /// </summary>
    private static RigPose Reaching(RigPose pose, bool left, Vector3 target, out Vector3 fist) {
        var shoulder = HumanShape.Standard.Shoulder(left);
        var torso = Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Lean), Mathf.DegToRad(pose.Twist), Mathf.DegToRad(pose.Sway)));
        var hips = new Vector3(0, pose.HipHeight, 0);
        var to = torso.Inverse() * (target - hips) - shoulder;
        to = to.Normalized() * Math.Clamp(to.Length(), .1f, ActorRig.UpperArm + ActorRig.Forearm - .005f);
        fist = hips + torso * (shoulder + to);
        var (raise, outward, elbow) = ArmReach.ArmTo(shoulder + to, left);
        return left ? pose with { RaiseL = raise, OutL = outward, ElbowL = elbow } : pose with { RaiseR = raise, OutR = outward, ElbowR = elbow };
    }

    /// <summary>Fits the fist to the tilted face and puts the elbow on the surface below it.</summary>
    private void RestHeadOnHand(RigPose pose, bool left, Vector3 fistAt, Vector3 turn, Vector3 elbowToward, float supportY) {
        TurnHead(hero, turn.X, turn.Y, turn.Z);
        var aim = ArmReach.ToTrunk(pose, hero.Root.ToLocal(hero.Head.ToGlobal(fistAt)));
        var up = ArmReach.TrunkBasis(pose).Inverse() * Vector3.Up;
        float height = supportY + .055f - hero.Root.GlobalPosition.Y - pose.HipHeight;
        var support = new ArmReach.ElbowSupport(up, height, 1);
        var (raise, outward, elbow, roll) = ArmReach.ArmTo(aim, left, elbowToward, ActorRig.Palm, support: support);
        var reached = ArmReach.WithArm(pose, left, raise, outward, elbow, 1);
        hero.Show(left ? reached with { ArmRollL = roll } : reached with { ArmRollR = roll }, null, false);
        TurnHead(hero, turn.X, turn.Y, turn.Z);
    }

    private static float Span(float from, float to, float time) => Math.Clamp((time - from) / (to - from), 0, 1);

    // ---------- Flying wreckage ----------

    private sealed class Wreck {
        public Node3D Node = null!;
        public Vector3 Velocity, Spin;
        public float Floor, Life = float.MaxValue, Age;
        public bool Bat, Landed;
    }
    private readonly List<Wreck> wreckage = new();

    private Wreck Throw(Node3D node, Vector3 velocity, Vector3 spin, float floor) {
        var wreck = new Wreck { Node = node, Velocity = velocity, Spin = spin, Floor = floor };
        wreckage.Add(wreck);
        return wreck;
    }

    /// <summary>Shards and sparks burst from <paramref name="at"/>, mostly toward <paramref name="toward"/>.</summary>
    private void Shatter(Vector3 at, Vector3 toward, int shards, int sparks, Random rng) {
        float R() => (float)rng.NextDouble();
        string[] colors = { "d6cfbd", "c4bdab", "2a3238", "8fa7b0" };
        for (int i = 0; i < shards; i++) {
            var shard = Box(office.Root, new Vector3(.03f + R() * .07f, .012f + R() * .03f, .03f + R() * .06f), at + new Vector3(R() - .5f, R() * .2f, R() - .5f) * .3f, Mat(colors[i % colors.Length], .5f));
            Throw(shard, toward * (1 + R() * 2) + new Vector3(R() - .5f, R() * 1.2f + .8f, R() - .5f) * 3.2f, new Vector3(R() - .5f, R() - .5f, R() - .5f) * 900, at.Y < IntroSets.DeskTop ? .01f : R() < .5f ? IntroSets.DeskTop + .01f : .01f);
        }
        for (int i = 0; i < sparks; i++) {
            var spark = Box(office.Root, Vector3.One * .014f, at, Mat("ffd27a", .4f, 0, 4));
            spark.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            Throw(spark, toward + new Vector3(R() - .5f, R() * .9f + .3f, R() - .5f) * 5.5f, Vector3.Zero, 0).Life = .25f + R() * .45f;
        }
    }

    private void UpdateWreckage(float dt) {
        foreach (var wreck in wreckage) {
            wreck.Age += dt;
            if (wreck.Age >= wreck.Life) { wreck.Node.Visible = false; continue; }
            wreck.Velocity += Vector3.Down * 9.8f * dt;
            var at = wreck.Node.Position + wreck.Velocity * dt;
            float floor = wreck.Floor;
            if (wreck.Bat) {
                wreck.Node.RotationDegrees += wreck.Spin * dt;
                float up = wreck.Node.Basis.Y.Y;
                floor += .026f - Math.Min(-.73f * up, .10f * up);
            }
            if (at.Y < floor) {
                at.Y = floor;
                if (wreck.Bat && !wreck.Landed) {
                    wreck.Landed = true;
                    Cue("wood_rocks_inventory", -2);
                }
                wreck.Velocity = new Vector3(wreck.Velocity.X * .45f, -wreck.Velocity.Y * .28f, wreck.Velocity.Z * .45f); wreck.Spin *= .4f;
                if (MathF.Abs(wreck.Velocity.Y) < .4f) { wreck.Velocity = Vector3.Zero; wreck.Spin = Vector3.Zero; }
            }
            wreck.Node.Position = at;
            if (!wreck.Bat) wreck.Node.RotationDegrees += wreck.Spin * dt;
            if (wreck.Life < 10) wreck.Node.Scale = Vector3.One * (1 - wreck.Age / wreck.Life);
        }
    }

    // ---------- The office ----------

    private static readonly Vector3 BossSpot = new(.86f, 0, .12f);
    /// <summary>How long each shot in the office runs, in seconds.</summary>
    private const float DeskSeconds = 5, MouseSeconds = 2.5f, ScreenSeconds = 5, BossSeconds = 4, StareSeconds = 3, SmashSeconds = 2.4f;
    private const float BossImpactSeconds = 3.6f, OfficeEscapeSeconds = 2;
    private const float OfficeSeconds = DeskSeconds + MouseSeconds + ScreenSeconds + BossSeconds + StareSeconds + SmashSeconds + BossImpactSeconds + OfficeEscapeSeconds;
    /// <summary>
    /// The boss is first heard this many seconds before the cut to him.
    /// His recording is played from <see cref="BossVoiceStarts"/> seconds in.
    /// </summary>
    private const float BossHeardBefore = 1, BossVoiceStarts = 1.6f;
    /// <summary>How long the ring rises under the boss's voice before the cut to the stare.</summary>
    private const float TinnitusOverlap = 1.4f;
    /// <summary>How loud <c>assets/intro/boss.mp3</c> is, in decibels, every tenth of a second.</summary>
    private static readonly sbyte[] BossLoudness = { -48, -57, -62, -65, -67, -69, -68, -70, -68, -60, -29, -23, -26, -31, -36, -33, -27, -26, -25, -32, -42, -25, -26, -26, -28, -40, -33, -25, -25, -39, -27, -37, -23, -29, -28, -25, -27, -24, -27, -23, -21, -23, -27, -41, -30, -20, -24, -31, -23, -27, -24, -22, -25, -29, -28, -34, -40, -17, -19, -20, -30, -31, -16, -13, -14, -16, -23, -47, -59, -67, -40, -43, -45, -50 };

    /// <summary>How hard the boss shouts at a moment of his recording, from 0 to 1.</summary>
    private static float Shout(float voice) {
        float sample = Mathf.Clamp(voice * 10, 0, BossLoudness.Length - 1);
        int i = (int)sample;
        float db = Mathf.Lerp(BossLoudness[i], BossLoudness[Math.Min(i + 1, BossLoudness.Length - 1)], sample - i);
        return Mathf.Clamp((db + 40) / 20, 0, 1);
    }

    /// <param name="rays">The way the strip lights' light travels, down onto the desks at a slant unless given.</param>
    private void OfficeLight(Vector3? rays = null) {
        Stage(office.Root);
        office.Monitor.Visible = true;
        Light(rays ?? new Vector3(.25f, -1, .2f), "eef2ff", .9f, "c4c3c0", .9f, 14);
        PaintSky("9fb4c8", "dfe6e2");
        hero.Pack!.Visible = false;
    }

    /// <summary>
    /// What goes on in the office by itself: colleagues typing, the catgirls dancing on his screen and its glow.
    /// </summary>
    private void OfficeAround() {
        for (int i = 0; i < office.Staff.Count; i++) office.Staff[i].Show(IntroPoses.Typing(filmTime + i * 1.37f), null, false);
        // The page's clock starts with the close-up of it; it is drawn again only while his screen can be seen.
        office.Page.Live = office.PageOnScreen.IsVisibleInTree();
        office.Page.Play(filmTime - DeskSeconds - MouseSeconds);
        // The tube's glow wavers a little on his face.
        office.Glow.LightEnergy = .12f + .012f * MathF.Sin(filmTime * 31) + .008f * MathF.Sin(filmTime * 7.3f);
    }

    /// <summary>Bored at his desk, cheek on fist.</summary>
    private void DeskShot(float t, float dt) {
        // The camera sits where his screen is, so the monitor itself is out of the picture.
        if (cut) {
            OfficeLight(); office.LeaningBat.Visible = true;
            // Short, overlapping excerpts keep the room audible through the escape.
            const float middle = 11.8f, cross = .77f, last = 20.2f;
            foreach (var (hum, gain) in new[] { ("intro/office", 2f), ("intro/office_room", -6f) }) {
                Cue(hum, gain, "length=12.57\tfadein=0.4\tfadeout=0.77");
                Cue(hum, gain, "from=1\tlength=9.17\tfadein=0.77\tfadeout=0.77", middle);
                Cue(hum, gain, string.Create(CultureInfo.InvariantCulture,
                    $"from=0.4\tlength={OfficeSeconds - last:0.00}\tfadein={cross:0.00}\tfadeout=0.8"), last);
            }
        }
        office.Monitor.Visible = false;
        Fade(1 - t / .7f);
        OfficeAround();
        var pose = IntroPoses.Bored(t);
        Place(hero, Vector3.Zero, 0, pose);
        RestHeadOnHand(pose, false, new Vector3(-.098f, .065f, .070f), new Vector3(4, -6, 22),
            new Vector3(-.35f, -1, .51f), IntroSets.DeskTop);
        // A slow blink, lids already half shut.
        float blink = MathF.Pow(Math.Max(0, MathF.Sin(t * 1.9f + .6f)), 14);
        heroFace.Look(.54f * (1 - blink), -.36f, .14f);
        float push = Mathf.SmoothStep(0, DeskSeconds, t);
        Frame(new Vector3(.2f, 1.08f, 1.05f).Lerp(new Vector3(.12f, 1.05f, .82f), push), new Vector3(-.05f, .97f, .2f), 44);
    }

    /// <summary>
    /// When the left mouse button goes down, in seconds into the mouse shot: a double click and one more.
    /// </summary>
    private static readonly float[] MouseClicks = { 1.25f, 1.5f, 2.15f };
    /// <summary>Where the right fist lies on the mouse, from the mouse's origin.</summary>
    private static readonly Vector3 HandOnMouse = new(0, .034f, -.035f);
    /// <summary>How far a finger slopes down from its knuckle to a mouse button, in degrees.</summary>
    private const float FingerSlope = 14;

    /// <summary>His hand on the mouse: it slides around the pad, stops and clicks.</summary>
    private void MouseShot(float t, float dt) {
        const float pressSeconds = .09f;
        // The light falls steeper here, which moves a strip light's shadow off his hand.
        if (cut) { OfficeLight(new Vector3(.02f, -1, -.05f)); mouseFingers.Visible = true; }
        OfficeAround();
        // Round the pad, to a stop for the double click, then a short slide to the last click.
        office.Mouse.Position = IntroSets.MouseSpot + Path(t, (0, new Vector3(.03f, 0, -.02f)), (.35f, new Vector3(-.04f, 0, .03f)), (.7f, new Vector3(.05f, 0, .04f)),
            (1.05f, Vector3.Zero), (1.65f, Vector3.Zero), (1.95f, new Vector3(-.035f, 0, .025f)));
        float press = 0;
        foreach (float click in MouseClicks) {
            press = Math.Max(press, 1 - Math.Abs(t - click - pressSeconds / 2) / (pressSeconds / 2));
            if (Reached(click, t, dt)) { Cue("intro/mouse_click", -11); Cue("intro/mouse_release", -13, after: pressSeconds); }
        }
        office.MouseButton.Scale = new Vector3(1, 1 - .7f * press, 1);
        indexFinger.RotationDegrees = new Vector3(FingerSlope + 8 * press, 0, 0);
        Place(hero, Vector3.Zero, 0, Reaching(IntroPoses.Browsing, false, office.Mouse.Position + HandOnMouse, out _));
        heroFace.Look(.7f, -.25f);
        // From the partition beside him, a hand's height over the desk, side on to the hand and drifting closer.
        Frame(new Vector3(-.8f, .83f, .3f).Lerp(new Vector3(-.76f, .81f, .33f), Mathf.SmoothStep(0, MouseSeconds, t)), IntroSets.MouseSpot + new Vector3(0, .03f, 0), 34);
    }

    /// <summary>What he has on his screen: a web page of catgirls dancing, until the boss's voice breaks in.</summary>
    private void ScreenShot(float t, float dt) {
        if (cut) { OfficeLight(); hero.Root.Visible = false; }
        office.Page.FrameStage(t / ScreenSeconds);
        OfficeAround();
        if (Reached(ScreenSeconds - BossHeardBefore, t, dt)) {
            float voiceLength = BossHeardBefore + BossSeconds;
            Cue("intro/boss", 0, string.Create(CultureInfo.InvariantCulture,
                $"from={BossVoiceStarts:0.00}\tlength={voiceLength:0.00}\tfadein=0.03\tfadeout={TinnitusOverlap:0.00}"));
            Cue("intro/tinnitus", -27, string.Create(CultureInfo.InvariantCulture,
                $"length={TinnitusOverlap + StareSeconds:0.00}\tfadein={TinnitusOverlap:0.00}\tfadeout=0.35"),
                voiceLength - TinnitusOverlap);
        }
        // Through his eyes, square on to the screen, drifting closer.
        var screen = office.Monitor.Position + IntroSets.ScreenMiddle;
        Frame(screen + new Vector3(0, .01f, -.56f + .08f * Mathf.SmoothStep(0, ScreenSeconds, t)), screen, 34);
    }

    /// <summary>The soloist's dance alone: the page as tall as the picture, on black, and nothing else drawn.</summary>
    private void DanceShot(float t, float dt) {
        if (cut) {
            // The page draws in a world of its own, so the film's own world need not be drawn at all.
            GetViewport().Disable3D = true;
            office.Page.Live = true;
            office.Page.Rehearse();
            // The beat is only for timing the moves while the dance is worked on; the film has no music here.
            Cue("intro/dance_beat", -11.5f);
            // The page is narrower than the picture, so black fills its sides.
            var backdrop = new ColorRect { Color = Colors.Black };
            var page = new TextureRect { Texture = office.Page.Picture.AlbedoTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); page.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            backdrop.AddChild(page);
            filmLayer.AddChild(backdrop); filmLayer.MoveChild(backdrop, 0);
        }
        office.Page.Play(t);
    }

    /// <summary>
    /// The boss bent over the hero's left shoulder, <paramref name="yelling"/> seconds into his rant.
    /// His mouth opens as wide as he shouts.
    /// </summary>
    private void YellingBoss(float yelling, float shout) {
        boss.Root.Visible = true;
        Place(boss, BossSpot, -100, IntroPoses.Yelling(yelling));
        float screen = Track(yelling, (.93f, 0), (1.29f, 1), (1.65f, 1), (2.08f, 0));
        var eyes = boss.Head.ToGlobal(new Vector3(0, .145f, .10f));
        var listener = hero.Head.ToGlobal(new Vector3(0, .14f, .09f));
        var glance = listener.Lerp(office.Monitor.ToGlobal(IntroSets.ScreenMiddle), screen * .87f);
        var facing = boss.Head.GlobalBasis.Orthonormalized().Slerp(Basis.LookingAt(glance - eyes, Vector3.Up, true), .76f);
        boss.Head.GlobalBasis = facing.Scaled(boss.Head.GlobalBasis.Scale);
        float question = Track(yelling, (1.94f, 0), (2.28f, 1), (2.74f, 1), (3.13f, 0));
        bossFace.OpenMouth(1.12f - .24f * shout, .16f + shout, -.95f + .78f * question);
    }

    /// <summary>Over his shoulder, the hero follows the boss's glance at the screen and draws back.</summary>
    private void BossShot(float t, float dt) {
        if (cut) OfficeLight();
        OfficeAround();
        float shout = Shout(BossVoiceStarts + BossHeardBefore + t);
        float flinch = Mathf.SmoothStep(3.05f, 3.43f, t);
        var reaction = IntroPoses.Cowering with { Lean = -4 - 6 * flinch, Sway = 8 + 4 * flinch };
        Place(hero, Vector3.Zero, 0, reaction);
        float look = Track(t, (0, 41), (1.33f, 41), (1.64f, 19), (2.14f, 25), (2.62f, 44));
        TurnHead(hero, 4 * flinch, look, 0);
        heroFace.Look(1.13f - .12f * flinch, look / 80, .78f);
        YellingBoss(t, shout);
        float push = Mathf.SmoothStep(.7f, BossSeconds, t);
        Frame(new Vector3(-.57f, 1.31f, -.62f).Lerp(new Vector3(-.36f, 1.34f, -.53f), push),
            new Vector3(.53f + .14f * push, 1.23f, .10f), 47);
    }

    /// <summary>Only the ring remains as the hero stares into the lens.</summary>
    private void StareShot(float t, float dt) {
        if (cut) OfficeLight();
        // The camera sits where his screen is again.
        office.Monitor.Visible = false;
        OfficeAround();
        Place(hero, Vector3.Zero, 0, IntroPoses.Staring);
        float settle = Mathf.SmoothStep(0, .27f, t);
        heroFace.Look(1.06f - .16f * settle, .34f * (1 - settle), -.29f);
        YellingBoss(BossSeconds + t, .44f);
        Frame(new Vector3(0, 1.14f, .82f).Lerp(new Vector3(0, 1.14f, .7f), Mathf.SmoothStep(0, StareSeconds, t)), new Vector3(0, 1.13f, .1f), 28);
    }

    private readonly Random smashRandom = new(7);

    private static readonly Vector3 OfficeSwingSpot = new(1.10f, 0, -.775f), BossImpactSpot = new(2.035f, 0, -1.185f);
    private const float OfficeSwingYaw = 111, BossImpactYaw = -64;
    private static readonly Vector3 MonitorDentedAngles = new(9, 0, -6), MonitorDentedScale = new(1, .78f, 1);
    private Vector3 monitorFirstTop, monitorSecondTop;

    /// <summary>He takes the bat to his computer twice, then turns toward the boss in the aisle.</summary>
    private void SmashShot(float t, float dt) {
        if (cut) {
            OfficeLight(); office.LeaningBat.Visible = false; boss.Root.Visible = true;
            monitorFirstTop = office.Monitor.ToGlobal(IntroSets.MonitorFrontEdge + Vector3.Right * .06f);
            var dented = Basis.FromEuler(MonitorDentedAngles * (Mathf.Pi / 180));
            monitorSecondTop = office.Monitor.ToGlobal(dented * (IntroSets.MonitorFrontEdge * MonitorDentedScale + Vector3.Right * .13f));
            office.Chair.Position += new Vector3(-.35f, .2f, -1.25f); office.Chair.RotationDegrees = new Vector3(0, 40, 84);
        }
        OfficeAround();
        heroFace.Look(.83f, .4f, -.7f);
        heldBat.Visible = true;
        bool first = Reached(IntroPoses.FirstBlow, t, dt), second = Reached(IntroPoses.SecondBlow, t, dt);
        if (dt > 0 && (Mathf.RoundToInt((t - .45f) * IntroFps) == 0 || Mathf.RoundToInt((t - 1.26f) * IntroFps) == 0)) Cue("tool_air_swing", 1);
        UpdateWreckage(dt);
        if (first) {
            // The screen goes dark on contact. The case visibly gives on the following frame.
            office.PageOnScreen.Visible = false;
            office.ScreenIndicators.Hide();
            office.Screen.AlbedoColor = new Color("0e1114"); office.Screen.EmissionEnabled = false; office.Glow.Visible = false;
            Shatter(monitorFirstTop, new Vector3(0, 0, .6f), 16, 22, smashRandom);
            Throw(office.Mug, new Vector3(.9f, 1.6f, -.4f), new Vector3(300, 0, 500), .04f);
            Cue("intro/hit", -3, "from=0.26\tlength=0.46");
        }
        if (Reached(IntroPoses.FirstBlow + IntroStep, t, dt)) {
            office.Monitor.RotationDegrees = MonitorDentedAngles;
            office.Monitor.Scale = MonitorDentedScale;
        }
        if (second) {
            // The second blow sends what is left of it off the back of the desk and the keyboard into the air.
            Throw(office.Monitor, new Vector3(.2f, 3.8f, 1.8f), new Vector3(-420, 60, 90), .16f);
            Throw(office.Keyboard, new Vector3(-1.7f, 2.9f, .46f), new Vector3(540, 200, -300), .02f);
            Shatter(monitorSecondTop, new Vector3(0, .5f, .4f), 12, 14, smashRandom);
            // The recording's next hit, a little lower; its last one falls as the monitor lands on the floor.
            Cue("intro/hit", -2, "from=0.74\tlength=0.48\trate=0.9");
            Cue("intro/hit", -4, "from=1.24\trate=0.9", .85f);
        }
        const float step = 1.78f;
        var from = new Vector3(0, 0, -.40f);
        float advance = Mathf.SmoothStep(step, SmashSeconds, t);
        var pose = RigPose.Lerp(IntroPoses.Smashing(t), IntroPoses.OfficeSwing(0), advance);
        if (t >= step) {
            // One stride away from the desk as the bat rises into the aisle pose.
            float gait = .13f + advance * ActorAnimation.Duration(ActorAction.Walk) * .75f;
            var walk = ActorAnimation.PoseAt(ActorAction.Walk, gait, true);
            pose = RigPose.Lerp(pose.WithLegs(walk.Leg(true), walk.Leg(false)) with { Hips = walk.Hips }, pose,
                Mathf.SmoothStep(.88f, 1, advance));
        }
        pose = PoseMonitorBat(pose, t, out var barrel);
        Place(hero, from.Lerp(OfficeSwingSpot, advance), OfficeSwingYaw * advance, pose);
        heldBat.GlobalBasis = new Basis(new Quaternion(Vector3.Down, hero.Root.GlobalBasis * barrel));
        float lift = Mathf.SmoothStep(2.08f, 2.30f, t);
        if (lift > 0) StageAisleBat(pose, lift, 0);
        float aghast = Mathf.SmoothStep(IntroPoses.FirstBlow, IntroPoses.FirstBlow + .25f, t);
        Place(boss, new Vector3(1.45f, 0, -1).Lerp(BossImpactSpot, aghast),
            Mathf.Lerp(-55, BossImpactYaw, aghast), RigPose.Lerp(IntroPoses.Yelling(0), IntroPoses.Aghast(0), aghast));
        bossFace.OpenMouth(1, .3f + aghast * .9f, .38f + .39f * aghast);
        float jolt = (first || second ? 1 : 0) * .02f;
        var cameraAt = new Vector3(2.11f, 1.52f + jolt, .80f).Lerp(new Vector3(2.7f, 1.48f, .60f), advance);
        Frame(cameraAt, new Vector3(0, 1.29f, .39f).Lerp(new Vector3(1.36f, 1.17f, -.71f), advance), Mathf.Lerp(59, 50, advance));
    }

    /// <summary>Moves through a prop pose without pausing at it, following the tangent from its neighbouring poses.</summary>
    private static Vector3 BatPath(float time, params (float At, Vector3 Point)[] keys) {
        for (int i = 1; i < keys.Length; i++) {
            var (toTime, to) = keys[i];
            if (time > toTime) continue;
            var (fromTime, from) = keys[i - 1];
            var (beforeTime, before) = keys[Math.Max(0, i - 2)];
            var (afterTime, after) = keys[Math.Min(keys.Length - 1, i + 1)];
            float duration = toTime - fromTime, u = Span(fromTime, toTime, time), u2 = u * u, u3 = u2 * u;
            var outTangent = i == 1 ? Vector3.Zero : (to - before) * (duration / (toTime - beforeTime));
            var inTangent = i == keys.Length - 1 ? Vector3.Zero : (after - from) * (duration / (afterTime - fromTime));
            return from * (2 * u3 - 3 * u2 + 1) + outTangent * (u3 - 2 * u2 + u)
                + to * (3 * u2 - 2 * u3) + inTangent * (u3 - u2);
        }
        return keys[^1].Point;
    }

    private const float AisleHandSpacing = .073f, MonitorHandSpacing = .201f;
    private static readonly Vector3 MonitorGrip = new(-.123f, .818f, .191f);
    private static readonly Vector3 MonitorBarrel = new Vector3(.8725f, .3032f, -.3831f).Normalized();
    private static readonly RigPose MonitorArms = FitMonitorArms();

    /// <summary>Fits the elbows and both fists to the overhead pose in the trunk's own frame.</summary>
    private static RigPose FitMonitorArms() {
        var (arms, rightRoll) = ArmReach.WithHandAt(default, false, MonitorGrip, new Vector3(-1, -.2f, .15f));
        (arms, float leftRoll) = ArmReach.WithHandAt(arms, true, MonitorGrip + MonitorBarrel * MonitorHandSpacing, new Vector3(1, -.2f, .15f));
        return arms with { ArmRollL = leftRoll, ArmRollR = rightRoll };
    }

    /// <summary>Rotates both arms and the bat together about the line through the shoulders.</summary>
    private static RigPose PoseMonitorBat(RigPose pose, float time, out Vector3 barrel) {
        float angle = Track(time, (0, 70), (.41f, 0), (.45f, 0),
            (IntroPoses.FirstBlow, 73), (.76f, 95), (.89f, 91), (35f / IntroFps, 0),
            (IntroPoses.SecondBlow, 63), (1.57f, 94), (1.74f, 93), (2.09f, 83));
        barrel = ArmReach.TrunkBasis(pose) * (new Basis(Vector3.Right, Mathf.DegToRad(angle)) * MonitorBarrel);
        return pose with {
            RaiseL = MonitorArms.RaiseL - angle,
            RaiseR = MonitorArms.RaiseR - angle,
            OutL = MonitorArms.OutL,
            OutR = MonitorArms.OutR,
            ElbowL = MonitorArms.ElbowL,
            ElbowR = MonitorArms.ElbowR,
            ArmRollL = MonitorArms.ArmRollL,
            ArmRollR = MonitorArms.ArmRollR,
        };
    }

    /// <summary>Fits the two fists to the same handle, keeping the barrel in its posed orientation.</summary>
    private RigPose BatGrip(RigPose pose, Vector3 grip, Basis direction, float spacing, Vector3 rightElbow, Vector3 leftElbow) {
        const float reach = ActorRig.UpperArm + ActorRig.Forearm - .015f;
        var root = hero.Root.GlobalTransform;
        var rightShoulder = ArmReach.Shoulder(pose, root, false);
        var leftOffset = -direction.Y * spacing;
        var leftShoulder = ArmReach.Shoulder(pose, root, true) - leftOffset;
        for (int i = 0; i < 4; i++) {
            grip = leftShoulder + (grip - leftShoulder).LimitLength(reach);
            grip = rightShoulder + (grip - rightShoulder).LimitLength(reach);
        }
        var (both, rightRoll) = ArmReach.Reaching(pose, grip, root, elbowToward: rightElbow);
        (both, float leftRoll) = ArmReach.Reaching(both, grip + leftOffset, root, left: true, elbowToward: leftElbow);
        var fitted = both with { ArmRollL = leftRoll, ArmRollR = rightRoll };
        hero.Show(fitted, null, false);
        heldBat.GlobalBasis = direction;
        return fitted;
    }

    private const float BossContact = 26f / IntroFps, SwingContact = .5f;
    private const float BossFallStarts = BossImpactSeconds - .78f, BossLands = BossImpactSeconds + .42f;
    private const float BossGuardTurn = -26, ContactRelease = .33f;
    private const float AisleSlowStart = .34f, AisleSlowRate = .62f;
    private static float AisleSweepTime(float t) => t - Math.Max(0, t - AisleSlowStart) * (1 - AisleSlowRate);
    private static readonly Vector3 BossCheek = new(-.071f, .111f, .084f);
    private static readonly Vector3 AisleContactSide = new(-1, 0, -.52f);
    private static readonly Vector3 AisleContactAxis = new Vector3(.86f, .12f, -.49f).Normalized();
    private Vector3 bossCheek;
    private RigPose swingEnd;
    private Basis batFollowThrough;

    /// <summary>Holds the bat at shoulder height across the cut and sweeps it around the hero's outside.</summary>
    private RigPose StageAisleBat(RigPose pose, float lead, float swing, Vector3 closeShift = default,
        (Vector3 Grip, Vector3 Axis, float Weight) contact = default) {
        var root = hero.Root.GlobalTransform;
        var nearSide = AisleContactSide.Slide(AisleContactAxis).Normalized();
        var contactGrip = bossCheek + nearSide * .032f - AisleContactAxis * .65f;
        var grip = BatPath(swing, (0, root * new Vector3(-.245f, 1.31f, .20f)),
            (.28f, root * new Vector3(-.30f, 1.34f, .23f)), (.40f, root * new Vector3(-.22f, 1.42f, .42f)),
            (.462f, contactGrip + new Vector3(-.075f, .015f, .19f)), (SwingContact, contactGrip),
            (.59f, contactGrip + new Vector3(.018f, -.006f, -.134f) + closeShift),
            (.67f, root * new Vector3(.32f, 1.425f, .46f)), (.79f, root * new Vector3(.37f, 1.37f, .18f)),
            (1, root * new Vector3(.31f, 1.34f, .09f)));
        var barrel = BatPath(swing, (0, root.Basis * new Vector3(-.85f, .05f, -.52f)),
            (.28f, root.Basis * new Vector3(-.89f, .04f, -.46f)), (.36f, root.Basis * new Vector3(-.98f, .08f, .13f)),
            (.42f, root.Basis * new Vector3(-.63f, .12f, .76f)), (SwingContact, AisleContactAxis),
            (.59f, AisleContactAxis + new Vector3(-.09f, -.015f, -.19f)),
            (.675f, root.Basis * new Vector3(.72f, -.06f, .67f)), (.79f, root.Basis * new Vector3(.99f, -.31f, -.05f)),
            (1, root.Basis * new Vector3(.94f, -.08f, -.34f))).Normalized();
        if (contact.Weight > 0) {
            grip = grip.Lerp(contact.Grip, contact.Weight);
            barrel = barrel.Slerp(contact.Axis, contact.Weight).Normalized();
            // Keep both the slide and its blend into the free arc outside the drawn cheek.
            var away = AisleContactSide.Slide(barrel).Normalized();
            float margin = bossFace.BarrelOffset(grip, barrel, away);
            if (margin > 0) grip += away * margin;
        }
        var direction = new Basis(new Quaternion(Vector3.Down, barrel));
        var liveGrip = heldBat.GlobalPosition;
        var liveDirection = heldBat.GlobalBasis;
        var rightElbow = new Vector3(-1, -.7f, -.35f);
        var leftElbow = new Vector3(1, -.56f, -.26f);
        if (lead < 1) {
            rightElbow = (ArmReach.ToTrunk(pose, hero.Elbow(false)) - HumanShape.Standard.Shoulder(false)).Normalized().Lerp(rightElbow, lead);
            leftElbow = (ArmReach.ToTrunk(pose, hero.Elbow(true)) - HumanShape.Standard.Shoulder(true)).Normalized().Lerp(leftElbow, lead);
        }
        float spacing = Mathf.Lerp(MonitorHandSpacing, AisleHandSpacing, lead);
        var fitted = BatGrip(pose, liveGrip.Lerp(grip, lead), liveDirection.Slerp(direction, lead), spacing, rightElbow, leftElbow);
        // In the insert only the arms are visible. Keep the posed grip on its mark if either elbow ran out of reach.
        if (contact.Weight > 0) hero.Root.GlobalPosition += grip - heldBat.GlobalPosition;
        return fitted;
    }

    /// <summary>Slows the barrel against the cheek and tips it away as the cheek gives.</summary>
    private (Vector3 Grip, Vector3 Axis, float Weight) SlideAcrossCheek(float since, float recoil) {
        float weight = Mathf.SmoothStep(-.20f, -.13f, since) * (1 - Mathf.SmoothStep(.68f, .84f, since));
        float squash = Track(since, (-IntroStep, 0), (0, .20f), (.14f, .99f), (ContactRelease, .82f), (.52f, .43f), (.70f, 0));
        if (weight <= 0) {
            bossFace.Deform(squash, recoil);
            return default;
        }
        float slide = Track(since, (-IntroStep, 0), (0, 0), (.075f, .12f), (.20f, .55f), (ContactRelease, 1));
        // The raised hands keep the barrel above the vest and the boss's lowered guard.
        var acrossScreen = Vector3.Up.Cross(AisleContactSide).Normalized();
        float tilt = Mathf.DegToRad(Mathf.Lerp(26, 12, slide));
        var barrel = acrossScreen * Mathf.Cos(tilt) + Vector3.Up * Mathf.Sin(tilt);
        var outward = AisleContactSide.Slide(barrel).Normalized();
        var cheek = boss.Head.ToGlobal(new Vector3(0, .13f, 0)) + acrossScreen * Mathf.Lerp(.081f, -.064f, slide)
            + Vector3.Up * Mathf.Lerp(.011f, .005f, slide);
        bossFace.SetCrease(cheek, barrel, outward);
        bossFace.Deform(squash, recoil);
        var grip = cheek - barrel * Mathf.Lerp(.655f, .605f, slide);
        float offset = bossFace.BarrelOffset(grip, barrel, outward);
        if (float.IsFinite(offset)) grip += outward * offset;
        float travel = Track(since, (-.20f, 1.45f), (-.13f, 1.03f), (-2 * IntroStep, .48f), (-IntroStep, .22f),
            (0, 0), (ContactRelease, 0), (.45f, -.22f), (.61f, -.76f), (.74f, -1.22f));
        grip += acrossScreen * travel;
        // Keep the grip and forearms in front of the shirt and near sleeve on entry.
        // The barrel reaches the crease only after the arms have moved past the sleeve.
        grip += outward * Track(since, (-.18f, .30f), (-.105f, .30f), (-.075f, .29f), (-.052f, .225f),
            (-.029f, .13f), (0, 0), (ContactRelease, 0), (.49f, .11f));
        grip += Vector3.Up * Track(since, (-.18f, .07f), (-.070f, .07f), (-.016f, 0));
        return (grip, barrel, weight);
    }

    /// <summary>Turns the recoil into a single falling motion across the cut to the aisle.</summary>
    private float StageBossFall(float t, float recoil) {
        float fall = Span(BossFallStarts, BossLands, t);
        float down = fall * fall * (2 - fall);
        float thump = Mathf.SmoothStep(0, .07f, t - BossLands) * (1 - Mathf.SmoothStep(.07f, .19f, t - BossLands));
        var stagger = new Vector3(.075f, .015f, -.21f) * recoil;
        var slide = new Vector3(.25f, .08f, -.14f) * down;
        float guard = Mathf.SmoothStep(.10f, .76f, t) * (1 - Mathf.SmoothStep(.22f, .76f, down));
        var pose = down > 0 ? IntroPoses.BossOnFloor(down) : IntroPoses.BossReeling(recoil);
        pose = pose with {
            RaiseR = Mathf.Lerp(pose.RaiseR, 31, guard),
            OutR = Mathf.Lerp(pose.OutR, 39, guard),
            ElbowR = Mathf.Lerp(pose.ElbowR, 49, guard),
            RaiseL = Mathf.Lerp(pose.RaiseL, 39, guard),
            OutL = Mathf.Lerp(pose.OutL, 51, guard),
            ElbowL = Mathf.Lerp(pose.ElbowL, 42, guard)
        };
        Place(boss, BossImpactSpot + stagger + slide, Mathf.Lerp(BossImpactYaw + 14 * recoil, -115, down), pose);
        var fallAxis = new Vector3(1, 0, -.26f).Normalized();
        boss.Root.Basis = new Basis(fallAxis, Mathf.DegToRad(-87 * down - 3 * thump)) * boss.Root.Basis;
        float headYaw = Mathf.Lerp(BossGuardTurn * Mathf.SmoothStep(0, .55f, t) - 25 * recoil, 12, down);
        TurnHead(boss, -9 * recoil * (1 - down), headYaw, -19 * recoil * (1 - down));
        if (down > 0) KeepBossAboveCarpet(.017f * thump);
        return down;
    }

    private static readonly (float Delay, Vector3 Velocity, Vector3 Spin)[] ToothFlight = [
        (.16f, new Vector3(-2.72f, 1.04f, 1.28f), new Vector3(640, -840, 330)),
        (.27f, new Vector3(.88f, .30f, 1.58f), new Vector3(-370, 420, 710)),
        (.35f, new Vector3(-1.18f, -.42f, 1.07f), new Vector3(-620, -410, 510)),
        (.51f, new Vector3(.18f, -.94f, 1.0f), new Vector3(510, 210, -420)),
    ];

    /// <summary>The bat crosses his face in close-up, with the contact and recoil stretched in time.</summary>
    private void BossImpactShot(float t, float dt) {
        if (cut) {
            OfficeLight(new Vector3(-.25f, -1, -.35f)); boss.Root.Visible = true; heroFace.Look(.83f, .4f, -.7f);
            Place(boss, BossImpactSpot, BossImpactYaw, IntroPoses.Aghast(0));
            TurnHead(boss, 0, BossGuardTurn, 0);
            bossCheek = boss.Head.ToGlobal(BossCheek);
        }
        OfficeAround();
        float beat = AisleSweepTime(t), beatStep = beat - AisleSweepTime(t - dt);
        float since = beat - BossContact;
        float past = Math.Max(0, since);
        float recoil = Track(past, (0, 0), (.24f, .23f), (.75f, .82f), (1.19f, 1));
        StageBossFall(t, recoil);
        const float closeStart = .73f, wideReturn = 2.77f;
        bool close = beat >= closeStart && t < wideReturn;
        var contact = SlideAcrossCheek(since, recoil);
        if (!close) contact = default;

        float swing = BatPath(beat, (0, Vector3.Zero), (.35f, new Vector3(.23f, 0, 0)),
            (.53f, new Vector3(.29f, 0, 0)), (.71f, new Vector3(.40f, 0, 0)),
            (BossContact, new Vector3(SwingContact, 0, 0)), (BossContact + .20f, new Vector3(.53f, 0, 0)),
            (BossContact + ContactRelease, new Vector3(.61f, 0, 0)), (1.55f, new Vector3(.83f, 0, 0)), (1.91f, Vector3.Right)).X;
        // In the insert only the hero's hands and arms enter at the lower edge.
        hero.Head.Visible = heroTie.Visible = !close;
        foreach (var part in heroTrunkMeshes) part.Visible = !close;
        // In the insert the hero takes the handle out past the left edge, keeping both hands on it.
        var closeShift = close ? new Vector3(-.34f, .03f, -.99f) * Mathf.SmoothStep(ContactRelease, .65f, past) : Vector3.Zero;
        var pose = IntroPoses.OfficeSwing(swing);
        var heroSpot = OfficeSwingSpot + closeShift;
        if (close && contact.Weight > 0) {
            // The shoulders stay below the insert. The full standing pose resumes in the wide shot.
            var belowFrame = contact.Grip + AisleContactSide.Normalized() * .30f - Vector3.Up * 1.61f;
            heroSpot = heroSpot.Lerp(belowFrame, contact.Weight);
        }
        if (t >= BossImpactSeconds - OfficeEscapeLead) {
            StageOfficeEscape(t - (BossImpactSeconds - OfficeEscapeLead), dt);
        } else {
            heldBat.Show();
            Place(hero, heroSpot, OfficeSwingYaw, pose);
            swingEnd = StageAisleBat(pose, 1, swing, closeShift, contact);
            batFollowThrough = heldBat.GlobalBasis;
        }
        if (Reached(.57f, beat, beatStep)) Cue("tool_air_swing", 1, "rate=0.78");
        if (Reached(BossContact, beat, beatStep)) Cue("wolf_hit", 0, "rate=0.72");
        if (!close) {
            Vector3 from = beat < closeStart ? new Vector3(1.44f, 1.65f, .60f) : new Vector3(1.98f, 1.66f, 1.12f);
            Frame(from, new Vector3(1.56f, 1.12f, -1.11f), 53);
        } else {
            var focus = bossFace.MouthPosition + Vector3.Up * .012f;
            float push = Mathf.SmoothStep(closeStart, BossContact + .46f, beat);
            float drift = Mathf.SmoothStep(.42f, 2.3f, past);
            // The three-quarter view exposes the shape at contact and keeps the shoulders below the insert.
            Frame(focus + new Vector3(-.935f, .115f, -.95f).Lerp(new Vector3(-1.06f, .11f, -.98f), push),
                focus + new Vector3(.04f * drift, -.085f * drift, -.085f), Mathf.Lerp(35, 32f, push));
        }

        // Advance the loose teeth first so each new one is still at the lip on its first visible frame.
        UpdateWreckage(t >= BossImpactSeconds - OfficeEscapeLead ? dt : beat < BossContact ? beatStep : .18f * beatStep);
        // Two teeth follow the sweep, one skips nearer the camera, and one drops almost straight down.
        var view = camera.GlobalBasis;
        for (int i = 0; i < ToothFlight.Length; i++) {
            var (delay, velocity, spin) = ToothFlight[i];
            if (!Reached(BossContact + delay, beat, beatStep)) continue;
            Throw(bossFace.ReleaseTooth(i, office.Root, view.Z), view * velocity, spin, .009f);
        }
    }

    private const float OfficeEscapeLead = .30f;
    private Vector3 escapePlant, escapeGrip;
    private Vector2 escapeRunFrom;

    /// <summary>Keeps the weight on the lead foot as the hero lowers the bat, peels his free hand away and pushes off.</summary>
    private void StageOfficeEscape(float t, float dt) {
        const float release = .13f, bolt = .24f, accelerate = .34f;
        if (t < IntroStep * .5f) {
            escapePlant = hero.Root.ToGlobal(hero.BootSole(true).Toe);
            escapeGrip = heldBat.GlobalPosition;
        }
        var launch = ActorAnimation.OnFeet(new RigPose(Lean: 21, Twist: -7, Head: -2, RaiseL: 50, ElbowL: 76, RaiseR: 26,
            ElbowR: 72, Hips: .70f) { HipShift = .09f }, .18f, -.27f);
        if (t < bolt || Reached(bolt, t, dt)) {
            float turn = Mathf.SmoothStep(.02f, bolt, t);
            var pose = RigPose.Lerp(swingEnd, launch, turn);
            Place(hero, OfficeSwingSpot, Mathf.Lerp(OfficeSwingYaw, 180, turn), pose);
            var pivot = escapePlant - hero.Root.ToGlobal(hero.BootSole(true).Toe);
            hero.Root.GlobalPosition += new Vector3(pivot.X, 0, pivot.Z);
            if (t < bolt) {
                float lower = Mathf.SmoothStep(0, release, t);
                var grip = escapeGrip.Lerp(OfficeSwingSpot + new Vector3(-.22f, .98f, -.13f), lower);
                var lowered = new Basis(new Quaternion(Vector3.Down, new Vector3(-.26f, -.87f, -.42f).Normalized()));
                var direction = batFollowThrough.Slerp(lowered, lower);
                var fitted = BatGrip(pose, grip, direction, AisleHandSpacing, new Vector3(-1, -.7f, -.35f), new Vector3(1, -.56f, -.26f));
                float freeLeft = Mathf.SmoothStep(.036f, .17f, t), freeRight = Mathf.SmoothStep(release, bolt, t);
                var freed = ArmReach.WithArm(fitted, true, pose.RaiseL, pose.OutL, pose.ElbowL, freeLeft);
                freed = ArmReach.WithArm(freed, false, pose.RaiseR, pose.OutR, pose.ElbowR, freeRight);
                hero.Show(freed with {
                    ArmRollL = Mathf.Lerp(fitted.ArmRollL, pose.ArmRollL, freeLeft),
                    ArmRollR = Mathf.Lerp(fitted.ArmRollR, pose.ArmRollR, freeRight)
                }, null, false);
                heldBat.GlobalBasis = direction;
                if (Reached(release, t, dt)) {
                    var dropped = IntroSets.Bat();
                    office.Root.AddChild(dropped);
                    dropped.GlobalTransform = heldBat.GlobalTransform;
                    Throw(dropped, new Vector3(-.14f, -.04f, -.38f), new Vector3(155, 59, 151), .007f).Bat = true;
                    heldBat.Hide();
                }
            }
        }
        if (t < bolt) return;
        if (Reached(bolt, t, dt)) {
            Cue("music/path_to_lake_land", -5, "fadein=1.2");
            escapeRunFrom = new Vector2(hero.Root.Position.X, hero.Root.Position.Z);
            heroStep.Time = .44f * ActorAnimation.Duration(ActorAction.Run);
            Stride(hero, heroStep, escapeRunFrom, ActorAction.Run, escapeRunFrom + Vector2.Up, 0);
        }

        // Starting the run in the preceding shot leaves more time to reach the same corner.
        const float runDistance = 9.2f, runTime = OfficeEscapeLead + OfficeEscapeSeconds - bolt - accelerate / 2;
        float time = t - bolt;
        float travel = time < accelerate ? time * time / (2 * accelerate) : time - accelerate / 2;
        float distance = travel * runDistance / runTime;
        // Round the last cubicle and run along the wall of closed blinds.
        const float straight = 5.17f, radius = 1.15f;
        float around = Math.Max(0, distance - straight);
        float bend = Math.Min(around / radius, Mathf.Pi / 2);
        float alongWall = Math.Max(0, around - radius * Mathf.Pi / 2);
        var heading = new Vector2(MathF.Sin(bend), -MathF.Cos(bend));
        var at = escapeRunFrom + new Vector2(radius * (1 - MathF.Cos(bend)) + alongWall,
            -Math.Min(distance, straight) - radius * MathF.Sin(bend));
        Stride(hero, heroStep, at, ActorAction.Run, at + heading, dt, "footsteps_default", 1);
        if (time < accelerate) hero.Show(RigPose.Lerp(launch, heroStep.Pose, Mathf.SmoothStep(0, accelerate, time)), null, false);
    }

    /// <summary>The boss hits the floor and stays there while the hero runs down the aisle.</summary>
    private void OfficeEscapeShot(float t, float dt) {
        OfficeAround();
        if (Reached(BossLands, BossImpactSeconds + t, dt)) Cue("footsteps_default", 3, "rate=0.58");
        float down = StageBossFall(BossImpactSeconds + t, 1);
        bossFace.Deform(0, 1 - .85f * down);
        // The view cuts, but the pose and the falling bat have been moving since the wide return.
        float elapsed = cut ? IntroStep : dt;
        StageOfficeEscape(t + OfficeEscapeLead, elapsed);
        WatchOfficeEscape(t);
        UpdateWreckage(elapsed);
        // Move toward the middle of the aisle and look down across his shoulders, with the runner’s turn still visible beyond him.
        float settle = Mathf.SmoothStep(.14f, 1.02f, t);
        Frame(new Vector3(1.84f, 1.59f, 1.10f).Lerp(new Vector3(1.02f, 1.94f, .58f), settle),
            new Vector3(1.68f, .86f, -1.49f).Lerp(new Vector3(1.83f, .59f, -3.52f), settle), 54);
    }

    // The clerks nearest the aisle react in order: near left, across from them, then farther down on the left.
    private static readonly (int Clerk, float Delay, float HipHeight, bool FollowRunner)[] OfficeReactions = [
        (1, .48f, .70f, true), (3, .80f, .65f, false), (4, 1.04f, .56f, true),
    ];

    /// <summary>Breaks a few clerks out of their typing so they rise and look toward the fall or follow the runner.</summary>
    private void WatchOfficeEscape(float t) {
        const float seatedAnkle = .393f;
        var fallen = boss.Head.GlobalPosition;
        var runner = hero.Head.GlobalPosition;
        foreach (var (index, delay, hipHeight, followRunner) in OfficeReactions) {
            if (t <= delay) continue;
            var clerk = office.Staff[index];
            float startle = Mathf.SmoothStep(delay, delay + .16f, t);
            float rise = Mathf.SmoothStep(delay + .08f, delay + .49f, t);
            float follow = followRunner ? Mathf.SmoothStep(delay + .17f, delay + .45f, t) : 0;
            var target = fallen.Lerp(runner, follow);
            var towards = clerk.Root.ToLocal(target);
            float angle = Mathf.Clamp(Mathf.RadToDeg(ActorAnimation.Yaw(new Vector2(towards.X, towards.Z), 0)), -108, 108) * startle;
            var typing = IntroPoses.Typing(filmTime + index * 1.37f);
            var drawnBack = typing with { RaiseL = 10, ElbowL = 137, RaiseR = 13, ElbowR = 128, OutL = 8, OutR = 12 };
            var peeking = new RigPose(Lean: 8, RaiseL: -20, ElbowL: 20, RaiseR: -17, ElbowR: 24, OutL: 10, OutR: 9);
            var pose = RigPose.Lerp(RigPose.Lerp(typing, drawnBack, startle), peeking, rise) with {
                Hips = Mathf.Lerp(typing.Hips, hipHeight, rise),
                HipShift = .088f * rise,
                Twist = angle * .44f * rise,
                Sway = -angle * .075f * rise,
            };
            // Keep the shoes where they were under the desk as the weight comes off the chair.
            var planted = ActorAnimation.OnFeet(pose, seatedAnkle, seatedAnkle);
            clerk.Show(RigPose.Lerp(pose, planted, startle), null, false);
            var sight = target - clerk.Head.GlobalPosition;
            float pitch = Mathf.RadToDeg(MathF.Atan2(-sight.Y, new Vector2(sight.X, sight.Z).Length()));
            TurnHead(clerk, Mathf.Clamp(pitch, -15, 32) * startle, angle - pose.Twist * .5f, 0);
        }
    }

    /// <summary>Stops the lowest part of the falling rig at the carpet with no gap under the part that lands first.</summary>
    private void KeepBossAboveCarpet(float bounce) {
        float Under(Node3D joint, Vector3 center, Vector3 radii) {
            var basis = joint.GlobalBasis;
            var vertical = new Vector3(basis.X.Y * radii.X, basis.Y.Y * radii.Y, basis.Z.Y * radii.Z);
            return joint.ToGlobal(center).Y - vertical.Length();
        }

        float low = Under(boss.Head, new Vector3(0, .13f, 0), new Vector3(.114f, .126f, .12f));
        low = Math.Min(low, Under(boss.Head, new Vector3(0, .12f, .115f), new Vector3(.022f, .03f, .03f)));
        low = Math.Min(low, Under(boss.Torso, new Vector3(0, .23f, .045f), new Vector3(.158f, .15f, .128f)));
        low = Math.Min(low, Under(boss.Torso, new Vector3(0, .05f, 0), new Vector3(.163f, .095f, .115f)));
        foreach (bool left in new[] { true, false }) {
            low = Math.Min(low, boss.Root.ToGlobal(boss.Elbow(left)).Y - .059f);
            low = Math.Min(low, boss.Root.ToGlobal(boss.Fist(left)).Y - .049f);
            low = Math.Min(low, boss.Root.ToGlobal(boss.KneeJoint(left)).Y - .064f);
            var (toe, heel) = boss.BootSole(left);
            low = Math.Min(low, Math.Min(boss.Root.ToGlobal(toe).Y, boss.Root.ToGlobal(heel).Y) - .032f);
        }
        boss.Root.Position += Vector3.Up * (Math.Max(0, -low) + bounce);
    }

    // ---------- The room ----------

    /// <summary>Where each hand waits over the knees between reaches, in the rig's own space.</summary>
    private static readonly Vector3 RightRest = new(-.17f, .3f, .4f), LeftRest = new(.17f, .3f, .4f);

    /// <summary>Where his left hand takes hold of the pack, measured from the pack itself.</summary>
    private static readonly Vector3 PackGrip = new(-.19f, .205f, .015f);

    /// <summary>
    /// In the room at night, he works quickly, watching the door as he crams his things into the pack and zips it shut.
    /// </summary>
    private void PackingShot(float t, float dt) {
        // The recording of a bag being packed is laid so its zip falls on the flap shutting and the hand's pull across it.
        const float each = .65f, shut = 2.95f, zip = 3.3f, zipped = 3.7f, lift = 3.81f, shoulder = 4.57f, done = 4.83f, zipInRecording = 5.68f;
        if (cut) {
            Stage(room.Root);
            // The street window gives a cold edge to the warm pool from the bedside lamp.
            Light(new Vector3(1, -.34f, -.19f), "83adf5", .99f, "667997", .25f, 12);
            PaintSky("080f20", "1b2945");
            hero.Pack!.Visible = false;
            Cue("intro/bag", 6, string.Create(CultureInfo.InvariantCulture, $"from={zipInRecording - shut - .13f:0.00}\tlength=5\tfadeout=0.5"));
        }
        room.Pack.Transform = room.PackOnRug;
        // His eyes dart away from the bag a fraction before his shoulders turn.
        float alarm = Track(t, (0, .47f), (.52f, .12f), (.75f, .16f), (.92f, 1), (1.15f, .95f), (1.38f, .18f),
            (2.17f, .13f), (2.38f, .83f), (2.59f, .8f), (2.86f, .15f), (4.02f, .16f), (4.51f, 1), (5, .95f));
        float eyes = Track(t, (0, .42f), (.72f, 0), (.82f, 1), (1.12f, 1), (1.25f, 0),
            (2.15f, 0), (2.29f, .88f), (2.52f, .88f), (2.67f, 0), (4.06f, 0), (4.45f, 1));
        float lean = 51 - 9 * alarm + .8f * MathF.Sin(t * 12), twist = 8 * alarm;
        Vector3 right = RightRest, left = LeftRest;
        int rightHolds = -1, leftHolds = -1;
        for (int i = 0; i < room.Items.Count; i++) {
            var (item, lies, grip) = room.Items[i];
            bool rightHand = i % 2 == 0, torch = item == room.Torch;
            float stow = torch ? .72f : .65f;
            float u = t - (.1f + i * each);
            // Out to the thing, closing on it, over the pack's mouth, down into it and back.
            var mouth = IntroSets.PackMouth;
            var rest = rightHand ? RightRest : LeftRest;
            var inside = torch ? mouth + new Vector3(-.018f, -.111f, -.04f) : mouth;
            var hand = Path(u, (0, rest), (.24f, lies + grip), (.32f, lies + grip),
                (.53f, mouth + Vector3.Up * .19f), (stow, inside), (stow + .20f, rest));
            if (u > 0 && u < stow + .20f) { if (rightHand) right = hand; else left = hand; }
            float reaching = Mathf.SmoothStep(0, .24f, u) * (1 - Mathf.SmoothStep(.32f, .55f, u));
            lean += 11 * reaching; twist += (rightHand ? -15 : 15) * reaching;
            if (u >= .28f && u < stow) { if (rightHand) rightHolds = i; else leftHolds = i; }
            item.Visible = u < stow;
            item.Position = lies;
            if (torch) {
                // Turn the long torch diagonally and slide it under the opening before letting go.
                float tipIn = Mathf.SmoothStep(.32f, .60f, u);
                var insidePack = Basis.LookingAt(new Vector3(.15f, -.42f, .895f).Normalized(), Vector3.Up, useModelFront: true);
                item.Rotation = room.TorchOnRug.Slerp(insidePack, tipIn).GetEuler();
            } else if (i == room.Items.Count - 1) {
                float tipIn = Mathf.SmoothStep(.33f, .60f, u);
                item.RotationDegrees = new Vector3(Mathf.Lerp(90, 21, tipIn), Mathf.Lerp(70, 24, tipIn), 0);
            }
            if (Reached(stow, u, dt)) Cue(rightHand ? "putting_items_on_hotbar" : "moving_leather_in_inventory", -4);
        }
        // His right fist follows the sewn tab down, then crosses the zipper as his left hand braces the pack.
        room.Flap.RotationDegrees = new Vector3(-80 * (1 - Mathf.SmoothStep(shut - .1f, shut + .13f, t)), 0, 0);
        var tab = room.Flap.ToGlobal(new Vector3(0, .058f, .26f)) + Vector3.Up * .018f;
        float pull = Mathf.SmoothStep(zip, zipped, t);
        float across = Mathf.Lerp(.86f, -.82f, pull);
        var zipper = room.Flap.ToGlobal(BackpackShape.FlapSeam(across)) + Vector3.Up * .005f;
        right = Path(t, (shut - .27f, right), (shut - .1f, tab), (shut + .13f, tab),
            (zip, zipper + Vector3.Up * .021f), (lift, zipper + Vector3.Up * .021f));
        left = Path(t, (shut - .15f, left), (shut + .13f, room.Pack.ToGlobal(PackGrip)));
        var squat = ActorAnimation.PoseAt(ActorAction.Craft, 0, false) with { Lean = lean, Twist = twist, Head = 31 };
        (squat, float rightRoll) = ArmReach.Reaching(squat, right, Transform3D.Identity,
            elbowToward: new Vector3(-1, -.55f, -.4f));
        (squat, float leftRoll) = ArmReach.Reaching(squat, left, Transform3D.Identity, left: true,
            elbowToward: new Vector3(1, -.55f, -.4f));
        squat = squat with { ArmRollR = rightRoll, ArmRollL = leftRoll };
        var standing = new RigPose(Lean: 7, Twist: 8, Head: 2, RaiseR: -6, ElbowR: 19,
            SwingL: 12, KneeL: 17, SwingR: -12, KneeR: 14);
        // His free hand drops as he stands. The other hand catches the shoulder strap at his chest.
        (standing, float strapRoll) = ArmReach.WithHandAt(standing, true, new Vector3(.094f, .351f, .131f),
            new Vector3(.7f, -1, -.2f));
        standing = standing with { ArmRollL = strapRoll };
        float up = Mathf.SmoothStep(lift, done, t);
        float toDoor = Mathf.SmoothStep(4.26f, 4.9f, t);
        var rising = RigPose.Lerp(squat, standing, up);
        Place(hero, Vector3.Zero, -10 * up + 59 * toDoor, rising);
        room.Zipper.Visible = t >= zip - .17f && t < lift;
        room.Zipper.GlobalPosition = zipper;
        if (rightHolds >= 0) room.Items[rightHolds].Item.Position = hero.Fist(false) - room.Items[rightHolds].Grip;
        if (leftHolds >= 0) room.Items[leftHolds].Item.Position = hero.Fist(true) - room.Items[leftHolds].Grip;
        ShoulderPack(t, dt, lift, shoulder, rising);
        TurnHead(hero, -24 * alarm, 25 + 50 * alarm, -9 * alarm);
        if (toDoor > 0) {
            // Bring his eyes level with the doorway instead of carrying the crouch's lifted chin into standing.
            var towardDoor = new Vector3(1.5f - hero.Head.GlobalPosition.X, 0, -2.32f - hero.Head.GlobalPosition.Z);
            var level = Basis.LookingAt(towardDoor, Vector3.Up, useModelFront: true);
            hero.Head.GlobalBasis = hero.Head.GlobalBasis.Slerp(level, toDoor);
        }
        float blink = Mathf.SmoothStep(0, .073f, Math.Abs(t - 1.33f)) *
            Mathf.SmoothStep(0, .061f, Math.Abs(t - 2.73f));
        heroFace.Look((.86f + .36f * alarm) * blink, .77f * eyes - .28f, .73f + .27f * alarm);
        room.Lamp.LightEnergy = 1.55f;
        // The bag and his face share the frame between the window and the lamp.
        if (t >= zip - .17f && t < lift) {
            Frame(new Vector3(.61f, .64f, 1.01f), new Vector3(.01f, .30f, .57f), 39);
        } else if (t >= lift) {
            // Come out of the zipper insert from the door side, so we see his eyes when he turns to leave.
            Frame(new Vector3(1.61f, .94f + .43f * up, -.56f - .22f * up),
                new Vector3(0, .57f + .54f * up, .19f - .15f * up), 46);
        } else {
            float push = Mathf.SmoothStep(0, 2.6f, t);
            var from = new Vector3(1.51f - .15f * push, .85f - .08f * push, 1.17f - .07f * push);
            Frame(from, new Vector3(-.09f, .56f, .37f), 44);
        }
    }

    /// <summary>The pack follows the hand around one hip and meets the straps on his back.</summary>
    private void ShoulderPack(float t, float dt, float lift, float shoulder, RigPose pose) {
        room.Pack.Visible = t < shoulder;
        hero.Pack!.Visible = t >= shoulder;
        float swing = Mathf.SmoothStep(lift, shoulder, t);
        if (swing <= 0 || swing >= 1) {
            if (Reached(shoulder, t, dt)) Cue("moving_leather_in_inventory", -3);
            return;
        }
        var onBack = hero.WornPackBody!.GlobalTransform * room.PackBody.Transform.AffineInverse();
        var pack = room.PackOnRug.InterpolateWith(onBack, swing);
        var around = hero.Root.GlobalBasis.X * .37f + Vector3.Up * .11f;
        pack.Origin += around * MathF.Sin(Mathf.Pi * swing);
        room.Pack.GlobalTransform = pack;
        float hold = 1 - Mathf.SmoothStep(.62f, .98f, swing);
        if (hold > 0) {
            var grip = room.Pack.ToGlobal(PackGrip);
            var (lifting, roll) = ArmReach.Reaching(pose, grip, hero.Root.GlobalTransform, left: true, weight: hold,
                elbowToward: new Vector3(1, -.55f, -.4f));
            hero.Show(lifting with { ArmRollL = roll + pose.ArmRollL * (1 - hold) }, null, false);
        }
    }

    // ---------- The alley ----------

    private const float AlleySeconds = 4;

    /// <summary>He spots police lights at the street and turns into a side passage before he reaches them.</summary>
    private void AlleyShot(float t, float dt) {
        if (cut) {
            Stage(alley.Root);
            Light(new Vector3(.2f, -1, -.44f), "6885b3", .33f, "525c81", .42f, 15);
            PaintSky("101827", "20283b");
            hero.Pack!.Show();
        }

        float beat = t / 1.32f;
        float Pulse(float offset) => MathF.Pow(.5f + .5f * MathF.Cos(Mathf.Tau * (beat - offset)), 6);
        alley.Red.LightEnergy = .66f + 7.4f * Pulse(.12f);
        alley.Blue.LightEnergy = .79f + 8.1f * Pulse(.62f);

        // He slows at the first flash, then quickens into the passage to his right.
        float Distance(float time) {
            float hurry = Math.Max(0, time - 2.05f);
            return 1.97f * time - .33f * Mathf.SmoothStep(.35f, 1.35f, time) + .65f * hurry * hurry;
        }
        const float radius = 1.42f, entry = .46f, startZ = -3.65f;
        float distance = Distance(t), intoTurn = Math.Max(0, distance - (entry - startZ));
        float turn = Math.Min(Mathf.Pi / 2, intoTurn / radius);
        float through = Math.Max(0, intoTurn - radius * Mathf.Pi / 2);
        var at = new Vector3(.34f - radius * (1 - MathF.Cos(turn)) - through, 0,
            Math.Min(startZ + distance, entry) + radius * MathF.Sin(turn));
        float steps = distance / ActorAnimation.Stride(ActorAction.Walk);
        float gait = steps * ActorAnimation.Duration(ActorAction.Walk);
        var walk = ActorAnimation.PoseAt(ActorAction.Walk, gait, true);
        float glance = Track(t, (0, 5), (.51f, 10), (1.05f, 23), (1.65f, 7), (2.10f, -47), (2.52f, -30), (3.16f, 3), (4, 0));
        float yaw = -Mathf.RadToDeg(turn);
        Place(hero, at, yaw, walk);
        var left = hero.BootSole(true);
        var right = hero.BootSole(false);
        float duck = Mathf.SmoothStep(.48f, .93f, t) * (1 - Mathf.SmoothStep(1.63f, 2.4f, t));
        hero.Show(walk with {
            Lean = 24 + 10 * duck,
            Hips = walk.Hips - .085f - .045f * duck,
            Head = 4,
            RaiseL = walk.RaiseL + 12,
            RaiseR = walk.RaiseR + 7,
            ElbowL = 73,
            ElbowR = 62
        }, null, false);
        KeepSneakingBoot(true, left);
        KeepSneakingBoot(false, right);
        TurnHead(hero, 2, glance, -5);
        heroFace.Look(1.12f, Mathf.Clamp(glance / 60, -1, 1), .72f);
        float previousDistance = Distance(t - dt);
        float halfStride = ActorAnimation.Stride(ActorAction.Walk) / 2;
        if (MathF.Floor(distance / halfStride + .5f) > MathF.Floor(previousDistance / halfStride + .5f)) Cue("footsteps_default", -9, "rate=0.9");

        // Follow from behind, then turn to look into the passage as he leaves the flashing street behind.
        float follow = Mathf.SmoothStep(2.28f, 3.74f, t);
        float giveRoom = Mathf.SmoothStep(2.19f, 3.22f, t);
        var from = new Vector3(-1.55f + 1.2f * giveRoom, 1.22f, Math.Min(at.Z - 1.65f, .17f));
        var street = new Vector3(.72f, .8f, at.Z + 1.36f);
        var passage = new Vector3(at.X - .6f, .84f, at.Z + .14f);
        Frame(from, street.Lerp(passage, follow), 53 + 4 * follow);
    }

    /// <summary>Leaves a sneaking boot on the same step as in the walking gait, under the lowered hips.</summary>
    private void KeepSneakingBoot(bool left, (Vector3 Toe, Vector3 Heel) sole) {
        var forward = (sole.Toe - sole.Heel).Normalized();
        var side = Vector3.Up.Cross(forward).Normalized();
        var up = forward.Cross(side);
        hero.PlaceBoot(left, sole.Heel + up * ActorAnimation.AboveSole + forward * ActorAnimation.Heel,
            new Basis(side, up, forward));
    }

    // ---------- The train ----------

    private static readonly Vector3 TrainSeat = new(.26f, 0, 0);

    /// <summary>By the train window, chin in hand, watching the country go by.</summary>
    private void TrainShot(float t, float dt) {
        if (cut) {
            Stage(train.Root);
            Light(new Vector3(-1, -.36f, -.5f), "ffd9a8", 1.5f, "aab4c4", .55f, 90);
            PaintSky("2f6fd0", "c3daee");
            hero.Pack!.Visible = false;
        }
        Wind.Update(dt, [Vector2.Zero]);
        foreach (var passing in train.Passing) {
            float z = passing.Position.Z - 30 * dt;
            passing.Position = passing.Position with { Z = z < -IntroSets.TrackLoop / 2 ? z + IntroSets.TrackLoop : z };
        }
        // The car rocks on the rails, and jolts over each joint.
        const float joints = .67f;
        float joint = MathF.Exp(-Mathf.PosMod(t, joints) / joints * 9), rock = MathF.Sin(t * 2.6f);
        if (cut) Cue("intro/train", -13, "length=3\tfadein=0.25\tfadeout=0.3");
        var pose = IntroPoses.Riding(t);
        Place(hero, TrainSeat + new Vector3(0, joint * .006f, 0), 0, pose);
        RestHeadOnHand(pose, true, new Vector3(.046f, .047f, .083f), new Vector3(4, 46, -12),
            new Vector3(.88f, -1, .01f), .84f);
        heroFace.Look(.72f + .17f * Mathf.SmoothStep(0, 3, t), .22f);
        var sway = new Vector3(rock * .006f, joint * .008f, 0);
        Frame(new Vector3(.4f, 1.04f, 1.3f).Lerp(new Vector3(.38f, 1.03f, 1.12f), Mathf.SmoothStep(0, 3, t)) + sway, new Vector3(.2f, 1, 0) + sway * .5f, 46);
    }

    // ---------- The valley ----------

    /// <summary>The middle of the hero's route from the clearing down the valley to the shore, at a distance along the valley.</summary>
    private static Vector2 Trail(float along) =>
        Terrain.Point(along, Terrain.Across(Terrain.Spawn) + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f);

    /// <summary>Where the route meets the water.</summary>
    private static float TrailShore => Terrain.Waterline(Terrain.Across(Terrain.Spawn));

    private const float ForestApproachSeconds = 3, BerryPickingSeconds = 2.7f, ForestDepartureSeconds = 2.8f;
    private const float ForestTrailSeconds = 10;
    private static float ForestTrailStart => ArrivalAlong(0) - ForestTrailSeconds * World.WorkerSpeed;

    /// <summary>Walks farther down the trail, eating the berries without breaking stride.</summary>
    private void ForestDepartureShot(float t, float dt) {
        var (at, forward, side) = WalkForestTrail(ForestTrailSeconds - ForestDepartureSeconds + t, dt);
        EatBerriesWhileWalking(t, dt);
        // The tracking camera stays on his left to see the same hand from the picking shot.
        float widen = Mathf.SmoothStep(1.38f, ForestDepartureSeconds, t);
        var from = at - side * (.39f + 2.22f * widen) + forward * (1.63f - 1.04f * widen);
        var toward = at + forward * .17f;
        // Light the hand and face from the open side of the trail.
        sun.Basis = Basis.LookingAt(To3(side * .60f - forward * .38f, -.84f).Normalized(), Vector3.Up);
        Frame(StandsAt(from, 1.38f - .23f * widen), StandsAt(toward, 1.16f - .19f * widen), 43);
    }

    /// <summary>Follows the path from a low view through the ferns.</summary>
    private void ForestApproachShot(float t, float dt) {
        var (at, forward, side) = WalkForestTrail(t, dt);
        float look = Track(t, (0, -9), (1.28f, -31), (2.08f, -26), (3.05f, 6));
        TurnHead(hero, -7, look, 0);
        heroFace.Look(.94f, look / 55);
        Frame(To3(at + forward * (4.5f - .26f * t) - side * 1.13f, .57f), To3(at + forward * .4f, 1.19f), 47);
    }

    /// <summary>Follows the trail's grade and keeps the gait and sounds tied to the distance walked.</summary>
    private (Vector2 At, Vector2 Forward, Vector2 Side) WalkForestTrail(float trailTime, float dt) {
        if (cut) {
            Stage(null);
            hero.Pack!.Show();
            landscape.Forest.Show();
        }
        float along = ForestTrailStart + trailTime * World.WorkerSpeed;
        var at = Trail(along);
        var ahead = Trail(along + 1);
        Valley(.36f, at, dt, "2a64b8", "cadce7", .0029f);
        Stride(hero, heroStep, at, ActorAction.Walk, ahead, dt, "footsteps_gravel", -4);
        var forward = (ahead - at).Normalized();
        return (at, forward, new Vector2(-forward.Y, forward.X));
    }

    /// <summary>Raises the fruit from the picking hand, aims at the posed mouth and lowers the empty hand into the gait.</summary>
    private void EatBerriesWhileWalking(float t, float dt) {
        const float flick = .82f, bite = .99f;
        var pose = heroStep.Pose;
        float inspect = Track(t, (0, .20f), (.28f, 1), (.73f, .75f), (1.17f, 0));
        float nod = Track(t, (0, 0), (.30f, 14), (.78f, -6), (1.08f, -6), (1.26f, 3), (1.55f, 0));
        TurnHead(hero, nod, 21 * inspect, -4 * inspect);
        heroFace.Look(.94f - .09f * inspect, .3f * inspect);
        float opening = Track(t, (0, 0), (.52f, 0), (.78f, .91f), (bite, .8f), (1.10f, .03f),
            (1.23f, .22f), (1.34f, .025f), (1.45f, .15f), (1.60f, 0));
        heroFace.OpenMouth(opening);

        var mouth = heroFace.MouthPosition;
        var head = hero.Head.Basis;
        var front = hero.Head.GlobalBasis.Z.Normalized();
        var left = hero.Head.GlobalBasis.X.Normalized();
        var root = hero.Root.GlobalTransform;
        var carry = root * new Vector3(.13f, pose.HipHeight + .24f, .37f);
        var ready = mouth + front * .108f + left * .035f - Vector3.Up * .067f;
        float lift = Track(t, (0, 0), (.18f, 0), (.73f, .95f), (flick, 1), (.99f, .92f), (1.38f, 0));
        float carryWeight = 1 - Mathf.SmoothStep(.99f, 1.58f, t);
        if (carryWeight > 0) {
            float tip = Track(t, (flick - .07f, 0), (flick + .05f, .032f), (bite + .08f, 0));
            var hand = carry.Lerp(ready, lift) + Vector3.Up * tip;
            (pose, float roll) = ArmReach.Reaching(pose, hand, root, true,
                weight: carryWeight, elbowToward: new Vector3(.87f, -.92f, .18f));
            hero.Show(pose with { ArmRollL = roll }, null, false);
            hero.Head.Basis = head;
        }

        var berries = landscape.BerryCluster;
        berries.Visible = t < bite;
        if (berries.Visible) {
            float flight = Span(flick, bite, t);
            berries.GlobalPosition = BerryPalm.Lerp(mouth, flight) + Vector3.Up * (.025f * MathF.Sin(flight * Mathf.Pi));
        }
        if (Reached(bite, t, dt)) Cue("eat_berry", -7);
    }

    /// <summary>Where the cluster sits at the top of the hero's left hand.</summary>
    private Vector3 BerryPalm => hero.Root.ToGlobal(hero.Fist(true)) + Vector3.Up * .044f + hero.Root.GlobalBasis.Z * .012f;

    /// <summary>Stops at a bush, plucks a cluster and tips the fruit into his other hand.</summary>
    private void BerryPickingShot(float t, float dt) {
        if (cut) {
            Stage(null);
            landscape.Forest.Show();
            landscape.BerryCluster.Show();
        }
        const float pluck = 1.04f, drop = 1.62f, catchBerries = 1.82f;
        var bush = landscape.BerryBush;
        var stem = landscape.BerryStemAtRest;
        var berryAt = new Vector2(stem.X, stem.Z);
        var toward = (new Vector2(bush.Position.X, bush.Position.Z) - berryAt).Normalized();
        var actorSide = new Vector2(toward.Y, -toward.X);
        var at = berryAt - toward * .56f + actorSide * .105f;
        Valley(.36f, at, dt, "2a64b8", "cadce7", .0029f);
        float settle = Track(t, (0, .24f), (.55f, 1), (1.18f, .94f), (1.87f, .73f), (2.61f, .10f));
        var pose = ActorAnimation.OnFeet(new RigPose(Lean: 8 + 23 * settle, Head: 15 + 10 * settle,
            Hips: .73f - .135f * settle) { HipShift = .10f * settle }, .18f, -.12f);
        var root = new Transform3D(new Basis(Vector3.Up, ActorAnimation.Yaw(toward, 0)), StandsAt(at));
        var onTwig = root.AffineInverse() * stem + new Vector3(0, .012f, -.036f);
        var overPalm = new Vector3(-.07f, .99f, .36f);
        var picking = Path(t, (0, new Vector3(-.20f, .78f, .12f)), (.37f, new Vector3(-.23f, .83f, .34f)),
            (.89f, onTwig), (pluck, onTwig), (1.29f, new Vector3(-.17f, 1.02f, .44f)),
            (1.55f, overPalm), (1.74f, overPalm + Vector3.Up * .03f), (2.42f, new Vector3(-.21f, .84f, .20f)));
        var palm = Path(t, (0, new Vector3(.22f, .71f, .10f)), (.86f, new Vector3(.07f, .77f, .47f)),
            (1.68f, new Vector3(.03f, .765f, .34f)), (2.46f, new Vector3(.04f, .94f, .39f)));
        (pose, float rightRoll) = ArmReach.Reaching(pose, root * picking, root, elbowToward: new Vector3(-1, -.92f, .12f));
        (pose, float leftRoll) = ArmReach.Reaching(pose, root * palm, root, true, elbowToward: new Vector3(1, -.91f, .20f));
        Place(hero, root.Origin, Mathf.RadToDeg(ActorAnimation.Yaw(toward, 0)),
            pose with { ArmRollL = leftRoll, ArmRollR = rightRoll });
        float glance = Mathf.SmoothStep(2.05f, 2.53f, t);
        TurnHead(hero, -18 * glance, -24 * glance, -4);
        heroFace.Look(.90f + .09f * glance, -.24f * glance, -.18f);

        // A brief release of the twig kicks it back while the cluster moves with the hands.
        float spring = t >= pluck ? MathF.Sin((t - pluck) * 28) * MathF.Exp(-(t - pluck) * 10) : 0;
        bush.RotationDegrees = bush.RotationDegrees with { Z = 3.5f * spring };
        landscape.PlaceBerryOnStem();
        if (t >= pluck) {
            var taking = hero.Root.ToGlobal(hero.Fist(false)) + root.Basis.Z * .033f - Vector3.Up * .01f;
            float falling = Span(drop, catchBerries, t);
            var inHand = taking.Lerp(BerryPalm, falling * falling);
            landscape.BerryCluster.GlobalPosition = landscape.BerryCluster.GlobalPosition.Lerp(inHand, Mathf.SmoothStep(pluck, pluck + .09f, t));
        }
        if (Reached(pluck, t, dt)) Cue("rustle", -12, "length=0.40");
        float drift = Mathf.SmoothStep(0, BerryPickingSeconds, t);
        // A shaft of daylight from the path lights the fingertips and the fruit.
        sun.Basis = Basis.LookingAt(root.Basis * new Vector3(.62f, -.83f, -.42f).Normalized(), Vector3.Up);
        float handoffView = Mathf.SmoothStep(1.08f, 1.64f, t);
        Frame(root * new Vector3(-1.64f + .10f * drift, 1.19f, .69f + .27f * handoffView),
            root * new Vector3(0, 1.01f, .43f - .046f * handoffView), 43);
    }

    private const float ArrivalStop = 1.5f;
    private static float ArrivalAlong(float t) => TrailShore - 3.5f - Math.Max(0, ArrivalStop - t) * World.WorkerSpeed;

    /// <summary>He comes out of the trees onto the sand, stops, looks along the beach and lets out a long breath.</summary>
    private void ArrivalShot(float t, float dt) {
        if (cut) { Stage(null); landscape.Shore.Show(); }
        var at = Trail(ArrivalAlong(t)); var sea = Trail(TrailShore + 3);
        Valley(.47f, at, dt, "2f6fd0", "c3daee", .003f);
        bool walking = t < ArrivalStop;
        Stride(hero, heroStep, at, walking ? ActorAction.Walk : ActorAction.Idle, sea, dt);
        if (!walking) {
            float settled = Mathf.SmoothStep(ArrivalStop, ArrivalStop + .3f, t);
            if (t > 3.05f) hero.Show(RigPose.Lerp(heroStep.Pose, IntroPoses.Sigh(Span(3.05f, 4.4f, t)), Mathf.SmoothStep(3.05f, 3.2f, t)), null, false);
            // A look up the beach to his left, then down it to his right.
            float look = Track(t, (ArrivalStop, 0), (2.05f, 38), (2.35f, 38), (2.9f, -30), (3.1f, -30), (3.5f, 0));
            TurnHead(hero, 0, look * settled, 0);
            if (Reached(3.3f, t, dt)) Cue("bellows_pump", -6, "rate=0.7");
        }
        // Side on to him where he stops, the sea behind, drifting closer.
        var forward = (sea - at).Normalized(); var side = new Vector2(-forward.Y, forward.X);
        var stop = Trail(ArrivalAlong(ArrivalStop));
        Frame(To3(stop - side * (3.6f - Mathf.SmoothStep(0, 4.4f, t) * .8f) - forward * .3f, 1.3f), To3(stop + forward * .5f, 1.28f), 40);
    }

    private static readonly StandardMaterial3D RippleWater = Flat(new Color(.86f, .95f, 1, .4f));
    private readonly List<(MeshInstance3D Ring, float Born)> ripples = new();

    /// <summary>He walks down to the water's edge, squats and puts a hand in the sea.</summary>
    private void ShoreShot(float t, float dt) {
        const float go = .15f, touch = 2.5f;
        if (cut) { Stage(null); landscape.Shore.Show(); }
        float edge = TrailShore + .1f, from = ArrivalAlong(ArrivalStop), arrive = go + (edge - from) / World.WorkerSpeed;
        var at = Trail(Math.Min(edge, from + Math.Max(0, t - go) * World.WorkerSpeed)); var sea = Trail(TrailShore + 6);
        Valley(.47f, at, dt, "2f6fd0", "c3daee", .003f);
        Stride(hero, heroStep, at, t >= go && t < arrive ? ActorAction.Walk : ActorAction.Idle, sea, dt);
        if (t >= arrive) {
            // Down onto his heels, the game's crafting squat, with the right hand going out into the water and stirring it.
            var water = new Vector3(-.1f + .05f * MathF.Sin(t * 5), .04f, .56f + .03f * MathF.Cos(t * 5));
            var squat = ActorAnimation.PoseAt(ActorAction.Craft, 0, false) with { Lean = 55, Head = 24, Twist = -8, RaiseL = 44, OutL = 6, ElbowL = 70 };
            squat = Reaching(squat, false, RightRest.Lerp(water, Mathf.SmoothStep(arrive + .3f, touch, t)), out _);
            hero.Show(RigPose.Lerp(heroStep.Pose, squat, Mathf.SmoothStep(arrive, arrive + .5f, t)), null, false);
            for (int ring = 0; ring < 3; ring++) {
                float starts = touch + ring * .3f;
                if (t < starts || t - dt >= starts) continue;
                var dipped = hero.Root.ToGlobal(hero.Fist(false)) with { Y = .032f };
                var ripple = Torus(film, .94f, 1, dipped, RippleWater);
                ripple.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                ripples.Add((ripple, filmTime));
                if (ring > 0) continue;
                Burst(dipped, Splash, 6, 1.1f, .45f, 7, .035f);
                Cue("gather_water", 2);
            }
        }
        foreach (var (ring, born) in ripples) {
            float age = filmTime - born;
            ring.Scale = new Vector3(.07f + age * .45f, 1, .07f + age * .45f);
            SetFade(ring, RippleWater, Math.Min(1, age / .9f));
        }
        Fade((t - 3) / .6f);
        if (Reached(2.4f, t, dt)) Cue("music/path_to_lake_land", 0, "stop=2");
        // Low over the water just off the shore, looking back at where he comes down to it, drifting in.
        var shore = Trail(edge);
        var forward = (sea - shore).Normalized(); var side = new Vector2(-forward.Y, forward.X);
        Frame(To3(shore - side * (2.7f - Mathf.SmoothStep(0, 3.6f, t) * .5f) + forward * 1.5f, .38f), To3(shore + forward * .2f, .55f), 42);
    }

    /// <summary>He shivers by the fire, then the camera catches the wolves approaching from the dark.</summary>
    private void NightShot(float t, float dt) {
        const float howl = 1.9f, pack = 3.9f, wolfCut = 5.87f;
        var fire = Terrain.Spawn;
        var toward = Terrain.Seaward;
        var seat = fire - toward * 1.15f;
        if (cut) {
            Stage(null);
            campfire.Burn = World.Data.CampfireSeconds;
            Cue("start_fire", -4, "rate=0.8");
            Cue("music/mystic_theme", -13, "fadein=1.5\tlength=8.6\tfadeout=2.4");
            Cue("wolf_howl", -2, "far=1", howl);
            Cue("wolf_howl_group", 1, "far=1", pack);
        }
        bool close = t >= wolfCut;
        Valley(.8f, fire, dt, "070b1c", "182748", close ? .019f : .004f, stars: true);
        Fade(Math.Max(1 - t / 1.2f, (t - 6.88f) / 1.07f));
        // The first howl lifts his head toward it; when the pack answers he looks the other way and pulls in tighter.
        float scared = Mathf.SmoothStep(howl + .3f, howl + .6f, t);
        // Arms crossed over the chest, each palm on the other shoulder, the right arm outside, rubbing a little for warmth.
        float rub = .012f * MathF.Sin(t * 5.2f) * (1 - scared);
        var (huddled, leftRoll) = ArmReach.WithHandAt(IntroPoses.Shivering(t, scared), true, new Vector3(-.15f, .44f + rub, .1f), new Vector3(.5f, -1, .3f));
        (huddled, float rightRoll) = ArmReach.WithHandAt(huddled, false, new Vector3(.15f, .44f - rub, .15f), new Vector3(-.5f, -1, .45f));
        Place(hero, To3(seat), Mathf.RadToDeg(ActorAnimation.Yaw(toward, 0)), huddled);
        hero.RollArm(true, leftRoll); hero.RollArm(false, rightRoll);
        float look = Track(t, (howl + .3f, 0), (howl + .55f, 55), (pack + .2f, 50), (pack + .5f, -62), (pack + 1.9f, -55), (pack + 2.4f, -20));
        TurnHead(hero, -4 * scared, look, 0);
        heroFace.Look(.9f + .34f * scared, look / 66, .64f + .35f * scared);
        // Across the fire from him, low, drawing back and up into the dark.
        var side = new Vector2(-toward.Y, toward.X);
        float back = Mathf.SmoothStep(2.2f, 8, t);
        var from = fire + toward * (2.1f + back * 3.1f) + side * (.9f + back * 1.2f);
        var wolfEdge = seat - toward * 6.7f + side * 3.65f;
        if (close) {
            // Cut from the edge of the firelight to the shapes walking out of the tree line.
            float reveal = Span(wolfCut, 8, t);
            from = seat - toward * (1.64f - .09f * reveal) + side * 3.02f;
            var aim = wolfEdge + toward * (.66f + .68f * reveal) - side * (.48f + .38f * reveal);
            Frame(To3(from, .61f), To3(aim, .9f), 41);
        } else Frame(To3(from, .5f + back * 1.17f), To3(seat + side * (.69f * back), .66f), 43);
        moon.Visible = true;
        var moonward = To3(-toward * .97f - side * .21f, .37f).Normalized();
        moon.Position = To3(fire) + moonward * 560;
        ShowNightWolves(t, dt, seat, toward, side, from, wolfEdge, close);
    }

    /// <summary>Two wolves approach from beyond the firelight, their edges and eyes just visible before black.</summary>
    private void ShowNightWolves(float t, float dt, Vector2 seat, Vector2 toward, Vector2 side, Vector2 seenFrom, Vector2 edge, bool close) {
        float reveal = Mathf.SmoothStep(4.77f, 5.73f, t);
        wolfRim.Position = To3(edge - toward * .95f + side * 1.9f, 2.35f);
        wolfRim.LightEnergy = (close ? 1.85f : .38f) * reveal;
        for (int i = 0; i < nightWolves.Length; i++) {
            var wolf = nightWolves[i];
            float clock = t - (4.78f + i * .27f);
            float end = 2.34f - i * .36f;
            var start = edge + side * (i * 2.05f - .35f) - toward * (i * 1.28f);
            var heading = (seat - start).Normalized();
            wolf.Root.Visible = clock > 0;
            const float brake = .34f;
            float Distance(float time) {
                float u = Mathf.Clamp(time, 0, end);
                float ease = Math.Max(0, u - end + brake);
                return (.69f + i * .09f) * (u - ease * ease / (2 * brake));
            }
            float distance = Distance(clock);
            var at = start + heading * distance;
            wolf.Root.Position = StandsAt(at);
            float yaw = ActorAnimation.Yaw(heading, 0);
            wolf.Root.Rotation = Vector3.Up * yaw;
            float eyes = ActorAnimation.Yaw(seenFrom - at, yaw);
            wolf.Glance = Mathf.AngleDifference(yaw, eyes) * Mathf.SmoothStep(.32f, .79f, clock);
            float moving = distance - Distance(clock - dt);
            if (moving > .0001f) wolf.Move(moving, dt, chasing: false, wary: true);
            else wolf.Stand(t, dt, wary: true);
            if (wolf.Root.Visible) wolf.Plant();
        }
    }
}
