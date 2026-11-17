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
    /// <summary>Banging on the shut door of a cabin someone is in.</summary>
    Knocking,
    /// <summary>Dragged back out of the doorway by a wolf holding them by the waist, to step in again at a run.</summary>
    Pulled,
    /// <summary>Dragged off the door they banged on by a wolf, to stand free.</summary>
    PulledOff,
    /// <summary>Stepping out through the door, out in the open for the second half.</summary>
    Leaving,
    /// <summary>Standing aside from the door for someone stepping out, to step in once they are out.</summary>
    Aside,
}

/// <summary>
/// Cabins: one player at a time steps in through the door, the next one at the door bangs on it, and a wolf can drag
/// whoever is out in the open in the doorway back out.
/// </summary>
public sealed partial class World {
    /// <summary>Seconds stepping in through a cabin door at a walk and at a sprint, stepping out, banging on a shut door, and being dragged back out.</summary>
    public const float EnterSeconds = 1.3f, RushSeconds = .7f, LeaveSeconds = 1.5f, KnockSeconds = 1, PullSeconds = 1.5f;
    /// <summary>
    /// Seconds a cabin's door takes to swing shut, and before the first bang on it: a player left outside waits for the
    /// door to shut, then steps up to it.
    /// </summary>
    public const float ShutSeconds = .4f, KnockLead = .5f;
    /// <summary>How far, in metres, a wolf drags a player back out from a cabin's door, in how many tugs.</summary>
    public const float DragDistance = 1;
    public const int Tugs = 3;
    /// <summary>The way out of a cabin's door, which is in its front wall.</summary>
    public static readonly Vector2 OutOfDoor = Vector2.Down;
    /// <summary>How far behind a dragged player a tugging wolf stands, with its jaws at their waist.</summary>
    public const float TugReach = .75f;
    /// <summary>
    /// How far further out a player dragged out of a doorway has their throat once they fall onto their back, and how far
    /// to the side of it the wolf that pins them stands.
    /// </summary>
    public const float FallenThroat = .66f, AsideReach = .7f;
    /// <summary>Bangs on a shut door, spread over <see cref="KnockSeconds"/>.</summary>
    public const int Knocks = 3;
    /// <summary>How far, in metres, from <see cref="DoorPoint"/> a player can start stepping in.</summary>
    public const float DoorReach = .5f;
    /// <summary>Where a player stands at a cabin's door, from the cabin's middle: in front of the doorway in the model's front wall, past its step and clear of the door as it swings open.</summary>
    private static readonly Vector2 DoorOffset = new(-.34f, 2f);

    /// <summary>The spot just outside a cabin's door where a player steps in and out.</summary>
    public static Vector2 DoorPoint(Thing cabin) => cabin.Pos + DoorOffset;
    /// <summary>What the acting player is doing at a cabin's door.</summary>
    public Doorway Door => S.Actor.Door;

    /// <summary>Seconds a player's doorway stage lasts.</summary>
    public static float DoorSeconds(PlayerState p) => p.Door switch {
        Doorway.Entering => p.DoorRush ? RushSeconds : EnterSeconds,
        Doorway.Leaving => LeaveSeconds,
        Doorway.Knocking => KnockLead + KnockSeconds,
        Doorway.Pulled or Doorway.PulledOff => PullSeconds,
        _ => 0
    };

    /// <summary>Whether wolves can neither smell nor reach a player: inside a cabin, through the second half of stepping in and the first half of stepping out.</summary>
    public static bool Indoors(PlayerState p) =>
        p.Sheltered != 0 || p.Door == Doorway.Entering && p.DoorTime >= DoorSeconds(p) / 2 || p.Door == Doorway.Leaving && p.DoorTime < LeaveSeconds / 2;

    /// <summary>
    /// Starts stepping into a cabin from its door: in when it is free, up behind someone already stepping in, or banging on
    /// the door when someone is inside. A player who sprinted there, <paramref name="rush"/>, steps in at a run and slams the door.
    /// </summary>
    public bool Enter(Thing hut, bool rush) {
        if (hut.Kind != ThingKind.Hut || Downed || S.Sheltered != 0 || S.Actor.Door != Doorway.None || Player.DistanceTo(DoorPoint(hut)) > DoorReach) return false;
        if (!Forwarded(Verb.Enter, hut.Id, count: rush ? 1 : 0)) { S.Actor.DoorQueued = ++S.DoorArrivals; StepIn(hut.Id, rush); }
        return true;
    }

