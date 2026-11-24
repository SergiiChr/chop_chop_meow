using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Cabin doors in <paramref name="world"/>: players step in through the door one at a time until the cabin is full, anyone
/// at the door of a full or locked cabin knocks on it, and players standing aside let someone step out.
/// What a wolf does to a player in the doorway is <see cref="DoorTug"/>'s, and what it does at the door of a cabin with
/// someone inside is <see cref="DoorCheck"/>'s.
/// </summary>
public sealed class Doorways(World world) {
    /// <summary>How many players a cabin holds.</summary>
    public const int Places = 2;
    /// <summary>Seconds stepping in through a cabin door at a walk and at a sprint, stepping out, and knocking on a shut door.</summary>
    public const float EnterSeconds = 1.3f, RushSeconds = .7f, LeaveSeconds = 1.5f, KnockSeconds = 1;
    /// <summary>
    /// Seconds a cabin's door takes to swing shut, and before the first knock on it: a player left outside waits for the
    /// door to shut, then steps up to it.
    /// </summary>
    public const float ShutSeconds = .4f, KnockLead = .5f;
    /// <summary>The way out of a cabin's door, which is in its front wall.</summary>
    public static readonly Vector2 OutOfDoor = Vector2.Down;
    /// <summary>Knocks on a shut door, spread over <see cref="KnockSeconds"/>.</summary>
    public const int Knocks = 3;
    /// <summary>How far, in metres, from <see cref="DoorPoint"/> a player can start stepping in.</summary>
    public const float DoorReach = .5f;
    /// <summary>Where a player stands at a cabin's door, from the cabin's middle: in front of the doorway in the model's front wall, past its step and clear of the door as it swings open.</summary>
    internal static readonly Vector2 DoorOffset = new(-.34f, 2f);

    /// <summary>The spot just outside a cabin's door where a player steps in and out.</summary>
    public static Vector2 DoorPoint(Thing cabin) => cabin.ToWorld(DoorOffset);

    /// <summary>The way out through the door of that cabin, or the usual way when it is gone.</summary>
    public Vector2 OutOfDoorAt(int cabin) => OutOfDoor.Rotated(world.Find(cabin)?.Rotation ?? 0);

    /// <summary>
    /// Where a player stepping out of or waking in a cabin stands: at its door, or, with that blocked, as in a cabin from an
    /// older save built before its door had to be clear, on the first open ground round the cabin.
    /// </summary>
    public Vector2 Doorstep(Thing cabin) {
        if (world.CanStand(DoorPoint(cabin))) return DoorPoint(cabin);
        for (int i = 0; i < 8; i++) {
            var p = cabin.ToWorld(Vector2.Down.Rotated(i * MathF.PI / 4) * (World.Footprint(ThingKind.Hut) + .6f));
            if (world.CanStand(p)) return p;
        }
        return DoorPoint(cabin);
    }

    /// <summary>Seconds a player's doorway stage lasts.</summary>
    public static float DoorSeconds(PlayerState p) => p.Door switch {
        Doorway.Entering => p.DoorRush ? RushSeconds : EnterSeconds,
        Doorway.Leaving => LeaveSeconds,
        Doorway.Knocking => KnockLead + KnockSeconds,
        Doorway.Tugged or Doorway.TuggedOff => DoorTug.Seconds(p),
        _ => 0
    };

    /// <summary>Whether wolves can neither smell nor reach a player: inside a cabin, through the second half of stepping in and the first half of stepping out.</summary>
    public static bool Indoors(PlayerState p) =>
        p.Sheltered != 0 || p.Door == Doorway.Entering && p.DoorTime >= DoorSeconds(p) / 2 || p.Door == Doorway.Leaving && !Through(p);

    /// <summary>How many players are inside a cabin.</summary>
    public int Inside(int cabin) {
        int inside = 0;
        foreach (var p in world.S.Players) if (p.Sheltered == cabin) inside++;
        return inside;
    }

