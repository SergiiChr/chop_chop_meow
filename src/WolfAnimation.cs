using System;
using Godot;

namespace ChopChopMeow;

/// <summary>What a wolf is doing, each with its own keyframed motion; <see cref="WolfAnimation.PoseAt"/> gives the pose at any point of it.</summary>
public enum WolfState { Idle, Stalk, Trot, Gallop, Bite, Leap, Feed, Recoil, Fall }

/// <summary>
/// One leg's joint angles in degrees. Swing turns the whole leg about its shoulder or hip, positive back.
/// Knee folds the leg at its middle joint and Hock at its lower one, positive the way the leg flexes:
/// a foreleg folds its forearm and paw back at the elbow and wrist, a hind leg folds its shin and paw forward at the stifle and hock.
/// </summary>
public readonly record struct WolfLeg(float Swing, float Knee, float Hock) {
    public static WolfLeg Lerp(WolfLeg a, WolfLeg b, float t) => new(Mathf.Lerp(a.Swing, b.Swing, t), Mathf.Lerp(a.Knee, b.Knee, t), Mathf.Lerp(a.Hock, b.Hock, t));
}

/// <summary>
/// Joint angles in degrees for the wolf rig, built facing +Z.
/// Pitch tips the whole body nose down, Roll tips it onto its right side and Spine arches the back, folding the hindquarter and its legs under; negative hollows it.
/// Neck lowers the neck from its resting rise, Head lowers the muzzle on the neck, Turn and Tilt turn and cock the head, Jaw opens the mouth and Ears lays the ears back.
/// Tail raises the tail from hanging straight down, 90 straight back; Wag swings it sideways and Curl bends its outer half up.
/// Shift moves the body sideways and Lunge forward over the paws. The body stands with its lowest paw on the ground, Lift above that, unless Height sets its height outright, as when it lies on its side.
/// </summary>
public readonly record struct WolfPose(
    float Pitch, float Roll, float Spine, float Neck, float Head, float Turn, float Tilt, float Jaw, float Ears, float Tail, float Wag, float Curl,
    WolfLeg FrontL, WolfLeg FrontR, WolfLeg HindL, WolfLeg HindR, float Shift = 0, float Lift = 0, float? Height = null, float Lunge = 0) {
    public WolfLeg Leg(int i) => i switch { 0 => FrontL, 1 => FrontR, 2 => HindL, _ => HindR };
}

/// <summary>Keyframed wolf motion and the rig's joint positions, shared by the model and the checks; everything here is plain arithmetic.</summary>
public static class WolfAnimation {
    /// <summary>The loin pivot, where the back flexes; the hindquarter, hips and tail hang from it.</summary>
    public static readonly Vector3 Loin = new(0, .64f, -.06f);
    /// <summary>The neck pivot at the withers and the head pivot at the neck's end, in the neck's space.</summary>
    public static readonly Vector3 Withers = new(0, .69f, .40f), NeckEnd = new(0, .14f, .25f);
    public static readonly Vector3 TailRoot = new(0, .66f, -.49f);
    /// <summary>Shoulder and hip pivots in the body's space: front left, front right, hind left, hind right.</summary>
    public static readonly Vector3[] Pivots = { new(.10f, .58f, .30f), new(-.10f, .58f, .30f), new(.10f, .60f, -.34f), new(-.10f, .60f, -.34f) };
    private static readonly Vector3[] foreJoints = { new(0, -.23f, -.04f), new(0, -.47f, -.01f) }, hindJoints = { new(0, -.18f, .09f), new(0, -.38f, -.07f) };
    private static readonly Vector3 forePaw = new(0, -.58f, .05f), hindPaw = new(0, -.60f, .045f);
    /// <summary>The elbow and wrist, or the stifle and hock, below a leg's pivot.</summary>
    public static Vector3[] Joints(bool hind) => hind ? hindJoints : foreJoints;
    /// <summary>The bottom of a paw below its leg's pivot, with the leg straight.</summary>
    public static Vector3 PawRest(bool hind) => hind ? hindPaw : forePaw;
    /// <summary>How long a change of state blends from the old pose into the new one; a leap and a hit are too quick for more than a snap.</summary>
    public static float BlendSeconds(WolfState into) => into switch { WolfState.Leap => .08f, WolfState.Bite => .06f, WolfState.Recoil => .05f, _ => .2f };
    /// <summary>How long a bite's lunge and snap take.</summary>
    public const float BiteSeconds = .35f;
    /// <summary>Strides a tile of ground covers at each gait, as radians of its cycle: the swing of the legs matches the ground passing under them.</summary>
    public const float StalkRate = 11f, TrotRate = 7.5f, GallopRate = 3.6f;

