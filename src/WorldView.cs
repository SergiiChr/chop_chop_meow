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
        warmStage = new Node3D(); WorldModels.Warm(warmStage); scene.AddChild(warmStage);
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
        public ActorAction Action;
        public float Time;
        public float PreviousTime = -1;
        public bool Moving;
        // The action blended out of after a change, where it was, and how far the blend into the new one has gone.
        public ActorAction From;
        public float FromTime;
        public bool FromMoving;
        public float Blend = 1;
        public void Update(Vector2 position, ActorAction action, Vector2 facingTarget, float dt) {
            float moved = position.DistanceTo(Position);
            bool moving = moved > .001f;
            float yaw = ActorAnimation.Yaw(moving ? position - Position : facingTarget - position, Yaw);
            Yaw = Mathf.LerpAngle(Yaw, yaw, Math.Min(1, dt * 14));
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
        float pitch = Mathf.DegToRad(AnimationReview ? ReviewPitch : Pitch), across = MathF.Cos(pitch) * Mathf.Sqrt2 / 2;
        var back = new Vector3(across, MathF.Sin(pitch), across);
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

    /// <summary>How far a world position lies outside the visible screen, in tiles; zero when on screen.</summary>
    public float OffScreen(Vector2 p) {
        var at = Screen(p); var rect = GetViewport().GetVisibleRect();
        var outside = new Vector2(Math.Max(0, Math.Max(rect.Position.X - at.X, at.X - rect.End.X)), Math.Max(0, Math.Max(rect.Position.Y - at.Y, at.Y - rect.End.Y)));
        return outside.Length() / PixelsPerUnit;
    }

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

    /// <summary>The screen rectangle of one hit cylinder, used to decide when a canopy hides something.</summary>
    private Rect2 ScreenRect(Vector2 at, Hit hit, float ppu) {
        var bottom = camera.UnprojectPosition(To3(at, hit.Bottom)); var top = camera.UnprojectPosition(To3(at, hit.Top));
        float half = hit.Radius * ppu;
        return new Rect2(bottom.X - half, top.Y - half * .5f, half * 2, bottom.Y - top.Y + half);
    }

    internal Prop? PropOf(int id) => props.GetValueOrDefault(id);

    internal bool TreeCanopyCoversThing(Thing tree, Thing thing) {
        SyncCamera();
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
        var head = camera.UnprojectPosition(To3(person, 1.2f));
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
        if (action == ActorAction.Idle && StationToHammer(World.Me) is Thing station) { action = ActorAnimation.StationAction(station.Kind); target = station.Pos; subject = station; }
        // A leaping wolf knocks the player over as its jaws reach them, before it lands; a pinned player stays down.
        var knocker = Knocking(World.Me);
        if (World.Mauled || knocker != null) {
            mauledFrom = (knocker ?? PinningWolf(World.Me.Id))?.Pos ?? (downedFor == 0 ? World.Player + Vector2.Down : mauledFrom);
            // Facing the wolf, the player goes over backwards with it on top.
            action = World.Dead ? ActorAction.Devoured : ActorAction.Mauled; target = mauledFrom; subject = null;
            downedFor += dt;
        } else downedFor = 0;
        player.Update(World.Player, action, target, dt);
        // A foot lands at each extreme of the stride.
        if (action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run && (player.Struck(.25f) || player.Struck(.75f))) Sounds?.Play(FootstepSound(World.Player));
        pulseRemaining = Math.Max(0, pulseRemaining - dt);
        string? tool = ActorAnimation.Tool(action) ?? (action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run ? HeldTool(SelectedTool, World.S.Backpack) : null);
        bool torch = World.Torch > 0 && !World.Dead;
        Pose(playerRig, player, Grade(tool, SelectedTool, World.S.Backpack), torch);
        Torch(playerRig, player, World.TorchLit, World.TorchFlare, null);
        if (downedFor > 0) KnockDown(playerRig, player, downedFor);
        playerRig.Root.Visible = (!World.Dead || World.Mauled) && World.Sheltered == 0;
        Work(player, subject);
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
            var work = ActorAnimation.WorkerAction(worker, assignment, walking);
            if (work is ActorAction.Hammer or ActorAction.Pack or ActorAction.Grind && assignment?.Processing != true && assignment?.Kind != ThingKind.Mine) work = ActorAction.Idle;
            // At a mine the worker faces the rock past the entrance, not the cart out front.
            // A protester turns the sign toward the camera.
            var facing = work == ActorAction.Protest ? worker.Pos + Vector2.One
                : work is ActorAction.Sit or ActorAction.Perch && World.LunchFire is Thing fire ? fire.Pos : work == ActorAction.Pack ? worker.Pos + Vector2.Down
                : assignment is { Kind: ThingKind.Mine } mine ? World.MineDoor(mine) * 2 - mine.Pos : assignment?.Pos ?? worker.Pos;
            state.Update(worker.Pos, work, facing, dt);
            Pose(entry.Rig, state, Grade(ActorAnimation.Tool(work), worker.Tool, assignment?.Tools));
            entry.Rig.Root.Visible = World.Sees(worker);
            Fade(entry.Parts, entry.Rig.Root, TripFade(World.TripLeg(worker)));
            Work(state, work is ActorAction.Idle or ActorAction.Walk or ActorAction.Carry or ActorAction.Sit or ActorAction.Perch or ActorAction.Protest ? null : assignment);
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
            foreach (var t in World.ThingsOf(kind)) if (World.Worked(t) && t.Processing && World.Near(t) && !World.CrewHammers(t)) return t;
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
            if (action == ActorAction.Idle && StationToHammer(p) is Thing station) { action = ActorAnimation.StationAction(station.Kind); facing = station.Pos; subject = station; }
            var knocker = Knocking(p);
            if (p.Mauled || knocker != null) {
                action = p.RespawnIn > 0 ? ActorAction.Devoured : ActorAction.Mauled;
                facing = (knocker ?? PinningWolf(p.Id))?.Pos ?? p.Pos + Vector2.Down;
                other.DownedFor += dt;
            } else other.DownedFor = 0;
            state.Update(p.Pos, action, facing, dt);
            other.PulseRemaining = Math.Max(0, other.PulseRemaining - dt);
            if (action is ActorAction.Walk or ActorAction.Jog or ActorAction.Run && (state.Struck(.25f) || state.Struck(.75f))) Sounds?.Play(FootstepSound(p.Pos), p.Pos);
            Pose(other.Rig, state, Grade(ActorAnimation.Tool(action) ?? (action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run ? HeldTool(p.Held, p.Backpack) : null), p.Held, p.Backpack), p.Torch > 0 && p.RespawnIn <= 0);
            Torch(other.Rig, state, p.TorchLit, p.TorchFlare, p.Pos);
            if (other.DownedFor > 0) KnockDown(other.Rig, state, other.DownedFor);
            other.Rig.Root.Visible = (p.RespawnIn <= 0 || p.Mauled) && p.Sheltered == 0 && World.Sees(p.Pos);
            Work(state, subject);
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

    private static void Pose(ActorRig rig, ActorState state, string? tool, bool torch = false) {
        rig.Root.Position = To3(state.Position);
        rig.Root.Rotation = new Vector3(0, state.Yaw, 0);
        var pose = state.Pose;
        // Between actions the torch arm stays raised with the forearm level, which is the angle the flame is built for.
        if (torch && state.Action is ActorAction.Idle or ActorAction.Walk or ActorAction.Jog or ActorAction.Run) pose = pose with { RaiseL = 30, OutL = 4, ElbowL = 60 };
        rig.Show(pose, tool, state.Action == ActorAction.Carry, state.Action == ActorAction.Perch, torch);
    }

    /// <summary>Shows a bench slot's model of the slotted tool's grade: the first child for the lower grade, the second for steel.</summary>
    private static void ShowSlotted(Node3D slot, string? tool) {
        slot.Visible = tool != null;
        if (tool == null) return;
        bool steel = tool.StartsWith("steel");
        slot.GetChild<Node3D>(0).Visible = !steel;
        slot.GetChild<Node3D>(1).Visible = steel;
    }

    /// <summary>Whether working on a thing hammers in a mine's frame and rails rather than digging.</summary>
    private static bool Fitting(Thing? target) => target is { Kind: ThingKind.Mine, MineStage: MineStage.Supplied };

    /// <summary>Feeds an actor's work into the object it works on: bellows travel, impact shakes, chips and sparks.</summary>
    private void Work(ActorState actor, Thing? subject) {
        if (subject == null) return;
        var action = actor.Action;
        // The bellows board follows the lean of the person pressing it.
        if (action == ActorAction.Pump)
            pumping[subject.Id] = Math.Max(pumping.GetValueOrDefault(subject.Id), Math.Clamp((ActorAnimation.PoseAt(action, actor.Time, false).Lean - 16) / 16, 0, 1));
        if (action == ActorAction.Grind) grinding.Add(subject.Id);
        float impact = action switch {
            ActorAction.Chop or ActorAction.SteelChop => .52f,
            ActorAction.Mine or ActorAction.SteelMine => .55f,
            ActorAction.Hammer => .5f,
            ActorAction.Pump => .35f,
            ActorAction.Gather => .1f,
            _ => -1
        };
        if (impact < 0 || !actor.Struck(impact) || !props.TryGetValue(subject.Id, out var prop)) return;
        // What is out of sight is drawn as it was last seen, so nothing there shakes or throws chips; the blow is still heard.
        bool inSight = World.Sees(subject.Pos);
        if (inSight) prop.Shake = 1;
        // A bench knocks wood on wood, so it borrows the axe's knock.
        string? sound = action is ActorAction.Mine or ActorAction.SteelMine ? "mine_ore"
            : action == ActorAction.Pump ? "bellows_pump"
            : subject.Kind == ThingKind.Anvil ? "anvil_work"
            : subject.Kind is ThingKind.Tree or ThingKind.Bench ? "chop_tree"
            : null;
        if (sound != null) Sounds?.Play(sound, subject.Pos);
        if (!inSight) return;
        var impactAt = World.GatherAt(subject, actor.Position);
        var toward = (actor.Position - impactAt).Normalized();
        var side = To3(impactAt + toward * .35f);
        switch (subject.Kind) {
            case ThingKind.Tree:
                Burst(side + Vector3.Up * 1.1f, Mat("d9b07a"), 7, 2.6f, .55f, 9, .06f);
                Burst(To3(subject.Pos, 3.6f), Mat("5d8a3c"), 3, .6f, 1.8f, 1.2f, .09f);
                break;
            case ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin or ThingKind.Mine:
                var color = subject.Kind is ThingKind.Stone or ThingKind.Mine ? "9a9d99" : subject.Kind == ThingKind.Iron ? "a35a2e" : subject.Kind == ThingKind.Tin ? "b9bbb4" : "26282b";
                Burst(side + Vector3.Up * .5f, Mat(color), 6, 2.4f, .5f, 9, .07f);
                Burst(side + Vector3.Up * .4f, GatherDust, 3, .5f, .9f, -.3f, .16f, 1.5f);
                if (action == ActorAction.SteelMine) Burst(side + Vector3.Up * .5f, Sparks, 5, 3.2f, .3f, 4, .025f);
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

    /// <summary>Seconds after it is felled when a tree's trunk and crown have faded away, leaving the stump.</summary>
    private const float TreeGone = 1.7f;
    private readonly HashSet<int> seen = new();
    private readonly List<Thing> known = new();
    private readonly List<int> gone = new();
    private SaveState? builtFor;
    private bool loadingProps;
    private int thingsCounted = -1;
    private static readonly Vector2 Nowhere = new(-1e6f, -1e6f);
    // Where the player stood when things were last drawn.
    private Vector2 sawFrom = Nowhere;
    /// <summary>Seconds a thing going out of sight is moved on by, enough to finish whatever it was doing: a tree falling, a chip flying, a door swinging.</summary>
    private const float SettleSeconds = 10;
    /// <summary>
    /// Whether a spot was in the player's sight when things were last drawn, so that a change there happened in front of them.
    /// What changed out of sight, or is first seen as the player walks up, shows as it now is without playing the change.
    /// </summary>
    private bool Watched(Vector2 at) => at.DistanceTo(sawFrom) <= World.Data.SightRadius;
    private void SyncThings(float dt) {
        // Every model is built while a world loads, hidden until the player first sees it, so exploring builds nothing mid-game.
        // Another world, loaded or started, starts from fresh models: the old ones carry the state of things with the same ids.
        if (builtFor != World.S) {
            builtFor = World.S;
            thingsCounted = -1;
            sawFrom = Nowhere;
            foreach (var (_, old) in props) old.Root.QueueFree();
            props.Clear();
            loadingProps = true;
            foreach (var t in World.Things) Show(PropFor(t), false);
            loadingProps = false;
        }
        seen.Clear();
        bool everything = filming || EnvironmentReview && EnvironmentCenter.HasValue;
        if (everything) { known.Clear(); known.AddRange(World.Things); } else World.CollectKnown(known);
        bool dark = night > .45f;
        foreach (var t in known) {
            seen.Add(t.Id);
            var prop = PropFor(t);
            // Most of an explored valley is out of sight, drawn as it was last seen. That only changes when it is seen again, by this player or another,
            // or when night falls or lifts, so such a thing is settled once and then left alone: only what is in sight is drawn every frame.
            bool live = everything || World.Sees(t.Pos);
            var look = (t.Remaining, t.Progress, t.MineStage, t.FarmStage, dark);
            if (!live && prop.Settled && prop.Look == look && prop.At == t.Pos) continue;
            Show(prop, true);
            if (prop.At != t.Pos) { prop.At = t.Pos; prop.Root.Position = To3(t.Pos); }
            Animate(prop, t, live ? dt : SettleSeconds, live);
            prop.Settled = !live; prop.Look = look;
        }
        sawFrom = World.Player;
        foreach (var (id, prop) in props) if (!seen.Contains(id)) Show(prop, false);
        // Things leave the world rarely, so their models are looked for only when the count changes.
        if (thingsCounted != World.Things.Count) {
            thingsCounted = World.Things.Count;
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
        bool appeared = !loadingProps && Watched(t.Pos);
        if (prop == null && appeared && (t.Kind == ThingKind.Crate || World.Data.Buildings.ContainsKey(t.Kind))) Plop(t.Pos, World.Footprint(t.Kind));
        if (prop != null) prop.Root.QueueFree();
        prop = WorldModels.Build(t);
        scene.AddChild(prop.Root);
        props[t.Id] = prop;
        prop.At = t.Pos; prop.Root.Position = To3(t.Pos);
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
    /// </summary>
    private void Animate(Prop prop, Thing t, float dt, bool live) {
        bool first = !prop.Seen;
        prop.Seen = true;
        prop.Shake = Math.Max(0, prop.Shake - dt * 2.5f);
        float wobble = prop.Shake * MathF.Sin(Elapsed * 34);
        float flicker = MathF.Sin(Elapsed * 13 + t.Id) * .5f + MathF.Sin(Elapsed * 7.3f + t.Id * 3) * .5f;
        switch (prop) {
            case TreeProp tree:
                var sway = tree.Sway;
                if (t.Remaining > 0) {
                    // A forest in sight is mostly trees standing still, so one is written to only as it changes.
                    if (first || tree.Fall >= 0) { tree.Fall = -1; sway.Visible = true; tree.Stump.Visible = false; tree.Swayed = true; }
                    if (prop.Shake > 0 || tree.Swayed) {
                        sway.RotationDegrees = new Vector3(wobble * 3, 0, prop.Shake * 2 * MathF.Cos(Elapsed * 29));
                        tree.Swayed = prop.Shake > 0;
                    }
                    break;
                }
                // A tree felled in front of the player topples, fades away and leaves its stump; one felled out of sight is a stump when seen again.
                if (tree.Fall < 0) tree.Fall = first || !live || !Watched(t.Pos) ? 9 : 0;
                else if (tree.Fall >= TreeGone) break;
                float before = tree.Fall; tree.Fall += dt;
                float drop = Math.Min(1, tree.Fall / 1.1f);
                sway.RotationDegrees = new Vector3(90 * drop * drop, 0, 0);
                if (live && before < 1.1f && tree.Fall >= 1.1f) Burst(prop.Root.Position + sway.GlobalBasis.Y * 3.5f + Vector3.Up * .3f, FallDust, 8, 1.2f, 1.1f, -.2f, .3f, 1.5f);
                SetFade(tree.SwayParts, Math.Clamp((tree.Fall - 1.2f) / .5f, 0, 1));
                sway.Visible = tree.Fall < TreeGone;
                tree.Stump.Visible = true;
                break;
            case LooseProp chip when chip.Flight < WorldModels.ChipFlight:
                // An arc from the rock with a hop at the end, spinning as it goes.
                chip.Flight += dt;
                float flown = Math.Min(1, chip.Flight / WorldModels.ChipFlight);
                prop.Body.Position = chip.From * (1 - flown) + Vector3.Up * (MathF.Sin(flown * MathF.PI) * .6f + MathF.Abs(MathF.Sin(flown * MathF.PI * 3)) * .08f * flown);
                prop.Body.Rotation = new Vector3(flown * 9, flown * 4, 0) * (1 - flown);
                break;
            case Prop when t.IsLoose || t.Kind is ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin or ThingKind.Leaves:
                prop.Body.Visible = t.Remaining > 0;
                prop.Body.Scale = Vector3.One * (.55f + .45f * Math.Clamp(WorldModels.Fullness(t), 0, 1));
                prop.Body.Position = new Vector3(wobble * .03f, 0, 0);
                break;
            case CarcassProp carcass:
                // Thrown, the carcass covers the knockback while it is in the air and stays where it lands.
                float since = t.Progress - carcass.HitIn, dying = Math.Clamp(since / WolfModel.FallSeconds, 0, 1), thrown = Math.Min(1, dying / WolfModel.ThrowShare);
                carcass.Body.Position = carcass.From * (1 - thrown * (2 - thrown));
                carcass.Wolf.Fall(dying);
                // Butchering flattens the carcass while the blood pool spreads out from under it.
                carcass.Wolf.Root.Scale = new Vector3(1, .6f + .4f * WorldModels.Fullness(t), 1);
                carcass.Wolf.Root.Position = new Vector3(wobble * .03f, 0, 0);
                carcass.Pool.Scale = Vector3.One * (.2f + .8f * Ease(dying)) * (1.25f - .25f * WorldModels.Fullness(t));
                break;
            case BushProp bush:
                bush.Fruit.Visible = t.Remaining > 0;
                prop.Body.Scale = new Vector3(1 + wobble * .04f, 1 - wobble * .03f, 1 + wobble * .04f);
                break;
            case FurnaceProp furnace:
                if (World.Sees(t.Pos)) WorldModels.SyncFuel(furnace, t, World.Data);
                furnace.Glow.Visible = t.Processing;
                furnace.Light.LightEnergy = t.Processing ? (1.1f + flicker * .25f) * (.5f + night) : 0;
                Smoke(furnace, t, t.Processing, dt, FurnaceSmoke);
                break;
            case StoveProp stove:
                // The fire only burns for recipes that take fuel; the mortar's work and kneading dough are cold.
                bool cooking = t.Processing && World.Data.Recipes.TryGetValue(t.Recipe, out var cooked) && cooked.Burns;
                stove.Glow.Visible = cooking;
                stove.Light.LightEnergy = cooking ? (.9f + flicker * .25f) * (.5f + night) : 0;
                if (stove.Mortar != null && World.Sees(t.Pos)) stove.Mortar.Visible = t.Tools != null && World.BenchTool(t, "mortar") != null && !grinding.Contains(t.Id);
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
                if (!World.Sees(t.Pos)) break;
                ShowSlotted(bench.Chisel, World.BenchTool(t, "chisel"));
                ShowSlotted(bench.Hammer, World.BenchTool(t, "hammer"));
                prop.Body.Position = new Vector3(wobble * .02f, 0, 0);
                break;
            case PileProp pile:
                // Remembered piles carry no inventory, so they keep the stacks they had when last seen.
                if (!World.Sees(t.Pos)) break;
                WorldModels.SyncPile(pile, t, World.Data);
                break;
            case FarmProp farm:
                if (!World.Sees(t.Pos)) break;
                int look = World.CropLook(t, World.GrowSeconds);
                farm.Crops[0].Visible = look > 0;
                for (int i = 1; i < farm.Crops.Length; i++) farm.Crops[i].Visible = look == i + 1;
                break;
            case PackerProp packer:
                // Remembered stations carry no inventory, so they keep the crates they had when last seen.
                if (!World.Sees(t.Pos)) break;
                for (int i = 0; i < packer.Crates.Length; i++) packer.Crates[i].Visible = i < t.Inventory["crate"];
                packer.Packing.Visible = t.Processing;
                // The heap grows a pile for each third of a crate's worth.
                packer.Stock.Fill(t.Packs, t.Packs == "" ? 0 : t.Inventory[t.Packs], World.Data.CrateCapacity / 3, World.Data);
                break;
            case RackProp rack:
                if (!World.Sees(t.Pos)) break;
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
                if (!World.Sees(t.Pos)) break;
                int count = t.Inventory.Count;
                if (!first && Watched(t.Pos) && count != box.LastCount) box.Puff = .9f;
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
                // The door stands open for someone at it and shuts behind them once they are inside.
                bool near = live && AnyPlayerOutsideNear(t.Pos, 2.6f);
                // Running inside from a hunting pack slams the door instead of swinging it open.
                if (dt > 0 && near && hut.Open == 0) Sounds?.Play(World.Wolves.Count > 0 ? "hut_door_escaping" : "hut_door_normal", t.Pos);
                hut.Open = Mathf.MoveToward(hut.Open, near ? 1 : 0, Math.Max(dt, .016f) * 2.5f);
                hut.Door.RotationDegrees = new Vector3(0, -100 * Ease(hut.Open), 0);
                bool lit = night > .45f;
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
                bool mining = finished && World.Sees(t.Pos) && Underground(t.Id);
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
    private bool AnyPlayerOutsideNear(Vector2 at, float radius) {
        foreach (var p in World.Players) if (p.RespawnIn <= 0 && p.Sheltered == 0 && p.Pos.DistanceTo(at) < radius) return true;
        return false;
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
        var bounds = GetViewport().GetVisibleRect().Grow(300);
        float ppu = PixelsPerUnit;
        // The view never draws ground larger than it is, so nothing further from the player than the far corner of those bounds can lie inside them:
        // that rules out most of an explored valley before the camera is asked where each thing is on screen.
        var size = bounds.Size;
        float reach = EnvironmentReview ? float.MaxValue : Math.Max(Center.X + 300, size.X - Center.X - 300) / ppu + Math.Max(Center.Y + 300, size.Y - Center.Y - 300) / ppu;
        float reach2 = reach * reach;
        var focus = World.Player;
        structures.Clear();
        foreach (var kind in World.Structures)
            foreach (var o in World.ThingsOf(kind))
                if (o.Pos.DistanceSquaredTo(focus) <= reach2 && seen.Contains(o.Id) && bounds.HasPoint(camera.UnprojectPosition(To3(o.Pos)))) structures.Add(o);
        foreach (var t in World.ThingsOf(ThingKind.Tree)) {
            if (t.Remaining <= 0 || t.Pos.DistanceSquaredTo(focus) > reach2 || !seen.Contains(t.Id) || props.GetValueOrDefault(t.Id) is not TreeProp tree) continue;
            if (!bounds.HasPoint(camera.UnprojectPosition(To3(t.Pos)))) continue;
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
                if (model.HitIn == 0) { model.Recoiling = WolfModel.RecoilSeconds; model.KnockFrom = model.Knock; FurAndEmbers(To3(at, .6f)); }
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
