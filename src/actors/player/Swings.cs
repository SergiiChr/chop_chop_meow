using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Players' swings at wolves in <paramref name="world"/>: an axe, a torch or a fist is swung the way the player aims,
/// and lands when the animation brings it down, on every wolf whose body it touches then.
/// </summary>
public sealed class Swings(World world) {
    /// <summary>
    /// What each swing passes over as it lands, measured on the player's model: the overhead chop comes down on a strip
    /// straight ahead to the height of a wolf's back, the sideways sweep crosses from behind the left shoulder all the way
    /// round the front to the right at chest height, over a wolf on the ground, the torch arm sweeps across in front, bent
    /// over to bring the flame down to a wolf, and the right fist reaches out a little to the right. A fist counts at any height.
    /// </summary>
    public static readonly SwingShape Chop = new(.5f, 1.35f, 10, 8, .7f), Sweep = new(.5f, 1.3f, 138, 62, 1.1f), Torch = new(0, 1, 52, 33, .65f), Punch = new(0, .7f, -10, 35);
    /// <summary>What a punch and a torch take off a wolf's health, and an axe for each unit of its speed.</summary>
    public const float PunchDamage = .5f, TorchDamage = 1, AxeDamage = 1.5f;
    /// <summary>How far either side of its damage a blow can land: each one is weaker or stronger by up to this much.</summary>
    public const float PunchSpread = .1f, TorchSpread = .2f, AxeSpread = .5f;
    /// <summary>Seconds a player waits between one swing and the next.</summary>
    public const float Cooldown = .6f;
    /// <summary>How many round sections along a wolf's body, from the root of its tail to its nose, a swing is tried against.</summary>
    private const int BodySections = 7;
    // Seeded, so a recording of a fight plays the same every time.
    private readonly Random rolls = new(7919);

    /// <summary>The ground a swing passes over as it lands; nothing for an action that is not a swing.</summary>
    public static SwingShape Shape(ActorAction action) => action switch {
        ActorAction.AxeSwing or ActorAction.SteelAxeSwing => Chop,
        ActorAction.AxeSweep or ActorAction.SteelAxeSweep => Sweep,
        ActorAction.TorchSwing => Torch,
        ActorAction.Attack => Punch,
        _ => default
    };

    /// <summary>Whether a swing from <paramref name="from"/> along <paramref name="aim"/>, a unit vector, touches a wolf's body.</summary>
    public static bool Touches(SwingShape shape, Vector2 from, Vector2 aim, Wolf wolf) {
        var (tail, nose) = WolfBodies.Body(wolf);
        float top = WolfBodies.Top(wolf);
        for (int i = 0; i < BodySections; i++)
            if (shape.Touches(from, aim, tail.Lerp(nose, i / (BodySections - 1f)), WolfBodies.WolfHalfWidth, top)) return true;
        return false;
    }

    /// <summary>What a swing takes off a wolf's health, and how far either side of that a blow can land.</summary>
    private static (float Damage, float Spread) Blow(ActorAction action) => action switch {
        ActorAction.Attack => (PunchDamage, PunchSpread),
        ActorAction.TorchSwing => (TorchDamage, TorchSpread),
        _ => (AxeDamage, AxeSpread)
    };

    /// <summary>
    /// The acting player starts a swing along <paramref name="aim"/>, a unit vector; it lands when its animation does.
    /// <paramref name="strength"/> is the speed of the axe swung, which multiplies its damage.
    /// </summary>
    internal void Start(ActorAction action, Vector2 aim, float strength = 1) {
        var (damage, spread) = Blow(action);
        world.S.Actor.Swing = new Swing(action, aim, damage * strength, spread, ActorAnimation.Contact(action));
    }

    /// <summary>Counts the acting player's swing down and lands it; one who went down or indoors meanwhile lands nothing.</summary>
    internal void Tick(float dt) {
        if (world.S.Actor.Swing is not Swing swing) return;
        if (world.Downed || world.S.Sheltered != 0) { world.S.Actor.Swing = null; return; }
        if (swing.In > dt) { world.S.Actor.Swing = swing with { In = swing.In - dt }; return; }
        world.S.Actor.Swing = null;
        Land(swing);
    }

    /// <summary>
    /// A swing lands on every wolf it touches: each takes its own roll of the damage and is knocked back.
    /// A torch leaves alone a wolf in the air in a throat leap or feeding, and sends the ones it burns running for a
    /// moment.
    /// </summary>
    private void Land(Swing swing) {
        var shape = Shape(swing.Action);
        bool torch = swing.Action == ActorAction.TorchSwing;
        var wolves = world.S.Wolves;
        // A wolf that falls leaves the list, so it is walked from its end.
        for (int i = wolves.Count - 1; i >= 0; i--) {
            var wolf = wolves[i];
            if (torch && (wolf.Leap > 0 && WolfSnap.InAir(wolf) || wolf.Feeding) || !Touches(shape, world.Player,
                swing.Aim, wolf)) continue;
            world.Strike(wolf, swing.Damage + ((float)rolls.NextDouble() * 2 - 1) * swing.Spread, burnt: torch);
            if (torch) wolf.Flee = World.FleeSeconds;
        }
    }
}
