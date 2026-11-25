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
        var mark = BitePosition();
        float gap = wolf.Pos.DistanceTo(mark);
        // Up against the player it first backs up far enough to leap.
        if (gap >= Trigger || gap < JawsAhead + ShortestHop - Reach * .6f) return;
        Commit(wolf, mark, throat: false);
    }

    internal bool Commit(Wolf wolf, Vector2 mark, bool throat) {
        var toward = mark - wolf.Pos;
        float heading = toward.LengthSquared() > 1e-6f ? toward.Angle() : wolf.Heading;
        float jawsAhead = throat ? ThroatJawsAhead : JawsAhead;
        float longHop = throat ? World.LeapRange - jawsAhead : LongestHop;
        float distance = Math.Clamp(toward.Length() - jawsAhead - NearSide, ShortestHop, longHop);
        var landing = wolf.Pos + Vector2.FromAngle(heading) * distance;
        if (!world.WolfBodies.HasLandingRoom(wolf, landing, heading)) return false;
        wolf.Heading = heading;
        wolf.SnapTo = landing;
        if (throat) wolf.Leap = Windup + World.LeapSeconds;
        else wolf.Snap = Windup + Flight;
        return true;
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
    internal Vector2 ClearTo(Vector2 from, Vector2 to, Thing? cabin = null) {
        int steps = Math.Max(1, (int)MathF.Ceiling(from.DistanceTo(to) / GroundStep));
        var last = from;
        for (int i = 1; i <= steps; i++) {
            var at = i == steps ? to : from.Lerp(to, (float)i / steps);
            var except = cabin != null && DoorwayPath.OnSteps(cabin, at) ? cabin : null;
            if (!world.CanStand(at, except)) break;
            last = at;
        }
        return last;
    }

    /// <summary>Where the acting player stands on screen, including while stepping onto a cabin's threshold.</summary>
    internal Vector2 BitePosition() {
        var p = world.S.Actor;
        return DoorwayPath.UsesBody(p.Door) ? p.DoorBody : p.Pos;
    }

    private void Land(Wolf wolf, bool throat) {
        wolf.Attack = Rest;
        world.PlaceBiteTarget?.Invoke(world.S.Actor);
        var mark = BitePosition();
        var jaws = wolf.Pos + Vector2.FromAngle(wolf.Heading) * (throat ? ThroatJawsAhead : JawsAhead);
        var steps = DoorwayPath.UsesBody(world.S.Actor.Door) ? world.Find(world.S.Actor.DoorCabin) : null;
        bool hit = !world.Downed && !world.DoorTug.Tugged && jaws.DistanceSquaredTo(mark) <= Reach * Reach &&
            ClearTo(wolf.Pos, mark, steps) == mark;
        wolf.Recover = hit ? HitRecovery : MissRecovery;
        if (!hit) {
            if (wolf.Aggressive) world.WolfHunt.CircleOnMiss(wolf);
            return;
        }
        bool pins = throat && !world.FireDeters(wolf, world.Player);
        if (!pins) world.S.Health -= world.Data.WolfBite;
        if (world.DoorTug.TugOut(wolf) || !pins) world.Play("wolf_bite", mark);
        else world.WolfHunt.Pin(wolf);
    }
}