    /// <summary>
    /// The pose at <paramref name="t"/> into <paramref name="state"/>: seconds for standing still and feeding, tiles travelled for a gait,
    /// and 0 to 1 through a bite, a leap, a hit's recoil or a death fall.
    /// </summary>
    public static WolfPose PoseAt(WolfState state, float t) => state switch {
        WolfState.Stalk => Stalk(t),
        WolfState.Trot => Trot(t),
        WolfState.Gallop => Gallop(t),
        WolfState.Bite => Bite(t),
        WolfState.Leap => Leap(t),
        WolfState.Feed => Feed(t),
        WolfState.Recoil => Recoil(t),
        WolfState.Fall => Fall(t),
        _ => Idle(t),
    };

    private static WolfLeg Fore(float swing = -2, float knee = 5, float hock = 5) => new(swing, knee, hock);
    private static WolfLeg Rear(float swing = 3, float knee = 7, float hock = 9) => new(swing, knee, hock);

    /// <summary>Standing alert: slow breaths, the head looking about, an ear flicking back now and then, the weight settling from one hind leg to the other and the tail swaying.</summary>
    private static WolfPose Idle(float t) {
        float breath = MathF.Sin(t * 2.4f), settle = MathF.Sin(t * .23f) * 4, flick = MathF.Pow(Math.Max(0, MathF.Sin(t * 1.9f + 2)), 9);
        return new WolfPose(Pitch: breath * .5f, Roll: 0, Spine: 1, Neck: 4 + breath * 1.2f, Head: -3 + MathF.Sin(t * .55f) * 6 + breath * .8f,
            Turn: MathF.Sin(t * .37f) * 22 + MathF.Sin(t * 1.3f) * 4, Tilt: MathF.Sin(t * .37f) * 4, Jaw: 2, Ears: 4 + flick * 28,
            Tail: 46 + MathF.Sin(t * .8f) * 4, Wag: MathF.Sin(t * 1.1f) * 9, Curl: 14 + MathF.Sin(t * 1.1f + .8f) * 8,
            Fore(), Fore(), Rear(3 + settle, 7 + Math.Max(0, settle) * 2.5f), Rear(3 - settle, 7 + Math.Max(0, -settle) * 2.5f));
    }

    /// <summary>A slow walk in the lateral sequence of a stalking canine, head and tail held low, each leg a quarter cycle behind the last.</summary>
    private static WolfPose Stalk(float travelled) {
        float a = travelled * StalkRate, sway = MathF.Sin(a) * 1.5f;
        // Hind left, front left, hind right, front right: each foot lands a quarter stride after the last.
        return new WolfPose(Pitch: 2, Roll: sway * .6f, Spine: 2, Neck: 30, Head: -14, Turn: MathF.Sin(a * .5f) * 3, Tilt: 0, Jaw: 2, Ears: 6,
            Tail: 38 + MathF.Cos(a) * 2, Wag: MathF.Sin(a) * 5, Curl: 10,
            Step(a - Mathf.Pi / 2, false, 24, give: 16), Step(a + Mathf.Pi / 2, false, 24, give: 16), Step(a, true, 26, give: 18), Step(a + Mathf.Pi, true, 26, give: 18), Shift: sway * .01f);
    }

