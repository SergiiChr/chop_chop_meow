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
    /// <summary>Dragged back out of the doorway by a wolf, to step in again at a run.</summary>
    Pulled,
    /// <summary>Dragged off the door they banged on by a wolf, to stand free.</summary>
    PulledOff,
    /// <summary>Stepping out through the door, out in the open for the second half.</summary>
    Leaving,
}

/// <summary>
/// Cabins: one player at a time steps in through the door, the next one at the door bangs on it, and a wolf can drag
/// whoever is out in the open in the doorway back out.
/// </summary>
public sealed partial class World {
    /// <summary>Seconds stepping in through a cabin door at a walk and at a sprint, stepping out, banging on a shut door, and being dragged back out.</summary>
    public const float EnterSeconds = 1.5f, RushSeconds = .7f, LeaveSeconds = 1.5f, KnockSeconds = 1, PullSeconds = .3f;
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
        Doorway.Knocking => KnockSeconds,
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
        if (!Forwarded(Verb.Enter, hut.Id, count: rush ? 1 : 0)) StepIn(hut.Id, rush);
        return true;
    }

    /// <summary>Starts stepping out of the cabin the acting player is in, onto its doorstep.</summary>
    public void Leave() {
        if (Find(S.Sheltered) is not Thing cabin || S.Actor.Door != Doorway.None || Forwarded(Verb.Leave)) return;
        S.Sheltered = 0; Player = DoorPoint(cabin); S.Actor.Warp++;
        Stage(Doorway.Leaving, cabin.Id);
    }

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
            if (p.Sheltered == cabin || p.DoorCabin == cabin && p.Door == Doorway.Leaving) return Doorway.Knocking;
            if (p.DoorCabin == cabin && p.Door is Doorway.Entering or Doorway.Pulled) door = Doorway.Following;
        }
        return door;
    }

    private void StepIn(int cabin, bool rush) {
        var door = Ahead(cabin, S.Actor);
        Stage(door, cabin, rush);
        if (door == Doorway.Knocking) Play("door_bang", Player);
    }

    private void Stage(Doorway door, int cabin, bool rush = false) {
        var p = S.Actor;
        p.Door = door; p.DoorCabin = cabin; p.DoorTime = 0; p.DoorRush = rush;
    }

    /// <summary>Plays the bangs on a shut door after the first that fall after <paramref name="from"/> seconds and by <paramref name="to"/>.</summary>
    private void Knock(float from, float to) {
        for (int i = 1; i < Knocks; i++)
            if (from < i * KnockSeconds / Knocks && i * KnockSeconds / Knocks <= to) Play("door_bang", Player);
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
                Knock(before, p.DoorTime);
                if (done) p.Door = Doorway.None;
                break;
            case Doorway.Pulled or Doorway.PulledOff when done:
                // Out of the doorway, a badly hurt player is knocked down; anyone else steps in again at a run, or stops banging.
                if (S.Health <= 50 && NearestHunter() is Wolf wolf) Pin(wolf, Hunters.Skip(1).Any());
                else if (p.Door == Doorway.Pulled) StepIn(p.DoorCabin, true);
                else p.Door = Doorway.None;
                break;
            case Doorway.Leaving when done:
                p.Door = Doorway.None;
                break;
        }
    }

    /// <summary>A wolf reaching a player in the open part of a doorway drags them back out of it; returns whether it did.</summary>
    private bool PullOut() {
        var p = S.Actor;
        if (p.Door is not (Doorway.Entering or Doorway.Following or Doorway.Knocking) || Indoors(p)) return false;
        Stage(p.Door == Doorway.Knocking ? Doorway.PulledOff : Doorway.Pulled, p.DoorCabin);
        return true;
    }

    private Wolf? NearestHunter() {
        Wolf? best = null;
        foreach (var w in S.Wolves)
            if (w.Target == S.Actor.Id && w.Flee <= 0 && (best == null || w.Pos.DistanceTo(Player) < best.Pos.DistanceTo(Player))) best = w;
        return best;
    }
}
