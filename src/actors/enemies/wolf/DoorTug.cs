using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A wolf in <paramref name="world"/> that reaches a player out in the open in a cabin's doorway takes them by the waist
/// and tugs them back out of it, then lets go: a badly hurt player falls back and is pinned, anyone else steps in again.
/// While it tugs, one more wolf on each side of the player can take them by the arm, bite once and hold on until the tug
/// ends; any other wolf waits beside them.
/// </summary>
public sealed class DoorTug(World world) {
    /// <summary>Seconds a wolf takes to tug a player back out of a doorway.</summary>
    public const float TugSeconds = 1.5f;
    /// <summary>How far, in metres, a wolf tugs a player back out from a cabin's door, in how many tugs.</summary>
    public const float TugDistance = 1;
    public const int Tugs = 3;
    /// <summary>The share of each tug spent tugging; the wolf holds for the rest of it.</summary>
    public const float Heave = .4f;
    /// <summary>How far behind a tugged player a tugging wolf stands, with its jaws at their waist.</summary>
    public const float TugReach = .75f;
    /// <summary>
    /// How far further out a player tugged out of a doorway has their throat once they fall onto their back, and how far
    /// to the side of it the wolf that pins them stands.
    /// </summary>
    public const float FallenThroat = .66f, AsideReach = .7f;
    /// <summary>How far to a side of a tugged player a wolf that holds their arm stands, and how near them it has to come to take it.</summary>
    public const float SideReach = .8f, SideWithin = 1.3f;
    /// <summary>Seconds into a tug out of a cabin by which the player is far enough out of its door for wolves to take their sides.</summary>
    public const float OutOfDoorBy = .6f;

    /// <summary>Seconds a tug out of a doorway lasts: a wounded player is taken down as the last tug lands, with no hold after it.</summary>
    public static float Seconds(PlayerState p) => p.Health <= World.TakedownHealth ? TugSeconds * (Tugs - 1 + Heave) / Tugs : TugSeconds;

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
    public Vector2 TuggedTo {
        get {
            var p = world.S.Actor;
            return p.Door is Doorway.Tugged or Doorway.TuggedOff ? world.Player + Doorways.OutOfDoor * (TugDistanceFrom(world.Player) * TugProgress(p.DoorTime / TugSeconds)) : world.Player;
        }
    }

    /// <summary>
    /// How far a wolf tugs a player standing at <paramref name="from"/> straight out of a doorway: <see cref="TugDistance"/>, cut
    /// short where open ground ends, leaving room on it for the wolf behind them where there is any.
    /// </summary>
    public float TugDistanceFrom(Vector2 from) {
        if (from != measured.From || Math.Abs(world.Time - measured.At) > TugSeconds) {
            float both = OpenOut(from, TugDistance + TugReach) - TugReach;
            measured = (from, world.Time, both >= 0 ? both : OpenOut(from, TugDistance));
        }
        return measured.Distance;
    }
    /// <summary>
    /// The distance last measured, from where and when: every wolf at a tug reads it several times a tick, and open
    /// ground does not change in the second and a half a tug lasts.
    /// </summary>
    private (Vector2 From, float At, float Distance) measured = (new Vector2(float.NaN, float.NaN), 0, 0);

    /// <summary>How far open ground runs from <paramref name="from"/> straight out of a doorway, up to <paramref name="most"/> metres.</summary>
    private float OpenOut(Vector2 from, float most) {
        float open = 0;
        while (open < most && world.CanStand(from + Doorways.OutOfDoor * Math.Min(open + OpenStep, most))) open = Math.Min(open + OpenStep, most);
        return open;
    }
    private const float OpenStep = .05f;

    /// <summary>Puts a wolf tugging the acting player behind them, where it holds on; with no open ground there, it stays where it stands.</summary>
    private void StandBehind(Wolf wolf) {
        var spot = TuggedTo + Doorways.OutOfDoor * TugReach;
        if (world.CanStand(spot)) wolf.Pos = spot;
    }

    /// <summary>The wolf tugging the acting player out of a doorway, or null.</summary>
    private Wolf? Tugger() {
        foreach (var w in world.S.Wolves) if (w.Tugging && w.Target == world.S.Actor.Id) return w;
        return null;
    }

    /// <summary>Whether a wolf is tugging the acting player out of a doorway, when only it may take them down.</summary>
    public bool Tugged => world.S.Actor.Door is Doorway.Tugged or Doorway.TuggedOff && Tugger() != null;

    /// <summary>A wolf reaching a player in the open part of a doorway takes them by the waist to tug them back out of it; returns whether it did.</summary>
    public bool TugOut(Wolf wolf) {
        var p = world.S.Actor;
        if (p.Door is not (Doorway.Entering or Doorway.Following or Doorway.Knocking or Doorway.Aside) || Doorways.Indoors(p)) return false;
        Begin(wolf, p.Door is Doorway.Knocking or Doorway.Aside ? Doorway.TuggedOff : Doorway.Tugged, p.DoorCabin);
        return true;
    }

    /// <summary>A wolf takes the acting player by the waist on a tugged doorway stage at a cabin, and they scream through the whole tug.</summary>
    internal void Begin(Wolf wolf, Doorway stage, int cabin) {
        Doorways.Stage(world.S.Actor, stage, cabin);
        wolf.Tugging = true; wolf.Leap = 0;
        world.Play("tug_scream", world.Player);
    }

