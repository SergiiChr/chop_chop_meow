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
    /// <summary>How near a firebug the player walks instead of sprinting, and how far inside the hoop's reach the player stops to swing, in metres.</summary>
    private const float WalkWithin = 2.2f, Margin = .1f;
    /// <summary>Seconds the player stands after a swing has played before moving on.</summary>
    private const float Pause = .15f;
    private readonly WalkKeys keys = new();
    // Seconds left of a swing just asked for, while nothing else is pressed.
    private double swinging;

    public override void _Process(double delta) {
        if ((swinging -= delta) > 0) return;
        Vector2? nearest = null;
        foreach (var t in World.S.Things.Of(ThingKind.Firebug))
            if (World.Firebugs.At(t) is var at && (nearest is not Vector2 best || at.DistanceSquaredTo(World.Player) < best.DistanceSquaredTo(World.Player))) nearest = at;
        if (nearest is not Vector2 bug) { keys.Hold(); return; }
        var to = bug - World.Player;
        float away = to.Length();
        if (away >= Swings.Net.Near + Margin && away <= Swings.Net.Far - Margin) {
            keys.Hold();
            // A click is given in the window's units, which the window's stretch sets apart from the view's.
            var point = GetViewport().GetFinalTransform() * View.Screen(bug, FirebugShape.Height);
            foreach (bool pressed in (ReadOnlySpan<bool>)[true, false])
                Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right, Pressed = pressed });
            swinging = ActorAnimation.Duration(ActorAction.NetSwing) + Pause;
            return;
        }
        // Of the eight ways the keys walk, the one nearest the way to the firebug, or straight away from it when too near.
        var toward = away < Swings.Net.Near + Margin ? -to : to;
        var (right, down, fit) = (0, 0, float.NegativeInfinity);
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if ((x != 0 || y != 0) && PlayerControl.KeyDirection(x, y).Dot(toward) is float fits && fits > fit) (right, down, fit) = (x, y, fits);
        keys.Hold(right, down, away > WalkWithin);
    }
}
