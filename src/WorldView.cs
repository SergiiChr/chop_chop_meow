using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// Renders the simulation as a 3D scene under an orthographic isometric camera.
/// World (x, y) maps to 3D (x, 0, y); one tile is one unit and an adult is about 1.65 units tall.
/// </summary>
public partial class WorldView : Node3D
{
    public World World = null!;
    public int Selected;
    public int Hover;
    public int SelectedWorker;
    public int HoverWorker;
    public string BuildMode = "";
    /// <summary>The crate the player is lifting with a held right click, and how far along the lift is from 0 to 1.</summary>
    public int Lifting;
    public float LiftProgress;
    public Vector2 Mouse;
    public float Zoom = 1;
    public bool Gathering;
    public Thing? GatheringTarget;
    public string? SelectedTool;
    public bool Paused;
    public bool AnimationReview;
    public float Elapsed;
    public bool PanelOpen;
    public Sounds? Sounds;
    // A 30° pitch keeps the old 2:1 isometric footprint; this ortho height matches the old 85 px actor scale at zoom 1.
    private const float Pitch = 30, BaseSize = 14.5f;
    private Camera3D camera = null!;
    private DirectionalLight3D sun = null!;
    private Godot.Environment environment = null!;
    private Node3D scene = null!;
    private Node2D overlay = null!;
    private readonly Dictionary<int, Prop> props = new();
    private readonly ActorState player = new();
    private ActorRig playerRig = null!;
    private readonly Dictionary<int, (ActorRig Rig, ActorState State)> crew = new();
    private readonly Dictionary<Wolf, WorldModels.WolfModel> wolves = new();
    private readonly Dictionary<TradeOrder, WorldModels.CartModel> carts = new();
    private readonly Dictionary<int, float> pumping = new();
    private readonly Dictionary<string, Prop> ghosts = new();
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
    private readonly ShaderMaterial fog = new() { Shader = new Shader { Code = FogShader } };
    private Image explored = null!;
    private ImageTexture exploredTexture = null!;
    private int exploredRevision = -1;
    // Drawn over every mesh: black where never explored, a grey wash over remembered ground, clear inside the sight circle.
    // Texel centres sit on whole tiles, matching where the simulation measures sight.
    private const string FogShader = """
        shader_type spatial;
        render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
        uniform sampler2D explored : filter_linear, repeat_disable;
        uniform vec2 player;
        uniform float radius;
        uniform float map_size;
        uniform vec3 memory : source_color;
        void fragment() {
            vec2 at = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz;
            float seen = texture(explored, (at + 0.5) / map_size).r;
            float live = 1.0 - smoothstep(radius - 1.5, radius, distance(at, player));
            ALBEDO = memory * seen;
            ALPHA = (1.0 - live) * mix(1.0, 0.62, seen);
        }
        """;

    private static Vector3 To3(Vector2 p, float y = 0) => new(p.X, y, p.Y);

