using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>What the next left click in the world does.</summary>
public abstract record ClickMode {
    /// <summary>A left click selects what is under it.</summary>
    public sealed record Normal : ClickMode;
    /// <summary>A click places a building of this kind.</summary>
    public sealed record Build(ThingKind Kind, PipeShape Shape = PipeShape.Straight) : ClickMode;
    /// <summary>A click places a crate or barrel, from a specific backpack cell when one is named.</summary>
    public sealed record Container(string Item, int BackpackSlot = -1) : ClickMode;
    /// <summary>Holding right mouse on open ground digs a hole.</summary>
    public sealed record Dig : ClickMode;
    /// <summary>A click on a resource makes it this worker's gathering spot.</summary>
    public sealed record Assign(int Worker) : ClickMode;
    /// <summary>The remove selector takes down what the player clicks on the selected layer.</summary>
    public sealed record Remove(Layer Layer = Layer.Surface) : ClickMode;

    internal int QuarterTurns { get; init; }
    /// <summary>The turn of the plan or container awaiting placement, in radians.</summary>
    public float Rotation => QuarterTurns * Mathf.Pi / 2;

    /// <summary>The kind being placed, or null when a click places nothing.</summary>
    public ThingKind? Placing => this switch { Build build => build.Kind, Container container => Barrel.Is(container.Item) ? ThingKind.Barrel : ThingKind.Crate, Dig => ThingKind.Hole, _ => null };
}

/// <summary>The player in the world: walking, gathering, lifting containers, placing buildings, taking them down and clicking things.</summary>
public sealed class PlayerControl {
    private const float LiftSeconds = .6f;
    private readonly UiKit kit;
    private readonly WorldView view;
    private readonly SidePanel panel;
    private readonly Hotbar hotbar;
    private readonly HeldCursor cursor;
    private readonly ItemMoves moves;
    private readonly World world;
    /// <summary>Whether <see cref="Mode"/> places the container or plan on the cursor, which it does only while the mouse is over open ground.</summary>
    private bool placingHeld;
    private Layer selectedLayer;
    /// <summary>The placement on the cursor, with a turn that stays set while the pointer crosses the interface.</summary>
    private ClickMode? heldPlacement;
    private Thing? gathering;
    private int digPress, digAttempt;
    private bool digDismissed;
    /// <summary>The spot under the mouse when this hold started, locked until it ends.</summary>
    public Vector2? DiggingAt { get; private set; }
    private readonly CabinApproach cabinApproach;
    private readonly InteractionApproach interactionApproach;
    private Momentum momentum;
    /// <summary>The way the player last walked, which is the way Space swings.</summary>
    private Vector2 walked;
    /// <summary>A container held under the right mouse button; it is lifted once held for <see cref="LiftSeconds"/>.</summary>
    private Thing? lifting;
    private float liftClock;
    /// <summary>The cabin the last left press in the world opened, which the second press of a double click walks into (<see cref="WalkIntoClickedCabin"/>).</summary>
    private Thing? clickedCabin;

    /// <summary>What the next left click in the world does.</summary>
    public ClickMode Mode { get; set; } = new ClickMode.Normal();

    /// <summary>The layer the player selected, unless a world action needs a particular layer.</summary>
    public Layer Layer => Mode switch {
        ClickMode.Remove remove => remove.Layer,
        ClickMode.Build { Kind: ThingKind.SteelPiping } => Layer.Underground,
        ClickMode.Normal => selectedLayer,
        _ => Layer.Surface
    };

    public PlayerControl(UiKit kit, WorldView view, SidePanel panel, Hotbar hotbar, HeldCursor cursor, ItemMoves moves) {
        this.kit = kit; this.view = view; this.panel = panel; this.hotbar = hotbar; this.cursor = cursor; this.moves = moves; world = kit.World;
        cabinApproach = new CabinApproach(world);
        interactionApproach = new InteractionApproach(world, kit);
        world.Replaced += () => { Stop(); heldPlacement = null; placingHeld = false; selectedLayer = Layer.Surface; digDismissed = false; Mode = new ClickMode.Normal(); };
    }