    /// <summary>A trot: diagonal pairs swing together, the swinging legs folding to clear the ground, the head carried low and level like a hunting wolf's.</summary>
    private static WolfPose Trot(float travelled) {
        float a = travelled * TrotRate, bob = MathF.Cos(a * 2);
        // Front left with hind right, front right with hind left; the body vaults over each stance and dips as the pairs change.
        return new WolfPose(Pitch: -1 + bob * 1.5f, Roll: MathF.Sin(a) * 1.2f, Spine: 2 - bob, Neck: 22, Head: -6 + bob, Turn: MathF.Sin(a) * 2, Tilt: 0, Jaw: 5, Ears: 8,
            Tail: 60 + bob * 3, Wag: MathF.Sin(a) * 10, Curl: 15 + MathF.Sin(a + .8f) * 8,
            Step(a, false, 32, give: 26), Step(a + Mathf.Pi, false, 32, give: 26), Step(a + Mathf.Pi, true, 34, give: 28), Step(a, true, 34, give: 28));
    }

    /// <summary>
    /// A rotary gallop: the hind legs land one after the other with the back arched and drive the body into a stretched flight, then the forelegs catch it.
    /// The legs fold tight as they swing forward; the head stretches low ahead with the ears back and the tail streams behind.
    /// </summary>
    private static WolfPose Gallop(float travelled) {
        float a = travelled * GallopRate, ground = GallopGround(a);
        // Airborne: the body flies a shallow arc from the last foreleg's push-off to the first hind leg's landing.
        if (ground == float.MinValue) {
            float s = Mathf.PosMod(a - GallopTakeOff, Mathf.Tau) / (GallopLanding - GallopTakeOff);
            ground = Mathf.Lerp(GallopGround(GallopTakeOff), GallopGround(GallopLanding), s) + .1f * MathF.Sin(s * Mathf.Pi);
        }
        return GallopPose(a) with { Height = ground };
    }

    /// <summary>Each gallop leg's beat ahead of the stride (hind left, hind right, fore right, fore left land in turn) and the share of the stride a paw stays planted.</summary>
    private static readonly float[] gallopBeats = { -2.9f, -2.4f, 0, -.5f };
    private const float GallopDuty = .33f;
    private static readonly float GallopTakeOff = -gallopBeats[0] + GallopDuty * Mathf.Pi - .001f, GallopLanding = Mathf.Tau - GallopDuty * Mathf.Pi + .001f;

    /// <summary>The body height that puts the lowest planted paw on the ground at <paramref name="a"/> of a gallop, or <see cref="float.MinValue"/> while airborne.</summary>
    private static float GallopGround(float a) {
        var pose = GallopPose(a); float ground = float.MinValue;
        for (int leg = 0; leg < 4; leg++) if (Planted(a + gallopBeats[leg], GallopDuty)) ground = Math.Max(ground, -PawFromBody(pose, leg).Y);
        return ground;
    }

    private static WolfPose GallopPose(float a) {
        // The back hollows as the hind legs drive out and arches as they reach under again through the gathered flight.
        float spine = -14 * MathF.Sin(a - .4f);
        // The nose comes up through the hind legs' drive and dips as the forelegs catch the body, then the hind legs reach under the lowered forequarters.
        float pitch = -4 * MathF.Cos(a - 1) + 3 * MathF.Cos(2 * (a - 2.65f));
        // The arched back already carries the hind legs forward, so they swing less from the hips than the forelegs do from the shoulders.
        var hindL = Step(a + gallopBeats[2], true, 26, 55, 50, 18, GallopDuty); var hindR = Step(a + gallopBeats[3], true, 26, 55, 50, 18, GallopDuty);
        return new WolfPose(Pitch: pitch, Roll: 0, Spine: spine, Neck: 24 - pitch * .3f, Head: -8 - pitch * .5f, Turn: 0, Tilt: 0, Jaw: 14, Ears: 36,
            Tail: 84 - pitch * .8f, Wag: 0, Curl: 8 - 14 * MathF.Sin(a),
            Step(a + gallopBeats[0], false, 30, 70, 85, 22, GallopDuty), Step(a + gallopBeats[1], false, 30, 70, 85, 22, GallopDuty),
            hindL with { Swing = hindL.Swing + spine }, hindR with { Swing = hindR.Swing + spine });
    }

    /// <summary>Whether a paw stepping at <paramref name="beat"/> with <paramref name="duty"/> is on the ground.</summary>
    private static bool Planted(float beat, float duty) => Math.Abs(Mathf.PosMod(beat + Mathf.Pi, Mathf.Tau) - Mathf.Pi) <= duty * Mathf.Pi;

