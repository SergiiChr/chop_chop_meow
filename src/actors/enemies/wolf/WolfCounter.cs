using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The phases of the repositioning a wolf attempts after its first weapon hit.</summary>
public enum WolfCounterStage { None, Wince, SideHop, Slide, Run, Bound, Pivot, Face }

/// <summary>Moves a struck wolf around the attacker's flank before it can bite again.</summary>
public sealed class WolfCounter(World world) {
    private const float HopAt = .12f, SlideAt = .43f, RunAt = .67f, BoundAt = 1.43f, PivotAt = 1.87f,
        FaceAt = 2.23f, DoneAt = 2.46f;
    private const float SampleSeconds = .035f, BodyClearance = .64f, ForepawReach = .39f;
    private static readonly Vector2 SideHopStep = new(.40f, 1.05f);
    internal static readonly float SideHopBearing = SideHopStep.Angle();
    private static readonly Vector2 Launch = new(.55f, -2.48f), Land = new(-2.15f, -1.78f);
    private static readonly Vector2 BoundDirection = (Land - Launch).Normalized();
    private static readonly Vector2 Forepaws = Land + BoundDirection * ForepawReach;
    private static readonly float LandHeading = BoundDirection.Angle(), TurnHeading = (-Forepaws).Angle();

    public static bool Active(Wolf wolf) => wolf.CounterSide != 0;

    /// <summary>Selects the first clear route around the bearing the wolf was struck from.</summary>
    internal bool Start(Wolf wolf, Vector2 mark) {
        var offset = wolf.Pos - mark;
        if (offset.LengthSquared() is < .15f or > 9) return false;
        wolf.CounterMarkX = mark.X; wolf.CounterMarkY = mark.Y;
        wolf.CounterBearing = offset.Angle(); wolf.CounterRadius = offset.Length();
        // Wolves try opposite sides when their ids alternate.
        wolf.CounterSide = (wolf.Id & 1) == 0 ? 1 : -1;
        if (!HasRoom(wolf)) {
            wolf.CounterSide = -wolf.CounterSide;
            if (!HasRoom(wolf)) { Clear(wolf); return false; }
        }
        wolf.Heading = (-offset).Angle();
        wolf.Stagger = World.StaggerSeconds;
        wolf.Chase = 0;
        return true;
    }

    /// <summary>Checks the ground and makes sure the first hop has room beside any packmates.</summary>
    private bool HasRoom(Wolf wolf) {
        var from = wolf.Pos;
        for (float time = SampleSeconds; time < DoneAt + SampleSeconds; time += SampleSeconds) {
            var (to, heading) = Sample(wolf, Math.Min(time, DoneAt));
            if (!ClearGround(from, to) || time < RunAt && PackInWay(wolf, from, to, heading)) return false;
            from = to;
        }
        return true;
    }

    /// <summary>Steps along the chosen path, leaving the wolf planted if the route closes up.</summary>
    internal void Tick(Wolf wolf, float dt) {
        float end = Math.Min(DoneAt, wolf.CounterTime + dt);
        while (wolf.CounterTime < end) {
            float next = Math.Min(end, wolf.CounterTime + SampleSeconds);
            var (at, heading) = Sample(wolf, next);
            if (at != wolf.Pos && (!ClearGround(wolf.Pos, at) || Crowded(wolf, at, heading))) {
                Clear(wolf); wolf.Stagger = 0;
                wolf.Threaten = FaceAt < next ? .12f : .25f;
                var backToPlayer = world.Player - wolf.Pos;
                if (backToPlayer != Vector2.Zero) wolf.Heading = backToPlayer.Angle();
                return;
            }
            if (wolf.CounterTime < FaceAt && next >= FaceAt) {
                wolf.Growled = wolf.Sprinted = true;
                world.Play("wolf_growl_approaching", at);
            }
            // The landing is committed. Once grounded the wolf can turn to a player who moved.
            var toward = world.Player - at;
            wolf.Heading = next <= FaceAt || toward == Vector2.Zero ? heading :
                Mathf.RotateToward(wolf.Heading, toward.Angle(), 16 * (next - wolf.CounterTime));
            wolf.Pos = at;
            wolf.CounterTime = next;
            wolf.Stagger = Math.Max(0, World.StaggerSeconds - next);
        }
        if (end >= DoneAt) Clear(wolf);
    }

    private bool ClearGround(Vector2 from, Vector2 to) =>
        (!world.Protected(to) || world.FireLighting(world.Player) is Thing fire && world.FullPackAt(fire.Pos)) &&
        world.WolfSnap.ClearTo(from, to) == to;

    /// <summary>A clear path may close if a player or another wolf reaches it.</summary>
    private bool Crowded(Wolf wolf, Vector2 to, float heading) {
        foreach (var p in world.S.Players)
            if (p.RespawnIn <= 0 && !Doorways.Indoors(p) && Approaches(wolf.Pos, to, p.Pos)) return true;
        return PackInWay(wolf, wolf.Pos, to, heading);
    }

