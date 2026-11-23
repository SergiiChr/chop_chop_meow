using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// How wolves at a player in <paramref name="world"/> keep their distance: each closes in only to where its jaws reach,
/// and one whose body runs into another's steps aside.
/// </summary>
public sealed class WolfBodies(World world) {
    /// <summary>How far a wolf's body reaches ahead of where it stands to its nose and back to its tail's root, and half its width, in metres.</summary>
    public const float WolfNose = .76f, WolfTail = .5f, WolfHalfWidth = .18f;
    /// <summary>Metres a second a wolf steps back from a player who comes too close, and steps aside from another wolf in its way.</summary>
    public const float BackOffSpeed = 1.2f, SideStepSpeed = 3;
    /// <summary>How far from a player a wolf closing in on them stops, within biting reach of them.</summary>
    public const float StandOff = 1.05f;

    /// <summary>Where a wolf's body runs from the root of its tail to its nose, facing <paramref name="facing"/>.</summary>
    public static (Vector2 Tail, Vector2 Nose) Body(Wolf wolf, Vector2 facing) {
        return Along(wolf, facing == wolf.Pos ? Vector2.Right : (facing - wolf.Pos).Normalized());
    }

    /// <summary>
    /// How tall a wolf's body stands on the ground, how much higher a leap for the throat carries it, and how tall it
    /// stands reared up with a player's arm in its jaws, in metres.
    /// </summary>
    public const float StandingTop = .87f, LeapRise = .5f, RearedTop = 1.16f;

    /// <summary>How high off the ground the top of a wolf's body is: it rises through the first part of a leap for the throat, and stands taller reared up.</summary>
    public static float Top(Wolf wolf) {
        if (wolf.Leap <= 0 || !WolfSnap.InAir(wolf)) return wolf.Side != 0 ? RearedTop : StandingTop;
        float flown = World.LeapSeconds - wolf.Leap;
        return StandingTop + LeapRise * Mathf.SmoothStep(.05f, .875f, flown / World.LeapSeconds) +
            Movement.Rise(flown, World.LeapSeconds);
    }

    /// <summary>Where a wolf's body runs from the root of its tail to its nose, the way it faces (<see cref="Wolf.Heading"/>).</summary>
    public static (Vector2 Tail, Vector2 Nose) Body(Wolf wolf) => Along(wolf, Vector2.Right.Rotated(wolf.Heading));

    private static (Vector2 Tail, Vector2 Nose) Along(Wolf wolf, Vector2 ahead) => (wolf.Pos - ahead * WolfTail, wolf.Pos + ahead * WolfNose);

    /// <summary>
    /// How far a wolf closing in on the acting player goes straight at them this tick, up to <paramref name="step"/>: on to
    /// <see cref="StandOff"/>, where its jaws reach them, or back to it at a walk when they come closer.
    /// </summary>
    public static float Closing(Wolf wolf, Vector2 at, float step, float dt) =>
        Math.Clamp(wolf.Pos.DistanceTo(at) - StandOff, -Math.Min(step, dt * BackOffSpeed), step);

    /// <summary>
    /// Wolves at the acting player crowd round them without their bodies running into each other: one that overlaps another
    /// steps aside, unless it holds the player, by the jaws or in the air.
    /// </summary>
    public void KeepApart(List<Wolf> hunters, float dt) {
        var at = world.DoorTug.Tugged ? world.DoorTug.TuggedTo : world.WolfSnap.BitePosition();
        for (int i = 0; i < hunters.Count; i++) for (int j = i + 1; j < hunters.Count; j++) {
            var a = hunters[i]; var b = hunters[j];
            bool holdsA = Holds(a), holdsB = Holds(b);
            // Bodies this far apart cannot touch.
            if (holdsA && holdsB || a.Pos.DistanceSquaredTo(b.Pos) > (2 * (WolfNose + WolfHalfWidth)) * (2 * (WolfNose + WolfHalfWidth))) continue;
            var (aTail, aNose) = Near(a, at); var (bTail, bNose) = Near(b, at);
            var (onA, onB) = Closest(aTail, aNose, bTail, bNose);
            float overlap = 2 * WolfHalfWidth - onA.DistanceTo(onB);
            if (overlap <= 0) continue;
            var apart = onA.DistanceSquaredTo(onB) > 1e-6f ? (onA - onB).Normalized() : (a.Pos - b.Pos).LengthSquared() > 1e-6f ? (a.Pos - b.Pos).Normalized() : Vector2.Right;
            var push = apart * Math.Min(overlap / 2, dt * SideStepSpeed);
            if (!holdsA) a.Pos = world.Slide(a.Pos, holdsB ? push * 2 : push);
            if (!holdsB) b.Pos = world.Slide(b.Pos, holdsA ? -push * 2 : -push);
        }
    }

    /// <summary>
    /// Where the body of a wolf at a player runs: toward them for one closing in, and along its own way for one that
    /// follows them round, so two going opposite ways pass side by side.
    /// </summary>
    private static (Vector2 Tail, Vector2 Nose) Near(Wolf wolf, Vector2 player) => wolf.Following || Holds(wolf) ? Body(wolf) : Body(wolf, player);

    /// <summary>Whether a wolf holds its place at the player: tugging them, pinning them or in the air.</summary>
    private static bool Holds(Wolf wolf) => wolf.Tugging || wolf.Side != 0 || wolf.Pinning || wolf.Stagger > 0 || WolfSnap.Active(wolf) || wolf.Recover > 0;

    /// <summary>The nearest points to each other on two line segments, from <paramref name="a0"/> to <paramref name="a1"/> and <paramref name="b0"/> to <paramref name="b1"/>.</summary>
    private static (Vector2 OnA, Vector2 OnB) Closest(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1) {
        Vector2 da = a1 - a0, db = b1 - b0, r = a0 - b0;
        float aa = da.Dot(da), bb = db.Dot(db), ab = da.Dot(db), ar = da.Dot(r), br = db.Dot(r), denominator = aa * bb - ab * ab;
        float s = denominator > 1e-8f ? Math.Clamp((ab * br - ar * bb) / denominator, 0, 1) : 0;
        float t = Math.Clamp((ab * s + br) / bb, 0, 1);
        s = Math.Clamp((ab * t - ar) / aa, 0, 1);
        return (a0 + da * s, b0 + db * t);
    }
}
