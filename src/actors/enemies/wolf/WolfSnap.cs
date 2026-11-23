using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The wolf plants its paws, leaps on a fixed line and bites where its jaws close as it lands.</summary>
public sealed class WolfSnap(World world) {
    /// <summary>How near a wolf has to be to start a snap, in metres.</summary>
    public const float Trigger = 1.45f;
    /// <summary>Seconds spent gathering itself and seconds in the air for an ordinary snap.</summary>
    public const float Windup = .3f, Flight = .24f;
    /// <summary>Seconds spent planted after a bite lands or misses.</summary>
    public const float HitRecovery = .3f, MissRecovery = .55f;
    /// <summary>Seconds after a snap before the wolf can start another.</summary>
    public const float Rest = .6f;
    /// <summary>How far ahead of its feet a wolf's jaws close, in metres.</summary>
    public const float JawsAhead = .85f, ThroatJawsAhead = .47f;
    /// <summary>The reach of the closing jaws and the player's body at bite height, in metres.</summary>
    public const float Reach = .22f;
    private const float NearSide = .12f, ShortestHop = .08f, LongestHop = .55f, GroundStep = .1f;

    public static bool Active(Wolf wolf) => wolf.Snap > 0 || wolf.Leap > 0;
    public static float Remaining(Wolf wolf) => wolf.Leap > 0 ? wolf.Leap : wolf.Snap;
    public static float FlightOf(Wolf wolf) => wolf.Leap > 0 ? World.LeapSeconds : Flight;
    public static bool Winding(Wolf wolf) => Remaining(wolf) > FlightOf(wolf);
    public static bool InAir(Wolf wolf) => Active(wolf) && !Winding(wolf);

    /// <summary>
    /// A wolf within reach starts a snap. While another wolf tugs its player it takes an arm instead,
    /// or waits beside the player with both arms taken.
    /// </summary>
    internal void Start(Wolf wolf) {
        if (world.DoorTug.TakeSide(wolf) || world.DoorTug.Tugged || world.Downed || wolf.Attack > 0) return;
        float gap = wolf.Pos.DistanceTo(world.Player);
        // Up against the player it first backs up far enough to leap.
        if (gap >= Trigger || gap < JawsAhead + ShortestHop - Reach * .6f) return;
        Commit(wolf, false);
    }

    internal void Commit(Wolf wolf, bool throat) {
        var toward = world.Player - wolf.Pos;
        if (toward.LengthSquared() > 1e-6f) wolf.Heading = toward.Angle();
        float jawsAhead = throat ? ThroatJawsAhead : JawsAhead;
        float longHop = throat ? World.LeapRange - jawsAhead : LongestHop;
        float distance = Math.Clamp(toward.Length() - jawsAhead - NearSide, ShortestHop, longHop);
        wolf.SnapTo = wolf.Pos + Vector2.FromAngle(wolf.Heading) * distance;
        if (throat) wolf.Leap = Windup + World.LeapSeconds;
        else wolf.Snap = Windup + Flight;
    }

    /// <summary>Spends the last part of the countdown in flight. Ground or buildings can cut the leap short.</summary>
    internal void Tick(Wolf wolf, float dt) {
        bool throat = wolf.Leap > 0;
        float flight = FlightOf(wolf), left = Remaining(wolf), next = Math.Max(0, left - dt);
        float air = Math.Min(left, flight), flown = air - Math.Min(next, flight);
        if (flown > 0) {
            var stepTo = wolf.Pos.Lerp(wolf.SnapTo, flown / air);
            wolf.Pos = ClearTo(wolf.Pos, stepTo);
            if (wolf.Pos != stepTo) wolf.SnapTo = wolf.Pos;
        }
        if (throat) wolf.Leap = next;
        else wolf.Snap = next;
        if (next == 0) Land(wolf, throat);
    }

    /// <summary>Checks the line in short steps, as a leap stays on its bearing even when it meets a wall.</summary>
    private Vector2 ClearTo(Vector2 from, Vector2 to) {
        int steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) / GroundStep));
        var last = from;
        for (int i = 1; i <= steps; i++) {
            var at = i == steps ? to : from.Lerp(to, (float)i / steps);
            if (!world.CanStand(at)) break;
            last = at;
        }
        return last;
    }

    private void Land(Wolf wolf, bool throat) {
        if (world.DoorTug.TakeSide(wolf)) return;
        wolf.Attack = Rest;
        var jaws = wolf.Pos + Vector2.FromAngle(wolf.Heading) * (throat ? ThroatJawsAhead : JawsAhead);
        bool hit = !world.Downed && !world.DoorTug.Tugged && jaws.DistanceSquaredTo(world.Player) <= Reach * Reach &&
            ClearTo(wolf.Pos, world.Player) == world.Player;
        wolf.Recover = hit ? HitRecovery : MissRecovery;
        if (!hit) return;
        bool pins = throat && !world.Protected(world.Player);
        if (!pins) world.S.Health -= world.Data.WolfBite;
        if (world.DoorTug.TugOut(wolf) || !pins) world.Play("wolf_bite", world.Player);
        else world.WolfHunt.Pin(wolf);
    }
}