    /// <summary>
    /// A leg's swing at <paramref name="beat"/> of its cycle: back at a quarter, forward at three quarters, folding as it swings through.
    /// A planted leg gives at its joints by <paramref name="give"/> as the body passes over it, so the body rides level instead of vaulting over each straight leg.
    /// <paramref name="duty"/> is the share of the cycle the paw spends on the ground, centred on the beat's start: a running leg is planted briefly and swings slowly.
    /// </summary>
    private static WolfLeg Step(float beat, bool hind, float swing, float knee = 0, float hock = 0, float give = 0, float duty = .5f) {
        float phase = Mathf.PosMod(beat + Mathf.Pi, Mathf.Tau) - Mathf.Pi, edge = duty * Mathf.Pi;
        // Warp the beat so the stance spans the first and last quarters of the turn whatever its duty.
        beat = Math.Abs(phase) < edge ? phase / (2 * duty) : MathF.CopySign(Mathf.Pi / 2 + (Math.Abs(phase) - edge) / (2 * (1 - duty)), phase);
        float lift = Math.Max(0, -MathF.Cos(beat)), planted = Math.Max(0, MathF.Cos(beat));
        if (knee == 0) (knee, hock) = hind ? (40, 45) : (45, 70);
        return new WolfLeg(MathF.Sin(beat) * swing, (hind ? 8 : 6) + lift * knee + planted * give, (hind ? 10 : 6) + lift * hock + planted * give);
    }

    /// <summary>A bite from a stand: the body lunges forward over braced forelegs with the ears back, the jaws snap shut a third of the way in and it settles back.</summary>
    private static WolfPose Bite(float t) {
        t = Math.Clamp(t, 0, 1);
        float lunge = t < .3f ? Ease(t / .3f) : 1 - Ease((t - .3f) / .7f), jaw = t < .22f ? Ease(t / .22f) : 1 - Ease(Math.Min(1, (t - .22f) / .18f));
        return new WolfPose(Pitch: 9 * lunge, Roll: 0, Spine: -6 * lunge, Neck: 14 * lunge, Head: -6 * lunge, Turn: 0, Tilt: 0, Jaw: 4 + 44 * jaw, Ears: 42 * lunge,
            Tail: 66, Wag: 0, Curl: 6, Fore(-22 * lunge, 10, 8), Fore(-18 * lunge, 10, 8), Rear(12 * lunge, 6, 8), Rear(10 * lunge, 6, 8), Lunge: .22f * lunge);
    }

    /// <summary>
    /// A leap at the throat, <paramref name="t"/> 0 at take-off and 1 landing on the player: the hind legs coil and drive out straight, the forelegs tuck and then reach,
    /// the body rises nose-up and comes down nose-first with the jaws wide, closing as it lands.
    /// </summary>
    private static WolfPose Leap(float t) {
        t = Math.Clamp(t, 0, 1);
        float arc = MathF.Sin(t * Mathf.Pi), drive = Ease(Math.Clamp((t - .04f) / .22f, 0, 1)), reach = Ease(Math.Clamp((t - .35f) / .45f, 0, 1));
        float gather = Ease(Math.Clamp((t - .72f) / .28f, 0, 1)), dive = Ease(t), jaw = 8 + 38 * MathF.Pow(arc, .7f) * (1 - gather * .7f);
        var fore = new WolfLeg(Mathf.Lerp(-22, -72, reach) + gather * 20, Mathf.Lerp(72, 8, reach), Mathf.Lerp(80, 12, reach));
        var rear = new WolfLeg(Mathf.Lerp(Mathf.Lerp(-18, 56, drive), 12, gather), Mathf.Lerp(Mathf.Lerp(46, 2, drive), 32, gather), Mathf.Lerp(Mathf.Lerp(42, 2, drive), 30, gather));
        return new WolfPose(Pitch: Mathf.Lerp(-24, 16, dive), Roll: 0, Spine: Mathf.Lerp(-12, 8, dive), Neck: Mathf.Lerp(0, 32, Ease(Math.Clamp((t - .45f) / .55f, 0, 1))),
            Head: Mathf.Lerp(-16, 22, dive), Turn: 0, Tilt: 0, Jaw: jaw, Ears: 48, Tail: 92 + arc * 14, Wag: 0, Curl: -8 - arc * 8,
            fore, fore, rear, rear, Lift: arc * .7f);
    }

