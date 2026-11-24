using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Draws players at cabin doors: stepping in and out, following, knocking, standing aside and squeezing past someone
/// with a palm on the door frame. <paramref name="world"/> gives the world drawn, and <paramref name="stateOf"/> how a
/// player is drawn, or null for one not drawn yet.
/// </summary>
public sealed class DoorwayView(Func<World> world, Func<int, ActorState?> stateOf) {
    /// <summary>
    /// A player at a cabin's door as their doorway stage shows them, null when they are at no door.
    /// </summary>
    public DoorShown? AtDoor(PlayerState p, ActorState state, float dt, bool swinging) {
        if (state.Stage != p.Door) {
            if (state.Stage == Doorway.Knocking && p.Door == Doorway.None) state.SteppingDown =
                DoorwayPath.StepUpSeconds;
            state.StageBefore = state.Stage; state.Stage = p.Door; state.StageFrom = state.Position;
        }
        if (p.Door == Doorway.None && (state.SteppingDown <= 0 || swinging)) {
            state.SteppingDown = 0;
            return null;
        }
        if (world().Find(p.DoorCabin) is not Thing cabin) return null;
        var door = Doorways.DoorPoint(cabin);
        var inside = door + DoorwayPath.IntoCabin.Rotated(cabin.Rotation);
        if (p.Door == Doorway.None && state.SteppingDown > 0) {
            // Done knocking, the player steps back down off the step to where they stand, still facing the door.
            state.SteppingDown = Math.Max(0, state.SteppingDown - dt);
            var down = state.StageFrom.Lerp(p.Pos, Mathf.SmoothStep(0, 1, 1 -
                state.SteppingDown / DoorwayPath.StepUpSeconds));
            return new DoorShown(down, inside, ActorAction.Walk, door, true, 0, 0, 0, cabin.Rotation);
        }
        if (p.Door is Doorway.Tugged or Doorway.TuggedOff) {
            // The wolf and the player face the door as it holds the player's waist and the pack takes their sides.
            state.Yaw = ActorAnimation.Yaw(inside - door, state.Yaw);
            state.Turning.Hold(state.Yaw);
        }
        var body = DoorwayPath.UsesBody(p.Door) ? p.DoorBody : world().DoorTug.BodyAt(p);
        var shown = FacingAt(p.Door, body, door, inside, p.DoorFrom, p.DoorTime, p.DoorRush, cabin.Rotation);
        float twist = 0, grip = 0;
        // Waiting aside, the right hand starts up toward the door frame as the one coming out nears the doorway; squeezing in
        // past them, it goes on to the frame.
        if (p.Door == Doorway.Aside && LeftToGo(p) is float left) grip = AsideRaise * Mathf.SmoothStep(RaiseLead, 0, left);
        if (p.Door == Doorway.Entering && state.StageBefore == Doorway.Aside)
            grip = Mathf.Lerp(AsideRaise, 1, Mathf.SmoothStep(0, RaiseSeconds, p.DoorTime));
        var (squeeze, way, tuck) = p.Door is Doorway.Entering or Doorway.Leaving ? SqueezePast(p, shown.At, cabin.Rotation) : default;
        if (squeeze > 0) {
            // Turned sideways to squeeze past someone coming the other way through the door, still stepping on.
            // The one going in keeps facing the way they walk and only turns the shoulders a little, keeping the door frame ahead
            // of the right shoulder for the hand; the one coming out turns sideways.
            if (p.Door == Doorway.Leaving) {
                var ahead = (shown.At - state.Position).LengthSquared() > 1e-8f ? (shown.At - state.Position).Normalized() : (shown.Facing - shown.At).Normalized();
                shown.Facing = shown.At + ahead.Rotated(-way * Mathf.DegToRad(SqueezeTurn) * squeeze);
                shown.Backing = true;
            }
            twist = way * (p.Door == Doorway.Entering ? EnterTwist : SqueezeTwist) * squeeze;
        }
        return new DoorShown(shown.At, shown.Facing, shown.Action, door, shown.Backing, twist, grip, tuck, cabin.Rotation);
    }