    private bool PackInWay(Wolf wolf, Vector2 from, Vector2 to, float heading) {
        const float width = 2 * WolfBodies.WolfHalfWidth;
        const float far = 2 * WolfBodies.WolfNose + width;
        foreach (var other in world.S.Wolves) {
            if (other == wolf || other.Pos.DistanceSquaredTo(to) >= far * far) continue;
            float gap = WolfBodies.DistanceBetween(to, heading, other);
            if (gap < width * width && (gap < WolfBodies.DistanceBetween(from, wolf.Heading, other) ||
                to.DistanceSquaredTo(other.Pos) < from.DistanceSquaredTo(other.Pos))) return true;
        }
        return false;
    }

    private static bool Approaches(Vector2 from, Vector2 to, Vector2 occupied) {
        float distance = to.DistanceSquaredTo(occupied);
        return distance < BodyClearance * BodyClearance && distance < from.DistanceSquaredTo(occupied);
    }

    internal static void Clear(Wolf wolf) {
        wolf.CounterSide = 0;
        wolf.CounterTime = wolf.CounterMarkX = wolf.CounterMarkY = wolf.CounterBearing = wolf.CounterRadius = 0;
    }

    /// <summary>An active path has a finite, bounded clock and an anchor inside the map.</summary>
    internal static bool ValidSavedPath(Wolf? wolf) => wolf != null && (!Active(wolf) ||
        wolf.CounterSide is 1 or -1 && wolf.CounterTime is >= 0 and < DoneAt &&
        wolf.CounterMarkX is >= 0 and < Terrain.Size && wolf.CounterMarkY is >= 0 and < Terrain.Size &&
        wolf.CounterRadius is > 0 and <= 3 && wolf.CounterBearing is >= -MathF.PI and <= MathF.PI);

    public static (WolfCounterStage Stage, float Progress) Pose(Wolf wolf) =>
        Active(wolf) ? Phase(wolf.CounterTime) : default;

    private static (WolfCounterStage Stage, float Progress) Phase(float time) =>
        time < HopAt ? (WolfCounterStage.Wince, time / HopAt) :
        time < SlideAt ? (WolfCounterStage.SideHop, (time - HopAt) / (SlideAt - HopAt)) :
        time < RunAt ? (WolfCounterStage.Slide, (time - SlideAt) / (RunAt - SlideAt)) :
        time < BoundAt ? (WolfCounterStage.Run, (time - RunAt) / (BoundAt - RunAt)) :
        time < PivotAt ? (WolfCounterStage.Bound, (time - BoundAt) / (PivotAt - BoundAt)) :
        time < FaceAt ? (WolfCounterStage.Pivot, (time - PivotAt) / (FaceAt - PivotAt)) :
        (WolfCounterStage.Face, (time - FaceAt) / (DoneAt - FaceAt));

    /// <summary>How far above its standing height the body rises in each of the two non-attacking hops.</summary>
    public static float Height(Wolf wolf) {
        var (stage, progress) = Pose(wolf);
        float seconds = stage == WolfCounterStage.SideHop ? SlideAt - HopAt :
            stage == WolfCounterStage.Bound ? PivotAt - BoundAt : 0;
        return Movement.Rise(progress * seconds, seconds);
    }

    private static (Vector2 At, float Heading) Sample(Wolf wolf, float time) {
        var (stage, u) = Phase(time);
        float radius = wolf.CounterRadius;
        var start = new Vector2(radius, 0);
        var hop = start + SideHopStep;
        var slide = new Vector2(radius + .56f, 1.44f);
        var startRun = new Vector2(radius + 1.40f, 2.12f);
        var endRun = new Vector2(radius + 1.15f, -2.70f);
        float sideHeading = SideHopBearing;
        Vector2 at;
        float heading;
        switch (stage) {
            case WolfCounterStage.Wince:
                at = start; heading = Mathf.Pi;
                break;
            case WolfCounterStage.SideHop:
                at = start.Lerp(hop, u);
                heading = Mathf.LerpAngle(Mathf.Pi, sideHeading, Mathf.SmoothStep(0, 1, u));
                break;
            case WolfCounterStage.Slide:
                at = hop.Lerp(slide, u * (2 - u)); heading = sideHeading;
                break;
            case WolfCounterStage.Run:
                float run = u * (.55f + .45f * u);
                at = slide.BezierInterpolate(startRun, endRun, Launch, run);
                var tangent = slide.BezierDerivative(startRun, endRun, Launch, run);
                heading = Mathf.LerpAngle(tangent.Angle(), LandHeading, Mathf.SmoothStep(.7f, 1, u));
                break;
            case WolfCounterStage.Bound:
                at = Launch.Lerp(Land, u); heading = LandHeading;
                break;
            default:
                // The forepaw midpoint stays fixed while the body swings around it.
                heading = stage == WolfCounterStage.Face ? TurnHeading
                    : Mathf.LerpAngle(LandHeading, TurnHeading, u * (2 - u));
                at = Forepaws - Vector2.FromAngle(heading) * ForepawReach;
                break;
        }
        var anchor = new Vector2(wolf.CounterMarkX, wolf.CounterMarkY);
        return (anchor + new Vector2(at.X, at.Y * wolf.CounterSide).Rotated(wolf.CounterBearing),
            wolf.CounterBearing + heading * wolf.CounterSide);
    }
}