    /// <summary>Starts stepping out of the cabin the acting player is in, onto its doorstep.</summary>
    public void Leave() {
        if (Find(S.Sheltered) is not Thing cabin || S.Actor.Door != Doorway.None || Forwarded(Verb.Leave)) return;
        S.Sheltered = 0; Player = DoorPoint(cabin); S.Actor.Warp++;
        Stage(Doorway.Leaving, cabin.Id);
        // Whoever waits at the door, banging on it or standing there, steps aside to let them out, keeping the order they came in,
        // and the one stepping out keeps to the other side of the door, to pass them as they squeeze in.
        foreach (var other in S.Players.Where(o => o != S.Actor && Waiting(o, cabin)).OrderBy(o => o.DoorQueued).ToList()) {
            other.Door = Doorway.Aside; other.DoorCabin = cabin.Id; other.DoorTime = 0; other.DoorRush = false; other.DoorQueued = ++S.DoorArrivals;
            other.Pos = AsideSpot(other); other.Warp++;
            Player = DoorPoint(cabin) - AsideWay * MakeWay;
        }
    }
    /// <summary>How far to the other side of a cabin's door point someone stepping out past a player standing aside keeps.</summary>
    public const float MakeWay = .2f;

    /// <summary>Whether a player stepping out of a cabin is through its doorway, out in the open on the step.</summary>
    private static bool Through(PlayerState p) => p.DoorTime >= LeaveSeconds / 2;

    /// <summary>Whether someone in the doorway on their way out of the cabin a player stands aside at, or standing aside there before them, still keeps them from stepping in.</summary>
    private bool Blocked(PlayerState aside) {
        foreach (var o in S.Players)
            if (o.DoorCabin == aside.DoorCabin && (o.Door == Doorway.Leaving && !Through(o) || o.Door == Doorway.Aside && o.DoorQueued < aside.DoorQueued)) return true;
        return false;
    }

    /// <summary>Whether a player waits at a cabin's door: banging on it, or standing free within reach of it.</summary>
    private static bool Waiting(PlayerState p, Thing cabin) =>
        p.Sheltered == 0 && !p.Mauled && p.RespawnIn <= 0
        && (p.Door == Doorway.Knocking && p.DoorCabin == cabin.Id || p.Door == Doorway.None && p.Pos.DistanceTo(DoorPoint(cabin)) <= DoorReach);

    /// <summary>
    /// Where a player standing aside at a cabin's door stands: a step to the right of the door point, away from the door as it
    /// swings open, a further step out for each player who stood aside before them.
    /// </summary>
    public Vector2 AsideSpot(PlayerState p) {
        int before = S.Players.Count(o => o != p && o.Door == Doorway.Aside && o.DoorCabin == p.DoorCabin && o.DoorQueued < p.DoorQueued);
        return DoorPoint(Find(p.DoorCabin) ?? new Thing()) + AsideWay * (AsideStep + AsideGap * before);
    }
    /// <summary>The way players at a cabin's door step aside: to their right as they face the door.</summary>
    public static readonly Vector2 AsideWay = OutOfDoor.Orthogonal();
    /// <summary>How far to the side of a cabin's door point the first player standing aside stands, and how far apart the next ones stand.</summary>
    public const float AsideStep = .7f, AsideGap = .6f;

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
    /// What a player arriving at a cabin's door does, given who is there before them: steps in when nobody is, bangs on the
    /// door when someone is inside or stepping out, and follows someone stepping in.
    /// </summary>
    private Doorway Ahead(int cabin, PlayerState arriving) {
        var door = Doorway.Entering;
        foreach (var p in S.Players) {
            if (p == arriving) continue;
            // Someone still in the doorway on their way out keeps the door; once they are through it, the next one squeezes past them in.
            if (p.Sheltered == cabin || p.DoorCabin == cabin && p.Door == Doorway.Leaving && !Through(p)) return Doorway.Knocking;
            // Someone dragged back out, or standing aside for someone stepping out, stays ahead only if they came to the door first.
            if (p.DoorCabin == cabin && (p.Door == Doorway.Entering || p.Door is Doorway.Pulled or Doorway.Aside && p.DoorQueued < arriving.DoorQueued)) door = Doorway.Following;
        }
        return door;
    }

    private void StepIn(int cabin, bool rush) {
        Stage(Ahead(cabin, S.Actor), cabin, rush);
    }

