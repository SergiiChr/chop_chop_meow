using System;
using Godot;

namespace ChopChopMeow;

/// <summary>How an actor, a player or worker, is drawn: where, facing which way, doing what and how far into it.</summary>
public sealed class ActorState {
    public Vector2 Position;
    public float Yaw;
    /// <summary>
    /// How the body is turning: the head turns toward a new heading first and the body follows with weight, easing into
    /// and out of the turn and leaning into it.
    /// </summary>
    public Turning Turning;

    /// <summary>The doorway stage drawn last frame, where the actor stood when it began, and the stage before it.</summary>
    public Doorway Stage;
    public Vector2 StageFrom;
    public Doorway StageBefore;
    /// <summary>Seconds left stepping back down off a cabin's step after knocking on its door.</summary>
    public float SteppingDown;
    /// <summary>How high the steps of a cabin's door lift the actor as last drawn, and how fast, in metres a second, that height is changing.</summary>
    public float Lift, LiftSpeed;
    /// <summary>Whether the actor was drawn on a cabin's steps last frame; one that was not starts where it stands on them.</summary>
    public bool OnSteps;
    /// <summary>How high the actor's hips stand above the ground it is posed on, as last drawn.</summary>
    public float HipHeight;
    public ActorAction Action;
    public float Time;
    public float PreviousTime = -1;
    public bool Moving;
    // The action blended out of after a change, where it was, and how far the blend into the new one has gone.
    public ActorAction From;
    public float FromTime;
    public bool FromMoving;
    private float eatingWeight = 1, fromEatingWeight = 1;
    public float Blend = 1;
    /// <summary>Where a blow lands its tool's working end, a point in the world, or null when the action strikes nothing.</summary>
    public Vector3? Mark;
    /// <summary>That point in the rig's own frame as the rig was last placed, which the pose aims at.</summary>
    public Vector3? MarkInRig { get => aim.Mark; set => aim.Mark = value; }
    /// <summary>
    /// How the action's blows are aimed at <see cref="MarkInRig"/>, and how the action blended out of aimed its own,
    /// which its pose keeps through the blend.
    /// </summary>
    private ActorAnimation.BlowAim aim = new(), fromAim = new();
    /// <summary>Moves and poses the actor for a frame; it faces where it moves, or <paramref name="facingTarget"/> while standing or <paramref name="backing"/> away from it.</summary>
    public void Update(Vector2 position, ActorAction action, Vector2 facingTarget, float dt, bool backing = false,
        Vector2? swingAim = null, bool eating = true) {
        float moved = position.DistanceTo(Position);
        bool moving = moved > .001f;
        float yaw = ActorAnimation.Yaw(moving && !backing ? position - Position : facingTarget - position, Yaw);
        // A jump such as a respawn is no speed to lean into.
        if (swingAim is Vector2 aimDirection) {
            Yaw = ActorAnimation.Yaw(aimDirection, Yaw);
            Turning.Hold(Yaw);
        } else Turning.Step(ref Yaw, yaw, dt > 0 ? Math.Min(moved / dt, 8) : 0, dt, action == ActorAction.Run);
        Position = position;
        float wantsEating = eating ? 1 : 0;
        if (Action != action) {
            From = Action; FromTime = Time; FromMoving = Moving; Blend = 0;
            fromEatingWeight = eatingWeight; eatingWeight = wantsEating;
            (aim, fromAim) = (fromAim, aim);
            aim.Mark = fromAim.Mark;
            Action = action; Time = 0; PreviousTime = -1;
        } else {
            PreviousTime = Time; Time += ActorAnimation.Advance(action, dt, moved);
            eatingWeight = Mathf.MoveToward(eatingWeight, wantsEating, dt / ActorAnimation.BlendSeconds);
        }
        FromTime += ActorAnimation.Advance(From, dt, moved);
        Blend = Math.Min(1, Blend + dt / ActorAnimation.BlendSeconds);
        Moving = moving;
    }
    public Pose Pose => ActorAnimation.Blended(From, FromTime, FromMoving, Action, Time, Moving, Blend, aim, fromAim, eatingWeight, fromEatingWeight);

    /// <summary>True on the frame an action loop passes its impact moment.</summary>
    public bool Struck(float at) {
        if (PreviousTime < 0) return false;
        float before = ActorAnimation.Phase(Action, PreviousTime), now = ActorAnimation.Phase(Action, Time);
        return before <= now ? before < at && at <= now : at > before || at <= now;
    }
}
