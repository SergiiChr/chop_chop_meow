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
    public Vector2 Mouse;
    public float Zoom = 1;
    public bool Gathering;
    public Thing? GatheringTarget;
    public string? SelectedTool;
    public bool Paused;
    public bool AnimationReview;
    public float Elapsed;
    public bool PanelOpen;
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

    public override void _ExitTree() => World.PlayerActed -= PlayerActed;

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
        Thing? best = null; float nearest = float.MaxValue;
        foreach (var t in World.S.Things)
        {
            if (t.Remaining <= 0 && t.IsResource && t.Kind != "berries") continue;
            if (RayHits(screen, t.Pos, HitsOf(t), out float d) && d < nearest) { nearest = d; best = t; }
        }
        return best;
    }

    private static readonly Hit[] PersonHit = { new(.38f, 0, 1.75f) };
    public Worker? PickWorker(Vector2 screen)
    {
        SyncCamera();
        Worker? best = null; float nearest = float.MaxValue;
        foreach (var w in World.S.Workers)
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
        if (AnimationReview) { UpdateReview(); overlay.QueueRedraw(); return; }
        if (dt > 0) UpdateActors(dt);
        SyncThings(dt);
        if (dt > 0) { SyncWolves(dt); SyncCarts(dt); }
        UpdateEffects(dt);
        UpdateMarkers();
        UpdateLighting();
        overlay.QueueRedraw();
    }

    private string? HeldTool() => SelectedTool switch
    {
        "steel_tools" when World.S.Backpack["steel_tools"] > 0 => "steel_axe",
        "iron_axe" or "iron_pick" when World.S.Backpack[SelectedTool] > 0 => SelectedTool,
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
            var anvil = World.S.Things.FirstOrDefault(t => t.Kind == "anvil" && t.Processing && World.Near(t) &&
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
        pulseRemaining = Math.Max(0, pulseRemaining - dt);
        string? tool = ActorAnimation.Tool(action) ?? (action is ActorAction.Idle or ActorAction.Walk ? HeldTool() : null);
        Pose(playerRig, player, tool);
        if (World.S.Mauled)
        {
            // Tip backwards about the feet, then slide forward so the body lies centred on the player's tile.
            float tip = Ease(Math.Min(1, downedFor / .3f));
            var forward = new Vector3(MathF.Sin(player.Yaw), 0, MathF.Cos(player.Yaw));
            playerRig.Root.Position = To3(player.Position) + forward * .8f * tip + Vector3.Up * .17f * tip;
            playerRig.Root.Rotation = new Vector3(-Mathf.Pi / 2 * tip, player.Yaw, 0);
        }
        playerRig.Root.Visible = !World.Dead || World.S.Mauled;
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
            if (work == ActorAction.Hammer && assignment?.Processing != true) work = ActorAction.Idle;
            state.Update(worker.Pos, work, assignment?.Pos ?? worker.Pos, dt);
            Pose(entry.Rig, state, ActorAnimation.Tool(work));
            Work(state, work is ActorAction.Idle or ActorAction.Walk or ActorAction.Carry ? null : assignment);
        }
        foreach (int id in crew.Keys.Where(id => World.S.Workers.All(w => w.Id != id)).ToArray())
        {
            crew[id].Rig.Root.QueueFree(); crew.Remove(id);
        }
    }

    private static void Pose(ActorRig rig, ActorState state, string? tool)
    {
        rig.Root.Position = To3(state.Position);
        rig.Root.Rotation = new Vector3(0, state.Yaw, 0);
        rig.Show(ActorAnimation.PoseAt(state.Action, state.Time, state.Moving), tool, state.Action == ActorAction.Carry);
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
        var toward = (actor.Position - subject.Pos).Normalized();
        var side = To3(subject.Pos + toward * .35f);
        switch (subject.Kind)
        {
            case "tree":
                Burst(side + Vector3.Up * 1.1f, Mat("d9b07a"), 7, 2.6f, .55f, 9, .06f);
                Burst(To3(subject.Pos, 3.6f), Mat("5d8a3c"), 3, .6f, 1.8f, 1.2f, .09f);
                break;
            case "stone" or "iron" or "coal" or "loose_iron" or "loose_coal":
                var color = subject.Kind == "stone" ? "9a9d99" : subject.Kind == "iron" ? "a35a2e" : "26282b";
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
        foreach (var t in World.S.Things)
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
            case "stone" or "iron" or "coal" or "loose_iron" or "loose_coal":
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
                Smoke(prop, t, t.Processing, dt);
                break;
            case "reinforced":
                float heat = t.Heat / 100;
                prop.Glow!.Visible = heat > .05f || t.Processing;
                prop.Light!.LightEnergy = (heat * 1.5f + (t.Processing ? .4f : 0)) * (1 + flicker * .15f) * (.5f + night);
                prop.Needle!.RotationDegrees = new Vector3(0, 0, Mathf.Lerp(prop.Needle.RotationDegrees.Z, WorldModels.Gauge(heat), Math.Min(1, dt * 6)));
                prop.Lever!.RotationDegrees = new Vector3(-16 * (1 - pumping.GetValueOrDefault(t.Id)), 0, 0);
                Smoke(prop, t, t.Processing || heat > .3f, dt);
                break;
            case "campfire":
                bool burning = t.Burn > 0;
                prop.Fire!.Visible = burning;
                prop.Fire.Scale = new Vector3(1 + flicker * .08f, .85f + MathF.Abs(MathF.Sin(Elapsed * 9 + t.Id)) * .3f, 1 - flicker * .08f);
                prop.Fire.RotationDegrees = new Vector3(0, Elapsed * 40, 0);
                prop.Light!.LightEnergy = burning ? (1.2f + flicker * .3f) * (.35f + night * 1.4f) : 0;
                if (burning && dt > 0 && Jitter() < dt * 3) Burst(To3(t.Pos, .55f), Flat(new Color("ff9a3a")), 1, .5f, 1.1f, -1.4f, .025f);
                Smoke(prop, t, burning, dt);
                break;
            case "chest" or "grave":
                int count = t.Inventory.Count;
                if (!first && count != prop.LastCount) prop.Puff = .9f;
                prop.LastCount = count;
                prop.Puff = Math.Max(0, prop.Puff - dt);
                bool open = prop.Puff > 0 || t.Id == Selected && PanelOpen && World.Near(t);
                prop.Open = Mathf.MoveToward(prop.Open, open ? 1 : 0, dt * 4);
                prop.Lid!.RotationDegrees = new Vector3(-80 * Ease(prop.Open), 0, 0);
                break;
            case "hut":
                bool near = !World.Dead && World.Player.DistanceTo(t.Pos) < 2.6f;
                prop.Open = Mathf.MoveToward(prop.Open, near ? 1 : 0, Math.Max(dt, .016f) * 2.5f);
                prop.Door!.RotationDegrees = new Vector3(0, -100 * Ease(prop.Open), 0);
                prop.Window!.MaterialOverride = night > .45f ? NightGlass : Mat("8fb4c4", .15f, .2f);
                prop.Light!.LightEnergy = night > .45f ? .9f : 0;
                break;
            case "signpost":
                bool waiting = World.S.Orders.Any(o => o.Signpost == t.Id);
                prop.Open = Mathf.MoveToward(prop.Open, waiting ? 1 : 0, Math.Max(dt, .016f) * 3);
                prop.Flag!.RotationDegrees = new Vector3(-90 * (1 - Ease(prop.Open)), 0, 0);
                break;
        }
    }

    private static float Ease(float t) => t * t * (3 - 2 * t);

    private void Smoke(Prop prop, Thing t, bool active, float dt)
    {
        if (!active || dt <= 0) return;
        prop.Puff -= dt;
        if (prop.Puff > 0) return;
        prop.Puff = .5f;
        Burst(To3(t.Pos) + prop.Vent, Flat(new Color(.62f, .62f, .6f, .38f)), 1, .25f, 2.4f, -.45f, .14f, 2.2f);
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
                World.S.Workers.Any(w => TreeCoversPerson(t, w.Pos)) ||
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

    private void SyncCarts(float dt)
    {
        foreach (var order in World.S.Orders)
        {
            var sign = World.Find(order.Signpost);
            float phase = order.Elapsed / World.Data.TradeSeconds;
            var pos = sign == null ? new Vector2(49, 24) : sign.Pos.Lerp(new Vector2(49, 24), phase < .5f ? phase * 2 : (1 - phase) * 2);
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
            cart.Root.Rotation = new Vector3(0, cart.Yaw, 0);
            cart.Roll(cart.Distance);
            // The courier walks with arms trailing back to the cart handle.
            var stride = ActorAnimation.PoseAt(ActorAction.Walk, cart.Distance / 2.1f, true) with { RaiseL = -22, RaiseR = -22, OutL = 12, OutR = 12, ElbowL = 8, ElbowR = 8, Lean = 10 };
            cart.Courier.Show(stride, null, false);
            bool cash = !order.Buying && phase >= .5f;
            cart.Cash.Visible = cash; cart.Goods.Visible = !cash;
        }
        foreach (var gone in carts.Keys.Where(o => !World.S.Orders.Contains(o)).ToArray())
        {
            carts[gone].Root.QueueFree(); carts.Remove(gone);
        }
    }

    private void UpdateMarkers()
    {
        Mark(selectRing, World.Find(Selected), 0);
        Mark(hoverRing, Hover != Selected ? World.Find(Hover) : null, .004f);
        var worker = World.S.Workers.FirstOrDefault(w => w.Id == SelectedWorker);
        workerRing.Visible = worker != null;
        if (worker != null) { workerRing.Position = To3(worker.Pos, .02f); workerRing.Scale = new Vector3(.5f, 1, .5f); }
        playerRing.Visible = !World.Dead;
        playerRing.Position = To3(World.Player, .015f); playerRing.Scale = new Vector3(.42f, 1, .42f);
        foreach (var (kind, ghost) in ghosts) ghost.Root.Visible = kind == BuildMode;
        placeTile.Visible = BuildMode != "";
        if (BuildMode == "") return;
        var at = ToWorld(Mouse).Round();
        bool valid = World.PlacementBlock(BuildMode, at) == null;
        if (!ghosts.TryGetValue(BuildMode, out var shown))
        {
            shown = WorldModels.Build(new Thing { Kind = BuildMode, Pos = at });
            SetFade(shown.Root, .35f);
            scene.AddChild(shown.Root); ghosts[BuildMode] = shown;
        }
        if (shown.Tint != (valid ? 1 : 0)) { SetOverride(shown.Root, valid ? GhostValid : GhostBlocked); shown.Tint = valid ? 1 : 0; }
        shown.Root.Position = To3(at);
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
        float dayPhase = World.DayPhase, dusk = World.Data.NightStarts - .1f;
        float darkness = World.Night ? .65f : dayPhase > dusk ? .02f + (dayPhase - dusk) / .1f * .63f : dayPhase < .05f ? .65f - dayPhase / .05f * .63f : .02f;
        night = Math.Clamp((darkness - .02f) / .63f, 0, 1);
        sun.LightEnergy = Mathf.Lerp(.8f, .08f, night);
        sun.LightColor = new Color("fff1dc").Lerp(new Color("8296d6"), night);
        environment.AmbientLightColor = new Color("c7d3dc").Lerp(new Color("33447a"), night);
        environment.AmbientLightEnergy = Mathf.Lerp(.32f, .3f, night);
        environment.BackgroundColor = new Color("879c69").Lerp(new Color("1b2733"), night);
    }

    // ---------- 2D overlay: names, progress and the landmark guide ----------

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
        foreach (var t in World.S.Things.Where(t => t.Processing && World.Data.Recipes.ContainsKey(t.Recipe)))
        {
            var p = Screen(t.Pos); var r = World.Data.Recipes[t.Recipe];
            overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56, 5)), new Color("2b4135"));
            overlay.DrawRect(new Rect2(p + new Vector2(-28, 10), new Vector2(56 * Math.Min(1, t.Progress / r.Seconds), 5)), new Color("e5bf68"));
        }
        // Only the hovered object is named; the side panel already names the selection.
        var hovered = World.Find(Hover);
        if (hovered != null) Tag(Screen(hovered.Pos, HitsOf(hovered).Max(h => h.Top) + .2f) - new Vector2(0, 8), World.Data.Name(hovered.Kind));
        foreach (var w in World.S.Workers.Where(w => w.Id == HoverWorker || w.Id == SelectedWorker))
            Tag(Screen(w.Pos, 2.05f), w.Name);
        // The landmark guide follows the player's position rather than assuming they remain at spawn.
        string Direction(Vector2 target)
        {
            var d = Screen(target) - Screen(World.Player);
            if (d.Length() < 95) return "HERE";
            if (Math.Abs(d.X) > Math.Abs(d.Y) * 2) return d.X > 0 ? "→" : "←";
            if (Math.Abs(d.Y) > Math.Abs(d.X) * 2) return d.Y > 0 ? "↓" : "↑";
            return d.Y > 0 ? d.X > 0 ? "↘" : "↙" : d.X > 0 ? "↗" : "↖";
        }
        // Point only toward what the current objective sends the player to find.
        string? guide = World.S.Goal switch { 2 => $"IRON {Direction(World.CliffOf("iron").Center)}", 5 => $"COAL {Direction(World.CliffOf("coal").Center)}", _ => null };
        if (guide != null) overlay.DrawString(font, new Vector2(25, GetViewport().GetVisibleRect().Size.Y - 210), guide, HorizontalAlignment.Left, -1, 13, new Color("f4ecd3"));
    }
}
