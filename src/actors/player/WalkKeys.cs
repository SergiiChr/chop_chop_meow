using Godot;

namespace ChopChopMeow;

/// <summary>
/// The movement keys and Shift held down by code that plays the player, as a player's fingers would hold them: each is
/// pressed or let go only when it changes.
/// </summary>
public sealed class WalkKeys {
    private int right, down;
    private bool sprint;

    /// <summary>
    /// Holds exactly the keys that walk right <paramref name="right"/> and down <paramref name="down"/> on screen, each
    /// -1, 0 or 1, and Shift to sprint; with none given it lets go of them all.
    /// </summary>
    public void Hold(int right = 0, int down = 0, bool sprint = false) {
        if (sprint != this.sprint) Press(Key.Shift, this.sprint = sprint);
        if (right != this.right) { Press(Of(this.right, Key.A, Key.D), false); Press(Of(right, Key.A, Key.D), true); this.right = right; }
        if (down != this.down) { Press(Of(this.down, Key.W, Key.S), false); Press(Of(down, Key.W, Key.S), true); this.down = down; }
    }

    private static Key Of(int way, Key back, Key ahead) => way < 0 ? back : way > 0 ? ahead : Key.None;

    private static void Press(Key key, bool pressed) {
        if (key != Key.None) Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed });
    }
}