    /// <summary>Head down and tearing, legs braced: a hard pull back now and then with quicker shakes of the head between, and the jaws working.</summary>
    private static WolfPose Feed(float t) {
        float pull = MathF.Pow(Math.Max(0, MathF.Sin(t * 2.6f)), 3), shake = MathF.Sin(t * 13) * (1 - pull * .6f), chew = Math.Max(0, MathF.Sin(t * 6.5f)) * (1 - pull);
        return new WolfPose(Pitch: 9 - pull * 5, Roll: MathF.Sin(t * 4.5f) * 1.5f, Spine: 6, Neck: 52 - pull * 22, Head: 30 - pull * 12 + shake * 3,
            Turn: shake * 14, Tilt: shake * 5, Jaw: 16 + chew * 22 + pull * 10, Ears: 22 + pull * 20,
            Tail: 34 + MathF.Sin(t * 1.7f) * 5, Wag: MathF.Sin(t * 2.1f) * 14, Curl: 10,
            Fore(-16 + pull * 10, 8, 6), Fore(-14 + pull * 10, 8, 6), Rear(20 - pull * 12, 14 + pull * 10, 12), Rear(18 - pull * 12, 14 + pull * 10, 12), Shift: MathF.Sin(t * 4.5f) * .015f);
    }

    /// <summary>
    /// Struck from the front: the head is thrown up and aside with a yelp and the ears flat, the body hunches low on bent legs with the tail tucked,
    /// shudders, and straightens most of the way as <paramref name="t"/> runs 0 to 1.
    /// </summary>
    private static WolfPose Recoil(float t) {
        t = Math.Clamp(t, 0, 1);
        float flinch = t < .14f ? Ease(t / .14f) : 1 - Ease((t - .14f) / .86f) * .8f, shudder = MathF.Sin(t * 46) * (1 - t), yelp = Math.Max(0, 1 - Math.Max(0, t - .1f) / .3f);
        return new WolfPose(Pitch: -8 * flinch, Roll: 6 * flinch + shudder * 3, Spine: 14 * flinch, Neck: -18 * flinch, Head: -22 * flinch + shudder * 6,
            Turn: 30 * flinch + shudder * 8, Tilt: -10 * flinch, Jaw: 32 * yelp, Ears: 60 * flinch, Tail: Mathf.Lerp(46, 14, flinch), Wag: shudder * 10, Curl: 30 * flinch,
            Fore(-12 - 18 * flinch, 8 + 24 * flinch, 6 + 20 * flinch), Fore(-8 - 18 * flinch, 8 + 24 * flinch, 6 + 20 * flinch),
            Rear(6 + 12 * flinch, 10 + 40 * flinch, 10 + 34 * flinch), Rear(2 + 12 * flinch, 10 + 40 * flinch, 10 + 34 * flinch), Shift: .03f * flinch);
    }

    /// <summary>A killed wolf: the forelegs buckle and the chest drops, then it rolls onto its side, its legs paddle once and go still, as <paramref name="t"/> runs 0 to 1.</summary>
    private static WolfPose Fall(float t) {
        t = Math.Clamp(t, 0, 1);
        float buckle = Ease(Math.Clamp(t / .3f, 0, 1)), roll = Ease(Math.Clamp((t - .28f) / .45f, 0, 1)), still = Ease(Math.Clamp((t - .6f) / .4f, 0, 1));
        float drop = buckle * (1 - roll), twitch = t < 1 ? MathF.Sin(t * 40) * 6 * roll * (1 - still) : 0;
        var pose = new WolfPose(Pitch: 18 * drop, Roll: 90 * roll, Spine: 10 * drop + 6 * roll, Neck: 12 * drop - 6 * roll, Head: 20 * drop - 16 * roll, Turn: 22 * roll, Tilt: 0,
            Jaw: 22 * drop + 14 * roll, Ears: 40, Tail: 30 + 48 * roll, Wag: 0, Curl: 12,
            Fore(-14 * buckle - 20 * roll + twitch, 10 + 80 * drop + 38 * roll, 6 + 50 * drop + 30 * roll), Fore(-10 * buckle - 24 * roll + twitch, 10 + 80 * drop + 34 * roll, 6 + 50 * drop + 30 * roll),
            Rear(6 + 20 * roll - twitch, 10 + 20 * drop + 44 * roll, 10 + 16 * drop + 32 * roll), Rear(2 + 24 * roll - twitch, 10 + 20 * drop + 40 * roll, 10 + 16 * drop + 32 * roll),
            Shift: .5f * roll);
        // Rolling about the ground point would sink the body, so it comes down onto its flank beside its old footprint.
        return roll == 0 ? pose : pose with { Height = Mathf.Lerp(Standing(pose with { Roll = 0, Shift = 0 }), .2f, roll) };
    }