    /// <summary>Whether a cabin has a place left for <paramref name="one"/> more player: fewer than <see cref="Places"/> others are inside it or at its door on their way in.</summary>
    public bool HasPlace(int cabin, PlayerState one) {
        int taken = 0;
        foreach (var p in world.S.Players)
            if (p != one && (p.Sheltered == cabin || p.DoorCabin == cabin && p.Door is Doorway.Entering or Doorway.Following or Doorway.Tugged or Doorway.Aside)) taken++;
        return taken < Places;
    }

    /// <summary>Unlocks the door of a cabin nobody is left inside.</summary>
    public void UnlockEmpty(int cabin) {
        if (world.Find(cabin) is Thing hut && Inside(cabin) == 0) hut.Locked = false;
    }

    /// <summary>
    /// Turns the key in the door of the cabin the acting player is in, which locks it or unlocks it.
    /// The key does nothing once a wolf has its snout in the door.
    /// </summary>
    public void TurnKey() {
        var s = world.S;
        if (world.Find(s.Sheltered) is not Thing cabin || s.Actor.Door != Doorway.None) return;
        if (world.DoorCheck.Forcing(cabin.Id)) return;
        if (world.Forwarded(Verb.TurnKey)) return;
        cabin.Locked = !cabin.Locked; world.Play("door_lock", DoorPoint(cabin));
    }

    /// <summary>
    /// Starts stepping into a cabin from its door: in when it has a place and its door is not locked, up behind someone
    /// already stepping in, or knocking on the door. A player who sprinted there, <paramref name="rush"/>, steps in at a run and slams the door.
    /// </summary>
    public bool Enter(Thing hut, bool rush) {
        var s = world.S;
        if (hut.Kind != ThingKind.Hut || world.Occupied || s.Sheltered != 0 || world.Player.DistanceTo(DoorPoint(hut)) > DoorReach) return false;
        if (!world.Forwarded(Verb.Enter, hut.Id, count: rush ? 1 : 0)) { s.Actor.DoorQueued = ++s.DoorArrivals; StepIn(hut.Id, rush); }
        return true;
    }

    /// <summary>
    /// Starts stepping out of the cabin the acting player is in, onto its doorstep, unlocking its door first; nobody steps
    /// out while a wolf is at the door.
    /// </summary>
    public void Leave() {
        var s = world.S;
        if (world.Find(s.Sheltered) is not Thing cabin || s.Actor.Door != Doorway.None) return;
        if (world.DoorCheck.AtDoor(cabin.Id) != null) return;
        if (world.Forwarded(Verb.Leave)) return;
        if (cabin.Locked) { cabin.Locked = false; world.Play("door_lock", DoorPoint(cabin)); }
        s.Sheltered = 0; world.Player = Doorstep(cabin); s.Actor.Warp++;
        Stage(s.Actor, Doorway.Leaving, cabin.Id);
        s.Actor.DoorBody = DoorPoint(cabin) + DoorwayPath.IntoCabin.Rotated(cabin.Rotation);
        // Whoever knocks on the door steps aside to let them out, keeping the order they came in,
        // and the one stepping out keeps to the other side of the door, to pass them as they squeeze in.
        foreach (var other in s.Players.Where(o => o != s.Actor && Waiting(o, cabin)).OrderBy(o => o.DoorQueued).ToList()) {
            Stage(other, Doorway.Aside, cabin.Id); other.DoorQueued = ++s.DoorArrivals;
            other.Pos = AsideSpot(other, cabin); other.Warp++;
        }
        var aside = AsideWay.Rotated(cabin.Rotation);
        if (s.Players.Any(o => o.Door == Doorway.Aside && o.DoorCabin == cabin.Id)) world.Player = DoorPoint(cabin) - aside * MakeWay;
        // Someone just standing at the door stays where they are, and the one stepping out ends beside them instead.
        foreach (var other in s.Players)
            if (other != s.Actor && other.Sheltered == 0 && other.Door == Doorway.None && other.Pos.DistanceTo(world.Player) < ClearOf) {
                float side = (other.Pos - DoorPoint(cabin)).Dot(aside) > 0 ? -1 : 1;
                world.Player = other.Pos + aside * (side * ClearOf);
            }
    }
    /// <summary>How far apart, in metres, someone stepping out of a cabin keeps from a player standing at its door.</summary>
    public const float ClearOf = .6f;
    /// <summary>How far to the other side of a cabin's door point someone stepping out past a player standing aside keeps.</summary>
    public const float MakeWay = .1f;

