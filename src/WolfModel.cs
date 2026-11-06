using System;
using Godot;

namespace ChopChopMeow;

/// <summary>What a wolf model shows; a change blends from the pose it had into the new one.</summary>
public enum WolfMotion { Stand, Walk, Trot, Gallop, Leap, Feed, Pin, Recoil, Fall }

/// <summary>
/// A leg's joint angles in degrees, each swinging the segment below forward from its parent's line; Splay swings the whole leg out from the body.
/// Upper hangs from the shoulder or hip, Middle bends at the elbow or stifle, Lower at the wrist or hock, and Paw at the fetlock.
/// </summary>
public readonly record struct WolfLeg(float Upper, float Middle, float Lower, float Paw, float Splay = 0) {
    public static WolfLeg Lerp(WolfLeg a, WolfLeg b, float t) => new(
        Mathf.Lerp(a.Upper, b.Upper, t), Mathf.Lerp(a.Middle, b.Middle, t), Mathf.Lerp(a.Lower, b.Lower, t), Mathf.Lerp(a.Paw, b.Paw, t), Mathf.Lerp(a.Splay, b.Splay, t));
}

/// <summary>
/// A whole wolf pose; angles are in degrees.
/// Lift, Shift and Surge move the body up, sideways and forward from standing; Pitch tips it nose down and Roll onto its right side.
/// Flex rounds the back, tucking chest and pelvis toward each other, and negative stretches it out; Bend curls it to its left.
/// Neck and Head lower the head, NeckTurn and HeadTurn look to its left, and HeadTilt rolls the head.
/// Jaw opens the mouth and Ears lays the ears back. Tail lifts the tail from straight back, Wag swings it to the left and Curl bends it on up.
/// </summary>
public readonly record struct WolfPose(
    float Lift = 0, float Pitch = 0, float Roll = 0, float Shift = 0, float Surge = 0, float Flex = 0, float Bend = 0,
    float Neck = 0, float NeckTurn = 0, float Head = 0, float HeadTurn = 0, float HeadTilt = 0, float Jaw = 0, float Ears = 0,
    float Tail = -65, float Wag = 0, float Curl = 0,
    WolfLeg FrontLeft = default, WolfLeg FrontRight = default, WolfLeg HindLeft = default, WolfLeg HindRight = default) {
    public WolfLeg Leg(int i) => i switch { 0 => FrontLeft, 1 => FrontRight, 2 => HindLeft, _ => HindRight };

    public WolfPose WithLeg(int i, WolfLeg leg) => i switch {
        0 => this with { FrontLeft = leg },
        1 => this with { FrontRight = leg },
        2 => this with { HindLeft = leg },
        _ => this with { HindRight = leg },
    };

    public static WolfPose Lerp(WolfPose a, WolfPose b, float t) => new(
        Mathf.Lerp(a.Lift, b.Lift, t), Mathf.Lerp(a.Pitch, b.Pitch, t), Mathf.Lerp(a.Roll, b.Roll, t), Mathf.Lerp(a.Shift, b.Shift, t), Mathf.Lerp(a.Surge, b.Surge, t),
        Mathf.Lerp(a.Flex, b.Flex, t), Mathf.Lerp(a.Bend, b.Bend, t), Mathf.Lerp(a.Neck, b.Neck, t), Mathf.Lerp(a.NeckTurn, b.NeckTurn, t),
        Mathf.Lerp(a.Head, b.Head, t), Mathf.Lerp(a.HeadTurn, b.HeadTurn, t), Mathf.Lerp(a.HeadTilt, b.HeadTilt, t), Mathf.Lerp(a.Jaw, b.Jaw, t), Mathf.Lerp(a.Ears, b.Ears, t),
        Mathf.Lerp(a.Tail, b.Tail, t), Mathf.Lerp(a.Wag, b.Wag, t), Mathf.Lerp(a.Curl, b.Curl, t),
        WolfLeg.Lerp(a.FrontLeft, b.FrontLeft, t), WolfLeg.Lerp(a.FrontRight, b.FrontRight, t), WolfLeg.Lerp(a.HindLeft, b.HindLeft, t), WolfLeg.Lerp(a.HindRight, b.HindRight, t));
}