    private static float Ease(float t) => t * t * (3 - 2 * t);

    /// <summary>Blends two poses; a set height blends against the standing height, so a body on its paws in both stays on them.</summary>
    public static WolfPose Lerp(in WolfPose a, in WolfPose b, float t) {
        var blend = new WolfPose(Mathf.Lerp(a.Pitch, b.Pitch, t), Mathf.Lerp(a.Roll, b.Roll, t), Mathf.Lerp(a.Spine, b.Spine, t), Mathf.Lerp(a.Neck, b.Neck, t), Mathf.Lerp(a.Head, b.Head, t),
            Mathf.Lerp(a.Turn, b.Turn, t), Mathf.Lerp(a.Tilt, b.Tilt, t), Mathf.Lerp(a.Jaw, b.Jaw, t), Mathf.Lerp(a.Ears, b.Ears, t), Mathf.Lerp(a.Tail, b.Tail, t), Mathf.Lerp(a.Wag, b.Wag, t), Mathf.Lerp(a.Curl, b.Curl, t),
            WolfLeg.Lerp(a.FrontL, b.FrontL, t), WolfLeg.Lerp(a.FrontR, b.FrontR, t), WolfLeg.Lerp(a.HindL, b.HindL, t), WolfLeg.Lerp(a.HindR, b.HindR, t),
            Mathf.Lerp(a.Shift, b.Shift, t), Mathf.Lerp(a.Lift, b.Lift, t), Lunge: Mathf.Lerp(a.Lunge, b.Lunge, t));
        if (a.Height == null && b.Height == null) return blend;
        float above = Mathf.Lerp(a.Height is float ha ? ha - Standing(a) - a.Lift : 0, b.Height is float hb ? hb - Standing(b) - b.Lift : 0, t);
        return blend with { Height = Standing(blend) + blend.Lift + above };
    }

    /// <summary>The body's height above the ground that puts the lowest paw on it, before <see cref="WolfPose.Lift"/>.</summary>
    public static float Standing(in WolfPose pose) {
        float lowest = float.MaxValue;
        for (int leg = 0; leg < 4; leg++) lowest = Math.Min(lowest, PawFromBody(pose, leg).Y);
        return -lowest;
    }

    /// <summary>The height of the body's pivot above the ground in <paramref name="pose"/>.</summary>
    public static float HeightOf(in WolfPose pose) => pose.Height ?? Standing(pose) + pose.Lift;

    /// <summary>The bottom of a paw in the wolf's root space: 0 front left, 1 front right, 2 hind left, 3 hind right.</summary>
    public static Vector3 Paw(in WolfPose pose, int leg) => PawFromBody(pose, leg) + new Vector3(pose.Shift, HeightOf(pose), pose.Lunge);

    private static Vector3 PawFromBody(in WolfPose pose, int leg) {
        bool hind = leg >= 2; var joints = Joints(hind); var angles = pose.Leg(leg); float fold = hind ? -1 : 1;
        var paw = joints[1] + Turn(angles.Hock * fold) * ((hind ? hindPaw : forePaw) - joints[1]);
        var local = Turn(angles.Swing) * (joints[0] + Turn(angles.Knee * fold) * (paw - joints[0]));
        var body = hind ? Loin + Turn(-pose.Spine) * (Pivots[leg] - Loin + local) : Pivots[leg] + local;
        return Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Pitch), 0, Mathf.DegToRad(pose.Roll))) * body;
    }

    private static Basis Turn(float degrees) => new(Vector3.Right, Mathf.DegToRad(degrees));
}