    /// <summary>Whether a player stepping out of a cabin is through its doorway, out in the open on the step.</summary>
    public static bool Through(PlayerState p) => p.DoorTime >= ThroughSeconds;
    /// <summary>Seconds into stepping out at which a player is through the doorway.</summary>
    public const float ThroughSeconds = LeaveSeconds / 2;

    /// <summary>Whether someone in the doorway on their way out of the cabin a player stands aside at, or standing aside there before them, still keeps them from stepping in.</summary>
    private bool Blocked(PlayerState aside) {
        foreach (var o in world.S.Players)
            if (o.DoorCabin == aside.DoorCabin && (o.Door == Doorway.Leaving && !Through(o) || o.Door == Doorway.Aside && o.DoorQueued < aside.DoorQueued)) return true;
        return false;
    }

    /// <summary>Whether a player waits at a cabin's door: knocking on it.</summary>
    private static bool Waiting(PlayerState p, Thing cabin) => p.Door == Doorway.Knocking && p.DoorCabin == cabin.Id;

    /// <summary>
    /// Where a player standing aside at a cabin's door stands: a step to the right of the door point, away from the door as it
    /// swings open, a further step out for each player who stood aside before them.
    /// </summary>
    private Vector2 AsideSpot(PlayerState p, Thing cabin) {
        int before = world.S.Players.Count(o => o != p && o.Door == Doorway.Aside && o.DoorCabin == cabin.Id && o.DoorQueued < p.DoorQueued);
        return DoorPoint(cabin) + AsideWay.Rotated(cabin.Rotation) * (AsideStep + AsideGap * before);
    }
    /// <summary>The way players at a cabin's door step aside: to their right as they face the door.</summary>
    public static readonly Vector2 AsideWay = OutOfDoor.Orthogonal();
    /// <summary>How far to the side of a cabin's door point the first player standing aside stands, and how far apart the next ones stand.</summary>
    public const float AsideStep = .7f, AsideGap = .6f;

    /// <summary>The nearest cabin with a place left for the acting player, locked or not, or null.</summary>
    public Thing? NearestFreeCabin(Vector2 from) {
        Thing? best = null;
        foreach (var t in world.S.Things.Of(ThingKind.Hut))
            if (HasPlace(t.Id, world.S.Actor) && (best == null || t.Pos.DistanceSquaredTo(from) < best.Pos.DistanceSquaredTo(from))) best = t;
        return best;
    }

    /// <summary>
    /// What a player arriving at a cabin's door does, given who is there before them: follows someone stepping in, knocks on
    /// the door of a cabin that is full, locked or being stepped out of, and steps in otherwise.
    /// </summary>
    public Doorway Ahead(int cabin, PlayerState arriving) {
        // A player not at any door, such as one waking or rejoining, comes after everyone already there.
        int queued = arriving.Door == Doorway.None ? int.MaxValue : arriving.DoorQueued;
        int inside = 0;
        // A wolf with its snout in the door is in the way of anyone stepping in.
        bool leaving = false, ahead = world.DoorCheck.Forcing(cabin);
        foreach (var p in world.S.Players) {
            if (p == arriving) continue;
            if (p.Sheltered == cabin) inside++;
            if (p.DoorCabin != cabin) continue;
            // Someone still in the doorway on their way out keeps the door; once they are through it, the next one squeezes past them in.
            if (p.Door == Doorway.Leaving && !Through(p)) leaving = true;
            // Someone tugged back out, or standing aside for someone stepping out, stays ahead only if they came to the door first.
            if (p.Door == Doorway.Entering || p.Door is Doorway.Tugged or Doorway.Aside && p.DoorQueued < queued) ahead = true;
        }
        if (leaving || inside >= Places) return Doorway.Knocking;
        if (ahead) return Doorway.Following;
        return inside > 0 && world.Find(cabin) is { Locked: true } ? Doorway.Knocking : Doorway.Entering;
    }