/// <summary>
/// A wolf built facing +Z, posed every frame.
/// Gaits plant each paw where it lands and solve the leg down to it, so a paw on the ground stays put while the body passes over it; a change of motion blends from the old pose.
/// </summary>
public sealed class WolfModel {
    public readonly Node3D Root = new();
    private readonly Skeleton3D skeleton;
    /// <summary>The view's bookkeeping: heading, last world position, last stagger seen and the gap the knockback slide closes.</summary>
    public float Yaw, Stagger;
    public Vector2 Last, Knock;
    /// <summary>
    /// A blow: seconds until the swing lands and how long it took in all, where the wolf stood and who swung, then seconds left in the recoil and the knockback it started from.
    /// Pinned counts seconds since a leap's jaws reached the throat.
    /// </summary>
    public float HitIn, HitSeconds, Recoiling, Pinned;
    public Vector2 HitFrom, Striker, KnockFrom;
    /// <summary>A leap's bearing from the player to the wolf, and where it holds the throat once the player is down.</summary>
    public Vector2 Toward;
    public Vector3 Held;

    /// <summary>How far ahead of its root a leaping wolf's jaws reach the throat of a standing player: the reach of the bite plus the throat's own depth.</summary>
    public const float ContactReach = .65f;
    /// <summary>How close to the swinger a struck wolf lunges before the blow lands: about the reach of an arm and a muzzle.</summary>
    public const float StrikeReach = 1.35f;
    /// <summary>How long the recoil from a blow plays, from the moment it lands, and the share of it spent hopping back.</summary>
    public const float RecoilSeconds = .6f, HopShare = .4f;
    /// <summary>How long a killed wolf takes to go down, and the share of that it is thrown through the air before it hits the ground.</summary>
    public const float FallSeconds = .7f, ThrowShare = .35f;

    private WolfMotion motion = WolfMotion.Stand;
    private WolfPose shown, from;
    private float blend = 1, blendSeconds = .25f;
    /// <summary>Position in the stride, and ground speed smoothed over a few frames.</summary>
    private float cycle, speed;

    public WolfModel() {
        skeleton = WolfShape.Build(Root);
        shown = Standing(0, false);
        Apply(shown);
    }

    public WolfMotion Motion => motion;

    // ---------- Gaits ----------

    /// <summary>
    /// A gait: stride length in tiles, the share of the stride each paw is on the ground, each leg's place in the stride and how high front and hind paws lift.
    /// Reach carries a swinging paw on past its landing spot before it comes back to land.
    /// </summary>
    private readonly record struct Gait(float Length, float Duty, float FrontLeft, float FrontRight, float HindLeft, float HindRight, float FrontLift, float HindLift, float Reach, float Width) {
        public float Offset(int leg) => leg switch { 0 => FrontLeft, 1 => FrontRight, 2 => HindLeft, _ => HindRight };
    }

    // A walk sets the feet down one at a time, hind then fore on each side; a trot moves diagonal pairs together with a short float between them;
    // the rotary gallop lands both forefeet then both hind feet, folding the spine under between them and stretching it out after.
    private static readonly Gait WalkGait = new(.9f, .68f, .23f, .73f, 0, .5f, .07f, .06f, 0, .9f);
    private static readonly Gait TrotGait = new(1.26f, .42f, 0, .5f, .52f, .02f, .12f, .1f, .04f, .72f);
    private static readonly Gait GallopGait = new(2.42f, .25f, 0, .1f, .6f, .5f, .2f, .16f, .14f, .85f);

    /// <summary>Where each paw stands at rest under its shoulder or hip.</summary>
    private static float Home(int leg) => leg < 2 ? .33f : -.34f;