    /// <summary>Drops actions whose targets belong to the previous world.</summary>
    private void Stop() {
        DiggingAt = null; digAttempt = 0;
        gathering = null; lifting = null; clickedCabin = null; cabinApproach.Cancel(); interactionApproach.Cancel();
    }

    public bool Entering(Thing cabin) => cabinApproach.GoesTo(cabin);
    public bool CancelEntry() => cabinApproach.Cancel();

    /// <summary>Starts or cancels the walk to this cabin's door from its Enter button.</summary>
    public void ToggleEntry(Thing cabin) {
        interactionApproach.Cancel();
        if (Entering(cabin)) CancelEntry(); else cabinApproach.Start(cabin);
    }

    /// <summary>Works the selected pump or bellows, walking to its handle when needed.</summary>
    internal void UsePump(Thing station) {
        if (kit.Paused) { kit.Notify("Resume before pumping."); return; }
        if (world.Occupied || world.Sheltered != 0) return;
        cabinApproach.Cancel();
        bool water = station.Kind == ThingKind.Pump;
        bool ready = !water && (GlassBlowing.IsRecipe(station) ? GlassBlowing.AtPump(world, station) : world.AtStation(station));
        interactionApproach.Start(station, station.ToWorld(World.BellowsStand), ready, world.Pump,
            water ? "No clear path to the pump handle on the bank." : "No clear path to the furnace bellows.");
    }

    /// <summary>Enters placement for a container from the backpack.</summary>
    public void PlaceContainer(string item, int backpackSlot = -1) {
        var container = new ClickMode.Container(item, backpackSlot);
        if (ContainerCount(container) == 0) { kit.Notify(Barrel.Is(item) ? "No barrel in your backpack." : "No empty crate in your backpack."); return; }
        Mode = container; panel.Close();
    }

    /// <summary>Moves the player for this frame from held keys and buttons, and carries on what a click started; <paramref name="overPanel"/> tells that the mouse is on the interface.</summary>
    public void Update(float dt, bool overPanel) {
        PlaceHeld(overPanel);
        UpdateDigMode();
        // The recipe map covers the world, so nothing held over it walks or gathers.
        if (kit.Paused || panel.Map.Visible) { StopGroundDig(); world.Me.Sprinting = false; return; }
        if (view.RisingFromStrawBed) {
            momentum = default; world.Me.Sprinting = world.Me.Braking = false;
            view.Gathering = false; view.GatheringTarget = null;
            return;
        }
        bool right = Input.IsMouseButtonPressed(MouseButton.Right);
        var direction = cabinApproach.Active ? cabinApproach.Direction()
            : interactionApproach.Direction(world.Door == Doorway.None ? HeldDirection() : Vector2.Zero, panel.Selection.Thing);
        direction = Gather(direction, right, dt);
        direction = DigGround(direction, right);
        direction = Lift(direction, right, dt);
        Walk(direction, right, dt);
    }

    /// <summary>Shows the dig outline again once a temporary action ends, unless the player put it away.</summary>
    private void UpdateDigMode() {
        bool selected = world.Holes.ShovelSelected;
        if (!selected || world.InHand == "") digDismissed = false;
        if (selected && !digDismissed && Mode is ClickMode.Normal && selectedLayer == Layer.Surface) Mode = new ClickMode.Dig();
        else if (!selected && Mode is ClickMode.Dig) Mode = new ClickMode.Normal();
    }

    internal void SelectDigging() {
        digDismissed = false;
        if (selectedLayer == Layer.Surface) Mode = new ClickMode.Dig();
    }

    internal bool CanCancelMode => Mode is not ClickMode.Normal && (Mode is not ClickMode.Dig || world.InHand != "");

