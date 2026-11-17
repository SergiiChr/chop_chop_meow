using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// How the player's movement answers the movement keys: a key for about the opposite way brakes the run to a stop, holds a
/// moment to turn round and then runs back, building up pace again; any other change of keys turns at once.
/// </summary>
public struct Momentum {
    /// <summary>Seconds to brake from full pace to a stop, to stand turning round, and to get back up to full pace after.</summary>
    public const float BrakeSeconds = .25f, TurnSeconds = .1f, StartSeconds = .15f;
    /// <summary>Which way the player runs, and the share of their full pace they run at.</summary>
    public Vector2 Heading;
    public float Pace;
    private float turning;
    /// <summary>Slowing down against a key for the other way.</summary>
    public bool Braking { readonly get; private set; }

    /// <summary>The share of full speed, and its way, to move this frame with <paramref name="wanted"/> held, a unit vector or zero.</summary>
    public Vector2 Step(Vector2 wanted, float dt) {
        // Letting go of the keys stops the player at once, and starting from a stand is at full pace.
        if (wanted == Vector2.Zero) { this = default; return Vector2.Zero; }
        if (Heading == Vector2.Zero) { Heading = wanted; Pace = 1; }
        bool back = wanted.Dot(Heading) < -.5f;
        if (back && turning <= 0) Braking = true;
        if (Braking && !back) Braking = false;
        if (Braking) {
            Pace -= dt / BrakeSeconds;
            if (Pace > 0) return Heading * Pace;
            Braking = false; Pace = 0; turning = TurnSeconds;
        }
        Heading = wanted;
        if (turning > 0) { turning -= dt; return Vector2.Zero; }
        Pace = Math.Min(1, Pace + dt / StartSeconds);
        return Heading * Pace;
    }
}