    /// <summary>Starts the acting player stepping into a cabin, or following or knocking as whoever is there before them leaves them.</summary>
    public void StepIn(int cabin, bool rush) => Stage(world.S.Actor, Ahead(cabin, world.S.Actor), cabin, rush);

    /// <summary>Starts a player on a doorway stage at a cabin.</summary>
    public static void Stage(PlayerState p, Doorway door, int cabin, bool rush = false) {
        p.DoorFrom = p.DoorBody = DoorwayPath.UsesBody(p.Door) ? p.DoorBody : p.Pos;
        p.Door = door; p.DoorCabin = cabin; p.DoorTime = 0; p.DoorRush = rush;
        p.Swing = null;
    }

    /// <summary>
    /// Plays <paramref name="sound"/> at each of <paramref name="count"/> beats <paramref name="every"/> seconds apart from
    /// <paramref name="first"/> seconds into a doorway stage that fall after <paramref name="from"/> seconds of it and by <paramref name="to"/>.
    /// </summary>
    private void Beats(string sound, float from, float to, float first, float every, int count) {
        for (int i = 0; i < count; i++)
            if (from < first + i * every && first + i * every <= to) world.Play(sound, world.Player);
    }

    /// <summary>Moves the acting player's doorway stage on: inside once stepped in, knocking once the door shuts in front, and free once done.</summary>
    public void Tick(float dt) {
        var p = world.S.Actor;
        if (p.Door == Doorway.None) return;
        if (world.Downed || world.Find(p.DoorCabin) is not { Kind: ThingKind.Hut } cabin) { p.Door = Doorway.None; return; }
        float before = p.DoorTime;
        p.DoorTime += dt;
        if (DoorwayPath.UsesBody(p.Door))
            p.DoorBody = p.Door == Doorway.Following ? DoorwayPath.Follow(p, world.Players, cabin, dt) :
                DoorwayPath.At(p.Door, DoorPoint(cabin), p.Pos, p.DoorFrom, p.DoorTime, DoorSeconds(p), p.DoorRush, cabin.Rotation);
        bool done = p.DoorTime >= DoorSeconds(p);
        switch (p.Door) {
            case Doorway.Entering when done:
                // A door left locked with nobody behind it, as in a save loaded without whoever locked it, is unlocked by the first one in.
                UnlockEmpty(p.DoorCabin);
                p.Sheltered = p.DoorCabin; p.Door = Doorway.None;
                p.CabinEntry = ++world.S.DoorArrivals;
                break;
            case Doorway.Following:
                // The door shuts in front of a follower once the player ahead is inside; with nobody ahead any more, it is their turn.
                if (Ahead(p.DoorCabin, p) != Doorway.Following) StepIn(p.DoorCabin, p.DoorRush);
                break;
            case Doorway.Knocking:
                Beats("door_knock", before, p.DoorTime, KnockLead, KnockSeconds / Knocks, Knocks);
                if (done) p.Door = Doorway.None;
                break;
            case Doorway.Tugged or Doorway.TuggedOff when done:
                world.DoorTug.End();
                break;
            case Doorway.Leaving when done:
                p.Door = Doorway.None;
                break;
            case Doorway.Aside:
                // Once the one stepping out is through the doorway, whoever stood aside first squeezes past them in.
                if (!Blocked(p)) StepIn(p.DoorCabin, false);
                break;
        }
    }
}
