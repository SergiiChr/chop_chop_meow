using System;
using System.Collections.Generic;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// Renders the simulation as a 3D scene under an orthographic isometric camera.
/// World (x, y) maps to 3D (x, 0, y); one tile is one unit and an adult is about 1.65 units tall.
/// </summary>
public partial class WorldView : Node3D {
    public World World = null!;
    public Selection Selection;
    public int Hover;
    public int HoverWorker;
    /// <summary>The building being placed, or a packed crate, or null when not placing.</summary>
    public ThingKind? Placing;
    /// <summary>The crate the player is lifting with a held right click, and how far along the lift is from 0 to 1.</summary>
    public int Lifting;
    public float LiftProgress;
    public Vector2 Mouse;
    public float Zoom = 1;
    public bool Gathering;
    public Thing? GatheringTarget;
    public string? SelectedTool;
    public bool Paused;
    private bool worldReplaced;
    /// <summary>One model of everything, drawn behind the ground for the first frames: a pipeline is built on its first draw, which stalls that frame, up to a second on a machine that has not played yet.</summary>
    private Node3D? warmStage;
    private int warmFrames = 3;
    /// <summary>From the camera along its view: past the ground it looks at, by more than the tallest model, and still inside what it draws and casts shadows for.</summary>
    private const float WarmStageDepth = 85;
    public bool AnimationReview;
    /// <summary>Seconds the clock moves each frame while the animation review is captured, so frames are evenly spaced whatever the frame rate; null follows real time.</summary>
    public float? CaptureStep;
    /// <summary>A fixed daylight landscape view with the lake and sea waves held at one moment, for <c>--environment-review</c>; set before the view enters the tree.</summary>
    public bool EnvironmentReview;
    public bool EnvironmentReviewFog;
    public Vector2? EnvironmentCenter;
    public float EnvironmentCameraDistance = 60;
    public float Elapsed;
    public bool PanelOpen;
    public Sounds? Sounds;
    // A 30° pitch keeps the old 2:1 isometric footprint; this ortho height matches the old 85 px actor scale at zoom 1.
    private const float Pitch = 30, BaseSize = 14.5f;
    /// <summary>Degrees the camera is turned round the player from where the game puts it, and its pitch in place of the game's, for a review from another side.</summary>
    public float CameraTurn;
    public float? CameraPitch;
    private Camera3D camera = null!;
    private DirectionalLight3D sun = null!;
    private Godot.Environment environment = null!;
    /// <summary>Never drawn: its light from above and bounce from below is the ambient light, so a roof and a wall differ in shade.</summary>
    private ProceduralSkyMaterial skylight = null!;
    private Node3D scene = null!;
    private Overlay overlay = null!;
    private readonly Dictionary<int, Prop> props = new();
    private readonly ActorState player = new();
    private ActorRig playerRig = null!;
    private string playerShirt = "";
    /// <summary>The other players in the game, keyed by their id.</summary>
    private readonly Dictionary<int, Avatar> others = new();
    // The host keeps the original teal; each guest keeps the first one free when it joined.
    private static readonly string[] Shirts = { "2f7f86", "a8473a", "7a5c9e", "c9a23b" };
    private readonly Dictionary<int, (ActorRig Rig, ActorState State, (MeshInstance3D Mesh, Material Material)[] Parts)> crew = new();
    private readonly Dictionary<Wolf, WolfModel> wolves = new();
    private readonly Dictionary<TradeOrder, WorldModels.CartModel> carts = new();
    /// <summary>The village's carts no order holds, built while loading and reused for every trip.</summary>
    private readonly Stack<WorldModels.CartModel> idleCarts = new();
    private readonly Dictionary<int, float> pumping = new();
    /// <summary>The cooking tables whose mortar is in someone's hands this frame, so it is off the counter.</summary>
    private readonly HashSet<int> grinding = new();
    /// <summary>Build-mode ghosts by building kind, made hidden while loading; mines have one per cliff ore instead.</summary>
    private readonly Dictionary<ThingKind, Prop> ghosts = new(), mineGhosts = new();
    private Prop? ghost;
    private MeshInstance3D selectRing = null!, hoverRing = null!, workerRing = null!, playerRing = null!, placeTile = null!;
    private float pulseRemaining;
    private ActorAction pulseAction;
    private Vector2 pulseTarget;
    private float night;
    private float downedFor;
    private Vector2 mauledFrom;
    private readonly Font font = ThemeDB.FallbackFont;
    private static readonly StandardMaterial3D GhostValid = Flat(new Color(.55f, .95f, .45f, .45f));
    private static readonly StandardMaterial3D GhostBlocked = Flat(new Color(1, .35f, .3f, .5f));
    private static readonly StandardMaterial3D NightGlass = Mat("ffd58a", .3f, 0, 1.6f);
    private static readonly StandardMaterial3D Sparks = Flat(new Color("ffd27a"));
    private static readonly StandardMaterial3D GatherDust = Flat(new Color(.8f, .78f, .7f, .45f));
    private static readonly StandardMaterial3D Soil = Mat("5a4330", .95f);
    private static readonly StandardMaterial3D Splash = Flat(new Color(.7f, .88f, .95f, .8f));
    private static readonly StandardMaterial3D Embers = Flat(new Color("ffb347"));
    private static readonly StandardMaterial3D FallDust = Flat(new Color(.7f, .66f, .52f, .45f));
    private static readonly StandardMaterial3D Cinders = Flat(new Color("ff9a3a"));
    private static readonly StandardMaterial3D DayGlass = Mat("8fb4c4", .15f, .2f);
    private static readonly Color TagText = new Color("f5ecd1");
    private static readonly Color BarTrack = new Color("2b4135");
    private static readonly Color BarFill = new Color("e5bf68");
    private static readonly Color HeatReady = new Color("f0a03c");
    private static readonly Color HeatLow = new Color("8a5a46");
    private static readonly Color HeatTick = new Color("f2e8cf");
    private readonly ShaderMaterial fog = new() { Shader = new Shader { Code = FogShader }, RenderPriority = (int)Material.RenderPriorityMax };
    private Image explored = null!;
    private ImageTexture exploredTexture = null!;
    private int exploredRevision = -1;
    // Drawn once over the whole screen after everything else: black where never explored, a grey wash over remembered ground, clear inside the sight circle.
    // Each pixel's place comes from the depth buffer, so swaying leaves and grass and cut-out leaf cards are covered exactly as drawn.
    // Texel centres sit on whole tiles, matching where the simulation measures sight.
    private const string FogShader = """
        shader_type spatial;
        render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;
        uniform sampler2D depth : hint_depth_texture, filter_nearest;
        uniform sampler2D explored : filter_linear, repeat_disable;
        uniform vec2 player;
        uniform float radius;
        uniform float map_size;
        uniform vec3 memory : source_color;
        void vertex() {
            POSITION = vec4(VERTEX.xy, 1.0, 1.0);
        }
        void fragment() {
            float depth_at = texture(depth, SCREEN_UV).r;
            // OpenGL's depth runs from -1 to 1 like its screen coordinates; Vulkan's runs from 0 to 1.
            #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
            vec3 ndc = vec3(SCREEN_UV, depth_at) * 2.0 - 1.0;
            #else
            vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth_at);
            #endif
            vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
            vec2 at = (INV_VIEW_MATRIX * vec4(view.xyz / view.w, 1.0)).xz;
            float seen = texture(explored, (at + 0.5) / map_size).r;
            float live = 1.0 - smoothstep(radius - 1.5, radius, distance(at, player));
            ALBEDO = memory * seen;
            ALPHA = (1.0 - live) * mix(1.0, 0.62, seen);
        }
        """;

    private static Vector3 To3(Vector2 p, float y = 0) => new(p.X, y, p.Y);