    internal bool TryCancelMode() {
        if (!CanCancelMode) return false;
        if (Mode is ClickMode.Dig) digDismissed = true;
        Mode = new ClickMode.Normal();
        return true;
    }

    /// <summary>A container or building plan on the cursor goes into placement over open ground, and back to the cursor over the interface.</summary>
    private void PlaceHeld(bool overPanel) {
        var plan = cursor.Plan is string id ? Kinds.Parse(id) : null;
        string? container = cursor.Stack is HeldStack held && (World.IsCrate(held.Item) || Barrel.Is(held.Item)) ? held.Item : null;
        var previous = heldPlacement;
        // Built only when it changes, as this runs every frame.
        if (plan is ThingKind kind) {
            if (heldPlacement is not ClickMode.Build build || build.Kind != kind) heldPlacement = new ClickMode.Build(kind);
        } else if (container != null) {
            if (heldPlacement is not ClickMode.Container placing || placing.Item != container) heldPlacement = new ClickMode.Container(container);
        } else heldPlacement = null;
        bool showing = heldPlacement != null && !overPanel;
        if (showing) Mode = heldPlacement!;
        else if (placingHeld && ReferenceEquals(Mode, previous)) Mode = new ClickMode.Normal();
        placingHeld = showing;
    }

    /// <summary>Turns a plan or container by a quarter turn and reports whether placement took the key.</summary>
    public bool RotatePlacement(int quarters, bool overPanel) {
        PlaceHeld(overPanel);
        var placement = heldPlacement ?? Mode;
        if (placement.Placing is not ThingKind kind || kind == ThingKind.Hole) return false;
        if (kind == ThingKind.Mine) return true;
        var turned = placement with { QuarterTurns = (placement.QuarterTurns + quarters + 4) % 4 };
        if (heldPlacement != null) heldPlacement = turned;
        if (placingHeld || heldPlacement == null) Mode = turned;
        return true;
    }

    /// <summary>Whether the current placement or the plan held over a panel is steel piping.</summary>
    internal bool PlacingPipe => cursor.Plan is string plan ? plan == ThingKind.SteelPiping.Id() : Mode.Placing == ThingKind.SteelPiping;

    public void CyclePipeShape(bool overPanel) {
        PlaceHeld(overPanel);
        if ((heldPlacement ?? Mode) is not ClickMode.Build { Kind: ThingKind.SteelPiping } build) return;
        var next = build with {
            Shape = build.Shape switch {
                PipeShape.Straight => PipeShape.Tee,
                PipeShape.Tee => PipeShape.Cross,
                _ => PipeShape.Straight
            }
        };
        if (heldPlacement != null) heldPlacement = next;
        if (placingHeld || heldPlacement == null) Mode = next;
        kit.Refresh();
    }

