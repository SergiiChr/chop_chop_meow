using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

/// <summary>Real input for the UI and network checks: keys, mouse and waiting for the game to react.</summary>
public partial class Game {
    /// <summary>Waits for frames; in the UI checks each one starts with what the server has made of everything so far.</summary>
    private async Task Frames(int count = 2) {
        for (int i = 0; i < count; i++) { Settle(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    }

    private async Task Seconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Waits frame by frame until <paramref name="done"/> holds, and fails naming <paramref name="what"/> if it still does not after <paramref name="seconds"/> real seconds.</summary>
    private async Task Until(Func<bool> done, string what, double seconds = 60, [CallerFilePath] string file = "") {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        WaitingFor(what);
        while (!done() && Time.GetTicksMsec() < end) await Frames(1);
        WaitingFor("");
        Require(done(), $"{what} never happened.", file);
    }

    private async Task KeyStroke(Key key) {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        await Frames();
    }

    /// <summary>Window position of a control's centre, scrolled into view first.</summary>
    private async Task<Vector2> At(Control control) {
        for (Node? n = control.GetParent(); n != null; n = n.GetParent()) if (n is ScrollContainer scroll) scroll.EnsureControlVisible(control);
        await Frames();
        return WindowPoint(control);
    }

    /// <summary>Window position of a spot just inside a control's lower edge, scrolled into view first.</summary>
    private async Task<Vector2> AtFoot(Control control) {
        await At(control);
        return WindowPoint(control, new Vector2(control.Size.X / 2, control.Size.Y - 2));
    }

    /// <summary>Where a control's centre is in the window, through its canvas layer and the window's stretch.</summary>
    public static Vector2 WindowPoint(Control control) => WindowPoint(control, control.Size / 2);

    /// <summary>Where a point given in a control's own units is in the window.</summary>
    private static Vector2 WindowPoint(Control control, Vector2 local) => control.GetViewport().GetFinalTransform() * (control.GetGlobalTransformWithCanvas() * local);

    /// <summary>Moves the mouse onto a control.</summary>
    private async Task Hover(Control control) {
        var at = await At(control);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await Frames();
    }

    /// <summary>Moves the mouse onto a point of the world on screen.</summary>
    private async Task Hover(Vector2 point) {
        var at = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await Frames();
    }

    /// <summary>Opens an option button with a click and picks an item with the arrow keys and Enter.</summary>
    private async Task Choose(OptionButton picker, int index) {
        await Click(picker);
        // The popup opens with nothing focused, so the first press lands on the first item.
        for (int i = 0; i <= index; i++) await KeyStroke(Key.Down);
        await KeyStroke(Key.Enter);
    }

    // Where in the window the last click pressed and when the game had handled that press, so the next click there waits long enough not to make a double click.
    private Vector2? lastClicked;
    private ulong lastClickAt;

    private async Task Click(Control control, bool alt = false) {
        var at = await At(control);
        await Apart(at);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, AltPressed = alt, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Handled(at);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, AltPressed = alt, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }

    /// <summary>Waits until a press at a window point is a click of its own, not the second half of a double click with the press before it there.</summary>
    private async Task Apart(Vector2 at) {
        if (lastClicked == at) while (Time.GetTicksMsec() - lastClickAt <= SlotInput.DoubleClickMs) await Frames(1);
    }

    /// <summary>Notes a press at a window point once the frames after it have passed, by which time the game has handled it: parsed input waits for the next frame.</summary>
    private void Handled(Vector2 at) { lastClicked = at; lastClickAt = Time.GetTicksMsec(); }

    private async Task Click(Vector2 point) {
        var at = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }

    /// <summary>
    /// Two clicks within a double click's time, the second press also flagged as a double click the way Godot reports one.
    /// Each press and release waits out the frames after it, as real ones do, so a page the first click redraws is drawn before the second press.
    /// </summary>
    private async Task DoubleClick(Control control) {
        var at = await At(control);
        await Apart(at);
        await DoublePress(at);
        Handled(at);
    }

    /// <summary>Double clicks a window point in the world, as <see cref="DoubleClick(Control)"/> does a control.</summary>
    private async Task DoubleClick(Vector2 point) => await DoublePress(GetViewport().GetFinalTransform() * point);

    /// <summary>Two presses and releases at a window point, the second press flagged as a double click, each waiting out the frames after it.</summary>
    private async Task DoublePress(Vector2 at) {
        foreach (bool second in new[] { false, true }) {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true, DoubleClick = second });
            await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
            await Frames();
        }
        await Frames();
    }