    /// <summary>
    /// Moves over <paramref name="distance"/> tiles this frame: a walk while slow, a trot when quicker and a gallop for a chase.
    /// A negative distance steps backward. Wary carries the body low with the head down, as when stalking.
    /// </summary>
    public void Move(float distance, float dt, bool chasing, bool wary) {
        if (dt > 0) speed = Mathf.Lerp(speed, Math.Abs(distance) / dt, 1 - MathF.Exp(-dt * 8));
        var gait = chasing ? WolfMotion.Gallop : speed > (motion == WolfMotion.Trot ? 1.6f : 2.1f) ? WolfMotion.Trot : WolfMotion.Walk;
        var table = gait switch { WolfMotion.Gallop => GallopGait, WolfMotion.Trot => TrotGait, _ => WalkGait };
        cycle = Mathf.PosMod(cycle + distance / table.Length, 1);
        Show(gait, gait switch { WolfMotion.Gallop => Gallop(cycle), WolfMotion.Trot => Trot(cycle, wary), _ => Walk(cycle, wary) }, dt);
    }

    private WolfPose Walk(float p, bool wary) {
        float step = MathF.Cos(p * Mathf.Tau * 2), side = MathF.Sin(p * Mathf.Tau);
        var body = new WolfPose(Lift: -.008f + .006f * step - (wary ? .07f : 0), Pitch: .8f * step + (wary ? 3 : 0), Roll: 1.2f * side, Bend: 2 * side,
            Neck: (wary ? 30 : 12) + 3 * step, Head: wary ? -24 : -6, HeadTurn: -2 * side, Ears: wary ? -8 : 0,
            Tail: wary ? -78 : -64, Wag: 7 * side, Curl: 8);
        return Stride(body, WalkGait, p);
    }

    private WolfPose Trot(float p, bool wary) {
        // Lowest as each diagonal pair takes the weight, highest in the float between them.
        float bob = MathF.Cos((p - .2f) * Mathf.Tau * 2), side = MathF.Sin(p * Mathf.Tau);
        var body = new WolfPose(Lift: .006f - .013f * bob - (wary ? .06f : 0), Pitch: .6f * bob, Roll: 1.5f * side, Bend: 1.5f * side,
            Neck: (wary ? 30 : 20) + 2 * bob, Head: wary ? -24 : -12, Ears: wary ? -6 : 6, Jaw: 3,
            Tail: wary ? -72 : -48, Wag: 9 * side, Curl: 10);
        return Stride(body, TrotGait, p);
    }

    private WolfPose Gallop(float p) {
        // The spine folds under at the gathered float and stretches out in the extended one; the head stays level over the rocking body.
        float flex = MathF.Cos((p - .45f) * Mathf.Tau), pitch = 6 * MathF.Cos((p - .25f) * Mathf.Tau);
        var body = new WolfPose(Lift: .045f + .05f * MathF.Cos((p - .44f) * Mathf.Tau * 2), Pitch: pitch, Flex: 15 * flex, Roll: 1.5f * MathF.Sin(p * Mathf.Tau),
            Neck: 12 - pitch - 4 * flex, Head: -10, Jaw: 8, Ears: 28, Tail: -18 + 9 * flex, Curl: 6 - 6 * flex);
        return Stride(body, GallopGait, p);
    }