    public override void _Ready() {
        World.Replaced += () => { worldReplaced = true; ClearPaths(); };
        camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = BaseSize, Near = 1, Far = 200, Current = true };
        AddChild(camera);
        // Half energy puts the sky's light at DayAmbientEnergy.
        skylight = new ProceduralSkyMaterial { SkyEnergyMultiplier = .5f, GroundEnergyMultiplier = .5f };
        environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("879c69"),
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            // Two flat colours need no detail in the sky's light map, and a small one is cheap to rebuild as the day changes.
            Sky = new Sky { SkyMaterial = skylight, RadianceSize = Sky.RadianceSizeEnum.Size64 },
            AmbientLightEnergy = DayAmbientEnergy,
            // Light bounced from what is on screen, such as firelight off the ground onto nearby walls.
            SsilEnabled = true,
            // Contact shade under and between things; it darkens the sun's light too, since the sun is most of the light.
            SsaoEnabled = true,
            SsaoIntensity = 3,
            SsaoLightAffect = .4f,
            // AgX keeps hues as light brightens and adds contrast of its own.
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapAgxContrast = 1.25f,
            TonemapExposure = 1.2f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        // UpdateLighting moves it along the day's arc.
        // One shadow map covers the whole view: split maps leave a seam across this tilted overhead view and their soft edges jump as the camera moves.
        // Shadows this soft need the Ultra filter set in project.godot: lower qualities break their wide edges into a hatched pattern that flickers.
        // The blur has one width everywhere: a penumbra that widens with the caster's height is shaded per pixel, and at low sun the trees' long shadows cost 20 ms a frame at 1440p.
        // The sun lights the scene directly and stays out of the sky, so its turning does not rebuild the sky's light.
        sun = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal, DirectionalShadowMaxDistance = 90, ShadowBlur = 1.5f, LightEnergy = DaySun, SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly };
        AddChild(sun);
        scene = new Node3D(); AddChild(scene);
        explored = Image.CreateFromData(Terrain.Size, Terrain.Size, false, Image.Format.L8, World.S.Explored);
        exploredTexture = ImageTexture.CreateFromImage(explored);
        fog.SetShaderParameter("explored", exploredTexture);
        fog.SetShaderParameter("map_size", Terrain.Size);
        // A screen-sized quad on the camera; its shader places it over the whole view, so it is never culled and casts no shadow.
        camera.AddChild(new MeshInstance3D {
            Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            MaterialOverride = fog,
            Position = new Vector3(0, 0, -2),
            ExtraCullMargin = 16384,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        BuildGround(scene);
        BuildLook();
        BuildEffects(scene);
        PileShape.Warm(World.Data);
        warmStage = new Node3D(); WorldModels.Warm(warmStage); PileShape.Stage(warmStage, World.Data); scene.AddChild(warmStage);
        BuildGhosts();
        for (int i = 0; i < World.Carts; i++) idleCarts.Push(NewCart());
        playerRig = new ActorRig(ActorSkin.Player); scene.AddChild(playerRig.Root); playerShirt = Shirts[0];
        selectRing = Ring(new Color(.95f, .82f, .4f, .85f));
        hoverRing = Ring(new Color(1, .95f, .75f, .55f));
        workerRing = Ring(new Color(1, .8f, .3f, .8f));
        playerRing = Ring(new Color(.95f, .88f, .57f, .6f));
        placeTile = Add(scene, new PlaneMesh { Size = new Vector2(1.4f, 1.4f) }, GhostValid, Vector3.Zero);
        placeTile.Visible = false;
        player.Position = World.Player;
        overlay = new Overlay { Paint = PaintOverlay }; AddChild(overlay);
        World.PlayerActed += PlayerActed;
    }

    private MeshInstance3D Ring(Color color) {
        var ring = Torus(scene, .9f, 1, Vector3.Zero, Flat(color));
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        ring.Visible = false;
        return ring;
    }

    public override void _ExitTree() => World.PlayerActed -= PlayerActed;

    // Made once: passing a plain string converts it to a new StringName on every call.
    private static readonly StringName FogPlayer = "player", FogRadius = "radius", FogMemory = "memory";

    private void UpdateFog() {
        bool clearReview = AnimationReview || filming || EnvironmentReview && !EnvironmentReviewFog;
        fog.SetShaderParameter(FogPlayer, World.Player);
        fog.SetShaderParameter(FogRadius, clearReview ? 1e6f : World.Data.SightRadius);
        fog.SetShaderParameter(FogMemory, new Color(.38f, .4f, .42f).Lerp(new Color(.07f, .08f, .11f), night));
        if (exploredRevision == World.ExploredRevision) return;
        exploredRevision = World.ExploredRevision;
        explored.SetData(Terrain.Size, Terrain.Size, false, Image.Format.L8, World.S.Explored);
        exploredTexture.Update(explored);
    }

    private void PlayerActed(ActorAction action, Vector2 target) => Acted(World.S.Actor.Id, action, target);

    /// <summary>Plays a swing, pump or kindling on the player who made it, here or at another machine.</summary>
    public void Acted(int id, ActorAction action, Vector2 target) {
        if (id == World.Me.Id) { pulseAction = action; pulseTarget = target; pulseRemaining = ActorAnimation.Duration(action); player.Time = 0; } else if (others.TryGetValue(id, out var other)) { other.Pulse = action; other.PulseTarget = target; other.PulseRemaining = ActorAnimation.Duration(action); other.State.Time = 0; }
    }

    private sealed class Avatar(ActorRig rig, string shirt) {
        public readonly ActorRig Rig = rig;
        public readonly string Shirt = shirt;
        public readonly ActorState State = new();
        public ActorAction Pulse;
        public Vector2 PulseTarget;
        public float PulseRemaining;
        /// <summary>Seconds since a wolf knocked this player down, zero while they stand.</summary>
        public float DownedFor;
    }

    public bool ShowsPlayer(int id) => others.ContainsKey(id);

    private static string ShirtOf(PlayerState p) => Shirts[p.Shirt % Shirts.Length];

    public ActorAction PlayerAnimation => player.Action;
    public float PlayerAnimationTime => player.Time;
    public ActorAction OtherAnimation(int id) => others.TryGetValue(id, out var other) ? other.State.Action : ActorAction.Idle;
    public ActorAction WorkerAnimation(int id) => crew.TryGetValue(id, out var entry) ? entry.State.Action : ActorAction.Idle;

    private sealed class ActorState {
        public Vector2 Position;
        public float Yaw;
        /// <summary>
        /// How the body is turning: the head turns toward a new heading first and the body follows with weight, easing into
        /// and out of the turn and leaning into it.
        /// </summary>
        public Turning Turning;
        /// <summary>The doorway stage drawn last frame, and where the actor stood when it began.</summary>
        public Doorway Stage;
        public Vector2 StageFrom;
        /// <summary>Seconds left stepping back down off a cabin's step after banging on its door.</summary>
        public float SteppingDown;
        public ActorAction Action;
        public float Time;
        public float PreviousTime = -1;
        public bool Moving;
        // The action blended out of after a change, where it was, and how far the blend into the new one has gone.
        public ActorAction From;
        public float FromTime;
        public bool FromMoving;
        public float Blend = 1;
        /// <summary>Moves and poses the actor for a frame; it faces where it moves, or <paramref name="facingTarget"/> while standing or <paramref name="backing"/> away from it.</summary>
        public void Update(Vector2 position, ActorAction action, Vector2 facingTarget, float dt, bool backing = false) {
            float moved = position.DistanceTo(Position);
            bool moving = moved > .001f;
            float yaw = ActorAnimation.Yaw(moving && !backing ? position - Position : facingTarget - position, Yaw);
            // A jump such as a respawn is no speed to lean into.
            Turning.Step(ref Yaw, yaw, dt > 0 ? Math.Min(moved / dt, 8) : 0, dt, action == ActorAction.Run);
            Position = position;
            if (Action != action) {
                From = Action; FromTime = Time; FromMoving = Moving; Blend = 0;
                Action = action; Time = 0; PreviousTime = -1;
            } else { PreviousTime = Time; Time += ActorAnimation.Advance(action, dt, moved); }
            FromTime += ActorAnimation.Advance(From, dt, moved);
            Blend = Math.Min(1, Blend + dt / ActorAnimation.BlendSeconds);
            Moving = moving;
        }
        public Pose Pose => ActorAnimation.Blended(From, FromTime, FromMoving, Action, Time, Moving, Blend);
        /// <summary>True on the frame an action loop passes its impact moment.</summary>
        public bool Struck(float at) {
            if (PreviousTime < 0) return false;
            float before = ActorAnimation.Phase(Action, PreviousTime), now = ActorAnimation.Phase(Action, Time);
            return before <= now ? before < at && at <= now : at > before || at <= now;
        }
    }

    // ---------- Camera and picking ----------

    public Vector2 Center => GetViewport().GetVisibleRect().Size * new Vector2(PanelOpen ? .38f : .5f, .52f);

    /// <summary>Keeps the camera on the player; called before any projection so input sees the current frame.</summary>
    private void SyncCamera() {
        var size = GetViewport().GetVisibleRect().Size;
        camera.Size = (AnimationReview ? ReviewWolfClip >= 0 ? 3.2f : ReviewClip >= 0 ? 2.6f : 11 : BaseSize) / Zoom;
        float pitch = Mathf.DegToRad(AnimationReview ? ReviewPitch : CameraPitch ?? Pitch), across = MathF.Cos(pitch) * Mathf.Sqrt2 / 2;
        var back = new Vector3(across, MathF.Sin(pitch), across).Rotated(Vector3.Up, Mathf.DegToRad(CameraTurn));
        var target = AnimationReview ? ReviewCenter + To3(reviewActor.Position) : To3(EnvironmentReview ? EnvironmentCenter ?? World.Player : World.Player);
        camera.Basis = Basis.LookingAt(-back, Vector3.Up);
        camera.Position = target + back * (EnvironmentReview ? EnvironmentCameraDistance : 60);
        if (EnvironmentReview && EnvironmentCameraDistance > 60) sun.DirectionalShadowMaxDistance = EnvironmentCameraDistance + 40;
        float unitsPerPixel = camera.Size / Math.Max(1, size.Y);
        camera.HOffset = (.5f - Center.X / Math.Max(1, size.X)) * size.X * unitsPerPixel;
        camera.VOffset = (Center.Y / Math.Max(1, size.Y) - .5f) * size.Y * unitsPerPixel;
    }

    /// <summary>Whether the models drawn while loading are still in the scene.</summary>
    public bool Warming => warmStage != null;

    private void ShowWarmStage() {
        if (warmStage == null) return;
        warmStage.Position = camera.Position - camera.Basis.Z * WarmStageDepth;
        if (--warmFrames > 0) return;
        warmStage.QueueFree(); warmStage = null;
    }

    private float PixelsPerUnit => GetViewport().GetVisibleRect().Size.Y / camera.Size;
    public Vector2 Screen(Vector2 p) => Screen(p, 0);
    public Vector2 Screen(Vector2 p, float height) { SyncCamera(); return camera.UnprojectPosition(To3(p, height)); }

    public Vector2 ToWorld(Vector2 screen) {
        SyncCamera();
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        if (Math.Abs(direction.Y) < 1e-5f) return World.Player;
        var hit = origin + direction * (-origin.Y / direction.Y);
        return new Vector2(hit.X, hit.Z);
    }

    private Hit[] HitsOf(Thing t) => props.TryGetValue(t.Id, out var prop) && prop.Kind == t.Kind ? prop.Hits : WorldModels.Hits(t);

    private static bool RayCylinder(Vector3 origin, Vector3 direction, Vector2 center, Hit hit, out float distance) {
        distance = float.MaxValue;
        float r2 = hit.Radius * hit.Radius;
        if (Math.Abs(direction.Y) > 1e-5f) {
            float cap = (hit.Top - origin.Y) / direction.Y;
            var p = origin + direction * cap;
            if (cap > 0 && (p.X - center.X) * (p.X - center.X) + (p.Z - center.Y) * (p.Z - center.Y) <= r2) distance = cap;
        }
        float ox = origin.X - center.X, oz = origin.Z - center.Y;
        float a = direction.X * direction.X + direction.Z * direction.Z, b = 2 * (ox * direction.X + oz * direction.Z), c = ox * ox + oz * oz - r2;
        float disc = b * b - 4 * a * c;
        if (a > 1e-8f && disc >= 0) {
            float side = (-b - MathF.Sqrt(disc)) / (2 * a), y = origin.Y + direction.Y * side;
            if (side > 0 && y >= hit.Bottom && y <= hit.Top && side < distance) distance = side;
        }
        return distance < float.MaxValue;
    }

    private static bool RayHits(Vector3 origin, Vector3 direction, Vector2 at, ReadOnlySpan<Hit> hits, out float distance) {
        distance = float.MaxValue;
        foreach (var hit in hits)
            if (RayCylinder(origin, direction, at, hit, out float d)) distance = Math.Min(distance, d);
        return distance < float.MaxValue;
    }

    private readonly List<Thing> picking = new();
    private const float StumpHeight = 1.3f;
    /// <summary>The visible object nearest the camera under the cursor, tested against the models' hit cylinders.</summary>
    public Thing? Pick(Vector2 screen) {
        SyncCamera();
        // One ray for the whole pick: asking the camera again for every thing is most of what a pick costs.
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        Thing? best = null; float nearest = float.MaxValue;
        // Hits follow what is drawn, including memories, but only things that still exist can be picked.
        World.CollectKnown(picking);
        foreach (var drawn in picking) {
            if (drawn.Remaining <= 0 && drawn.IsResource && drawn.Kind != ThingKind.Berries || World.Find(drawn.Id) is not Thing t) continue;
            var hits = HitsOf(drawn);
            // Only the foot of a tree's trunk, its first hit, picks the tree, so a click among the crowns of a forest reaches the ground and swings.
            if (t.Kind == ThingKind.Tree) {
                if (hits.Length > 0 && RayHits(origin, direction, t.Pos, [hits[0] with { Top = Math.Min(hits[0].Top, StumpHeight) }], out float s) && s < nearest) { nearest = s; best = t; }
                continue;
            }
            if (RayHits(origin, direction, t.Pos, hits, out float d) && d < nearest) { nearest = d; best = t; }
        }
        if (best != null) return best;
        var point = ToWorld(screen);
        if (Terrain.WaterRing(point) && World.Sees(point))
            foreach (var thing in World.ThingsOf(ThingKind.Water)) return thing;
        return null;
    }

    private static readonly Hit[] PersonHit = { new(.38f, 0, 1.75f) };
    public Worker? PickWorker(Vector2 screen) {
        SyncCamera();
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        Worker? best = null; float nearest = float.MaxValue;
        foreach (var w in World.Workers)
            if (World.Sees(w) && RayHits(origin, direction, w.Pos, PersonHit, out float d) && d < nearest) { nearest = d; best = w; }
        return best;
    }

    // The camera is orthogonal, so where a point shows on screen is a straight-line function of where it is in the world.
    // Read once a frame, it places every tree, building and head on screen without asking the camera for each.
    private Vector2 screenOrigin, screenX, screenZ, screenUp;
    private void ReadScreenAxes() {
        screenOrigin = camera.UnprojectPosition(Vector3.Zero);
        screenX = camera.UnprojectPosition(Vector3.Right) - screenOrigin;
        screenZ = camera.UnprojectPosition(Vector3.Back) - screenOrigin;
        screenUp = camera.UnprojectPosition(Vector3.Up) - screenOrigin;
    }
    /// <summary>Where a world position shows on screen, by the axes last read.</summary>
    private Vector2 OnScreen(Vector2 at, float height = 0) => screenOrigin + screenX * at.X + screenZ * at.Y + screenUp * height;

    /// <summary>The screen rectangle of one hit cylinder, used to decide when a canopy hides something.</summary>
    private Rect2 ScreenRect(Vector2 at, Hit hit, float ppu) {
        var bottom = OnScreen(at, hit.Bottom); var top = OnScreen(at, hit.Top);
        float half = hit.Radius * ppu;
        return new Rect2(bottom.X - half, top.Y - half * .5f, half * 2, bottom.Y - top.Y + half);
    }

    internal Prop? PropOf(int id) => props.GetValueOrDefault(id);
    /// <summary>How many models of things that left the world are still playing their way out.</summary>
    internal int Departing => leaving.Count;

    internal bool TreeCanopyCoversThing(Thing tree, Thing thing) {
        SyncCamera(); ReadScreenAxes();
        return Covers(tree, thing, PixelsPerUnit);
    }

    private bool Covers(Thing tree, Thing thing, float ppu) {
        // Depth decides which object is in front; projected volumes must then actually overlap.
        if (tree.X + tree.Y <= thing.X + thing.Y) return false;
        var thingHits = HitsOf(thing);
        Span<Rect2> thingRects = stackalloc Rect2[thingHits.Length];
        for (int i = 0; i < thingHits.Length; i++) thingRects[i] = ScreenRect(thing.Pos, thingHits[i], ppu);
        foreach (var hit in HitsOf(tree)) {
            var rect = ScreenRect(tree.Pos, hit, ppu);
            foreach (var other in thingRects) if (rect.Intersects(other)) return true;
        }
        return false;
    }

    private bool TreeCoversPerson(Thing tree, Vector2 person, float ppu) {
        if (tree.X + tree.Y <= person.X + person.Y) return false;
        var head = OnScreen(person, 1.2f);
        foreach (var hit in HitsOf(tree)) if (ScreenRect(tree.Pos, hit, ppu).HasPoint(head)) return true;
        return false;
    }

    // ---------- Frame update ----------

    public override void _Process(double delta) {
        // The film moves everything by its own clock.
        if (World == null || filming) return;
        float dt = Paused ? 0 : CaptureStep ?? Math.Min((float)delta, .1f);
        Elapsed += dt;
        SyncCamera();
        ShowWarmStage();
        // The review camera follows its actor to where it moved this frame.
        if (AnimationReview) { UpdateReview(dt); SyncCamera(); UpdateFog(); overlay.QueueRedraw(); return; }
        // A menu holds everyone still, but what a replaced world had goes at once, or the start menu would show it.
        bool moved = dt > 0 || worldReplaced;
        if (moved || EnvironmentReview) UpdateActors(dt);
        SyncThings(dt);
        if (moved) { SyncWolves(dt); SyncCarts(dt); }
        worldReplaced = false;
        UpdateEffects(dt);
        UpdateMarkers();
        UpdateLighting();
        UpdateLook(dt);
        UpdateFog();
        overlay.QueueRedraw();
    }

    /// <summary>The hammer model for a hammering action: the grade in hand, carried or slotted in the worked bench, and steel for someone with none, such as an anvil worker.</summary>
    private static string? Grade(string? tool, string? held, Inventory? carried) =>
        tool != "hammer" ? tool
        : held is "wood_hammer" or "steel_hammer" ? held
        : carried != null && carried["wood_hammer"] > 0 && carried["steel_hammer"] == 0 ? "wood_hammer" : "steel_hammer";

    private static string? HeldTool(string? selected, Inventory backpack) => selected switch {
        "steel_axe" or "steel_pick" when backpack[selected] > 0 => selected,
        "wood_hammer" or "steel_hammer" when backpack[selected] > 0 => selected,
        _ => null
    };

    private void UpdateActors(float dt) {
        pumping.Clear(); grinding.Clear();
        if (ShirtOf(World.Me) != playerShirt) {
            playerShirt = ShirtOf(World.Me); playerRig.Root.QueueFree();
            playerRig = new ActorRig(ActorSkin.Player, playerShirt); scene.AddChild(playerRig.Root);
        }
        bool moving = World.Player.DistanceSquaredTo(player.Position) > .000001f;
        ActorAction action = ActorAnimation.PlayerAction(GatheringTarget?.Kind, Gathering, moving, World.S.Backpack, World.Me.Sprinting, World.CraftRate > 0, Fitting(GatheringTarget));
        Vector2 target = Gathering && GatheringTarget != null ? World.GatherAt(GatheringTarget, World.Player) : World.Player;
        Thing? subject = Gathering ? GatheringTarget : null;
        if (pulseRemaining > 0 && (!moving || ActorAnimation.SwingsOnTheMove(pulseAction))) { action = pulseAction; target = pulseTarget; subject = ThingAt(pulseTarget); }
        if (action == ActorAction.Idle && StationToHammer(World.Me) is Thing station) { action = ActorAnimation.StationAction(station, World.Data); target = station.Pos; subject = station; }
        if (World.Me.Braking && moving) action = ActorAction.Brake;
        // A leaping wolf knocks the player over as its jaws reach them, before it lands; a pinned player stays down.
        var knocker = Knocking(World.Me);
        bool fallsBack = FallsBack(World.Me.Id);
        if (World.Mauled || knocker != null) {
            mauledFrom = (knocker ?? PinningWolf(World.Me.Id))?.Pos ?? (downedFor == 0 ? World.Player + Vector2.Down : mauledFrom);
            // Facing the wolf, the player goes over backwards with it on top; one yanked out of a doorway falls back still facing the door.
            action = World.Dead ? ActorAction.Devoured : fallsBack && downedFor < ActorAnimation.FallBackSeconds ? ActorAction.FallBack : ActorAction.Mauled;
            target = fallsBack ? World.Player - World.OutOfDoor : mauledFrom; subject = null;
            downedFor += dt;
        } else downedFor = 0;
        var at = World.Player;
        var door = AtDoor(World.Me, player, dt);
        if (door is var (doorAt, doorFacing, doorAction, _, _, _, _)) { at = doorAt; target = doorFacing; action = doorAction; subject = null; }
        player.Update(at, action, target, dt, door?.Backing ?? false);
        // A foot lands at each extreme of the stride.
        if (action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run && (player.Struck(.25f) || player.Struck(.75f))) Sounds?.Play(FootstepSound(World.Player));
        pulseRemaining = Math.Max(0, pulseRemaining - dt);
        string? tool = ActorAnimation.Tool(action) ?? (action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run ? HeldTool(SelectedTool, World.S.Backpack) : null);
        bool torch = World.Torch > 0 && !World.Dead;
        Pose(playerRig, player, Grade(tool, SelectedTool, World.S.Backpack), torch, door?.Door, door?.Twist ?? 0, door?.Grip ?? 0);
        Torch(playerRig, player, World.TorchLit, World.TorchFlare, null);
        if (downedFor > 0 && fallsBack) FallBack(playerRig, player, downedFor, torch);
        else if (downedFor > 0) KnockDown(playerRig, player, downedFor);
        playerRig.Root.Visible = (!World.Dead || World.Mauled) && World.Sheltered == 0;
        Work(player, subject, playerRig);
        UpdateOthers(dt);
        foreach (var worker in World.Workers) {
            if (!crew.TryGetValue(worker.Id, out var entry)) {
                var rig = new ActorRig(ActorSkin.Worker);
                // Gathered and warmed now, so the first walk home at dusk makes no faded materials.
                entry = (rig, new ActorState { Position = worker.Pos }, FadeParts(rig.Root));
                scene.AddChild(entry.Rig.Root); crew.Add(worker.Id, entry);
            }
            var state = entry.State;
            bool walking = worker.Pos.DistanceSquaredTo(state.Position) > .000001f;
            var assignment = World.Find(worker.Assignment);
            var work = ActorAnimation.WorkerAction(worker, assignment, walking, World.Data);
            if (work is ActorAction.Hammer or ActorAction.Pack or ActorAction.Grind && assignment?.Processing != true && assignment?.Kind != ThingKind.Mine) work = ActorAction.Idle;
            // At a mine the worker faces the rock past the entrance, not the cart out front.
            // A protester turns the sign toward the camera.
            var facing = work == ActorAction.Protest ? worker.Pos + Vector2.One
                : work is ActorAction.Sit or ActorAction.Perch && World.LunchFire is Thing fire ? fire.Pos : work == ActorAction.Pack && assignment?.Kind == ThingKind.Packer ? worker.Pos + Vector2.Down
                : assignment is { Kind: ThingKind.Mine } mine ? World.MineDoor(mine) * 2 - mine.Pos : assignment?.Pos ?? worker.Pos;
            state.Update(worker.Pos, work, facing, dt);
            Pose(entry.Rig, state, Grade(ActorAnimation.Tool(work), worker.Tool, assignment?.Tools));
            entry.Rig.Root.Visible = World.Sees(worker);
            Fade(entry.Parts, entry.Rig.Root, TripFade(World.TripLeg(worker)));
            Work(state, work is ActorAction.Idle or ActorAction.Walk or ActorAction.Carry or ActorAction.Sit or ActorAction.Perch or ActorAction.Protest ? null : assignment, entry.Rig);
        }
        // Removing while walking a dictionary is allowed, so no copy of its keys is needed.
        foreach (var (id, entry) in crew) {
            if (HasWorker(id)) continue;
            fades.Remove(entry.Rig.Root); entry.Rig.Root.QueueFree(); crew.Remove(id);
        }
    }

    /// <summary>
    /// A worked station near a player that is processing with nobody of the crew working it, for an idle player to work at.
    /// A player holding on to a gather target is busy with that, even with a pose that shows no gathering.
    /// </summary>
    private Thing? StationToHammer(PlayerState p) {
        if (p.GatherTarget != 0) return null;
        using var acting = World.ActAs(p);
        foreach (var kind in World.WorkedKinds)
            foreach (var t in World.ThingsOf(kind)) if (World.Worked(t) && t.Processing && World.AtStation(t) && !World.CrewHammers(t)) return t;
        return null;
    }

    /// <summary>The flame of a torch in hand, fanned by a swing, and the sparks of each strike while it is being lit.</summary>
    private void Torch(ActorRig rig, ActorState state, bool lit, float flare, Vector2? at) {
        rig.Flame.Visible = lit;
        if (lit) {
            // A slow flicker: a fast one makes the shadows the torch casts jitter.
            float slow = Elapsed / 3, flicker = MathF.Sin(slow * 13) * .5f + MathF.Sin(slow * 29 + 1) * .5f; flare /= .5f;
            rig.Flame.Scale = new Vector3(1 + flicker * .1f, .85f + MathF.Abs(MathF.Sin(slow * 11)) * .3f, 1 - flicker * .1f) * (1 + flare * .25f);
            // A swing fans the flame: the lit circle grows by a fifth and settles back.
            rig.TorchLight.OmniRange = 5 * (1 + .2f * flare);
            rig.TorchLight.LightEnergy = (1 + flicker * .25f + flare * .3f) * (.3f + night * 1.3f);
        }
        // Each strike of steel on flint throws a few sparks onto the torch head.
        if (state.Action == ActorAction.Kindle && (state.Struck(.1875f) || state.Struck(.4375f) || state.Struck(.6875f) || state.Struck(.9375f))) {
            Burst(rig.Flame.GlobalPosition, Sparks, 7, 2.2f, .3f, 6, .022f);
            Sounds?.Play("anvil_work", at);
        }
    }

    /// <summary>
    /// The other players, posed from what their machines report:
    /// walking, the resource they work on, the station they stand at, the tool in hand and their swings.
    /// </summary>
    private void UpdateOthers(float dt) {
        var me = World.Me;
        foreach (var p in World.Players) {
            if (p == me) continue;
            if (!others.TryGetValue(p.Id, out var other) || other.Shirt != ShirtOf(p)) {
                other?.Rig.Root.QueueFree();
                other = others[p.Id] = new Avatar(new ActorRig(ActorSkin.Player, ShirtOf(p)), ShirtOf(p));
                other.State.Position = p.Pos; scene.AddChild(other.Rig.Root);
            }
            var state = other.State;
            bool moving = p.Pos.DistanceSquaredTo(state.Position) > .000001f;
            var target = World.Find(p.GatherTarget);
            bool gathering = target != null && p.Pos.DistanceTo(World.GatherAt(target, p.Pos)) < 2.5f;
            var action = ActorAnimation.PlayerAction(target?.Kind, gathering, moving, p.Backpack, p.Sprinting, p.Making != "" || p.Crafting.Count > 0, Fitting(target));
            var subject = gathering ? target : null;
            var facing = subject != null ? World.GatherAt(subject, p.Pos) : p.Pos;
            if (other.PulseRemaining > 0 && (!moving || ActorAnimation.SwingsOnTheMove(other.Pulse))) { action = other.Pulse; facing = other.PulseTarget; subject = ThingAt(other.PulseTarget); }
            if (action == ActorAction.Idle && StationToHammer(p) is Thing station) { action = ActorAnimation.StationAction(station, World.Data); facing = station.Pos; subject = station; }
            if (p.Braking && moving) action = ActorAction.Brake;
            var knocker = Knocking(p);
            bool fallsBack = FallsBack(p.Id);
            if (p.Mauled || knocker != null) {
                action = p.RespawnIn > 0 ? ActorAction.Devoured : fallsBack && other.DownedFor < ActorAnimation.FallBackSeconds ? ActorAction.FallBack : ActorAction.Mauled;
                facing = fallsBack ? p.Pos - World.OutOfDoor : (knocker ?? PinningWolf(p.Id))?.Pos ?? p.Pos + Vector2.Down;
                other.DownedFor += dt;
            } else other.DownedFor = 0;
            var at = p.Pos;
            var door = AtDoor(p, state, dt);
            if (door is var (doorAt, doorFacing, doorAction, _, _, _, _)) { at = doorAt; facing = doorFacing; action = doorAction; subject = null; }
            state.Update(at, action, facing, dt, door?.Backing ?? false);
            other.PulseRemaining = Math.Max(0, other.PulseRemaining - dt);
            if (action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run && (state.Struck(.25f) || state.Struck(.75f))) Sounds?.Play(FootstepSound(p.Pos), p.Pos);
            Pose(other.Rig, state, Grade(ActorAnimation.Tool(action) ?? (action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run ? HeldTool(p.Selected, p.Backpack) : null), p.Selected, p.Backpack), p.Torch > 0 && p.RespawnIn <= 0, door?.Door, door?.Twist ?? 0, door?.Grip ?? 0);
            Torch(other.Rig, state, p.TorchLit, p.TorchFlare, p.Pos);
            if (other.DownedFor > 0 && fallsBack) FallBack(other.Rig, state, other.DownedFor, p.Torch > 0 && p.RespawnIn <= 0);
            else if (other.DownedFor > 0) KnockDown(other.Rig, state, other.DownedFor);
            other.Rig.Root.Visible = (p.RespawnIn <= 0 || p.Mauled) && p.Sheltered == 0 && World.Sees(p.Pos);
            Work(state, subject, other.Rig);
        }
        foreach (var (id, other) in others)
            if (!HasPlayer(id)) { other.Rig.Root.QueueFree(); others.Remove(id); }
    }

    /// <summary>How far in from a cabin's door point a player stepping in walks, out of sight behind the walls.</summary>
    private static readonly Vector2 IntoCabin = new(0, -1.8f);
    /// <summary>Where a player banging on a cabin's door stands, from its door point: on the lower tread of the step, close enough to reach the boards.</summary>
    private static readonly Vector2 KnockSpot = new(0, -.5f);
    /// <summary>Seconds a player left outside a shut door takes to step up to it, ending as the first bang lands, and to step back down after banging.</summary>
    private const float StepUpSeconds = .3f;
    /// <summary>How far behind a player's hips a tugging wolf's jaws hold them.</summary>
    private const float WaistBehind = .12f;

    /// <summary>
    /// Where the treads of a cabin's step and then its floor begin, in from the door point, each this much higher than the
    /// last, as <see cref="WorldModels"/> builds the cabin.
    /// </summary>
    private static ReadOnlySpan<float> StepEdges => new[] { .4f, .62f, .8f };
    private const float StepRise = .14f;

    /// <summary>
    /// A player at a cabin's door as their doorway stage shows them: where they stand, what they face, what they do, the
    /// door point whose steps they stand on, whether they move backward, how far their shoulders turn and how far their right
    /// hand has gone to the door frame, 0 to 1; null when they are at no door.
    /// </summary>
    private (Vector2 At, Vector2 Facing, ActorAction Action, Vector2 Door, bool Backing, float Twist, float Grip)? AtDoor(PlayerState p, ActorState state, float dt) {
        if (state.Stage != p.Door) {
            if (state.Stage == Doorway.Knocking && p.Door == Doorway.None) state.SteppingDown = StepUpSeconds;
            state.Stage = p.Door; state.StageFrom = state.Position;
        }
        if (World.Find(p.DoorCabin) is not Thing cabin) return null;
        var door = World.DoorPoint(cabin);
        if (p.Door == Doorway.None && state.SteppingDown > 0) {
            // Done banging, the player steps back down off the step to where they stand, still facing the door.
            state.SteppingDown = Math.Max(0, state.SteppingDown - dt);
            var down = state.StageFrom.Lerp(p.Pos, Mathf.SmoothStep(0, 1, 1 - state.SteppingDown / StepUpSeconds));
            return (down, door + IntoCabin, ActorAction.Walk, door, true, 0, 0);
        }
        if (p.Door == Doorway.None) return null;
        (Vector2 At, Vector2 Facing, ActorAction Action, bool Backing) shown = p.Door == Doorway.Following ? (Behind(p, state, door, dt), door + IntoCabin, ActorAction.Walk, false)
            : p.Door == Doorway.Aside ? StandAside(state.StageFrom, World.AsideSpot(p), door, p.DoorTime)
            : DoorStep(p.Door, door, p.Pos, state.StageFrom, p.DoorTime, World.DoorSeconds(p), p.DoorRush, p.Door == Doorway.Leaving && MakingWay(p));
        float twist = 0, grip = 0;
        // Squeezing in from beside the step, the right hand takes the door frame as they reach it and lets go once through.
        if (p.Door == Doorway.Entering && Math.Abs((state.StageFrom - door).Dot(World.AsideWay)) > SideLane) {
            float depth = (shown.At - door).Dot(IntoCabin.Normalized());
            grip = Mathf.SmoothStep(GripFrom, GripFrom + .15f, depth) * (1 - Mathf.SmoothStep(GripTo, GripTo + .15f, depth));
        }
        if (p.Door is Doorway.Entering or Doorway.Leaving && SqueezePast(p, shown.At) is var (squeeze, way) && squeeze > 0) {
            // Turned sideways to squeeze past someone coming the other way through the door, chest toward them, still stepping on.
            var ahead = (shown.At - state.Position).LengthSquared() > 1e-8f ? (shown.At - state.Position).Normalized() : (shown.Facing - shown.At).Normalized();
            shown.Facing = shown.At + ahead.Rotated(-way * Mathf.DegToRad(SqueezeTurn) * squeeze);
            shown.Backing = true; twist = way * SqueezeTwist * squeeze;
        }
        return (shown.At, shown.Facing, shown.Action, door, shown.Backing, twist, grip);
    }

    /// <summary>
    /// Where a player <paramref name="time"/> seconds into a doorway stage of <paramref name="seconds"/> other than following stands, what
    /// they face and do, and whether they move backward: <paramref name="from"/> is where the world has them and <paramref name="start"/>
    /// where they were drawn as the stage began.
    /// </summary>
    private static (Vector2 At, Vector2 Facing, ActorAction Action, bool Backing) DoorStep(Doorway stage, Vector2 door, Vector2 from, Vector2 start, float time, float seconds, bool rush, bool keepLeft = false) {
        var inside = door + IntoCabin;
        float done = Math.Clamp(time / Math.Max(seconds, .01f), 0, 1);
        // Left outside, a player waits for the door to swing shut, then steps up to it and bangs.
        float up = Mathf.SmoothStep(0, 1, Math.Clamp((time - World.KnockLead + StepUpSeconds) / StepUpSeconds, 0, 1));
        return stage switch {
            Doorway.Entering => (Inward(start, door, rush ? done : WalkedIn(time)), inside, rush ? ActorAction.Jog : ActorAction.Walk, false),
            Doorway.Leaving => (Outward(door, from, done, keepLeft), door - IntoCabin, ActorAction.Walk, false),
            Doorway.Knocking => (start.Lerp(door + KnockSpot, up), inside, time >= World.KnockLead ? ActorAction.Knock : up > 0 ? ActorAction.Walk : ActorAction.Idle, false),
            _ => (start.Lerp(from + World.OutOfDoor * World.DragDistance, World.Dragged(done)), inside, ActorAction.Tugged, true),
        };
    }

    /// <summary>A player standing aside at a cabin's door: stepping over to <paramref name="spot"/> from <paramref name="start"/>, then waiting there, facing the door.</summary>
    private static (Vector2 At, Vector2 Facing, ActorAction Action, bool Backing) StandAside(Vector2 start, Vector2 spot, Vector2 door, float time) =>
        (start.Lerp(spot, Mathf.SmoothStep(0, 1, time / AsideSeconds)), door + IntoCabin, time < AsideSeconds ? ActorAction.Walk : ActorAction.Idle, true);
    /// <summary>Seconds a player takes to step aside at a cabin's door.</summary>
    private const float AsideSeconds = .5f;

    /// <summary>
    /// Where a player stepping into a cabin from <paramref name="start"/> is, <paramref name="done"/> of the way: straight in,
    /// or from beside the step, back in front of it and up it along its side, so they pass whoever comes down the middle and
    /// never walk into the step's side or the door frame.
    /// </summary>
    private static Vector2 Inward(Vector2 start, Vector2 door, float done) {
        var inside = door + IntoCabin;
        float across = (start - door).Dot(World.AsideWay);
        if (Math.Abs(across) <= SideLane) return start.Lerp(inside, done);
        var side = World.AsideWay * (Math.Sign(across) * SideLane);
        var front = door + side + IntoCabin.Normalized() * .25f;
        var tread = door + side + IntoCabin.Normalized() * .62f;
        float first = start.DistanceTo(front), second = front.DistanceTo(tread), last = tread.DistanceTo(inside), along = done * (first + second + last);
        return along < first ? start.Lerp(front, along / first)
            : along < first + second ? front.Lerp(tread, (along - first) / second)
            : tread.Lerp(inside, Math.Min(1, (along - first - second) / last));
    }
    /// <summary>How far to the side of the door point a player stepping in past someone walks up the step, which is a metre wide, keeping their boots on it.</summary>
    private const float SideLane = .35f;
    /// <summary>
    /// Where a hand flat on the right door jamb's inner face is from a cabin's door point, to the side, in and up, as the
    /// cabin model builds the jamb, and how far in a player squeezing in reaches for it while it is still ahead of them and lets go as their shoulder passes it.
    /// </summary>
    private const float JambOut = .38f, JambIn = .97f, JambUp = 1.4f, GripFrom = .15f, GripTo = .38f;

    /// <summary>
    /// Where a player stepping out of a cabin to <paramref name="end"/> is, <paramref name="done"/> of the way: straight out,
    /// or down the side of the step they end at to make way for someone stepping in.
    /// </summary>
    private static Vector2 Outward(Vector2 door, Vector2 end, float done, bool keepLeft) {
        var inside = door + IntoCabin;
        if (!keepLeft) return inside.Lerp(end, done);
        var side = end + IntoCabin.Normalized() * .62f;
        float first = inside.DistanceTo(side), along = done * (first + side.DistanceTo(end));
        return along < first ? inside.Lerp(side, along / first) : side.Lerp(end, (along - first) / side.DistanceTo(end));
    }

    /// <summary>Whether someone stands aside at, or steps into, the cabin a player steps out of, so the player keeps to one side.</summary>
    private bool MakingWay(PlayerState leaving) {
        foreach (var o in World.Players)
            if (o != leaving && o.DoorCabin == leaving.DoorCabin && o.Door is Doorway.Aside or Doorway.Entering) return true;
        return false;
    }

    /// <summary>
    /// How much a player stepping into or out of a cabin squeezes past someone going the other way through its door, 0 to 1,
    /// by how near they are, and which way they turn their chest to face them: positive to their left.
    /// </summary>
    private (float Squeeze, float Way) SqueezePast(PlayerState passing, Vector2 at) {
        var other = passing.Door == Doorway.Entering ? Doorway.Leaving : Doorway.Entering;
        foreach (var p in World.Players)
            if (p != passing && p.DoorCabin == passing.DoorCabin && p.Door == other) {
                var them = (p.Id == World.Me.Id ? player : others.TryGetValue(p.Id, out var avatar) ? avatar.State : player).Position;
                // Facing into the cabin the way aside is to the right, so the chest turns left toward someone on the left; facing out, the other way.
                float side = (at - them).Dot(World.AsideWay) >= 0 ? 1 : -1;
                return (Mathf.SmoothStep(SqueezeFrom, SqueezeAt, at.DistanceTo(them)), passing.Door == Doorway.Entering ? side : -side);
            }
        return (0, 0);
    }
    /// <summary>How near, in metres, two players passing in a cabin's door are when they start turning sideways, and are fully turned.</summary>
    private const float SqueezeFrom = 1.6f, SqueezeAt = .9f;
    /// <summary>How far a player squeezing past someone turns their body and, further, their shoulders, in degrees.</summary>
    private const float SqueezeTurn = 30, SqueezeTwist = 45;
    /// <summary>The arms of a player squeezing past someone: hanging straight down at the body's sides.</summary>
    private const float TuckRaise = 0, TuckOut = 3, TuckElbow = 8;

    /// <summary>
    /// How far through a walk into a cabin a player is <paramref name="time"/> seconds in, 0 to 1: at the pace of a 1.5 s
    /// walk until they are up the first tread, then speeding up over <see cref="HurrySeconds"/> to hurry the rest in the
    /// time <see cref="World.EnterSeconds"/> leaves.
    /// </summary>
    private static float WalkedIn(float time) {
        float fast = (1 - StepInPace * (HurryFrom + HurrySeconds / 2)) / (World.EnterSeconds - HurryFrom - HurrySeconds / 2);
        float ramp = Math.Clamp(time - HurryFrom, 0, HurrySeconds);
        float walked = StepInPace * Math.Min(time, HurryFrom) + StepInPace * ramp + (fast - StepInPace) * ramp * ramp / (2 * HurrySeconds);
        return Math.Min(1, walked + fast * Math.Max(0, time - HurryFrom - HurrySeconds));
    }
    private const float StepInPace = 1 / 1.5f, HurryFrom = .45f, HurrySeconds = .15f;

    /// <summary>
    /// Where a player following someone into a cabin stands, stepping there from where they were drawn: up toward the door no
    /// closer than a stride behind the one ahead, or, while a wolf drags that one back out, aside from the doorway and out of their way.
    /// </summary>
    private Vector2 Behind(PlayerState follower, ActorState state, Vector2 door, float dt) {
        var spot = door;
        foreach (var p in World.Players)
            if (p != follower && p.DoorCabin == follower.DoorCabin && p.Door is Doorway.Entering or Doorway.Pulled) {
                if (p.Door == Doorway.Pulled) {
                    float side = (state.Position - door).Dot(World.AsideWay) < 0 ? -1 : 1;
                    spot = door + World.AsideWay * (side * World.AsideStep);
                    break;
                }
                var ahead = (p.Id == World.Me.Id ? player : others.TryGetValue(p.Id, out var other) ? other.State : player).Position;
                spot = door + (door - ahead).Normalized() * Math.Max(0, .7f - ahead.DistanceTo(door));
            }
        return state.Position.MoveToward(spot, dt * FollowSpeed);
    }
    /// <summary>Metres a second a player following someone into a cabin steps along.</summary>
    private const float FollowSpeed = 1.4f;

    private bool HasWorker(int id) {
        foreach (var w in World.Workers) if (w.Id == id) return true;
        return false;
    }

    private bool HasPlayer(int id) {
        foreach (var p in World.Players) if (p.Id == id) return true;
        return false;
    }

    private Thing? ThingAt(Vector2 at) {
        foreach (var t in World.Things) if (t.Pos == at) return t;
        return null;
    }

    private string FootstepSound(Vector2 p) {
        if (Terrain.InLake(p, 1)) return "footsteps_water";
        foreach (var c in World.Cliffs) if (c.DistanceTo(p) < Cliff.Depth + 3) return "footsteps_rocks";
        foreach (var t in World.Things) if ((t.Kind == ThingKind.Stone || t.IsDeposit) && t.Remaining > 0 && t.Pos.DistanceTo(p) < 1.3f) return "footsteps_rocks";
        return "footsteps_default";
    }

    /// <summary>How long a knocked-down player takes to go over onto their back, and how far their feet slide toward the wolf as they go.</summary>
    public const float KnockSeconds = .3f, KnockSlide = .65f;

    /// <summary>Tips a knocked-down player back about the feet, then slides the body forward so it lies centred on their tile, under the wolf.</summary>
    private static void KnockDown(ActorRig rig, ActorState state, float downedFor) {
        float tip = Ease(Math.Min(1, downedFor / KnockSeconds));
        var forward = new Vector3(MathF.Sin(state.Yaw), 0, MathF.Cos(state.Yaw));
        rig.Root.Position = To3(state.Position) + forward * KnockSlide * tip + Vector3.Up * .17f * tip;
        rig.Root.Rotation = new Vector3(-Mathf.Pi / 2 * tip, state.Yaw, 0);
    }

    /// <summary>Poses an actor's rig; one at the steps of the cabin door at <paramref name="door"/> stands on them.</summary>
    /// <summary>
    /// Poses a player yanked off their feet backward as <see cref="ActorAnimation.FallingBack"/> has them, onto the back
    /// where <see cref="KnockDown"/> lays a player, so <see cref="Throat"/> finds their throat.
    /// </summary>
    private static void FallBack(ActorRig rig, ActorState state, float downedFor, bool torch) {
        var (pose, tip, hips) = ActorAnimation.FallingBack(state.Pose, downedFor / ActorAnimation.FallBackSeconds, KnockSlide - ActorAnimation.FallHips);
        rig.Show(pose, null, false, false, torch);
        // The rig turns about its feet, so they go where the tipped hips need them.
        float t = Mathf.DegToRad(tip);
        var forward = new Vector3(MathF.Sin(state.Yaw), 0, MathF.Cos(state.Yaw));
        rig.Root.Position = To3(state.Position) + forward * (hips.X + pose.HipHeight * MathF.Sin(t)) + Vector3.Up * (hips.Y - pose.HipHeight * MathF.Cos(t));
        rig.Root.Rotation = new Vector3(-t, state.Yaw, 0);
    }

    /// <summary>A pose with the right fist on <paramref name="aim"/> in the world, for a rig placed at <paramref name="root"/>.</summary>
    private static Pose Reaching(Pose pose, Vector3 aim, Transform3D root) {
        var hips = root.AffineInverse() * aim - Vector3.Up * pose.HipHeight;
        var trunk = Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Lean), Mathf.DegToRad(pose.Twist), Mathf.DegToRad(pose.Sway))).Inverse() * hips;
        var (raise, outward, elbow) = ActorAnimation.ArmTo(trunk, left: false, ActorRig.Palm);
        return pose with { RaiseR = raise, OutR = outward, ElbowR = elbow };
    }

    private static void Pose(ActorRig rig, ActorState state, string? tool, bool torch = false, Vector2? door = null, float twist = 0, float grip = 0) {
        rig.Root.Position = To3(state.Position);
        // The body leans about its feet into a turn, and the head looks ahead of it.
        rig.Root.Rotation = new Vector3(0, state.Yaw, state.Turning.Bank);
        var pose = state.Pose with { Twist = state.Pose.Twist + twist };
        Vector3? jamb = null;
        if (twist != 0) {
            // Squeezing chest to chest past someone, the arms hang still at the sides instead of swinging out at them.
            float tuck = Math.Abs(twist) / SqueezeTwist;
            pose = pose with {
                RaiseL = Mathf.Lerp(pose.RaiseL, TuckRaise, tuck),
                OutL = Mathf.Lerp(pose.OutL, TuckOut, tuck),
                ElbowL = Mathf.Lerp(pose.ElbowL, TuckElbow, tuck),
                RaiseR = Mathf.Lerp(pose.RaiseR, TuckRaise, tuck),
                OutR = Mathf.Lerp(pose.OutR, TuckOut, tuck),
                ElbowR = Mathf.Lerp(pose.ElbowR, TuckElbow, tuck),
            };
        }
        if (door is Vector2 steps) {
            // The way up the steps, in the body's frame: X toward its left and Y ahead.
            var inward = IntoCabin.Normalized();
            var up = new Vector2(new Vector2(MathF.Cos(state.Yaw), -MathF.Sin(state.Yaw)).Dot(inward), new Vector2(MathF.Sin(state.Yaw), MathF.Cos(state.Yaw)).Dot(inward));
            float depth = (state.Position - steps).Dot(inward), lift = ActorAnimation.StepLift(pose, depth, up, StepEdges, StepRise);
            pose = ActorAnimation.OnSteps(pose, lift, depth, up, StepEdges, StepRise);
            rig.Root.Position += Vector3.Up * lift;
            if (grip > 0) jamb = To3(steps + World.AsideWay * JambOut + inward * JambIn, JambUp);
        }
        // Between actions the torch arm stays raised with the forearm level, which is the angle the flame is built for.
        if (torch && state.Action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run) pose = pose with { RaiseL = 30, OutL = 4, ElbowL = 60 };
        rig.Show(pose, tool, state.Action == ActorAction.Carry, state.Action == ActorAction.Perch, torch);
        if (jamb is Vector3 frame) {
            // The right palm goes to the door frame and back along a straight line from where the walk swings it.
            var swung = rig.Root.Transform * rig.Fist(false);
            rig.Show(Reaching(pose, swung.Lerp(frame, Mathf.SmoothStep(0, 1, grip)), rig.Root.Transform), tool, false, false, torch);
        }
        if (state.Turning.Look != 0) rig.Head.Rotation += new Vector3(0, state.Turning.Look, 0);
    }

    /// <summary>Shows a bench slot's model of the slotted tool's grade: the first child for the lower grade, the second for steel.</summary>
    private static void ShowSlotted(Node3D slot, string? tool) {
        slot.Visible = tool != null;
        if (tool == null) return;
        bool steel = tool.StartsWith("steel");
        slot.GetChild<Node3D>(0).Visible = !steel;
        slot.GetChild<Node3D>(1).Visible = steel;
    }

    /// <summary>The sound of hands closing on a gathered thing: plants rustle and a carcass tears, while stone and water make none.</summary>
    private static string? GrabSound(ThingKind kind) => kind switch {
        ThingKind.Tree or ThingKind.Berries or ThingKind.Leaves or ThingKind.Farm => "rustle",
        ThingKind.WolfCorpse => "butcher_carcass",
        _ => null
    };

    /// <summary>Whether working on a thing hammers in a mine's frame and rails rather than digging.</summary>
    private static bool Fitting(Thing? target) => target is { Kind: ThingKind.Mine, MineStage: MineStage.Supplied };

    /// <summary>Feeds an actor's work into the object it works on: bellows travel, impact shakes, chips and sparks.</summary>
    private void Work(ActorState actor, Thing? subject, ActorRig rig) {
        if (subject == null) return;
        var action = actor.Action;
        // The bellows board follows the lean of the person pressing it.
        if (action == ActorAction.Pump)
            pumping[subject.Id] = Math.Max(pumping.GetValueOrDefault(subject.Id), Math.Clamp((ActorAnimation.PoseAt(action, actor.Time, false).Lean - 16) / 16, 0, 1));
        if (action == ActorAction.Grind) grinding.Add(subject.Id);
        // Hands close on what they gather twice a loop; packing digs into the heap of goods as each loop starts, grinding plays one clip a loop, and a dig throws its soil aside once.
        string? handSound = action switch {
            ActorAction.Gather when actor.Struck(.22f) || actor.Struck(.72f) => GrabSound(subject.Kind),
            ActorAction.Pack when actor.Struck(.04f) => "rustle",
            ActorAction.Grind when actor.Struck(.03f) => "grind_mortar",
            ActorAction.Dig when actor.Struck(ActorAnimation.DigToss) => "dig_toss",
            _ => null
        };
        if (handSound != null) Sounds?.Play(handSound, subject.Pos);
        // The soil a dig throws aside flies off the blade.
        if (handSound == "dig_toss" && World.Sees(actor.Position)) Burst(rig.Root.Transform * rig.ShovelTip, Soil, 5, 1.6f, .5f, 9, .06f);
        float impact = action switch {
            ActorAction.Chop or ActorAction.SteelChop => .52f,
            ActorAction.Mine or ActorAction.SteelMine => .55f,
            ActorAction.Dig => ActorAnimation.DigStrike,
            ActorAction.Hammer => .5f,
            ActorAction.Pump => .35f,
            ActorAction.Gather => .1f,
            _ => -1
        };
        if (impact < 0 || !actor.Struck(impact) || !props.TryGetValue(subject.Id, out var prop)) return;
        // What is out of sight is drawn as it was last seen, so nothing there shakes or throws chips.
        bool inSight = World.Sees(subject.Pos);
        if (inSight) prop.Shake = 1;
        // Fitting a mine's frame knocks wood like a bench, and every other blow of each hammer also clinks on a rail.
        string? sound = action is ActorAction.Mine or ActorAction.SteelMine ? "mine_ore"
            : action == ActorAction.Dig ? "dig_earth"
            : action == ActorAction.Pump ? "bellows_pump"
            : subject.Kind == ThingKind.Anvil ? "anvil_work"
            : subject.Kind is ThingKind.Bench or ThingKind.Mine ? "bench_work"
            : subject.Kind == ThingKind.Tree && action != ActorAction.Gather ? "chop_tree"
            : null;
        if (sound != null) Sounds?.Play(sound, subject.Pos);
        if (action == ActorAction.Hammer && Fitting(subject) && (int)(actor.Time / ActorAnimation.Duration(action)) % 2 == 1) Sounds?.Play("rail_clink", subject.Pos);
        if (!inSight) return;
        var impactAt = World.GatherAt(subject, actor.Position);
        var toward = (actor.Position - impactAt).Normalized();
        var side = To3(impactAt + toward * .35f);
        switch (subject.Kind) {
            case ThingKind.Tree:
                Burst(side + Vector3.Up * 1.1f, Mat("d9b07a"), 7, 2.6f, .55f, 9, .06f);
                Burst(To3(subject.Pos, 3.6f * WorldModels.TreeScale(subject.Id)), Mat("5d8a3c"), 3, .6f, 1.8f, 1.2f, .09f);
                break;
            case ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin or ThingKind.Mine:
                var color = subject.Kind is ThingKind.Stone or ThingKind.Mine ? "9a9d99" : subject.Kind == ThingKind.Iron ? "a35a2e" : subject.Kind == ThingKind.Tin ? "b9bbb4" : "26282b";
                Burst(side + Vector3.Up * .5f, Mat(color), 6, 2.4f, .5f, 9, .07f);
                Burst(side + Vector3.Up * .4f, GatherDust, 3, .5f, .9f, -.3f, .16f, 1.5f);
                if (action == ActorAction.SteelMine) Burst(side + Vector3.Up * .5f, Sparks, 5, 3.2f, .3f, 4, .025f);
                break;
            case ThingKind.Stump or ThingKind.Bush:
                // The blade goes in at the foot of it and throws up soil, and a bush sheds leaves as it is rocked.
                Burst(side + Vector3.Up * .08f, Soil, 6, 2, .5f, 9, .06f);
                Burst(side + Vector3.Up * .1f, GatherDust, 2, .4f, .8f, -.3f, .14f, 1.5f);
                if (subject.Kind == ThingKind.Bush) Burst(To3(subject.Pos, .5f), Mat("5d8a3c"), 3, 1.2f, .7f, 5, .06f);
                break;
            case ThingKind.WolfCorpse: Burst(side + Vector3.Up * .3f, Mat("7a1414", .6f), 5, 1.4f, .45f, 8, .04f); break;
            case ThingKind.Berries: Burst(To3(subject.Pos, .7f), Mat("5d8a3c"), 3, 1.2f, .7f, 5, .06f); break;
            case ThingKind.Water: Burst(side + Vector3.Up * .05f, Splash, 6, 1.6f, .45f, 7, .045f); break;
            case ThingKind.Anvil when prop is AnvilProp anvil: Burst(To3(subject.Pos) + anvil.Vent, Sparks, 10, 3, .35f, 5, .025f); break;
            case ThingKind.Reinforced:
                if (subject.Heat > 10) Burst(To3(subject.Pos, .9f) + new Vector3(0, 0, .5f), Embers, 6, 1.4f, .6f, -1, .03f);
                break;
        }
    }

    /// <summary>Seconds a felled tree takes to reach the ground: a second for the shortest, just over two for the tallest.</summary>
    private static float FallSeconds(float tall) => 1 + 1.1f * tall;
    /// <summary>Seconds a felled tree lies where it landed before it starts to fade, and seconds its trunk and crown then take to fade away over the stump.</summary>
    private const float TreeLiesSeconds = .1f, TreeFadeSeconds = .5f;
    /// <summary>How tall a tree stands (<see cref="WorldModels.TreeTall"/>) before it tears at length as it goes over rather than cracks.</summary>
    private const float TearsFrom = .7f;
    /// <summary>How much lower and slower a tree's sounds play the taller it stands.</summary>
    private static float TreePitch(float tall) => 1.1f - .2f * tall;
    private readonly HashSet<int> seen = new();
    private readonly List<Thing> known = new();
    private readonly List<int> gone = new();
    // Models of things that left the world in front of the player, shown until they have played their way out.
    private readonly List<int> departed = new();
    private readonly List<Prop> leaving = new();
    /// <summary>Seconds a dug out stump or bush takes to come out of the ground and be gone.</summary>
    private const float DugOutSeconds = .6f;
    private SaveState? builtFor;
    // How many things the world held, and the newest of them, when models were last cleared away: a tree swapped for its stump changes only the second.
    private int thingsCounted = -1, newestThing;
    private static readonly Vector2 Nowhere = new(-1e6f, -1e6f);
    // Where the player stood when things were last drawn.
    private Vector2 sawFrom = Nowhere;
    /// <summary>Seconds a thing going out of sight is moved on by, enough to finish whatever it was doing: a tree falling, a chip flying, a door swinging.</summary>
    private const float SettleSeconds = 10;
    /// <summary>
    /// Whether a spot was in the player's sight when things were last drawn, so that a thing which has no model yet appeared there in front of them.
    /// One that appeared out of sight, or is first seen as the player walks up, shows as it now is without flying in or landing in dust.
    /// </summary>
    private bool Watched(Vector2 at) => at.DistanceTo(sawFrom) <= World.Data.SightRadius;
    /// <summary>Dark enough for cabin windows to be lit, which is all that night changes in a thing drawn as last seen.</summary>
    private bool Dark => night > .45f;
    private void SyncThings(float dt) {
        // Every model is built while a world loads, hidden until the player first sees it, so exploring builds nothing mid-game.
        // Another world, loaded or started, starts from fresh models: the old ones carry the state of things with the same ids.
        if (builtFor != World.S) {
            builtFor = World.S;
            thingsCounted = -1;
            // Nothing in a world just loaded appeared in front of the player.
            sawFrom = Nowhere;
            foreach (var (_, old) in props) old.Root.QueueFree();
            props.Clear();
            foreach (var old in leaving) old.Root.QueueFree();
            leaving.Clear();
            foreach (var t in World.Things) Show(PropFor(t), false);
        }
        seen.Clear();
        bool everything = filming || EnvironmentReview && EnvironmentCenter.HasValue;
        if (everything) { known.Clear(); known.AddRange(World.Things); } else World.CollectKnown(known);
        bool dark = Dark;
        foreach (var t in known) {
            seen.Add(t.Id);
            var prop = PropFor(t);
            // Most of an explored valley is out of sight, drawn as it was last seen.
            // That only changes when it is seen again, by this player or another, or when night falls or lifts,
            // so such a thing is settled once and then left alone: only what is in sight is drawn every frame.
            bool live = everything || World.Sees(t.Pos);
            // What a thing drawn as last seen can show: what a memory keeps of it (World.LookAround) that its kind draws, and night in a cabin's windows.
            var look = (t.Pos, t.Remaining, t.Kind is ThingKind.Mine or ThingKind.WolfCorpse || t.IsDug ? t.Progress : 0, t.MineStage, t.FarmStage, t.Kind == ThingKind.Hut && dark, live);
            if (!live && prop.Seen && prop.Look == look) continue;
            Show(prop, true);
            if (prop.Look.Pos != t.Pos) prop.Root.Position = To3(t.Pos);
            // A change plays out only where the player was already watching, not on a thing first seen or seen again this frame.
            bool watched = live && prop.Seen && prop.Look.Live;
            Animate(prop, t, live ? dt : SettleSeconds, live, watched);
            prop.Look = look;
        }
        sawFrom = World.Player;
        departed.Clear();
        foreach (var (id, prop) in props) {
            if (seen.Contains(id) || !prop.Shown) continue;
            if (World.Find(id) != null) { Show(prop, false); continue; }
            // What was in sight a moment ago and has left the world may show how it went; anything else is cleared away below.
            if (prop.Look.Live && Departs(prop)) departed.Add(id); else Show(prop, false);
        }
        foreach (int id in departed) { leaving.Add(props[id]); props.Remove(id); }
        for (int i = leaving.Count - 1; i >= 0; i--) {
            if (Depart(leaving[i], dt)) continue;
            leaving[i].Root.QueueFree(); leaving.RemoveAt(i);
        }
        // Things come and go rarely, so the models of those that left are looked for only when the count or the newest thing changes.
        int newest = World.Things.Count > 0 ? World.Things[World.Things.Count - 1].Id : 0;
        if (thingsCounted != World.Things.Count || newestThing != newest) {
            (thingsCounted, newestThing) = (World.Things.Count, newest);
            RefreshDressing();
            gone.Clear();
            foreach (var (id, _) in props) if (!seen.Contains(id) && World.Find(id) == null) gone.Add(id);
            foreach (int id in gone) { props[id].Root.QueueFree(); props.Remove(id); }
        }
        FadeCanopies(dt);
    }

    private static void Show(Prop prop, bool shown) {
        if (prop.Shown == shown) return;
        prop.Shown = shown; prop.Root.Visible = shown;
    }

    /// <summary>The model drawn for a thing, built when it has none or its kind changed.</summary>
    private Prop PropFor(Thing t) {
        if (props.TryGetValue(t.Id, out var prop) && prop.Kind == t.Kind) return prop;
        // A building or crate put down in front of the player lands with a puff of dust; a kind changing in place, such as a dug mine, does not.
        bool appeared = Watched(t.Pos);
        if (prop == null && appeared && (t.Kind == ThingKind.Crate || World.Data.Buildings.ContainsKey(t.Kind))) Plop(t.Pos, World.Footprint(t.Kind));
        if (prop != null) prop.Root.QueueFree();
        prop = WorldModels.Build(t);
        scene.AddChild(prop.Root);
        props[t.Id] = prop;
        prop.Root.Position = To3(t.Pos);
        // A fresh carcass keeps the heading of the wolf model it replaces.
        // It also waits for the killing blow to land, and is thrown back from where the wolf stood.
        if (prop is CarcassProp carcass && Dying(t.Pos) is { } dying) {
            prop.Body.Rotation = new Vector3(0, dying.Yaw, 0);
            carcass.From = dying.Root.Position - To3(t.Pos); carcass.HitIn = SwingLands(dying.Last + dying.Knock).In;
        }
        // A chip that appears in front of the player was knocked off the rock beside it, and flies from there.
        if (prop is LooseProp chip && appeared && ChippedFrom(t) is Thing rock) { chip.From = To3(rock.Pos) - To3(t.Pos) + Vector3.Up * .7f; chip.Flight = 0; }
        return prop;
    }

    /// <summary>Whether a model whose thing has left the world plays its way out: a felled tree, and a stump or bush that was dug out rather than built over.</summary>
    private bool Departs(Prop prop) => prop is TreeProp || prop is DugProp && !BuiltOver(prop.Look.Pos);

    /// <summary>Whether a building, yard or crate now stands over a spot, having cleared away the stump or bush that was there.</summary>
    private bool BuiltOver(Vector2 at) {
        foreach (var kind in World.Structures)
            foreach (var t in World.ThingsOf(kind)) if (World.Clears(t, at)) return true;
        return false;
    }

    /// <summary>
    /// Moves on a model whose thing has left the world, and tells whether it still has more to show.
    /// A felled tree topples, raises dust where its crown lands and fades away over the stump it left.
    /// A dug out stump or bush is thrown up out of a spray of soil, tumbling over, and shrinks away as it comes down.
    /// </summary>
    private bool Depart(Prop prop, float dt) {
        switch (prop) {
            case TreeProp tree:
                float lands = FallSeconds(tree.Tall);
                // The wood gives as the tree starts to go: the tearing of a tall tree lasts about as long as its fall.
                if (tree.Fall < 0) Sounds?.Play(tree.Tall >= TearsFrom ? "tree_bend" : "tree_crack", prop.Look.Pos, pitch: TreePitch(tree.Tall));
                float before = Math.Max(0, tree.Fall); tree.Fall = before + dt;
                float drop = Math.Min(1, tree.Fall / lands);
                tree.Sway.RotationDegrees = new Vector3(90 * drop * drop, 0, 0);
                if (before < lands && tree.Fall >= lands) {
                    Burst(prop.Root.Position + tree.Sway.GlobalBasis.Y * 3.5f + Vector3.Up * .3f, FallDust, 8, 1.2f, 1.1f, -.2f, .3f, 1.5f);
                    Sounds?.Play("tree_crash", prop.Look.Pos, pitch: TreePitch(tree.Tall));
                }
                SetFade(tree.SwayParts, Math.Clamp((tree.Fall - lands - TreeLiesSeconds) / TreeFadeSeconds, 0, 1));
                return tree.Fall < lands + TreeLiesSeconds + TreeFadeSeconds;
            case DugProp dug:
                if (dug.Out < 0) {
                    dug.Out = 0;
                    var foot = prop.Root.Position + dug.Seat;
                    Burst(foot + Vector3.Up * .1f, Soil, 10, 2.4f, .6f, 9, .07f);
                    Burst(foot + Vector3.Up * .15f, GatherDust, 4, .6f, .9f, -.3f, .2f, 1.5f);
                    if (prop.Kind == ThingKind.Bush) Burst(foot + Vector3.Up * .5f, Mat("5d8a3c"), 6, 1.6f, .8f, 5, .06f);
                }
                dug.Out += dt;
                float u = Math.Min(1, dug.Out / DugOutSeconds);
                // It carries on from the lean digging left it at.
                prop.Body.Position = dug.Seat + new Vector3(0, .07f + MathF.Sin(u * MathF.PI) * .45f, 0);
                prop.Body.RotationDegrees = new Vector3(16 + 150 * Ease(u), dug.Yaw, 0);
                prop.Body.Scale = Vector3.One * (1 - Mathf.SmoothStep(.45f, 1, u));
                return u < 1;
            default: return false;
        }
    }

    /// <summary>The rock or ore deposit a newly appeared chip came off, or null when none is close enough.</summary>
    private Thing? ChippedFrom(Thing chip) {
        Thing? rock = null; float nearest = (GatherRules.ChipReach + .5f) * (GatherRules.ChipReach + .5f);
        foreach (var t in World.Things)
            if (GatherRules.Of(t.Kind)?.Chip == chip.Kind && t.Pos.DistanceSquaredTo(chip.Pos) <= nearest) { rock = t; nearest = t.Pos.DistanceSquaredTo(chip.Pos); }
        return rock;
    }

    /// <summary>
    /// Moves a thing's model on by <paramref name="dt"/> and shows the state of <paramref name="t"/>.
    /// A thing that is not <paramref name="live"/> is out of sight: it is drawn as remembered, with nothing that only shows while someone watches.
    /// A change plays out only when the player <paramref name="watched"/> the thing before it; otherwise the thing shows as it now is.
    /// </summary>
    private void Animate(Prop prop, Thing t, float dt, bool live, bool watched) {
        bool first = !prop.Seen, shaking = prop.Shake > 0;
        // Most things in sight stand still: one that is neither shaking nor holding more or less than when last drawn needs no writing to.
        bool still = !first && !shaking && prop.Look.Remaining == t.Remaining;
        prop.Seen = true;
        prop.Shake = Math.Max(0, prop.Shake - dt * 2.5f);
        float wobble = prop.Shake * MathF.Sin(Elapsed * 34);
        float flicker = MathF.Sin(Elapsed * 13 + t.Id) * .5f + MathF.Sin(Elapsed * 7.3f + t.Id * 3) * .5f;
        switch (prop) {
            case TreeProp tree:
                if (first || shaking) tree.Sway.RotationDegrees = new Vector3(wobble * 3, 0, prop.Shake * 2 * MathF.Cos(Elapsed * 29));
                break;
            case DugProp dug:
                if (still && prop.Look.Progress == t.Progress) break;
                // Digging works it loose: it leans further over with the work done, and rocks as each push of the shovel lands.
                float loose = Ease(Math.Clamp(t.Progress / GatherRules.DigSeconds, 0, 1));
                prop.Body.RotationDegrees = new Vector3(loose * 16 + wobble * 5, dug.Yaw, wobble * 3);
                prop.Body.Position = dug.Seat + new Vector3(wobble * .02f, loose * .07f, 0);
                break;
            case LooseProp chip when chip.Flight < WorldModels.ChipFlight:
                // An arc from the rock with a hop at the end, spinning as it goes.
                chip.Flight += dt;
                float flown = Math.Min(1, chip.Flight / WorldModels.ChipFlight);
                prop.Body.Position = chip.From * (1 - flown) + Vector3.Up * (MathF.Sin(flown * MathF.PI) * .6f + MathF.Abs(MathF.Sin(flown * MathF.PI * 3)) * .08f * flown);
                prop.Body.Rotation = new Vector3(flown * 9, flown * 4, 0) * (1 - flown);
                break;
            case Prop when t.IsLoose || t.Kind is ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin or ThingKind.Leaves:
                if (still) break;
                prop.Body.Visible = t.Remaining > 0;
                prop.Body.Scale = Vector3.One * (.55f + .45f * Math.Clamp(WorldModels.Fullness(t), 0, 1));
                prop.Body.Position = new Vector3(wobble * .03f, 0, 0);
                break;
            case CarcassProp carcass:
                // Thrown, the carcass covers the knockback while it is in the air and stays where it lands.
                float since = t.Progress - carcass.HitIn, dying = Math.Clamp(since / WolfModel.FallSeconds, 0, 1), thrown = Math.Min(1, dying / WolfModel.ThrowShare);
                // The killing blow thuds as it lands, heard only where the player saw the wolf fall.
                if (!carcass.Landed && since >= 0) { carcass.Landed = true; if (watched) Sounds?.Play("wolf_hit", t.Pos); }
                carcass.Body.Position = carcass.From * (1 - thrown * (2 - thrown));
                carcass.Wolf.Fall(dying);
                // Butchering flattens the carcass while the blood pool spreads out from under it.
                carcass.Wolf.Root.Scale = new Vector3(1, .6f + .4f * WorldModels.Fullness(t), 1);
                carcass.Wolf.Root.Position = new Vector3(wobble * .03f, 0, 0);
                carcass.Pool.Scale = Vector3.One * (.2f + .8f * Ease(dying)) * (1.25f - .25f * WorldModels.Fullness(t));
                break;
            case BushProp bush:
                if (still) break;
                bush.Fruit.Visible = t.Remaining > 0;
                prop.Body.Scale = new Vector3(1 + wobble * .04f, 1 - wobble * .03f, 1 + wobble * .04f);
                break;
            case FurnaceProp furnace:
                if (live) WorldModels.SyncFuel(furnace, t, World.Data);
                furnace.Glow.Visible = t.Processing;
                furnace.Light.LightEnergy = t.Processing ? (1.1f + flicker * .25f) * (.5f + night) : 0;
                Smoke(furnace, t, t.Processing, dt, FurnaceSmoke);
                break;
            case StoveProp stove:
                // The fire only burns for recipes that take fuel; the mortar's work and kneading dough are cold.
                bool cooking = t.Processing && World.Data.Recipes.TryGetValue(t.Recipe, out var cooked) && cooked.Burns;
                stove.Glow.Visible = cooking;
                stove.Light.LightEnergy = cooking ? (.9f + flicker * .25f) * (.5f + night) : 0;
                if (stove.Mortar != null && live) stove.Mortar.Visible = t.Tools != null && World.BenchTool(t, "mortar") != null && !grinding.Contains(t.Id);
                Smoke(stove, t, cooking, dt, FurnaceSmoke);
                break;
            case ForgeProp forge:
                float heat = t.Heat / 100;
                forge.Glow.Visible = heat > .05f || t.Processing;
                forge.Light.LightEnergy = (heat * 1.5f + (t.Processing ? .4f : 0)) * (1 + flicker * .15f) * (.5f + night);
                forge.Needle.RotationDegrees = new Vector3(0, 0, Mathf.Lerp(forge.Needle.RotationDegrees.Z, WorldModels.Gauge(heat), Math.Min(1, dt * 6)));
                float lift = 18 * (1 - pumping.GetValueOrDefault(t.Id));
                forge.Lever.RotationDegrees = new Vector3(0, 0, lift);
                for (int i = 0; i < forge.Pleats.Length; i++) forge.Pleats[i].RotationDegrees = new Vector3(0, 0, lift * (i + 1) / (forge.Pleats.Length + 1));
                Smoke(forge, t, t.Processing || heat > .3f, dt, ForgeSmoke);
                break;
            case CampfireProp campfire:
                bool burning = t.Burn > 0;
                campfire.Fire.Visible = burning;
                campfire.Fire.Scale = new Vector3(1 + flicker * .08f, .85f + MathF.Abs(MathF.Sin(Elapsed * 9 + t.Id)) * .3f, 1 - flicker * .08f);
                campfire.Fire.RotationDegrees = new Vector3(0, Elapsed * 40, 0);
                campfire.Light.LightEnergy = burning ? (1.2f + flicker * .3f) * (.35f + night * 1.4f) : 0;
                if (burning && dt > 0 && Jitter() < dt * 3) Burst(To3(t.Pos, .55f), Cinders, 1, .5f, 1.1f, -1.4f, .025f);
                Smoke(campfire, t, burning, dt, CampfireSmoke);
                break;
            case BenchProp bench:
                // Slotted tools lie on the bench top.
                if (!live) break;
                ShowSlotted(bench.Chisel, World.BenchTool(t, "chisel"));
                ShowSlotted(bench.Hammer, World.BenchTool(t, "hammer"));
                prop.Body.Position = new Vector3(wobble * .02f, 0, 0);
                break;
            case PileProp pile:
                // Remembered piles carry no inventory, so they keep the stacks they had when last seen.
                if (!live) break;
                WorldModels.SyncPile(pile, t, World.Data);
                break;
            case FarmProp farm:
                if (!live) break;
                int look = World.CropLook(t, World.GrowSeconds);
                farm.Crops[0].Visible = look > 0;
                for (int i = 1; i < farm.Crops.Length; i++) farm.Crops[i].Visible = look == i + 1;
                break;
            case PackerProp packer:
                // Remembered stations carry no inventory, so they keep the crates they had when last seen.
                if (!live) break;
                for (int i = 0; i < packer.Crates.Length; i++) packer.Crates[i].Visible = i < t.Inventory["crate"];
                packer.Packing.Visible = t.Processing;
                // The heap grows toward a full one at a crate's worth.
                packer.Stock.Fill(t.Packs, t.Packs == "" ? 0 : t.Inventory[t.Packs] / (float)World.Data.CrateCapacity, World.Data);
                break;
            case RackProp rack:
                if (!live) break;
                // Each peg shows the tool in its slot.
                var layout = t.Inventory.Layout;
                for (int slot = 0; slot < rack.Pegs.Length; slot++) {
                    string held = slot < layout.Count ? layout[slot].Item : "";
                    string kind = held != "" && World.Data.Items.TryGetValue(held, out var def) ? def.Tool : "";
                    foreach (var (tool, hook) in rack.Pegs[slot]) hook.Visible = tool == kind;
                }
                break;
            case BoxProp box:
                // Remembered boxes carry no inventory, so the lid stays as it was last seen.
                if (!live) break;
                int count = t.Inventory.Count;
                if (watched && count != box.LastCount) box.Puff = .9f;
                box.LastCount = count;
                box.Puff = Math.Max(0, box.Puff - dt);
                bool open = box.Puff > 0 || t.Id == Selection.Thing && PanelOpen && World.Reaches(t);
                if (dt > 0 && (open ? box.Open == 0 : box.Open == 1)) Sounds?.Play(open ? "open_chest" : "close_chest", t.Pos);
                box.Open = Mathf.MoveToward(box.Open, open ? 1 : 0, dt * 4);
                box.Lid.RotationDegrees = new Vector3(-80 * Ease(box.Open), 0, 0);
                if (box.Marker != null) {
                    box.Marker.Position = new Vector3(0, 1.2f + .08f * MathF.Sin(Elapsed * 2.5f), 0);
                    box.Marker.Rotation = new Vector3(0, Elapsed * 1.5f, 0);
                }
                // A crate being lifted rises and rocks before it goes into the backpack.
                float lifted = t.Id == Lifting ? Ease(LiftProgress) : 0;
                if (t.Kind == ThingKind.Crate) { prop.Body.Position = new Vector3(0, lifted * .35f, 0); prop.Body.RotationDegrees = new Vector3(lifted * 9 * MathF.Sin(Elapsed * 18), 0, lifted * 6 * MathF.Cos(Elapsed * 15)); }
                break;
            case HutProp hut:
                // The door stands open while someone steps in or out, and shuts behind them: swung shut after a walk, slammed after a run.
                var through = live ? Passing(t.Id) : null;
                if (through != null) hut.Slam = through.DoorRush;
                if (dt > 0 && through != null && hut.Open == 0 && !through.DoorRush) Sounds?.Play("hut_door_normal", t.Pos);
                if (dt > 0 && through == null && hut.Open == 1 && hut.Slam) Sounds?.Play("hut_door_escaping", t.Pos);
                hut.Open = Mathf.MoveToward(hut.Open, through != null ? 1 : 0, Math.Max(dt, .016f) * (through == null && hut.Slam ? 8 : 1 / World.ShutSeconds));
                hut.Door.RotationDegrees = new Vector3(0, -100 * Ease(hut.Open), 0);
                bool lit = Dark;
                if (lit != hut.Lit) {
                    hut.Lit = lit;
                    hut.Window.MaterialOverride = lit ? NightGlass : DayGlass;
                    hut.Light.LightEnergy = lit ? .9f : 0;
                }
                break;
            case MineProp mine:
                // Remembered mines carry only their stage and progress, so out of sight the cart rests outside, empty, and the lantern is out.
                float stageShare = Math.Clamp(t.Progress / World.MineStageSeconds, 0, 1);
                bool finished = t.MineStage == MineStage.Open;
                mine.Outline.Visible = !finished;
                mine.Hole.Visible = t.MineStage > MineStage.Marked || stageShare > 0;
                mine.Hole.Scale = new Vector3(1, 1, t.MineStage > MineStage.Marked ? 1 : Math.Max(.05f, stageShare));
                mine.Supplies.Visible = t.MineStage == MineStage.Supplied;
                mine.Frame.Visible = finished || t.MineStage == MineStage.Supplied && stageShare > 0;
                mine.Frame.Scale = new Vector3(1, finished ? 1 : Math.Max(.05f, stageShare), 1);
                bool mining = finished && live && Underground(t.Id);
                mine.Rails.Visible = finished;
                mine.Cart.Visible = finished && !mining;
                mine.Load.Visible = (t.Output?.Count ?? 0) > 0;
                mine.Glow.Visible = mine.Fire.Visible = mining;
                // The lantern lights its surroundings like a held torch.
                if (mining) mine.Fire.Scale = new Vector3(1, .9f + flicker * .1f, 1);
                mine.Light.LightEnergy = mining ? (1 + flicker * .25f) * (.3f + night * 1.3f) : 0;
                break;
            case SignpostProp sign:
                bool waiting = live && OrderAt(t.Id);
                sign.Open = Mathf.MoveToward(sign.Open, waiting ? 1 : 0, Math.Max(dt, .016f) * 3);
                sign.Flag.RotationDegrees = new Vector3(-90 * (1 - Ease(sign.Open)), 0, 0);
                break;
        }
    }

    private static float Ease(float t) => t * t * (3 - 2 * t);

    // Lookups that would otherwise capture the thing in a lambda, which allocates on every call for every thing drawn.
    private WolfModel? Dying(Vector2 at) {
        foreach (var (wolf, model) in wolves) if (!World.Wolves.Contains(wolf) && model.Last.DistanceTo(at) < 2) return model;
        return null;
    }
    /// <summary>The player a tugging wolf drags as drawn, and how far through the drag they are, 0 to 1; null once they are not being dragged.</summary>
    private (ActorState Held, float Progress)? Dragged(Wolf wolf) {
        foreach (var p in World.Players)
            if (p.Id == wolf.Target && p.Door is Doorway.Pulled or Doorway.PulledOff)
                return (p.Id == World.Me.Id ? player : others.TryGetValue(p.Id, out var other) ? other.State : player, Math.Clamp(p.DoorTime / World.PullSeconds, 0, 1));
        return null;
    }
    /// <summary>
    /// How far beside a fallen player's throat a wolf that dragged them out waits while they fall, and the shares of
    /// <see cref="ActorAnimation.FallBackSeconds"/> over which it then lunges onto the throat.
    /// </summary>
    private const float WaitAside = 1.2f, LungeFrom = .74f, LungeTo = .92f;
    /// <summary>The drawn player a wolf pins from beside them, or null.</summary>
    private ActorState? Felled(Wolf wolf) {
        foreach (var p in World.Players)
            if (p.Id == wolf.Target) return p.Id == World.Me.Id ? player : others.TryGetValue(p.Id, out var other) ? other.State : null;
        return null;
    }
    /// <summary>Whether a player is pinned by a wolf beside them, which dragged them back out of a doorway, so they fall back rather than turn to it.</summary>
    private bool FallsBack(int player) => PinningWolf(player) is { Beside: var beside } && beside != Vector2.Zero;
    /// <summary>
    /// Which way a wolf dragging <paramref name="held"/> turns from another wolf dragging someone within <see cref="TuggersApart"/>
    /// of them: 1 to the held player's left, -1 to their right, 0 with nobody there.
    /// </summary>
    private float Beside(Wolf tugger, ActorState held) {
        foreach (var other in World.Wolves)
            if (other != tugger && other.Tugging && Dragged(other) is var (them, _) && them != held && them.Position.DistanceTo(held.Position) < TuggersApart) {
                var left = new Vector2(MathF.Cos(held.Yaw), -MathF.Sin(held.Yaw));
                return (held.Position - them.Position).Dot(left) >= 0 ? -1 : 1;
            }
        return 0;
    }
    /// <summary>How near two dragged players are when their wolves angle apart, and by how many degrees.</summary>
    private const float TuggersApart = 1.2f, SplayTugger = 35;
    private Wolf? PinningWolf(int player) {
        foreach (var w in World.Wolves) if (w.Pinning && w.Target == player) return w;
        return null;
    }
    /// <summary>A wolf in the air at <paramref name="p"/> whose jaws have reached them.</summary>
    private Wolf? Knocking(PlayerState p) {
        foreach (var w in World.Wolves) if (w.Leap > 0 && w.Target == p.Id && w.Pos.DistanceTo(p.Pos) <= WolfModel.ContactReach) return w;
        return null;
    }
    private Vector2 Hunted(Wolf wolf) {
        foreach (var p in World.Players) if (p.Id == wolf.Target) return p.Pos;
        return World.Player;
    }
    private bool HuntedTorchLit(Wolf wolf) {
        foreach (var p in World.Players) if (p.Id == wolf.Target) return p.TorchLit;
        return false;
    }
    /// <summary>Seconds until the swing of the player nearest <paramref name="at"/> lands, or zero when nobody is mid-swing; and where that player stands.</summary>
    private (float In, Vector2 From) SwingLands(Vector2 at) {
        float best = float.MaxValue, lands = 0;
        var from = World.Player;
        if (pulseRemaining > 0) { best = World.Player.DistanceTo(at); lands = ActorAnimation.UntilContact(pulseAction, pulseRemaining); }
        foreach (var (_, other) in others)
            if (other.PulseRemaining > 0 && other.State.Position.DistanceTo(at) < best) {
                best = other.State.Position.DistanceTo(at); from = other.State.Position;
                lands = ActorAnimation.UntilContact(other.Pulse, other.PulseRemaining);
            }
        return (lands, best == float.MaxValue ? World.Player : from);
    }
    /// <summary>The player stepping in or out through a cabin's door, or null.</summary>
    private PlayerState? Passing(int cabin) {
        foreach (var p in World.Players) if (p.DoorCabin == cabin && p.Door is Doorway.Entering or Doorway.Pulled or Doorway.Leaving) return p;
        return null;
    }
    private bool Underground(int mine) {
        foreach (var w in World.Workers) if (w.Assignment == mine && w.WorkState == WorkerWorkState.Underground) return true;
        return false;
    }
    private bool OrderAt(int signpost) {
        foreach (var o in World.Orders) if (o.Signpost == signpost) return true;
        return false;
    }

    // Campfire, furnace and reinforced furnace smoke: the coal forge's plume is darker, denser and wider.
    private readonly record struct SmokeStyle(float Every, Color Color, float Size, float Grow);
    private static readonly SmokeStyle CampfireSmoke = new(.5f, new Color(.62f, .62f, .6f, .38f), .14f, 2.2f);
    private static readonly SmokeStyle FurnaceSmoke = new(.3f, new Color(.58f, .58f, .56f, .42f), .14f, 2.4f);
    private static readonly SmokeStyle ForgeSmoke = new(.16f, new Color(.36f, .36f, .36f, .55f), .2f, 2.8f);

    private void Smoke(HearthProp prop, Thing t, bool active, float dt, SmokeStyle smoke) {
        // Seen from the ground by the film's camera, the puffs are flat discs.
        if (!active || dt <= 0 || filming) return;
        prop.Smoke -= dt;
        if (prop.Smoke > 0) return;
        prop.Smoke = smoke.Every;
        Burst(To3(t.Pos) + prop.Vent, Flat(smoke.Color), 1, .25f, 2.4f, -.45f, smoke.Size, smoke.Grow);
    }

    private readonly List<Thing> structures = new();
    /// <summary>Trees in front of the player, a worker or a structure turn see-through instead of hiding them.</summary>
    private void FadeCanopies(float dt) {
        // The film's camera stands among the trees, where nothing needs seeing through.
        if (filming) return;
        var screen = GetViewport().GetVisibleRect();
        var bounds = screen.Grow(300);
        float ppu = screen.Size.Y / camera.Size;
        ReadScreenAxes();
        structures.Clear();
        foreach (var kind in World.Structures)
            foreach (var o in World.ThingsOf(kind))
                if (bounds.HasPoint(OnScreen(o.Pos)) && seen.Contains(o.Id)) structures.Add(o);
        foreach (var t in World.ThingsOf(ThingKind.Tree)) {
            if (t.Remaining <= 0 || !bounds.HasPoint(OnScreen(t.Pos)) || !seen.Contains(t.Id) || props.GetValueOrDefault(t.Id) is not TreeProp tree) continue;
            float fade = Mathf.MoveToward(tree.Fade, CoversSomething(t, ppu) ? .68f : 0, Math.Max(dt, .016f) * 3);
            if (Math.Abs(fade - tree.Fade) < .001f) continue;
            tree.Fade = fade; SetFade(tree.SwayParts, fade);
        }
    }

    private bool CoversSomething(Thing tree, float ppu) {
        if (!World.Dead && TreeCoversPerson(tree, World.Player, ppu)) return true;
        foreach (var w in World.Workers) if (World.Sees(w) && TreeCoversPerson(tree, w.Pos, ppu)) return true;
        foreach (var p in World.Players) if (p != World.Me && p.RespawnIn <= 0 && World.Sees(p.Pos) && TreeCoversPerson(tree, p.Pos, ppu)) return true;
        foreach (var o in structures) if (Covers(tree, o, ppu)) return true;
        return false;
    }

    private void SyncWolves(float dt) {
        foreach (var wolf in World.Wolves) {
            if (!wolves.TryGetValue(wolf, out var model)) {
                model = new WolfModel { Last = wolf.Pos, Yaw = ActorAnimation.Yaw(Hunted(wolf) - wolf.Pos, 0) };
                scene.AddChild(model.Root); wolves[wolf] = model;
            }
            var hunted = Hunted(wolf);
            var moved = wolf.Pos - model.Last;
            model.Last = wolf.Pos;
            if (wolf.Tugging && Dragged(wolf) is var (held, through)) {
                // Its jaws hold the back of the player's hips as they are drawn, the wolf facing the way they face,
                // or angled away from another wolf dragging someone beside them so the two bodies keep clear.
                model.Yaw = held.Yaw + Mathf.DegToRad(SplayTugger) * Beside(wolf, held); model.Knock = Vector2.Zero; model.Stagger = wolf.Stagger; model.Toward = Vector2.Zero; model.Pinned = 0;
                model.Root.Rotation = new Vector3(0, model.Yaw, 0);
                model.Root.Visible = World.Sees(wolf.Pos);
                model.Tug(through, Elapsed, dt);
                var waist = To3(held.Position - new Vector2(MathF.Sin(held.Yaw), MathF.Cos(held.Yaw)) * WaistBehind) + Vector3.Up * held.Pose.HipHeight;
                var grip = waist - model.Root.Basis * model.Bite;
                model.Root.Position = grip with { Y = 0 };
                continue;
            }
            if (wolf.Pinning && wolf.Beside != Vector2.Zero && Felled(wolf) is ActorState fallen) {
                // Having dragged its player out, it lets go as they fall back toward it, steps round to their side and lunges onto the throat as they land.
                if (model.Pinned == 0) model.StepFrom = model.Root.Position;
                model.Pinned += dt;
                float fell = model.Pinned / ActorAnimation.FallBackSeconds;
                var throat = Throat(fallen.Position, new Vector2(MathF.Sin(fallen.Yaw), MathF.Cos(fallen.Yaw)), float.MaxValue);
                var wait = (throat + To3(wolf.Beside) * WaitAside) with { Y = 0 };
                model.Knock = Vector2.Zero; model.Stagger = wolf.Stagger; model.Toward = Vector2.Zero;
                model.Yaw = Mathf.LerpAngle(model.Yaw, ActorAnimation.Yaw(-wolf.Beside, model.Yaw), Math.Min(1, dt * 8));
                model.Root.Rotation = new Vector3(0, model.Yaw, 0);
                model.Root.Visible = World.Sees(wolf.Pos);
                if (fell < LungeFrom) {
                    // It springs back off the yanked body at once, then slows as it comes round.
                    float off = fell / LungeFrom;
                    var at = model.StepFrom.Lerp(wait, 1 - (1 - off) * (1 - off));
                    // It backs off the falling body rather than walking forward while it goes back.
                    model.Move((at - model.Root.Position).Dot(model.Root.Basis.Z), dt, false, false);
                    model.Root.Position = at;
                    continue;
                }
                model.Feed(Elapsed + wolf.GetHashCode() % 7, dt, true, 1, beside: true);
                if (fell < LungeTo) {
                    var bite = (throat - model.Root.Basis * model.Bite) with { Y = 0 };
                    model.Root.Position = model.Held = wait.Lerp(bite, Ease((fell - LungeFrom) / (LungeTo - LungeFrom)));
                } else model.Root.Position = model.Held;
                continue;
            }
            // A hit throws the wolf back at once in the world; the model holds its place and lunges into the swing, then is thrown back when it lands.
            if (wolf.Stagger > model.Stagger) {
                model.Knock -= moved; moved = Vector2.Zero;
                (model.HitIn, model.Striker) = SwingLands(wolf.Pos + model.Knock);
                model.HitSeconds = model.HitIn; model.HitFrom = wolf.Pos + model.Knock; model.Recoiling = 0;
            }
            model.Stagger = wolf.Stagger;
            float back = 0;
            if (model.HitIn > 0) {
                model.HitIn = Math.Max(0, model.HitIn - dt);
                // The lunge closes to striking distance of the swing; a wolf already that close stays put.
                var toward = model.HitFrom - model.Striker;
                var strike = model.Striker + toward.Normalized() * Math.Min(toward.Length(), WolfModel.StrikeReach);
                var at = model.HitFrom.Lerp(strike, Ease(1 - model.HitIn / Math.Max(model.HitSeconds, .001f)));
                back = -(at - wolf.Pos - model.Knock).Length();
                model.Knock = at - wolf.Pos;
                if (model.HitIn == 0) { model.Recoiling = WolfModel.RecoilSeconds; model.KnockFrom = model.Knock; FurAndEmbers(To3(at, .6f)); Sounds?.Play("wolf_hit", at); }
            } else if (model.Recoiling > 0) {
                // The wolf goes back only while it is in the air, so its paws never slide.
                model.Recoiling = Math.Max(0, model.Recoiling - dt);
                float hop = Math.Min(1, (1 - model.Recoiling / WolfModel.RecoilSeconds) / WolfModel.HopShare);
                model.Knock = model.KnockFrom * (1 - hop * (2 - hop));
            } else model.Knock = Vector2.Zero;
            bool struck = model.HitIn > 0 || model.Recoiling > 0, running = moved.LengthSquared() > 1e-6f && !wolf.Feeding && !struck;
            // A struck wolf keeps facing whoever hit it; a running one turns into its path.
            var facing = struck ? model.Striker - wolf.Pos - model.Knock : running ? moved : hunted - wolf.Pos;
            model.Yaw = Mathf.LerpAngle(model.Yaw, ActorAnimation.Yaw(facing, model.Yaw), Math.Min(1, dt * (struck ? 16 : 10)));
            model.Root.Visible = World.Sees(wolf.Pos);
            model.Root.Rotation = new Vector3(0, model.Yaw, 0);
            bool wary = wolf.Scared || HuntedTorchLit(wolf);
            float flight = 0;
            if (model.HitIn > 0) model.Move(-back, dt, false, true);
            else if (model.Recoiling > 0) model.Recoil(1 - model.Recoiling / WolfModel.RecoilSeconds, dt);
            else if (wolf.Leap > 0) {
                // The world closes the leap's gap evenly, so the start, and the point where the jaws reach the throat, follow from how far it has come.
                float progress = 1 - wolf.Leap / World.LeapSeconds, gap = wolf.Pos.DistanceTo(hunted);
                float start = progress < 1 ? (gap - World.PinDistance * progress) / (1 - progress) : gap;
                float contact = start <= WolfModel.ContactReach ? 0 : (start - WolfModel.ContactReach) / (start - World.PinDistance);
                model.Pinned = gap <= WolfModel.ContactReach ? model.Pinned + dt : 0;
                if (model.Pinned > 0) model.Feed(Elapsed, dt, true, model.Pinned / KnockSeconds);
                else { model.Leap(progress, contact, dt); flight = contact > 0 ? progress / contact : 1; }
                if (model.Pinned == 0) model.Toward = (wolf.Pos - hunted).Normalized();
            } else if (wolf.Feeding) {
                if (wolf.Pinning) model.Pinned += dt;
                model.Feed(Elapsed + wolf.GetHashCode() % 7, dt, wolf.Pinning, model.Pinned / KnockSeconds);
            } else if (wolf.Howl > 0) model.Howl(1 - wolf.Howl / wolf.HowlLength, dt);
            else if (running) model.Move(moved.Length(), dt, wolf.Chase > 0, wary);
            else model.Stand(Elapsed + wolf.GetHashCode() % 7, dt, wary);
            var place = To3(wolf.Pos + model.Knock);
            if (wolf.Leap > 0 || wolf.Pinning) {
                if (model.Toward == Vector2.Zero) model.Toward = (wolf.Pos - hunted).Normalized();
                place = AtTheThroat(model, hunted, place, flight);
            } else model.Toward = Vector2.Zero;
            model.Root.Position = place;
        }
        foreach (var (wolf, model) in wolves)
            if (!World.Wolves.Contains(wolf)) { model.Root.QueueFree(); wolves.Remove(wolf); }
    }

    /// <summary>
    /// Where a leaping or pinning wolf stands so its jaws are at the hunted player's throat.
    /// The flight eases from the world's path onto the throat by the moment of contact; the jaws then ride the throat down as the player goes over, and the wolf holds that spot once they are down.
    /// </summary>
    private static Vector3 AtTheThroat(WolfModel model, Vector2 hunted, Vector3 path, float flight) {
        if (model.Pinned > KnockSeconds) return model.Held;
        var held = Throat(hunted, model.Toward, model.Pinned) - model.Root.Basis * model.Bite;
        held.Y = Math.Max(0, held.Y);
        if (model.Pinned > 0) return model.Held = held;
        return path.Lerp(held, Ease(Math.Clamp(flight, 0, 1)));
    }

    /// <summary>The rig's throat: above the feet and a little in front, before a knockdown tips it back.</summary>
    private const float ThroatHeight = 1.31f, ThroatAhead = .06f;

    /// <summary>Where the throat of a player at <paramref name="at"/> facing <paramref name="toward"/> is, <paramref name="downedFor"/> seconds into being knocked down; it follows KnockDown.</summary>
    public static Vector3 Throat(Vector2 at, Vector2 toward, float downedFor) {
        float tip = Ease(Math.Min(1, downedFor / KnockSeconds)), back = Mathf.Pi / 2 * tip;
        var forward = To3(toward.Normalized());
        return To3(at) + forward * (KnockSlide * tip + ThroatAhead * MathF.Cos(back) - ThroatHeight * MathF.Sin(back))
            + Vector3.Up * (.17f * tip + ThroatHeight * MathF.Cos(back) + ThroatAhead * MathF.Sin(back));
    }

    /// <summary>Tufts of fur knocked loose by a hit, and embers off a burning torch.</summary>
    private void FurAndEmbers(Vector3 at) {
        Burst(at, Mat("6f746c"), 5, 1.8f, .5f, 6, .045f);
        if (World.TorchFlare > 0) Burst(at, Embers, 9, 2.8f, .35f, 3, .03f);
    }

    /// <summary>Fully drawn for the first part of a trip, then fading to nothing at the turn.</summary>
    private static float TripFade(float leg) => Math.Clamp((leg - .35f) / .55f, 0, 1);
    // The cart walks at a steady pace until it is well down the valley, waits there out of view, and walks back for the return; the trade delay is unchanged.
    private const float CartSpeed = 2.2f, CartDistance = 16;
    /// <summary>How far out a cart is, from 0 at the signpost to 1 where it has gone from view.</summary>
    private float CartLeg(float elapsed) => World.WalkLeg(elapsed, World.Data.TradeSeconds - elapsed, CartSpeed, CartDistance);
    /// <summary>Fully drawn for most of the walk, fading out over its last quarter.</summary>
    private static float CartFade(float leg) => Math.Clamp((leg - .75f) / .25f, 0, 1);
    private readonly Dictionary<Node, float> fades = new();
    /// <summary>Rounds a fade to tenths and records it for the node, returning false when the node already shows that step.</summary>
    private bool FadeStep(Node node, float transparency, out float step) {
        step = MathF.Round(transparency * 10) / 10;
        if (fades.GetValueOrDefault(node) == step) return false;
        fades[node] = step; return true;
    }

    /// <summary>Fades a model through its gathered parts, which walks no nodes.</summary>
    private void Fade((MeshInstance3D Mesh, Material Material)[] parts, Node node, float transparency) {
        if (FadeStep(node, transparency, out float step)) SetFade(parts, step);
    }

    private void SyncCarts(float dt) {
        // Carts whose trip ended go back to the pool first, so an order starting the same frame finds one.
        foreach (var (order, cart) in carts)
            if (!World.Orders.Contains(order)) { fades.Remove(cart.Root); SetFade(cart.Parts, 0); cart.Root.Visible = false; carts.Remove(order); idleCarts.Push(cart); }
        foreach (var order in World.Orders) {
            var sign = World.Find(order.Signpost);
            float phase = order.Elapsed / World.Data.TradeSeconds;
            // The cart sets off down the valley and fades out on its way to Bramblewick, then fades back in on return.
            float leg = CartLeg(order.Elapsed);
            var pos = (sign?.Pos ?? Terrain.Spawn) + Terrain.Seaward * CartDistance * leg;
            if (!carts.TryGetValue(order, out var cart)) {
                // An order beyond the village's carts, such as an edited save's, gets a new one.
                cart = idleCarts.Count > 0 ? idleCarts.Pop() : NewCart();
                cart.Last = pos; cart.Distance = 0; cart.Yaw = 0;
                carts[order] = cart;
            }
            var moved = pos - cart.Last;
            cart.Distance += moved.Length();
            cart.Yaw = Mathf.LerpAngle(cart.Yaw, ActorAnimation.Yaw(moved, cart.Yaw), Math.Min(1, dt * 8));
            cart.Last = pos;
            cart.Root.Position = To3(pos);
            cart.Root.Visible = World.Sees(pos) && leg < 1;
            Fade(cart.Parts, cart.Root, CartFade(leg));
            cart.Root.Rotation = new Vector3(0, cart.Yaw, 0);
            cart.Roll(cart.Distance);
            // The courier walks with arms trailing back to the cart handle.
            var stride = ActorAnimation.PoseAt(ActorAction.Walk, ActorAnimation.Advance(ActorAction.Walk, 0, cart.Distance), true) with { RaiseL = -22, RaiseR = -22, OutL = 12, OutR = 12, ElbowL = 8, ElbowR = 8, Lean = 10 };
            cart.Courier.Show(stride, null, false);
            // A sale leaves with its crates and returns with the payment; a purchase leaves with the payment and returns with the goods.
            bool back = phase >= .5f;
            cart.Load(order.Buying == back ? order.Crates : 0, order.Buying && back, order.Buying != back);
        }
    }

    private WorldModels.CartModel NewCart() {
        var cart = new WorldModels.CartModel();
        cart.Root.Visible = false; scene.AddChild(cart.Root);
        return cart;
    }

    private void UpdateMarkers() {
        if (EnvironmentReview) {
            selectRing.Visible = hoverRing.Visible = workerRing.Visible = playerRing.Visible = placeTile.Visible = false;
            return;
        }
        // The pond is one thing all along its shore, so its selection ring shows only while the player collects water there.
        var selected = World.Find(Selection.Thing);
        if (selected?.Kind == ThingKind.Water && !(Gathering && GatheringTarget?.Kind == ThingKind.Water)) selected = null;
        Mark(selectRing, selected, 0);
        Mark(hoverRing, Hover != (selected?.Id ?? 0) ? World.Find(Hover) : null, .004f);
        Worker? worker = null;
        foreach (var w in World.Workers) if (w.Id == Selection.Worker) worker = w;
        workerRing.Visible = worker != null && World.Sees(worker);
        if (worker != null) { workerRing.Position = To3(worker.Pos, .02f); workerRing.Scale = new Vector3(.5f, 1, .5f); }
        playerRing.Visible = !World.Dead;
        playerRing.Position = To3(World.Player, .015f); playerRing.Scale = new Vector3(.42f, 1, .42f);
        placeTile.Visible = Placing != null;
        var kind = Placing.GetValueOrDefault();
        var at = Placing == null ? Vector2.Zero : World.Snap(kind, ToWorld(Mouse).Round());
        var shown = Placing == null ? null : kind == ThingKind.Mine ? mineGhosts[World.CliffAt(at).Ore] : ghosts[kind];
        if (shown != ghost) {
            if (ghost != null) ghost.Root.Visible = false;
            if (shown != null) shown.Root.Visible = true;
            ghost = shown;
        }
        if (shown == null) return;
        bool valid = World.PlacementBlock(kind, at) == null;
        if (shown.Tint != valid) { SetOverride(shown.Root, valid ? GhostValid : GhostBlocked); shown.Tint = valid; }
        shown.Root.Position = To3(at);
        if (kind == ThingKind.Mine) shown.Body.Rotation = WorldModels.MineYaw(at);
        placeTile.Position = To3(at, .02f);
        placeTile.MaterialOverride = valid ? GhostValid : GhostBlocked;
    }

    /// <summary>Builds a hidden, faded ghost of every building and of a crate, so picking one in build mode compiles nothing mid-game.</summary>
    private void BuildGhosts() {
        Prop Ghost(ThingKind kind, Vector2 at) {
            var built = WorldModels.Build(new Thing { Kind = kind, Pos = at });
            SetFade(built.Root, .35f);
            built.Root.Visible = false;
            scene.AddChild(built.Root);
            return built;
        }
        foreach (var kind in World.Data.Buildings.Keys) if (kind != ThingKind.Mine) ghosts[kind] = Ghost(kind, Vector2.Zero);
        ghosts[ThingKind.Crate] = Ghost(ThingKind.Crate, Vector2.Zero);
        foreach (var ore in World.MineOres) mineGhosts[ore] = Ghost(ThingKind.Mine, World.MineSpot(World.CliffOf(ore)));
    }

    private void Mark(MeshInstance3D ring, Thing? t, float lift) {
        ring.Visible = t != null;
        if (t == null) return;
        float smallest = float.MaxValue;
        foreach (var hit in HitsOf(t)) smallest = Math.Min(smallest, hit.Radius);
        float radius = Math.Min(1.7f, smallest + .25f);
        ring.Position = To3(t.Kind == ThingKind.Water ? Terrain.WaterPoint(ring == hoverRing ? ToWorld(GetViewport().GetMousePosition()) : World.Player) : t.Pos, .02f + lift);
        ring.Scale = new Vector3(radius, 1, radius);
    }

    // Strong sun against weak ambient light keeps shaded sides and shadows dark enough to show shapes.
    private const float DaySun = 1, DayAmbientEnergy = .22f;
    private static readonly Color NoonLight = new("ffeccc"), LowSunLight = new("ffb070"), MoonLight = new("8296d6");
    private static readonly Color DayAmbient = new("c7d3dc"), LowSunAmbient = new("d9c3ad"), NightAmbient = new("33447a");
    private static readonly Color DayGround = new("879c69"), NightGround = new("1b2733");
    /// <summary>The noon light's heading across the ground, from the upper left and slightly behind so shadows fall to the right.</summary>
    private static readonly Vector2 NoonHeading = new Vector2(1, -.45f).Normalized();
    /// <summary>Full moonlight is faint, so night stays dark with barely a shadow.</summary>
    private const float MoonEnergy = .05f;
    /// <summary>Night's ambient light without a moon: dark enough that fires and torches matter.</summary>
    private const float NewMoonAmbient = .1f;

    /// <summary>The sky light last written.</summary>
    private Color skyAmbient;

    private void UpdateLighting() {
        // The sun rises at dawn and sets halfway through dusk, then the moon takes the same path until dawn.
        // Each fades out near the horizon, so the hand-over at sunset moves no visible shadow.
        float dayPhase = World.DayPhase, dusk = World.Data.DuskStarts, sunset = World.Data.Sunset;
        // Dusk deepens into full night soon after sunset and dawn fades out, so neither boundary jumps in brightness.
        night = dayPhase >= dusk ? Mathf.SmoothStep(dusk, sunset + .02f, dayPhase) : dayPhase < .05f ? 1 - dayPhase / .05f : 0;
        bool moon = World.MoonUp;
        float arc = World.SkyArc;
        float height = MathF.Sin(MathF.PI * arc);
        // The light turns a little every frame; the engine redraws the sun's shadows every frame anyway.
        float swing = Mathf.DegToRad(Mathf.Lerp(-75, 75, arc)), elevation = Mathf.DegToRad(Mathf.Lerp(18, moon ? 35 : 40, height));
        var heading = NoonHeading.Rotated(swing);
        sun.Basis = Basis.LookingAt(new Vector3(heading.X * MathF.Cos(elevation), -MathF.Sin(elevation), heading.Y * MathF.Cos(elevation)), Vector3.Up);
        // The moon waxes and wanes over the month: a full moon lights the night sky, a new moon leaves only firelight to see by.
        float moonlight = World.Moonlight;
        sun.LightEnergy = (moon ? MoonEnergy * moonlight : Mathf.Lerp(DaySun, .08f, night)) * Mathf.SmoothStep(0, .25f, height);
        // A low sun gives warm light at sunrise and sunset.
        float low = moon ? 0 : 1 - Mathf.SmoothStep(.1f, .45f, height);
        sun.LightColor = NoonLight.Lerp(LowSunLight, low).Lerp(MoonLight, night);
        var ambient = DayAmbient.Lerp(LowSunAmbient, low * .6f).Lerp(NightAmbient, night);
        // Sky light from above, and below it that light bounced off the grass; each write rebuilds the sky's light, so a steady sky gets none.
        if (ambient != skyAmbient) {
            skyAmbient = ambient;
            var bounce = ambient * DayGround;
            skylight.SkyTopColor = ambient; skylight.SkyHorizonColor = ambient;
            skylight.GroundHorizonColor = bounce; skylight.GroundBottomColor = bounce;
        }
        environment.AmbientLightEnergy = Mathf.Lerp(DayAmbientEnergy, Mathf.Lerp(NewMoonAmbient, .3f, moonlight), night);
        environment.BackgroundColor = DayGround.Lerp(NightGround, night);
    }

    // ---------- 2D overlay: names, progress and forge heat ----------

    private readonly StyleBoxFlat tagBox = new() { BgColor = new Color(.12f, .18f, .15f, .86f), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 };
    private void Tag(Vector2 at, string text) {
        var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 14);
        overlay.DrawStyleBox(tagBox, new Rect2(at - new Vector2(size.X / 2 + 6, 18), size + new Vector2(12, 6)));
        overlay.DrawString(font, at - new Vector2(size.X / 2, 0), text, HorizontalAlignment.Left, -1, 14, TagText);
    }

    private void PaintOverlay() {
        if (EnvironmentReview) return;
        if (World == null) return;
        if (AnimationReview) { PaintReview(); return; }
        foreach (var kind in World.StationKinds)
            foreach (var t in World.ThingsOf(kind)) {
                if (!t.Processing || !World.Sees(t.Pos) || !World.Data.Recipes.TryGetValue(t.Recipe, out var r)) continue;
                var p = Screen(t.Pos);
                overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56, 5)), BarTrack);
                overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56 * Math.Min(1, t.Progress / r.Seconds), 5)), BarFill);
            }
        // Forge heat under each reinforced furnace, with a tick at the clear bar steel needs.
        foreach (var t in World.ThingsOf(ThingKind.Reinforced)) {
            if (!World.Sees(t.Pos)) continue;
            var p = Screen(t.Pos) + new Vector2(-28, 18);
            overlay.DrawRect(new Rect2(p, new Vector2(56, 5)), BarTrack);
            overlay.DrawRect(new Rect2(p, new Vector2(56 * Math.Clamp(t.Heat / 100, 0, 1), 5)), t.Heat >= World.SteelHeat ? HeatReady : HeatLow);
            overlay.DrawLine(p + new Vector2(56 * World.SteelHeat / 100, -2), p + new Vector2(56 * World.SteelHeat / 100, 7), HeatTick, 2);
        }
        // Only the hovered object is named; the side panel already names the selection.
        var hovered = World.Find(Hover);
        if (hovered != null) {
            float top = 0;
            foreach (var hit in HitsOf(hovered)) top = Math.Max(top, hit.Top);
            Tag(Screen(hovered.Kind == ThingKind.Water ? Terrain.WaterPoint(ToWorld(GetViewport().GetMousePosition())) : hovered.Pos, top + .2f) - new Vector2(0, 8), hovered.Kind == ThingKind.Water ? "Collect water" : World.Data.Name(hovered.Kind));
        }
        foreach (var w in World.Workers)
            if (World.Sees(w) && (w.Id == HoverWorker || w.Id == Selection.Worker)) Tag(Screen(w.Pos, 2.05f), w.Name);
    }
}

/// <summary>Draws the 2D overlay by overriding <c>_Draw</c>: a C# handler on the Draw signal allocates on every emission.</summary>
public partial class Overlay : Node2D {
    public Action Paint = () => { };
    public override void _Draw() => Paint();
}