    /// <summary>Places a doorway pose at its time for the close-up review and along a tug out of the cabin.</summary>
    public static (Vector2 At, Vector2 Facing, ActorAction Action, bool Backing) DoorStep(Doorway stage, Vector2 door,
        Vector2 from, Vector2 start, float time, float seconds, bool rush, float reach, float rotation = 0) {
        var inward = DoorwayPath.IntoCabin.Rotated(rotation);
        var at = stage is Doorway.Tugged or Doorway.TuggedOff
            ? DoorTug.AtTime(start, from - inward.Normalized() * reach, time)
            : DoorwayPath.At(stage, door, from, start, time, seconds, rush, rotation);
        return FacingAt(stage, at, door, door + inward, start, time, rush, rotation);
    }

    private static (Vector2 At, Vector2 Facing, ActorAction Action, bool Backing) FacingAt(Doorway stage, Vector2 at,
        Vector2 door, Vector2 inside, Vector2 start, float time, bool rush, float rotation) => stage switch {
            Doorway.Entering => (at, inside, rush ? ActorAction.Jog : ActorAction.Walk, DoorwayPath.FromTheSide(start,
                door, rotation)),
            Doorway.Leaving => (at, 2 * door - inside, ActorAction.Walk, false),
            Doorway.Knocking => (at, inside, time >= Doorways.KnockLead ? ActorAction.Knock :
                DoorwayPath.KnockStep(time) > 0 ? ActorAction.Walk : ActorAction.Idle, false),
            Doorway.Following => (at, inside, ActorAction.Walk, false),
            Doorway.Aside => (at, inside, time < DoorwayPath.AsideSeconds ? ActorAction.Walk : ActorAction.Idle, true),
            _ => (at, at + inside - door, ActorAction.Tugged, true),
        };

    /// <summary>
    /// Where a hand flat on the right door jamb's inner face is from a cabin's door point, to the side, in and up, as the
    /// cabin model builds the jamb.
    /// </summary>
    private const float JambOut = .38f, JambIn = .97f, JambUp = 1.45f;
    /// <summary>
    /// How far toward the door frame a player waiting aside has raised the right hand by the time they step off, how many
    /// seconds before that they start, and how many seconds after it the hand reaches the frame.
    /// </summary>
    private const float AsideRaise = .65f, RaiseLead = .45f, RaiseSeconds = .2f;
    /// <summary>
    /// How far ahead of the right shoulder, along the chest, the door frame is when a player squeezing in lets go of it, and
    /// has let go: any nearer and the arm would have to swing its elbow up to keep the palm on it.
    /// </summary>
    private const float ReleaseAt = .3f, Released = 0;
    /// <summary>Where, in the trunk's frame from the hips, the right hand waits raised in front of its shoulder before it takes the door frame.</summary>
    private static readonly Vector3 ReadyHand = new(-ActorRig.ShoulderOut - .03f, ActorRig.ShoulderUp - .08f, .32f);
    /// <summary>How far to the body's right, in metres from its middle, the door frame is when the hand starts going to it, and when it can go all the way.</summary>
    private const float ReachableFrom = .05f, ReachableAt = .17f;
    /// <summary>How far above the shoulder a hand reaching for the door frame goes at most.</summary>
    private const float AboveShoulder = .1f;
    /// <summary>Seconds until someone stepping out of the cabin a player stands aside at is through its doorway, or null with nobody stepping out.</summary>
    private float? LeftToGo(PlayerState aside) {
        foreach (var p in world().Players)
            if (p.DoorCabin == aside.DoorCabin && p.Door == Doorway.Leaving) return Math.Max(0, Doorways.ThroughSeconds - p.DoorTime);
        return null;
    }