    /// <summary>The legs of a gait on <paramref name="body"/>: each paw sweeps back flat on the ground through its stance, then lifts, folds and reaches forward to land.</summary>
    private static WolfPose Stride(WolfPose body, Gait g, float p) {
        float sweep = g.Length * g.Duty / 2;
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            float q = Mathf.PosMod(p - g.Offset(leg), 1), z, y = WolfShape.Sole, lower, paw = 0;
            if (q < g.Duty) {
                // A stance moves the paw back at exactly the body's speed, so it stays where it landed; the heel rises to push off.
                float u = q / g.Duty, push = Mathf.SmoothStep(.7f, 1, u);
                z = Home(leg) + sweep - 2 * sweep * u;
                lower = hind ? Mathf.Lerp(6, -28, u) : Mathf.Lerp(16, -14, u);
                paw = -30 * push; y += .02f * push;
            } else {
                float s = (q - g.Duty) / (1 - g.Duty), fold = MathF.Sin(MathF.PI * MathF.Pow(s, .7f));
                z = Home(leg) - sweep + 2 * sweep * Mathf.SmoothStep(0, 1, s) + g.Reach * MathF.Sin(MathF.PI * s) * s;
                y += (hind ? g.HindLift : g.FrontLift) * fold;
                // The wrist flips the forepaw up behind as it leaves the ground; the hock lifts the hind paw back and up.
                lower = hind ? Mathf.Lerp(-28, 6, s) - 40 * fold : Mathf.Lerp(-14, 16, s) - 100 * fold;
                paw = (hind ? -25 : -45) * fold;
            }
            float x = WolfShape.Pivot(leg).X * g.Width;
            body = body.WithLeg(leg, Reach(body, leg, new Vector3(x, y, z), lower, lower + paw));
        }
        return body;
    }

    // ---------- Standing, leaping and feeding ----------

    /// <summary>Standing: slow breaths, weight shifting between the hind legs, the head looking about; wary crouches low with the head down and the lips drawn back.</summary>
    public void Stand(float time, float dt, bool wary) => Show(WolfMotion.Stand, Standing(time, wary), dt);

    private static WolfPose Standing(float time, bool wary) {
        float breath = MathF.Sin(time * 2.4f), look = MathF.Sin(time * .37f), settle = MathF.Sin(time * .23f);
        var body = wary
            ? new WolfPose(Lift: -.08f + .004f * breath, Pitch: 4, Neck: 30 + breath, Head: -26, NeckTurn: look * 4, Jaw: 9 + 2 * breath, Ears: -10, Tail: -82, Wag: MathF.Sin(time * .9f) * 3)
            : new WolfPose(Lift: .004f * breath, Shift: .008f * settle, Roll: -1.2f * settle, Neck: 6 + 4 * MathF.Sin(time * .55f), NeckTurn: look * 16 + MathF.Sin(time * 1.3f) * 4,
                Head: -6 + breath, HeadTurn: look * 10, HeadTilt: look * 4, Jaw: 2 + 2 * Math.Max(0, MathF.Sin(time * 7)), Ears: 4 + 6 * Math.Max(0, MathF.Sin(time * .9f + 1)),
                Tail: -66 + 3 * MathF.Sin(time * .8f), Wag: 8 * MathF.Sin(time * 1.1f), Curl: 8 + 4 * MathF.Sin(time * 1.1f + .8f));
        return Planted(body, wary ? 1.15f : 1, settle * .015f);
    }

    /// <summary>All four paws flat on the ground at home, spread by <paramref name="spread"/>; <paramref name="shift"/> moves the hind paws forward.</summary>
    private static WolfPose Planted(WolfPose body, float spread, float shift = 0, float frontAhead = 0, float frontUp = 0) {
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            var at = new Vector3(WolfShape.Pivot(leg).X * spread, WolfShape.Sole + (hind ? 0 : frontUp), Home(leg) + (hind ? shift * (leg == 2 ? 1 : -1) : frontAhead));
            body = body.WithLeg(leg, Reach(body, leg, at, hind ? 2 : 10, 0));
        }
        return body;
    }

    /// <summary>
    /// The leap at a standing player's throat, from the push off at <paramref name="progress"/> 0 to the moment the jaws reach it at <paramref name="contact"/>.
    /// The hind legs drive while the forelegs fold up, then reach out for the chest with the jaws wide.
    /// </summary>
    public void Leap(float progress, float contact, float dt) {
        float t = Math.Clamp(progress / Math.Max(contact, .05f), 0, 1);
        float push = Mathf.SmoothStep(0, .3f, t), air = Mathf.SmoothStep(.15f, 1, t), reach = Mathf.SmoothStep(.4f, 1, t);
        // The body arcs up and comes down onto the throat, which sits lower than the top of the leap.
        var body = new WolfPose(Lift: Mathf.Lerp(-.06f, .14f, air) + .22f * MathF.Sin(MathF.PI * air) + .06f * push * (1 - air), Pitch: -12 * push - 22 * air, Flex: Mathf.Lerp(8, -12, air),
            Neck: Mathf.Lerp(10, 16, air), Head: Mathf.Lerp(-8, 8, reach), Jaw: 34 * reach, Ears: Mathf.Lerp(0, 22, air), Tail: Mathf.Lerp(-50, -10, air), Curl: -8);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            float x = WolfShape.Pivot(leg).X;
            // Hind paws stay planted until they straighten, then trail out behind; forepaws tuck under the chest and then reach up and forward.
            var at = hind ? new Vector3(x, WolfShape.Sole + Mathf.Lerp(0, .3f, air), Mathf.Lerp(-.3f, -.78f, air))
                : new Vector3(x, Mathf.Lerp(WolfShape.Sole, .45f, push) + .55f * reach, Mathf.Lerp(.36f, .3f, push) + .45f * reach);
            float lower = hind ? Mathf.Lerp(-10, -70, air) : Mathf.Lerp(10, -90, push * (1 - reach)) + 60 * reach;
            body = body.WithLeg(leg, Reach(body, leg, at, lower, hind ? lower - 30 * air : lower - 20));
        }
        Show(WolfMotion.Leap, body, dt);
    }

    /// <summary>
    /// Feeding at a downed player: braced over the body, tearing with sharp pulls back.
    /// A pinning wolf stands astride the player with its forepaws on the chest; <paramref name="down"/> runs 0 to 1 as the player goes down under it.
    /// </summary>
    public void Feed(float time, float dt, bool pinning, float down = 1) {
        float pull = MathF.Pow(Math.Max(0, MathF.Sin(time * 2.6f)), 3), shake = MathF.Sin(time * 13) * (1 - pull * .6f), bite = Math.Max(0, MathF.Sin(time * 4.5f));
        WolfPose body;
        if (pinning) {
            // Riding the player down: high and nose up while they still stand, then low over them with the jaws at the throat.
            float up = 1 - Mathf.SmoothStep(0, 1, down);
            body = new WolfPose(Lift: -.05f + .3f * up - .02f * pull, Pitch: 14 - 34 * up - 4 * pull, Surge: -.03f * pull, Flex: 4,
                Neck: 52 - 60 * up - 12 * pull, Head: 14 + 4 * shake, HeadTurn: 12 * shake, HeadTilt: 6 * shake, Jaw: 10 + 6 * bite, Ears: 26, Tail: -70 + 6 * MathF.Sin(time * 1.7f), Wag: 8 * MathF.Sin(time * 2.1f));
            body = Planted(body, 1.9f, 0, .05f, .14f + .5f * up);
        } else {
            body = new WolfPose(Lift: -.07f, Pitch: 12 - 4 * pull, Surge: -.05f * pull, Flex: 6, Neck: 46 - 14 * pull + 6 * MathF.Sin(time * 9), Head: 18,
                HeadTurn: 18 * shake, HeadTilt: 6 * shake, Jaw: 6 + 18 * bite, Ears: 22, Tail: -74 + 5 * MathF.Sin(time * 1.7f), Wag: 12 * MathF.Sin(time * 2.1f));
            body = Planted(body, 1.25f, 0, .1f);
        }
        Show(pinning ? WolfMotion.Pin : WolfMotion.Feed, body, dt);
    }

    /// <summary>
    /// Struck: the head snaps away from the blow and the wolf hops back off its forelegs, lands hind paws first and ducks low with its ears flat and tail tucked, then rises wary.
    /// <paramref name="progress"/> runs 0 to 1 over the recoil; the view moves the wolf back only while it is in the air, the first HopShare of it.
    /// </summary>
    public void Recoil(float progress, float dt) {
        float t = Math.Clamp(progress, 0, 1), air = t < HopShare ? MathF.Sin(MathF.PI * t / HopShare) : 0;
        float snap = Mathf.SmoothStep(0, .08f, t) * (1 - Mathf.SmoothStep(.15f, .55f, t));
        // The duck eases into the wary stand the wolf goes on to, rather than out of it.
        float duck = Mathf.SmoothStep(HopShare - .12f, HopShare + .08f, t) * (1 - .45f * Mathf.SmoothStep(.7f, 1, t)), cower = Math.Max(snap, duck);
        var body = new WolfPose(Lift: .1f * air - .15f * duck, Pitch: -6 * air + 6 * duck, Roll: 6 * snap, Bend: -12 * snap, Flex: 10 * air + 4 * duck,
            Neck: -16 * snap + 40 * duck, Head: -10 * snap - 30 * duck, HeadTurn: -36 * snap, HeadTilt: -14 * snap, Jaw: 24 * snap + 12 * duck,
            Ears: 45 * cower, Tail: -62 - 36 * cower, Curl: -30 * cower);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            // In the air the hind paws reach back for the landing and the forepaws trail where they pushed off; landed, the legs spread to brace the crouch.
            var at = new Vector3(WolfShape.Pivot(leg).X * (1 + .2f * duck), WolfShape.Sole + .15f * air, Home(leg) + (hind ? -.1f : .07f) * air + (hind ? 0 : .05f * duck));
            body = body.WithLeg(leg, Reach(body, leg, at, hind ? 2 - 24 * air : 10 - 50 * air, hind ? 0 : -30 * air));
        }
        Show(WolfMotion.Recoil, body, dt);
    }

    /// <summary>
    /// A killed wolf is thrown off its feet, twisting onto its right side in the air, and slams down with a bounce that whips the head and legs.
    /// It then lies limp, legs stretched out and the upper ones resting on the lower, as a dog lies on its side.
    /// <paramref name="progress"/> runs 0 to 1 over FallSeconds; it starts from a stand, so a carcass never blends.
    /// </summary>
    public void Fall(float progress) {
        float p = Math.Clamp(progress, 0, 1), fly = Math.Min(1, p / ThrowShare), down = Mathf.SmoothStep(0, 1, fly), air = MathF.Sin(MathF.PI * fly);
        float after = Math.Max(0, (p - ThrowShare) / (1 - ThrowShare)), bounce = after < .3f ? MathF.Sin(MathF.PI * after / .3f) : 0;
        // The head lags the body and hits the ground a moment after it.
        float slam = p < ThrowShare ? 0 : Mathf.SmoothStep(0, .25f, after), kick = after is > .55f and < .8f ? MathF.Sin(MathF.PI * (after - .55f) / .25f) : 0;
        // Rolled onto its side the body sinks until its flank lies on the ground.
        var body = new WolfPose(Lift: -.5f * down + .12f * air + .07f * bounce, Pitch: -14 * air, Roll: 90 * down + 6 * bounce, Shift: -.05f * down,
            Flex: -12 * air - 4 * down, Bend: 6 * down, Neck: -34 * air + 10 * bounce - 8 * slam, NeckTurn: -12 * slam, Head: 12 * air, HeadTilt: -10 * slam,
            Jaw: 26 * air + 14 * down, Ears: 30 * down, Tail: Mathf.Lerp(-65, -6, down) + 30 * air, Curl: -10 * down);
        var standing = Planted(new WolfPose(), 1);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2, upper = leg % 2 == 0;
            // Knocked off its feet the legs buckle and fold under; on the ground the forelegs stretch forward and the hind legs back, the upper pair drooping onto the lower.
            var flung = hind ? new WolfLeg(45, -100, 70, 20) : new WolfLeg(-35, 30, -90, -20);
            var limp = hind ? new WolfLeg(-30 - 14 * kick, -12, 16 + 10 * kick, 25, upper ? -12 : 4) : new WolfLeg(32, 14, -10, -25, upper ? -12 : 4);
            var at = WolfLeg.Lerp(WolfLeg.Lerp(standing.Leg(leg), limp, down), flung, .8f * air);
            body = body.WithLeg(leg, at with { Upper = at.Upper + (hind ? -14 : 14) * bounce });
        }
        motion = WolfMotion.Fall; blend = 1; shown = body;
        Apply(body);
    }

    /// <summary>Where a paw bone is, in the model, under the pose shown; it stands WolfShape.Sole above the ground when the paw is planted.</summary>
    public Vector3 Paw(int leg) {
        var l = shown.Leg(leg);
        int bone = WolfShape.Leg(leg);
        var at = Frame(shown, leg >= 2) * Posed(bone, Vector3.Zero, Turn(-l.Upper, 0, l.Splay)) * Posed(bone + 1, Vector3.Zero, Turn(-l.Middle))
            * Posed(bone + 2, Vector3.Zero, Turn(-l.Lower)) * Posed(bone + 3, Vector3.Zero, Turn(-l.Paw));
        return at.Origin;
    }

    /// <summary>Where the jaws close, in the model, under the pose shown.</summary>
    public Vector3 Bite {
        get {
            var neck = Frame(shown, false) * Posed(WolfShape.Neck, Vector3.Zero, Turn(shown.Neck, shown.NeckTurn));
            var head = neck * Posed(WolfShape.Head, Vector3.Zero, Turn(shown.Head, shown.HeadTurn, shown.HeadTilt));
            return head * (WolfShape.Bite - WolfShape.Bones[WolfShape.Head].At);
        }
    }

    // ---------- Blending and posing ----------

    private void Show(WolfMotion next, WolfPose target, float dt) {
        if (next != motion) {
            from = shown; blend = 0;
            // A leap and a blow happen fast; changes of gait and settling down take longer.
            blendSeconds = next is WolfMotion.Leap or WolfMotion.Recoil ? .08f : next == WolfMotion.Pin ? .12f : motion == WolfMotion.Recoil ? .3f : .25f;
            motion = next;
        }
        blend = Math.Min(1, blend + dt / blendSeconds);
        shown = blend >= 1 ? target : WolfPose.Lerp(from, target, Mathf.SmoothStep(0, 1, blend));
        Apply(shown);
    }

    private static Quaternion Turn(float pitch, float yaw = 0, float roll = 0) =>
        Basis.FromEuler(new Vector3(Mathf.DegToRad(pitch), Mathf.DegToRad(yaw), Mathf.DegToRad(roll))).GetRotationQuaternion();

    /// <summary>The loin's offset and turn, and each half of the spine's turn and slide against it, shared by posing and the leg solver.</summary>
    private static (Vector3 At, Quaternion Turn) Spine(WolfPose p, int bone) => bone switch {
        WolfShape.Loin => (new Vector3(p.Shift, p.Lift, p.Surge), Turn(p.Pitch, 0, p.Roll)),
        WolfShape.Chest => (new Vector3(0, 0, -.0012f * p.Flex), Turn(p.Flex / 2, p.Bend / 2)),
        _ => (new Vector3(0, 0, .0012f * p.Flex), Turn(-p.Flex / 2, -p.Bend / 2))
    };

    private static Transform3D Rest(int bone) {
        var (parent, at) = WolfShape.Bones[bone];
        return new Transform3D(Basis.Identity, parent >= 0 ? at - WolfShape.Bones[parent].At : at);
    }

    /// <summary>A bone's pose relative to its parent: its rest offset moved by <paramref name="by"/> and turned.</summary>
    private static Transform3D Posed(int bone, Vector3 by, Quaternion turn) => new(new Basis(turn), Rest(bone).Origin + by);

    /// <summary>Where the chest or pelvis sits in the model under <paramref name="p"/>.</summary>
    private static Transform3D Frame(WolfPose p, bool hind) {
        var (loinAt, loinTurn) = Spine(p, WolfShape.Loin);
        int half = hind ? WolfShape.Pelvis : WolfShape.Chest;
        var (at, turn) = Spine(p, half);
        return Posed(WolfShape.Loin, loinAt, loinTurn) * Posed(half, at, turn);
    }

    /// <summary>
    /// Solves a leg so its paw bone reaches <paramref name="paw"/> in the model, with the lowest segment and the paw at the given angles forward of straight down.
    /// The foreleg's elbow bends back and the hind stifle forward, as a dog's do; a paw out of reach is reached for as far as the leg goes.
    /// </summary>
    private static WolfLeg Reach(WolfPose body, int leg, Vector3 paw, float lowerAngle, float pawAngle) {
        bool hind = leg >= 2;
        var frame = Frame(body, hind);
        var local = frame.AffineInverse() * paw;
        var pivot = WolfShape.Pivot(leg) - WolfShape.Bones[hind ? WolfShape.Pelvis : WolfShape.Chest].At;
        var down = frame.Basis * Vector3.Down;
        float lean = MathF.Atan2(down.Z, -down.Y);
        var lengths = WolfShape.Lengths(leg);
        float a3 = Mathf.DegToRad(lowerAngle) - lean;
        // The joint above the lowest segment, then the two upper segments meet it by the law of cosines.
        float jz = local.Z - lengths.Z * MathF.Sin(a3), jy = local.Y + lengths.Z * MathF.Cos(a3);
        float dz = jz - pivot.Z, dy = jy - pivot.Y, reach = MathF.Sqrt(dz * dz + dy * dy);
        float d = Math.Clamp(reach, Math.Abs(lengths.X - lengths.Y) + .01f, lengths.X + lengths.Y - .001f);
        float toward = MathF.Atan2(dz, -dy), spread = MathF.Acos(Math.Clamp((lengths.X * lengths.X + d * d - lengths.Y * lengths.Y) / (2 * lengths.X * d), -1, 1));
        float a1 = hind ? toward + spread : toward - spread;
        float ez = pivot.Z + lengths.X * MathF.Sin(a1), ey = pivot.Y - lengths.X * MathF.Cos(a1);
        float a2 = MathF.Atan2(jz - ez, -(jy - ey));
        // A paw beyond reach takes the straightened leg's own line instead of a joint it cannot get to.
        if (reach > d) a3 = a2 + Math.Clamp(a3 - a2, -.3f, .3f);
        float pawAt = Mathf.DegToRad(pawAngle) - lean;
        float splay = MathF.Atan2(paw.X - WolfShape.Pivot(leg).X, Math.Max(.2f, WolfShape.Pivot(leg).Y - paw.Y));
        return new WolfLeg(Mathf.RadToDeg(a1), Mathf.RadToDeg(a2 - a1), Mathf.RadToDeg(a3 - a2), Mathf.RadToDeg(pawAt - a3), Mathf.RadToDeg(splay));
    }

    private void Apply(WolfPose p) {
        for (int bone = WolfShape.Loin; bone <= WolfShape.Pelvis; bone++) {
            var (at, turn) = Spine(p, bone);
            skeleton.SetBonePosePosition(bone, Rest(bone).Origin + at);
            skeleton.SetBonePoseRotation(bone, turn);
        }
        skeleton.SetBonePoseRotation(WolfShape.Neck, Turn(p.Neck, p.NeckTurn));
        skeleton.SetBonePoseRotation(WolfShape.Head, Turn(p.Head, p.HeadTurn, p.HeadTilt));
        skeleton.SetBonePoseRotation(WolfShape.Jaw, Turn(p.Jaw));
        skeleton.SetBonePoseRotation(WolfShape.EarL, Turn(-p.Ears, 0, -p.Ears * .3f));
        skeleton.SetBonePoseRotation(WolfShape.EarR, Turn(-p.Ears, 0, p.Ears * .3f));
        // The tail rests pointing straight back; most of the lift is at the root and the curl and wag carry on along it.
        skeleton.SetBonePoseRotation(WolfShape.Tail, Turn(p.Tail, p.Wag));
        skeleton.SetBonePoseRotation(WolfShape.Tail + 1, Turn(p.Curl * .5f, p.Wag * .6f));
        skeleton.SetBonePoseRotation(WolfShape.Tail + 2, Turn(p.Curl, p.Wag * .4f));
        for (int leg = 0; leg < 4; leg++) {
            var l = p.Leg(leg);
            int bone = WolfShape.Leg(leg);
            // The pose's angles swing forward; a positive turn about +X swings a hanging bone back.
            skeleton.SetBonePoseRotation(bone, Turn(-l.Upper, 0, l.Splay));
            skeleton.SetBonePoseRotation(bone + 1, Turn(-l.Middle));
            skeleton.SetBonePoseRotation(bone + 2, Turn(-l.Lower));
            skeleton.SetBonePoseRotation(bone + 3, Turn(-l.Paw));
        }
    }
}
