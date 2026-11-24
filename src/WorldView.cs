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
    /// <summary>What the remove selector points at, which shows red when it is in reach.</summary>
    public Removal Removal;
    public int HoverWorker;
    /// <summary>The building being placed, or a packed crate, or null when not placing.</summary>
    public ThingKind? Placing;
    public float PlacingRotation;
    public PipeShape PlacingPipeShape;
    public bool Underground;
    public Vector2I? PointedPipe;
    public Vector2I? PipePreviewTile;
    private PipeLayerView pipeLayer = null!;
    private readonly List<Vector3> surfacePeople = new();
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
    /// <summary>A fixed daylight landscape view with the lake and sea waves held at one moment, for <c>--environment-review</c>; set before the view enters the tree.</summary>
    public bool EnvironmentReview;
    public bool EnvironmentReviewFog;
    /// <summary>Whether to hide the world markers with the interface in a showcase.</summary>
    public bool HideMarkers { get; init; }
    public Vector2? EnvironmentCenter;
    public float EnvironmentCameraDistance = 60;
    public float Elapsed;
    public bool PanelOpen;
    public Sounds? Sounds;
    // A 30° pitch keeps the old 2:1 isometric footprint; this ortho height matches the old 85 px actor scale at zoom 1.
    private const float Pitch = 30, BaseSize = 14.5f, OverheadFar = 200;
    /// <summary>Degrees the camera is turned round the player from where the game puts it, and its pitch in place of the game's, for a review from another side.</summary>
    public float CameraTurn;
    /// <summary>A spot the camera keeps on in place of the player, for a review scene whose subject is away from them.</summary>
    public Vector2? CameraOn;
    public float? CameraPitch;
    private Camera3D camera = null!;
    private readonly FirstPersonCamera firstPerson = new();
    public bool FirstPersonPreview, MouseLook;
    /// <summary>Cabin scenes, downed players and underground pipe work keep the overhead camera.</summary>
    public bool FirstPerson => FirstPersonPreview && !Warming && !Underground && !World.Downed && World.Sheltered == 0;
    public Vector2 FirstPersonForward => firstPerson.Forward;
    public Vector2 FirstPersonMove(Vector2 keys) => firstPerson.GroundDirection(keys);
    public void Look(Vector2 motion) => firstPerson.Look(motion);
    private DirectionalLight3D sun = null!;
    private TerrainView terrainView = null!;
    private Godot.Environment environment = null!;
    /// <summary>Never drawn: its light from above and bounce from below is the ambient light, so a roof and a wall differ in shade.</summary>
    private ProceduralSkyMaterial skylight = null!;
    private Sky overheadSky = null!;
    private FirstPersonSky? previewSky;
    private Node3D scene = null!;
    private Overlay overlay = null!;
    private readonly Dictionary<int, Prop> props = new();
    private readonly StrikePoints strikes = new();
    private readonly ActorState player = new();
    private (float PullL, float PullR) playerArmPull;
    private ActorRig playerRig = null!;
    private string playerShirt = "";
    /// <summary>The other players in the game, keyed by their id.</summary>
    private readonly Dictionary<int, Avatar> others = new();
    /// <summary>Draws players at cabin doors.</summary>
    private readonly DoorwayView doorways;
    /// <summary>What a player inside a cabin sees of a wolf forcing its door, in a window of the interface.</summary>
    public InsideDoorPOV InsideDoorPOV { get; } = new();
    /// <summary>Draws wolves tugging players out of cabin doorways.</summary>
    private readonly TugView tugs;
    /// <summary>Where wolves stand round a fallen player.</summary>
    private readonly FeedingSpots feeding;

    public WorldView() {
        doorways = new DoorwayView(() => World, StateOf);
        tugs = new TugView(() => World, StateOf, DrawsAt);
        feeding = new FeedingSpots(wolves);
    }
    // The host keeps the original teal; each guest keeps the first one free when it joined.
    internal static readonly string[] Shirts = { "2f7f86", "a8473a", "7a5c9e", "c9a23b" };
    private readonly Dictionary<int, (ActorRig Rig, ActorState State, LocalClock WaterClock, (MeshInstance3D Mesh, Material Material)[] Parts)> crew = new();
    private readonly Dictionary<Wolf, WolfModel> wolves = new();
    private readonly Dictionary<TradeOrder, WorldModels.CartModel> carts = new();
    /// <summary>The village's carts no order holds, built while loading and reused for every trip.</summary>
    private readonly Stack<WorldModels.CartModel> idleCarts = new();
    private readonly Dictionary<int, float> pumping = new();
    private readonly List<(ActorRig Rig, ActorState Actor, Thing Pump)> pumpOperators = new();
    private readonly Dictionary<int, float> openBungs = new();
    /// <summary>The cooking tables whose mortar is in someone's hands this frame, so it is off the counter.</summary>
    private readonly HashSet<int> grinding = new();
    /// <summary>The stations whose slotted hammer is in someone's hands this frame, so it is off the station's top.</summary>
    private readonly HashSet<int> swung = new();
    /// <summary>Build-mode ghosts by building kind, made hidden while loading; mines have one per cliff ore instead.</summary>
    private readonly Dictionary<ThingKind, Prop> ghosts = new(), mineGhosts = new();
    private Prop? ghost;
    private MeshInstance3D playerRing = null!, placeTile = null!;
    /// <summary>The red on what the remove selector points at.</summary>
    private RemovalMark removalMark = null!;
    private float pulseRemaining;
    private ActorAction pulseAction;
    private Vector2 pulseTarget;
    private float night;
    private float downedFor;
    private Vector2 mauledFrom;
    private readonly Font font = ThemeDB.FallbackFont;
    private static readonly StandardMaterial3D GhostValid = PlacementTint(new Color(.55f, .95f, .45f, .45f));
    private static readonly StandardMaterial3D GhostBlocked = PlacementTint(new Color(1, .35f, .3f, .5f));
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
    private readonly ShaderMaterial fog = new() { Shader = new Shader { Code = FogShader }, RenderPriority = (int)Material.RenderPriorityMax - 1 };
    private MeshInstance3D fogCover = null!;
    private Image explored = null!;
    private ImageTexture exploredTexture = null!;
    private int exploredRevision = -1;
    // Covers the world beneath placement previews: black where never explored, a grey wash over remembered ground, clear inside the sight circle.
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

    internal static Vector3 To3(Vector2 p, float y = 0) => new(p.X, y, p.Y);

    public override void _Ready() {
        World.Replaced += () => { worldReplaced = true; Footpaths.Clear(); };
        camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = BaseSize, Near = 1, Far = OverheadFar, Current = true };
        AddChild(camera);
        // Half energy puts the sky's light at DayAmbientEnergy.
        skylight = new ProceduralSkyMaterial { SkyEnergyMultiplier = .5f, GroundEnergyMultiplier = .5f };
        // Two flat colours need no detail in the sky's light map.
        overheadSky = new Sky { SkyMaterial = skylight, RadianceSize = Sky.RadianceSizeEnum.Size64 };
        environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("879c69"),
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            Sky = overheadSky,
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
        sun = new DirectionalLight3D {
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 90,
            // A smaller offset keeps the shadow attached to feet and low props within the wide shadow map.
            ShadowBias = .018f,
            ShadowNormalBias = .7f,
            ShadowBlur = 1,
            LightEnergy = DaySun,
            SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly,
        };
        AddChild(sun);
        scene = new Node3D(); AddChild(scene);
        explored = Image.CreateFromData(Terrain.Size, Terrain.Size, false, Image.Format.L8, World.S.Explored);
        exploredTexture = ImageTexture.CreateFromImage(explored);
        fog.SetShaderParameter("explored", exploredTexture);
        fog.SetShaderParameter("map_size", Terrain.Size);
        // A screen-sized quad on the camera; its shader places it over the whole view, so it is never culled and casts no shadow.
        fogCover = new MeshInstance3D {
            Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            MaterialOverride = fog,
            Position = new Vector3(0, 0, -2),
            ExtraCullMargin = 16384,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        camera.AddChild(fogCover);
        BuildGround(scene);
        BuildLook();
        BuildEffects(scene);
        PileShape.Warm(World.Data);
        DoorCheckView.Warm();
        warmStage = new Node3D(); WorldModels.Warm(warmStage); PileShape.Stage(warmStage, World.Data); LakeDressing.Warm(warmStage); scene.AddChild(warmStage);
        BuildGhosts();
        for (int i = 0; i < World.Carts; i++) idleCarts.Push(NewCart());
        playerShirt = Shirts[0];
        playerRig = MakePlayerRig();
        playerRing = Torus(scene, .9f, 1, Vector3.Zero, Flat(new Color(.95f, .88f, .57f, .6f)));
        playerRing.Scale = new Vector3(.42f, 1, .42f);
        playerRing.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        playerRing.Visible = false;
        placeTile = Add(scene, new PlaneMesh { Size = new Vector2(1.4f, 1.4f) }, GhostValid, Vector3.Zero);
        placeTile.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        placeTile.Visible = false;
        removalMark = new RemovalMark(scene, id => props.GetValueOrDefault(id)?.Root);
        RemovalMark.Warm(warmStage);
        player.Position = World.Player;
        pipeLayer = new PipeLayerView(); AddChild(pipeLayer);
        overlay = new Overlay { Paint = PaintOverlay }; AddChild(overlay);
        World.PlayerActed += PlayerActed;
    }

    private ActorRig MakePlayerRig() {
        var rig = new ActorRig(ActorSkin.Player, playerShirt);
        FirstPersonCamera.SetHeadLayer(rig.Head);
        scene.AddChild(rig.Root);
        return rig;
    }

    public override void _ExitTree() => World.PlayerActed -= PlayerActed;

    // Made once: passing a plain string converts it to a new StringName on every call.
    private static readonly StringName FogPlayer = "player", FogRadius = "radius", FogMemory = "memory";

    private void UpdateFog() {
        if (FirstPerson) return;
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

    /// <summary>
    /// Plays an action on the player who made it, here or at another machine.
    /// A swing carries its fixed aim, and other actions carry a position.
    /// </summary>
    public void Acted(int id, ActorAction action, Vector2 target) {
        if (id == World.Me.Id)
            StartPulse(player, action, target, ref pulseAction, ref pulseTarget, ref pulseRemaining);
        else if (others.TryGetValue(id, out var other))
            StartPulse(other.State, action, target, ref other.Pulse, ref other.PulseTarget, ref other.PulseRemaining);
    }

    private static void StartPulse(ActorState actor, ActorAction action, Vector2 target,
        ref ActorAction pulse, ref Vector2 pulseAt, ref float remaining) {
        float duration = ActorAnimation.Duration(action);
        if (remaining > 0 && action == ActorAction.Pump && pulse == action && pulseAt == target && actor.Action == ActorAction.PumpWater) {
            // Queue one more visible stroke without rewinding the current one or building up a backlog.
            remaining = Math.Max(remaining, duration * (2 - ActorAnimation.Phase(actor.Action, actor.Time)));
            return;
        }
        (pulse, pulseAt, remaining) = (action, target, duration);
        actor.Time = 0;
    }

    private sealed class Avatar(ActorRig rig, string shirt) {
        public readonly ActorRig Rig = rig;
        public readonly string Shirt = shirt;
        public readonly ActorState State = new();
        public (float PullL, float PullR) ArmPull;
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
    /// <summary>The window's own player's rig, as last posed.</summary>
    public ActorRig PlayerRig => playerRig;
    /// <summary>Where the window's own player's blow lands, or null when it lands on nothing.</summary>
    public Vector3? PlayerMark => player.Mark;
    public ActorAction OtherAnimation(int id) => others.TryGetValue(id, out var other) ? other.State.Action : ActorAction.Idle;
    public ActorAction WorkerAnimation(int id) => crew.TryGetValue(id, out var entry) ? entry.State.Action : ActorAction.Idle;
    /// <summary>The tool a worker's figure shows in hand, or null for none.</summary>
    public string? WorkerTool(int id) => crew.TryGetValue(id, out var entry) ? entry.Rig.ToolShown : null;

    // ---------- Camera and picking ----------

    public Vector2 Center => GetViewport().GetVisibleRect().Size * new Vector2(PanelOpen ? .38f : .5f, .52f);

    /// <summary>Keeps the camera on the player; called before any projection so input sees the current frame.</summary>
    private void SyncCamera() {
        if (FirstPerson) {
            if (camera.Projection != Camera3D.ProjectionType.Perspective) {
                SetFirstPersonBackground(true);
                foreach (var prop in props.Values) {
                    if (prop is not TreeProp tree || tree.Fade == 0) continue;
                    tree.Fade = 0; SetFade(tree.SwayParts, 0);
                }
            }
            firstPerson.Frame(camera, World.Player);
            return;
        }
        if (camera.Projection != Camera3D.ProjectionType.Orthogonal) {
            camera.SetOrthogonal(BaseSize, 1, OverheadFar);
            camera.CullMask |= FirstPersonCamera.HeadLayer;
            SetFirstPersonBackground(false);
        }
        var size = GetViewport().GetVisibleRect().Size;
        camera.Size = (AnimationReview ? (ReviewWolfClip >= 0 ? 3.2f : ReviewClip >= 0 ? 2.6f : WaterReview != null ? 3.4f : 11) / ReviewZoom : BaseSize) / Zoom;
        float pitch = Mathf.DegToRad(AnimationReview ? ReviewPitch : CameraPitch ?? Pitch), across = MathF.Cos(pitch) * Mathf.Sqrt2 / 2;
        float turn = AnimationReview && WaterReview != null ? CameraTurn - ReviewTurn : CameraTurn;
        var back = new Vector3(across, MathF.Sin(pitch), across).Rotated(Vector3.Up, Mathf.DegToRad(turn));
        var target = AnimationReview ? ReviewCenter + To3(reviewActor.Position, WaterReview != null ? reviewActor.Lift : 0)
            : To3(EnvironmentReview ? EnvironmentCenter ?? World.Player : CameraOn ?? World.Player);
        camera.Basis = Basis.LookingAt(-back, Vector3.Up);
        camera.Position = target + back * (EnvironmentReview ? EnvironmentCameraDistance : 60);
        if (EnvironmentReview && EnvironmentCameraDistance > 60) sun.DirectionalShadowMaxDistance = EnvironmentCameraDistance + 40;
        float unitsPerPixel = camera.Size / Math.Max(1, size.Y);
        camera.HOffset = (.5f - Center.X / Math.Max(1, size.X)) * size.X * unitsPerPixel;
        camera.VOffset = (Center.Y / Math.Max(1, size.Y) - .5f) * size.Y * unitsPerPixel;
    }

    private void SetFirstPersonBackground(bool show) {
        if (show) previewSky ??= new FirstPersonSky(this);
        previewSky?.Show(show);
        environment.Sky = show ? previewSky!.Background : overheadSky;
        environment.BackgroundMode = show ? Godot.Environment.BGMode.Sky : Godot.Environment.BGMode.Color;
        fogCover.Visible = !show;
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

    public Vector2 ToWorld(Vector2 screen, float height = 0) {
        TryGroundPoint(screen, out var at, height);
        return at;
    }

    /// <summary>Where the ray meets the ground. Aiming up still gives tools a direction to swing, but no ground to click.</summary>
    public bool TryGroundPoint(Vector2 screen, out Vector2 at, float height = 0) {
        SyncCamera();
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        return GroundPoint(origin, direction, height, out at);
    }

    private bool GroundPoint(Vector3 origin, Vector3 direction, float height, out Vector2 at) {
        if (Math.Abs(direction.Y) >= 1e-5f) {
            float distance = (height - origin.Y) / direction.Y;
            var hit = origin + direction * distance;
            at = new Vector2(hit.X, hit.Z);
            if (!FirstPerson || distance > 0 && World.Sees(at)) return true;
        }
        at = FirstPerson ? World.Player + new Vector2(direction.X, direction.Z).Normalized() * World.Data.SightRadius : World.Player;
        return !FirstPerson;
    }

    private Hit[] HitsOf(Thing t) => props.TryGetValue(t.Id, out var prop) && prop.Kind == t.Kind ? prop.Hits : WorldModels.Hits(t);

    private static bool RayCylinder(Vector3 origin, Vector3 direction, Vector2 center, Hit hit, out float distance) {
        distance = float.MaxValue;
        float r2 = hit.Radius * hit.Radius;
        if (Math.Abs(direction.Y) > 1e-5f) {
            float face = direction.Y < 0 ? hit.Top : hit.Bottom;
            float cap = (face - origin.Y) / direction.Y;
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

    private bool RayFarm(Vector3 origin, Vector3 direction, Thing farm, out float distance) {
        origin -= To3(farm.Pos);
        RayBox(origin, direction, Farms.HalfSize, .16f, out distance);
        float height = World.Farms.CropLook(farm) switch { 2 => .23f, 3 => .62f, 4 => .95f, _ => 0 };
        if (height > 0 && RayBox(origin, direction, 2.6f, height, out float crop)) distance = Math.Min(distance, crop);
        return distance < float.MaxValue;
    }

    /// <summary>Picks the moving hardware with three pixels of clearance around narrow rods and pins.</summary>
    private bool RayPump(Vector3 origin, Vector3 direction, Thing pump, WaterPumpShape model, out float distance) {
        distance = float.MaxValue;
        // A pump and its fully extended lever fit inside this cylinder at every turn and stroke.
        if (!RayCylinder(origin, direction, pump.Pos, new Hit(2.5f, -.3f, 2.1f), out _)) return false;
        float ppu = PixelsPerUnit;
        if (FirstPerson) {
            float halfDepth = Mathf.Tan(Mathf.DegToRad(camera.Fov / 2)) * Mathf.Max(camera.Near, origin.DistanceTo(To3(pump.Pos)));
            ppu = GetViewport().GetVisibleRect().Size.Y / (2 * halfDepth);
        }
        float clearance = 3 / ppu;
        foreach (var (part, bounds) in model.Picking) {
            var into = part.GlobalTransform.AffineInverse();
            if (RayBox(into * origin, into.Basis * direction, bounds.Grow(clearance), out float hit))
                distance = Math.Min(distance, hit);
        }
        return distance < float.MaxValue;
    }

    private static bool RayBox(Vector3 origin, Vector3 direction, float half, float height, out float distance) =>
        RayBox(origin, direction, new Aabb(new Vector3(-half, 0, -half), new Vector3(2 * half, height, 2 * half)), out distance);

    /// <summary>Finds the point where a ray enters a box in its own frame.</summary>
    private static bool RayBox(Vector3 origin, Vector3 direction, Aabb bounds, out float distance) {
        var low = bounds.Position; var high = bounds.End;
        float enter = 0, exit = float.MaxValue;
        bool hit = Clip(origin.X, direction.X, low.X, high.X, ref enter, ref exit) &&
            Clip(origin.Z, direction.Z, low.Z, high.Z, ref enter, ref exit) &&
            Clip(origin.Y, direction.Y, low.Y, high.Y, ref enter, ref exit);
        distance = hit ? enter : float.MaxValue;
        return hit;

        static bool Clip(float start, float step, float low, float high, ref float enter, ref float exit) {
            if (Math.Abs(step) < 1e-5f) return start >= low && start <= high;
            float a = (low - start) / step, b = (high - start) / step;
            enter = Math.Max(enter, Math.Min(a, b));
            exit = Math.Min(exit, Math.Max(a, b));
            return enter <= exit;
        }
    }

    private readonly List<Thing> picking = new();
    private const float StumpHeight = 1.3f;
    /// <summary>The visible object nearest the camera under the cursor, tested against the models' hit shapes.</summary>
    public Thing? Pick(Vector2 screen) => Pick(screen, out _);

    internal Thing? Pick(Vector2 screen, out float nearest) {
        SyncCamera();
        // One ray for the whole pick: asking the camera again for every thing is most of what a pick costs.
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        Thing? best = null; nearest = float.MaxValue;
        // Hits follow what is drawn, including memories, but only things that still exist can be picked.
        World.CollectKnown(picking);
        bool firstPerson = FirstPerson;
        foreach (var drawn in picking) {
            if (drawn.Remaining <= 0 && drawn.IsResource && drawn.Kind != ThingKind.Berries || World.Find(drawn.Id) is not Thing t) continue;
            // The first-person view shows the valley, but its pointer works on nearby live objects.
            if (firstPerson && drawn != t) continue;
            // The lake has no raised model. Its shore is picked behind the objects standing there.
            if (t.Kind == ThingKind.Water) continue;
            var hits = HitsOf(drawn);
            // Only the foot of a tree's trunk, its first hit, picks the tree, so a click among the crowns of a forest reaches the ground and swings.
            if (t.Kind == ThingKind.Tree) {
                if (hits.Length == 0) continue;
                var trunk = firstPerson ? hits[0] : hits[0] with { Top = Math.Min(hits[0].Top, StumpHeight) };
                if (RayCylinder(origin, direction, t.Pos, trunk, out float s) && s < nearest) { nearest = s; best = t; }
                continue;
            }
            bool hit = t.Kind == ThingKind.Farm ? RayFarm(origin, direction, drawn, out float d)
                : t.Kind == ThingKind.Pump && PropOf(drawn.Id) is WaterPumpProp pump
                    ? RayPump(origin, direction, drawn, pump.Pump, out d)
                    : RayHits(origin, direction, t.Pos, hits, out d);
            if (hit && d < nearest) { nearest = d; best = t; }
        }
        if (best != null) return best;
        if (GroundPoint(origin, direction, 0, out var point) && Terrain.WaterRing(point) && World.Sees(point)) {
            foreach (var thing in World.ThingsOf(ThingKind.Water)) {
                nearest = origin.DistanceTo(To3(point));
                return thing;
            }
        }
        return null;
    }

    private static readonly Hit[] PersonHit = { new(.38f, 0, 1.75f) };
    public Worker? PickWorker(Vector2 screen, float thingDistance = float.MaxValue) {
        SyncCamera();
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        // Overhead clicks prefer workers. From eye level the object in front takes the click.
        Worker? best = null; float nearest = FirstPerson ? thingDistance : float.MaxValue;
        foreach (var w in World.Workers) {
            if (!World.Sees(w)) continue;
            var feet = WorkerFeet(w);
            if (RayHits(origin - Vector3.Up * feet.Y, direction, new Vector2(feet.X, feet.Z), PersonHit, out float d) && d < nearest) {
                nearest = d; best = w;
            }
        }
        return best;
    }

    private Vector3 WorkerFeet(Worker worker) =>
        crew.TryGetValue(worker.Id, out var entry) ? entry.Rig.Root.Position : To3(worker.Pos);

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
        float dt = Paused ? 0 : Math.Min((float)delta, .1f);
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
        SyncInsideDoorPOV();
        worldReplaced = false;
        UpdateEffects(dt);
        UpdateMarkers();
        if (Underground) {
            surfacePeople.Clear();
            AddFooting(playerRig);
            foreach (var entry in crew.Values) AddFooting(entry.Rig);
            foreach (var other in others.Values) AddFooting(other.Rig);
        }
        pipeLayer.Update(World, known, surfacePeople, camera, Underground, PipePreviewTile,
            PlacingRotation, PlacingPipeShape, removalMark.Shown.Pipe);
        // The sun and the light move every frame by the window's own clock, which each server update corrects.
        UpdateLighting(Clocked(dayClock, World.Time, true, dt));
        UpdateLook(dt);
        UpdateFog();
        overlay.QueueRedraw();
    }

    /// <summary>
    /// The hammer model for a hammering action: the grade in hand, carried or slotted in the worked station, and steel
    /// for someone with none.
    /// </summary>
    private static string? Grade(string? tool, string? held, Inventory? carried) =>
        tool != "hammer" ? tool
        : held is "wood_hammer" or "steel_hammer" ? held
        : carried != null && carried["wood_hammer"] > 0 && carried["steel_hammer"] == 0 ? "wood_hammer" : "steel_hammer";

    private static string? HeldTool(string? selected, Inventory backpack) => selected switch {
        "steel_axe" or "steel_pick" when backpack[selected] > 0 => selected,
        "wood_hammer" or "steel_hammer" when backpack[selected] > 0 => selected,
        _ => null
    };

    private void AddFooting(ActorRig rig) {
        if (rig.Root.Visible) surfacePeople.Add(rig.Root.GlobalPosition);
    }

    private void UpdateActors(float dt) {
        pumping.Clear(); pumpOperators.Clear(); grinding.Clear(); swung.Clear(); openBungs.Clear();
        if (ShirtOf(World.Me) != playerShirt) {
            playerShirt = ShirtOf(World.Me); playerRig.Root.QueueFree();
            playerRig = MakePlayerRig();
        }
        bool moving = World.Player.DistanceSquaredTo(player.Position) > .000001f;
        ActorAction action = ActorAnimation.PlayerAction(GatheringTarget?.Kind, Gathering, moving, World.S.Backpack, World.Me.Sprinting, World.CraftRate > 0, Fitting(GatheringTarget));
        Vector2 target = Gathering && GatheringTarget != null ? World.GatherAt(GatheringTarget, World.Player) : World.Player;
        Thing? subject = Gathering ? GatheringTarget : null;
        if (pulseRemaining > 0 && (!moving || ActorAnimation.SwingsOnTheMove(pulseAction))) {
            action = pulseAction; target = pulseTarget; subject = ActorAnimation.SwingsOnTheMove(action) ? null : ThingAt(target);
        }
        Thing? worked = action == ActorAction.Idle ? StationToHammer(World.Me) : null;
        if (worked != null) { action = ActorAnimation.StationAction(worked, World.Data); target = worked.Pos; subject = worked; }
        if (World.Me.Braking && moving && !ActorAnimation.SwingsOnTheMove(action)) action = ActorAction.Brake;
        if (World.Mauled) {
            bool fallsBack = FallsBack(World.Me.Id);
            mauledFrom = PinningWolf(World.Me.Id)?.Pos ?? (downedFor == 0 ? World.Player + Vector2.Down : mauledFrom);
            // Facing the wolf, the player goes over backwards with it on top; one yanked out of a doorway falls back still facing the door.
            action = World.Dead ? ActorAction.Devoured : downedFor < ActorAnimation.FallBackSeconds ? ActorAction.FallBack : ActorAction.Mauled;
            target = fallsBack ? World.Player - World.Doorways.OutOfDoorAt(World.Me.DoorCabin) : mauledFrom; subject = null;
            downedFor += dt;
        } else downedFor = 0;
        var at = World.Player;
        var door = doorways.AtDoor(World.Me, player, dt);
        if (door is DoorShown shown) { at = shown.At; target = shown.Facing; action = shown.Action; subject = null; }
        action = WorkPose(action, subject);
        player.Update(at, action, target, dt, door?.Backing ?? false, ActorAnimation.SwingsOnTheMove(action) ? target : null);
        if (FirstPerson && door == null && action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run or ActorAction.Brake) {
            // In first person the body faces the view, including while walking sideways or backward.
            player.Yaw = ActorAnimation.Yaw(FirstPersonForward, player.Yaw);
            player.Turning.Hold(player.Yaw);
        }
        player.Mark = Mark(subject, player, action);
        // A foot lands at each extreme of the stride.
        if (action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run && (player.Struck(.25f) || player.Struck(.75f))) Sounds?.Play(Footsteps.Sound(World, World.Player));
        pulseRemaining = Math.Max(0, pulseRemaining - dt);
        string? tool = ActorAnimation.Tool(action) ?? (Carries(player) ? HeldTool(SelectedTool, World.S.Backpack) : null);
        bool torch = World.Torch > 0 && !World.Dead;
        PosePlayer(playerRig, player, World.Me, ref playerArmPull, downedFor, Grade(tool, StationHammer(tool, worked, World.Me) ?? SelectedTool, World.S.Backpack), torch, door, dt);
        if (SwingsSlottedHammer(tool, worked, World.Me)) swung.Add(worked!.Id);
        Torch(playerRig, player, World.TorchLit, World.TorchFlare, null);
        playerRig.Root.Visible = (!World.Dead || World.Mauled) && World.Sheltered == 0;
        Work(player, subject, playerRig);
        UpdateOthers(dt);
        foreach (var worker in World.Workers) {
            if (!crew.TryGetValue(worker.Id, out var entry)) {
                var rig = new ActorRig(ActorSkin.Worker);
                // Gathered and warmed now, so the first walk home at dusk makes no faded materials.
                entry = (rig, new ActorState { Position = worker.Pos }, new LocalClock(.25f), FadeParts(rig.Root));
                scene.AddChild(entry.Rig.Root); crew.Add(worker.Id, entry);
            }
            var state = entry.State;
            bool walking = worker.Pos.DistanceSquaredTo(state.Position) > .000001f;
            var assignment = World.Find(worker.Assignment);
            var work = ActorAnimation.WorkerAction(worker, assignment, walking, World.Data);
            if (work is ActorAction.Hammer or ActorAction.Pack or ActorAction.Grind && assignment?.Processing != true && assignment?.Kind != ThingKind.Mine) work = ActorAction.Idle;
            // A protester turns the sign toward the camera.
            var facing = work == ActorAction.Protest ? worker.Pos + Vector2.One
                : work is ActorAction.Sit or ActorAction.Perch ? worker.Pos + Vector2.FromAngle(worker.LunchHeading) : work == ActorAction.Pack && assignment?.Kind == ThingKind.Packer ? worker.Pos + Vector2.Down.Rotated(assignment.Rotation)
                : assignment is { Kind: ThingKind.Mine } mine ? Mines.Facing(mine, worker.Pos) : assignment?.Pos ?? worker.Pos;
            bool sameAction = state.Action == work;
            state.Update(worker.Pos, work, facing, dt);
            state.Mark = Mark(assignment, state, work);
            string? vessel = BarrelHaul.Vessel(worker.Tool) ? worker.Tool : null;
            var waterTarget = worker.WaterAction is WorkerWaterAction.None or WorkerWaterAction.WalkFarm ? null : World.Find(worker.WaterTarget);
            if (waterTarget != null && vessel != null) {
                var point = waterTarget.Kind switch {
                    ThingKind.Barrel when worker.WaterAction == WorkerWaterAction.Fill => To3(waterTarget.Pos) + VesselShape.BarrelSpout.Rotated(Vector3.Up, -waterTarget.Rotation),
                    ThingKind.Barrel => To3(waterTarget.Pos) + VesselShape.BarrelFillOpening.Rotated(Vector3.Up, -waterTarget.Rotation),
                    ThingKind.Farm => To3(BarrelHaul.FarmPourPoint(waterTarget, worker.FarmWaterStop)) + Vector3.Up * .03f,
                    ThingKind.Water => To3(Terrain.WaterPoint(worker.Pos) - (worker.Pos - Terrain.Lake).Normalized() * .45f),
                    _ => To3(waterTarget.Pos)
                };
                float progress = Math.Clamp(Clocked(entry.WaterClock, worker.WaterProgress, sameAction, dt), 0, 1);
                bool watering = waterTarget.Kind == ThingKind.Farm;
                WorkerWaterView.Show(entry.Rig, worker.Pos, point, worker.WaterAction, waterTarget.Kind == ThingKind.Hut, progress, vessel,
                    ground: watering, facing: watering ? state.Yaw : null, rotation: waterTarget.Rotation);
                if (waterTarget.Kind == ThingKind.Barrel && worker.WaterAction == WorkerWaterAction.Pour) {
                    float opening = Mathf.SmoothStep(0, .28f, progress) * (1 - Mathf.SmoothStep(.73f, 1, progress));
                    openBungs[waterTarget.Id] = Math.Max(openBungs.GetValueOrDefault(waterTarget.Id), opening);
                }
            } else {
                // A worker works a station with the hammer slotted there, never one of their own, so that is the one they swing.
                string? slotted = ActorAnimation.Tool(work) == "hammer" && assignment?.Tools != null ? World.BenchTool(assignment, "hammer") : null;
                Pose(entry.Rig, state, Grade(ActorAnimation.Tool(work), slotted ?? worker.Tool, assignment?.Tools),
                    barrel: work == ActorAction.Carry && Barrel.In(worker.Bag) != null,
                    vessel: work is ActorAction.Idle or ActorAction.Walk or ActorAction.Carry ? vessel : null);
                if (slotted != null) swung.Add(assignment!.Id);
            }
            entry.Rig.Root.Visible = DrawsWorker(worker);
            Fade(entry.Parts, entry.Rig.Root, TripFade(World.TripLeg(worker)));
            Work(state, work is ActorAction.Idle or ActorAction.Walk or ActorAction.Carry or ActorAction.Sit or ActorAction.Perch or ActorAction.Protest
                or ActorAction.ClimbLadder or ActorAction.PourWater or ActorAction.FillVessel ? null : assignment, entry.Rig);
        }
        foreach (var (rig, actor, pump) in pumpOperators) {
            float pressed = pumping[pump.Id];
            var origin = To3(pump.Pos);
            var turn = new Basis(Vector3.Up, -pump.Rotation);
            rig.HoldPumpHandle(actor.Pose, origin + turn * WaterPumpShape.GripAt(pressed, true),
                origin + turn * WaterPumpShape.GripAt(pressed, false), Mathf.SmoothStep(0, .1f, actor.Time));
        }
        // Removing while walking a dictionary is allowed, so no copy of its keys is needed.
        foreach (var (id, entry) in crew) {
            if (HasWorker(id)) continue;
            fades.Remove(entry.Rig.Root); entry.Rig.Root.QueueFree(); crew.Remove(id);
        }
    }

    private static bool HugsBarrel(ActorAction action, Inventory backpack) =>
        action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run or ActorAction.Brake && Barrel.In(backpack) != null;

    /// <summary>
    /// Whether a player shows the tool they have selected: standing or moving about, once their arms have come down from an
    /// action that put it away, such as knocking on a door.
    /// </summary>
    private static bool Carries(ActorState state) =>
        state.Action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run
        && (state.Blend >= 1 || state.From is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run || ActorAnimation.Tool(state.From) != null);

    /// <summary>
    /// A worked station near a player that is processing with nobody of the crew working it, for an idle player to work at.
    /// A player holding on to a gather target is busy with that, even with a pose that shows no gathering.
    /// A station whose recipe waits for a tool that is neither slotted nor carried by the player is not worked.
    /// </summary>
    private Thing? StationToHammer(PlayerState p) {
        if (p.GatherTarget != 0) return null;
        using var acting = World.ActAs(p);
        foreach (var kind in World.WorkedKinds)
            foreach (var t in World.ThingsOf(kind)) if (World.Worked(t) && t.Processing && World.AtStation(t) && !World.CrewHammers(t) && Tooled(t)) return t;
        return null;
    }

    /// <summary>
    /// The hammer a player swinging <paramref name="tool"/> works a station with tool slots with: the faster of the one
    /// slotted there and the one they carry; null for another tool, away from such a station, or with no hammer at hand.
    /// </summary>
    private string? StationHammer(string? tool, Thing? station, PlayerState p) {
        if (tool != "hammer" || station?.Tools == null) return null;
        using var acting = World.ActAs(p);
        return World.ToolInUse(station, "hammer", 0);
    }

    /// <summary>Whether a player swinging <paramref name="tool"/> at a station swings the hammer slotted there.</summary>
    private bool SwingsSlottedHammer(string? tool, Thing? station, PlayerState p) {
        if (tool != "hammer" || station?.Tools == null) return false;
        using var acting = World.ActAs(p);
        return World.WorksWithSlotted(station, "hammer");
    }

    /// <summary>Whether the acting player has the tool a station's recipe needs: slotted, carried or none.</summary>
    private bool Tooled(Thing t) => t.Tools == null || !World.Data.Recipes.TryGetValue(t.Recipe, out var r) || World.HasToolFor(t, r);

    /// <summary>How high a flame leaps at <paramref name="phase"/>, from 0 to 1 and back twice a turn, smoothly at both ends.</summary>
    private static float Rising(float phase) => MathF.Sin(phase) * MathF.Sin(phase);

    /// <summary>The flame of a torch in hand, fanned by a swing, and the sparks of each strike while it is being lit.</summary>
    private void Torch(ActorRig rig, ActorState state, bool lit, float flare, Vector2? at) {
        rig.Flame.Visible = lit;
        if (lit) {
            // A slow flicker: a fast one makes the shadows the torch casts jitter.
            float slow = Elapsed / 3, flicker = MathF.Sin(slow * 13) * .5f + MathF.Sin(slow * 29 + 1) * .5f; flare /= .5f;
            rig.Flame.Scale = new Vector3(1 + flicker * .1f, .85f + Rising(slow * 11) * .3f, 1 - flicker * .1f) * (1 + flare * .25f);
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
            if (other.PulseRemaining > 0 && (!moving || ActorAnimation.SwingsOnTheMove(other.Pulse))) {
                action = other.Pulse; facing = other.PulseTarget; subject = ActorAnimation.SwingsOnTheMove(action) ? null : ThingAt(facing);
            }
            Thing? worked = action == ActorAction.Idle ? StationToHammer(p) : null;
            if (worked != null) { action = ActorAnimation.StationAction(worked, World.Data); facing = worked.Pos; subject = worked; }
            if (p.Braking && moving && !ActorAnimation.SwingsOnTheMove(action)) action = ActorAction.Brake;
            if (p.Mauled) {
                bool fallsBack = FallsBack(p.Id);
                action = p.RespawnIn > 0 ? ActorAction.Devoured : other.DownedFor < ActorAnimation.FallBackSeconds ? ActorAction.FallBack : ActorAction.Mauled;
                facing = fallsBack ? p.Pos - World.Doorways.OutOfDoorAt(p.DoorCabin) : PinningWolf(p.Id)?.Pos ?? p.Pos + Vector2.Down;
                other.DownedFor += dt;
            } else other.DownedFor = 0;
            var at = p.Pos;
            var door = doorways.AtDoor(p, state, dt);
            if (door is DoorShown shown) { at = shown.At; facing = shown.Facing; action = shown.Action; subject = null; }
            action = WorkPose(action, subject);
            state.Update(at, action, facing, dt, door?.Backing ?? false, ActorAnimation.SwingsOnTheMove(action) ? facing : null);
            state.Mark = Mark(subject, state, action);
            other.PulseRemaining = Math.Max(0, other.PulseRemaining - dt);
            if (action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run && (state.Struck(.25f) || state.Struck(.75f))) Sounds?.Play(Footsteps.Sound(World, p.Pos), p.Pos);
            bool torch = p.Torch > 0 && p.RespawnIn <= 0;
            string? tool = ActorAnimation.Tool(action) ?? (Carries(state) ? HeldTool(p.Selected, p.Backpack) : null);
            PosePlayer(other.Rig, state, p, ref other.ArmPull, other.DownedFor, Grade(tool, StationHammer(tool, worked, p) ?? p.Selected, p.Backpack), torch, door, dt);
            if (SwingsSlottedHammer(tool, worked, p)) swung.Add(worked!.Id);
            Torch(other.Rig, state, p.TorchLit, p.TorchFlare, p.Pos);
            other.Rig.Root.Visible = (p.RespawnIn <= 0 || p.Mauled) && p.Sheltered == 0 && DrawsAt(p.Pos);
            Work(state, subject, other.Rig);
        }
        foreach (var (id, other) in others)
            if (!HasPlayer(id)) { other.Rig.Root.QueueFree(); others.Remove(id); }
    }

    private bool HasWorker(int id) {
        foreach (var w in World.Workers) if (w.Id == id) return true;
        return false;
    }

    private bool HasPlayer(int id) {
        foreach (var p in World.Players) if (p.Id == id) return true;
        return false;
    }

    /// <summary>The shared pump event keeps its network action, while the water pump has its own pose.</summary>
    private static ActorAction WorkPose(ActorAction action, Thing? subject) =>
        action == ActorAction.Pump && subject?.Kind == ThingKind.Pump ? ActorAction.PumpWater : action;

    private Thing? ThingAt(Vector2 at) {
        foreach (var t in World.Things) if (t.Pos == at) return t;
        return null;
    }

    /// <summary>A player's arms follow the wolves holding them and ease free as the player stands or falls.</summary>
    private void PosePlayer(ActorRig rig, ActorState state, PlayerState who, ref (float PullL, float PullR) pull,
        float downed, string? tool, bool torch, DoorShown? door, float dt) {
        var pose = tugs.PulledArms(downed > 0 ? state.Pose : StandingPose(rig, state), who, ref pull, dt);
        if (downed > 0) FallBackView.FallBack(rig, state, pose, downed, torch, who.RespawnIn > 0 ? null : HoldingOff(who.Id));
        else ShowStanding(rig, state, pose, tool, torch, door, dt, HugsBarrel(state.Action, who.Backpack), null);
    }

    private static void Pose(ActorRig rig, ActorState state, string? tool, bool torch = false, DoorShown? door = null,
        float dt = 0, bool barrel = false, string? vessel = null) =>
        ShowStanding(rig, state, StandingPose(rig, state), tool, torch, door, dt, barrel, vessel);

    /// <summary>Places the upright rig so the pose can aim at the actor's mark in its own frame.</summary>
    private static Pose StandingPose(ActorRig rig, ActorState state) {
        rig.Root.Position = To3(state.Position);
        // The body leans about its feet into a turn, and the head looks ahead of it.
        rig.Root.Rotation = new Vector3(0, state.Yaw, state.Turning.Bank);
        state.MarkInRig = state.Mark is Vector3 mark ? rig.Root.Transform.AffineInverse() * mark : null;
        return state.Pose;
    }

    /// <summary>Draws the actor in its standing pose, settling its feet onto the steps while at a cabin door.</summary>
    private static void ShowStanding(ActorRig rig, ActorState state, Pose pose, string? tool, bool torch,
        DoorShown? door, float dt, bool barrel, string? vessel) {
        Vector3? jamb = null;
        if (door is DoorShown at) {
            (pose, jamb) = DoorwayView.StandAt(pose, state, at, state.OnSteps ? dt : StairSteps.Settle);
            rig.Root.Position += Vector3.Up * state.Lift;
            state.OnSteps = true;
        } else { state.Lift = 0; state.LiftSpeed = 0; state.OnSteps = false; }
        state.HipHeight = pose.HipHeight;
        // Between actions the torch arm stays raised with the forearm level, which is the angle the flame is built for.
        if (torch && state.Action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run) pose = pose with { RaiseL = 30, OutL = 4, ElbowL = 60 };
        if (barrel) rig.HugBarrel(pose, torch);
        else if (vessel != null) WorkerWaterView.Carry(rig, pose, vessel);
        else rig.Show(pose, tool, state.Action == ActorAction.Carry, state.Action == ActorAction.Perch, torch);
        if (state.From == ActorAction.PumpWater && state.Action == ActorAction.Idle && state.Blend < 1 && !barrel && vessel == null)
            rig.ReleasePumpHandle(pose, 1 - Ease(state.Blend));
        if (jamb is Vector3 frame) DoorwayView.HandOnFrame(rig, pose, frame, door!.Value.Grip, tool, torch);
        if (state.Turning.Look != 0) rig.Head.Rotation += new Vector3(0, state.Turning.Look, 0);
    }

    /// <summary>The sound of hands closing on a gathered thing: plants rustle and a carcass tears, while stone and water make none.</summary>
    private static string? GrabSound(ThingKind kind) => kind switch {
        ThingKind.Tree or ThingKind.Berries or ThingKind.Leaves or ThingKind.Farm => "rustle",
        ThingKind.WolfCorpse => "butcher_carcass",
        _ => null
    };

    /// <summary>Whether working on a thing hammers in a mine's frame and rails rather than digging.</summary>
    private static bool Fitting(Thing? target) => target is { Kind: ThingKind.Mine, MineStage: MineStage.Supplied };

    /// <summary>Where a blow of <paramref name="action"/> lands on <paramref name="subject"/> for an actor standing as <paramref name="state"/> has them, or null when it lands on nothing.</summary>
    private Vector3? Mark(Thing? subject, ActorState state, ActorAction action) =>
        subject != null && props.TryGetValue(subject.Id, out var prop) ? strikes.At(subject, state.Position, action, prop) : null;

    /// <summary>Feeds an actor's work into the object it works on: bellows travel, impact shakes, chips and sparks.</summary>
    private void Work(ActorState actor, Thing? subject, ActorRig rig) {
        if (subject == null) return;
        var action = actor.Action;
        if (action == ActorAction.PumpWater) {
            pumping[subject.Id] = Math.Max(pumping.GetValueOrDefault(subject.Id), ActorAnimation.WaterPumpPress(actor.Time));
            pumpOperators.Add((rig, actor, subject));
        } else if (action == ActorAction.Pump) {
            pumping[subject.Id] = Math.Max(pumping.GetValueOrDefault(subject.Id), Math.Clamp((ActorAnimation.PoseAt(action, actor.Time, false).Lean - 16) / 16, 0, 1));
        }
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
        float impact = ActorAnimation.StrikeShare(action) ?? action switch {
            ActorAction.Pump => .35f,
            ActorAction.PumpWater => ActorAnimation.WaterPumpBottom,
            ActorAction.Gather => .1f,
            _ => -1
        };
        if (impact < 0 || !actor.Struck(impact) || !props.TryGetValue(subject.Id, out var prop)) return;
        // What is out of sight is drawn as it was last seen, so nothing there shakes or throws chips.
        bool inSight = World.Sees(subject.Pos);
        if (inSight) prop.Shake = 1;
        // A recipe with a sound of its own is heard in place of its station's.
        // Fitting a mine's frame knocks wood like a bench, and every other blow of each hammer also clinks on a rail.
        string? sound = action is ActorAction.Mine or ActorAction.SteelMine ? "mine_ore"
            : action == ActorAction.Dig ? "dig_earth"
            : action is ActorAction.Pump or ActorAction.PumpWater ? "bellows_pump"
            : World.Data.Recipes.GetValueOrDefault(subject.Making)?.Sound is { Length: > 0 } made ? made
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
        // Chips and soil fly from where the blow lands, or from the side of what is worked toward the worker where no blow is aimed.
        Vector3 Hit(float height) => actor.Mark is Vector3 mark ? mark + Vector3.Up * (height < .2f ? height : 0) : side + Vector3.Up * height;
        switch (subject.Kind) {
            case ThingKind.Tree:
                Burst(Hit(1.1f), Mat("d9b07a"), 7, 2.6f, .55f, 9, .06f);
                Burst(To3(subject.Pos, 3.6f * WorldModels.TreeScale(subject.Id)), Mat("5d8a3c"), 3, .6f, 1.8f, 1.2f, .09f);
                break;
            case ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin or ThingKind.Mine:
                Burst(Hit(.5f), Mat(RockColor(subject.Kind)), 6, 2.4f, .5f, 9, .07f);
                Burst(Hit(.4f), GatherDust, 3, .5f, .9f, -.3f, .16f, 1.5f);
                if (action == ActorAction.SteelMine) Burst(Hit(.5f), Sparks, 5, 3.2f, .3f, 4, .025f);
                break;
            case ThingKind.Stump or ThingKind.Bush:
                // The blade goes in at the foot of it and throws up soil, and a bush sheds leaves as it is rocked.
                Burst(Hit(.08f), Soil, 6, 2, .5f, 9, .06f);
                Burst(Hit(.1f), GatherDust, 2, .4f, .8f, -.3f, .14f, 1.5f);
                if (subject.Kind == ThingKind.Bush) Burst(To3(subject.Pos, .5f), Mat("5d8a3c"), 3, 1.2f, .7f, 5, .06f);
                break;
            case ThingKind.WolfCorpse: Burst(side + Vector3.Up * .3f, Mat("7a1414", .6f), 5, 1.4f, .45f, 8, .04f); break;
            case ThingKind.Berries: Burst(To3(subject.Pos, .7f), Mat("5d8a3c"), 3, 1.2f, .7f, 5, .06f); break;
            case ThingKind.Water: Burst(side + Vector3.Up * .05f, Splash, 6, 1.6f, .45f, 7, .045f); break;
            case ThingKind.Anvil when prop is AnvilProp anvil: Burst(anvil.Body.ToGlobal(anvil.Vent), Sparks, 10, 3, .35f, 5, .025f); break;
            case ThingKind.Reinforced:
                if (subject.Heat > 10) Burst(prop.Body.ToGlobal(new Vector3(0, .9f, .5f)), Embers, 6, 1.4f, .6f, -1, .03f);
                break;
        }
    }

    /// <summary>The way a felled tree goes over: as its stump has it, or for a tree that left no stump, the way its model faces.</summary>
    private float FellToward(TreeProp tree) {
        foreach (var stump in World.ThingsOf(ThingKind.Stump)) if (stump.OfTree == tree.Id) return stump.FellToward;
        return -Mathf.DegToRad(tree.Body.RotationDegrees.Y) + MathF.PI / 2;
    }
    /// <summary>How fast a shake from a blow dies away, its share a second.</summary>
    private const float ShakeFades = 2.5f;
    /// <summary>A tree's turn as it shakes from a blow, a share <paramref name="shake"/> of the full shake.</summary>
    private Basis TreeShake(float shake) => Basis.FromEuler(new Vector3(Mathf.DegToRad(shake * MathF.Sin(Elapsed * 34) * 3), 0, Mathf.DegToRad(shake * 2 * MathF.Cos(Elapsed * 29))));
    /// <summary>Seconds a felled tree lies where it landed before it starts to fade, and seconds its trunk and crown then take to fade away over the stump.</summary>
    private const float TreeLiesSeconds = .1f, TreeFadeSeconds = .5f;
    /// <summary>How small a rock or deposit has grown as it is mined down to its last piece, and the seconds it then takes to crumble away.</summary>
    private const float MinedSize = .55f, CrumbleSeconds = .2f;
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

    private bool DrawsAt(Vector2 at) => FirstPerson || World.Sees(at);
    private bool DrawsWorker(Worker w) => FirstPerson ? !w.Home && w.WorkState != WorkerWorkState.Underground : World.Sees(w);

    private void CollectDrawnThings(List<Thing> into, bool everything) {
        if (!everything) { World.CollectKnown(into); return; }
        into.Clear();
        foreach (var thing in World.Things) into.Add(thing);
    }

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
        bool firstPerson = FirstPerson;
        bool everything = firstPerson || filming || EnvironmentReview && EnvironmentCenter.HasValue;
        CollectDrawnThings(known, everything);
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
            // Preview distant shapes and positions without replacing the small details the overhead view remembers.
            bool closeDetails = live && (!firstPerson || World.Sees(t.Pos));
            Animate(prop, t, live ? dt : SettleSeconds, closeDetails, watched && closeDetails);
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
        if (prop == null && appeared && (t.Kind is ThingKind.Crate or ThingKind.Barrel || World.Data.Buildings.ContainsKey(t.Kind))) Plop(t.Pos, World.Footprint(t.Kind));
        if (prop != null) prop.Root.QueueFree();
        prop = WorldModels.Build(t);
        scene.AddChild(prop.Root);
        props[t.Id] = prop;
        prop.Root.Position = To3(t.Pos);
        // A fresh carcass keeps the heading of the wolf model it replaces.
        // It also waits for the killing blow to land, and is thrown back from where the wolf stood.
        if (prop is CarcassProp carcass && Dying(t.Pos) is { } dying) {
            prop.Body.Rotation = new Vector3(0, dying.Yaw, 0);
            carcass.From = dying.Root.Position - To3(t.Pos); carcass.HitIn = SwingLands(dying.Last + dying.KnockBack).In;
            carcass.Wolf.FallFrom(dying);
        }
        // A chip that appears in front of the player was knocked off the rock beside it, and flies from there.
        if (prop is LooseProp chip && appeared && ChippedFrom(t) is Thing rock) { chip.From = To3(rock.Pos) - To3(t.Pos) + Vector3.Up * .7f; chip.Flight = 0; }
        return prop;
    }

    /// <summary>Whether a model whose thing has left the world plays its way out: a felled tree, and a stump or bush that was dug out rather than built over.</summary>
    private bool Departs(Prop prop) => prop is TreeProp || prop is DugProp && !BuiltOver(prop.Look.Pos);

    /// <summary>Whether a thing is a rock or ore deposit, mined down piece by piece until it breaks apart.</summary>
    private static bool Mined(ThingKind kind) => kind is ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin;

    /// <summary>The colour of a rock's chips, by the stone or ore it is.</summary>
    private static string RockColor(ThingKind kind) =>
        kind switch { ThingKind.Iron => "a35a2e", ThingKind.Tin => "b9bbb4", ThingKind.Coal => "26282b", _ => "9a9d99" };

    /// <summary>Whether a building, yard, crate or gravel path tile now lies over a spot, having cleared away the stump or bush that was there.</summary>
    private bool BuiltOver(Vector2 at) {
        if (World.GravelPaths.Laid(Terrain.TileAt(at))) return true;
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
                // The wood gives as the tree starts to go: the tearing of a tall tree lasts about as long as its fall.
                if (tree.Falling == null) {
                    tree.Falling = WorldModels.Topple(tree, FellToward(tree));
                    Sounds?.Play(tree.Tall >= TearsFrom ? "tree_bend" : "tree_crack", prop.Look.Pos, pitch: TreePitch(tree.Tall));
                }
                var fall = tree.Falling;
                if (fall.Advance(dt)) {
                    tree.Lying = 0;
                    Burst(tree.Sway.GlobalTransform * new Vector3(0, 3.5f, 0) + Vector3.Up * .3f, FallDust, 8, 1.2f, 1.1f, -.2f, .3f, 1.5f);
                    Sounds?.Play("tree_crash", prop.Look.Pos, pitch: TreePitch(tree.Tall));
                } else if (tree.Lying >= 0) tree.Lying += dt;
                // The last blow's shake carries on as it goes over.
                prop.Shake = Math.Max(0, prop.Shake - dt * ShakeFades);
                tree.Sway.Transform = fall.Turn * new Transform3D(TreeShake(prop.Shake).Scaled(Vector3.One * WorldModels.TreeScale(tree.Id)), Vector3.Zero);
                SetFade(tree.SwayParts, Math.Clamp((tree.Lying - TreeLiesSeconds) / TreeFadeSeconds, 0, 1));
                return tree.Lying < TreeLiesSeconds + TreeFadeSeconds;
            case DugProp dug:
                if (dug.Out < 0) {
                    dug.Out = 0; dug.Launch = prop.Body.Transform;
                    var foot = prop.Root.Position + dug.Seat;
                    Burst(foot + Vector3.Up * .1f, Soil, 10, 2.4f, .6f, 9, .07f);
                    Burst(foot + Vector3.Up * .15f, GatherDust, 4, .6f, .9f, -.3f, .2f, 1.5f);
                    if (prop.Kind == ThingKind.Bush) Burst(foot + Vector3.Up * .5f, Mat("5d8a3c"), 6, 1.6f, .8f, 5, .06f);
                }
                dug.Out = Math.Min(Thrown.DugOutSeconds, dug.Out + dt);
                // Thrown up from the lean digging left it at.
                prop.Body.Transform = Thrown.DugOut(dug.Launch, dug.Middle, dug.Out);
                return dug.Out < Thrown.DugOutSeconds;
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
        prop.Plumbing?.Show(t.ShowsPlumbing);
        // Most things in sight stand still: one that is neither shaking nor holding more or less than when last drawn needs no writing to.
        bool still = !first && !shaking && prop.Look.Remaining == t.Remaining;
        prop.Seen = true;
        prop.Shake = Math.Max(0, prop.Shake - dt * ShakeFades);
        float wobble = prop.Shake * MathF.Sin(Elapsed * 34);
        float flicker = MathF.Sin(Elapsed * 13 + t.Id) * .5f + MathF.Sin(Elapsed * 7.3f + t.Id * 3) * .5f;
        switch (prop) {
            case TreeProp tree:
                if (first || shaking) tree.Sway.Basis = TreeShake(prop.Shake).Scaled(Vector3.One * WorldModels.TreeScale(tree.Id));
                break;
            case DugProp dug:
                float worked = Clocked(dug.Worked, t.Progress, live, dt);
                if (still && worked == dug.Drawn) break;
                dug.Drawn = worked;
                // Digging works it loose: it leans further over with the work done, and rocks as each push of the shovel lands.
                float loose = Ease(Math.Clamp(worked / GatherRules.DigSeconds, 0, 1));
                prop.Body.RotationDegrees = new Vector3(loose * 16 + wobble * 5, dug.Yaw, wobble * 3);
                prop.Body.Position = dug.Seat + new Vector3(wobble * .02f, loose * .07f, 0);
                break;
            case LooseProp chip when chip.Flight < WorldModels.ChipFlight + WorldModels.ChipSettle:
                // An arc under gravity from the rock, spinning steadily round its middle, then a short skid to a stop where it lies.
                chip.Flight += dt;
                float way = Thrown.ChipWay(chip.Flight, WorldModels.ChipFlight, WorldModels.ChipSettle);
                var spin = Movement.About(new Basis(Vector3.Up, MathF.Tau * WorldModels.ChipTurns * way), chip.Middle);
                var along = new Vector3(chip.From.X, 0, chip.From.Z) * (1 - way)
                    + Vector3.Up * Thrown.ChipHeight(chip.Flight, WorldModels.ChipFlight, chip.From.Y);
                prop.Body.Transform = spin.Translated(along) * new Transform3D(chip.Rest, Vector3.Zero);
                break;
            case Prop rock when Mined(rock.Kind):
                // Its last piece mined while watched, the rock breaks into chips with a puff of dust and crumbles away.
                if (t.Remaining <= 0 && watched && prop.Look.Remaining > 0) {
                    rock.Crumbled = 0;
                    var middle = rock.Body.GlobalTransform * WorldModels.Bounds(rock.Body).GetCenter();
                    Burst(middle, Mat(RockColor(rock.Kind)), 14, 2.6f, .6f, 9, .08f);
                    Burst(middle + Vector3.Down * .1f, GatherDust, 7, .8f, 1.2f, -.3f, .3f, 1.6f);
                }
                if (rock.Crumbled >= 0 && rock.Crumbled < CrumbleSeconds) {
                    rock.Crumbled += dt;
                    rock.Body.Scale = Vector3.One * MinedSize * (1 - Mathf.SmoothStep(0, CrumbleSeconds, rock.Crumbled));
                    rock.Body.Visible = rock.Crumbled < CrumbleSeconds;
                    break;
                }
                if (still) break;
                if (t.Remaining > 0) rock.Crumbled = -1;
                rock.Body.Visible = t.Remaining > 0;
                rock.Body.Scale = Vector3.One * (MinedSize + (1 - MinedSize) * Math.Clamp(WorldModels.Fullness(t), 0, 1));
                rock.Body.Position = new Vector3(wobble * .03f, 0, 0);
                break;
            case Prop when t.IsLoose || t.Kind is ThingKind.Leaves:
                if (still) break;
                prop.Body.Visible = t.Remaining > 0;
                prop.Body.Scale = Vector3.One * (MinedSize + (1 - MinedSize) * Math.Clamp(WorldModels.Fullness(t), 0, 1));
                prop.Body.Position = new Vector3(wobble * .03f, 0, 0);
                break;
            case CarcassProp carcass:
                // Thrown, the carcass covers the knockback while it is in the air and stays where it lands.
                float since = Clocked(carcass.Dead, t.Progress, live, dt) - carcass.HitIn, dying = Math.Clamp(since / WolfModel.FallSeconds, 0, 1), thrown = Math.Min(1, dying / WolfModel.ThrowShare);
                // The killing blow thuds as it lands, heard only where the player saw the wolf fall.
                if (!carcass.Landed && since >= 0) { carcass.Landed = true; if (watched) Sounds?.Play("wolf_hit", t.Pos); }
                carcass.Body.Position = carcass.From * (1 - thrown);
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
                float smelting = furnace.Burning(t.Processing, dt);
                ShowGlow(furnace.Glow, smelting);
                furnace.Light.LightEnergy = smelting * (1.1f + flicker * .25f) * (.5f + night);
                Smoke(furnace, smelting > 0, dt, FurnaceSmoke);
                break;
            case StoveProp stove:
                // The fire only burns for recipes that take fuel; the mortar's work and kneading dough are cold.
                float cooking = stove.Burning(t.Processing && World.Data.Recipes.TryGetValue(t.Recipe, out var cooked) && cooked.Burns, dt);
                ShowGlow(stove.Glow, cooking);
                stove.Light.LightEnergy = cooking * (.9f + flicker * .25f) * (.5f + night);
                if (stove.Mortar != null && live) stove.Mortar.Visible = t.Tools != null && World.BenchTool(t, "mortar") != null && !grinding.Contains(t.Id);
                Smoke(stove, cooking > 0, dt, FurnaceSmoke);
                break;
            case WaterPumpProp pump:
                pump.ShowStroke(pumping.TryGetValue(t.Id, out float stroke) ? stroke : null, dt);
                if (live) pump.Pump.SetPressure(t.Pressure / 100);
                break;
            case ForgeProp forge:
                float heat = Clocked(forge.Heat, t.Heat, live, dt) / 100;
                float forging = forge.Burning(t.Processing, dt);
                // Hot coals glow on their own, as the fire over them dies down.
                ShowGlow(forge.Glow, Math.Max(forging, Math.Clamp(heat / HotCoals, 0, 1)));
                forge.Light.LightEnergy = (heat * 1.5f + forging * .4f) * (1 + flicker * .15f) * (.5f + night);
                forge.Needle.RotationDegrees = new Vector3(0, 0, Mathf.Lerp(forge.Needle.RotationDegrees.Z, WorldModels.Gauge(heat), 1 - MathF.Exp(-6 * dt)));
                // Let go, the board rises back as the body that pressed it blends out of pumping.
                if (pumping.TryGetValue(t.Id, out float pressed)) (forge.Press, forge.Released) = (pressed, 0);
                else forge.Released += dt;
                float lift = 18 * (1 - forge.Press * (1 - Mathf.SmoothStep(0, 1, forge.Released / ActorAnimation.BlendSeconds)));
                forge.Lever.RotationDegrees = new Vector3(0, 0, lift);
                for (int i = 0; i < forge.Pleats.Length; i++) forge.Pleats[i].RotationDegrees = new Vector3(0, 0, lift * (i + 1) / (forge.Pleats.Length + 1));
                Smoke(forge, forging > 0 || heat > .3f, dt, ForgeSmoke);
                break;
            case CampfireProp campfire:
                bool burning = t.Burn > 0;
                // Out of fuel, the flames sink and shrink as the fire dies down.
                float flames = campfire.Burning(burning, dt);
                campfire.Fire.Visible = flames > 0;
                campfire.Fire.Scale = new Vector3(1 + flicker * .08f, .85f + Rising(Elapsed * 9 + t.Id) * .3f, 1 - flicker * .08f) * flames;
                campfire.Fire.RotationDegrees = new Vector3(0, Elapsed * 40, 0);
                campfire.Light.LightEnergy = flames * (1.2f + flicker * .3f) * (.35f + night * 1.4f);
                if (burning && dt > 0 && Jitter() < dt * 3) Burst(To3(t.Pos, .55f), Cinders, 1, .5f, 1.1f, -1.4f, .025f);
                Smoke(campfire, flames > 0, dt, CampfireSmoke);
                break;
            case AnvilProp anvil:
                if (live) anvil.Hammer.Show(swung.Contains(t.Id) ? null : World.BenchTool(t, "hammer"));
                break;
            case BenchProp bench:
                // Slotted tools lie on the bench top, but for a hammer in someone's hands.
                if (!live) break;
                bench.Chisel.Show(World.BenchTool(t, "chisel"));
                bench.Hammer.Show(swung.Contains(t.Id) ? null : World.BenchTool(t, "hammer"));
                prop.Body.Position = new Vector3(wobble * .02f, 0, 0);
                break;
            case PileProp pile:
                // Remembered piles carry no inventory, so they keep the stacks they had when last seen.
                if (!live) break;
                WorldModels.SyncPile(pile, t, World.Data);
                break;
            case FarmProp farm:
                farm.Plumbing!.Water(live && t.PlumbingInstalled && t.ReceivingWater, Elapsed);
                if (!live) break;
                int look = World.Farms.CropLook(t);
                farm.Soil.Show(World.Farms.WaterLevel(t), look > 0);
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
                    string kind = World.ToolKind(held);
                    foreach (var (tool, hook) in rack.Pegs[slot]) hook.Visible = tool == kind;
                    if (kind == Water.BottleTool) {
                        int liters = Water.BottleLiters(held);
                        if (liters == rack.BottleLiters[slot]) continue;
                        BottleShape.SetGauge(rack.Pegs[slot][kind].GetChild<Node3D>(0), liters);
                        rack.BottleLiters[slot] = liters;
                    }
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
                if (dt > 0 && open && box.Open == 0) { Sounds?.Play("open_chest", t.Pos); box.ShutCue = null; }
                // The lid's sound is heard as it shuts, so it starts as far ahead as its loudest moment is into it.
                if (dt > 0 && !open && box.Open == 1) box.ShutCue = Sounds?.Next("close_chest");
                box.Open = Mathf.MoveToward(box.Open, open ? 1 : 0, dt / LidSeconds);
                if (box.ShutCue is Sounds.Cue shut && box.Open * LidSeconds <= Sounds.Lead(shut)) { Sounds?.Play(shut, t.Pos); box.ShutCue = null; }
                box.Lid.RotationDegrees = new Vector3(-80 * Ease(box.Open), 0, 0);
                if (box.Marker != null) {
                    box.Marker.Position = new Vector3(0, 1.2f + .08f * MathF.Sin(Elapsed * 2.5f), 0);
                    box.Marker.Rotation = new Vector3(0, Elapsed * 1.5f, 0);
                }
                // A crate being lifted rises and rocks before it goes into the backpack; let go, it drops back under gravity.
                if (t.Kind == ThingKind.Crate) {
                    float lifted = t.Id == Lifting ? Ease(LiftProgress) * CrateLift : 0;
                    if (lifted >= box.Raised) (box.Raised, box.Dropping) = (lifted, 0);
                    else { box.Dropping += Movement.Gravity * dt; box.Raised = Math.Max(lifted, box.Raised - box.Dropping * dt); }
                    float rocking = box.Raised / CrateLift;
                    prop.Body.Position = new Vector3(0, box.Raised, 0);
                    prop.Body.RotationDegrees = new Vector3(rocking * 9 * MathF.Sin(Elapsed * 18), 0, rocking * 6 * MathF.Cos(Elapsed * 15));
                }
                break;
            case BarrelProp barrel:
                if (live) {
                    barrel.SetBungOpen(openBungs.GetValueOrDefault(t.Id));
                    float barrelLift = t.Id == Lifting ? Ease(LiftProgress) : 0;
                    prop.Body.Position = Vector3.Up * (.22f * barrelLift);
                    prop.Body.RotationDegrees = new Vector3(-8 * barrelLift, 0, 3 * MathF.Sin(Elapsed * 12) * barrelLift);
                }
                break;
            case HutProp hut:
                if (live) hut.RoofBarrel.Visible = Barrel.In(t.Inventory) != null;
                // The door stands open while someone steps in or out, and shuts behind them: swung shut after a walk, slammed after a run.
                // A wolf forcing the door holds it as far open as its body has pushed it.
                if (live && World.DoorCheck.AtDoor(t.Id) is { DoorCheck: <= DoorCheckStage.Launching } caller) DoorwayView.PushDoor(hut, DoorCheckView.DoorDegrees(DoorCheckClock(caller)));
                else {
                    var through = live ? doorways.Passing(t.Id) : null;
                    if (through != null) hut.Slam = through.DoorRush;
                    if (dt > 0 && through is { DoorRush: false } && hut.Open == 0) Sounds?.Play("hut_door_normal", t.Pos);
                    // A runner's slam is picked as they start through, also when they follow straight after someone else.
                    if (dt > 0 && through is { DoorRush: true } && hut.Through != through.Id) hut.SlamCue = Sounds?.Next("hut_door_escaping");
                    if (dt > 0) hut.Through = through?.Id;
                    bool slams = through == null && hut.Slam;
                    DoorwayView.SwingDoor(hut, through != null, dt, slams ? DoorwayView.SlamSeconds : Doorways.ShutSeconds, slams);
                    // The slam is heard as the door hits its frame, so its sound starts that far ahead: for a sound that builds up longer than the slam, while the runner is still in the doorway.
                    float untilShut = through != null ? Doorways.DoorSeconds(through) - through.DoorTime + DoorwayView.SlamSeconds : hut.Open * DoorwayView.SlamSeconds;
                    if (dt > 0 && hut.SlamCue is Sounds.Cue slam && hut.Slam && untilShut <= Sounds.Lead(slam)) { Sounds?.Play(slam, t.Pos); hut.SlamCue = null; }
                }
                bool lit = Dark;
                if (lit != hut.Lit) {
                    hut.Lit = lit;
                    hut.Window.MaterialOverride = lit ? NightGlass : DayGlass;
                    hut.Light.LightEnergy = lit ? .9f : 0;
                }
                break;
            case MineProp mine:
                // Remembered mines carry only their stage and progress, so out of sight the cart rests outside, empty, and the lantern is out.
                float stageShare = Math.Clamp(Clocked(mine.Built, t.Progress, live, dt) / World.Mines.StageSeconds, 0, 1);
                bool finished = t.MineStage == MineStage.Open;
                mine.Outline.Visible = !finished;
                mine.Hole.Visible = t.MineStage > MineStage.Marked || stageShare > 0;
                mine.Hole.Scale = new Vector3(1, 1, t.MineStage > MineStage.Marked ? 1 : Math.Max(.05f, stageShare));
                mine.Supplies.Visible = t.MineStage == MineStage.Supplied;
                mine.Frame.Visible = finished || t.MineStage == MineStage.Supplied && stageShare > 0;
                mine.Frame.Scale = new Vector3(1, finished ? 1 : Math.Max(.05f, stageShare), 1);
                bool mining = finished && live && World.Mines.Underground(t) > 0;
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
                sign.Open = Mathf.MoveToward(sign.Open, waiting ? 1 : 0, dt * 3);
                sign.Flag.RotationDegrees = new Vector3(-90 * (1 - Ease(sign.Open)), 0, 0);
                break;
        }
    }

    internal static float Ease(float t) => t * t * (3 - 2 * t);

    /// <summary>A count the server keeps as a motion shows it: run on by the window's clock in sight, as it is out of sight.</summary>
    private float Clocked(LocalClock clock, float count, bool live, float dt) => live ? clock.Follow(count, World.Time, dt) : clock.Set(count, World.Time);

    /// <summary>Metres a crate rises as it is lifted.</summary>
    private const float CrateLift = .35f;
    /// <summary>Seconds a chest's or crate's lid takes to open or shut.</summary>
    private const float LidSeconds = .25f;

    /// <summary>A station's glowing fire, shown as brightly as it burns, from 0 out to 1 fully, fading as it dies down.</summary>
    private static void ShowGlow(MeshInstance3D glow, float burning) {
        glow.Visible = burning > 0;
        glow.Transparency = 1 - burning;
    }

    /// <summary>The forge's heat, from 0 to 1, at which its coals glow fully on their own.</summary>
    private const float HotCoals = .05f;

    // Lookups that would otherwise capture the thing in a lambda, which allocates on every call for every thing drawn.
    private WolfModel? Dying(Vector2 at) {
        foreach (var (wolf, model) in wolves) if (!World.Wolves.Contains(wolf) && model.Last.DistanceTo(at) < 2) return model;
        return null;
    }
    /// <summary>Whether a player is pinned by a wolf beside them, which tugged them back out of a doorway, so they fall back rather than turn to it.</summary>
    private bool FallsBack(int player) => PinningWolf(player) is { LungeFrom: var lungeFrom } && lungeFrom != Vector2.Zero;
    private Wolf? PinningWolf(int player) {
        foreach (var w in World.Wolves) if (w.Pinning && w.Target == player) return w;
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
    private bool OrderAt(int signpost) {
        foreach (var o in World.Orders) if (o.Signpost == signpost) return true;
        return false;
    }

    // Campfire, furnace and reinforced furnace smoke: the coal forge's plume is darker, denser and wider.
    private readonly record struct SmokeStyle(float Every, Color Color, float Size, float Grow);
    private static readonly SmokeStyle CampfireSmoke = new(.5f, new Color(.62f, .62f, .6f, .38f), .14f, 2.2f);
    private static readonly SmokeStyle FurnaceSmoke = new(.3f, new Color(.58f, .58f, .56f, .42f), .14f, 2.4f);
    private static readonly SmokeStyle ForgeSmoke = new(.16f, new Color(.36f, .36f, .36f, .55f), .2f, 2.8f);

    private void Smoke(HearthProp prop, bool active, float dt, SmokeStyle smoke) {
        // Seen from the ground by the film's camera, the puffs are flat discs.
        if (!active || dt <= 0 || filming) return;
        prop.Smoke -= dt;
        if (prop.Smoke > 0) return;
        prop.Smoke = smoke.Every;
        Burst(prop.Body.ToGlobal(prop.Vent), Flat(smoke.Color), 1, .25f, 2.4f, -.45f, smoke.Size, smoke.Grow);
    }

    private readonly List<Thing> structures = new();
    /// <summary>Trees in front of the player, a worker or a structure turn see-through instead of hiding them.</summary>
    private void FadeCanopies(float dt) {
        // The film's camera stands among the trees, where nothing needs seeing through.
        if (filming || FirstPerson) return;
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
            float fade = Mathf.MoveToward(tree.Fade, CoversSomething(t, ppu) ? .68f : 0, dt * 3);
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
        feeding.Remember(World.Wolves);
        foreach (var wolf in World.Wolves) {
            if (!wolves.TryGetValue(wolf, out var model)) {
                model = new WolfModel { Last = wolf.Pos, Yaw = ActorAnimation.Yaw(Hunted(wolf) - wolf.Pos, 0) };
                scene.AddChild(model.Root); wolves[wolf] = model;
            }
            var hunted = Hunted(wolf);
            var moved = wolf.Pos - model.Last;
            model.Last = wolf.Pos;
            if (wolf.DoorCheck is >= DoorCheckStage.Scratching and <= DoorCheckStage.Launching && World.Find(wolf.DoorCheckCabin) is Thing checkedCabin) {
                DoorCheckView.Show(wolf, model, checkedCabin, Elapsed + wolf.Id % 7, dt, DrawsAt(wolf.Pos));
                continue;
            }
            if (wolf.Tugging && tugs.ShowTugging(wolf, model, Elapsed, dt)) continue;
            if (wolf.Side != 0 && tugs.ShowHolding(wolf, model, RigOf(wolf.Target), Elapsed, dt)) continue;
            model.Steps = null;
            if (WolfCounter.Active(wolf)) { ShowWolfCounter(wolf, model, hunted, moved, dt); continue; }
            model.CounterStage = WolfCounterStage.None; model.CounterHitPending = false;
            if (wolf.Pinning && wolf.LungeFrom != Vector2.Zero && tugs.ShowLunging(wolf, model, Elapsed, dt)) continue;
            // A blow lands in the world as the swing comes down and throws the wolf back there at once; the model hops back from where it stood.
            // The blow can reach this machine a moment before the swing it shows comes down: the model stands until it does.
            if (wolf.Stagger > model.Stagger) {
                model.KnockBack -= moved; moved = Vector2.Zero;
                (model.HitIn, model.Striker) = SwingLands(wolf.Pos + model.KnockBack);
                model.Struck = true; model.Recoiling = 0;
            }
            model.Stagger = wolf.Stagger;
            if (model.Struck) {
                model.HitIn = Math.Max(0, model.HitIn - dt);
                if (model.HitIn == 0) {
                    var at = wolf.Pos + model.KnockBack;
                    model.Struck = false; model.Recoiling = WolfModel.RecoilSeconds; model.KnockBackFrom = model.KnockBack; FurAndEmbers(To3(at, .6f)); Sounds?.Play("wolf_hit", at);
                }
            } else if (model.Recoiling > 0) {
                // The wolf goes back only while it is in the air, at an even pace, so its paws never slide.
                model.Recoiling = Math.Max(0, model.Recoiling - dt);
                float hop = Math.Min(1, (1 - model.Recoiling / WolfModel.RecoilSeconds) / WolfModel.HopShare);
                model.KnockBack = model.KnockBackFrom * (1 - hop);
            } else model.KnockBack = Vector2.Zero;
            // A following wolf walking round its player turns its head to them now and then, each at its own moments.
            bool glances = wolf.Follow == FollowStage.Circling && wolf.Flee <= 0 && (Elapsed + wolf.Id * WolfModel.GlanceApart) % WolfModel.GlanceEvery < WolfModel.GlanceSeconds;
            float glance = glances ? Math.Clamp(Mathf.AngleDifference(model.Yaw, ActorAnimation.Yaw(hunted - wolf.Pos, model.Yaw)), -WolfModel.MaxGlance, WolfModel.MaxGlance) : 0;
            model.Glance = Mathf.MoveToward(model.Glance, glance, dt * WolfModel.GlanceRate);
            if (wolf.Recover > model.Recover) {
                model.LandedFor = 0;
                Sounds?.Play(Footsteps.Sound(World, wolf.Pos), wolf.Pos, pitch: .78f);
                if (World.Sees(wolf.Pos)) Burst(To3(wolf.Pos, .08f), FallDust, 3, .4f, .8f, -.25f, .14f, 1.4f);
            } else model.LandedFor += dt;
            model.Recover = wolf.Recover;
            bool committed = WolfSnap.Active(wolf) || wolf.Recover > 0 || wolf.Pinning;
            bool struck = model.Struck || model.Recoiling > 0, running = moved.LengthSquared() > 1e-6f && !wolf.Feeding && !struck;
            // No faster than a wolf backs off.
            bool slow = moved.Length() <= dt * WolfBodies.BackOffSpeed + 1e-4f;
            // Easing back from its player to biting distance, a wolf steps backward still facing them.
            bool backing = running && !wolf.Aggressive && wolf.Flee <= 0 && !wolf.Following && moved.Dot(hunted - wolf.Pos) < 0 && slow;
            // A scared or galloping wolf turns fast. At a walk or a trot, one that sets off far from the way it faces turns
            // round where it stands first, and only then goes after its place in the world.
            bool fast = wolf.Aggressive || wolf.Chase > 0 || wolf.Flee > 0;
            bool threatening = wolf.Threaten > 0;
            bool turnsRound = running && !backing && !fast && moved.Normalized().Dot(ActorAnimation.Forward(model.Yaw)) < WolfModel.SetsOffWithin;
            // Where it is drawn away from its place in the world, after turning round or letting go of a player it tugged,
            // closes up only as it walks, so it never slides.
            var drifted = model.Drift;
            model.Drift = committed || threatening ? Vector2.Zero : turnsRound ? model.Drift - moved : model.Drift.MoveToward(Vector2.Zero, WolfModel.CatchingUp(moved.Length(), dt));
            float walked = (moved + model.Drift - drifted).Length(), faced = model.Yaw;
            // A struck wolf keeps facing whoever hit it; a running one turns into its path.
            var facing = threatening ? Vector2.FromAngle(wolf.Heading) :
                struck ? model.Striker - wolf.Pos - model.KnockBack : running && !backing ? moved : hunted - wolf.Pos;
            // A struck wolf snaps round to whoever hit it; otherwise the head turns first and the body follows, leaning in.
            // Reversing, its body still faces the old way for a moment: it brakes rather than running backward.
            bool braking = false;
            if (struck) {
                model.Yaw = Mathf.LerpAngle(model.Yaw, ActorAnimation.Yaw(facing, model.Yaw), Math.Min(1, dt * 16)); model.Turning.Hold(model.Yaw);
                model.Root.Rotation = new Vector3(0, model.Yaw, 0);
            } else if (committed || threatening) {
                model.Yaw = ActorAnimation.Yaw(Vector2.FromAngle(wolf.Heading), model.Yaw);
                model.Turning.Hold(model.Yaw); model.Glance = 0;
                model.Root.Rotation = new Vector3(0, model.Yaw, 0);
            } else braking = model.TurnToward(facing, moved, dt, running, fast, backing);
            // Turning where it stands, it comes round about its hips, its forepaws stepping round them.
            float turned = Mathf.AngleDifference(faced, model.Yaw);
            // A howling wolf has turned already and keeps its paws planted.
            bool stepsRound = !committed && !struck && !fast && wolf.Howl <= 0 && (turnsRound || !running) && dt > 0 && Math.Abs(turned) > WolfModel.StepsRoundFrom * dt;
            if (stepsRound) model.Drift += (ActorAnimation.Forward(model.Yaw) - ActorAnimation.Forward(faced)) * WolfModel.HipsBehind;
            model.Root.Visible = DrawsAt(wolf.Pos);
            bool wary = wolf.Following || HuntedTorchLit(wolf);
            bool gathers = WolfSnap.Winding(wolf);
            float flight = WolfSnap.FlightOf(wolf), left = WolfSnap.Remaining(wolf);
            float wind = gathers ? 1 - (left - flight) / WolfSnap.Windup : 1;
            float drive = gathers ? 0 : 1 - left / flight;
            float rise = 0;
            if (!wolf.Pinning) model.Pinned = 0;
            if (model.Struck) model.Stand(Elapsed + wolf.Id % 7, dt, true);
            else if (model.Recoiling > 0) model.Recoil(1 - model.Recoiling / WolfModel.RecoilSeconds, dt, threatening);
            else if (threatening && !wolf.Feeding) model.Threaten(Elapsed + wolf.Id % 7, dt);
            else if (wolf.Leap > 0 && !gathers) {
                model.Leap(drive, dt, jawsUp: FallBackView.Throat(Vector2.Zero, Vector2.Up, 0).Y);
                rise = Movement.Rise(drive * flight, flight);
            } else if (wolf.Feeding) {
                if (wolf.Pinning) {
                    if (model.Pinned == 0) model.Toward = -Vector2.FromAngle(wolf.Heading);
                    model.Pinned += dt;
                    PinAt(model, hunted, FeedingSpots.PawsOnChest(model, RigOf(wolf.Target)), Elapsed + wolf.Id % 7, dt);
                } else model.Feed(Elapsed + wolf.Id % 7, dt, false);
            } else if (committed) {
                float settle = wolf.Recover > 0 ? model.LandedFor / WolfModel.SettleSeconds : 0;
                model.Snap(wind, drive, settle, dt);
            } else if (stepsRound) model.Move(Math.Abs(turned) * WolfModel.TurnStep, dt, false, wary);
            else if (wolf.Howl > 0) model.Howl(1 - wolf.Howl / wolf.HowlLength, dt);
            else if (braking) model.Brake(moved.Length(), dt);
            else if (running) model.Move(backing ? -walked : walked, dt, (wolf.Aggressive || wolf.Chase > 0 || wolf.Follow == FollowStage.Routed) && !backing, wary);
            else if (wolf.Follow == FollowStage.Braced) model.Brace(Elapsed + wolf.Id % 7, dt);
            else model.Stand(Elapsed + wolf.Id % 7, dt, wary);
            var place = To3(wolf.Pos + model.KnockBack + model.Drift, rise);
            if (wolf.Pinning) place = AtTheThroat(model, hunted);
            else model.Toward = Vector2.Zero;
            if (wolf.Feeding && !wolf.Pinning && Fallen(wolf) is var (lying, lyingRig)) place = feeding.Place(wolf, model, lying, lyingRig, dt);
            else { model.Fed = 0; model.FeedSide = 0; }
            model.Root.Position = place;
            // Only a wolf in sight has its paws set on the ground; one coming into sight stands where its pose has it.
            if (model.Root.Visible) model.Plant();
        }
        foreach (var (wolf, model) in wolves)
            if (!World.Wolves.Contains(wolf)) { model.Root.QueueFree(); wolves.Remove(wolf); }
    }

    /// <summary>Shows the two hops and puts the dust at the paws that touch or slide.</summary>
    private void ShowWolfCounter(Wolf wolf, WolfModel model, Vector2 hunted, Vector2 moved, float dt) {
        var (stage, progress) = WolfCounter.Pose(wolf);
        if (model.CounterStage == WolfCounterStage.None && stage == WolfCounterStage.Wince) {
            model.CounterHitPending = true;
            model.HitIn = SwingLands(wolf.Pos).In;
        }
        bool changed = model.CounterStage != stage;
        int previousDust = (int)(model.CounterProgress * 4);
        model.CounterStage = stage; model.CounterProgress = progress;
        model.KnockBack = model.Drift = Vector2.Zero;
        model.Struck = false; model.Recoiling = model.Recover = 0; model.Stagger = wolf.Stagger;
        model.Root.Visible = World.Sees(wolf.Pos);
        model.Root.Position = To3(wolf.Pos, WolfCounter.Height(wolf));
        model.Yaw = ActorAnimation.Yaw(Vector2.FromAngle(wolf.Heading), model.Yaw);
        model.Turning.Hold(model.Yaw); model.Glance = 0;
        model.Root.Rotation = new Vector3(0, model.Yaw, 0);

        if (model.CounterHitPending) {
            // A snapshot can reach a guest before the blow in the swing they are drawing lands.
            model.HitIn = Math.Max(0, model.HitIn - dt);
            if (model.HitIn > 0 && stage == WolfCounterStage.Wince) {
                model.Stand(Elapsed + wolf.Id % 7, dt, true);
                if (model.Root.Visible) model.Plant();
                return;
            }
            model.CounterHitPending = false;
            var impact = new Vector2(wolf.CounterMarkX, wolf.CounterMarkY) +
                Vector2.FromAngle(wolf.CounterBearing) * wolf.CounterRadius;
            if (model.Root.Visible) FurAndEmbers(To3(impact, .6f));
            Sounds?.Play("wolf_hit", impact);
        }
        if (stage == WolfCounterStage.Run) model.Move(moved.Length(), dt, true, false);
        else if (stage == WolfCounterStage.Face) model.Threaten(Elapsed + wolf.Id % 7, dt);
        else WolfCounterPoses.Show(model, wolf, hunted, stage, progress, dt);
        if (!model.Root.Visible) return;
        model.Plant();
        if (changed && stage is WolfCounterStage.Slide or WolfCounterStage.Pivot)
            Sounds?.Play(Footsteps.Sound(World, wolf.Pos), wolf.Pos, pitch: .76f);
        if (stage == WolfCounterStage.Slide && (changed || (int)(progress * 4) > previousDust))
            CounterDust(model, 0, 1);
        else if (stage == WolfCounterStage.Pivot) {
            if (changed) CounterDust(model, 0, 1);
            else if (progress > .36f && (int)(progress * 4) > previousDust) CounterDust(model, 2, 3);
        }
    }

    private void CounterDust(WolfModel model, int left, int right) {
        Burst((model.Root.Transform * model.SolePoint(left, 4)) with { Y = .05f }, FallDust, 2, .65f, .65f, -.25f, .095f, 1.4f);
        Burst((model.Root.Transform * model.SolePoint(right, 4)) with { Y = .05f }, FallDust, 2, .65f, .65f, -.25f, .095f, 1.4f);
    }

    /// <summary>Seconds into its door check at a cabin a wolf is drawn at, or as the world has it for one not drawn at the door yet.</summary>
    private float DoorCheckClock(Wolf wolf) => wolves.TryGetValue(wolf, out var model) && model.Motion == WolfMotion.Door ? model.DoorCheckClock : DoorCheckView.Clock(wolf.DoorCheck, wolf.DoorCheckTime);

    /// <summary>Seconds a player left inside a cabin goes on seeing the one a wolf takes out of it.</summary>
    private const float TakenOutSeconds = .9f;

    /// <summary>
    /// Shows the player inside a cabin what a wolf forcing its door looks like from in there: the wolves at the door, and
    /// for a moment the player the wolf takes out.
    /// </summary>
    private void SyncInsideDoorPOV() {
        InsideDoorPOV.Clear();
        if (World.Sheltered != 0 && World.DoorCheck.AtDoor(World.Sheltered) is { DoorCheck: >= DoorCheckStage.Prying } caller && props.TryGetValue(World.Sheltered, out var prop) && prop is HutProp hut) {
            var taken = caller.DoorCheck == DoorCheckStage.TakingOut ? World.PlayerById(caller.Target) : null;
            if (caller.DoorCheck != DoorCheckStage.TakingOut || taken is { Door: Doorway.Tugged, DoorTime: < TakenOutSeconds }) {
                InsideDoorPOV.Show(hut);
                if (wolves.TryGetValue(caller, out var lead)) InsideDoorPOV.Add(lead);
                foreach (var wolf in World.Wolves)
                    if (wolf != caller && (wolf.DoorCheckCabin == World.Sheltered || taken != null && wolf.Target == taken.Id) && wolves.TryGetValue(wolf, out var model)) InsideDoorPOV.Add(model);
                if (taken != null && RigOf(taken.Id) is ActorRig rig) InsideDoorPOV.Add(rig, ShirtOf(taken));
            }
        }
        InsideDoorPOV.Finish();
    }

    /// <summary>Places the wolf at the player's throat as they fall, then holds its paws where they landed.</summary>
    private static Vector3 AtTheThroat(WolfModel model, Vector2 hunted) {
        if (model.Pinned > ActorAnimation.FallBackSeconds) return model.Root.Position;
        var held = FallBackView.Throat(hunted, model.Toward, model.Pinned) - model.Root.Basis * model.Bite;
        return held with { Y = 0 };
    }

    /// <summary>
    /// Poses a wolf pinning the player it hunts, <see cref="WolfModel.Pinned"/> seconds after its jaws reached their throat: reared
    /// up to hold it as they go down, then leaning to keep its jaws on it where they lie, with its forepaws at <paramref name="chest"/>.
    /// </summary>
    private static void PinAt(WolfModel model, Vector2 hunted, (Vector3 Left, Vector3 Right)? chest, float time, float dt) {
        var throat = FallBackView.Throat(hunted, model.Toward, model.Pinned);
        bool lying = model.Pinned > ActorAnimation.FallBackSeconds;
        model.Feed(time, dt, true, model.Pinned / ActorAnimation.FallBackSeconds, chest: chest, jawsUp: lying ? null : throat.Y, jaws: lying ? model.Root.Transform.AffineInverse() * throat : null);
    }

    /// <summary>The wolf a downed player pushes at: the one knocking them down or pinning them, as drawn, or null.</summary>
    private WolfModel? HoldingOff(int player) {
        foreach (var w in World.Wolves)
            if (w.Target == player && w.Pinning && wolves.TryGetValue(w, out var model)) return model;
        return null;
    }

    /// <summary>The model a wolf is drawn with, or null for one not drawn yet.</summary>
    internal WolfModel? ModelOf(Wolf wolf) => wolves.GetValueOrDefault(wolf);
    /// <summary>The rig of a player as drawn, or null.</summary>
    internal ActorRig? RigOf(int id) => id == World.Me.Id ? playerRig : others.TryGetValue(id, out var other) ? other.Rig : null;
    /// <summary>How a player is drawn, or null for one not drawn yet.</summary>
    private ActorState? StateOf(int id) => id == World.Me.Id ? player : others.TryGetValue(id, out var other) ? other.State : null;

    /// <summary>The drawn player a wolf hunts once they lie fallen, or null.</summary>
    private (ActorState State, ActorRig Rig)? Fallen(Wolf wolf) =>
        StateOf(wolf.Target) is { Action: ActorAction.Mauled or ActorAction.Devoured } state && RigOf(wolf.Target) is ActorRig rig ? (state, rig) : null;

    /// <summary>Tufts of fur knocked loose by a hit, and embers off a burning torch.</summary>
    private void FurAndEmbers(Vector3 at) {
        Burst(at, Mat("6f746c"), 5, 1.8f, .5f, 6, .045f);
        if (World.TorchFlare > 0) Burst(at, Embers, 9, 2.8f, .35f, 3, .03f);
    }

    /// <summary>Fully drawn for the first part of a trip, then fading to nothing at the turn.</summary>
    private static float TripFade(float leg) => Math.Clamp((leg - .35f) / .55f, 0, 1);
    // The cart walks at a steady pace until it is well down the valley, waits there out of view, and walks back for the return; the trade delay is unchanged.
    internal const float CartSpeed = 2.2f, CartDistance = 16;
    /// <summary>Seconds a cart takes to reach its pace as it sets off, and to stop at the signpost on its return, gathering and losing pace evenly.</summary>
    private const float CartPicksUp = 1;
    /// <summary>How far out a cart is <paramref name="elapsed"/> seconds into a trip of <paramref name="trip"/> seconds, from 0 at the signpost to 1 where it has gone from view.</summary>
    internal static float CartLeg(float elapsed, float trip) {
        float walking = Math.Max(0, Math.Min(elapsed, trip - elapsed));
        return Math.Clamp(CartSpeed * Movement.Ramped(walking, CartPicksUp) / CartDistance, 0, 1);
    }
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
            if (!carts.TryGetValue(order, out var cart)) {
                // An order beyond the village's carts, such as an edited save's, gets a new one.
                cart = idleCarts.Count > 0 ? idleCarts.Pop() : NewCart();
                cart.Trip.Set(order.Elapsed, World.Time); cart.Distance = 0;
                carts[order] = cart;
            }
            float elapsed = cart.Trip.Follow(order.Elapsed, World.Time, dt), phase = elapsed / World.Data.TradeSeconds;
            // The cart sets off down the valley and fades out on its way to Bramblewick, then fades back in on return.
            float leg = CartLeg(elapsed, World.Data.TradeSeconds);
            var pos = (sign?.Pos ?? Terrain.Spawn) + Terrain.Seaward * CartDistance * leg;
            if (cart.Distance == 0) cart.Last = pos;
            cart.Distance += (pos - cart.Last).Length();
            // Out of view at the far end, it turns round to come back.
            cart.Yaw = ActorAnimation.Yaw(phase < .5f ? Terrain.Seaward : -Terrain.Seaward, 0);
            cart.Last = pos;
            cart.Root.Position = To3(pos);
            cart.Root.Visible = DrawsAt(pos) && leg < 1;
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
        if (EnvironmentReview || HideMarkers) {
            playerRing.Visible = placeTile.Visible = false;
            return;
        }
        playerRing.Visible = !World.Dead && !Underground && !FirstPerson;
        playerRing.Position = To3(World.Player, .015f);
        removalMark.Update(World, Removal);
        var ground = Vector2.Zero;
        bool canPoint = Placing != null && !Underground && TryGroundPoint(Mouse, out ground);
        placeTile.Visible = canPoint;
        var kind = Placing.GetValueOrDefault();
        var at = canPoint ? World.Construction.Snap(kind, ground.Round()) : Vector2.Zero;
        var shown = !canPoint ? null : kind == ThingKind.Mine ? mineGhosts[Cliff.Nearest(at).Ore] : ghosts[kind];
        if (shown != ghost) {
            if (ghost != null) ghost.Root.Visible = false;
            if (shown != null) shown.Root.Visible = true;
            ghost = shown;
        }
        if (shown == null) return;
        bool valid = World.Construction.PlacementBlock(kind, at, rotation: PlacingRotation) == null;
        if (shown.Tint != valid) { SetOverride(shown.Root, valid ? GhostValid : GhostBlocked); shown.Tint = valid; }
        shown.Root.Position = To3(at);
        if (kind == ThingKind.Mine) shown.Body.Rotation = MineModel.Yaw(Cliff.Nearest(at).Face);
        else shown.Root.Rotation = new Vector3(0, -PlacingRotation, 0);
        placeTile.Position = To3(at, .02f);
        placeTile.MaterialOverride = valid ? GhostValid : GhostBlocked;
    }

    /// <summary>Builds a hidden, faded ghost of every building, crate and barrel, so picking one in build mode compiles nothing mid-game.</summary>
    private void BuildGhosts() {
        Prop Ghost(ThingKind kind, Vector2 at) {
            var built = WorldModels.Build(new Thing { Kind = kind, Pos = at });
            SetFade(built.Root, .35f);
            NoShadows(built.Root);
            built.Root.Visible = false;
            scene.AddChild(built.Root);
            return built;
        }
        foreach (var kind in World.Data.Buildings.Keys) if (kind != ThingKind.Mine) ghosts[kind] = Ghost(kind, Vector2.Zero);
        ghosts[ThingKind.Crate] = Ghost(ThingKind.Crate, Vector2.Zero);
        ghosts[ThingKind.Barrel] = Ghost(ThingKind.Barrel, Vector2.Zero);
        foreach (var ore in Cliff.Ores) mineGhosts[ore] = Ghost(ThingKind.Mine, Cliff.Of(ore).MineSpot);
    }

    /// <summary>What the remove selector has turned red: the building or gravel path tile it points at, while that is in reach.</summary>
    public Removal RemovalShown => removalMark.Shown;
    /// <summary>The visible placement preview's model, while a plan or container is being placed.</summary>
    internal Node3D? PlacementGhost => ghost?.Root;

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

    /// <summary>The world's time of day as the sun and light show it; a sleep that skips the night jumps it.</summary>
    private readonly LocalClock dayClock = new(1);

    /// <summary>Lights the world for <paramref name="time"/> on the world's clock.</summary>
    private void UpdateLighting(float time) {
        // The sun rises at dawn and sets halfway through dusk, then the moon takes the same path until dawn.
        // Each fades out near the horizon, so the hand-over at sunset moves no visible shadow.
        float dayPhase = World.DayPhaseAt(time), dusk = World.Data.DuskStarts, sunset = World.Data.Sunset;
        // Dusk deepens into full night soon after sunset and dawn fades out, so neither boundary jumps in brightness.
        night = dayPhase >= dusk ? Mathf.SmoothStep(dusk, sunset + .02f, dayPhase) : dayPhase < .05f ? 1 - dayPhase / .05f : 0;
        bool moon = World.MoonUpAt(time);
        float arc = World.SkyArcAt(time);
        float height = MathF.Sin(MathF.PI * arc);
        // The light turns a little every frame; the engine redraws the sun's shadows every frame anyway.
        float swing = Mathf.DegToRad(Mathf.Lerp(-75, 75, arc)), elevation = Mathf.DegToRad(Mathf.Lerp(18, moon ? 35 : 40, height));
        var heading = NoonHeading.Rotated(swing);
        var lightDirection = Basis.LookingAt(new Vector3(heading.X * MathF.Cos(elevation), -MathF.Sin(elevation), heading.Y * MathF.Cos(elevation)), Vector3.Up);
        sun.Basis = lightDirection;
        // The moon waxes and wanes over the month: a full moon lights the night sky, a new moon leaves only firelight to see by.
        float moonlight = World.MoonlightAt(time);
        sun.LightEnergy = (moon ? MoonEnergy * moonlight : Mathf.Lerp(DaySun, .08f, night)) * Mathf.SmoothStep(0, .25f, height);
        // A low sun gives warm light at sunrise and sunset.
        float low = moon ? 0 : 1 - Mathf.SmoothStep(.1f, .45f, height);
        var lightColor = NoonLight.Lerp(LowSunLight, low).Lerp(MoonLight, night);
        sun.LightColor = lightColor;
        var ambient = DayAmbient.Lerp(LowSunAmbient, low * .6f).Lerp(NightAmbient, night);
        // Sky light from above, and below it that light bounced off the grass; each write rebuilds the sky's light, so a steady sky gets none.
        if (ambient != skyAmbient) {
            skyAmbient = ambient;
            var bounce = ambient * DayGround;
            skylight.SkyTopColor = ambient; skylight.SkyHorizonColor = ambient;
            skylight.GroundHorizonColor = bounce; skylight.GroundBottomColor = bounce;
        }
        if (previewSky != null && FirstPerson)
            previewSky.Update(lightDirection, ambient * DayGround, lightColor, night, low, height, moon, moonlight);
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

    private bool BehindCamera(Vector2 at, float height = 0) => FirstPerson && camera.IsPositionBehind(To3(at, height));

    private void PaintOverlay() {
        if (EnvironmentReview) return;
        if (FirstPerson && MouseLook) {
            var center = GetViewport().GetVisibleRect().GetCenter();
            overlay.DrawCircle(center, 4, new Color(.12f, .18f, .15f, .9f), antialiased: true);
            overlay.DrawCircle(center, 2, TagText, antialiased: true);
        }
        if (Underground) {
            // Outline the center tile where a pipe can meet each building, even before it is fitted out.
            foreach (var building in known) {
                if (building.Kind != ThingKind.Pump && !Plumbing.HasPlumbing(building.Kind)) continue;
                var tileCenter = Terrain.TileAt(building.Pos);
                var top = OnScreen(tileCenter);
                var center = OnScreen(tileCenter, PipeGround.FloorY);
                if ((building.Kind != ThingKind.Pump && !building.ShowsPlumbing) || !World.SteelPipes.Laid(tileCenter))
                    overlay.DrawDashedLine(top, center, new Color("75b5ffb0"), 1.5f, 4, true);
                var a = (screenX + screenZ) * .42f;
                var b = (screenX - screenZ) * .42f;
                var inlet = new Color("78b6ff");
                overlay.DrawLine(center + a, center + b, inlet, 1.5f, true);
                overlay.DrawLine(center + b, center - a, inlet, 1.5f, true);
                overlay.DrawLine(center - a, center - b, inlet, 1.5f, true);
                overlay.DrawLine(center - b, center + a, inlet, 1.5f, true);
            }
            if (PipePreviewTile == null && PointedPipe is Vector2I tile) Tag(Screen(tile, PipeGround.FloorY + .28f), "Steel pipe · E to inspect");
            return;
        }
        if (World == null) return;
        if (AnimationReview) { PaintReview(); return; }
        foreach (var kind in World.StationKinds)
            foreach (var t in World.ThingsOf(kind)) {
                if (!t.Processing || !World.Sees(t.Pos) || BehindCamera(t.Pos) || !World.Data.Recipes.TryGetValue(t.Recipe, out var r)) continue;
                var p = Screen(t.Pos);
                overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56, 5)), BarTrack);
                overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56 * Math.Min(1, t.Progress / r.Seconds), 5)), BarFill);
            }
        // Forge heat under each reinforced furnace, with a tick at the clear bar steel needs.
        foreach (var t in World.ThingsOf(ThingKind.Reinforced)) {
            if (!World.Sees(t.Pos) || BehindCamera(t.Pos)) continue;
            var p = Screen(t.Pos) + new Vector2(-28, 18);
            overlay.DrawRect(new Rect2(p, new Vector2(56, 5)), BarTrack);
            overlay.DrawRect(new Rect2(p, new Vector2(56 * Math.Clamp(t.Heat / 100, 0, 1), 5)), t.Heat >= World.SteelHeat ? HeatReady : HeatLow);
            overlay.DrawLine(p + new Vector2(56 * World.SteelHeat / 100, -2), p + new Vector2(56 * World.SteelHeat / 100, 7), HeatTick, 2);
        }
        if (Placing != null) return;
        // Only the hovered object is named; the side panel already names the selection.
        var hovered = World.Find(Hover);
        if (hovered != null) {
            float top = 0;
            foreach (var hit in HitsOf(hovered)) top = Math.Max(top, hit.Top);
            Tag(Screen(hovered.Kind == ThingKind.Water ? Terrain.WaterPoint(ToWorld(Mouse)) : hovered.Pos, top + .2f) - new Vector2(0, 8), hovered.Kind == ThingKind.Water ? "Collect water" : World.Data.Name(hovered.Kind));
        }
        foreach (var w in World.Workers) {
            if (!World.Sees(w) || w.Id != HoverWorker && w.Id != Selection.Worker) continue;
            var feet = WorkerFeet(w);
            var at = new Vector2(feet.X, feet.Z);
            if (!BehindCamera(at, feet.Y + 2.05f)) Tag(Screen(at, feet.Y + 2.05f), w.Name);
        }
    }
}

/// <summary>Draws the 2D overlay by overriding <c>_Draw</c>: a C# handler on the Draw signal allocates on every emission.</summary>
public partial class Overlay : Node2D {
    public Action Paint = () => { };
    public override void _Draw() => Paint();
}
