using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>What the next left click in the world does.</summary>
public abstract record ClickMode {
    /// <summary>A click selects or uses what is under it.</summary>
    public sealed record Normal : ClickMode;
    /// <summary>A click places a building of this kind.</summary>
    public sealed record Build(ThingKind Kind) : ClickMode;
    /// <summary>A click places this crate from the backpack.</summary>
    public sealed record Crate(string Item) : ClickMode;
    /// <summary>A click on a resource makes it this worker's gathering spot.</summary>
    public sealed record Assign(int Worker) : ClickMode;

    /// <summary>The kind being placed, or null when a click places nothing.</summary>
    public ThingKind? Placing => this switch { Build build => build.Kind, Crate => ThingKind.Crate, _ => null };
}

/// <summary>The player in the world: walking, gathering, lifting crates, placing buildings and clicking things.</summary>
public sealed class PlayerControl {
    private const float LiftSeconds = .6f;
    private readonly UiKit kit;
    private readonly WorldView view;
    private readonly SidePanel panel;
    private readonly Hotbar hotbar;
    private readonly World world;
    private Thing? gathering;
    /// <summary>A cabin the player is walking to after clicking it; they step inside on arrival.</summary>
    private Thing? entering;
    /// <summary>A crate held under the right mouse button; it is lifted once held for <see cref="LiftSeconds"/>.</summary>
    private Thing? lifting;
    private float liftClock;

    /// <summary>What the next left click in the world does.</summary>
    public ClickMode Mode { get; set; } = new ClickMode.Normal();

    public PlayerControl(UiKit kit, WorldView view, SidePanel panel, Hotbar hotbar) {
        this.kit = kit; this.view = view; this.panel = panel; this.hotbar = hotbar; world = kit.World;
        world.Replaced += () => { Stop(); Mode = new ClickMode.Normal(); };
    }

    /// <summary>Drops a gather, a crate lift and a walk into a cabin, whose targets belong to a world that went away.</summary>
    private void Stop() { gathering = null; lifting = null; entering = null; }

    /// <summary>Walks the player to a cabin's door and inside.</summary>
    public void WalkInto(Thing cabin) => entering = cabin;

    /// <summary>Enters placement for a crate from the backpack.</summary>
    public void PlaceCrate(string item) {
        if (world.Backpack[item] == 0) { kit.Notify("No empty crate in your backpack. Craft one at a bench."); return; }
        Mode = new ClickMode.Crate(item); panel.Close();
    }

    /// <summary>Moves the player for this frame from held keys and buttons, and carries on what a click started.</summary>
    public void Update(float dt) {
        if (kit.Paused) { world.Me.Sprinting = false; return; }
        bool right = Input.IsMouseButtonPressed(MouseButton.Right);
        var direction = TowardCabin(HeldDirection());
        direction = Gather(direction, right, dt);
        direction = Lift(direction, right, dt);
        Walk(direction, right, dt);
    }