    private Vector2 HeldDirection() {
        float x = (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
        float y = (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0);
        return view.FirstPerson ? view.FirstPersonMove(new Vector2(x, y)) : KeyDirection(x, y);
    }

    /// <summary>The ground direction for right <paramref name="x"/> and down <paramref name="y"/> on screen, each -1, 0 or 1 as movement keys give them.</summary>
    public static Vector2 KeyDirection(float x, float y) => new Vector2(x + y * 2, y * 2 - x).Normalized();

    /// <summary>Carries on a gather; a loose piece needs only one click: the player keeps walking to it after the button is released.</summary>
    private Vector2 Gather(Vector2 direction, bool right, float dt) {
        // What was being worked on can go: a tree falls and leaves its stump, and a stump or a bush comes out of the ground.
        if (gathering != null && world.Find(gathering.Id) == null) { panel.Deselect(gathering.Id); gathering = null; }
        if (gathering == null || !right && !gathering.IsLoose || direction != Vector2.Zero || cabinApproach.Active || interactionApproach.Active) {
            world.ResetGather();
            if (!right || gathering?.IsLoose == true) gathering = null;
            return direction;
        }
        // Step in close enough for the tool to reach the model; the lake edge keeps its wider reach.
        if (!world.Near(gathering, gathering.Kind == ThingKind.Water ? 2.5f : 1.35f)) return (world.GatherAt(gathering, world.Player) - world.Player).Normalized();
        if (gathering.IsLoose) {
            world.Gather(gathering, dt);
            if (world.Occupied) { gathering = null; return direction; }
            if (world.Find(gathering.Id) == null) panel.Deselect(gathering.Id);
            gathering = null;
            return direction;
        }
        // Emptying it shows on the bare bush or raked patch; a target that was already empty or out of reach still says why.
        bool had = gathering.Remaining > 0; world.Gather(gathering, dt);
        // A busy player is told once per click, not every frame the button is held.
        if (world.Occupied) { gathering = null; return direction; }
        if (world.GatherBlock(gathering) is string blocked) { if (!had || gathering.Remaining > 0) kit.Notify(blocked); gathering = null; }
        return direction;
    }

    private Vector2 DigGround(Vector2 direction, bool right) {
        if (DiggingAt is not Vector2 at) return direction;
        if (!right || Mode is not ClickMode.Dig || direction != Vector2.Zero || cabinApproach.Active || interactionApproach.Active
            || world.Occupied || !cursor.Empty) { StopGroundDig(); return direction; }
        if (world.Player.DistanceTo(at) > GroundHoles.Reach) {
            if (digAttempt == 0) return world.Player.DirectionTo(at);
            StopGroundDig();
            return direction;
        }
        if (digAttempt == 0) {
            int attempt = ++digPress;
            if (!world.Holes.Begin(at, attempt)) { DiggingAt = null; return direction; }
            digAttempt = attempt;
        }
        if (world.Holes.Ready(digAttempt)) {
            world.Holes.Finish(digAttempt);
            DiggingAt = null; digAttempt = 0;
        }
        return direction;
    }

    private void StopGroundDig() {
        if (digAttempt != 0) world.Holes.Cancel(digAttempt);
        DiggingAt = null; digAttempt = 0;
    }

    /// <summary>A held right click walks to a container and lifts it once the button has been held long enough.</summary>
    private Vector2 Lift(Vector2 direction, bool right, float dt) {
        if (lifting != null && right && direction == Vector2.Zero && !cabinApproach.Active && !interactionApproach.Active && world.Find(lifting.Id) != null) {
            if (!world.Near(lifting, 1.5f)) direction = (lifting.Pos - world.Player).Normalized();
            else if ((liftClock += dt) >= LiftSeconds) {
                var container = lifting; lifting = null;
                bool took = container.Kind == ThingKind.Barrel ? world.PickUpBarrel(container) : world.PickUpCrate(container);
                if (took && panel.Deselect(container.Id) && panel.Page == Page.Inspect) panel.Close();
            }
        } else { lifting = null; liftClock = 0; }
        view.Lifting = lifting?.Id ?? 0; view.LiftProgress = liftClock / LiftSeconds;
        return direction;
    }

    private void Walk(Vector2 direction, bool right, float dt) {
        // A carried barrel prevents sprinting.
        // Under 15% food or drink the player is too weak to sprint.
        world.Me.Sprinting = direction != Vector2.Zero && !world.Occupied && world.CanSprint && Input.IsPhysicalKeyPressed(Key.Shift) && world.Hunger >= GameData.LowNeed && world.Thirst >= GameData.LowNeed;
        var wanted = !world.Occupied ? direction : Vector2.Zero;
        Vector2 step;
        if (cabinApproach.Active || interactionApproach.Active) {
            momentum = default; step = direction;
        } else if (world.WolfNearby(world.Player)) {
            momentum = new Momentum { Heading = wanted, Pace = 1 }; step = wanted;
        } else step = momentum.Step(wanted, dt);
        if (step != Vector2.Zero) walked = step.Normalized();
        world.Me.Braking = momentum.Braking;
        var move = step * dt * world.PlayerSpeed;
        if (cabinApproach.Active) cabinApproach.Move(move.Length());
        else if (interactionApproach.Active) interactionApproach.Move(move.Length());
        else world.MovePlayer(move);
        view.GatheringTarget = gathering is { IsLoose: false } && right && world.Near(gathering, 2.5f) ? gathering
            : digAttempt != 0 ? GroundHoles.AnimationTarget(world.Me) : null;
        view.Gathering = view.GatheringTarget != null;
        // A tool in hand that has left the backpack leaves the hand empty.
        if (world.InHand != "" && world.Backpack[world.InHand] == 0) world.SelectInHand("");
        world.Me.Selected = hotbar.ActiveItem() ?? "";
    }

    /// <summary>Space works a pump or bellows, or swings with the current aim.</summary>
    public void Swing() {
        var station = world.Find(panel.Selection.Thing);
        if (station is { Kind: ThingKind.Pump or ThingKind.Reinforced }) UsePump(station);
        else if (GlassBlowing.PumpAtPlayer(world) is Thing glass) world.Pump(glass);
        else {
            var aim = view.FirstPerson ? view.FirstPersonForward : HeldDirection();
            world.Attack(aim == Vector2.Zero ? walked : aim);
        }
    }

    /// <summary>A mouse button over the world; <paramref name="overPanel"/> tells that the pointer is on a panel, which passes the wheel on when it has nothing to scroll, but must not zoom the world behind.</summary>
    public void Click(InputEventMouseButton mouse, Vector2 at, bool overPanel) {
        if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed && !overPanel && !view.FirstPerson) view.Zoom = Math.Min(1.6f, view.Zoom + .1f);
        if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed && !overPanel && !view.FirstPerson) view.Zoom = Math.Max(.55f, view.Zoom - .1f);
        if (mouse.ButtonIndex == MouseButton.Right) {
            if (!mouse.Pressed) StopGroundDig();
            else if (!overPanel) RightPress(at);
        }
        if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) LeftPress(at);
    }

    /// <summary>Switches the visible layer. The remove selector keeps its own layer while it is active.</summary>
    public void ToggleLayer() {
        StopGroundDig();
        var next = Layer == Layer.Surface ? Layer.Underground : Layer.Surface;
        if (Mode is ClickMode.Remove) Mode = new ClickMode.Remove(next);
        else { Mode = new ClickMode.Normal(); selectedLayer = next; }
        if (next == Layer.Surface) panel.ClosePipe();
    }

    private void RightPress(Vector2 at) {
        StopGroundDig();
        if (PlacingPipe) {
            world.Construction.Remove(world.Construction.RemovalAt(null, view.ToWorld(at, PipeGround.FloorY), Layer.Underground));
            return;
        }
        if (Mode is ClickMode.Remove) { ToggleLayer(); return; }
        // A right click in the world lets go of a held stack, as a left one does, and does nothing else.
        if (!cursor.Empty) { cursor.Release(); gathering = null; lifting = null; return; }
        if (Layer == Layer.Underground) return;
        var t = view.Pick(at);
        // With a bug net selected a right click on a firebug swings at it, as one on open ground swings toward the pointer.
        if (t is { Kind: ThingKind.Firebug } && Mode is ClickMode.Normal && hotbar.ActiveItem() is string net && world.ToolKind(net) == "net") {
            gathering = null; lifting = null;
            hotbar.UseSelected(world.Firebugs.At(t));
            return;
        }
        // A firebug in the air takes no ground: a shovel digs the spot it hovers over.
        if (t is { Roams: true } && Mode is ClickMode.Dig) t = null;
        if (t?.IsResource == true || t?.Kind == ThingKind.Farm || t is { Kind: ThingKind.Mine, MineStage: not MineStage.Open }) { gathering = t; panel.Mark(t); } else gathering = null;
        lifting = t?.Kind is ThingKind.Crate or ThingKind.Barrel ? t : null; liftClock = 0;
        if (t != null) return;
        if (Mode is ClickMode.Dig) {
            if (!view.TryGroundPoint(at, out var ground)) return;
            var spot = ground.Round();
            if (world.Holes.Block(spot) is string blocked) { kit.Notify(blocked); return; }
            DiggingAt = spot;
        } else if (Mode is ClickMode.Normal && view.PickWorker(at) == null) hotbar.UseSelected(view.ToWorld(at));
    }

    /// <summary>
    /// The second press of a double click on a cabin walks the player to its door and in, wherever the pointer is.
    /// The first press opened the cabin's page, which can put the page's windows under the pointer.
    /// So the game hands every left press here before the interface: one that is not such a second press returns false and goes on as usual.
    /// </summary>
    public bool WalkIntoClickedCabin(InputEventMouseButton mouse) {
        if (mouse.ButtonIndex != MouseButton.Left || !mouse.Pressed) return false;
        var cabin = clickedCabin; clickedCabin = null;
        if (!mouse.DoubleClick || !cursor.Empty || Mode is not ClickMode.Normal || cabin == null || world.Find(cabin.Id) is not { Kind: ThingKind.Hut } opened) return false;
        interactionApproach.Cancel(); cabinApproach.Start(opened);
        return true;
    }

    private void LeftPress(Vector2 at) {
        PlaceHeld(false);
        if (Layer == Layer.Underground && Mode is ClickMode.Normal) {
            cursor.Release();
            if (view.PickPipe(at) is Vector2I pipe) panel.SelectPipe(pipe);
            else panel.Close();
            return;
        }
        var t = view.Pick(at, out float thingDistance);
        // A held stack goes into storage clicked in the world; a held container or plan places on open ground, even before a frame has seen the mouse there;
        // and a click anywhere else lets go of the stack, back to its cell.
        if (!cursor.Empty) {
            if (cursor.Stack is HeldStack held && t != null && (t.IsStorage || t.Kind == ThingKind.Hut && Barrel.Is(held.Item))) {
                kit.Run(() => moves.PutDown(t.Inventory, -1, int.MaxValue, t)); return;
            }
            if (Mode.Placing != null && Mode is not ClickMode.Dig) Place(at); else cursor.Release();
            return;
        }
        if (Mode.Placing != null && Mode is not ClickMode.Dig) { Place(at); return; }
        if (Mode is ClickMode.Remove) { world.Construction.Remove(RemovalAt(t, at)); return; }
        if (Mode is ClickMode.Assign assign) { AssignGather(assign, t); return; }
        var w = view.PickWorker(at, thingDistance);
        if (w != null) panel.Select(w);
        else if (t != null) ClickThing(t);
        else panel.Close();
    }

    /// <summary>What the remove selector points at with <paramref name="picked"/> under the mouse at a screen point, or nothing while the selector is away.</summary>
    public Removal RemovalAt(Thing? picked, Vector2 at) {
        if (Mode is not ClickMode.Remove remove) return Removal.None;
        return view.TryGroundPoint(at, out var ground, remove.Layer == Layer.Underground ? PipeGround.FloorY : 0)
            ? world.Construction.RemovalAt(picked, ground, remove.Layer)
            : picked == null ? Removal.None : world.Construction.RemovalAt(picked, picked.Pos);
    }

    private void AssignGather(ClickMode.Assign assign, Thing? t) {
        var worker = world.Workers.FirstOrDefault(w => w.Id == assign.Worker);
        if (worker == null || t?.IsResource != true) { kit.Notify("Choose a tree, stump, bush, leaves, stone, ore, coal, berry bush, nettle, firebug or water source."); return; }
        if (world.GatherBlock(t, true) is string blocked) { kit.Notify(blocked); return; }
        world.SetDuty(worker, t, JobKind.Gather, true); Mode = new ClickMode.Normal(); panel.Select(worker);
    }

    /// <summary>Opens a thing's page; a cabin's is the one the second press of a double click then walks into.</summary>
    private void ClickThing(Thing t) {
        if (t.Kind == ThingKind.Hut) clickedCabin = t;
        panel.Select(t);
    }

    /// <summary>
    /// Placement stays on without opening what was placed, so several go down in a row until the materials or containers run out.
    /// A mine opens its panel instead, since each cliff takes only one.
    /// The backpack still holds what a placement just sent will cost, and the mine's panel opens when the server names it.
    /// </summary>
    private void Place(Vector2 screen) {
        if (!view.TryGroundPoint(screen, out var ground, Mode.Placing == ThingKind.SteelPiping ? PipeGround.FloorY : 0)) return;
        var at = ground.Round();
        if (Mode is ClickMode.Container { BackpackSlot: >= 0 } selected && ContainerCount(selected) == 0) {
            Mode = new ClickMode.Normal();
            return;
        }
        bool sent = Sent(at);
        if (Mode is ClickMode.Container container) PlaceContainerAt(container, at);
        else if (Mode is ClickMode.Build build) world.Construction.Build(build.Kind, at, build.Rotation, build.Shape);
        // Pipe placement also takes up laid pipes, even with no materials left in the backpack.
        if (!sent || Mode.Placing == ThingKind.SteelPiping || Mode.Placing != ThingKind.Mine && CanPlaceAgain()) return;
        Mode = new ClickMode.Normal();
        // A plan on the cursor goes once nothing more can be paid for; a held container stack runs out by itself.
        if (placingHeld && cursor.Plan != null) cursor.Release();
    }

    /// <summary>Places the container from the cursor, its chosen backpack cell or the active hotbar stack.</summary>
    private void PlaceContainerAt(ClickMode.Container container, Vector2 at) {
        string item = container.Item;
        void Put() {
            if (Barrel.Is(item)) world.PlaceBarrel(item, at, container.Rotation); else world.PlaceCrate(item, at, container.Rotation);
        }
        if (placingHeld && cursor.Stack?.Item == item) world.FromHeldStack(Put);
        else if (container.BackpackSlot >= 0) hotbar.FromStack(container.BackpackSlot, Put);
        else if (hotbar.ActiveItem() == item) hotbar.FromStack(hotbar.ActiveStack(), Put);
        else Put();
    }

    /// <summary>Whether a placement at <paramref name="at"/> goes to the server, which it does when nothing here blocks it.</summary>
    private bool Sent(Vector2 at) {
        var kind = Mode.Placing!.Value;
        return (Mode is ClickMode.Container container ? world.Construction.PlacementBlock(kind, at, container.Item, container.Rotation)
            : world.Construction.PlacementBlock(kind, world.Construction.Snap(kind, at), rotation: Mode.Rotation, pipeShape: ((ClickMode.Build)Mode).Shape)) == null;
    }

    /// <summary>Whether another can be paid for, on top of the one just sent.</summary>
    private bool CanPlaceAgain() => Mode switch {
        ClickMode.Container container => ContainerCount(container) > 1,
        ClickMode.Build build => world.Data.Buildings[build.Kind].Cost.All(cost => world.Backpack[cost.Key] >= cost.Value * 2),
        _ => false
    };

    /// <summary>How many matching containers can be placed from the chosen cell or the backpack as a whole.</summary>
    private int ContainerCount(ClickMode.Container container) {
        if (container.BackpackSlot < 0) return world.Backpack[container.Item];
        var layout = world.Backpack.Layout;
        if (container.BackpackSlot >= layout.Count) return 0;
        var slot = layout[container.BackpackSlot];
        return slot.Item == container.Item ? slot.Free : 0;
    }
}
