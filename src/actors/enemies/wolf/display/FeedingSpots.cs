using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Where wolves stand round a fallen player: feeding ones side-on to the body at the part nearest them, clear of each other
/// and of the player's arms, and a pinning one's forepaws on the chest. <paramref name="wolves"/> are the wolves drawn.
/// </summary>
public sealed class FeedingSpots(IReadOnlyDictionary<Wolf, WolfModel> wolves) {
    /// <summary>Where a player lying on their back as <paramref name="fallen"/> is drawn has their feet, and the way from there to their throat.</summary>
    public static (Vector2 Feet, Vector2 Length) BodyLine(ActorState fallen) {
        var forward = ActorAnimation.Forward(fallen.Yaw);
        var feet = fallen.Position + forward * FallBackView.KnockDownSlide;
        var throat = FallBackView.Throat(fallen.Position, forward, float.MaxValue);
        return (feet, new Vector2(throat.X, throat.Z) - feet);
    }

    /// <summary>
    /// Where a feeding wolf stands, and which way it faces, to bite a lying player's <paramref name="body"/>
    /// at <paramref name="along"/> of the way from their feet to their throat, from their <see cref="BodySide"/>
    /// <paramref name="side"/>, standing perpendicular to the body.
    /// </summary>
    public static (Vector3 At, float Yaw) OnTheBody((Vector2 Feet, Vector2 Length) body, float along, int side) {
        var near = body.Feet + body.Length * along;
        // A player on their back fell from facing the way their feet now point.
        var outward = BodySide.Out(-body.Length, side);
        // The legs are lower and narrower than the trunk.
        bool legs = along < LegsEnd;
        float yaw = ActorAnimation.Yaw(-outward, 0);
        var at = WorldView.To3(near + outward * (legs ? LegSide : TrunkSide), legs ? LegTop : TrunkTop) - new Basis(Vector3.Up, yaw) * WolfModel.FeedingBite;
        return (at with { Y = 0 }, yaw);
    }

    /// <summary>
    /// How far out from the middle of a body lying on its back, and how high, the side of its trunk and of its legs are, where a
    /// feeding wolf's jaws close; the share of the way from the feet to the throat where the legs end; and seconds a feeding wolf
    /// takes to ease onto its spot.
    /// </summary>
    private const float TrunkSide = .17f, TrunkTop = .2f, LegSide = .15f, LegTop = .1f, LegsEnd = .45f, FeedEasing = .4f;
    /// <summary>
    /// Where a wolf's left and right forepaws stand on a player's chest, in the wolf's model as last placed: on the front of the
    /// chest either side of the breastbone, a paw's thickness off it.
    /// </summary>
    public static (Vector3 Left, Vector3 Right)? PawsOnChest(WolfModel model, ActorRig? rig) {
        if (rig == null) return null;
        var chest = rig.Torso.GlobalTransform;
        var toModel = model.Root.GlobalTransform.AffineInverse();
        return WolfModel.Uncrossed(toModel * (chest * ChestSpot), toModel * (chest * (ChestSpot with { X = -ChestSpot.X })));
    }

    /// <summary>Where on the trunk, from the hips, a forepaw on the chest stands: beside the breastbone, inside the player's forearms pushing at the jaw, out from the shirt by a paw.</summary>
    private static readonly Vector3 ChestSpot = new(.05f, .3f, .18f);
    /// <summary>
    /// The spot a feeding wolf takes at a lying player: perpendicular to the body at the part nearest to where the wolf is
    /// drawn, preferring its own side. It keeps its last spot while its whole pull clears the player's arms,
    /// the other wolves as drawn and the space each feeding neighbor needs through its pull.
    /// With no free spot it keeps its last one.
    /// </summary>
    private (Vector3 At, float Yaw, int Along, int Side) FreeSpot(Wolf wolf, WolfModel model, ActorState lying, ActorRig rig) {
        FillObstacles(wolf, rig);
        var body = BodyLine(lying);
        if (model.FeedSide != 0) {
            var (at, yaw) = OnTheBody(body, model.FeedAlong / (float)FeedSpots, model.FeedSide);
            if (Free(at, yaw)) return (at, yaw, model.FeedAlong, model.FeedSide);
        }
        Span<(float Away, int Along, int Side)> spots = stackalloc (float, int, int)[2 * (FeedSpots + 1)];
        var drawn = new Vector2(model.Root.Position.X, model.Root.Position.Z);
        int near = BodySide.Of(body.Feet, -body.Length, drawn), count = 0;
        for (int i = 0; i <= FeedSpots; i++)
            foreach (int side in (ReadOnlySpan<int>)[near, -near]) {
                var (at, _) = OnTheBody(body, i / (float)FeedSpots, side);
                // A spot across the body counts as that much further off, so a wolf goes round only when its own side is full.
                spots[count++] = (new Vector2(at.X, at.Z).DistanceTo(drawn) + (side == near ? 0 : AcrossTheBody), i, side);
            }
        spots.Sort();
        foreach (var (_, along, side) in spots) {
            var (at, yaw) = OnTheBody(body, along / (float)FeedSpots, side);
            if (Free(at, yaw)) return (at, yaw, along, side);
        }
        int lastSide = model.FeedSide == 0 ? 1 : model.FeedSide;
        var (last, lastYaw) = OnTheBody(body, model.FeedAlong / (float)FeedSpots, lastSide);
        return (last, lastYaw, model.FeedAlong, lastSide);
    }

