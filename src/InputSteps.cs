using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>Real input for the UI and network checks: keys, mouse and waiting for the game to react.</summary>
public partial class Game {
    private async Task Frames(int count = 2) {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Seconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Waits frame by frame until <paramref name="done"/> holds, and fails naming <paramref name="what"/> if it still does not after <paramref name="seconds"/> real seconds.</summary>
    private async Task Until(Func<bool> done, string what, double seconds = 60, [CallerFilePath] string file = "") {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (!done() && Time.GetTicksMsec() < end) await Frames(1);
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
        return GetViewport().GetFinalTransform() * control.GetGlobalRect().GetCenter();
    }

    /// <summary>Moves the mouse onto a control.</summary>
    private async Task Hover(Control control) {
        var at = await At(control);
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

    private async Task Click(Control control) {
        var at = await At(control);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }

    private async Task Click(Vector2 point) {
        var at = GetViewport().GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
        await Frames();
    }

    /// <summary>Two presses, the second flagged as a double click the way Godot reports one.</summary>
    private async Task DoubleClick(Control control) {
        var at = await At(control);
        foreach (bool second in new[] { false, true }) {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Pressed = true, DoubleClick = second });
            await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = at, GlobalPosition = at, Pressed = false });
            await Frames();
        }
    }

    /// <summary>A real mouse drag: press, move in steps past the drag threshold, release over the target.</summary>
    private async Task DragTo(Control from, Control to) {
        var start = await At(from); var end = await At(to);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = start, GlobalPosition = start, Pressed = true });
        await Frames();
        for (int i = 1; i <= 8; i++) {
            var at = start.Lerp(end, i / 8f);
            Input.ParseInputEvent(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Position = at, GlobalPosition = at, Relative = (end - start) / 8 });
            await Frames(1);
        }
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = end, GlobalPosition = end, Pressed = false });
        await Frames();
    }

    /// <summary>A right click on a stack, a move to the target and a left click there.</summary>
    private async Task SplitTo(Control from, Control to) {
        var start = await At(from); var end = await At(to);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = start, GlobalPosition = start, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = start, GlobalPosition = start, Pressed = false });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = end, GlobalPosition = end, Relative = end - start });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Position = end, GlobalPosition = end, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = end, GlobalPosition = end, Pressed = false });
        await Frames();
    }

    /// <summary>Clicks open ground beside the player, so the click uses the item in hand instead of selecting something.</summary>
    private async Task ClickGround() {
        await Frames();
        var open = Enumerable.Range(1, 6).SelectMany(r => Enumerable.Range(0, 8).Select(a => world.Player + Vector2.Right.Rotated(a * Mathf.Tau / 8) * r))
            .Select(p => view.Screen(p)).First(p => view.Pick(p) == null && view.PickWorker(p) == null && GetViewport().GetVisibleRect().HasPoint(p) && p.Y < GetViewport().GetVisibleRect().Size.Y - 120);
        await Click(open);
    }

    /// <summary>Clicks a thing in the world where its model shows, clear of workers and of the panels over the world.</summary>
    private async Task ClickOn(Thing thing) {
        await Frames();
        var spots = Enumerable.Range(0, 24).Select(i => view.Screen(thing.Pos, i * .15f)).Where(p => view.Pick(p)?.Id == thing.Id && view.PickWorker(p) == null && Uncovered(p)).ToArray();
        Require(spots.Length > 0, $"No spot on the {thing.Kind} can be clicked: it is off screen, hidden or under a panel.");
        await Click(spots[0]);
    }

    /// <summary>Whether a click at this point reaches the world rather than a panel over it.</summary>
    private bool Uncovered(Vector2 point) => ui.GetChildren().OfType<Control>().All(c => !c.Visible || c.MouseFilter == Control.MouseFilterEnum.Ignore || !c.GetGlobalRect().HasPoint(point));

    /// <summary>Esc until no placement, side panel or backpack drawer is left; with nothing open it does nothing, so it never opens the pause menu.</summary>
    private async Task CloseWithEsc() {
        for (int i = 0; i < 3 && (view.BuildMode != null || panel.Visible || bag.Visible); i++) await KeyStroke(Key.Escape);
    }

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

    private async Task RightClick(Vector2 at) {
        var point = GetViewport().GetFinalTransform() * view.Screen(at, .2f);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = point, GlobalPosition = point, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = point, GlobalPosition = point, Pressed = false });
        await Frames();
    }
}