    /// <summary>
    /// Holds a tugging wolf at the waist of the player it tugs back, who is shown there; returns false once the tug has
    /// ended some other way, such as the player dying, and lets go. A wolf hit while it tugs is knocked back: the player
    /// stays as far out as it got them and steps in again at a run, or stops knocking.
    /// </summary>
    public bool Tug(Wolf wolf) {
        var p = world.S.Actor;
        if (p.Door is not (Doorway.Tugged or Doorway.TuggedOff)) { Release(wolf); return false; }
        if (wolf.Stagger > 0) {
            Release(wolf);
            LetGo(null);
            return false;
        }
        StandBehind(wolf);
        wolf.Attack = 1;
        return true;
    }

    /// <summary>The way a tugged player faces: into the cabin they are tugged out of.</summary>
    private static readonly Vector2 Facing = -Doorways.OutOfDoor;

    /// <summary>Where a wolf holding the acting player's arm on a <see cref="BodySide"/> stands.</summary>
    private Vector2 SideSpot(int side) => TuggedTo + BodySide.Out(Facing, side) * SideReach;

    /// <summary>
    /// A wolf that reaches the acting player while another wolf tugs them takes them by the arm on the side it is on, or on
    /// the other when that one is taken or has no open ground; returns whether it did.
    /// It bites as it takes hold, or as soon after as its last bite is a second behind it.
    /// A player a wolf takes out of a cabin has to be out of its door first.
    /// </summary>
    public bool TakeSide(Wolf wolf) {
        var p = world.S.Actor;
        if (Tugger() is not Wolf tugger || tugger == wolf || p.Door is not (Doorway.Tugged or Doorway.TuggedOff) || wolf.Pos.DistanceTo(TuggedTo) >= SideWithin) return false;
        if (tugger.DoorCheck == DoorCheckStage.TakingOut && p.DoorTime < OutOfDoorBy) return false;
        int side = BodySide.Free(TuggedTo, Facing, wolf.Pos, Taken(BodySide.Left), Taken(BodySide.Right));
        if (side == 0) return false;
        wolf.Side = side; wolf.SideBitten = false; wolf.Pos = SideSpot(side); wolf.Leap = 0;
        BiteArm(wolf);
        return true;
    }

    /// <summary>A wolf holding the acting player's arm bites it once, when its last bite is a second behind it.</summary>
    private void BiteArm(Wolf wolf) {
        if (wolf.SideBitten || wolf.Attack > 0) return;
        wolf.SideBitten = true; world.S.Actor.Health -= world.Data.WolfBite; world.Play("wolf_bite", world.Player);
    }

    /// <summary>Whether no wolf can take a side of the acting player: another holds it, or it has no open ground.</summary>
    private bool Taken(int side) => Holder(side) != null || !world.CanStand(SideSpot(side));

    /// <summary>The wolf holding the acting player's arm on a side, or null.</summary>
    private Wolf? Holder(int side) {
        foreach (var w in world.S.Wolves) if (w.Side == side && w.Target == world.S.Actor.Id) return w;
        return null;
    }

    /// <summary>
    /// Keeps a wolf that holds the acting player's arm at their side while they are tugged; returns false, letting go,
    /// once the tug is over or the wolf has been hit.
    /// </summary>
    public bool Hold(Wolf wolf) {
        if (wolf.Side == 0) return false;
        if (!Tugged || wolf.Stagger > 0) { wolf.LetGoOfArm(); wolf.Attack = 1; return false; }
        if (world.CanStand(SideSpot(wolf.Side))) wolf.Pos = SideSpot(wolf.Side);
        BiteArm(wolf);
        // Once it has bitten it only holds on.
        if (wolf.SideBitten) wolf.Attack = 1;
        return true;
    }

    /// <summary>A wolf lets go of the player it tugged, which also ends the door check at a cabin that took them out of it.</summary>
    private static void Release(Wolf wolf) { wolf.Tugging = false; wolf.EndDoorCheck(); }

    /// <summary>
    /// Ends a tug that has run its time: the player is let go, a badly hurt one knocked down, anyone else stepping in again
    /// at a run, or stopping knocking. The wolf bites again no sooner than after any other bite.
    /// </summary>
    public void End() {
        var tugger = Tugger();
        if (tugger != null) { Release(tugger); tugger.Attack = 1; StandBehind(tugger); }
        LetGo(tugger != null && world.S.Health <= World.TakedownHealth ? tugger : null);
    }

    /// <summary>
    /// Ends the acting player's tug out of a doorway where it has got them: <paramref name="takedown"/>, the wolf that tugged
    /// them, pins them from the side of them it stands on as they fall back toward it; otherwise a tugged player steps in
    /// again at a run, and one tugged off the door stands free.
    /// </summary>
    private void LetGo(Wolf? takedown) {
        var p = world.S.Actor;
        // The wolves at the player's sides let go with the one that tugged, and bite again no sooner than after any other bite.
        foreach (var w in world.S.Wolves) if (w.Side != 0 && w.Target == p.Id) { w.LetGoOfArm(); w.Attack = 1; }
        var at = TuggedTo;
        world.Player = at; p.Warp++;
        if (takedown != null) {
            world.WolfHunt.Pin(takedown, world.Hunters.Skip(1).Any(), BodySide.Out(Facing, BodySide.Of(at, Facing, takedown.Pos)));
            p.Door = Doorway.None;
        } else if (p.Door == Doorway.Tugged) world.Doorways.StepIn(p.DoorCabin, true);
        else p.Door = Doorway.None;
    }
}