    /// <summary>The wolves still in the world this frame; a dead one's model stays drawn a while after.</summary>
    private readonly HashSet<Wolf> liveWolves = new();
    /// <summary>The lying player's arms, every other wolf as drawn and each feeding neighbor's whole pull, as spheres in the world.</summary>
    private readonly List<(Vector3 At, float Radius)> obstacles = new();
    /// <summary>Fills <see cref="obstacles"/> for <paramref name="feeder"/> at the player drawn by <paramref name="rig"/>, once for all the spots it weighs.</summary>
    private void FillObstacles(Wolf feeder, ActorRig rig) {
        obstacles.Clear();
        Span<(Vector3 At, float Radius)> theirs = stackalloc (Vector3, float)[WolfModel.VolumeCount];
        foreach (var (wolf, model) in wolves) {
            if (wolf == feeder || !liveWolves.Contains(wolf)) continue;
            model.Volumes(theirs);
            var other = model.Root.Transform;
            foreach (var (at, radius) in theirs) obstacles.Add((other * at, radius));
            // A wolf lowering its head can still stand outside the space it needs for its feeding pulls.
            if (model.Motion == WolfMotion.Feed)
                foreach (var (at, radius) in WolfModel.FeedingVolumes) obstacles.Add((other * at, radius));
        }
        var body = rig.Root.Transform;
        for (int arm = 0; arm < 2; arm++) {
            Vector3 fist = body * rig.Fist(arm == 0), elbow = body * rig.Elbow(arm == 0);
            obstacles.Add((fist, ArmRadius)); obstacles.Add((fist.Lerp(elbow, .5f), ArmRadius)); obstacles.Add((elbow, ArmRadius));
        }
    }

    /// <summary>Whether a feeding wolf placed at <paramref name="at"/> facing <paramref name="yaw"/> touches none of the <see cref="obstacles"/>.</summary>
    private bool Free(Vector3 at, float yaw) {
        var place = new Transform3D(new Basis(Vector3.Up, yaw), at);
        foreach (var mine in WolfModel.FeedingVolumes) {
            var centre = place * mine.At;
            foreach (var (them, radius) in obstacles) if (centre.DistanceSquaredTo(them) < (mine.Radius + radius) * (mine.Radius + radius)) return false;
        }
        return true;
    }

    /// <summary>How many spots along a lying body, feet to throat, a feeding wolf chooses among, and how thick an arm is, in metres.</summary>
    private const int FeedSpots = 20;
    private const float ArmRadius = .06f;
    /// <summary>How many metres further off a feeding spot on the far side of the body counts than one on the wolf's own side.</summary>
    private const float AcrossTheBody = 1;

    /// <summary>Where a feeding wolf stands at a lying player, easing there from where it stood.</summary>
    public Vector3 Place(Wolf wolf, WolfModel model, ActorState lying, ActorRig lyingRig, float dt) {
        // A feeding wolf stands perpendicular to the lying body at the part of it nearest to it that leaves it touching neither
        // another wolf nor the player's arms, keeping that spot while it stays free; it eases there from where it stood, and to
        // any new spot it has to move to.
        var spot = FreeSpot(wolf, model, lying, lyingRig);
        if (model.Fed == 0 || spot.Along != model.FeedAlong || spot.Side != model.FeedSide) { model.StepFrom = model.Root.Position; model.FromYaw = model.Yaw; model.Fed = 0; }
        (model.FeedAlong, model.FeedSide) = (spot.Along, spot.Side);
        model.Fed += dt;
        float ease = WorldView.Ease(Math.Min(1, model.Fed / FeedEasing));
        model.Yaw = Mathf.LerpAngle(model.FromYaw, spot.Yaw, ease);
        model.Turning.Hold(model.Yaw); model.Root.Rotation = new Vector3(0, model.Yaw, 0);
        return model.StepFrom.Lerp(spot.At, ease);
    }

    /// <summary>Remembers the wolves still in the world this frame; a dead one's model stays drawn a while after.</summary>
    public void Remember(ListView<Wolf> live) {
        liveWolves.Clear();
        foreach (var wolf in live) liveWolves.Add(wolf);
    }
}
