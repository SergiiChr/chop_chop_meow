using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Plays the player of the firebug showcase with the keys and mouse a player has, for a recording of a chase:
/// sprints after the nearest firebug, slows to a walk close by, and right-clicks it once the hoop can reach it.
/// </summary>
public sealed partial class FirebugChase : Node {
    public required World World;
    public required WorldView View;
    /// <summary>How near a firebug the player walks instead of sprinting, and the nearest and furthest the hoop reaches it from, in metres.</summary>
    private const float WalkWithin = 2.2f, TooNear = 1.05f, InReach = 1.3f;
    private static readonly Key[] Keys = [Key.A, Key.D, Key.W, Key.S, Key.Shift];
    private readonly bool[] held = new bool[Keys.Length];
    // Seconds left of a swing just asked for, while nothing else is pressed.
    private double swinging;

    public override void _Process(double delta) {
        if ((swinging -= delta) > 0) return;
        Thing? bug = null;
        foreach (var t in World.S.Things)
            if (t.Kind == ThingKind.Firebug && (bug == null || World.Firebugs.At(t).DistanceTo(World.Player) < World.Firebugs.At(bug).DistanceTo(World.Player))) bug = t;
        if (bug == null) { Press(0, 0, false); return; }
        var to = World.Firebugs.At(bug) - World.Player;
        float away = to.Length();
        if (away is >= TooNear and <= InReach) {
            Press(0, 0, false);
            var at = View.Screen(World.Firebugs.At(bug), FirebugShape.Height);
            foreach (bool pressed in (ReadOnlySpan<bool>)[true, false])
                Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Right, Pressed = pressed });
            swinging = ActorAnimation.Duration(ActorAction.NetSwing) + .15;
            return;
        }
        // Of the eight ways the keys walk, the one nearest the way to the firebug, or straight away from it when too near.
        var toward = away < TooNear ? -to : to;
        var (x, y, best) = (0, 0, -2f);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && PlayerControl.KeyDirection(dx, dy).Dot(toward.Normalized()) is float fit && fit > best) (x, y, best) = (dx, dy, fit);
        Press(x, y, away > WalkWithin);
    }

    /// <summary>Holds the keys that walk right <paramref name="x"/> and down <paramref name="y"/> on screen, each -1, 0 or 1, and the sprint key, and lets go of the rest.</summary>
    private void Press(int x, int y, bool sprint) {
        ReadOnlySpan<bool> wanted = [x < 0, x > 0, y < 0, y > 0, sprint];
        for (int i = 0; i < Keys.Length; i++) {
            if (held[i] == wanted[i]) continue;
            held[i] = wanted[i];
            Input.ParseInputEvent(new InputEventKey { Keycode = Keys[i], PhysicalKeycode = Keys[i], Pressed = wanted[i] });
        }
    }
}