    private void Stage(Doorway door, int cabin, bool rush = false) {
        var p = S.Actor;
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
    /// How much of the way a wolf has dragged a player, 0 to 1, <paramref name="done"/> of the way through the drag: each of
    /// its tugs pulls them back sharply, then holds while the wolf gets a new grip.
    /// </summary>
    public static float Dragged(float done) {
        float tugs = Math.Clamp(done, 0, 1) * Tugs, tug = MathF.Min(MathF.Floor(tugs), Tugs - 1);
        return (tug + Mathf.SmoothStep(0, 1, Math.Clamp((tugs - tug) / .4f, 0, 1))) / Tugs;
    }

    /// <summary>Where the acting player is: their place, or as far back from it as a wolf dragging them out of a doorway has hauled them.</summary>
    private Vector2 Hauled => S.Actor.Door is Doorway.Pulled or Doorway.PulledOff ? Player + OutOfDoor * (DragDistance * Dragged(S.Actor.DoorTime / PullSeconds)) : Player;

    /// <summary>The wolf dragging the acting player out of a doorway, or null.</summary>
    private Wolf? Tugger() {
        foreach (var w in S.Wolves) if (w.Tugging && w.Target == S.Actor.Id) return w;
        return null;
    }

    /// <summary>Moves the acting player's doorway stage on: inside once stepped in, banging once the door shuts in front, and free once done.</summary>
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
                if (Ahead(p.DoorCabin, p) is var next && next != Doorway.Following) StepIn(p.DoorCabin, p.DoorRush);
                break;
            case Doorway.Knocking:
                Beats("door_bang", before, p.DoorTime, KnockLead, KnockSeconds / Knocks, Knocks);
                if (done) p.Door = Doorway.None;
                break;
            case Doorway.Pulled or Doorway.PulledOff:
                // The player screams at the grab and at each tug after it.
                Beats("pulled_scream", before, p.DoorTime, PullSeconds / Tugs, PullSeconds / Tugs, Tugs - 1);
                if (!done) break;
                // Dragged a metre out, the player is let go: a badly hurt one is knocked down, anyone else steps in again at a run, or stops banging.
                // The wolf bites again no sooner than after any other bite.
                Player += OutOfDoor * DragDistance; p.Warp++;
                var tugger = Tugger();
                if (tugger != null) { tugger.Tugging = false; tugger.Attack = 1; tugger.Pos = Player + OutOfDoor * TugReach; }
                // The wolf that dragged them steps aside, to their left, as they fall back toward it.
                if (S.Health <= 50 && (tugger ?? NearestHunter()) is Wolf wolf) {
                    Pin(wolf, Hunters.Skip(1).Any(), tugger != null ? -OutOfDoor.Orthogonal() : default);
                    p.Door = Doorway.None;
                } else if (p.Door == Doorway.Pulled) StepIn(p.DoorCabin, true);
                else p.Door = Doorway.None;
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

    /// <summary>A wolf reaching a player in the open part of a doorway takes them by the waist to drag them back out of it; returns whether it did.</summary>
    private bool PullOut(Wolf wolf) {
        var p = S.Actor;
        if (p.Door is not (Doorway.Entering or Doorway.Following or Doorway.Knocking) || Indoors(p)) return false;
        Stage(p.Door == Doorway.Knocking ? Doorway.PulledOff : Doorway.Pulled, p.DoorCabin);
        wolf.Tugging = true; wolf.Leap = 0;
        Play("pulled_scream", Player);
        return true;
    }

    /// <summary>
    /// Holds a tugging wolf at the waist of the player it drags back, who is shown there; returns false once the drag has
    /// ended some other way, such as the player dying, and lets go.
    /// </summary>
    private bool Tug(Wolf wolf) {
        if (S.Actor.Door is not (Doorway.Pulled or Doorway.PulledOff)) { wolf.Tugging = false; return false; }
        wolf.Pos = Player + OutOfDoor * (DragDistance * Dragged(S.Actor.DoorTime / PullSeconds) + TugReach);
        wolf.Attack = 1;
        return true;
    }

    /// <summary>Whether a wolf is dragging the acting player out of a doorway, when only it may take them down.</summary>
    private bool Tugged => S.Actor.Door is Doorway.Pulled or Doorway.PulledOff && Tugger() != null;

    private Wolf? NearestHunter() {
        Wolf? best = null;
        foreach (var w in S.Wolves)
            if (w.Target == S.Actor.Id && w.Flee <= 0 && (best == null || w.Pos.DistanceTo(Player) < best.Pos.DistanceTo(Player))) best = w;
        return best;
    }
}
