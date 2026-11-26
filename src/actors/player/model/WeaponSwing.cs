using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>A weapon's grip path, striking surface and timing in the player's frame, facing +Z.</summary>
internal sealed class WeaponSwing(string item, ActorAction action, float duration, float contact, float damage,
    Vector3 edgeStart, Vector3 edgeEnd, float radius, WeaponSwing.Key[] keys, float secondGrip = 0, bool carriesUpright = false) {
    internal readonly record struct Key(float At, Vector3 Grip, Basis Turn, float Lean, float Twist, float Hips, float Lag);
    internal readonly record struct Frame(Vector3 Grip, Basis Turn, float Lean, float Twist, float Hips, float Lag);
    public string Item { get; } = item;
    public ActorAction Action { get; } = action;
    public float Duration { get; } = duration;
    public float Contact { get; } = duration * contact;
    public float Damage { get; } = damage;
    public float ActiveFrom => Contact - Duration * .13f;
    public float ActiveUntil => Contact + Duration * .10f;
    public bool IsBucket => Item == Bucket.Item;
    public bool CarriesUpright { get; } = carriesUpright;

    private static readonly Key[] Pick = [
        Stand(0),
        At(.34f, -.26f, 1.42f, .15f, new(.12f, .93f, -.35f), -9, -30, .81f),
        At(.51f, -.19f, 1.09f, .43f, new(.06f, -.81f, .58f), 24, 3, .78f),
        At(.67f, -.15f, .96f, .41f, new(.1f, -.92f, .38f), 24, 12, .77f),
        Stand(1)
    ];
    private static readonly Key[] Hammer = [
        UprightReady(0),
        At(.34f, -.28f, 1.39f, .17f, new(-.19f, .92f, -.34f), -5, -23, .81f),
        At(.51f, -.11f, .61f, .49f, new(.06f, .25f, .97f), 32, 10, .74f),
        At(.68f, -.11f, .63f, .40f, new(.16f, .30f, .94f), 26, 17, .74f),
        UprightReady(1)
    ];
    private static readonly Key[] Thrust = [
        UprightReady(0),
        At(.29f, -.27f, 1.04f, .14f, new(.05f, .76f, .65f), 5, -25, .80f),
        At(.52f, -.15f, .71f, .47f, new(.06f, .12f, .99f), 33, 18, .75f),
        At(.65f, -.12f, .71f, .46f, new(.11f, .15f, .98f), 30, 20, .75f),
        UprightReady(1)
    ];
    private static readonly Key[] Shovel = [
        Stand(0),
        At(.31f, -.34f, 1.23f, .18f, new(-.55f, .73f, .4f), -5, -30, .81f),
        At(.53f, -.24f, .98f, .26f, new(.27f, -.49f, .83f), 15, 10, .78f),
        At(.72f, -.13f, .89f, .29f, new(.52f, -.58f, .63f), 23, 25, .77f),
        Stand(1)
    ];
    private static readonly Key[] Rake = [
        RakeReady(0),
        At(.33f, -.20f, 1.49f, .14f, new(.19f, .79f, -.58f), -12, -16, .81f),
        At(.43f, -.15f, 1.52f, .21f, new(.14f, .90f, .41f), -2, -8, .80f),
        At(.56f, -.13f, 1.11f, .35f, new(.13f, -.49f, .86f), 24, 8, .77f),
        At(.73f, -.14f, 1.01f, .32f, new(.14f, -.67f, .73f), 27, 14, .76f),
        RakeReady(1)
    ];
    private static readonly Key[] Net = [
        Stand(0),
        At(.34f, -.33f, 1.30f, .16f, new(-.63f, .46f, .59f), 3, -43, .8f),
        At(.54f, -.19f, .95f, .36f, new(.22f, -.45f, .86f), 24, 14, .77f),
        At(.73f, -.1f, .85f, .28f, new(.68f, -.59f, .43f), 26, 42, .76f),
        Stand(1)
    ];
    private static readonly Key[] BucketKeys = [
        Stand(0),
        At(.17f, -.29f, 1.12f, .21f, new(.08f, -.50f, -.86f), -4, -16, .80f, -18),
        At(.34f, -.28f, 1.44f, .12f, new(.08f, .61f, -.79f), -12, -23, .81f, -26),
        At(.43f, -.22f, 1.54f, .21f, new(.06f, .94f, .34f), -5, -12, .80f, -31),
        At(.58f, -.12f, .99f, .50f, new(.06f, -.82f, .57f), 29, 7, .76f, -12),
        At(.75f, -.13f, .83f, .45f, new(.12f, -.95f, .29f), 32, 14, .75f, 36),
        At(.89f, -.20f, .90f, .26f, new(.12f, -.98f, .10f), 15, 3, .78f, -12),
        Stand(1)
    ];

    private static Key UprightReady(float at) =>
        At(at, -.27f, 1.04f, .26f, new(.07f, .98f, .19f), 3, -6, .80f);
    private static Key RakeReady(float at) =>
        At(at, -.24f, 1.02f, .24f, new(.19f, .32f, .93f), 8, -8, .80f);
    private static Key Stand(float at) =>
        At(at, -.25f, .92f, .19f, new(.1f, -.98f, .16f), 8, -8, .8f);
    private static Key At(float at, float x, float y, float z, Vector3 shaft, float lean, float twist, float hips, float lag = 0) {
        // The shaft descends along local -Y. The working face is local -Z and rolls toward the target.
        var up = -shaft.Normalized();
        var face = Math.Abs(up.Z) <= .96f ? Vector3.Back : up.Y < 0 ? Vector3.Down : Vector3.Up;
        var back = -face.Slide(up).Normalized();
        return new(at, new(x, y, z), new Basis(up.Cross(back), up, back), lean, twist, hips, lag);
    }

    private static readonly WeaponSwing[] Swings = [
        new("steel_pick", ActorAction.PickStrike, .87f, .51f, 2.4f, new(0, -.66f, -.29f), new(0, -.66f, -.19f), .035f, Pick, .18f),
        new("steel_shovel", ActorAction.ShovelStrike, .93f, .53f, 1.8f, new(-.07f, -.91f, 0), new(.07f, -.95f, 0), .055f, Shovel, .28f),
        new("steel_hammer", ActorAction.SteelHammerStrike, .81f, .51f, 2.2f, new(0, -.32f, -.095f), new(0, -.32f, -.07f), .046f, Hammer, carriesUpright: true),
        new("wood_hammer", ActorAction.MalletStrike, .88f, .51f, 1.65f, new(0, -.32f, -.075f), new(0, -.32f, -.047f), .06f, Hammer, carriesUpright: true),
        new("stone_chisel", ActorAction.StoneChiselStrike, .68f, .52f, 1.1f, new(0, -.21f, -.02f), new(0, -.21f, .02f), .027f, Thrust, carriesUpright: true),
        new("steel_chisel", ActorAction.SteelChiselStrike, .65f, .52f, 1.4f, new(0, -.256f, -.025f), new(0, -.256f, .025f), .023f, Thrust, carriesUpright: true),
        new("rake", ActorAction.RakeStrike, 1.01f, .56f, 1.4f, RakeStroke.Tines + Vector3.Left * .17f, RakeStroke.Tines + Vector3.Right * .17f, .047f, Rake, .28f),
        new("bug_net", ActorAction.NetStrike, .87f, .54f, .8f, ActorRig.NetMouth + Vector3.Left * .11f, ActorRig.NetMouth + Vector3.Right * .11f, .074f, Net),
        new(Bucket.Item, ActorAction.BucketStrike, 1.23f, .58f, 1.95f, new(0, .085f, 0), new(0, .183f, 0), .118f, BucketKeys)
    ];
    private static readonly Dictionary<ActorAction, WeaponSwing> ByAction = IndexActions();
    private static Dictionary<ActorAction, WeaponSwing> IndexActions() {
        var index = new Dictionary<ActorAction, WeaponSwing>(Swings.Length);
        foreach (var swing in Swings) index.Add(swing.Action, swing);
        return index;
    }

    public static ReadOnlySpan<WeaponSwing> All => Swings;
    public static WeaponSwing? Of(ActorAction action) => ByAction.GetValueOrDefault(action);

    public static WeaponSwing? ForItem(string item) {
        if (Bucket.Is(item)) item = Bucket.Item;
        foreach (var swing in Swings) if (swing.Item == item) return swing;
        return null;
    }

    /// <summary>The pose of the grip and the fighter's weight between two numbered beats.</summary>
    public Frame At(float elapsed) {
        float phase = Math.Clamp(elapsed / Duration, 0, 1);
        int i = 1;
        while (i < keys.Length - 1 && keys[i].At < phase) i++;
        var a = keys[i - 1];
        var b = keys[i];
        float t = (phase - a.At) / (b.At - a.At);
        bool lands = Math.Abs(b.At - contact) < .001f;
        float ease = lands ? 1 - MathF.Cos(t * Mathf.Pi / 2) : Mathf.SmoothStep(0, 1, t);
        var previous = keys[Math.Max(0, i - 2)];
        var next = keys[Math.Min(keys.Length - 1, i + 1)];
        var grip = a.Grip.CubicInterpolateInTime(b.Grip, previous.Grip, next.Grip,
            t, b.At - a.At, previous.At - a.At, next.At - a.At);
        return new(grip, a.Turn.Slerp(b.Turn, ease), Mathf.Lerp(a.Lean, b.Lean, ease), Mathf.Lerp(a.Twist, b.Twist, ease),
            Mathf.Lerp(a.Hips, b.Hips, ease), Mathf.Lerp(a.Lag, b.Lag, ease));
    }

    public Pose PoseAt(float elapsed) {
        var frame = At(elapsed);
        var pose = new Pose(Lean: frame.Lean, Twist: frame.Twist, Hips: frame.Hips, Head: frame.Lean * .25f,
            RaiseL: 26, OutL: 24, ElbowL: 61);
        pose = ReachRightHand(pose, frame.Grip);
        if (secondGrip == 0) return pose;
        var left = ArmReach.InRightHand(pose, Vector3.Zero) + frame.Turn * new Vector3(0, -secondGrip, 0);
        (pose, float roll) = ArmReach.Reaching(pose, left, Transform3D.Identity, left: true, elbowToward: new(1, -1, -.2f));
        return pose with { ArmRollL = roll };
    }

    /// <summary>Places the fist on the ready grip at zero, or follows the swing while the body blends into it.</summary>
    public Pose FollowGrip(Pose pose, float elapsed) => ReachRightHand(pose, At(elapsed).Grip);

    private static Pose ReachRightHand(Pose pose, Vector3 grip) {
        (pose, float roll) = ArmReach.Reaching(pose, grip, Transform3D.Identity, elbowToward: new(-1, -1, -.2f));
        return pose with { ArmRollR = roll };
    }

    /// <summary>The mesh and contact surface share this transform. The bucket rotates around its bail hinge.</summary>
    public Transform3D Held(Frame frame, Vector3 fist, out Basis bail) {
        bail = Basis.Identity;
        if (!IsBucket) return new(frame.Turn, fist);
        var body = frame.Turn * new Basis(Vector3.Right, Mathf.DegToRad(frame.Lag));
        bail = body.Inverse() * frame.Turn;
        var grip = VesselShape.BucketHinge + bail * (VesselShape.BucketGrip - VesselShape.BucketHinge);
        return new(body, fist - body * grip);
    }

    /// <summary>Whether the sweep of the working end reaches the wolf's body, not the handle or wind-up.</summary>
    public bool TouchesDuring(Vector2 player, Vector2 aim, Wolf wolf, float from, float until) {
        from = Math.Max(from, ActiveFrom);
        until = Math.Min(until, ActiveUntil);
        if (from > until) return false;
        var (tail, nose) = WolfBodies.Body(wolf);
        var side = BodySide.Out(aim, BodySide.Left);
        float y = WolfBodies.Top(wolf) - .22f;
        var rear = new Vector3((tail - player).Dot(side), y, (tail - player).Dot(aim));
        var front = new Vector3((nose - player).Dot(side), y, (nose - player).Dot(aim));
        float combined = radius + WolfBodies.WolfHalfWidth;
        // A long tick traces the space between frames rather than jumping the working end over a target.
        int steps = Math.Max(1, (int)MathF.Ceiling((until - from) * 120));
        const int sections = 9;
        for (int step = 0; step <= steps; step++) {
            float elapsed = Mathf.Lerp(from, until, step / (float)steps);
            var pose = ActorAnimation.PoseAt(Action, elapsed, false);
            var held = Held(At(elapsed), ArmReach.InRightHand(pose, Vector3.Zero), out _);
            var a = held * edgeStart;
            var b = held * edgeEnd;
            for (int i = 0; i < sections; i++) {
                var body = rear.Lerp(front, i / (sections - 1f));
                var near = Geometry3D.GetClosestPointToSegment(body, a, b);
                if (body.DistanceSquaredTo(near) <= combined * combined) return true;
            }
        }
        return false;
    }
}
