using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The ground path a player takes through a cabin doorway, shared by combat and the view.</summary>
internal static class DoorwayPath {
    /// <summary>
    /// How far in from a cabin's door point a player stepping in walks, out of sight behind the walls.
    /// </summary>
    public static readonly Vector2 IntoCabin = new(0, -1.8f);
    /// <summary>
    /// Where a player knocking on a cabin's door stands, from its door point: on the lower tread of the step, close
    /// enough to reach the boards.
    /// </summary>
    private static readonly Vector2 KnockSpot = new(0, -.5f);
    /// <summary>
    /// Seconds a player left outside a shut door takes to step up to it, ending as the first knock lands, and to step
    /// back down after knocking.
    /// </summary>
    public const float StepUpSeconds = .3f;

    public static bool UsesBody(Doorway stage) =>
        stage is Doorway.Entering or Doorway.Leaving or Doorway.Knocking or Doorway.Following or Doorway.Aside;

    public static Vector2 At(Doorway stage, Vector2 door, Vector2 from, Vector2 start, float time, float seconds,
        bool rush, float rotation) {
        var inward = IntoCabin.Rotated(rotation);
        var aside = Doorways.AsideWay.Rotated(rotation);
        float done = Math.Clamp(time / Math.Max(seconds, .01f), 0, 1);
        return stage switch {
            Doorway.Entering => Inward(start, door, inward, aside, rush ? done : WalkedIn(time)),
            Doorway.Leaving => Outward(door, from, inward, aside, done),
            Doorway.Knocking => start.Lerp(door + KnockSpot.Rotated(rotation), KnockStep(time)),
            Doorway.Aside => start.Lerp(from, Mathf.SmoothStep(0, 1, time / AsideSeconds)),
            _ => from,
        };
    }

    public const float AsideSeconds = .5f;
    private const float FollowSpeed = 1.4f;

    /// <summary>
    /// A follower waits a stride behind the person ahead and steps aside if a wolf pulls them back out.
    /// </summary>
    public static Vector2 Follow(PlayerState follower, ListView<PlayerState> players, Thing cabin, float dt) {
        var door = Doorways.DoorPoint(cabin);
        var spot = door;
        foreach (var p in players) {
            if (p == follower || p.DoorCabin != cabin.Id) continue;
            if (p.Door == Doorway.Tugged) {
                var aside = Doorways.AsideWay.Rotated(cabin.Rotation);
                float side = (follower.DoorBody - door).Dot(aside) < 0 ? -1 : 1;
                spot = door + aside * (side * Doorways.AsideStep);
                break;
            }
            if (p.Door != Doorway.Entering) continue;
            var ahead = door - p.DoorBody;
            spot = door + ahead.Normalized() * Math.Max(0, .7f - ahead.Length());
        }
        return follower.DoorBody.MoveToward(spot, dt * FollowSpeed);
    }

    /// <summary>The open step outside the door, including the part within the hut's walking blocker.</summary>
    public static bool OnSteps(Thing cabin, Vector2 at) {
        var local = (at - Doorways.DoorPoint(cabin)).Rotated(-cabin.Rotation);
        return local.Y >= -StairSteps.DoorDepth && Math.Abs(local.X) <= StairSteps.StepHalfWidth;
    }

    public static bool FromTheSide(Vector2 from, Vector2 door, float rotation) =>
        Math.Abs((from - door).Dot(Doorways.AsideWay.Rotated(rotation))) > SideLane;

    /// <summary>How far a player has stepped up toward the door before the first knock.</summary>
    public static float KnockStep(float time) =>
        Mathf.SmoothStep(0, 1, (time - Doorways.KnockLead + StepUpSeconds) / StepUpSeconds);

    /// <summary>
    /// Where a player stands <paramref name="done"/> of the way in from <paramref name="start"/>.
    /// Someone starting beside the step comes round its front and goes up one side to leave room for the leaver.
    /// </summary>
    private static Vector2 Inward(Vector2 start, Vector2 door, Vector2 inward, Vector2 aside, float done) {
        var inside = door + inward;
        float across = (start - door).Dot(aside);
        if (Math.Abs(across) <= SideLane) return start.Lerp(inside, done);
        var side = aside * (Math.Sign(across) * SideLane);
        var front = door + side + inward.Normalized() * .25f;
        var tread = door + side + inward.Normalized() * StairSteps.StepEdges[1];
        float first = start.DistanceTo(front), second = front.DistanceTo(tread), last = tread.DistanceTo(inside),
            along = done * (first + second + last);
        return along < first ? start.Lerp(front, along / first)
            : along < first + second ? front.Lerp(tread, (along - first) / second)
            : tread.Lerp(inside, Math.Min(1, (along - first - second) / last));
    }

    /// <summary>
    /// How far to the side of the door point a player stepping in past someone walks up the step, which is a metre
    /// wide, keeping their boots on it.
    /// </summary>
    private const float SideLane = .22f;
    /// <summary>
    /// Where a player stands <paramref name="done"/> of the way out to <paramref name="end"/>.
    /// If they stop to one side, they follow that side of the step down before walking across.
    /// </summary>
    private static Vector2 Outward(Vector2 door, Vector2 end, Vector2 inward, Vector2 aside, float done) {
        var inside = door + inward;
        float offside = (end - door).Dot(aside);
        if (Math.Abs(offside) <= .05f) return inside.Lerp(end, done);
        // Down the step inside its width, then across to where they stop.
        var side = door + aside * Math.Clamp(offside, -SideLane, SideLane)
            + inward.Normalized() * StairSteps.StepEdges[1];
        float first = inside.DistanceTo(side), along = done * (first + side.DistanceTo(end));
        return along < first ? inside.Lerp(side, along / first)
            : side.Lerp(end, (along - first) / side.DistanceTo(end));
    }

    /// <summary>
    /// How far through a walk into a cabin a player is after <paramref name="time"/> seconds, from 0 to 1.
    /// They step onto the first tread at walking pace, then speed up to finish within
    /// <see cref="Doorways.EnterSeconds"/>.
    /// </summary>
    private static float WalkedIn(float time) {
        float slowTime = HurryFrom + HurrySeconds / 2;
        float fastTime = Doorways.EnterSeconds - HurryFrom - HurrySeconds / 2;
        float fast = (1 - StepInPace * slowTime) / fastTime;
        float ramp = Math.Clamp(time - HurryFrom, 0, HurrySeconds);
        float walked = StepInPace * Math.Min(time, HurryFrom) + StepInPace * ramp
            + (fast - StepInPace) * ramp * ramp / (2 * HurrySeconds);
        return Math.Min(1, walked + fast * Math.Max(0, time - HurryFrom - HurrySeconds));
    }
    private const float StepInPace = 1 / 1.5f, HurryFrom = .45f, HurrySeconds = .15f;
}