    /// <summary>
    /// How much a player stepping into or out of a cabin squeezes past someone going the other way through its door, 0 to 1,
    /// by how near they are, which way they turn their chest, positive to their left, and how far they have drawn their arms in, 0 to 1.
    /// </summary>
    private (float Squeeze, float Way, float Tuck) SqueezePast(PlayerState passing, Vector2 at, float rotation) {
        var other = passing.Door == Doorway.Entering ? Doorway.Leaving : Doorway.Entering;
        foreach (var p in world().Players)
            if (p != passing && p.DoorCabin == passing.DoorCabin && p.Door == other && stateOf(p.Id) is ActorState drawn) {
                var them = drawn.Position;
                // The two turn chest to chest; the one going in leads with the right shoulder toward the door frame.
                // Facing into the cabin the way aside is to the right, so the one there turns left toward someone on the left; facing out, the other way.
                float side = (at - them).Dot(Doorways.AsideWay.Rotated(rotation)) >= 0 ? 1 : -1;
                float apart = at.DistanceTo(them);
                return (Mathf.SmoothStep(SqueezeFrom, SqueezeAt, apart), passing.Door == Doorway.Entering ? side : -side, Mathf.SmoothStep(TuckFrom, SqueezeFrom, apart));
            }
        return (0, 0, 0);
    }

    /// <summary>How near, in metres, two players passing in a cabin's door are when they start drawing their arms in.</summary>
    private const float TuckFrom = 2.4f;
    /// <summary>How near, in metres, two players passing in a cabin's door are when they start turning sideways, and are fully turned.</summary>
    private const float SqueezeFrom = 1.6f, SqueezeAt = .9f;
    /// <summary>How far a player coming out past someone turns their body and, further, their shoulders, and how far the one going in turns only their shoulders, in degrees.</summary>
    private const float SqueezeTurn = 30, SqueezeTwist = 45, EnterTwist = 20;
    /// <summary>The arms of a player passing someone in a doorway: hanging at the sides, the forearms a little forward so a tool in hand clears the ground.</summary>
    private const float TuckRaise = 0, TuckOut = 3, TuckElbow = 35;
    /// <summary>The player stepping in or out through a cabin's door, or null.</summary>
    public PlayerState? Passing(int cabin) {
        foreach (var p in world().Players) if (p.DoorCabin == cabin && p.Door is Doorway.Entering or Doorway.Tugged or Doorway.Leaving) return p;
        return null;
    }

    /// <summary>Stands a cabin's door <paramref name="degrees"/> open, as a wolf pushing it holds it, for the door to swing on from there.</summary>
    public static void PushDoor(HutProp hut, float degrees) {
        hut.Open = SwungShare(degrees); hut.Slamming = false;
        hut.Door.RotationDegrees = new Vector3(0, -degrees, 0);
    }

    /// <summary>The share of an eased swing that stands a door <paramref name="degrees"/> open.</summary>
    private static float SwungShare(float degrees) {
        // The door's swing eases in and out, so the share is found by halving.
        float low = 0, high = 1;
        for (int i = 0; i < 12; i++) {
            float middle = (low + high) / 2;
            if (FullSwing * WorldView.Ease(middle) < degrees) low = middle; else high = middle;
        }
        return (low + high) / 2;
    }

    /// <summary>How many degrees a cabin's door swings open.</summary>
    private const float FullSwing = 100;
    /// <summary>Seconds a door slammed from wide open takes to hit its frame.</summary>
    public const float SlamSeconds = 1 / 8f;

    /// <summary>How far open a door stands, from 0 shut to 1 wide open, at a share of its slam: starting at rest, it hits the frame at its fastest.</summary>
    private static float Slammed(float share) => share * (2 - share);

    /// <summary>
    /// Swings a cabin's door further open, or shut, over <paramref name="seconds"/> for the whole swing; a door that
    /// <paramref name="slams"/> shut speeds up all the way into its frame, and any other swing eases in and out.
    /// </summary>
    public static void SwingDoor(HutProp hut, bool open, float dt, float seconds, bool slams) {
        slams &= !open;
        if (slams != hut.Slamming) {
            // Changing between a slam and a swing carries on from where the door stands.
            float shown = hut.Slamming ? Slammed(hut.Open) : WorldView.Ease(hut.Open);
            hut.Open = slams ? 1 - MathF.Sqrt(1 - shown) : SwungShare(shown * FullSwing);
            hut.Slamming = slams;
        }
        hut.Open = Mathf.MoveToward(hut.Open, open ? 1 : 0, dt / seconds);
        hut.Door.RotationDegrees = new Vector3(0, -FullSwing * (slams ? Slammed(hut.Open) : WorldView.Ease(hut.Open)), 0);
    }

