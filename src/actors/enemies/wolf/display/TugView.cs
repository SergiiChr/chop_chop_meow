using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Draws wolves tugging players out of cabin doorways, and the takedown that can end it. <paramref name="world"/> gives the
/// world drawn, and <paramref name="stateOf"/> how a player is drawn, or null for one not drawn yet.
/// </summary>
public sealed class TugView(Func<World> world, Func<int, ActorState?> stateOf) {
    /// <summary>How far behind a player's hips a tugging wolf's jaws hold them.</summary>
    private const float WaistBehind = .12f;
    /// <summary>The back of the waist of the player a wolf hunts as drawn, while they are out in the open in a cabin's doorway, or null.</summary>
    public Vector3? InTheDoorway(Wolf wolf) =>
        world().PlayerById(wolf.Target) is { Door: Doorway.Entering or Doorway.Following or Doorway.Knocking } p && !Doorways.Indoors(p) && stateOf(p.Id) is ActorState held ? Waist(held) : null;

    /// <summary>The player a tugging wolf tugs as drawn, and how far through the tug they are, 0 to 1; null once they are not being tugged.</summary>
    private (ActorState Held, float Progress)? TuggedBy(Wolf wolf) =>
        world().PlayerById(wolf.Target) is { Door: Doorway.Tugged or Doorway.TuggedOff } p && stateOf(p.Id) is ActorState held ? (held, Math.Clamp(p.DoorTime / DoorTug.TugSeconds, 0, 1)) : null;

    /// <summary>The back of a drawn player's waist, where a wolf takes hold of them.</summary>
    private static Vector3 Waist(ActorState held) => WorldView.To3(held.Position - ActorAnimation.Forward(held.Yaw) * WaistBehind) + Vector3.Up * (held.HipHeight + held.Lift);

    /// <summary>
    /// How far beside a fallen player's throat a wolf that tugged them out waits while they fall, and the shares of
    /// <see cref="ActorAnimation.FallBackSeconds"/> over which it then lunges onto the throat.
    /// </summary>
    private const float WaitAside = 1.2f, LungeStart = .74f, LungeEnd = .92f;
    /// <summary>
    /// Which way a wolf tugging <paramref name="held"/> turns from another wolf tugging someone within <see cref="TuggersApart"/>
    /// of them: 1 to the held player's left, -1 to their right, 0 with nobody there.
    /// </summary>
    private float ApartFrom(Wolf tugger, ActorState held) {
        foreach (var other in world().Wolves)
            if (other != tugger && other.Tugging && TuggedBy(other) is var (them, _) && them != held && them.Position.DistanceTo(held.Position) < TuggersApart)
                return (held.Position - them.Position).Dot(ActorAnimation.Forward(held.Yaw).Orthogonal()) >= 0 ? -1 : 1;
        return 0;
    }

    /// <summary>Seconds a wolf taking hold of a player to tug them takes to lunge in to their waist.</summary>
    private const float TugLunge = .2f;
    /// <summary>How near two tugged players are when their wolves angle apart, and by how many degrees.</summary>
    private const float TuggersApart = 1.2f, SplayTugger = 35;

    /// <summary>Shows a wolf tugging a player out of a cabin's doorway; returns false while the player it tugs is not drawn as tugged.</summary>
    public bool ShowTugging(Wolf wolf, WolfModel model, float elapsed, float dt) {
        if (TuggedBy(wolf) is not var (held, through)) return false;
        // Its jaws hold the back of the player's hips as they are drawn, the wolf facing the way they face,
        // or angled away from another wolf tugging someone beside them so the two bodies keep clear.
        // Taking hold, it lunges in to the waist from where it stood rather than appearing there.
        if (model.Motion != WolfMotion.Tug) { model.StepFrom = model.Root.Position; model.FromYaw = model.Yaw; }
        float lunge = WorldView.Ease(Math.Min(1, through * DoorTug.TugSeconds / TugLunge));
        model.Yaw = Mathf.LerpAngle(model.FromYaw, held.Yaw + Mathf.DegToRad(SplayTugger) * ApartFrom(wolf, held), lunge);
        model.KnockBack = Vector2.Zero; model.Stagger = wolf.Stagger; model.Toward = Vector2.Zero; model.Pinned = 0;
        model.Turning.Hold(model.Yaw); model.Root.Rotation = new Vector3(0, model.Yaw, 0);
        model.Root.Visible = world().Sees(wolf.Pos);
        model.Tug(through, elapsed, dt);
        var grip = Waist(held) - model.Root.Basis * model.Bite;
        model.Root.Position = model.StepFrom.Lerp(grip with { Y = 0 }, lunge);
        model.Drift = new Vector2(model.Root.Position.X, model.Root.Position.Z) - wolf.Pos;
        return true;
    }

    /// <summary>Shows a wolf that tugged its player out lunging onto them as they fall; returns false while the player is not drawn.</summary>
    public bool ShowLunging(Wolf wolf, WolfModel model, float elapsed, float dt) {
        if (stateOf(wolf.Target) is not ActorState fallen) return false;
        // Having tugged its player out, it lets go as they fall back toward it, steps round to their side and lunges onto the throat as they land.
        if (model.Pinned == 0) model.StepFrom = model.Root.Position;
        model.Pinned += dt;
        float fell = model.Pinned / ActorAnimation.FallBackSeconds;
        var throat = FallBackView.Throat(fallen.Position, ActorAnimation.Forward(fallen.Yaw), float.MaxValue);
        var wait = (throat + WorldView.To3(wolf.LungeFrom) * WaitAside) with { Y = 0 };
        model.KnockBack = Vector2.Zero; model.Stagger = wolf.Stagger; model.Toward = Vector2.Zero;
        model.Yaw = Mathf.LerpAngle(model.Yaw, ActorAnimation.Yaw(-wolf.LungeFrom, model.Yaw), Math.Min(1, dt * 8));
        model.Turning.Hold(model.Yaw); model.Root.Rotation = new Vector3(0, model.Yaw, 0);
        model.Root.Visible = world().Sees(wolf.Pos);
        if (fell < LungeStart) {
            // It springs back off the yanked body at once, then slows as it comes round.
            float off = fell / LungeStart;
            var at = model.StepFrom.Lerp(wait, 1 - (1 - off) * (1 - off));
            // It backs off the falling body rather than walking forward while it goes back.
            model.Move((at - model.Root.Position).Dot(model.Root.Basis.Z), dt, false, false);
            model.Root.Position = at;
            return true;
        }
        model.Feed(elapsed + wolf.Id % 7, dt, true, 1, lunged: true);
        if (fell < LungeEnd) {
            var bite = (throat - model.Root.Basis * model.Bite) with { Y = 0 };
            model.Root.Position = model.Held = wait.Lerp(bite, WorldView.Ease((fell - LungeStart) / (LungeEnd - LungeStart)));
        } else model.Root.Position = model.Held;
        return true;
    }
}
