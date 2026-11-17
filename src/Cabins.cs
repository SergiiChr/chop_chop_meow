using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>What a player is doing at a cabin's door.</summary>
public enum Doorway {
    None,
    /// <summary>Stepping in through the door, out in the open for the first half.</summary>
    Entering,
    /// <summary>Stepping up behind someone entering the same cabin, until the door shuts in front of them.</summary>
    Following,
    /// <summary>Knocking on the shut door of a cabin someone is in.</summary>
    Knocking,
    /// <summary>Tugged back out of the doorway by a wolf holding them by the waist, to step in again at a run.</summary>
    Tugged,
    /// <summary>Tugged off the door they knocked on, or stood aside at, by a wolf, to stand free.</summary>
    TuggedOff,
    /// <summary>Stepping out through the door, out in the open for the second half.</summary>
    Leaving,
    /// <summary>Standing aside from the door for someone stepping out, to step in once they are out.</summary>
    Aside,
}

/// <summary>
/// Cabins: one player at a time steps in through the door, the next one at the door knocks on it, and a wolf can tug
/// whoever is out in the open in the doorway back out.
/// </summary>
public sealed partial class World {
    /// <summary>Seconds stepping in through a cabin door at a walk and at a sprint, stepping out, knocking on a shut door, and being tugged back out.</summary>
    public const float EnterSeconds = 1.3f, RushSeconds = .7f, LeaveSeconds = 1.5f, KnockSeconds = 1, TugSeconds = 1.5f;
    /// <summary>
    /// Seconds a cabin's door takes to swing shut, and before the first knock on it: a player left outside waits for the
    /// door to shut, then steps up to it.
    /// </summary>
    public const float ShutSeconds = .4f, KnockLead = .5f;
    /// <summary>How far, in metres, a wolf tugs a player back out from a cabin's door, in how many tugs.</summary>
    public const float TugDistance = 1;
    public const int Tugs = 3;
    /// <summary>The share of each tug spent tugging; the wolf holds for the rest of it.</summary>
    public const float Heave = .4f;
    /// <summary>The way out of a cabin's door, which is in its front wall.</summary>
    public static readonly Vector2 OutOfDoor = Vector2.Down;
    /// <summary>How far behind a tugged player a tugging wolf stands, with its jaws at their waist.</summary>
    public const float TugReach = .75f;
    /// <summary>
    /// How far further out a player tugged out of a doorway has their throat once they fall onto their back, and how far
    /// to the side of it the wolf that pins them stands.
    /// </summary>
    public const float FallenThroat = .66f, AsideReach = .7f;
    /// <summary>Knocks on a shut door, spread over <see cref="KnockSeconds"/>.</summary>
    public const int Knocks = 3;
    /// <summary>How far, in metres, from <see cref="DoorPoint"/> a player can start stepping in.</summary>
    public const float DoorReach = .5f;
    /// <summary>Where a player stands at a cabin's door, from the cabin's middle: in front of the doorway in the model's front wall, past its step and clear of the door as it swings open.</summary>
    private static readonly Vector2 DoorOffset = new(-.34f, 2f);

    /// <summary>The spot just outside a cabin's door where a player steps in and out.</summary>
    public static Vector2 DoorPoint(Thing cabin) => cabin.Pos + DoorOffset;

    /// <summary>
    /// Where a player stepping out of or waking in a cabin stands: at its door, or, with that blocked, as in a cabin from an
    /// older save built before its door had to be clear, on the first open ground round the cabin.
    /// </summary>
    private Vector2 Doorstep(Thing cabin) {
        if (CanStand(DoorPoint(cabin))) return DoorPoint(cabin);
        for (int i = 0; i < 8; i++) {
            var p = cabin.Pos + Vector2.Down.Rotated(i * MathF.PI / 4) * (Footprint(ThingKind.Hut) + .6f);
            if (CanStand(p)) return p;
        }
        return DoorPoint(cabin);
    }
    /// <summary>What the acting player is doing at a cabin's door.</summary>
    public Doorway Door => S.Actor.Door;

    /// <summary>Seconds a player's doorway stage lasts.</summary>
    public static float DoorSeconds(PlayerState p) => p.Door switch {
        Doorway.Entering => p.DoorRush ? RushSeconds : EnterSeconds,
        Doorway.Leaving => LeaveSeconds,
        Doorway.Knocking => KnockLead + KnockSeconds,
        // A wounded player is taken down as the last tug lands, with no hold after it.
        Doorway.Tugged or Doorway.TuggedOff => p.Health <= TakedownHealth ? TugSeconds * (Tugs - 1 + Heave) / Tugs : TugSeconds,
        _ => 0
    };

