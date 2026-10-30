using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The player in the world: walking, gathering, lifting crates, placing buildings and clicking things.</summary>
public sealed class PlayerControl(UiKit kit, WorldView view, SidePanel panel, Hotbar hotbar) {
    private const float LiftSeconds = .6f;
    private readonly World world = kit.World;
    private readonly UiState state = kit.State;
    private Thing? gathering;
    /// <summary>A crate held under the right mouse button; it is lifted once held for <see cref="LiftSeconds"/>.</summary>
    private Thing? lifting;
    private float liftClock;

    /// <summary>Drops a gather, a crate lift and a walk into a cabin, whose targets belong to a world that went away.</summary>
    public void Stop() { gathering = null; lifting = null; state.Entering = null; }

    /// <summary>Moves the player for this frame from held keys and buttons, and carries on what a click started.</summary>
    public void Update(float dt) {
        if (!state.Paused) {
            float x = (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
            float y = (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0);
            Vector2 direction = new Vector2(x + y * 2, y * 2 - x).Normalized();
            bool right = Input.IsMouseButtonPressed(MouseButton.Right);
            // A clicked cabin draws the player to its door, even with a movement key still held from walking there.
            if (world.Downed || state.Entering != null && world.Find(state.Entering.Id) == null) state.Entering = null;
            if (state.Entering != null) { if (world.Enter(state.Entering)) state.Entering = null; else direction = (state.Entering.Pos - world.Player).Normalized(); }
            // A loose piece needs only one click: the player keeps walking to it after the button is released.
            if (gathering != null && (right || gathering.IsLoose) && direction == Vector2.Zero && state.Entering == null) {
                // Step in close enough for the tool to reach the model; the lake edge keeps its wider reach.
                if (!world.Near(gathering, gathering.Kind == ThingKind.Water ? 2.5f : 1.35f)) direction = (world.GatherAt(gathering, world.Player) - world.Player).Normalized();
                else if (gathering.IsLoose) {
                    world.Gather(gathering, dt);
                    if (world.Find(gathering.Id) == null && state.Selected == gathering.Id) { state.Selected = 0; view.Selected = 0; }
                    gathering = null;
                } else {
                    // Emptying it shows on the stump or bare bush; a target that was already empty or out of reach still says why.
                    bool had = gathering.Remaining > 0; world.Gather(gathering, dt);
                    if (world.GatherBlock(gathering) is string blocked) { if (!had || gathering.Remaining > 0) kit.Notify(blocked); gathering = null; }
                }
            } else { world.ResetGather(); if (!right || gathering?.IsLoose == true) gathering = null; }
            // A held right click walks to a crate and lifts it once the button has been held long enough.
            if (lifting != null && right && direction == Vector2.Zero && state.Entering == null && world.Find(lifting.Id) != null) {
                if (!world.Near(lifting, 1.5f)) direction = (lifting.Pos - world.Player).Normalized();
                else if ((liftClock += dt) >= LiftSeconds) {
                    var crate = lifting; lifting = null;
                    if (world.PickUpCrate(crate) && state.Selected == crate.Id) { state.Selected = 0; view.Selected = 0; if (state.Page == "inspect") panel.Close(); }
                }
            } else { lifting = null; liftClock = 0; }
            view.Lifting = lifting?.Id ?? 0; view.LiftProgress = liftClock / LiftSeconds;
            // Under 15% food or drink the player is too weak to sprint.
            world.Me.Sprinting = direction != Vector2.Zero && !world.Downed && Input.IsPhysicalKeyPressed(Key.Shift) && world.S.Hunger >= GameData.LowNeed && world.S.Thirst >= GameData.LowNeed;
            world.MovePlayer(direction * dt * world.Data.WalkSpeed * (world.Me.Sprinting ? world.Data.SprintSpeed : 1)); view.Gathering = gathering is { IsLoose: false } && right && world.Near(gathering, 2.5f);
            view.GatheringTarget = view.Gathering ? gathering : null;
            world.Me.Held = hotbar.ActiveItem() ?? "";
        } else world.Me.Sprinting = false;
    }

    /// <summary>A mouse button over the world; <paramref name="overPanel"/> tells that the pointer is on a panel, which passes the wheel on when it has nothing to scroll, but must not zoom the world behind.</summary>
    public void Click(InputEventMouseButton mouse, bool overPanel) {
        if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed && !overPanel) view.Zoom = Math.Min(1.6f, view.Zoom + .1f);
        if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed && !overPanel) view.Zoom = Math.Max(.55f, view.Zoom - .1f);
        if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed) {
            var t = view.Pick(mouse.Position); if (t?.IsResource == true || t?.Kind == ThingKind.Farm) { gathering = t; state.Selected = t.Id; view.Selected = t.Id; } else gathering = null;
            lifting = t?.Kind == ThingKind.Crate ? t : null; liftClock = 0;
        }
        if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) {
            if (view.BuildMode != null) {
                var at = view.ToWorld(mouse.Position).Round();
                var built = view.BuildMode == ThingKind.Crate ? world.PlaceCrate(state.PlacingCrate, at) : world.Build(view.BuildMode.Value, at);
                // Placement stays on without opening what was placed, so several go down in a row until the materials or crates run out.
                // A guest's building goes up on the host, so the guest stays in placement until Esc, except after a mine the host was sent.
                // A mine opens its panel instead, since each cliff takes only one.
                if (built?.Kind == ThingKind.Mine) { view.BuildMode = null; panel.Select(built); } else if (built != null && !CanPlaceAgain()) view.BuildMode = null;
                else if (world.Remote && view.BuildMode == ThingKind.Mine && world.PlacementBlock(ThingKind.Mine, world.Snap(ThingKind.Mine, at)) == null) view.BuildMode = null;
                return;
            }
            var t = view.Pick(mouse.Position);
            if (state.AssigningWorker > 0) {
                var worker = world.S.Workers.FirstOrDefault(w => w.Id == state.AssigningWorker);
                if (worker != null && t?.IsResource == true) {
                    string? blocked = world.GatherBlock(t, true);
                    if (blocked != null) kit.Notify(blocked); else { world.SetDuty(worker, t, "gather", true); state.AssigningWorker = 0; panel.Select(worker); }
                } else kit.Notify("Choose a tree, leaves, stone, ore, coal, berry bush or water source.");
                return;
            }
            var w = view.PickWorker(mouse.Position);
            if (w != null) panel.Select(w);
            else if (t != null) {
                if (t.Kind == ThingKind.Hut) state.Entering = t;
                // A crate in hand only opens a clicked crate: the nesting alert is for deliberate moves inside the inventory.
                if (t.IsStorage && hotbar.ActiveItem() is string item && hotbar.IsTransferable(item) && world.Holds(t, item) && !(t.Kind == ThingKind.Crate && World.IsCrate(item))) {
                    if (world.Near(t)) { int moved = 0; kit.Act(() => moved = world.Transfer(world.S.Backpack, t.Inventory, item, ItemMoves.All ? world.S.Backpack[item] : 1)); if (!world.Remote && moved == 0) kit.Notify($"No {world.Data.Name(item)} to store, or chest is full."); } else kit.Notify("Walk closer to the chest to store the selected stack.");
                }
                panel.Select(t);
            } else if (hotbar.ActiveItem() is string held && hotbar.HasUse(held)) hotbar.UseHeld(view.ToWorld(mouse.Position));
            else panel.Close();
        }
    }

    private bool CanPlaceAgain() => view.BuildMode == ThingKind.Crate ? world.S.Backpack[state.PlacingCrate] > 0 : view.BuildMode is ThingKind kind && world.S.Backpack.Has(world.Data.Buildings[kind].Cost);
}
