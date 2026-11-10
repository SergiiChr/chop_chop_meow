using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using static ChopChopMeow.ModelKit;
using RigPose = ChopChopMeow.Pose;

namespace ChopChopMeow;

/// <summary>
/// The intro film: the hero's last day at the office, the trip out, his arrival at the shore and his first night by a fire.
/// <c>--render-intro DIR</c> plays it in place of a game, under a film camera at eye level rather than the game's view from above.
/// Each frame is saved to DIR with a cue sheet of its sounds, which <c>tools/intro_video.py</c> turns into the video.
/// </summary>
public partial class WorldView {
    public const int IntroFps = 30;
    /// <summary>The film's frame size; the video tool shrinks it to the old console's resolution.</summary>
    public static readonly Vector2I IntroSize = new(1280, 720);
    private const float IntroStep = 1f / IntroFps;
    /// <summary>The cue sheet beside the frames: when each sound starts, how loud, and how it is treated.</summary>
    public const string IntroCues = "cues.tsv";

    /// <summary>Whether the film is playing, which holds the game's own camera, actors and frame updates still.</summary>
    private bool filming;
    /// <summary>True on the first frame after a cut.</summary>
    private bool cut;
    private float filmTime;
    private readonly StringBuilder cues = new();
    private Node3D film = null!, heroLids = null!, heroTie = null!, heldBat = null!, bossMouth = null!;
    private ActorRig hero = null!, boss = null!;
    private readonly ActorState heroStep = new(), bossStep = new();
    private IntroSets.Office office = null!;
    private IntroSets.Room room = null!;
    private IntroSets.Train train = null!;
    private Node3D skyline = null!;
    private MeshInstance3D skyDome = null!, moon = null!;
    private readonly ShaderMaterial skyColors = new() { Shader = new Shader { Code = SkyShader } };
    private ColorRect black = null!;
    private Thing campfire = null!;
    private readonly List<ShaderMaterial> waters = new();
    private static readonly StringName ReviewTime = "review_time", SkyTop = "top", SkyHorizon = "horizon", SkyStars = "stars";

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
        (5, DeskShot), (4, BossShot), (3, SmashShot), (5, PackingShot), (3, TrainShot), (3, ForestShot), (4.4f, ArrivalShot), (3.6f, ShoreShot),
        (2, (_, _) => Fade(1)), (8, NightShot), (1, (_, _) => Fade(1)),
    };

    /// <summary>
    /// Plays the film and saves every frame to <paramref name="folder"/>, or only the frames at <paramref name="stills"/> seconds when given.
    /// The film's clock moves one frame at a time, so the frames are evenly spaced however long each takes to draw and save.
    /// </summary>
    public async void RenderIntro(string folder, float[]? stills) {
        try {
            DirAccess.MakeDirRecursiveAbsolute(folder);
            BuildFilm();
            var shots = IntroShots;
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
                string name = still ? $"still_{filmTime:00.00}.png" : $"{saved:D5}.png";
                var result = GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}");
                if (result != Error.Ok) throw new InvalidOperationException($"Intro frame {name} was not saved: {result}");
                saved++;
            }
            using (var sheet = FileAccess.Open($"{folder}/{IntroCues}", FileAccess.ModeFlags.Write)) sheet.StoreString(cues.ToString());
            GD.Print($"INTRO RENDERED {saved} frames, {starts[^1] * IntroStep:0.00} s");
            GetTree().Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    /// <summary>Notes a sound for the video tool to lay under the film at this moment.</summary>
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
        camera.Projection = Camera3D.ProjectionType.Perspective; camera.Near = .05f; camera.Far = 700; camera.HOffset = 0; camera.VOffset = 0;
        sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits; sun.DirectionalShadowBlendSplits = true;
        environment.FogLightEnergy = 1; environment.FogSkyAffect = 0;
        film = new Node3D(); AddChild(film);
        skyDome = Sphere(film, 600, Vector3.Zero, skyColors, null, 24);
        skyDome.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        moon = Sphere(film, 1, Vector3.Zero, new StandardMaterial3D { AlbedoColor = new Color("e9edf2"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, DisableFog = true }, Vector3.One * 16, 16);
        moon.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var layer = new CanvasLayer { Layer = 50 }; AddChild(layer);
        black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(black);

        hero = new ActorRig(ActorSkin.Player); film.AddChild(hero.Root);
        // Half-shut eyelids for the office, a loosened tie, and the bat.
        heroLids = Pivot(hero.Head, Vector3.Zero);
        foreach (float side in new[] { -1f, 1f }) Box(heroLids, new Vector3(.027f, .014f, .012f), new Vector3(side * .046f, .152f, .113f), Mat("d9a27c"));
        heroTie = ActorRig.Tie(hero.Torso, "4a5368"); heroTie.RotationDegrees = new Vector3(0, 0, 5);
        heldBat = IntroSets.Bat(); hero.Hold(heldBat);
        boss = new ActorRig(ActorSkin.Boss); film.AddChild(boss.Root);
        bossMouth = Pivot(boss.Head, new Vector3(0, .066f, .112f));
        Box(bossMouth, new Vector3(.05f, .034f, .012f), Vector3.Zero, Mat("3a1512"));
        office = IntroSets.BuildOffice(); film.AddChild(office.Root);
        room = IntroSets.BuildRoom(); film.AddChild(room.Root);
        skyline = IntroSets.BuildSkyline(); film.AddChild(skyline);
        train = IntroSets.BuildTrain(); film.AddChild(train.Root);
        BuildShaders(film);
        FindWaters(scene);
        // The fire pit of the last shot stands in the clearing from the start, unlit, so it is not dropped in with a puff of dust.
        World.S.Things.RemoveAll(thing => thing.Pos.DistanceTo(Terrain.Spawn) < 2.5f);
        campfire = World.Add(ThingKind.Campfire, Terrain.Spawn);
        // The game's fog of war is lifted for the whole film.
        UpdateFog();
    }

    /// <summary>The sea's and the pond's materials, whose waves the film moves by its own clock.</summary>
    private void FindWaters(Node node) {
        if (node is MeshInstance3D { MaterialOverride: ShaderMaterial material } && material.Shader?.ResourcePath.EndsWith("lake.gdshader") == true) waters.Add(material);
        for (int i = 0; i < node.GetChildCount(); i++) FindWaters(node.GetChild(i));
    }

    // ---------- Shared by the shots ----------

    /// <summary>Puts the film camera at <paramref name="from"/>, looking at <paramref name="at"/> through a lens of <paramref name="fov"/> degrees.</summary>
    private void Frame(Vector3 from, Vector3 at, float fov) {
        camera.Position = from; camera.Basis = Basis.LookingAt(at - from, Vector3.Up); camera.Fov = fov;
    }

    /// <summary>Shows one of the built sets alone, or the valley when <paramref name="set"/> is null.</summary>
    private void Stage(Node3D? set) {
        scene.Visible = set == null;
        office.Root.Visible = set == office.Root; room.Root.Visible = skyline.Visible = set == room.Root; train.Root.Visible = set == train.Root;
        boss.Root.Visible = set == office.Root;
        heroLids.Visible = heroTie.Visible = set == office.Root;
        heldBat.Visible = false; moon.Visible = false;
        hero.Root.Scale = Vector3.One;
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
        SyncThings(dt); UpdateEffects(dt); UpdateLighting(); UpdateLook(dt); UpdateFog();
        sun.DirectionalShadowMaxDistance = 70;
        PaintSky(skyTop, skyHorizon, stars);
        environment.FogEnabled = true; environment.FogDensity = haze; environment.FogLightColor = new Color(skyHorizon);
        foreach (var water in waters) water.SetShaderParameter(ReviewTime, filmTime);
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

    /// <summary>A value eased through keys sorted by time, held before the first and after the last.</summary>
    private static float Track(float time, params (float At, float Value)[] keys) {
        for (int i = 0; i + 1 < keys.Length; i++) if (time < keys[i + 1].At) return Mathf.Lerp(keys[i].Value, keys[i + 1].Value, Eased(keys[i].At, keys[i + 1].At, time));
        return keys[^1].Value;
    }

    /// <summary>A point eased through keys sorted by time, held before the first and after the last.</summary>
    private static Vector3 Path(float time, params (float At, Vector3 Point)[] keys) {
        for (int i = 0; i + 1 < keys.Length; i++) if (time < keys[i + 1].At) return keys[i].Point.Lerp(keys[i + 1].Point, Eased(keys[i].At, keys[i + 1].At, time));
        return keys[^1].Point;
    }

    /// <summary>
    /// Bends one arm of a pose so its fist goes to a point in the rig's own space, or as near as the arm is long.
    /// <paramref name="fist"/> is where the fist ends up, for whatever it holds.
    /// </summary>
    private static RigPose Reaching(RigPose pose, bool left, Vector3 target, out Vector3 fist) {
        // The upper arm and forearm lengths, and where the shoulder sits on the torso, as ActorRig builds them.
        const float upper = .28f, fore = .27f;
        var shoulder = new Vector3(left ? .17f : -.17f, .45f, 0);
        var torso = Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Lean), Mathf.DegToRad(pose.Twist), Mathf.DegToRad(pose.Sway)));
        var hips = new Vector3(0, pose.HipHeight, 0);
        var to = torso.Inverse() * (target - hips) - shoulder;
        float reach = Math.Clamp(to.Length(), .1f, upper + fore - .005f);
        to = to.Normalized() * reach;
        fist = hips + torso * (shoulder + to);
        // The elbow folds until the fist is that far from the shoulder, then the shoulder swings the bent arm onto the point.
        float elbow = MathF.Acos(Math.Clamp((reach * reach - upper * upper - fore * fore) / (2 * upper * fore), -1, 1));
        float down = -upper - fore * MathF.Cos(elbow), ahead = fore * MathF.Sin(elbow);
        float outward = MathF.Asin(Math.Clamp(to.X / -down, -1, 1));
        float raise = Mathf.RadToDeg(MathF.Atan2(ahead, down * MathF.Cos(outward)) - MathF.Atan2(to.Z, to.Y));
        raise = Mathf.PosMod(raise + 180, 360) - 180;
        return left ? pose with { RaiseL = raise, OutL = Mathf.RadToDeg(outward), ElbowL = Mathf.RadToDeg(elbow) }
            : pose with { RaiseR = raise, OutR = -Mathf.RadToDeg(outward), ElbowR = Mathf.RadToDeg(elbow) };
    }

    private static float Span(float from, float to, float time) => Math.Clamp((time - from) / (to - from), 0, 1);
    private static float Eased(float from, float to, float time) => Ease(Span(from, to, time));

    // ---------- Flying wreckage ----------

    private sealed class Wreck {
        public Node3D Node = null!;
        public Vector3 Velocity, Spin;
        public float Floor, Life = float.MaxValue, Age;
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
            if (at.Y < wreck.Floor) {
                at.Y = wreck.Floor;
                wreck.Velocity = new Vector3(wreck.Velocity.X * .45f, -wreck.Velocity.Y * .28f, wreck.Velocity.Z * .45f); wreck.Spin *= .4f;
                if (MathF.Abs(wreck.Velocity.Y) < .4f) { wreck.Velocity = Vector3.Zero; wreck.Spin = Vector3.Zero; }
            }
            wreck.Node.Position = at;
            wreck.Node.RotationDegrees += wreck.Spin * dt;
            if (wreck.Life < 10) wreck.Node.Scale = Vector3.One * (1 - wreck.Age / wreck.Life);
        }
    }

    // ---------- The office ----------

    private static readonly Vector3 BossSpot = new(.86f, 0, .12f);
    /// <summary>Seconds into the desk shot at which the boss is first heard, and into his recording at which the yelling starts.</summary>
    private const float BossHeard = 3.35f, BossVoiceStarts = .95f;
    /// <summary>How loud <c>assets/intro/boss.mp3</c> is, in decibels, every tenth of a second.</summary>
    private static readonly sbyte[] BossLoudness = { -48, -57, -62, -65, -67, -69, -68, -70, -68, -60, -29, -23, -26, -31, -36, -33, -27, -26, -25, -32, -42, -25, -26, -26, -28, -40, -33, -25, -25, -39, -27, -37, -23, -29, -28, -25, -27, -24, -27, -23, -21, -23, -27, -41, -30, -20, -24, -31, -23, -27, -24, -22, -25, -29, -28, -34, -40, -17, -19, -20, -30, -31, -16, -13, -14, -16, -23, -47, -59, -67, -40, -43, -45, -50 };

    private void OfficeLight() {
        Stage(office.Root);
        Light(new Vector3(.25f, -1, .2f), "eef2ff", .9f, "c4c3c0", .9f, 14);
        PaintSky("9fb4c8", "dfe6e2");
        hero.Pack!.Visible = false;
    }

    private void Colleagues(float time) {
        for (int i = 0; i < office.Staff.Count; i++) office.Staff[i].Show(IntroPoses.Typing(time + i * 1.37f), null, false);
        // The tube's glow wavers a little on his face.
        office.Glow.LightEnergy = .12f + .012f * MathF.Sin(time * 31) + .008f * MathF.Sin(time * 7.3f);
    }

    /// <summary>Bored at his desk, cheek on fist, while the boss comes up the aisle behind him.</summary>
    private void DeskShot(float t, float dt) {
        // The camera sits where his screen is, so the monitor itself is out of the picture.
        if (cut) { OfficeLight(); office.LeaningBat.Visible = true; }
        office.Monitor.Visible = false;
        Fade(1 - t / .7f);
        Colleagues(t);
        Place(hero, Vector3.Zero, 0, IntroPoses.Bored(t));
        TurnHead(hero, 4, -6, 22);
        // A slow blink, lids already half shut; the boss's voice takes a while to reach him, then the eyes open and the head comes up.
        float roused = Eased(4.25f, 4.6f, t);
        TurnHead(hero, -8 * roused, 0, -12 * roused);
        float blink = MathF.Pow(Math.Max(0, MathF.Sin(t * 1.9f + .6f)), 14) * (1 - roused);
        heroLids.Scale = new Vector3(1, Math.Max(.01f, 1 + blink - roused), 1); heroLids.Position = new Vector3(0, -.007f * blink + .007f * roused, 0);
        var aisle = new Vector2(1.75f, -7.6f + t * 1.25f);
        Stride(boss, bossStep, aisle, ActorAction.Walk, aisle + Vector2.Down, dt, "footsteps_default", -12);
        bossMouth.Scale = new Vector3(1, .01f, 1);
        // Two recordings of an office hum under all three shots here, and the yelling starts before the boss is seen.
        if (cut) { Cue("intro/office", 2, "length=12\tfadein=0.4\tfadeout=0.8"); Cue("intro/office_room", -6, "length=12\tfadein=0.4\tfadeout=0.8"); }
        if (t >= BossHeard && t - dt < BossHeard) Cue("intro/boss", -3, string.Create(CultureInfo.InvariantCulture, $"from={BossVoiceStarts:0.00}\tlength=5.8\tfadeout=0.1"));
        float push = Eased(0, 5, t);
        Frame(new Vector3(.2f, 1.08f, 1.05f).Lerp(new Vector3(.12f, 1.05f, .82f), push), new Vector3(-.05f, .97f, .2f), 44);
    }

    /// <summary>The boss leans over him, yelling and jabbing a finger; he shrinks into the chair.</summary>
    private void BossShot(float t, float dt) {
        if (cut) { OfficeLight(); office.Monitor.Visible = true; }
        Colleagues(5 + t);
        // His mouth opens as wide as the recording is loud at this moment.
        float voice = BossVoiceStarts + 5 + t - BossHeard;
        float shout = Math.Clamp((BossLoudness[Math.Clamp((int)(voice * 10), 0, BossLoudness.Length - 1)] + 40) / 20f, 0, 1);
        Place(hero, Vector3.Zero, 0, IntroPoses.Cowering(t));
        TurnHead(hero, 0, 38, 0);
        heroLids.Scale = new Vector3(1, .01f, 1);
        Place(boss, BossSpot, -100, IntroPoses.Yelling(t));
        bossMouth.Scale = new Vector3(1 + shout * .2f, .25f + shout, 1);
        // From the desk top, looking up past his face at the boss, with a tremor on every shout.
        var tremor = new Vector3(MathF.Sin(t * 47), MathF.Sin(t * 61 + 1), 0) * .006f * shout;
        Frame(new Vector3(-.6f, .84f, .34f).Lerp(new Vector3(-.52f, .86f, .3f), Eased(0, 4, t)) + tremor, new Vector3(.5f, 1.26f, .05f), 54);
    }

    private readonly Random smashRandom = new(7);

    /// <summary>He takes the bat to his computer, twice, drops it and runs for the door.</summary>
    private void SmashShot(float t, float dt) {
        const float drop = 1.9f, bolt = 2.02f;
        if (cut) {
            OfficeLight(); office.LeaningBat.Visible = false;
            office.Chair.Position += new Vector3(-.35f, 0, -1.25f); office.Chair.RotationDegrees = new Vector3(0, 40, 84);
            office.Chair.Position += Vector3.Up * .2f;
        }
        Colleagues(9 + t);
        heroLids.Visible = false;
        heldBat.Visible = t < drop;
        bool first = t >= IntroPoses.FirstBlow && t - dt < IntroPoses.FirstBlow, second = t >= IntroPoses.SecondBlow && t - dt < IntroPoses.SecondBlow;
        if (dt > 0 && (Mathf.RoundToInt((t - .45f) * IntroFps) == 0 || Mathf.RoundToInt((t - 1.26f) * IntroFps) == 0)) Cue("tool_air_swing", 1);
        if (first) {
            // The tube bursts: the screen dies, the case caves in and glass flies.
            office.Screen.AlbedoColor = new Color("0e1114"); office.Screen.EmissionEnabled = false; office.Glow.Visible = false;
            office.Monitor.Scale = new Vector3(1, .78f, 1); office.Monitor.RotationDegrees = new Vector3(9, 0, -6);
            Shatter(office.Monitor.Position + new Vector3(0, .4f, -.1f), new Vector3(0, 0, .6f), 16, 22, smashRandom);
            Throw(office.Mug, new Vector3(.9f, 1.6f, -.4f), new Vector3(300, 0, 500), .04f);
            Cue("intro/hit", -3, "from=0.26\tlength=0.46");
        }
        if (second) {
            // The second blow sends what is left of it off the back of the desk and the keyboard into the air.
            Throw(office.Monitor, new Vector3(.2f, 3.8f, 1.8f), new Vector3(-420, 60, 90), .16f);
            Throw(office.Keyboard, new Vector3(-.5f, 2.9f, -.7f), new Vector3(540, 200, -300), .02f);
            Shatter(office.Monitor.Position + new Vector3(0, .25f, -.1f), new Vector3(0, .5f, .4f), 12, 14, smashRandom);
            // The recording's next hit, a little lower, with the crash that follows it.
            Cue("intro/hit", -2, "from=0.74\trate=0.9");
        }
        if (t >= drop && t - dt < drop) {
            var dropped = IntroSets.Bat(); office.Root.AddChild(dropped);
            dropped.GlobalTransform = heldBat.GlobalTransform;
            Throw(dropped, new Vector3(-.4f, .6f, .2f), new Vector3(200, 0, 260), .04f);
            Cue("wood_rocks_inventory", -2, after: .3f);
            Cue("music/path_to_lake_land", -5, "fadein=1.2");
        }
        UpdateWreckage(dt);
        if (t < bolt) Place(hero, new Vector3(0, 0, -.1f), 0, IntroPoses.Smashing(t));
        else {
            // Out of the cubicle into the aisle, then down it and away.
            float run = (t - bolt) * World.Data.MoveSpeed(sprinting: true);
            var from = new Vector2(0, -.1f); var corner = new Vector2(1.2f, -.75f);
            float turn = from.DistanceTo(corner);
            var at = run < turn ? from.Lerp(corner, run / turn) : corner + Vector2.Up * (run - turn);
            if (t - dt < bolt) Stride(hero, heroStep, from, ActorAction.Run, corner, 0);
            Stride(hero, heroStep, at, ActorAction.Run, at + Vector2.Up, dt, "footsteps_default", 1);
        }
        // The boss is thrown back by the first blow and watches him go.
        float aghast = Eased(IntroPoses.FirstBlow, IntroPoses.FirstBlow + .25f, t);
        Place(boss, new Vector3(1.45f, 0, -1) + new Vector3(.65f, 0, -.2f) * aghast, -55 - 100 * Eased(bolt + .1f, bolt + .7f, t), RigPose.Lerp(IntroPoses.Yelling(0), IntroPoses.Aghast(t), aghast));
        bossMouth.Scale = new Vector3(1, .3f + aghast * .9f, 1);
        // From the aisle, side on to the blows, then swinging round to watch him go.
        float swing = Eased(bolt - .1f, 2.9f, t);
        float jolt = (first || second ? 1 : 0) * .02f;
        Frame(new Vector3(2.05f, 1.4f + jolt, .95f), new Vector3(0, .95f, .4f).Lerp(new Vector3(1.2f, .95f, -5), swing), 50);
    }

    // ---------- The room ----------

    /// <summary>Where each hand waits over the knees between reaches, in the rig's own space.</summary>
    private static readonly Vector3 RightRest = new(-.17f, .3f, .4f), LeftRest = new(.17f, .3f, .4f);

    /// <summary>
    /// Squatting on the rug at home, he picks up what he will need, one thing a hand, and lowers each into the pack.
    /// Then he shuts the flap, zips it, shoulders the pack and stands with his fists on his hips.
    /// </summary>
    private void PackingShot(float t, float dt) {
        // The recording of a bag being packed is laid so its zip falls on the flap shutting and the hand's pull across it.
        const float each = .65f, shut = 2.95f, zip = 3.3f, zipped = 3.75f, rise = 3.85f, shoulder = 4.2f, done = 4.6f, zipInRecording = 5.68f;
        if (cut) {
            Stage(room.Root);
            // The low evening sun comes through the window; the lamp fills the rest.
            Light(new Vector3(-.32f, -.42f, -1), "ffa35e", 2.1f, "8a7888", .75f, 12);
            PaintSky("46508c", "f2a25c");
            hero.Pack!.Visible = false;
            Cue("intro/bag", 6, string.Create(CultureInfo.InvariantCulture, $"from={zipInRecording - shut - .13f:0.00}\tlength=5\tfadeout=0.5"));
        }
        // The game's crafting squat, with the body leaning and turning toward whatever a hand goes out for.
        float lean = 46, twist = 0;
        Vector3 right = RightRest, left = LeftRest;
        int rightHolds = -1, leftHolds = -1;
        for (int i = 0; i < room.Items.Count; i++) {
            var (item, lies, grip) = room.Items[i];
            bool rightHand = i % 2 == 0;
            float u = t - (.1f + i * each);
            // Out to the thing, closing on it, over the pack's mouth, down into it and back.
            var mouth = IntroSets.PackMouth;
            var hand = Path(u, (0, rightHand ? RightRest : LeftRest), (.32f, lies + grip), (.42f, lies + grip), (.68f, mouth + Vector3.Up * .2f), (.8f, mouth), (1, rightHand ? RightRest : LeftRest));
            if (u > 0 && u < 1) { if (rightHand) right = hand; else left = hand; }
            float reaching = Eased(0, .3f, u) * (1 - Eased(.42f, .68f, u));
            lean += 11 * reaching; twist += (rightHand ? -15 : 15) * reaching;
            if (u >= .37f && u < .8f) { if (rightHand) rightHolds = i; else leftHolds = i; }
            item.Visible = u < .8f;
            if (u >= .8f && u - dt < .8f) Cue(i % 2 == 0 ? "putting_items_on_hotbar" : "moving_leather_in_inventory", -4);
        }
        // The right hand swings the flap shut and pulls the zip across while the left steadies the pack.
        right = Path(t, (shut - .25f, right), (shut - .05f, new Vector3(0, .44f, .5f)), (shut + .13f, new Vector3(0, .3f, .62f)), (zip, new Vector3(-.15f, .27f, .66f)), (zipped, new Vector3(.13f, .27f, .6f)), (rise, new Vector3(-.15f, .22f, .6f)));
        left = Path(t, (shut - .05f, left), (shut + .2f, new Vector3(.17f, .22f, .6f)));
        room.Flap.RotationDegrees = new Vector3(-80 * (1 - Eased(shut - .1f, shut + .13f, t)), 0, 0);
        var squat = ActorAnimation.PoseAt(ActorAction.Craft, 0, false) with { Lean = lean, Twist = twist, Head = 20 };
        squat = Reaching(Reaching(squat, false, right, out var rightFist), true, left, out var leftFist);
        if (rightHolds >= 0) room.Items[rightHolds].Item.Position = rightFist - room.Items[rightHolds].Grip;
        if (leftHolds >= 0) room.Items[leftHolds].Item.Position = leftFist - room.Items[leftHolds].Grip;
        // Up with the pack: it goes from the rug onto his back as he rises.
        room.Pack.Visible = t < shoulder; hero.Pack!.Visible = t >= shoulder;
        if (t >= shoulder && t - dt < shoulder) Cue("moving_leather_in_inventory", -3);
        var lifting = new RigPose(Lean: 30, Head: 10, RaiseL: 70, OutL: -6, ElbowL: 70, RaiseR: 70, OutR: -6, ElbowR: 70, SwingL: 62, KneeL: 104, SwingR: 50, KneeR: 96);
        // All done: on his feet with his chest out, fists on his hips, and a nod at the room.
        var standing = new RigPose(Lean: -3, Head: -4, SwingL: 4, SwingR: -3);
        standing = Reaching(Reaching(standing, false, new Vector3(-.22f, standing.HipHeight + .07f, 0), out _), true, new Vector3(.22f, standing.HipHeight + .07f, 0), out _);
        Place(hero, Vector3.Zero, 0, IntroPoses.Keyed(t, (rise, squat), (shoulder, lifting), (done, standing)));
        TurnHead(hero, 9 * MathF.Sin(Span(done + .05f, done + .35f, t) * MathF.PI), 0, 0);
        room.Lamp.LightEnergy = 1.1f + .04f * MathF.Sin(t * 23);
        // Knee high beside the rug, looking down over the pack, rising with him as he stands.
        float up = Eased(rise, done, t);
        Frame(new Vector3(1.4f, .74f, 1.5f).Lerp(new Vector3(1.25f, .78f, 1.62f), Eased(0, 5, t)) + Vector3.Up * .3f * up, new Vector3(-.05f, .28f + .76f * up, .42f - .36f * up), 44);
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
        Wind.Update(dt, Vector2.Zero);
        foreach (var passing in train.Passing) {
            float z = passing.Position.Z - 30 * dt;
            passing.Position = passing.Position with { Z = z < -IntroSets.TrackLoop / 2 ? z + IntroSets.TrackLoop : z };
        }
        // The car rocks on the rails, and jolts over each joint.
        const float joints = .67f;
        float joint = MathF.Exp(-Mathf.PosMod(t, joints) / joints * 9), rock = MathF.Sin(t * 2.6f);
        if (cut) Cue("intro/train", -13, "length=3\tfadein=0.25\tfadeout=0.3");
        Place(hero, TrainSeat + new Vector3(0, joint * .006f, 0), 0, IntroPoses.Riding(t));
        TurnHead(hero, 4, 46, -12);
        var sway = new Vector3(rock * .006f, joint * .008f, 0);
        Frame(new Vector3(.4f, 1.04f, 1.3f).Lerp(new Vector3(.38f, 1.03f, 1.12f), Eased(0, 3, t)) + sway, new Vector3(.2f, 1, 0) + sway * .5f, 46);
    }

    // ---------- The valley ----------

    /// <summary>Where the trail meets the water.</summary>
    private static float TrailShore => Terrain.Coast(Terrain.Across(Terrain.Spawn)) - 1.2f;

    /// <summary>Walking the trail through the forest, the pack on his back.</summary>
    private void ForestShot(float t, float dt) {
        if (cut) { Stage(null); hero.Pack!.Visible = true; }
        float along = TrailShore - 30 + t * World.WorkerSpeed;
        var at = Trail(along); var ahead = Trail(along + 1);
        Valley(.36f, at, dt, "2f6fd0", "c3daee", .0035f);
        Stride(hero, heroStep, at, ActorAction.Walk, ahead, dt);
        // Backing down the trail ahead of him, low in the grass, looking up into the trees.
        var forward = (ahead - at).Normalized(); var side = new Vector2(-forward.Y, forward.X);
        Frame(To3(at + forward * (5.2f - t * .25f) - side * .6f, .6f), To3(at + forward * .4f, 1.25f), 42);
    }

    private const float ArrivalStop = 1.5f;
    private static float ArrivalAlong(float t) => TrailShore - 3.5f - Math.Max(0, ArrivalStop - t) * World.WorkerSpeed;

    /// <summary>He comes out of the trees onto the sand, stops, looks along the beach and lets out a long breath.</summary>
    private void ArrivalShot(float t, float dt) {
        if (cut) Stage(null);
        var at = Trail(ArrivalAlong(t)); var sea = Trail(TrailShore + 3);
        Valley(.47f, at, dt, "2f6fd0", "c3daee", .003f);
        bool walking = t < ArrivalStop;
        Stride(hero, heroStep, at, walking ? ActorAction.Walk : ActorAction.Idle, sea, dt);
        if (!walking) {
            float settled = Eased(ArrivalStop, ArrivalStop + .3f, t);
            if (t > 3.05f) hero.Show(RigPose.Lerp(heroStep.Pose, IntroPoses.Sigh(Span(3.05f, 4.4f, t)), Eased(3.05f, 3.2f, t)), null, false);
            // A look up the beach to his left, then down it to his right.
            float look = Track(t, (ArrivalStop, 0), (2.05f, 38), (2.35f, 38), (2.9f, -30), (3.1f, -30), (3.5f, 0));
            TurnHead(hero, 0, look * settled, 0);
            if (t >= 3.3f && t - dt < 3.3f) Cue("bellows_pump", -6, "rate=0.7");
        }
        // Side on to him where he stops, the sea behind, drifting closer.
        var forward = (sea - at).Normalized(); var side = new Vector2(-forward.Y, forward.X);
        var stop = Trail(ArrivalAlong(ArrivalStop));
        Frame(To3(stop - side * (3.6f - Eased(0, 4.4f, t) * .8f) - forward * .3f, 1.3f), To3(stop + forward * .5f, 1.28f), 40);
    }

    private static readonly StandardMaterial3D RippleWater = Flat(new Color(.86f, .95f, 1, .4f));
    private readonly List<(MeshInstance3D Ring, float Born)> ripples = new();

    /// <summary>He walks down to the water's edge, squats and puts a hand in the sea.</summary>
    private void ShoreShot(float t, float dt) {
        const float go = .15f, touch = 2.5f;
        if (cut) Stage(null);
        float edge = TrailShore + .1f, from = ArrivalAlong(ArrivalStop), arrive = go + (edge - from) / World.WorkerSpeed;
        var at = Trail(Math.Min(edge, from + Math.Max(0, t - go) * World.WorkerSpeed)); var sea = Trail(TrailShore + 6);
        Valley(.47f, at, dt, "2f6fd0", "c3daee", .003f);
        Stride(hero, heroStep, at, t >= go && t < arrive ? ActorAction.Walk : ActorAction.Idle, sea, dt);
        if (t >= arrive) {
            // Down onto his heels, the game's crafting squat, with the right hand going out into the water and stirring it.
            var water = new Vector3(-.1f + .05f * MathF.Sin(t * 5), .04f, .56f + .03f * MathF.Cos(t * 5));
            var squat = ActorAnimation.PoseAt(ActorAction.Craft, 0, false) with { Lean = 55, Head = 24, Twist = -8, RaiseL = 44, OutL = 6, ElbowL = 70 };
            squat = Reaching(squat, false, RightRest.Lerp(water, Eased(arrive + .3f, touch, t)), out var fist);
            hero.Show(RigPose.Lerp(heroStep.Pose, squat, Eased(arrive, arrive + .5f, t)), null, false);
            for (int ring = 0; ring < 3; ring++) {
                float starts = touch + ring * .3f;
                if (t < starts || t - dt >= starts) continue;
                var dipped = hero.Root.Transform * fist with { Y = .032f };
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
        if (t >= 2.4f && t - dt < 2.4f) Cue("music/path_to_lake_land", 0, "stop=2");
        // Low over the water just off the shore, looking back at where he comes down to it, drifting in.
        var shore = Trail(edge);
        var forward = (sea - shore).Normalized(); var side = new Vector2(-forward.Y, forward.X);
        Frame(To3(shore - side * (2.7f - Eased(0, 3.6f, t) * .5f) + forward * 1.5f, .38f), To3(shore + forward * .2f, .55f), 42);
    }

    /// <summary>Night: he sits hunched at a fire, shivering, and wolves howl somewhere out in the dark.</summary>
    private void NightShot(float t, float dt) {
        const float howl = 1.9f, pack = 3.9f;
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
        Valley(.8f, fire, dt, "070b1c", "141d38", .004f, stars: true);
        moon.Visible = true; moon.Position = camera.Position + new Vector3(-.5f, .42f, .75f).Normalized() * 560;
        Fade(Math.Max(1 - t / 1.2f, (t - 6.2f) / 1.6f));
        // The first howl lifts his head toward it; when the pack answers he looks the other way and pulls in tighter.
        float scared = Eased(howl + .3f, howl + .6f, t);
        Place(hero, To3(seat), Mathf.RadToDeg(ActorAnimation.Yaw(toward, 0)), IntroPoses.Shivering(t, scared));
        float look = Track(t, (howl + .3f, 0), (howl + .55f, 55), (pack + .2f, 50), (pack + .5f, -62), (pack + 1.9f, -55), (pack + 2.4f, -20));
        TurnHead(hero, -4 * scared, look, 0);
        // Across the fire from him, low, drawing back and up into the dark.
        var side = new Vector2(-toward.Y, toward.X);
        float back = Eased(2.2f, 8, t);
        Frame(To3(fire + toward * (2.1f + back * 4.2f) + side * (.9f + back * 1.4f), .5f + back * 2.6f), To3(seat, .62f), 40);
    }
}
