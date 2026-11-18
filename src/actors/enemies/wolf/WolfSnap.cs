using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A wolf's bite at a player out in the open in <paramref name="world"/>: within biting distance it gathers itself, then
/// leaps a short way to where the player is heading and snaps. It bites only a player its jaws reach as it lands, so one
/// who turns aside fast enough as it leaps is missed, and a blow that lands on it first stops the bite.
/// </summary>
public sealed class WolfSnap(World world) {
    /// <summary>How near a wolf has to be to start a snap, in metres.</summary>
    public const float Trigger = 1.1f;
    /// <summary>Seconds a wolf gathers itself before it leaps, still keeping on its player, and seconds it is in the air.</summary>
    public const float Windup = .3f, Flight = .08f;
    /// <summary>Seconds after a snap before the wolf starts another, and after it bites a player in a doorway before it bites again.</summary>
    public const float Rest = .6f, GrabRest = 1;
    /// <summary>How far a snap can carry a wolf, in metres.</summary>
    public const float Range = 1.2f;
    /// <summary>How far short of the middle of the player the jaws are aimed, at the near side of their body.</summary>
    public const float JawsShort = .2f;
    /// <summary>How near the jaws have to end to where they were aimed on the player for the bite to land, in metres.</summary>
    public const float Reach = .5f;

    /// <summary>Whether a wolf is gathering itself for a snap, before it leaves the ground.</summary>
    public static bool Winding(Wolf wolf) => wolf.Snap > Flight;

    /// <summary>
    /// A wolf within reach of the acting player whose last snap is behind it starts another. One in a doorway it bites
    /// and takes by the waist at once. While another wolf tugs them it takes them by an arm instead, or waits beside them
    /// with both arms taken.
    /// </summary>
    internal void Start(Wolf wolf) {
        if (world.DoorTug.TakeSide(wolf) || world.DoorTug.Tugged || wolf.Attack > 0 || wolf.Pos.DistanceTo(world.Player) >= Trigger) return;
        if (world.DoorTug.InOpenDoorway) { wolf.Attack = GrabRest; Bite(wolf); return; }
        wolf.Snap = Windup + Flight; wolf.SnapSeen = world.Player;
    }

    /// <summary>Counts a snap down: the wolf leaves the ground when it has gathered itself, flies to where it leapt for and snaps as it lands.</summary>
    internal void Tick(Wolf wolf, float dt) {
        if (Winding(wolf)) {
            if (wolf.Snap - dt > Flight) { wolf.Snap -= dt; return; }
            // The rest of this tick is spent in the air.
            dt -= wolf.Snap - Flight; wolf.Snap = Flight;
            Launch(wolf);
        }
        // Closing the remaining gap evenly makes every leap last Flight.
        wolf.Pos = world.Slide(wolf.Pos, (wolf.SnapTo - wolf.Pos) * Math.Min(1, dt / wolf.Snap));
        wolf.Snap = Math.Max(0, wolf.Snap - dt);
        if (wolf.Snap == 0) Land(wolf);
    }

    /// <summary>
    /// The wolf leaves the ground for where the player will be as it lands if they keep going the way they went while it
    /// gathered itself; from here on it does not follow them.
    /// </summary>
    private void Launch(Wolf wolf) {
        var aim = world.Player + (world.Player - wolf.SnapSeen) / Windup * Flight;
        var toward = aim - wolf.Pos;
        if (toward.LengthSquared() > 1e-6f) wolf.Heading = toward.Angle();
        wolf.SnapTo = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * Math.Clamp(toward.Length() - WolfBodies.WolfNose - JawsShort, 0, Range);
    }

    /// <summary>The jaws snap: they bite a player still out in the open within reach of them, and tug one in a doorway out of it.</summary>
    private void Land(Wolf wolf) {
        wolf.Attack = Rest;
        var jaws = wolf.Pos + Vector2.Right.Rotated(wolf.Heading) * (WolfBodies.WolfNose + JawsShort);
        if (!world.Downed && !world.DoorTug.Tugged && jaws.DistanceTo(world.Player) <= Reach) Bite(wolf);
    }

    /// <summary>The bite itself: it takes the player's health, and a wolf that bites a player in a doorway tugs them back out of it.</summary>
    private void Bite(Wolf wolf) { world.S.Health -= world.Data.WolfBite; world.Play("wolf_bite", world.Player); world.DoorTug.TugOut(wolf); }
}