    /// <summary>Picks up a cell's stack with a left click, which on a cell with a use waits out a possible second press first.</summary>
    private async Task PickUp(Control cell) {
        await Click(cell);
        await Until(() => !cursor.Empty, "the stack going onto the cursor", 3);
    }

    /// <summary>Picks up half a cell's stack with a right click.</summary>
    private async Task PickUpHalf(Control cell) {
        await RightClick(cell);
        await Until(() => !cursor.Empty, "half the stack going onto the cursor", 3);
    }

    /// <summary>Presses the left button on one control and lets it go over another.</summary>
    private async Task PressAndLetGoOver(Control from, Control to) {
        var start = await At(from); var end = await At(to);
        await Apart(start);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = start, GlobalPosition = start, Pressed = true });
        await Frames();
        Handled(start);
        Input.ParseInputEvent(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Position = end, GlobalPosition = end, Relative = end - start });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = end, GlobalPosition = end, Pressed = false });
        await Frames();
    }

    /// <summary>Picks up a cell's stack and puts it down on another cell with a second left click.</summary>
    private async Task Carry(Control from, Control to) {
        await PickUp(from);
        await Click(to);
    }

    /// <summary>Presses the left button at one window point and moves in steps to another, keeping it held; returns where it ends.</summary>
    private async Task<Vector2> HoldOver(Vector2 start, Vector2 end) {
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = start, GlobalPosition = start, Pressed = true });
        await Frames();
        for (int i = 1; i <= 8; i++) {
            var at = start.Lerp(end, i / 8f);
            Input.ParseInputEvent(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Relative = (end - start) / 8 });
            await Frames(1);
        }
        return end;
    }

    /// <summary>Lets go of the left button at a window point.</summary>
    private async Task LetGo(Vector2 end) {
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = end, GlobalPosition = end, Pressed = false });
        await Frames();
    }

    /// <summary>Drags a window by a spot just inside its top left corner, clear of what it holds, by a distance in the interface's units.</summary>
    private async Task MoveWindow(Control window, Vector2 by) {
        var corner = new Vector2(6, 6);
        await LetGo(await HoldOver(WindowPoint(window, corner), WindowPoint(window, corner + by)));
    }

    /// <summary>Picks up half a cell's stack with a right click and puts it down on another cell with a left click.</summary>
    private async Task SplitTo(Control from, Control to) {
        await PickUpHalf(from);
        await Click(to);
    }

    /// <summary>Clicks open ground beside the player with the given mouse button.</summary>
    private async Task ClickGround(MouseButton button = MouseButton.Left) {
        await Frames();
        var open = Enumerable.Range(1, 6).SelectMany(r => Enumerable.Range(0, 8).Select(a => world.Player + Vector2.Right.Rotated(a * Mathf.Tau / 8) * r))
            .First(at => {
                var screen = view.Screen(at);
                return GetViewport().GetVisibleRect().HasPoint(screen) && Uncovered(screen) && view.Pick(screen) == null && view.PickWorker(screen) == null;
            });
        if (button == MouseButton.Right) await RightClick(open, 0); else await Click(view.Screen(open));
    }

    /// <summary>Clicks a thing in the world where its model shows, clear of workers and of the panels over the world.</summary>
    private async Task ClickOn(Thing thing) => await Click(await SpotOn(thing));

    /// <summary>Double clicks a thing in the world where its model shows, clear of workers and of the panels over the world.</summary>
    private async Task DoubleClickOn(Thing thing) => await DoubleClick(await SpotOn(thing));

    /// <summary>A point on a thing's model in the world that a click reaches, clear of workers and of the panels over it.</summary>
    private async Task<Vector2> SpotOn(Thing thing) {
        await Frames();
        var spots = Enumerable.Range(0, 24).Select(i => view.Screen(thing.Pos, i * .15f)).Where(p => view.Pick(p)?.Id == thing.Id && view.PickWorker(p) == null && Uncovered(p)).ToArray();
        Require(spots.Length > 0, $"No spot on the {thing.Kind} can be clicked: it is off screen, hidden or under a panel.");
        return spots[0];
    }

    /// <summary>Whether a click at this point reaches the world rather than a panel over it.</summary>
    private bool Uncovered(Vector2 point) => ui.GetChildren().OfType<Control>().All(c => !c.Visible || c.MouseFilter == Control.MouseFilterEnum.Ignore || !(layer.Transform * c.GetGlobalRect()).HasPoint(point));

    /// <summary>Esc until no placement, side panel or backpack drawer is left; with nothing open it does nothing, so it never opens the pause menu.</summary>
    private async Task CloseWithEsc() {
        // A page about something a check has just removed closes by itself on the panel's next refresh after the window hears of it,
        // and Esc pressed after that would open the pause menu.
        await Frames(1);
        await Until(() => !panel.SubjectGone, "the page of something that is gone closing", 3);
        for (int i = 0; i < 3 && (player.Mode.Placing != null && player.CanCancelMode || panel.Visible || bag.Visible); i++) await KeyStroke(Key.Escape);
    }

    /// <summary>Whole spots round the player, two to four tiles out and the nearest ring first: where a check puts something the player can reach.</summary>
    private IEnumerable<Vector2> SpotsNearby() =>
        Enumerable.Range(2, 3).SelectMany(r => Enumerable.Range(0, 8).Select(a => (world.Player + Vector2.Right.Rotated(a * Mathf.Tau / 8) * r).Round()));

    /// <summary>Holds D until the player moves, so it shows the world is not held.</summary>
    private async Task<bool> PlayerWalks() {
        var from = world.Player;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        for (ulong end = Time.GetTicksMsec() + 1000; world.Player == from && Time.GetTicksMsec() < end;) await Frames(1);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        await Frames();
        return world.Player != from;
    }

    private async Task Wheel(Vector2 point, MouseButton wheel = MouseButton.WheelDown) {
        var at = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = wheel, Position = at, GlobalPosition = at, Pressed = true, Factor = 1 });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = wheel, Position = at, GlobalPosition = at, Pressed = false, Factor = 1 });
        await Frames();
    }

    private async Task Type(string text) {
        foreach (char c in text) {
            Input.ParseInputEvent(new InputEventKey { Keycode = (Key)c, Unicode = c, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = (Key)c, Unicode = c, Pressed = false });
            await Frames(1);
        }
    }

    private async Task RightClick(Control control) {
        var at = await At(control);
        // A right press comes between two left ones, so a left click after it makes no double click either.
        lastClicked = null;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        RightRelease(at);
        await Frames();
    }

    /// <summary>Runs a step with a key held down, and lets go of it even when the step fails.</summary>
    private static async Task Holding(Key key, Func<Task> step) {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        try { await step(); } finally { Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); }
    }

    private async Task RightClick(Vector2 at, float height = .2f) {
        var point = RightPress(at, height);
        await Frames();
        RightRelease(point);
        await Frames();
    }

    /// <summary>Presses the right mouse button on a world position, and returns the screen point for the release.</summary>
    private Vector2 RightPress(Vector2 at, float height = .2f) {
        var point = GetViewport().GetFinalTransform() * view.Screen(at, height);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = point, GlobalPosition = point, Pressed = true });
        return point;
    }

    private static void RightRelease(Vector2 point) =>
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = point, GlobalPosition = point, Pressed = false });
}