    /// <summary>
    /// A player's pose at a cabin's door as <paramref name="at"/> shows them, for a body drawn as <paramref name="state"/>
    /// has it: shoulders turned, arms drawn in to pass someone and boots stood on the steps, which carry the body up and down
    /// over <paramref name="dt"/> seconds in its <see cref="ActorState.Lift"/>; with where on the door frame the right hand
    /// goes, when it does.
    /// </summary>
    public static (Pose Pose, Vector3? Jamb) StandAt(Pose pose, ActorState state, DoorShown at, float dt) {
        pose = pose with { Twist = pose.Twist + at.Twist };
        // Passing someone in a doorway, the arms hang at the sides, a tool held low and pointing ahead, instead of swinging out.
        if (at.Tuck > 0) pose = ArmReach.WithArm(ArmReach.WithArm(pose, true, TuckRaise, TuckOut, TuckElbow, at.Tuck), false, TuckRaise, TuckOut, TuckElbow, at.Tuck);
        // The way up the steps, in the body's frame: X toward its left and Y ahead.
        var inward = DoorwayPath.IntoCabin.Normalized().Rotated(at.Rotation);
        var up = inward.Rotated(state.Yaw);
        float depth = (state.Position - at.Door).Dot(inward);
        pose = StairSteps.OnSteps(pose, depth, up, at.Backing, StairSteps.StepEdges, StairSteps.StepRise, ref state.Lift, ref state.LiftSpeed, dt);
        Vector3? jamb = at.Grip > 0 ? WorldView.To3(at.Door + Doorways.AsideWay.Rotated(at.Rotation) * JambOut + inward * JambIn, JambUp) : null;
        return (pose, jamb);
    }

    /// <summary>
    /// Moves the right hand of a rig shown as <paramref name="pose"/> toward the door <paramref name="frame"/>, <paramref name="grip"/>
    /// of the way, 0 to 1, as a player squeezing in past someone puts their palm on it.
    /// </summary>
    public static void HandOnFrame(ActorRig rig, Pose pose, Vector3 frame, float grip, string? tool, bool torch) {
        // The right hand rises from where the walk swings it to in front of the right shoulder, and goes on to the door frame
        // only once the frame is to that shoulder's right, so the arm never crosses the body; it lets go while the frame is
        // still ahead of the shoulder, so the elbow stays down.
        var root = rig.Root.Transform;
        var swung = root * rig.Fist(false);
        var trunk = ArmReach.TrunkBasis(pose);
        var local = trunk.Inverse() * (root.AffineInverse() * frame - Vector3.Up * pose.HipHeight);
        float hold = Mathf.SmoothStep(0, 1, grip) * Mathf.SmoothStep(Released, ReleaseAt, local.Z);
        var ready = root * (Vector3.Up * pose.HipHeight + trunk * ReadyHand);
        float rightward = Mathf.SmoothStep(ReachableFrom, ReachableAt, -local.X);
        var aim = swung.Lerp(ready, Math.Min(1, hold / AsideRaise)).Lerp(frame, Math.Max(0, hold - AsideRaise) / (1 - AsideRaise) * rightward);
        // While the step still keeps them low, the hand goes no higher than just over the shoulder, and the climb brings it up to the frame.
        float shoulder = ArmReach.Shoulder(pose, root, false).Y;
        if (hold > 0) rig.Show(ArmReach.Reaching(pose, aim with { Y = Math.Min(aim.Y, shoulder + AboveShoulder) }, root).Pose, tool, false, false, torch);
    }
}