    private static Vector2 HeldDirection() {
        float x = (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
        float y = (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0);
        return new Vector2(x + y * 2, y * 2 - x).Normalized();
    }

    /// <summary>A clicked cabin draws the player to its door, even with a movement key still held from walking there.</summary>
    private Vector2 TowardCabin(Vector2 direction) {
        if (world.Downed || entering != null && world.Find(entering.Id) == null) entering = null;
        if (entering == null) return direction;
        if (world.Enter(entering)) { entering = null; return direction; }
        return (entering.Pos - world.Player).Normalized();
    }

    /// <summary>Carries on a gather; a loose piece needs only one click: the player keeps walking to it after the button is released.</summary>
    private Vector2 Gather(Vector2 direction, bool right, float dt) {
        if (gathering == null || !right && !gathering.IsLoose || direction != Vector2.Zero || entering != null) {
            world.ResetGather();
            if (!right || gathering?.IsLoose == true) gathering = null;
            return direction;
        }
        // Step in close enough for the tool to reach the model; the lake edge keeps its wider reach.
        if (!world.Near(gathering, gathering.Kind == ThingKind.Water ? 2.5f : 1.35f)) return (world.GatherAt(gathering, world.Player) - world.Player).Normalized();
        if (gathering.IsLoose) {
            world.Gather(gathering, dt);
            if (world.Find(gathering.Id) == null) panel.Deselect(gathering.Id);
            gathering = null;
            return direction;
        }
        // Emptying it shows on the stump or bare bush; a target that was already empty or out of reach still says why.
        bool had = gathering.Remaining > 0; world.Gather(gathering, dt);
        if (world.GatherBlock(gathering) is string blocked) { if (!had || gathering.Remaining > 0) kit.Notify(blocked); gathering = null; }
        return direction;
    }

    /// <summary>A held right click walks to a crate and lifts it once the button has been held long enough.</summary>
    private Vector2 Lift(Vector2 direction, bool right, float dt) {
        if (lifting != null && right && direction == Vector2.Zero && entering == null && world.Find(lifting.Id) != null) {
            if (!world.Near(lifting, 1.5f)) direction = (lifting.Pos - world.Player).Normalized();
            else if ((liftClock += dt) >= LiftSeconds) {
                var crate = lifting; lifting = null;
                if (world.PickUpCrate(crate) && panel.Deselect(crate.Id) && panel.Page == Page.Inspect) panel.Close();
            }
        } else { lifting = null; liftClock = 0; }
        view.Lifting = lifting?.Id ?? 0; view.LiftProgress = liftClock / LiftSeconds;
        return direction;
    }

    private void Walk(Vector2 direction, bool right, float dt) {
        // Under 15% food or drink the player is too weak to sprint.
        world.Me.Sprinting = direction != Vector2.Zero && !world.Downed && Input.IsPhysicalKeyPressed(Key.Shift) && world.Hunger >= GameData.LowNeed && world.Thirst >= GameData.LowNeed;
        world.MovePlayer(direction * dt * world.Data.WalkSpeed * (world.Me.Sprinting ? world.Data.SprintSpeed : 1)); view.Gathering = gathering is { IsLoose: false } && right && world.Near(gathering, 2.5f);
        view.GatheringTarget = view.Gathering ? gathering : null;
        world.Me.Held = hotbar.ActiveItem() ?? "";
    }

    /// <summary>E: inspects the selected thing if it is in reach, else what is nearest.</summary>
    public void InspectNearest() {
        var t = world.Find(panel.Selection.Thing);
        if (t == null || !world.Near(t)) t = world.Things.Where(t => world.Near(t)).OrderBy(t => t.Pos.DistanceTo(world.Player)).FirstOrDefault();
        if (t != null) panel.Select(t);
    }

    /// <summary>Space: pumps a selected reinforced furnace in reach, else swings what is in hand.</summary>
    public void Swing() {
        var station = world.Find(panel.Selection.Thing);
        if (station != null && station.Kind == ThingKind.Reinforced && world.Near(station)) world.Pump(station); else world.Attack();
    }

    /// <summary>A mouse button over the world; <paramref name="overPanel"/> tells that the pointer is on a panel, which passes the wheel on when it has nothing to scroll, but must not zoom the world behind.</summary>
    public void Click(InputEventMouseButton mouse, bool overPanel) {
        if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed && !overPanel) view.Zoom = Math.Min(1.6f, view.Zoom + .1f);
        if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed && !overPanel) view.Zoom = Math.Max(.55f, view.Zoom - .1f);
        if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed) RightPress(mouse.Position);
        if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) LeftPress(mouse.Position);
    }

    private void RightPress(Vector2 at) {
        var t = view.Pick(at);
        if (t?.IsResource == true || t?.Kind == ThingKind.Farm) { gathering = t; panel.Mark(t); } else gathering = null;
        lifting = t?.Kind == ThingKind.Crate ? t : null; liftClock = 0;
    }

    private void LeftPress(Vector2 at) {
        if (Mode.Placing != null) { Place(view.ToWorld(at).Round()); return; }
        var t = view.Pick(at);
        if (Mode is ClickMode.Assign assign) { AssignGather(assign, t); return; }
        var w = view.PickWorker(at);
        if (w != null) panel.Select(w);
        else if (t != null) ClickThing(t);
        else if (hotbar.ActiveItem() is string held && hotbar.HasUse(held)) hotbar.UseHeld(view.ToWorld(at));
        else panel.Close();
    }

    private void AssignGather(ClickMode.Assign assign, Thing? t) {
        var worker = world.Workers.FirstOrDefault(w => w.Id == assign.Worker);
        if (worker == null || t?.IsResource != true) { kit.Notify("Choose a tree, leaves, stone, ore, coal, berry bush or water source."); return; }
        if (world.GatherBlock(t, true) is string blocked) { kit.Notify(blocked); return; }
        world.SetDuty(worker, t, "gather", true); Mode = new ClickMode.Normal(); panel.Select(worker);
    }

    private void ClickThing(Thing t) {
        if (t.Kind == ThingKind.Hut) entering = t;
        // A crate in hand only opens a clicked crate: the nesting alert is for deliberate moves inside the inventory.
        if (t.IsStorage && hotbar.ActiveItem() is string item && hotbar.IsTransferable(item) && world.Holds(t, item) && !(t.Kind == ThingKind.Crate && World.IsCrate(item))) StoreHeld(t, item);
        panel.Select(t);
    }

    private void StoreHeld(Thing t, string item) {
        if (!world.Near(t)) { kit.Notify("Walk closer to the chest to store the selected stack."); return; }
        int moved = 0;
        kit.Act(() => moved = world.Transfer(world.Backpack, t.Inventory, item, ItemMoves.All ? world.Backpack[item] : 1));
        if (!world.Remote && moved == 0) kit.Notify($"No {world.Data.Name(item)} to store, or chest is full.");
    }

    /// <summary>
    /// Placement stays on without opening what was placed, so several go down in a row until the materials or crates run out.
    /// A guest's building goes up on the host, so the guest stays in placement until Esc, except after a mine the host was sent.
    /// A mine opens its panel instead, since each cliff takes only one.
    /// </summary>
    private void Place(Vector2 at) {
        var built = Mode is ClickMode.Crate crate ? world.PlaceCrate(crate.Item, at) : world.Build(Mode.Placing!.Value, at);
        if (built?.Kind == ThingKind.Mine) { Mode = new ClickMode.Normal(); panel.Select(built); } else if (built != null && !CanPlaceAgain()) Mode = new ClickMode.Normal();
        else if (world.Remote && Mode.Placing == ThingKind.Mine && world.PlacementBlock(ThingKind.Mine, world.Snap(ThingKind.Mine, at)) == null) Mode = new ClickMode.Normal();
    }

    private bool CanPlaceAgain() => Mode switch {
        ClickMode.Crate crate => world.Backpack[crate.Item] > 0,
        ClickMode.Build build => world.Backpack.Has(world.Data.Buildings[build.Kind].Cost),
        _ => false
    };
}