    public override void _Ready()
    {
        camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = BaseSize, Near = 1, Far = 200, Current = true };
        AddChild(camera);
        environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("879c69"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("c7d3dc"),
            AmbientLightEnergy = .32f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        // Light from the upper left and slightly behind, like the illustrated references, so shadows fall to the right.
        sun = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMaxDistance = 90, ShadowBlur = 1.5f, LightEnergy = .8f };
        sun.Basis = Basis.LookingAt(new Vector3(1, -1.25f, -.45f), Vector3.Up);
        AddChild(sun);
        scene = new Node3D(); AddChild(scene);
        explored = Image.CreateFromData(World.MapSize, World.MapSize, false, Image.Format.L8, World.S.Explored);
        exploredTexture = ImageTexture.CreateFromImage(explored);
        fog.SetShaderParameter("explored", exploredTexture);
        fog.SetShaderParameter("map_size", World.MapSize);
        GetTree().NodeAdded += Fog;
        BuildGround(scene);
        BuildEffects(scene);
        playerRig = new ActorRig(ActorSkin.Player); scene.AddChild(playerRig.Root);
        selectRing = Ring(new Color(.95f, .82f, .4f, .85f));
        hoverRing = Ring(new Color(1, .95f, .75f, .55f));
        workerRing = Ring(new Color(1, .8f, .3f, .8f));
        playerRing = Ring(new Color(.95f, .88f, .57f, .6f));
        placeTile = Add(scene, new PlaneMesh { Size = new Vector2(1.4f, 1.4f) }, GhostValid, Vector3.Zero);
        placeTile.Visible = false;
        player.Position = World.Player;
        overlay = new Node2D(); AddChild(overlay);
        overlay.Draw += PaintOverlay;
        World.PlayerActed += PlayerActed;
    }

    private MeshInstance3D Ring(Color color)
    {
        var ring = Torus(scene, .9f, 1, Vector3.Zero, Flat(color));
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        ring.Visible = false;
        return ring;
    }

    public override void _ExitTree() { World.PlayerActed -= PlayerActed; GetTree().NodeAdded -= Fog; }

    /// <summary>Every mesh in the scene, including ones spawned later, takes the fog as its overlay.</summary>
    private void Fog(Node node)
    {
        if (node is GeometryInstance3D geometry && scene.IsAncestorOf(geometry)) geometry.MaterialOverlay = fog;
    }

    private void UpdateFog()
    {
        if (AnimationReview) { fog.SetShaderParameter("radius", 1e6f); return; }
        fog.SetShaderParameter("player", World.Player);
        fog.SetShaderParameter("radius", World.Data.SightRadius);
        fog.SetShaderParameter("memory", new Color(.38f, .4f, .42f).Lerp(new Color(.07f, .08f, .11f), night));
        if (exploredRevision == World.ExploredRevision) return;
        exploredRevision = World.ExploredRevision;
        explored.SetData(World.MapSize, World.MapSize, false, Image.Format.L8, World.S.Explored);
        exploredTexture.Update(explored);
    }

    private void PlayerActed(ActorAction action, Vector2 target)
    {
        pulseAction = action; pulseTarget = target; pulseRemaining = ActorAnimation.Duration(action);
        player.Time = 0;
    }

    public ActorAction PlayerAnimation => player.Action;
    public float PlayerAnimationTime => player.Time;
    public ActorAction WorkerAnimation(int id) => crew.TryGetValue(id, out var entry) ? entry.State.Action : ActorAction.Idle;

    private sealed class ActorState
    {
        public Vector2 Position;
        public float Yaw;
        public ActorAction Action;
        public float Time;
        public float PreviousTime = -1;
        public bool Moving;
        public void Update(Vector2 position, ActorAction action, Vector2 facingTarget, float dt)
        {
            Moving = position.DistanceSquaredTo(Position) > .000001f;
            float yaw = ActorAnimation.Yaw(Moving ? position - Position : facingTarget - position, Yaw);
            Yaw = Mathf.LerpAngle(Yaw, yaw, Math.Min(1, dt * 14));
            Position = position;
            if (Action != action) { Action = action; Time = 0; PreviousTime = -1; }
            else { PreviousTime = Time; Time += dt; }
        }
        /// <summary>True on the frame an action loop passes its impact moment.</summary>
        public bool Struck(float at)
        {
            if (PreviousTime < 0) return false;
            float before = ActorAnimation.Phase(Action, PreviousTime), now = ActorAnimation.Phase(Action, Time);
            return before <= now ? before < at && at <= now : at > before || at <= now;
        }
    }

    // ---------- Camera and picking ----------

    public Vector2 Center => GetViewport().GetVisibleRect().Size * new Vector2(PanelOpen ? .38f : .5f, .52f);

    /// <summary>Keeps the camera on the player; called before any projection so input sees the current frame.</summary>
    private void SyncCamera()
    {
        var size = GetViewport().GetVisibleRect().Size;
        camera.Size = (AnimationReview ? 9.5f : BaseSize) / Zoom;
        float pitch = Mathf.DegToRad(Pitch), across = MathF.Cos(pitch) * Mathf.Sqrt2 / 2;
        var back = new Vector3(across, MathF.Sin(pitch), across);
        var target = AnimationReview ? ReviewCenter : To3(World.Player);
        camera.Basis = Basis.LookingAt(-back, Vector3.Up);
        camera.Position = target + back * 60;
        float unitsPerPixel = camera.Size / Math.Max(1, size.Y);
        camera.HOffset = (.5f - Center.X / Math.Max(1, size.X)) * size.X * unitsPerPixel;
        camera.VOffset = (Center.Y / Math.Max(1, size.Y) - .5f) * size.Y * unitsPerPixel;
    }

    private float PixelsPerUnit => GetViewport().GetVisibleRect().Size.Y / camera.Size;
    public Vector2 Screen(Vector2 p) => Screen(p, 0);
    public Vector2 Screen(Vector2 p, float height) { SyncCamera(); return camera.UnprojectPosition(To3(p, height)); }

    /// <summary>How far a world position lies outside the visible screen, in tiles; zero when on screen.</summary>
    public float OffScreen(Vector2 p)
    {
        var at = Screen(p); var rect = GetViewport().GetVisibleRect();
        var outside = new Vector2(Math.Max(0, Math.Max(rect.Position.X - at.X, at.X - rect.End.X)), Math.Max(0, Math.Max(rect.Position.Y - at.Y, at.Y - rect.End.Y)));
        return outside.Length() / PixelsPerUnit;
    }

    public Vector2 ToWorld(Vector2 screen)
    {
        SyncCamera();
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        if (Math.Abs(direction.Y) < 1e-5f) return World.Player;
        var hit = origin + direction * (-origin.Y / direction.Y);
        return new Vector2(hit.X, hit.Z);
    }

    private Hit[] HitsOf(Thing t) => props.TryGetValue(t.Id, out var prop) && prop.Kind == t.Kind ? prop.Hits : WorldModels.Hits(t);

    private static bool RayCylinder(Vector3 origin, Vector3 direction, Vector2 center, Hit hit, out float distance)
    {
        distance = float.MaxValue;
        float r2 = hit.Radius * hit.Radius;
        if (Math.Abs(direction.Y) > 1e-5f)
        {
            float cap = (hit.Top - origin.Y) / direction.Y;
            var p = origin + direction * cap;
            if (cap > 0 && (p.X - center.X) * (p.X - center.X) + (p.Z - center.Y) * (p.Z - center.Y) <= r2) distance = cap;
        }
        float ox = origin.X - center.X, oz = origin.Z - center.Y;
        float a = direction.X * direction.X + direction.Z * direction.Z, b = 2 * (ox * direction.X + oz * direction.Z), c = ox * ox + oz * oz - r2;
        float disc = b * b - 4 * a * c;
        if (a > 1e-8f && disc >= 0)
        {
            float side = (-b - MathF.Sqrt(disc)) / (2 * a), y = origin.Y + direction.Y * side;
            if (side > 0 && y >= hit.Bottom && y <= hit.Top && side < distance) distance = side;
        }
        return distance < float.MaxValue;
    }

    private bool RayHits(Vector2 screen, Vector2 at, IEnumerable<Hit> hits, out float distance)
    {
        var origin = camera.ProjectRayOrigin(screen); var direction = camera.ProjectRayNormal(screen);
        distance = float.MaxValue;
        foreach (var hit in hits)
            if (RayCylinder(origin, direction, at, hit, out float d)) distance = Math.Min(distance, d);
        return distance < float.MaxValue;
    }

    /// <summary>The visible object nearest the camera under the cursor, tested against the models' hit cylinders.</summary>
    public Thing? Pick(Vector2 screen)
    {
        SyncCamera();
        Thing? best = null, crown = null; float nearest = float.MaxValue, nearestCrown = float.MaxValue;
        // Hits follow what is drawn, including memories, but only things that still exist can be picked.
        foreach (var known in World.Known)
        {
            if (known.Remaining <= 0 && known.IsResource && known.Kind != "berries" || World.Find(known.Id) is not Thing t) continue;
            var hits = HitsOf(known);
            // A tree's first hit is its trunk; a crown only wins when nothing else is under the cursor, so things behind it stay clickable.
            if (t.Kind == "tree" && !RayHits(screen, t.Pos, hits.Take(1), out _))
            {
                if (RayHits(screen, t.Pos, hits.Skip(1), out float c) && c < nearestCrown) { nearestCrown = c; crown = t; }
                continue;
            }
            if (RayHits(screen, t.Pos, hits, out float d) && d < nearest) { nearest = d; best = t; }
        }
        return best ?? crown;
    }

    private static readonly Hit[] PersonHit = { new(.38f, 0, 1.75f) };
    public Worker? PickWorker(Vector2 screen)
    {
        SyncCamera();
        Worker? best = null; float nearest = float.MaxValue;
        foreach (var w in World.S.Workers.Where(w => World.Sees(w)))
            if (RayHits(screen, w.Pos, PersonHit, out float d) && d < nearest) { nearest = d; best = w; }
        return best;
    }

    /// <summary>Screen rectangles of an object's hit cylinders, used to decide when a canopy hides something.</summary>
    private IEnumerable<Rect2> ScreenRects(Vector2 at, IEnumerable<Hit> hits)
    {
        float ppu = PixelsPerUnit;
        foreach (var hit in hits)
        {
            var bottom = camera.UnprojectPosition(To3(at, hit.Bottom)); var top = camera.UnprojectPosition(To3(at, hit.Top));
            float half = hit.Radius * ppu;
            yield return new Rect2(bottom.X - half, top.Y - half * .5f, half * 2, bottom.Y - top.Y + half);
        }
    }

    internal bool TreeCanopyCoversThing(Thing tree, Thing thing)
    {
        SyncCamera();
        return Covers(tree, thing);
    }

    private bool Covers(Thing tree, Thing thing)
    {
        // Depth decides which object is in front; projected volumes must then actually overlap.
        if (tree.X + tree.Y <= thing.X + thing.Y) return false;
        var thingRects = ScreenRects(thing.Pos, HitsOf(thing)).ToArray();
        return ScreenRects(tree.Pos, HitsOf(tree)).Any(a => thingRects.Any(b => a.Intersects(b)));
    }

    private bool TreeCoversPerson(Thing tree, Vector2 person)
    {
        if (tree.X + tree.Y <= person.X + person.Y) return false;
        var head = camera.UnprojectPosition(To3(person, 1.2f));
        return ScreenRects(tree.Pos, HitsOf(tree)).Any(r => r.HasPoint(head));
    }

    // ---------- Frame update ----------

    public override void _Process(double delta)
    {
        if (World == null) return;
        float dt = Paused ? 0 : Math.Min((float)delta, .1f);
        Elapsed += dt;
        SyncCamera();
        if (AnimationReview) { UpdateReview(); UpdateFog(); overlay.QueueRedraw(); return; }
        if (dt > 0) UpdateActors(dt);
        SyncThings(dt);
        if (dt > 0) { SyncWolves(dt); SyncCarts(dt); }
        UpdateEffects(dt);
        UpdateMarkers();
        UpdateLighting();
        UpdateFog();
        overlay.QueueRedraw();
    }

    private string? HeldTool() => SelectedTool switch
    {
        "iron_axe" or "iron_pick" or "steel_axe" or "steel_pick" when World.S.Backpack[SelectedTool] > 0 => SelectedTool,
        "wood_hammer" or "iron_hammer" or "steel_hammer" when World.S.Backpack[SelectedTool] > 0 => "hammer",
        _ => null
    };

    private void UpdateActors(float dt)
    {
        pumping.Clear();
        bool moving = World.Player.DistanceSquaredTo(player.Position) > .000001f;
        ActorAction action = ActorAnimation.PlayerAction(GatheringTarget?.Kind, Gathering, moving, World.S.Backpack, SelectedTool);
        Vector2 target = Gathering && GatheringTarget != null ? GatheringTarget.Pos : World.Player;
        Thing? subject = Gathering ? GatheringTarget : null;
        if (!moving && pulseRemaining > 0) { action = pulseAction; target = pulseTarget; subject = World.S.Things.FirstOrDefault(t => t.Pos == pulseTarget); }
        if (action == ActorAction.Idle)
        {
            var anvil = World.S.Things.FirstOrDefault(t => World.Worked(t) && t.Processing && World.Near(t) &&
                !World.S.Workers.Any(w => w.Assignment == t.Id && w.Pos.DistanceTo(t.Pos) < 2));
            if (anvil != null) { action = ActorAction.Hammer; target = anvil.Pos; subject = anvil; }
        }
        if (World.S.Mauled)
        {
            mauledFrom = World.S.Wolves.FirstOrDefault(w => w.Pinning)?.Pos ?? (downedFor == 0 ? World.Player + Vector2.Down : mauledFrom);
            // Facing away from the wolf at the throat lays the head toward it once the rig tips onto its back.
            action = World.Dead ? ActorAction.Devoured : ActorAction.Mauled; target = World.Player * 2 - mauledFrom; subject = null;
            downedFor += dt;
        }
        else downedFor = 0;
        player.Update(World.Player, action, target, dt);
        // A foot lands at each extreme of the stride.
        if (action == ActorAction.Walk && (player.Struck(.25f) || player.Struck(.75f))) Sounds?.Play(FootstepSound(World.Player));
        pulseRemaining = Math.Max(0, pulseRemaining - dt);
        string? tool = ActorAnimation.Tool(action) ?? (action is ActorAction.Idle or ActorAction.Walk ? HeldTool() : null);
        bool torch = World.S.Torch > 0 && !World.Dead;
        Pose(playerRig, player, tool, torch);
        playerRig.Flame.Visible = World.S.TorchLit;
        if (World.S.TorchLit)
        {
            float flicker = MathF.Sin(Elapsed * 13) * .5f + MathF.Sin(Elapsed * 29 + 1) * .5f, flare = World.TorchFlare / .5f;
            playerRig.Flame.Scale = new Vector3(1 + flicker * .1f, .85f + MathF.Abs(MathF.Sin(Elapsed * 11)) * .3f, 1 - flicker * .1f) * (1 + flare * .25f);
            // A swing fans the flame: the lit circle grows by a fifth and settles back.
            playerRig.TorchLight.OmniRange = 5 * (1 + .2f * flare);
            playerRig.TorchLight.LightEnergy = (1 + flicker * .25f + flare * .3f) * (.3f + night * 1.3f);
        }
        // Each strike of steel on flint throws a few sparks onto the torch head.
        if (player.Action == ActorAction.Kindle && (player.Struck(.1875f) || player.Struck(.4375f) || player.Struck(.6875f) || player.Struck(.9375f)))
        {
            Burst(playerRig.Flame.GlobalPosition, Flat(new Color("ffd27a")), 7, 2.2f, .3f, 6, .022f);
            Sounds?.Play("anvil_work");
        }
        if (World.S.Mauled)
        {
            // Tip backwards about the feet, then slide forward so the body lies centred on the player's tile.
            float tip = Ease(Math.Min(1, downedFor / .3f));
            var forward = new Vector3(MathF.Sin(player.Yaw), 0, MathF.Cos(player.Yaw));
            playerRig.Root.Position = To3(player.Position) + forward * .8f * tip + Vector3.Up * .17f * tip;
            playerRig.Root.Rotation = new Vector3(-Mathf.Pi / 2 * tip, player.Yaw, 0);
        }
        playerRig.Root.Visible = (!World.Dead || World.S.Mauled) && World.S.Sheltered == 0;
        Work(player, subject);
        foreach (var worker in World.S.Workers)
        {
            if (!crew.TryGetValue(worker.Id, out var entry))
            {
                entry = (new ActorRig(ActorSkin.Worker), new ActorState { Position = worker.Pos });
                scene.AddChild(entry.Rig.Root); crew.Add(worker.Id, entry);
            }
            var state = entry.State;
            bool walking = worker.Pos.DistanceSquaredTo(state.Position) > .000001f;
            var assignment = World.Find(worker.Assignment);
            var work = ActorAnimation.WorkerAction(worker, assignment, walking);
            if (work == ActorAction.Hammer && assignment?.Processing != true && assignment?.Kind != "mine") work = ActorAction.Idle;
            // At a mine the worker faces the rock past the entrance, not the cart out front.
            var facing = work is ActorAction.Sit or ActorAction.Perch && World.LunchFire is Thing fire ? fire.Pos : assignment is { Kind: "mine" } mine ? World.MineDoor(mine) * 2 - mine.Pos : assignment?.Pos ?? worker.Pos;
            state.Update(worker.Pos, work, facing, dt);
            Pose(entry.Rig, state, ActorAnimation.Tool(work));
            entry.Rig.Root.Visible = World.Sees(worker);
            float trip = worker.Away > 0 ? World.TripLeg(1 - worker.Away / World.Data.TradeSeconds) : worker.Commute / (World.Data.TradeSeconds / 2);
            Fade(entry.Rig.Root, TripFade(trip));
            Work(state, work is ActorAction.Idle or ActorAction.Walk or ActorAction.Carry or ActorAction.Sit or ActorAction.Perch ? null : assignment);
        }
        foreach (int id in crew.Keys.Where(id => World.S.Workers.All(w => w.Id != id)).ToArray())
        {
            fades.Remove(crew[id].Rig.Root); crew[id].Rig.Root.QueueFree(); crew.Remove(id);
        }
    }

    private string FootstepSound(Vector2 p)
    {
        if (Terrain.InLake(p, 1)) return "footsteps_water";
        bool rocky = World.Cliffs.Any(c => c.DistanceTo(p) < Cliff.Depth + 3) || World.S.Things.Any(t => (t.Kind == "stone" || t.IsDeposit) && t.Remaining > 0 && t.Pos.DistanceTo(p) < 1.3f);
        return rocky ? "footsteps_rocks" : "footsteps_default";
    }

    private static void Pose(ActorRig rig, ActorState state, string? tool, bool torch = false)
    {
        rig.Root.Position = To3(state.Position);
        rig.Root.Rotation = new Vector3(0, state.Yaw, 0);
        var pose = ActorAnimation.PoseAt(state.Action, state.Time, state.Moving);
        // Between actions the torch arm stays raised with the forearm level, which is the angle the flame is built for.
        if (torch && state.Action is ActorAction.Idle or ActorAction.Walk) pose = pose with { RaiseL = 30, OutL = 4, ElbowL = 60 };
        rig.Show(pose, tool, state.Action == ActorAction.Carry, state.Action == ActorAction.Perch, torch);
    }

    /// <summary>Feeds an actor's work into the object it works on: bellows travel, impact shakes, chips and sparks.</summary>
    private void Work(ActorState actor, Thing? subject)
    {
        if (subject == null) return;
        var action = actor.Action;
        // The bellows board follows the lean of the person pressing it.
        if (action == ActorAction.Pump)
            pumping[subject.Id] = Math.Max(pumping.GetValueOrDefault(subject.Id), Math.Clamp((ActorAnimation.PoseAt(action, actor.Time, false).Lean - 16) / 16, 0, 1));
        float impact = action switch
        {
            ActorAction.Chop or ActorAction.SteelChop => .52f,
            ActorAction.Mine or ActorAction.SteelMine => .55f,
            ActorAction.Hammer => .5f,
            ActorAction.Pump => .35f,
            ActorAction.Gather => .1f,
            _ => -1
        };
        if (impact < 0 || !actor.Struck(impact) || !props.TryGetValue(subject.Id, out var prop)) return;
        prop.Shake = 1;
        // A bench knocks wood on wood, so it borrows the axe's knock.
        if (subject.Kind is "tree" or "anvil" or "bench") Sounds?.Play(subject.Kind == "anvil" ? "anvil_work" : "chop_tree", subject.Pos);
        var toward = (actor.Position - subject.Pos).Normalized();
        var side = To3(subject.Pos + toward * .35f);
        switch (subject.Kind)
        {
            case "tree":
                Burst(side + Vector3.Up * 1.1f, Mat("d9b07a"), 7, 2.6f, .55f, 9, .06f);
                Burst(To3(subject.Pos, 3.6f), Mat("5d8a3c"), 3, .6f, 1.8f, 1.2f, .09f);
                break;
            case "stone" or "iron" or "coal" or "tin" or "mine":
                var color = subject.Kind is "stone" or "mine" ? "9a9d99" : subject.Kind == "iron" ? "a35a2e" : subject.Kind == "tin" ? "b9bbb4" : "26282b";
                Burst(side + Vector3.Up * .5f, Mat(color), 6, 2.4f, .5f, 9, .07f);
                Burst(side + Vector3.Up * .4f, Flat(new Color(.8f, .78f, .7f, .45f)), 3, .5f, .9f, -.3f, .16f, 1.5f);
                if (action == ActorAction.SteelMine) Burst(side + Vector3.Up * .5f, Flat(new Color("ffd27a")), 5, 3.2f, .3f, 4, .025f);
                break;
            case "wolf_corpse": Burst(side + Vector3.Up * .3f, Mat("7a1414", .6f), 5, 1.4f, .45f, 8, .04f); break;
            case "berries": Burst(To3(subject.Pos, .7f), Mat("5d8a3c"), 3, 1.2f, .7f, 5, .06f); break;
            case "water": Burst(side + Vector3.Up * .05f, Flat(new Color(.7f, .88f, .95f, .8f)), 6, 1.6f, .45f, 7, .045f); break;
            case "anvil": Burst(To3(subject.Pos) + prop.Vent, Flat(new Color("ffd27a")), 10, 3, .35f, 5, .025f); break;
            case "reinforced":
                if (subject.Heat > 10) Burst(To3(subject.Pos, .9f) + new Vector3(0, 0, .5f), Flat(new Color("ffb347")), 6, 1.4f, .6f, -1, .03f);
                break;
        }
    }

    private void SyncThings(float dt)
    {
        var seen = new HashSet<int>();
        foreach (var t in World.Known)
        {
            seen.Add(t.Id);
            if (!props.TryGetValue(t.Id, out var prop) || prop.Kind != t.Kind)
            {
                if (prop != null) prop.Root.QueueFree();
                prop = WorldModels.Build(t);
                scene.AddChild(prop.Root);
                props[t.Id] = prop;
                // A fresh carcass keeps the heading of the wolf model it replaces.
                if (t.Kind == "wolf_corpse" && wolves.FirstOrDefault(pair => !World.S.Wolves.Contains(pair.Key) && pair.Value.Last.DistanceTo(t.Pos) < 2).Value is { } dying)
                    prop.Body.Rotation = new Vector3(0, dying.Yaw, 0);
            }
            prop.Root.Position = To3(t.Pos);
            Animate(prop, t, dt);
        }
        foreach (int id in props.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            props[id].Root.QueueFree(); props.Remove(id);
        }
        FadeCanopies(dt);
    }

    private void Animate(Prop prop, Thing t, float dt)
    {
        bool first = !prop.Seen;
        prop.Seen = true;
        prop.Shake = Math.Max(0, prop.Shake - dt * 2.5f);
        float wobble = prop.Shake * MathF.Sin(Elapsed * 34);
        float flicker = MathF.Sin(Elapsed * 13 + t.Id) * .5f + MathF.Sin(Elapsed * 7.3f + t.Id * 3) * .5f;
        switch (t.Kind)
        {
            case "tree":
                var sway = prop.Sway!;
                if (t.Remaining > 0)
                {
                    prop.Fall = -1; sway.Visible = true; prop.Stump!.Visible = false;
                    sway.RotationDegrees = new Vector3(wobble * 3, 0, prop.Shake * 2 * MathF.Cos(Elapsed * 29));
                    break;
                }
                // A felled tree topples, fades away and leaves its stump.
                if (prop.Fall < 0) prop.Fall = first ? 9 : 0;
                float before = prop.Fall; prop.Fall += dt;
                float drop = Math.Min(1, prop.Fall / 1.1f);
                sway.RotationDegrees = new Vector3(90 * drop * drop, 0, 0);
                if (before < 1.1f && prop.Fall >= 1.1f) Burst(prop.Root.Position + sway.GlobalBasis.Y * 3.5f + Vector3.Up * .3f, Flat(new Color(.7f, .66f, .52f, .45f)), 8, 1.2f, 1.1f, -.2f, .3f, 1.5f);
                SetFade(sway, Math.Clamp((prop.Fall - 1.2f) / .5f, 0, 1));
                sway.Visible = prop.Fall < 1.7f;
                prop.Stump!.Visible = true;
                break;
            case "stone" or "iron" or "coal" or "tin" or "loose_iron" or "loose_coal" or "leaves":
                prop.Body.Visible = t.Remaining > 0;
                prop.Body.Scale = Vector3.One * (.55f + .45f * Math.Clamp(WorldModels.Fullness(t), 0, 1));
                prop.Body.Position = new Vector3(wobble * .03f, 0, 0);
                break;
            case "wolf_corpse":
                float dying = Math.Clamp(t.Progress / .9f, 0, 1);
                prop.Wolf!.Fall(dying);
                // Butchering flattens the carcass while the blood pool spreads out from under it.
                prop.Wolf.Root.Scale = new Vector3(1, .6f + .4f * WorldModels.Fullness(t), 1);
                prop.Wolf.Root.Position = new Vector3(wobble * .03f, 0, 0);
                prop.Pool!.Scale = Vector3.One * (.2f + .8f * Ease(dying)) * (1.25f - .25f * WorldModels.Fullness(t));
                break;
            case "berries":
                prop.Fruit!.Visible = t.Remaining > 0;
                prop.Body.Scale = new Vector3(1 + wobble * .04f, 1 - wobble * .03f, 1 + wobble * .04f);
                break;
            case "furnace":
                prop.Glow!.Visible = t.Processing;
                prop.Light!.LightEnergy = t.Processing ? (1.1f + flicker * .25f) * (.5f + night) : 0;
                Smoke(prop, t, t.Processing, dt, FurnaceSmoke);
                break;
            case "cooktable" or "cannery":
                // The fire only burns for recipes that take fuel; the mortar's work and kneading dough are cold.
                bool cooking = t.Processing && World.Data.Recipes.TryGetValue(t.Recipe, out var cooked) && cooked.Fuel;
                prop.Glow!.Visible = cooking;
                prop.Light!.LightEnergy = cooking ? (.9f + flicker * .25f) * (.5f + night) : 0;
                if (prop.Mortar != null && World.Sees(t.Pos)) prop.Mortar.Visible = t.Tools != null && World.BenchTool(t, "mortar") != null;
                Smoke(prop, t, cooking, dt, FurnaceSmoke);
                break;
            case "reinforced":
                float heat = t.Heat / 100;
                prop.Glow!.Visible = heat > .05f || t.Processing;
                prop.Light!.LightEnergy = (heat * 1.5f + (t.Processing ? .4f : 0)) * (1 + flicker * .15f) * (.5f + night);
                prop.Needle!.RotationDegrees = new Vector3(0, 0, Mathf.Lerp(prop.Needle.RotationDegrees.Z, WorldModels.Gauge(heat), Math.Min(1, dt * 6)));
                float lift = 18 * (1 - pumping.GetValueOrDefault(t.Id));
                prop.Lever!.RotationDegrees = new Vector3(0, 0, lift);
                for (int i = 0; i < prop.Pleats.Length; i++) prop.Pleats[i].RotationDegrees = new Vector3(0, 0, lift * (i + 1) / (prop.Pleats.Length + 1));
                Smoke(prop, t, t.Processing || heat > .3f, dt, ForgeSmoke);
                break;
            case "campfire":
                bool burning = t.Burn > 0;
                prop.Fire!.Visible = burning;
                prop.Fire.Scale = new Vector3(1 + flicker * .08f, .85f + MathF.Abs(MathF.Sin(Elapsed * 9 + t.Id)) * .3f, 1 - flicker * .08f);
                prop.Fire.RotationDegrees = new Vector3(0, Elapsed * 40, 0);
                prop.Light!.LightEnergy = burning ? (1.2f + flicker * .3f) * (.35f + night * 1.4f) : 0;
                if (burning && dt > 0 && Jitter() < dt * 3) Burst(To3(t.Pos, .55f), Flat(new Color("ff9a3a")), 1, .5f, 1.1f, -1.4f, .025f);
                Smoke(prop, t, burning, dt, CampfireSmoke);
                break;
            case "bench":
                // Slotted tools lie on the bench top.
                if (!World.Sees(t.Pos)) break;
                prop.Chisel!.Visible = t.Tools != null && World.BenchTool(t, "chisel") != null;
                prop.Hammer!.Visible = t.Tools != null && World.BenchTool(t, "hammer") != null;
                prop.Body.Position = new Vector3(wobble * .02f, 0, 0);
                break;
            case "pile":
                // Remembered piles carry no inventory, so they keep the stacks they had when last seen.
                if (!World.Sees(t.Pos)) break;
                var stacks = t.Inventory.Stacks().Select(s => s.Item).ToList();
                string key = string.Join(",", stacks);
                if (key == prop.StackKey) break;
                prop.StackKey = key;
                for (int i = 0; i < prop.Stacks.Length; i++)
                {
                    prop.Stacks[i].Visible = i < stacks.Count;
                    if (i < stacks.Count) prop.Stacks[i].MaterialOverride = Mat(World.Data.Items.TryGetValue(stacks[i], out var def) ? def.Color : "b0a58a", .95f);
                }
                break;
            case "toolstation":
                if (!World.Sees(t.Pos)) break;
                foreach (var (kind, tool) in prop.Racked) tool.Visible = World.BestTool(kind, t.Inventory) != null;
                break;
            case "chest" or "grave" or "crate":
                // Remembered boxes carry no inventory, so the lid stays as it was last seen.
                if (!World.Sees(t.Pos)) break;
                int count = t.Inventory.Count;
                if (!first && count != prop.LastCount) prop.Puff = .9f;
                prop.LastCount = count;
                prop.Puff = Math.Max(0, prop.Puff - dt);
                bool open = prop.Puff > 0 || t.Id == Selected && PanelOpen && World.Near(t);
                if (dt > 0 && (open ? prop.Open == 0 : prop.Open == 1)) Sounds?.Play(open ? "open_chest" : "close_chest", t.Pos);
                prop.Open = Mathf.MoveToward(prop.Open, open ? 1 : 0, dt * 4);
                prop.Lid!.RotationDegrees = new Vector3(-80 * Ease(prop.Open), 0, 0);
                // A crate being lifted rises and rocks before it goes into the backpack.
                float lifted = t.Id == Lifting ? Ease(LiftProgress) : 0;
                if (t.Kind == "crate") { prop.Body.Position = new Vector3(0, lifted * .35f, 0); prop.Body.RotationDegrees = new Vector3(lifted * 9 * MathF.Sin(Elapsed * 18), 0, lifted * 6 * MathF.Cos(Elapsed * 15)); }
                break;
            case "hut":
                bool near = !World.Dead && World.Player.DistanceTo(t.Pos) < 2.6f;
                // Running inside from a hunting pack slams the door instead of swinging it open.
                if (dt > 0 && near && prop.Open == 0) Sounds?.Play(World.S.Wolves.Count > 0 ? "hut_door_escaping" : "hut_door_normal", t.Pos);
                prop.Open = Mathf.MoveToward(prop.Open, near ? 1 : 0, Math.Max(dt, .016f) * 2.5f);
                prop.Door!.RotationDegrees = new Vector3(0, -100 * Ease(prop.Open), 0);
                prop.Window!.MaterialOverride = night > .45f ? NightGlass : Mat("8fb4c4", .15f, .2f);
                prop.Light!.LightEnergy = night > .45f ? .9f : 0;
                break;
            case "mine":
                // Remembered mines carry only their stage and progress, so out of sight the cart rests outside, empty and unlit.
                float stageShare = Math.Clamp(t.Progress / World.MineStageSeconds, 0, 1);
                bool finished = t.Stage == World.MineOpen;
                prop.Outline!.Visible = !finished;
                prop.Hole!.Visible = t.Stage > 0 || stageShare > 0;
                prop.Hole.Scale = new Vector3(1, t.Stage > 0 ? 1 : Math.Max(.05f, stageShare), 1);
                prop.Supplies!.Visible = t.Stage == World.MineSupplied;
                prop.Frame!.Visible = finished || t.Stage == World.MineSupplied && stageShare > 0;
                prop.Frame.Scale = new Vector3(1, finished ? 1 : Math.Max(.05f, stageShare), 1);
                prop.Rails!.Visible = prop.Cart!.Visible = finished;
                bool mining = finished && World.Sees(t.Pos) && World.S.Workers.Any(w => w.Assignment == t.Id && w.WorkState == WorkerWorkState.Underground);
                // The cart runs a round trip into the dark every few seconds while miners are inside.
                float cartTrip = (Elapsed / 7 + WorldModels.Jitter(t.Id, 3)) % 1;
                prop.Open = mining ? World.TripLeg(cartTrip) : Mathf.MoveToward(prop.Open, 0, dt * .4f);
                prop.Cart.Position = new Vector3(0, 0, -3 * Ease(prop.Open));
                prop.Load!.Visible = (t.Output?.Count ?? 0) > 0 && (!mining || cartTrip > .5f);
                prop.Glow!.Visible = mining;
                prop.Light!.LightEnergy = mining ? (1.3f + flicker * .2f) * (.5f + night) : 0;
                break;
            case "signpost":
                bool waiting = World.S.Orders.Any(o => o.Signpost == t.Id);
                prop.Open = Mathf.MoveToward(prop.Open, waiting ? 1 : 0, Math.Max(dt, .016f) * 3);
                prop.Flag!.RotationDegrees = new Vector3(-90 * (1 - Ease(prop.Open)), 0, 0);
                break;
        }
    }

    private static float Ease(float t) => t * t * (3 - 2 * t);

    // Campfire, furnace and bellows furnace smoke: the coal forge's plume is darker, denser and wider.
    private readonly record struct SmokeStyle(float Every, Color Color, float Size, float Grow);
    private static readonly SmokeStyle CampfireSmoke = new(.5f, new Color(.62f, .62f, .6f, .38f), .14f, 2.2f);
    private static readonly SmokeStyle FurnaceSmoke = new(.3f, new Color(.58f, .58f, .56f, .42f), .14f, 2.4f);
    private static readonly SmokeStyle ForgeSmoke = new(.16f, new Color(.36f, .36f, .36f, .55f), .2f, 2.8f);

    private void Smoke(Prop prop, Thing t, bool active, float dt, SmokeStyle smoke)
    {
        if (!active || dt <= 0) return;
        prop.Puff -= dt;
        if (prop.Puff > 0) return;
        prop.Puff = smoke.Every;
        Burst(To3(t.Pos) + prop.Vent, Flat(smoke.Color), 1, .25f, 2.4f, -.45f, smoke.Size, smoke.Grow);
    }

    /// <summary>Trees in front of the player, a worker or a structure turn see-through instead of hiding them.</summary>
    private void FadeCanopies(float dt)
    {
        var bounds = GetViewport().GetVisibleRect().Grow(300);
        var structures = World.S.Things.Where(o => !o.IsResource && props.ContainsKey(o.Id) && bounds.HasPoint(camera.UnprojectPosition(To3(o.Pos)))).ToArray();
        foreach (var t in World.S.Things)
        {
            if (t.Kind != "tree" || t.Remaining <= 0 || !props.TryGetValue(t.Id, out var prop)) continue;
            if (!bounds.HasPoint(camera.UnprojectPosition(To3(t.Pos)))) continue;
            bool covers = !World.Dead && TreeCoversPerson(t, World.Player) ||
                World.S.Workers.Any(w => World.Sees(w) && TreeCoversPerson(t, w.Pos)) ||
                structures.Any(o => Covers(t, o));
            float fade = Mathf.MoveToward(prop.Fade, covers ? .68f : 0, Math.Max(dt, .016f) * 3);
            if (Math.Abs(fade - prop.Fade) < .001f) continue;
            prop.Fade = fade; SetFade(prop.Sway!, fade);
        }
    }

    private void SyncWolves(float dt)
    {
        foreach (var wolf in World.S.Wolves)
        {
            if (!wolves.TryGetValue(wolf, out var model))
            {
                model = new WorldModels.WolfModel { Last = wolf.Pos, Yaw = ActorAnimation.Yaw(World.Player - wolf.Pos, 0) };
                scene.AddChild(model.Root); wolves[wolf] = model;
            }
            var moved = wolf.Pos - model.Last;
            bool running = moved.LengthSquared() > 1e-6f && !wolf.Feeding;
            model.Stride += running ? moved.Length() * 5 : dt * 2;
            model.Yaw = Mathf.LerpAngle(model.Yaw, ActorAnimation.Yaw(running ? moved : World.Player - wolf.Pos, model.Yaw), Math.Min(1, dt * 10));
            model.Last = wolf.Pos;
            model.Root.Position = To3(wolf.Pos);
            model.Root.Visible = World.Sees(wolf.Pos);
            model.Root.Rotation = new Vector3(0, model.Yaw, 0);
            if (wolf.Leap > 0) model.Leap(1 - wolf.Leap / World.LeapSeconds);
            else if (wolf.Feeding) model.Feed(Elapsed + wolf.GetHashCode() % 7);
            else model.Trot(model.Stride, running);
        }
        foreach (var gone in wolves.Keys.Where(w => !World.S.Wolves.Contains(w)).ToArray())
        {
            wolves[gone].Root.QueueFree(); wolves.Remove(gone);
        }
    }

    /// <summary>Fully drawn for the first part of a trip, then fading to nothing at the turn.</summary>
    private static float TripFade(float leg) => Math.Clamp((leg - .35f) / .55f, 0, 1);
    private readonly Dictionary<Node, float> fades = new();
    /// <summary>Applies a fade only when it changes, since it walks every mesh under the node.</summary>
    private void Fade(Node node, float transparency)
    {
        float step = MathF.Round(transparency * 10) / 10;
        if (fades.GetValueOrDefault(node) == step) return;
        fades[node] = step; SetFade(node, step);
    }

    private void SyncCarts(float dt)
    {
        foreach (var order in World.S.Orders)
        {
            var sign = World.Find(order.Signpost);
            float phase = order.Elapsed / World.Data.TradeSeconds;
            // The cart sets off down the valley and fades out on its way to Bramblewick, then fades back in on return.
            float leg = World.TripLeg(phase);
            var pos = (sign?.Pos ?? World.Spawn) + Terrain.Seaward * World.TripDistance * leg;
            if (!carts.TryGetValue(order, out var cart))
            {
                cart = new WorldModels.CartModel { Last = pos };
                scene.AddChild(cart.Root); carts[order] = cart;
            }
            var moved = pos - cart.Last;
            cart.Distance += moved.Length();
            cart.Yaw = Mathf.LerpAngle(cart.Yaw, ActorAnimation.Yaw(moved, cart.Yaw), Math.Min(1, dt * 8));
            cart.Last = pos;
            cart.Root.Position = To3(pos);
            cart.Root.Visible = World.Sees(pos) && leg < 1;
            Fade(cart.Root, TripFade(leg));
            cart.Root.Rotation = new Vector3(0, cart.Yaw, 0);
            cart.Roll(cart.Distance);
            // The courier walks with arms trailing back to the cart handle.
            var stride = ActorAnimation.PoseAt(ActorAction.Walk, cart.Distance / 2.1f, true) with { RaiseL = -22, RaiseR = -22, OutL = 12, OutR = 12, ElbowL = 8, ElbowR = 8, Lean = 10 };
            cart.Courier.Show(stride, null, false);
            // A sale leaves with its crates and returns with the payment; a purchase leaves with the payment and returns with the goods.
            bool back = phase >= .5f;
            cart.Load(order.Buying == back ? order.Crates : 0, order.Buying && back, order.Buying != back);
        }
        foreach (var gone in carts.Keys.Where(o => !World.S.Orders.Contains(o)).ToArray())
        {
            fades.Remove(carts[gone].Root); carts[gone].Root.QueueFree(); carts.Remove(gone);
        }
    }

    private void UpdateMarkers()
    {
        Mark(selectRing, World.Find(Selected), 0);
        Mark(hoverRing, Hover != Selected ? World.Find(Hover) : null, .004f);
        var worker = World.S.Workers.FirstOrDefault(w => w.Id == SelectedWorker);
        workerRing.Visible = worker != null && World.Sees(worker);
        if (worker != null) { workerRing.Position = To3(worker.Pos, .02f); workerRing.Scale = new Vector3(.5f, 1, .5f); }
        playerRing.Visible = !World.Dead;
        playerRing.Position = To3(World.Player, .015f); playerRing.Scale = new Vector3(.42f, 1, .42f);
        foreach (var (kind, ghost) in ghosts) ghost.Root.Visible = kind == BuildMode;
        placeTile.Visible = BuildMode != "";
        if (BuildMode == "") return;
        var at = World.Snap(BuildMode, ToWorld(Mouse).Round());
        bool valid = World.PlacementBlock(BuildMode, at) == null;
        if (!ghosts.TryGetValue(BuildMode, out var shown))
        {
            shown = WorldModels.Build(new Thing { Kind = BuildMode, Pos = at });
            SetFade(shown.Root, .35f);
            scene.AddChild(shown.Root); ghosts[BuildMode] = shown;
        }
        if (shown.Tint != (valid ? 1 : 0)) { SetOverride(shown.Root, valid ? GhostValid : GhostBlocked); shown.Tint = valid ? 1 : 0; }
        shown.Root.Position = To3(at);
        if (BuildMode == "mine") shown.Body.Rotation = WorldModels.MineYaw(at);
        placeTile.Position = To3(at, .02f);
        placeTile.MaterialOverride = valid ? GhostValid : GhostBlocked;
    }

    private void Mark(MeshInstance3D ring, Thing? t, float lift)
    {
        ring.Visible = t != null;
        if (t == null) return;
        float radius = Math.Min(1.7f, HitsOf(t).Min(h => h.Radius) + .25f);
        ring.Position = To3(t.Pos, .02f + lift);
        ring.Scale = new Vector3(radius, 1, radius);
    }

    private void UpdateLighting()
    {
        // Dusk deepens into full night and dawn fades out, so neither boundary jumps in brightness.
        float dayPhase = World.DayPhase, dusk = World.Data.DuskStarts;
        float darkness = World.Night ? .65f : dayPhase > dusk ? .02f + (dayPhase - dusk) / .1f * .63f : dayPhase < .05f ? .65f - dayPhase / .05f * .63f : .02f;
        night = Math.Clamp((darkness - .02f) / .63f, 0, 1);
        sun.LightEnergy = Mathf.Lerp(.8f, .08f, night);
        sun.LightColor = new Color("fff1dc").Lerp(new Color("8296d6"), night);
        environment.AmbientLightColor = new Color("c7d3dc").Lerp(new Color("33447a"), night);
        environment.AmbientLightEnergy = Mathf.Lerp(.32f, .3f, night);
        environment.BackgroundColor = new Color("879c69").Lerp(new Color("1b2733"), night);
    }

    // ---------- 2D overlay: names, progress and forge heat ----------

    private void Tag(Vector2 at, string text)
    {
        var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 14);
        overlay.DrawStyleBox(new StyleBoxFlat { BgColor = new Color(.12f, .18f, .15f, .86f), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 }, new Rect2(at - new Vector2(size.X / 2 + 6, 18), size + new Vector2(12, 6)));
        overlay.DrawString(font, at - new Vector2(size.X / 2, 0), text, HorizontalAlignment.Left, -1, 14, new Color("f5ecd1"));
    }

    private void PaintOverlay()
    {
        if (World == null) return;
        if (AnimationReview) { PaintReview(); return; }
        foreach (var t in World.S.Things.Where(t => t.Processing && World.Sees(t.Pos) && World.Data.Recipes.ContainsKey(t.Recipe)))
        {
            var p = Screen(t.Pos); var r = World.Data.Recipes[t.Recipe];
            overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56, 5)), new Color("2b4135"));
            overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56 * Math.Min(1, t.Progress / r.Seconds), 5)), new Color("e5bf68"));
        }
        // Forge heat under each bellows furnace, with a tick at the clear bar steel needs.
        foreach (var t in World.S.Things.Where(t => t.Kind == "reinforced" && World.Sees(t.Pos)))
        {
            var p = Screen(t.Pos) + new Vector2(-28, 18);
            overlay.DrawRect(new Rect2(p, new Vector2(56, 5)), new Color("2b4135"));
            overlay.DrawRect(new Rect2(p, new Vector2(56 * Math.Clamp(t.Heat / 100, 0, 1), 5)), t.Heat >= World.SteelHeat ? new Color("f0a03c") : new Color("8a5a46"));
            overlay.DrawLine(p + new Vector2(56 * World.SteelHeat / 100, -2), p + new Vector2(56 * World.SteelHeat / 100, 7), new Color("f2e8cf"), 2);
        }
        // Only the hovered object is named; the side panel already names the selection.
        var hovered = World.Find(Hover);
        if (hovered != null) Tag(Screen(hovered.Pos, HitsOf(hovered).Max(h => h.Top) + .2f) - new Vector2(0, 8), World.Data.Name(hovered.Kind));
        foreach (var w in World.S.Workers.Where(w => World.Sees(w) && (w.Id == HoverWorker || w.Id == SelectedWorker)))
            Tag(Screen(w.Pos, 2.05f), w.Name);
    }
}