    /// <summary>Whether wolves can neither smell nor reach a player: inside a cabin, through the second half of stepping in and the first half of stepping out.</summary>
    public static bool Indoors(PlayerState p) =>
        p.Sheltered != 0 || p.Door == Doorway.Entering && p.DoorTime >= DoorSeconds(p) / 2 || p.Door == Doorway.Leaving && !Through(p);

    /// <summary>
    /// Starts stepping into a cabin from its door: in when it is free, up behind someone already stepping in, or knocking on
    /// the door when someone is inside. A player who sprinted there, <paramref name="rush"/>, steps in at a run and slams the door.
    /// </summary>
    public bool Enter(Thing hut, bool rush) {
        if (hut.Kind != ThingKind.Hut || Occupied || Player.DistanceTo(DoorPoint(hut)) > DoorReach) return false;
        if (!Forwarded(Verb.Enter, hut.Id, count: rush ? 1 : 0)) { S.Actor.DoorQueued = ++S.DoorArrivals; StepIn(hut.Id, rush); }
        return true;
    }

    /// <summary>Starts stepping out of the cabin the acting player is in, onto its doorstep.</summary>
    public void Leave() {
        if (Find(S.Sheltered) is not Thing cabin || S.Actor.Door != Doorway.None || Forwarded(Verb.Leave)) return;
        S.Sheltered = 0; Player = Doorstep(cabin); S.Actor.Warp++;
        Stage(S.Actor, Doorway.Leaving, cabin.Id);
        // Whoever knocks on the door steps aside to let them out, keeping the order they came in,
        // and the one stepping out keeps to the other side of the door, to pass them as they squeeze in.
        foreach (var other in S.Players.Where(o => o != S.Actor && Waiting(o, cabin)).OrderBy(o => o.DoorQueued).ToList()) {
            Stage(other, Doorway.Aside, cabin.Id); other.DoorQueued = ++S.DoorArrivals;
            other.Pos = AsideSpot(other, cabin); other.Warp++;
        }
        if (S.Players.Any(o => o.Door == Doorway.Aside && o.DoorCabin == cabin.Id)) Player = DoorPoint(cabin) - AsideWay * MakeWay;
        // Someone just standing at the door stays where they are, and the one stepping out ends beside them instead.
        foreach (var other in S.Players)
            if (other != S.Actor && other.Sheltered == 0 && other.Door == Doorway.None && other.Pos.DistanceTo(Player) < ClearOf) {
                float side = (other.Pos - DoorPoint(cabin)).Dot(AsideWay) > 0 ? -1 : 1;
                Player = other.Pos + AsideWay * (side * ClearOf);
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
        foreach (var o in S.Players)
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
        int before = S.Players.Count(o => o != p && o.Door == Doorway.Aside && o.DoorCabin == cabin.Id && o.DoorQueued < p.DoorQueued);
        return DoorPoint(cabin) + AsideWay * (AsideStep + AsideGap * before);
    }
    /// <summary>The way players at a cabin's door step aside: to their right as they face the door.</summary>
    public static readonly Vector2 AsideWay = OutOfDoor.Orthogonal();
    /// <summary>How far to the side of a cabin's door point the first player standing aside stands, and how far apart the next ones stand.</summary>
    public const float AsideStep = .7f, AsideGap = .6f;

    /// <summary>A step of <paramref name="move"/> for the acting player walking to a cabin's door, cut short to stop on the door rather than past it.</summary>
    public Vector2 StepToDoor(Thing cabin, Vector2 move) => move.LimitLength(Player.DistanceTo(DoorPoint(cabin)));

    /// <summary>
    /// The way to walk from <paramref name="from"/> to a cabin's door: straight there when the cabin is not in the way, else
    /// to the edge of the cabin's walls and round them on the shorter side; zero once at the door.
    /// </summary>
    public static Vector2 TowardDoor(Thing cabin, Vector2 from) {
        var door = DoorPoint(cabin);
        if (from.DistanceTo(door) < .15f) return Vector2.Zero;
        // The circle a walk round the cabin keeps to, a little outside what blocks a walker.
        float round = Footprint(ThingKind.Hut) + .3f;
        var centre = cabin.Pos;
        var line = door - from;
        float along = Math.Clamp((centre - from).Dot(line) / line.LengthSquared(), 0, 1);
        if ((from + line * along).DistanceTo(centre) >= round) return line.Normalized();
        var outward = from - centre;
        float distance = outward.Length(), side = Mathf.AngleDifference(outward.Angle(), (door - centre).Angle()) >= 0 ? 1 : -1;
        // Out past the circle, head for where a line from here touches it on the door's side; on it, walk along it, held at its radius.
        if (distance > round + .1f) return (centre + outward.Rotated(side * MathF.Acos(round / distance)) / distance * round - from).Normalized();
        return (outward.Rotated(side * MathF.PI / 2) / distance + outward / distance * (round - distance)).Normalized();
    }

    /// <summary>The nearest cabin nobody is in or at the door of, or null.</summary>
    private Thing? NearestFreeCabin(Vector2 from) {
        Thing? best = null;
        foreach (var t in S.Things.Of(ThingKind.Hut))
            if (Ahead(t.Id, S.Actor) == Doorway.Entering && (best == null || t.Pos.DistanceSquaredTo(from) < best.Pos.DistanceSquaredTo(from))) best = t;
        return best;
    }

    /// <summary>
    /// What a player arriving at a cabin's door does, given who is there before them: steps in when nobody is, knocks on the
    /// door when someone is inside or stepping out, and follows someone stepping in.
    /// </summary>
    private Doorway Ahead(int cabin, PlayerState arriving) {
        var door = Doorway.Entering;
        // A player not at any door, such as one waking or rejoining, comes after everyone already there.
        int queued = arriving.Door == Doorway.None ? int.MaxValue : arriving.DoorQueued;
        foreach (var p in S.Players) {
            if (p == arriving) continue;
            // Someone still in the doorway on their way out keeps the door; once they are through it, the next one squeezes past them in.
            if (p.Sheltered == cabin || p.DoorCabin == cabin && p.Door == Doorway.Leaving && !Through(p)) return Doorway.Knocking;
            // Someone tugged back out, or standing aside for someone stepping out, stays ahead only if they came to the door first.
            if (p.DoorCabin == cabin && (p.Door == Doorway.Entering || p.Door is Doorway.Tugged or Doorway.Aside && p.DoorQueued < queued)) door = Doorway.Following;
        }
        return door;
    }

    private void StepIn(int cabin, bool rush) {
        Stage(S.Actor, Ahead(cabin, S.Actor), cabin, rush);
    }

    /// <summary>Starts a player on a doorway stage at a cabin.</summary>
    private static void Stage(PlayerState p, Doorway door, int cabin, bool rush = false) {
        p.Door = door; p.DoorCabin = cabin; p.DoorTime = 0; p.DoorRush = rush;
    }

    /// <summary>
    /// Plays <paramref name="sound"/> at each of <paramref name="count"/> beats <paramref name="every"/> seconds apart from
    /// <paramref name="first"/> seconds into a doorway stage that fall after <paramref name="from"/> seconds of it and by <paramref name="to"/>.
    /// </summary>
    private void Beats(string sound, float from, float to, float first, float every, int count) {
        for (int i = 0; i < count; i++)
            if (from < first + i * every && first + i * every <= to) Play(sound, Player);
    }

    /// <summary>
    /// How much of the way a wolf has tugged a player, 0 to 1, <paramref name="done"/> of the way through the tug: each of
    /// its tugs jerks them back sharply, then holds while the wolf gets a new grip.
    /// </summary>
    public static float TugProgress(float done) {
        var (tug, u) = TugPhase(done);
        return (tug + Mathf.SmoothStep(0, 1, Math.Min(1, u / Heave))) / Tugs;
    }

    /// <summary>Which tug a tug <paramref name="done"/> of the way through is in, from 0, and how far through that tug, 0 to 1.</summary>
    public static (int Tug, float U) TugPhase(float done) {
        float tugs = Math.Clamp(done, 0, 1) * Tugs, tug = MathF.Min(MathF.Floor(tugs), Tugs - 1);
        return ((int)tug, tugs - tug);
    }

    /// <summary>How hard a tug is tugging <paramref name="u"/> of the way through it: rising and falling over its heave, then nothing while the wolf holds.</summary>
    public static float HeavePulse(float u) => u < Heave ? MathF.Sin(MathF.PI * u / Heave) : 0;

    /// <summary>Where the acting player is: their place, or as far back from it as a wolf tugging them out of a doorway has taken them.</summary>
    private Vector2 TuggedTo => S.Actor.Door is Doorway.Tugged or Doorway.TuggedOff ? Player + OutOfDoor * (TugDistanceFrom(Player) * TugProgress(S.Actor.DoorTime / TugSeconds)) : Player;

    /// <summary>
    /// How far a wolf tugs a player standing at <paramref name="from"/> straight out of a doorway: <see cref="TugDistance"/>, cut
    /// short where open ground ends, leaving room on it for the wolf behind them where there is any.
    /// </summary>
    public float TugDistanceFrom(Vector2 from) {
        float both = OpenOut(from, TugDistance + TugReach) - TugReach;
        return both >= 0 ? both : OpenOut(from, TugDistance);
    }

    /// <summary>How far open ground runs from <paramref name="from"/> straight out of a doorway, up to <paramref name="most"/> metres.</summary>
    private float OpenOut(Vector2 from, float most) {
        float open = 0;
        while (open < most && CanStand(from + OutOfDoor * Math.Min(open + OpenStep, most))) open = Math.Min(open + OpenStep, most);
        return open;
    }
    private const float OpenStep = .05f;

    /// <summary>Puts a wolf tugging the acting player behind them, where it holds on; with no open ground there, it stays where it stands.</summary>
    private void StandBehind(Wolf wolf) {
        var spot = TuggedTo + OutOfDoor * TugReach;
        if (CanStand(spot)) wolf.Pos = spot;
    }

    /// <summary>The wolf tugging the acting player out of a doorway, or null.</summary>
    private Wolf? Tugger() {
        foreach (var w in S.Wolves) if (w.Tugging && w.Target == S.Actor.Id) return w;
        return null;
    }

    /// <summary>Moves the acting player's doorway stage on: inside once stepped in, knocking once the door shuts in front, and free once done.</summary>
    private void TickDoorway(float dt) {
        var p = S.Actor;
        if (p.Door == Doorway.None) return;
        if (Downed || Find(p.DoorCabin)?.Kind != ThingKind.Hut) { p.Door = Doorway.None; return; }
        float before = p.DoorTime;
        p.DoorTime += dt;
        bool done = p.DoorTime >= DoorSeconds(p);
        switch (p.Door) {
            case Doorway.Entering when done:
                p.Sheltered = p.DoorCabin; p.Door = Doorway.None;
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
                // Tugged out, the player is let go: a badly hurt one is knocked down, anyone else steps in again at a run, or stops knocking.
                // The wolf bites again no sooner than after any other bite.
                var tugger = Tugger();
                if (tugger != null) { tugger.Tugging = false; tugger.Attack = 1; StandBehind(tugger); }
                LetGo(tugger != null && S.Health <= TakedownHealth ? tugger : null);
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

    /// <summary>A wolf reaching a player in the open part of a doorway takes them by the waist to tug them back out of it; returns whether it did.</summary>
    private bool TugOut(Wolf wolf) {
        var p = S.Actor;
        if (p.Door is not (Doorway.Entering or Doorway.Following or Doorway.Knocking or Doorway.Aside) || Indoors(p)) return false;
        Stage(p, p.Door is Doorway.Knocking or Doorway.Aside ? Doorway.TuggedOff : Doorway.Tugged, p.DoorCabin);
        wolf.Tugging = true; wolf.Leap = 0;
        // The player screams through the whole tug.
        Play("tug_scream", Player);
        return true;
    }

    /// <summary>
    /// Holds a tugging wolf at the waist of the player it tugs back, who is shown there; returns false once the tug has
    /// ended some other way, such as the player dying, and lets go. A wolf hit while it tugs is knocked back: the player
    /// stays as far out as it got them and steps in again at a run, or stops knocking.
    /// </summary>
    private bool Tug(Wolf wolf) {
        var p = S.Actor;
        if (p.Door is not (Doorway.Tugged or Doorway.TuggedOff)) { wolf.Tugging = false; return false; }
        if (wolf.Stagger > 0) {
            wolf.Tugging = false;
            LetGo(null);
            return false;
        }
        StandBehind(wolf);
        wolf.Attack = 1;
        return true;
    }

    /// <summary>
    /// Ends the acting player's tug out of a doorway where it has got them: <paramref name="takedown"/>, the wolf that tugged
    /// them, pins them from their left as they fall back toward it; otherwise a tugged player steps in again at a run, and
    /// one tugged off the door stands free.
    /// </summary>
    private void LetGo(Wolf? takedown) {
        var p = S.Actor;
        Player = TuggedTo; p.Warp++;
        if (takedown != null) {
            Pin(takedown, Hunters.Skip(1).Any(), -OutOfDoor.Orthogonal());
            p.Door = Doorway.None;
        } else if (p.Door == Doorway.Tugged) StepIn(p.DoorCabin, true);
        else p.Door = Doorway.None;
    }

    /// <summary>At this much health or less, a wolf's attack takes a player down rather than biting.</summary>
    public const float TakedownHealth = 50;

    /// <summary>Whether a wolf is tugging the acting player out of a doorway, when only it may take them down.</summary>
    private bool Tugged => S.Actor.Door is Doorway.Tugged or Doorway.TuggedOff && Tugger() != null;
}
