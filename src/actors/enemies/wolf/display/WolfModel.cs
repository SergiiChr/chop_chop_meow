using System;
using Godot;

namespace ChopChopMeow;

/// <summary>What a wolf model shows; a change blends from the pose it had into the new one.</summary>
public enum WolfMotion { Stand, Walk, Trot, Gallop, Brake, Leap, Feed, Pin, Recoil, Fall, Howl, Tug, Rear, Door, Brace, Snap }

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
/// Jaw opens the mouth and Ears lays the ears back. Snarl, 0 to 1, lifts the lips off the upper teeth and lowers the brows.
/// Tail lifts the tail from straight back, Wag swings it to the left and Curl bends it on up.
/// </summary>
public readonly record struct WolfPose(
    float Lift = 0, float Pitch = 0, float Roll = 0, float Shift = 0, float Surge = 0, float Flex = 0, float Bend = 0,
    float Neck = 0, float NeckTurn = 0, float Head = 0, float HeadTurn = 0, float HeadTilt = 0, float Jaw = 0, float Ears = 0, float Snarl = 0,
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
        Mathf.Lerp(a.Head, b.Head, t), Mathf.Lerp(a.HeadTurn, b.HeadTurn, t), Mathf.Lerp(a.HeadTilt, b.HeadTilt, t), Mathf.Lerp(a.Jaw, b.Jaw, t), Mathf.Lerp(a.Ears, b.Ears, t), Mathf.Lerp(a.Snarl, b.Snarl, t),
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
    /// <summary>How the wolf turns: the head toward a new heading first and the body after it, leaning into the turn.</summary>
    public Turning Turning;
    public Vector2 Last, KnockBack;
    /// <summary>
    /// A blow: whether one has reached the wolf that the swing shown has yet to land, the seconds until it does and who swung,
    /// then seconds left in the recoil and the knockback it started from.
    /// Pinned counts seconds since a leap's jaws reached the throat.
    /// </summary>
    public bool Struck;
    public float HitIn, Recoiling, Pinned;
    public Vector2 Striker, KnockBackFrom;
    /// <summary>How far the wolf's head is turned from its body toward a player it glances at as it walks round them, in radians to its left.</summary>
    public float Glance;
    /// <summary>The most a wolf's head turns from its body to glance at a player, and how fast it turns there and back, in radians and radians a second.</summary>
    public static readonly float MaxGlance = Mathf.DegToRad(105), GlanceRate = Mathf.DegToRad(260);
    /// <summary>Seconds from one glance of a following wolf at its player to the next, how long it looks, and how far apart wolves with neighbouring ids glance.</summary>
    public const float GlanceEvery = 4.6f, GlanceSeconds = 1.7f, GlanceApart = 1.3f;
    /// <summary>Seconds left of holding the end of a snap, jaws shut, after the wolf has landed.</summary>
    public float SnapHeld;
    /// <summary>A leap's bearing from the player to the wolf, and where it holds the throat once the player is down.</summary>
    public Vector2 Toward;
    public Vector3 Held;
    /// <summary>Where the wolf was drawn as its latest move began, to ease from: lunging in to take hold, stepping round to pin, or onto a feeding spot.</summary>
    public Vector3 StepFrom;
    /// <summary>How far the wolf is drawn from its place in the world after letting go of a player it tugged; it closes as the wolf walks.</summary>
    public Vector2 Drift;
    /// <summary>Which way the wolf faced as its latest move began, to turn from.</summary>
    public float FromYaw;
    /// <summary>How far a wolf at a cabin's door has settled into its stand there from where it arrived, 0 to 1.</summary>
    public float Settled;
    /// <summary>Seconds into its door check at a cabin the wolf is drawn at, which runs on between the world's updates.</summary>
    public float DoorCheckClock;
    /// <summary>The door point of the cabin whose step the wolf stands at, when it does: its paws are set on the treads and the floor past the door, and its body lifted and tipped to stand on them.</summary>
    public Vector2? Steps;
    /// <summary>Seconds a wolf has fed at its spot at a fallen player, easing it there from where it stood.</summary>
    public float Fed;
    /// <summary>The spot a feeding wolf holds at a fallen player: how far along the body, in WorldView's steps, and from which side; zero side for none yet.</summary>
    public int FeedAlong, FeedSide;

    /// <summary>How far ahead of its root a leaping wolf's jaws reach the throat of a standing player: the reach of the bite plus the throat's own depth.</summary>
    public const float ContactReach = .65f;
    /// <summary>Seconds a wolf holds the end of a snap, stretched out with its jaws shut, before it gathers its legs under it again.</summary>
    public const float SnapHold = .14f;
    /// <summary>How long the recoil from a blow plays, from the moment it lands, and the share of it spent hopping back.</summary>
    public const float RecoilSeconds = .6f, HopShare = .4f, HopSeconds = RecoilSeconds * HopShare;
    /// <summary>How long a killed wolf takes to go down, and the share of that it is thrown through the air before it hits the ground.</summary>
    public const float FallSeconds = .7f, ThrowShare = .35f;
    /// <summary>How high off the ground the chest of a player lying on their back is, where a pinning wolf's forepaws stand.</summary>
    private const float ChestTop = .3f;

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
    /// <summary>The pose shown, with every paw set on the ground as <see cref="Plant"/> left it.</summary>
    public WolfPose Shown => shown;

    // ---------- Gaits ----------

    /// <summary>
    /// A gait: the share of the stride each paw is on the ground, each leg's place in the stride, how high front and hind paws
    /// lift, how many degrees the wrist folds the forepaw up behind it in the swing, and how far apart, as a share of the legs'
    /// own width, the fore and the hind paws are set.
    /// </summary>
    private readonly record struct Gait(float Duty, float FrontLeft, float FrontRight, float HindLeft, float HindRight, float FrontLift, float HindLift, float Fold, float FrontWidth, float HindWidth) {
        public float Offset(int leg) => leg switch { 0 => FrontLeft, 1 => FrontRight, 2 => HindLeft, _ => HindRight };
        public float Width(int leg) => leg < 2 ? FrontWidth : HindWidth;
    }

    // A walk sets the feet down one at a time, hind then fore on each side; a trot moves diagonal pairs together with a short float between them;
    // the rotary gallop lands both forefeet then both hind feet, folding the spine under between them and stretching it out after.
    // Galloping, the hind paws pass outside the fore, which land close to one line.
    private static readonly Gait WalkGait = new(.68f, .23f, .73f, 0, .5f, .07f, .06f, 70, .9f, .9f);
    private static readonly Gait TrotGait = new(.42f, 0, .5f, .52f, .02f, .12f, .1f, 100, .72f, .72f);
    private static readonly Gait GallopGait = new(.25f, 0, .1f, .6f, .5f, .2f, .16f, 110, .6f, 1.45f);
    /// <summary>The walk's and the trot's stride, in tiles; the gallop's grows with its pace (<see cref="GallopStride"/>).</summary>
    private const float WalkStride = .9f, TrotStride = 1.26f;

    /// <summary>
    /// The gallop's stride at a pace, in tiles: about two and a half strides a second, a few more the faster it goes, so a faster
    /// gallop mostly reaches further, as in running dogs (Heglund, Taylor and McMahon 1974).
    /// </summary>
    private static float GallopStride(float speed) => Math.Max(speed, MinGallop) / GallopRate(speed);
    private static float GallopRate(float speed) => 2.2f + .07f * Math.Max(speed, MinGallop);
    /// <summary>The slowest pace, in tiles a second, the gallop's stride is timed for.</summary>
    private const float MinGallop = .5f;

    /// <summary>The shares of the gallop's stride from which every paw is off the ground until one lands, worked out once from its footfalls.</summary>
    private static readonly (float From, float To)[] GallopFlights = Flights(GallopGait);

    private static (float From, float To)[] Flights(Gait g) {
        const int samples = 1000;
        bool Down(float p) {
            for (int leg = 0; leg < 4; leg++) if (Mathf.PosMod(p - g.Offset(leg), 1) < g.Duty) return true;
            return false;
        }
        var flights = new System.Collections.Generic.List<(float, float)>();
        int first = 0;
        while (!Down(first / (float)samples)) first++;
        for (int i = 0; i < samples; i++) {
            float p = (first + i) / (float)samples;
            if (Down(p) || !Down(p - 1f / samples)) continue;
            int end = i;
            while (!Down((first + end) / (float)samples)) end++;
            flights.Add((Mathf.PosMod(p, 1), Mathf.PosMod((first + end) / (float)samples, 1)));
        }
        return flights.ToArray();
    }

    /// <summary>
    /// How far the body is above where it left the ground, at <paramref name="p"/> through a stride of <paramref name="seconds"/>
    /// with every paw off the ground over <paramref name="flights"/>: in the air it rises and falls under gravity, and on the
    /// ground the legs take its weight like springs, lowest between the flights, so it leaves the ground as fast as it landed.
    /// </summary>
    private static float Bounce(ReadOnlySpan<(float From, float To)> flights, float p, float seconds) {
        for (int i = 0; i < flights.Length; i++) {
            var (from, to) = flights[i];
            float into = Mathf.PosMod(p - from, 1), air = Mathf.PosMod(to - from, 1);
            if (into < air) return Movement.Rise(into * seconds, air * seconds);
            // On the ground between this flight and the next.
            var (next, _) = flights[(i + 1) % flights.Length];
            float ground = Mathf.PosMod(next - to, 1), on = Mathf.PosMod(p - to, 1);
            if (on <= ground) {
                float stance = ground * seconds, t = on * stance / ground, landed = Movement.Gravity * air * seconds / 2;
                float push = MathF.PI * (2 * landed + Movement.Gravity * stance) / (2 * stance);
                return -landed * t + push * stance / MathF.PI * (t - stance / MathF.PI * MathF.Sin(MathF.PI * t / stance)) - Movement.Gravity * t * t / 2;
            }
        }
        return 0;
    }

    /// <summary>Where each paw stands at rest under its shoulder or hip.</summary>
    internal static float Home(int leg) => leg < 2 ? .33f : -.34f;

    /// <summary>
    /// Moves over <paramref name="distance"/> tiles this frame: a walk while slow, a trot when quicker and a gallop for a chase.
    /// A negative distance steps backward. Wary carries the body low with the head down, as when stalking.
    /// </summary>
    public void Move(float distance, float dt, bool chasing, bool wary) {
        if (dt > 0) speed = Mathf.Lerp(speed, Math.Abs(distance) / dt, 1 - MathF.Exp(-dt * 8));
        var gait = chasing ? WolfMotion.Gallop : speed > (motion == WolfMotion.Trot ? 1.6f : 2.1f) ? WolfMotion.Trot : WolfMotion.Walk;
        float length = gait switch { WolfMotion.Gallop => GallopStride(speed), WolfMotion.Trot => TrotStride, _ => WalkStride };
        cycle = Mathf.PosMod(cycle + distance / length, 1);
        Show(gait, gait switch { WolfMotion.Gallop => Gallop(cycle, speed), WolfMotion.Trot => Trot(cycle, wary), _ => Walk(cycle, wary) }, dt, wary);
    }

    private WolfPose Walk(float p, bool wary) {
        float step = MathF.Cos(p * Mathf.Tau * 2), side = MathF.Sin(p * Mathf.Tau);
        var body = new WolfPose(Lift: -.008f + .006f * step - (wary ? .07f : 0), Pitch: .8f * step + (wary ? 3 : 0), Roll: 1.2f * side, Bend: 2 * side,
            Neck: (wary ? 30 : 12) + 3 * step, Head: wary ? -24 : -6, HeadTurn: -2 * side, Jaw: wary ? 8 : 0, Ears: wary ? -8 : 0, Snarl: wary ? 1 : 0,
            Tail: wary ? -78 : -64, Wag: 7 * side, Curl: 8);
        return Stride(body, WalkGait, p, WalkStride);
    }

    private WolfPose Trot(float p, bool wary) {
        // Lowest as each diagonal pair takes the weight, highest in the float between them.
        float bob = MathF.Cos((p - .2f) * Mathf.Tau * 2), side = MathF.Sin(p * Mathf.Tau);
        var body = new WolfPose(Lift: .006f - .013f * bob - (wary ? .06f : 0), Pitch: .6f * bob, Roll: 1.5f * side, Bend: 1.5f * side,
            Neck: (wary ? 30 : 20) + 2 * bob, Head: wary ? -24 : -12, Ears: wary ? -6 : 6, Jaw: wary ? 8 : 3, Snarl: wary ? 1 : 0,
            Tail: wary ? -72 : -48, Wag: 9 * side, Curl: 10);
        return Stride(body, TrotGait, p, TrotStride);
    }

    private static WolfPose Gallop(float p, float speed) {
        // The spine folds under at the gathered float and stretches out in the extended one; the head stays level over the rocking body.
        // The body runs low enough for the forelegs to reach the ground ahead, and flies only as gravity lets it in its floats.
        float flex = MathF.Cos((p - .45f) * Mathf.Tau), pitch = 6 * MathF.Cos((p - .25f) * Mathf.Tau);
        var body = new WolfPose(Lift: GallopLow + Bounce(GallopFlights, p, 1 / GallopRate(speed)), Pitch: pitch, Flex: 15 * flex, Roll: 1.5f * MathF.Sin(p * Mathf.Tau),
            Neck: 12 - pitch - 4 * flex, Head: -10, Jaw: 10, Ears: 28, Tail: -18 + 9 * flex, Curl: 6 - 6 * flex);
        return Stride(body, GallopGait, p, GallopStride(speed));
    }
    /// <summary>How far below standing, in metres, a galloping wolf's body is as it leaves the ground and lands.</summary>
    private const float GallopLow = -.03f;

    /// <summary>
    /// The legs of a gait on <paramref name="body"/>, with strides <paramref name="length"/> tiles long: each paw lands flat and
    /// stays where it landed as the body passes over it, rolls onto its toes to push off, then lifts, folds and reaches forward,
    /// coming back to the ground moving as fast as the ground goes under it, so it neither slides as it lands nor as it leaves.
    /// </summary>
    private static WolfPose Stride(WolfPose body, Gait g, float p, float length) {
        float sweep = length * g.Duty / 2;
        var halves = new Halves(body);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            float q = Mathf.PosMod(p - g.Offset(leg), 1), x = WolfShape.Pivot(leg).X * g.Width(leg), lands = Home(leg) + sweep;
            // The toe pads on the ground as the paw leaves it, rolled onto them, and the paw bone as it lands.
            var left = Rolled(new Vector3(x, 0, Home(leg) - sweep + ToePad), -PushOff);
            Vector3 at; float lower, paw;
            if (q < g.Duty) {
                // A stance moves the paw back at exactly the body's speed, so it stays where it landed; the heel rises about the toes to push off.
                float u = q / g.Duty, roll = -PushOff * Mathf.SmoothStep(.7f, 1, u);
                at = Rolled(new Vector3(x, 0, lands + ToePad - 2 * sweep * u), roll);
                lower = hind ? Mathf.Lerp(6, -28, u) : Mathf.Lerp(16, -14, u);
                paw = roll;
            } else {
                // The paw folds and lifts from rest as it leaves the ground and comes to rest as it lands, highest a little before half way.
                float s = (q - g.Duty) / (1 - g.Duty), rise = MathF.Sin(MathF.PI * MathF.Pow(s, .85f)), fold = rise * rise;
                // Moving with the ground as it leaves and as it lands: in the model, back at the body's pace.
                float span = lands - left.Z, slope = -length * (1 - g.Duty) / span;
                float along = -2 * s * s * s + 3 * s * s + slope * (2 * s * s * s - 3 * s * s + s);
                at = new Vector3(x, Mathf.Lerp(left.Y, WolfShape.Sole, Mathf.SmoothStep(0, 1, s)) + (hind ? g.HindLift : g.FrontLift) * fold, left.Z + span * along);
                // The wrist flips the forepaw up behind as it leaves the ground; the hock lifts the hind paw back and up.
                lower = hind ? Mathf.Lerp(-28, 6, s) - 40 * fold : Mathf.Lerp(-14, 16, s) - g.Fold * fold;
                // The toes come level again before the paw lands, so they clear the ground.
                paw = Mathf.Lerp(-PushOff, 0, Mathf.SmoothStep(0, .6f, s)) + (hind ? -25 : -45) * fold;
            }
            body = body.WithLeg(leg, ReachTo(halves, leg, at, lower, paw));
        }
        return body;
    }
    /// <summary>How far, in degrees, a paw rolls forward onto its toes as it pushes off.</summary>
    private const float PushOff = 30;

    /// <summary>Where the paw bone is with the toe pads at <paramref name="toes"/> and the paw rolled <paramref name="pitch"/> degrees forward of flat.</summary>
    private static Vector3 Rolled(Vector3 toes, float pitch) => toes - new Basis(Vector3.Right, -Mathf.DegToRad(pitch)) * new Vector3(0, -WolfShape.Sole, ToePad);

    /// <summary>
    /// Braking while the body still faces away from where the wolf moves, over <paramref name="distance"/> tiles this frame:
    /// the forelegs braced out ahead and the paws sliding, the hindquarters dropped under, the head up.
    /// </summary>
    public void Brake(float distance, float dt) {
        if (dt > 0) speed = Mathf.Lerp(speed, Math.Abs(distance) / dt, 1 - MathF.Exp(-dt * 8));
        var body = new WolfPose(Lift: -.06f, Pitch: -10, Flex: 6, Neck: -6, Head: -14, Ears: 20, Tail: -40, Curl: 4);
        Show(WolfMotion.Brake, Planted(body, 1.6f, .06f, BrakeReach), dt);
    }
    /// <summary>
    /// Turns the wolf toward <paramref name="facing"/>, the head first and the body after it, leaning into the turn when it
    /// <paramref name="moved"/> that far this frame. <paramref name="galloping"/>, it turns fast; returns whether it then brakes,
    /// running but with its body still facing away from where it goes, as just after it reverses. Slower, the body takes about
    /// a second to come round after the head, curled into the turn. A wolf <paramref name="backing"/> off steps backward on purpose.
    /// </summary>
    public bool TurnToward(Vector2 facing, Vector2 moved, float dt, bool running, bool galloping, bool backing) {
        Turning.Step(ref Yaw, ActorAnimation.Yaw(facing, Yaw), running && dt > 0 ? moved.Length() / dt : 0, dt, galloping, galloping ? Turning.BodyRate : SlowTurn);
        Root.Rotation = new Vector3(0, Yaw, Turning.Bank);
        return galloping && running && !backing && moved.Dot(ActorAnimation.Forward(Yaw)) < 0;
    }

    /// <summary>How quickly a wolf's body turns at a walk or a trot, as a spring's rate a second: nine tenths of a turn in about a second.</summary>
    private const float SlowTurn = 4;
    /// <summary>
    /// How far short of the way it has to go a wolf at a walk or a trot may face and still set off: facing further away, it
    /// turns round where it stands first. The cosine of 75 degrees.
    /// </summary>
    public const float SetsOffWithin = .26f;
    /// <summary>
    /// How far behind its middle a wolf's hips are, which it turns about on the spot; the metres its forepaws step for each
    /// radian of such a turn; and how fast, in radians a second, it has to turn to step round rather than stand.
    /// </summary>
    public const float HipsBehind = .3f, TurnStep = .45f, StepsRoundFrom = .5f;
    /// <summary>The share of its pace a wolf adds to catch up with its place in the world after turning round or letting go.</summary>
    public const float CatchUp = .6f;
    /// <summary>
    /// How far a wolf that <paramref name="moved"/> that far in the world this frame closes the gap to its place there: by
    /// <see cref="CatchUp"/> of its pace, but a wolf walking in the world catches up no faster than it can still walk, so it
    /// never breaks into a trot to do it.
    /// </summary>
    public static float CatchingUp(float moved, float dt) =>
        moved >= dt * WalkTop ? moved * CatchUp : Math.Min(moved * CatchUp, dt * WalkTop - moved);
    /// <summary>The fastest a wolf walks, in tiles a second, a little under where it breaks into a trot.</summary>
    private const float WalkTop = 2;
    /// <summary>
    /// The share of the head's turn ahead of the body that the spine curls into the turn by, and that the tail swings into
    /// it by: a turning wolf bends along its length, nose toward tail, as a dog walking a circle does.
    /// </summary>
    private const float CurlShare = .7f, TailShare = .5f;

    /// <summary>How far ahead of home, in metres, a braking wolf braces its forepaws.</summary>
    private const float BrakeReach = .12f;
    /// <summary>The share of the head's turn ahead of the body that the neck takes; the head takes the rest.</summary>
    internal const float NeckShare = .6f;

    // ---------- Standing, leaping and feeding ----------

    /// <summary>Standing: slow breaths, weight shifting between the hind legs, the head looking about; wary crouches low with the head down and the lips drawn back.</summary>
    public void Stand(float time, float dt, bool wary) => Show(WolfMotion.Stand, Standing(time, wary), dt, wary);

    private static WolfPose Standing(float time, bool wary) {
        float breath = MathF.Sin(time * 2.4f), look = MathF.Sin(time * .37f), settle = MathF.Sin(time * .23f);
        var body = wary
            ? new WolfPose(Lift: -.08f + .004f * breath, Pitch: 4, Neck: 30 + breath, Head: -26, NeckTurn: look * 4, Jaw: 9 + 2 * breath, Ears: -10, Snarl: 1, Tail: -82, Wag: MathF.Sin(time * .9f) * 3)
            : new WolfPose(Lift: .004f * breath, Shift: .008f * settle, Roll: -1.2f * settle, Neck: 6 + 4 * MathF.Sin(time * .55f), NeckTurn: look * 16 + MathF.Sin(time * 1.3f) * 4,
                Head: -6 + breath, HeadTurn: look * 10, HeadTilt: look * 4, Jaw: 2 + 2 * Math.Max(0, MathF.Sin(time * 7)), Ears: 4 + 6 * Math.Max(0, MathF.Sin(time * .9f + 1)),
                Tail: -66 + 3 * MathF.Sin(time * .8f), Wag: 8 * MathF.Sin(time * 1.1f), Curl: 8 + 4 * MathF.Sin(time * 1.1f + .8f));
        return Planted(body, wary ? 1.15f : 1, settle * .015f);
    }

    /// <summary>
    /// Braced, standing its ground square to whoever walks at it: sunk low over forepaws set wide and a little ahead, its
    /// weight back on bent hind legs, the neck down and the head level, ears flat, lips drawn back off the teeth, the tail
    /// held stiff. Its sides heave and the jaw trembles.
    /// </summary>
    public void Brace(float time, float dt) => Show(WolfMotion.Brace, Bracing(time), dt);

    private static WolfPose Bracing(float time) {
        float breath = MathF.Sin(time * 3.4f), tremble = MathF.Sin(time * 19);
        var body = new WolfPose(Lift: -.11f + .005f * breath, Surge: -.05f, Pitch: 5, Flex: 5, Neck: 36 + breath, Head: -32, Jaw: 12 + 2 * breath + tremble,
            Ears: 34, Snarl: 1, Tail: -46, Wag: tremble, Curl: -6);
        return Planted(body, 1.35f, 0, frontAhead: .1f, hindSpread: 1.15f);
    }

    /// <summary>
    /// A snap at a player. Gathering itself, as <paramref name="wind"/> runs 0 to 1, the wolf sinks back onto its haunches,
    /// back rounded, head low and lips drawn. As <paramref name="drive"/> runs 0 to 1 it drives off its hind legs and throws
    /// chest, neck and wide jaws out ahead, the forepaws reaching for the ground under them; the jaws close as
    /// <paramref name="shut"/> runs 0 to 1 once it has landed. Its jaws close <paramref name="jawsAhead"/> ahead of its place, which
    /// <see cref="SnapReach"/> gives for a player.
    /// </summary>
    public void Snap(float wind, float drive, float shut, float dt, float jawsAhead) =>
        Show(WolfMotion.Snap, Snapping(wind, drive, shut, Math.Clamp(jawsAhead - SnapBite, -.1f, MostSurge)), dt);

    private static WolfPose Snapping(float wind, float drive, float shut, float surge) {
        float g = Mathf.SmoothStep(0, 1, wind), d = Mathf.SmoothStep(0, 1, drive), flown = Math.Clamp(drive, 0, 1), air = MathF.Sin(MathF.PI * flown);
        // Off the ground the body rises and falls under gravity.
        float hop = Movement.Rise(flown * WolfSnap.Flight, WolfSnap.Flight);
        // From the wary stand it stalks in, to gathered, to stretched out at the end of the leap.
        float Through(float wary, float gathered, float struck) => Mathf.Lerp(Mathf.Lerp(wary, gathered, g), struck, d);
        var body = new WolfPose(Lift: Through(-.08f, -.15f, -.05f) + hop, Surge: Through(0, -.1f, surge), Pitch: Through(4, -2, 3), Flex: Through(0, 12, -10),
            Neck: Through(30, 26, 14), Head: Through(-26, -18, -12), Jaw: Through(9, 14, 34 - 30 * Mathf.SmoothStep(0, 1, shut)), Ears: Through(-10, 34, 34), Snarl: 1,
            Tail: Through(-82, -74, -40), Curl: -4);
        var halves = new Halves(body);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            // The hind paws push off where they stand; the forepaws lift and come down under the chest where it lands.
            var at = new Vector3(WolfShape.Pivot(leg).X * Through(1.15f, 1.3f, 1), WolfShape.Sole + (hind ? 0 : .1f * air), Home(leg) + (hind ? 0 : surge * d));
            body = body.WithLeg(leg, Reach(halves, leg, at, hind ? 2 - 20 * d : 10 - 40 * air, hind ? -24 * d : -20 * air));
        }
        return body;
    }

    /// <summary>How far ahead of its place a snapping wolf's jaws close at the end of the snap with its body not thrown forward, and how far forward it throws its body at most.</summary>
    private static readonly float SnapBite = BiteOf(Snapping(1, 1, 1, 0)).Z;
    private const float MostSurge = .3f;

    /// <summary>
    /// How far ahead of a snapping wolf placed at <paramref name="wolf"/> its jaws close on a player standing at
    /// <paramref name="player"/> facing <paramref name="yaw"/>: on the near side of their body, round their hips and thighs.
    /// </summary>
    public static float SnapReach(Vector2 wolf, Vector2 player, float yaw) {
        var toWolf = wolf - player;
        float apart = toWolf.Length();
        if (apart < 1e-4f) return 0;
        var forward = ActorAnimation.Forward(yaw);
        float ahead = toWolf.Dot(forward) / apart / PlayerDepth, aside = toWolf.Dot(new Vector2(forward.Y, -forward.X)) / apart / PlayerWidth;
        return apart - 1 / MathF.Sqrt(ahead * ahead + aside * aside);
    }
    /// <summary>How far out from a standing player's middle, at a wolf's bite height, their thighs and hips reach to the front and back, and to the sides.</summary>
    private const float PlayerDepth = .09f, PlayerWidth = .17f;

    /// <summary>
    /// Howling: the wolf braces on all four paws, lifts its chest and throws its head back with the muzzle to the sky, the jaw opening as the note swells and closing as it fades.
    /// <paramref name="progress"/> runs 0 to 1 over the howl.
    /// </summary>
    public void Howl(float progress, float dt) {
        float p = Math.Clamp(progress, 0, 1), up = Mathf.SmoothStep(0, .2f, p) * (1 - Mathf.SmoothStep(.85f, 1, p));
        float note = MathF.Sin(MathF.PI * Math.Clamp((p - .12f) / .76f, 0, 1));
        var body = new WolfPose(Lift: .02f * up, Pitch: -12 * up, Neck: 6 - 50 * up, Head: -6 - 40 * up, HeadTilt: 3 * note, Jaw: 2 + 22 * note * up,
            Ears: 14 * up, Tail: -66 - 10 * up, Curl: 8 - 6 * up);
        Show(WolfMotion.Howl, Planted(body, 1.05f, .04f * up), dt);
    }

    /// <summary>
    /// All four paws flat on the ground at home, spread by <paramref name="spread"/>, the hind ones by <paramref name="hindSpread"/>
    /// when given; <paramref name="shift"/> moves the hind paws forward.
    /// </summary>
    private static WolfPose Planted(WolfPose body, float spread, float shift = 0, float frontAhead = 0, float frontUp = 0, float hindSpread = 0) {
        var halves = new Halves(body);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            var at = new Vector3(WolfShape.Pivot(leg).X * (hind && hindSpread > 0 ? hindSpread : spread), WolfShape.Sole + (hind ? 0 : frontUp), Home(leg) + (hind ? shift * (leg == 2 ? 1 : -1) : frontAhead));
            body = body.WithLeg(leg, ReachTo(halves, leg, at, hind ? 2 : 10, 0));
        }
        return body;
    }

    /// <summary>
    /// The leap at a standing player's throat, from the push off at <paramref name="progress"/> 0 to the moment the jaws reach it at <paramref name="contact"/>.
    /// The hind legs drive while the forelegs fold up, then reach out for the chest, landing at <paramref name="chest"/> in the model when given, with the jaws wide.
    /// Reaching, it rears up so its jaws come to <paramref name="jawsUp"/> metres off the ground when given, where the throat is.
    /// </summary>
    public void Leap(float progress, float contact, float dt, (Vector3 Left, Vector3 Right)? chest = null, float? jawsUp = null) => Show(WolfMotion.Leap, Leaping(progress, contact, chest, jawsUp), dt);

    /// <summary>A leaping wolf's pose, <paramref name="progress"/> of the way through the leap, with its jaws reaching their mark at <paramref name="contact"/>.</summary>
    private static WolfPose Leaping(float progress, float contact, (Vector3 Left, Vector3 Right)? chest = null, float? jawsUp = null) {
        float t = Math.Clamp(progress / Math.Max(contact, .05f), 0, 1);
        // It rises out of its crouch onto straightening hind legs as it pushes off, faster as it goes, and leaves the ground at full stretch.
        float drive = Math.Min(1, t / PushShare), push = Mathf.SmoothStep(0, 1, drive), air = Mathf.SmoothStep(PushShare, 1, t), reach = Mathf.SmoothStep(.4f, 1, t);
        var body = new WolfPose(Lift: -.06f + LeapRise * push, Pitch: -12 * push - 22 * air, Flex: Mathf.Lerp(8, -12, air),
            Neck: Mathf.Lerp(10, 16, air), Head: Mathf.Lerp(-8, 8, reach), Jaw: 8 + 26 * reach, Ears: Mathf.Lerp(0, 22, air), Snarl: 1, Tail: Mathf.Lerp(-50, -10, air), Curl: -8);
        if (jawsUp is float high) body = body with { Pitch = Mathf.Lerp(body.Pitch, RearedTo(body, high, MostNoseDown).Pitch, reach) };
        var halves = new Halves(body);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            float x = WolfShape.Pivot(leg).X;
            // Hind paws stay where they push off until they leave the ground, then trail out behind; forepaws tuck under the chest and then reach up and forward.
            var at = hind ? new Vector3(x, WolfShape.Sole + .3f * air, Mathf.Lerp(-.3f, -.78f, air))
                : new Vector3(x, Mathf.Lerp(WolfShape.Sole, .45f, push) + .55f * reach, Mathf.Lerp(.36f, .3f, push) + .45f * reach);
            float lower = hind ? Mathf.Lerp(-10, -70, air) : Mathf.Lerp(10, -90, push * (1 - reach)) + 60 * reach;
            // Reaching out, the forepaws come down onto the player's chest as the jaws reach the throat.
            if (!hind && chest is var (left, right)) at = at.Lerp(leg == 0 ? left : right, reach);
            body = body.WithLeg(leg, ReachTo(halves, leg, at, lower, hind ? lower - 30 * air : lower - 20));
        }
        return body;
    }
    /// <summary>The share of a leap up to the moment the jaws reach their mark that the wolf spends pushing off, and how far, in metres, it rises out of its crouch as it does.</summary>
    private const float PushShare = .3f, LeapRise = .08f;

    /// <summary>
    /// Where a leaping wolf's model stands <paramref name="flight"/> of the way from where it set off, <paramref name="from"/>, to
    /// where its jaws reach their mark, <paramref name="to"/>, <paramref name="seconds"/> after it set off: it speeds up from a
    /// stand as it pushes off along the ground, rising as its legs straighten, then flies at an even pace, rising and falling
    /// under gravity onto the mark.
    /// </summary>
    public static Vector3 LeapPath(Vector3 from, Vector3 to, float flight, float seconds) {
        float f = Math.Clamp(flight, 0, 1);
        var at = from.Lerp(to, Movement.Ramped(f, PushShare) / (1 - PushShare / 2));
        float pushing = PushShare * seconds, air = (1 - PushShare) * seconds;
        if (pushing + air <= 0) return to;
        // It leaves the ground as fast upward as the flight needs to come down on the mark, gathering that speed over the push.
        float rise = (to.Y - from.Y + Movement.Gravity * air * air / 2) / (pushing / 2 + air), t = f * seconds;
        float up = rise * Movement.Ramped(t, pushing) - (t < pushing ? 0 : Movement.Gravity * (t - pushing) * (t - pushing) / 2);
        return at with { Y = from.Y + up };
    }

    /// <summary>
    /// Two spots in the model for a wolf's forepaws, as its left and its right: each paw takes the one on its own side, so
    /// the forelegs never cross.
    /// </summary>
    public static (Vector3 Left, Vector3 Right) Uncrossed(Vector3 a, Vector3 b) => a.X >= b.X ? (a, b) : (b, a);

    /// <summary>
    /// Feeding at a downed player: braced over the body, tearing with sharp pulls back.
    /// A pinning wolf stands astride the player with its forepaws on the chest, at <paramref name="chest"/> in the model when given, and its hind paws wide of their legs, or, having <paramref name="lunged"/> onto the throat from beside them, with its forepaws by their shoulder;
    /// <paramref name="down"/> runs 0 to 1 as the player goes down under it.
    /// </summary>
    /// <paramref name="jawsUp"/>, when given, is how high off the ground a pinning wolf's jaws hold the throat, which it rears up on
    /// its hind legs to reach as the player goes down; <paramref name="jaws"/>, where in the model they hold it once the player lies.
    public void Feed(float time, float dt, bool pinning, float down = 1, bool lunged = false, (Vector3 Left, Vector3 Right)? chest = null, float? jawsUp = null, Vector3? jaws = null) {
        WolfPose body;
        if (pinning) {
            float pull = MathF.Pow(Math.Max(0, MathF.Sin(time * 2.6f)), 3), shake = MathF.Sin(time * 13) * (1 - pull * .6f), bite = Math.Max(0, MathF.Sin(time * 4.5f));
            // Riding the player down: high and nose up while they still stand, then low over them with the jaws at the throat.
            float up = 1 - Mathf.SmoothStep(0, 1, down);
            body = new WolfPose(Lift: -.05f - .02f * pull, Pitch: 14 - 34 * up - 4 * pull, Surge: -.03f * pull, Flex: 4,
                Neck: 52 - 60 * up - 12 * pull, Head: 14 + 4 * shake, HeadTurn: 12 * shake, HeadTilt: 6 * shake, Jaw: 10 + 6 * bite, Ears: 26, Snarl: 1, Tail: -70 + 6 * MathF.Sin(time * 1.7f), Wag: 8 * MathF.Sin(time * 2.1f));
            // It rears up on its hind legs to hold the throat of a player still on their feet, and leans its body to keep its jaws on it once they lie.
            if (jawsUp is float high) body = RearedTo(body, high, MostNoseDown);
            if (jaws is Vector3 hold)
                for (int pass = 0; pass < 3; pass++) {
                    var off = hold - BiteOf(body);
                    body = body with { Shift = Math.Clamp(body.Shift + off.X, -MostLean, MostLean), Lift = body.Lift + off.Y, Surge = Math.Clamp(body.Surge + off.Z, -MostLean, MostLean) };
                }
            // Beside the player it keeps its forepaws on the ground by their shoulder.
            body = lunged ? Planted(body, 1.4f, 0, .05f) : Planted(body, 2.4f, 0, .05f, ChestTop + .5f * up, PinStance);
            // Astride, its forepaws stay on the player's chest as they go down under it and once they lie.
            if (!lunged && chest is var (left, right)) {
                float lower = Mathf.Lerp(10, 50, up);
                var halves = new Halves(body);
                body = body.WithLeg(0, ReachTo(halves, 0, left, lower, lower - 20)).WithLeg(1, ReachTo(halves, 1, right, lower, lower - 20));
            }
        } else body = Tearing(time);
        Show(pinning ? WolfMotion.Pin : WolfMotion.Feed, body, dt);
    }

    /// <summary>How far apart a pinning wolf's hind paws stand, as a share of its legs' own width, wide of the legs of the player under it, and how far it leans its body to keep its jaws on the throat.</summary>
    private const float PinStance = 2.5f, MostLean = .15f;

    /// <summary>A feeder grips low, rocks back on its haunches, then lets go and dips for another bite.</summary>
    private static WolfPose Tearing(float time) {
        float beat = time / 1.35f, u = Mathf.PosMod(beat, 1);
        float pull = Mathf.SmoothStep(.16f, .39f, u) * (1 - Mathf.SmoothStep(.48f, .84f, u));
        float open = Mathf.SmoothStep(.48f, .61f, u) * (1 - Mathf.SmoothStep(.78f, .96f, u));
        float wrench = pull * MathF.Sin(beat * Mathf.Pi);
        var body = new WolfPose(Lift: -.07f - .045f * pull, Pitch: 12 - 12 * pull, Surge: -.11f * pull,
            Flex: 6 + 10 * pull, Bend: 8 * wrench, Neck: 46 - 23 * pull, Head: 18 + 6 * pull,
            HeadTurn: 16 * wrench, HeadTilt: 12 * wrench, Jaw: 6 + 19 * open, Ears: 22, Snarl: 1,
            Tail: -74 + 12 * pull, Wag: -14 * wrench);
        return Planted(body, 1.25f, 0, .1f);
    }

    /// <summary>Where a feeding wolf's jaws close, in the model, between its pulls.</summary>
    public static readonly Vector3 FeedingBite = BiteOf(Tearing(0));

    /// <summary>
    /// Tugging a player back by the waist like a dog at tug of war, low on bent hind legs with the forelegs braced wide
    /// either side of the player's feet. Each tug it throws its weight back: the forelegs straighten and push against the ground, the chest
    /// rears and the head wrenches to one side, the next tug to the other, while its paws stay where they stand until the
    /// forepaws hop back at the end of the heave; between tugs the hind paws step back after it and it shakes its head.
    /// <paramref name="progress"/> runs 0 to 1 over the tug, timed as <see cref="DoorTug.TugProgress"/> moves the player and the
    /// view holds the jaws at their waist, <paramref name="jawsUp"/> metres off the ground, which the wolf lifts or lowers its neck to reach.
    /// </summary>
    public void Tug(float progress, float time, float dt, float jawsUp) {
        var (tug, u) = DoorTug.TugPhase(progress);
        float heave = DoorTug.HeavePulse(u), shake = u < DoorTug.Heave ? 0 : MathF.Sin(time * 15) * Mathf.SmoothStep(DoorTug.Heave, DoorTug.Heave + .15f, u);
        // Each tug wrenches the other way.
        float side = tug % 2 == 0 ? 1 : -1;
        var body = Tugging(heave, side, shake);
        for (int pass = 0; pass < 3; pass++) body = body with { Neck = body.Neck - (jawsUp - BiteOf(body).Y) / NeckRise };
        // How far the player, and so the jaws, have come back in this tug: planted paws come that far forward against the body.
        float moved = DoorTug.TugDistance * (DoorTug.TugProgress(progress) - (float)tug / DoorTug.Tugs);
        // The jaws hold still at the waist, so a body that rears back carries its paws with it unless they make up the difference.
        var drift = BiteOf(body) - TuggingBite;
        var halves = new Halves(body);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            // The forepaws hop back to where they started once the heave has moved the player most of the way; each hind paw steps back after it in turn.
            float leaves = hind ? leg == 2 ? .45f : .65f : HopFrom, lands = hind ? leaves + .2f : HopTo;
            float back = Mathf.SmoothStep(leaves, lands, u), lift = (hind ? .04f : .06f) * MathF.Sin(MathF.PI * Math.Clamp((u - leaves) / (lands - leaves), 0, 1));
            // The forepaws straddle the player's feet, which step back between them.
            var at = new Vector3(WolfShape.Pivot(leg).X * (hind ? 1.3f : 3.2f), WolfShape.Sole + lift, Home(leg) + (hind ? -.08f : .05f) + moved * (1 - back)) + (drift with { Y = 0 });
            // Braced forelegs slant forward, straighter as they push.
            body = body.WithLeg(leg, Reach(halves, leg, at, hind ? 6 : 24 - 10 * heave, hind ? 0 : -12));
        }
        Show(WolfMotion.Tug, body, dt);
    }

    /// <summary>About how far a degree less of neck bend lifts a wolf's jaws, in metres.</summary>
    private const float NeckRise = .006f;

    /// <summary>Shares of a tug between which a tugging wolf's forepaws hop back, once the heave has moved the player about half way.</summary>
    private const float HopFrom = .18f, HopTo = .34f;

    /// <summary>Where a tugging wolf's jaws close between heaves, in the model; which way the next heave wrenches makes no difference at rest.</summary>
    private static readonly Vector3 TuggingBite = BiteOf(Tugging(0, 1, 0));

    /// <summary>A tugging wolf's body: <paramref name="heave"/> 0 to 1 through a tug, wrenching toward <paramref name="side"/>, and its head shaking.</summary>
    private static WolfPose Tugging(float heave, float side, float shake) =>
        new(Lift: -.06f - .1f * heave, Pitch: -4 - 16 * heave, Roll: 7 * side * heave, Surge: -.1f * heave, Flex: 8 + 12 * heave, Bend: 14 * side * heave,
            Neck: 8 - 18 * heave, Head: 6 + 8 * heave, HeadTurn: 22 * side * heave + 12 * shake, HeadTilt: 14 * side * heave + 10 * shake, Jaw: 10, Ears: -18 - 8 * heave, Snarl: 1,
            Tail: -28 + 12 * heave, Wag: 6 * shake - 12 * side * heave, Curl: 6);

    /// <summary>
    /// The body of a wolf reared up on its hind legs with its jaws closed on something <paramref name="jaws"/> metres off the
    /// ground, such as the arm of a player another wolf tugs: the higher that is, the steeper it rears. It worries what it
    /// holds, its head shaking and wrenching back as <paramref name="time"/> runs on.
    /// </summary>
    internal static WolfPose Rearing(float jaws, float time) {
        float shake = MathF.Sin(time * 14), wrench = MathF.Pow(Math.Max(0, MathF.Sin(time * 5.2f)), 2);
        var body = new WolfPose(Lift: -.03f, Pitch: -45, Flex: -4 + 4 * wrench, Surge: -.02f * wrench, Neck: 8 - 8 * wrench, Head: 24, HeadTurn: 9 * shake, HeadTilt: 10 * shake,
            Jaw: 13, Ears: -22, Snarl: 1, Tail: -34 + 8 * wrench, Wag: 10 * MathF.Sin(time * 6), Curl: 8);
        return RearedTo(body, jaws, ShallowestRear);
    }

    /// <summary>
    /// A body pitched up or down so its jaws come to <paramref name="jawsUp"/> metres off the ground, reared no steeper than
    /// <see cref="SteepestRear"/> and tipped no further nose down than <paramref name="shallowest"/> degrees.
    /// </summary>
    private static WolfPose RearedTo(WolfPose body, float jawsUp, float shallowest) {
        for (int pass = 0; pass < 4; pass++) body = body with { Pitch = Math.Clamp(body.Pitch - (jawsUp - BiteOf(body).Y) / RiseADegree, SteepestRear, shallowest) };
        return body;
    }
    /// <summary>
    /// About how far a degree more of rearing lifts a wolf's jaws, in metres; the pitches a wolf holding something rears
    /// between; and the furthest a leaping or pinning wolf tips nose down to bring its jaws onto a throat.
    /// </summary>
    private const float RiseADegree = .011f, SteepestRear = -68, ShallowestRear = -12, MostNoseDown = 30;

    /// <summary>
    /// Shows a wolf reared up as <paramref name="body"/> has it, from <see cref="Rearing"/>: standing on its hind paws with
    /// its forepaws on what it holds, at <paramref name="paws"/> in the model.
    /// </summary>
    public void Rear(WolfPose body, (Vector3 Left, Vector3 Right) paws, float dt) {
        var halves = new Halves(body);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            // The hind paws stand apart under the hips, a little behind where they stand on all fours; the forelegs reach up and forward.
            var at = hind ? new Vector3(WolfShape.Pivot(leg).X * 1.5f, WolfShape.Sole, Home(leg) - .06f) : leg == 0 ? paws.Left : paws.Right;
            body = body.WithLeg(leg, hind ? ReachTo(halves, leg, at, -8, 0) : Reach(halves, leg, at, 55, 30));
        }
        Show(WolfMotion.Rear, body, dt);
    }

    /// <summary>
    /// Struck: the head snaps away from the blow and the wolf hops back off its forelegs, lands hind paws first and ducks low with its ears flat and tail tucked, then rises wary.
    /// <paramref name="progress"/> runs 0 to 1 over the recoil; the view moves the wolf back only while it is in the air, the first HopShare of it.
    /// </summary>
    public void Recoil(float progress, float dt) {
        // In the air it rises and falls under gravity.
        float t = Math.Clamp(progress, 0, 1), hop = t / HopShare, air = t < HopShare ? 4 * hop * (1 - hop) : 0, rise = t < HopShare ? Movement.Rise(hop * HopSeconds, HopSeconds) : 0;
        float snap = Mathf.SmoothStep(0, .08f, t) * (1 - Mathf.SmoothStep(.15f, .55f, t));
        // The duck eases into the wary stand the wolf goes on to, rather than out of it.
        // It ducks once it has landed.
        float duck = Mathf.SmoothStep(HopShare, HopShare + .2f, t) * (1 - .45f * Mathf.SmoothStep(.7f, 1, t)), cower = Math.Max(snap, duck);
        var body = new WolfPose(Lift: rise - .15f * duck, Pitch: -6 * air + 6 * duck, Roll: 6 * snap, Bend: -12 * snap, Flex: 10 * air + 4 * duck,
            Neck: -16 * snap + 40 * duck, Head: -10 * snap - 30 * duck, HeadTurn: -36 * snap, HeadTilt: -14 * snap, Jaw: 24 * snap + 12 * duck,
            Ears: 45 * cower, Snarl: cower, Tail: -62 - 36 * cower, Curl: -30 * cower);
        var halves = new Halves(body);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            // In the air the hind paws reach back for the landing and the forepaws trail where they pushed off; landed, the legs spread to brace the crouch.
            var at = new Vector3(WolfShape.Pivot(leg).X * (1 + .2f * duck), WolfShape.Sole + rise + .05f * MathF.Sin(MathF.PI * Math.Min(1, hop)), Home(leg) + (hind ? -.1f : .07f) * air + (hind ? 0 : .05f * duck));
            body = body.WithLeg(leg, Reach(halves, leg, at, hind ? 2 - 24 * air : 10 - 50 * air, hind ? 0 : -30 * air));
        }
        Show(WolfMotion.Recoil, body, dt);
    }

    /// <summary>
    /// A killed wolf is thrown off its feet, twisting onto its right side in the air, and slams down with a bounce that whips the head and legs.
    /// It then lies limp, legs stretched out and the upper ones resting on the lower, as a dog lies on its side.
    /// <paramref name="progress"/> runs 0 to 1 over FallSeconds; it starts from the pose <see cref="FallFrom"/> hands it, or a stand.
    /// Thrown, it drops under gravity from where it stood to lying on its side.
    /// </summary>
    public void Fall(float progress) {
        float p = Math.Clamp(progress, 0, 1), fly = Math.Min(1, p / ThrowShare), down = Mathf.SmoothStep(0, 1, fly), air = MathF.Sin(MathF.PI * fly);
        float thrown = ThrowShare * FallSeconds, drop = -LyingDrop * fly + Movement.Rise(fly * thrown, thrown);
        float after = Math.Max(0, (p - ThrowShare) / (1 - ThrowShare)), bounce = after < .3f ? MathF.Sin(MathF.PI * after / .3f) : 0;
        // The head lags the body and hits the ground a moment after it.
        float slam = p < ThrowShare ? 0 : Mathf.SmoothStep(0, .25f, after), kick = after is > .55f and < .8f ? MathF.Sin(MathF.PI * (after - .55f) / .25f) : 0;
        // Rolled onto its side the body sinks until its flank lies on the ground.
        var body = new WolfPose(Lift: drop + .07f * bounce, Pitch: -14 * air, Roll: 90 * down + 6 * bounce, Shift: -.05f * down,
            Flex: -12 * air - 4 * down, Bend: 6 * down, Neck: -34 * air + 10 * bounce - 8 * slam, NeckTurn: -12 * slam, Head: 12 * air, HeadTilt: -10 * slam,
            Jaw: 26 * air + 14 * down, Ears: 30 * down, Tail: Mathf.Lerp(-65, -6, down) + 30 * air, Curl: -10 * down);
        var standing = fallFrom ?? Planted(new WolfPose(), 1);
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2, upper = leg % 2 == 0;
            // Knocked off its feet the legs buckle and fold under; on the ground the forelegs stretch forward and the hind legs back, the upper pair drooping onto the lower.
            var flung = hind ? new WolfLeg(45, -100, 70, 20) : new WolfLeg(-35, 30, -90, -20);
            var limp = hind ? new WolfLeg(-30 - 14 * kick, -20, 18 + 10 * kick, 25, upper ? -12 : 4) : new WolfLeg(32, 16, -10, -25, upper ? -12 : 4);
            var at = WolfLeg.Lerp(WolfLeg.Lerp(standing.Leg(leg), limp, down), flung, .8f * air);
            body = body.WithLeg(leg, at with { Upper = at.Upper + (hind ? -14 : 14) * bounce });
        }
        // The body comes out of the pose it was killed in over the first part of the throw.
        if (fallFrom is WolfPose was) body = WolfPose.Lerp(was, body, Mathf.SmoothStep(0, .5f, fly)) with { FrontLeft = body.FrontLeft, FrontRight = body.FrontRight, HindLeft = body.HindLeft, HindRight = body.HindRight };
        // No paw goes into the ground, and in the air they all clear it.
        float lowest = float.MaxValue;
        for (int leg = 0; leg < 4; leg++) lowest = Math.Min(lowest, Lowest(PawFrame(body, leg)));
        float clear = p < ThrowShare ? ThrownClear * air : 0;
        if (lowest < clear) body = body with { Lift = body.Lift + clear - lowest };
        motion = WolfMotion.Fall; blend = 1; shown = body;
        Apply(body);
    }

    /// <summary>The pose a killed wolf's model showed, which its carcass's fall starts from.</summary>
    private WolfPose? fallFrom;
    /// <summary>Starts this carcass's fall from the pose <paramref name="dying"/>, the wolf's model as it was killed, shows.</summary>
    public void FallFrom(WolfModel dying) => fallFrom = dying.shown;
    /// <summary>How far a killed wolf's body drops from standing to lying on its side, and how far its paws clear the ground while it is thrown.</summary>
    private const float LyingDrop = .5f, ThrownClear = .03f;

    /// <summary>Where a paw bone is, in the model, under the pose shown; it stands WolfShape.Sole above the ground when the paw is planted.</summary>
    public Vector3 Paw(int leg) => PawFrame(shown, leg).Origin;

    /// <summary>Where a paw bone is, in the model, under a pose, and how it is turned.</summary>
    private static Transform3D PawFrame(WolfPose pose, int leg) => PawFrame(Frame(pose, leg >= 2), pose.Leg(leg), leg);

    /// <summary>Where a paw bone is, in the model, and how it is turned, with its leg at <paramref name="l"/> from the chest or pelvis at <paramref name="half"/>.</summary>
    private static Transform3D PawFrame(Transform3D half, WolfLeg l, int leg) {
        int bone = WolfShape.Leg(leg);
        return half * Posed(bone, Vector3.Zero, Turn(-l.Upper, 0, l.Splay)) * Posed(bone + 1, Vector3.Zero, Turn(-l.Middle))
            * Posed(bone + 2, Vector3.Zero, Turn(-l.Lower)) * Posed(bone + 3, Vector3.Zero, Turn(-l.Paw));
    }

    /// <summary>
    /// The outline of the underside of a paw from the paw bone, back to front: the heel, the sole from the back pad to the
    /// toe pads, which is flat on the ground when the paw is, and the tip of the toes.
    /// </summary>
    private static readonly Vector3[] SoleOutline = {
        new(0, .005f, -.034f), new(0, -.015f, -.03f), new(0, -WolfShape.Sole, -.005f), new(0, -WolfShape.Sole, .03f), new(0, -WolfShape.Sole, ToePad), new(0, -.031f, .07f), new(0, -.023f, .074f) };
    /// <summary>How many points <see cref="SolePoint"/> gives along a paw's underside.</summary>
    public static int SolePoints => SoleOutline.Length;
    /// <summary>How far ahead of the paw bone the toe pads are, which a paw rolls onto as its heel lifts.</summary>
    private const float ToePad = .058f;

    /// <summary>A point on the underside of a paw, in the model, under the pose shown: 0 at the heel to <see cref="SolePoints"/> - 1 at the tip of the toes.</summary>
    public Vector3 SolePoint(int leg, int point) => PawFrame(shown, leg) * SoleOutline[point];

    /// <summary>How high the lowest point of the underside of a paw whose bone is at <paramref name="paw"/> is.</summary>
    private static float Lowest(Transform3D paw) {
        float lowest = float.MaxValue;
        foreach (var point in SoleOutline) lowest = Math.Min(lowest, (paw * point).Y);
        return lowest;
    }

    /// <summary>How many spheres <see cref="Volumes(Span{ValueTuple{Vector3, float}})"/> gives.</summary>
    public const int VolumeCount = 16;

    /// <summary>Spheres roughly filling the wolf's body, neck, head and legs, in the model, under the pose shown: centre and radius.</summary>
    public void Volumes(Span<(Vector3 At, float Radius)> into) => VolumesOf(shown, into);

    /// <summary>Spheres roughly filling a wolf's body, neck, head and legs, in the model, under a pose.</summary>
    public static void VolumesOf(WolfPose pose, Span<(Vector3 At, float Radius)> into) {
        Transform3D chest = Frame(pose, false), pelvis = Frame(pose, true);
        // The trunk from the rump to the front of the chest.
        into[0] = (pelvis * new Vector3(0, 0, -.12f), .14f);
        into[1] = (pelvis.Origin, .16f);
        into[2] = (pelvis.Origin.Lerp(chest.Origin, .5f), .16f);
        into[3] = (chest.Origin, .17f);
        into[4] = (chest * new Vector3(0, 0, .14f), .15f);
        var neck = chest * Posed(WolfShape.Neck, Vector3.Zero, Turn(pose.Neck, pose.NeckTurn));
        var bite = BiteOf(pose);
        into[5] = (neck.Origin, .1f);
        into[6] = (neck.Origin.Lerp(bite, .5f), .08f);
        into[7] = (bite, .06f);
        // Each leg from the shoulder or hip down: its middle and its paw.
        for (int leg = 0; leg < 4; leg++) {
            bool hind = leg >= 2;
            var top = (hind ? pelvis : chest) * (WolfShape.Pivot(leg) - WolfShape.Bones[hind ? WolfShape.Pelvis : WolfShape.Chest].At);
            var paw = PawFrame(hind ? pelvis : chest, pose.Leg(leg), leg).Origin;
            into[8 + leg * 2] = (top.Lerp(paw, .5f), .06f);
            into[9 + leg * 2] = (paw, .05f);
        }
    }

    /// <summary>The spheres of a feeding wolf between its pulls, in the model.</summary>
    public static readonly (Vector3 At, float Radius)[] FeedingVolumes = Feeding();
    private static (Vector3 At, float Radius)[] Feeding() {
        var volumes = new (Vector3 At, float Radius)[VolumeCount];
        VolumesOf(Tearing(0), volumes);
        return volumes;
    }

    /// <summary>Where the root of the neck and the jaws are, in the model, under the pose shown; a player pushing the wolf off takes hold between them.</summary>
    public (Vector3 Root, Vector3 Jaws) NeckLine => ((Frame(shown, false) * Posed(WolfShape.Neck, Vector3.Zero, Turn(shown.Neck, shown.NeckTurn))).Origin, BiteOf(shown));
    /// <summary>How far from the root of the neck to the jaws, 0 to 1, the back of the jaw is, where a player pushing the wolf's head off takes hold.</summary>
    public const float JawShare = .75f;

    /// <summary>Where the jaws close, in the model, under the pose shown.</summary>
    public Vector3 Bite => BiteOf(shown);

    /// <summary>Where the jaws close, in the model, under a pose.</summary>
    internal static Vector3 BiteOf(WolfPose pose) {
        var neck = Frame(pose, false) * Posed(WolfShape.Neck, Vector3.Zero, Turn(pose.Neck, pose.NeckTurn));
        var head = neck * Posed(WolfShape.Head, Vector3.Zero, Turn(pose.Head, pose.HeadTurn, pose.HeadTilt));
        return head * (WolfShape.Bite - WolfShape.Bones[WolfShape.Head].At);
    }

    // ---------- Blending and posing ----------

    /// <summary>Shows a pose worked out elsewhere, already stood on what its paws stand on, blending into it from the pose shown when the motion changes.</summary>
    public void Pose(WolfMotion motion, WolfPose pose, float dt) {
        Steps = null;
        Show(motion, pose, dt);
    }

    /// <summary>
    /// Stands the model upright the way it faces, with no knock-back or lean of its own, for something else to place:
    /// a tug, a hold on an arm, a lunge or a door check. <paramref name="stagger"/> is its wolf's, so a later hit shows.
    /// </summary>
    public void StandPlaced(float stagger) {
        KnockBack = Vector2.Zero; Stagger = stagger; Toward = Vector2.Zero;
        Turning.Hold(Yaw); Root.Rotation = new Vector3(0, Yaw, 0);
    }

    /// <summary>Where a wolf's head is, in the model, under a pose, and the way its muzzle points, which is the way it looks.</summary>
    private static (Vector3 From, Vector3 Along) Gaze(WolfPose pose) {
        var neck = Frame(pose, false) * Posed(WolfShape.Neck, Vector3.Zero, Turn(pose.Neck, pose.NeckTurn));
        var head = neck * Posed(WolfShape.Head, Vector3.Zero, Turn(pose.Head, pose.HeadTurn, pose.HeadTilt));
        return (head.Origin, head.Basis * (WolfShape.Bite - WolfShape.Bones[WolfShape.Head].At).Normalized());
    }

    /// <summary>A pose with its neck and head turned and its head raised or lowered so that it looks at a spot in the model.</summary>
    internal static WolfPose LookingAt(WolfPose pose, Vector3 spot) {
        for (int pass = 0; pass < 5; pass++) {
            var (from, along) = Gaze(pose);
            var want = (spot - from).Normalized();
            float turn = Mathf.RadToDeg(Mathf.AngleDifference(MathF.Atan2(along.X, along.Z), MathF.Atan2(want.X, want.Z))), lower = Mathf.RadToDeg(MathF.Asin(along.Y) - MathF.Asin(want.Y));
            pose = pose with { NeckTurn = pose.NeckTurn + NeckShare * turn, HeadTurn = pose.HeadTurn + (1 - NeckShare) * turn, Head = pose.Head + lower };
        }
        return pose;
    }

    /// <summary>A pose stood on the step of the cabin whose door point is <paramref name="door"/>, each paw on the tread or floor under it.</summary>
    private WolfPose OnSteps(WolfPose pose, Vector2 door) {
        Span<float> ground = stackalloc float[4];
        var root = Root.Transform;
        Transform3D chest = Frame(pose, false), pelvis = Frame(pose, true);
        for (int leg = 0; leg < 4; leg++) {
            var paw = PawFrame(leg >= 2 ? pelvis : chest, pose.Leg(leg), leg).Origin;
            var at = root * paw;
            ground[leg] = StairSteps.HeightAt(new Vector2(at.X, at.Z) - door, paw.Y - WolfShape.Sole > .01f);
        }
        return Stood(pose, ground);
    }

    /// <summary>
    /// A pose with each paw raised <paramref name="ground"/> metres, onto what it stands on, and the body lifted and tipped
    /// to stand over them, keeping each leg's lowest joints at their angles.
    /// </summary>
    internal static WolfPose Stood(WolfPose pose, ReadOnlySpan<float> ground) {
        Span<float> raised = stackalloc float[4];
        Span<Vector3> paws = stackalloc Vector3[4];
        float front = 0, hind = 0, frontDown = 0, hindDown = 0;
        var halves = new Halves(pose);
        for (int leg = 0; leg < 4; leg++) {
            paws[leg] = PawFrame(halves.Of(leg).At, pose.Leg(leg), leg).Origin;
            // A paw held well off the ground, such as one scratching at the door, stands on nothing.
            float down = 1 - Mathf.SmoothStep(HeldFrom, HeldBy, paws[leg].Y - WolfShape.Sole);
            raised[leg] = down * ground[leg];
            if (leg < 2) { front += down * raised[leg]; frontDown += down; } else { hind += down * raised[leg]; hindDown += down; }
        }
        // The chest stands over the forepaws that are down and the hips over the hind ones; with a pair in the air, over the other pair.
        if (frontDown > 0) front /= frontDown;
        if (hindDown > 0) hind /= hindDown;
        if (frontDown == 0) front = hind; else if (hindDown == 0) hind = front;
        if (front == 0 && hind == 0) return pose;
        var stood = pose with { Lift = pose.Lift + (front + hind) / 2, Pitch = pose.Pitch - Mathf.RadToDeg(MathF.Atan2(front - hind, Home(0) - Home(2))) };
        var standing = new Halves(stood);
        for (int leg = 0; leg < 4; leg++) {
            var (lower, paw) = Slant(halves.Of(leg), pose.Leg(leg));
            stood = stood.WithLeg(leg, ReachTo(standing, leg, paws[leg] + Vector3.Up * raised[leg], lower, paw));
        }
        return stood;
    }
    /// <summary>How high off the ground, in metres, a paw starts to count as held up rather than stepping, and counts as held up.</summary>
    private const float HeldFrom = .12f, HeldBy = .3f;

    /// <summary>
    /// Shows <paramref name="target"/> for <paramref name="next"/>, blending into it from the pose shown when the motion changes
    /// or turns <paramref name="wary"/> or calm. Only a wolf in sight has its skeleton posed.
    /// </summary>
    private void Show(WolfMotion next, WolfPose target, float dt, bool wary = false) {
        if (Steps is Vector2 door) target = OnSteps(target, door);
        // The head turns ahead of the body, the neck taking most of the turn, and the spine and tail curl after it.
        if (Turning.Look != 0) {
            float look = Mathf.RadToDeg(Turning.Look);
            target = target with { NeckTurn = target.NeckTurn + NeckShare * look, HeadTurn = target.HeadTurn + (1 - NeckShare) * look, Bend = target.Bend + CurlShare * look, Wag = target.Wag + TailShare * look };
        }
        // A glance turns only the neck and head, and with the turn no further from the body than a head turns.
        if (Glance != 0) {
            float most = Mathf.RadToDeg(MaxGlance), turned = Mathf.RadToDeg(Turning.Look);
            float glance = Math.Clamp(turned + Mathf.RadToDeg(Glance), -most, most) - Math.Clamp(turned, -most, most);
            target = target with { NeckTurn = target.NeckTurn + NeckShare * glance, HeadTurn = target.HeadTurn + (1 - NeckShare) * glance };
        }
        if (next != motion || wary != shownWary) {
            from = shown; blend = 0;
            blendSeconds = TraitsOf(next).BlendIn ?? (motion == WolfMotion.Recoil ? FromRecoil : Settling);
            motion = next; shownWary = wary;
        }
        blend = Math.Min(1, blend + dt / blendSeconds);
        shown = blend >= 1 ? target : Blended(from, target, Mathf.SmoothStep(0, 1, blend));
        shownDt = dt;
        // A motion whose paws are set elsewhere, such as at a door or in a tug, leaves nothing for Plant to hold on to after it.
        if (Paws == Planting.Set) placedAt = null;
        if (Root.Visible) Apply(shown);
    }

    /// <summary>
    /// How <see cref="Plant"/> sets a motion's paws on the ground: held where they land while the body moves over them; held
    /// the same way under a body that stands still, and stepped back under it one at a time when left too far behind; or
    /// left where the motion sets them.
    /// </summary>
    private enum Planting : byte { Held, Stepped, Set }

    /// <summary>How a motion's paws are set on the ground, and the seconds the model blends into it from another when it takes its own time.</summary>
    private readonly record struct MotionTraits(Planting Paws, float? BlendIn = null);

    /// <summary>
    /// Each motion's traits. A leap and a blow happen fast, and a wolf at a door stands still before its first scratch;
    /// changes of gait and settling down take longer. Thrown dead, at a door, in a tug, or braking with the forepaws
    /// sliding, the motion sets its own paws.
    /// </summary>
    private static MotionTraits TraitsOf(WolfMotion motion) => motion switch {
        WolfMotion.Stand or WolfMotion.Brace or WolfMotion.Feed or WolfMotion.Howl => new(Planting.Stepped),
        WolfMotion.Pin or WolfMotion.Rear => new(Planting.Stepped, .12f),
        WolfMotion.Recoil => new(Planting.Stepped, .08f),
        WolfMotion.Leap or WolfMotion.Snap => new(Planting.Held, .08f),
        WolfMotion.Door => new(Planting.Set, .12f),
        WolfMotion.Fall or WolfMotion.Tug or WolfMotion.Brake => new(Planting.Set),
        // The gaits.
        _ => new(Planting.Held),
    };
    /// <summary>Seconds a change of gait or settling down blends over, and coming out of a recoil.</summary>
    private const float Settling = .25f, FromRecoil = .3f;

    /// <summary>How <see cref="Plant"/> sets the paws of the motion shown: a motion on a cabin's step sets its own.</summary>
    private Planting Paws => Steps != null ? Planting.Set : TraitsOf(motion).Paws;

    /// <summary>
    /// A pose <paramref name="t"/> of the way from <paramref name="a"/> to <paramref name="b"/>: the body's angles in between, and
    /// each paw on the way from where it is in one to where it is in the other, so a paw on the ground in both stays on it.
    /// </summary>
    private static WolfPose Blended(WolfPose a, WolfPose b, float t) {
        var body = WolfPose.Lerp(a, b, t);
        Halves inA = new(a), inB = new(b), halves = new(body);
        for (int leg = 0; leg < 4; leg++) {
            var (lowerA, pawA) = Slant(inA.Of(leg), a.Leg(leg));
            var (lowerB, pawB) = Slant(inB.Of(leg), b.Leg(leg));
            var paw = PawFrame(inA.Of(leg).At, a.Leg(leg), leg).Origin.Lerp(PawFrame(inB.Of(leg).At, b.Leg(leg), leg).Origin, t);
            body = body.WithLeg(leg, ReachTo(halves, leg, paw, Mathf.Lerp(lowerA, lowerB, t), Mathf.Lerp(pawA, pawB, t)));
        }
        return body;
    }

    /// <summary>The angles, in degrees forward of straight down in the model, of a leg's lowest segment and of its paw, with the leg at <paramref name="l"/> from <paramref name="half"/>.</summary>
    private static (float Lower, float Paw) Slant(in Half half, WolfLeg l) {
        float lower = l.Upper + l.Middle + l.Lower + Mathf.RadToDeg(half.Lean);
        return (lower, lower + l.Paw);
    }

    // ---------- Paws on the ground ----------

    /// <summary>How a paw meets the ground: following its pose, held where it stands, or stepping to where its pose has it.</summary>
    private enum PawContact : byte { Free, Down, Stepping }
    private readonly PawContact[] contacts = new PawContact[4];
    /// <summary>Where each paw that is down has its toe pads on the ground, in the space the model is placed in.</summary>
    private readonly Vector3[] anchor = new Vector3[4];
    /// <summary>How far, in the model, each paw that has left the ground was from where its pose has it, and how far it has come since, 0 to 1.</summary>
    private readonly Vector3[] parted = new Vector3[4];
    private readonly float[] since = new float[4];
    /// <summary>Seconds the last pose shown moved on by, and where the model was placed when its paws were last set on the ground.</summary>
    private float shownDt;
    private Vector3? placedAt;
    /// <summary>Whether the motion shown is its wary kind, such as a stalk rather than a walk.</summary>
    private bool shownWary;

    /// <summary>
    /// Sets the paws on the ground once the model is placed for the frame: a paw that comes down stays where it landed, in the
    /// world, while the body moves and turns over it, until its pose lifts it, and a lifted paw goes on from where it left the
    /// ground to where its pose has it. Standing, a paw left too far from where its pose has it steps there, one paw at a time.
    /// No paw goes into the ground.
    /// </summary>
    public void Plant() {
        var root = Root.Transform;
        bool jumped = placedAt is not Vector3 last || last.DistanceTo(root.Origin) > Jump;
        placedAt = root.Origin;
        var paws = Paws;
        if (jumped || paws == Planting.Set) {
            for (int leg = 0; leg < 4; leg++) { contacts[leg] = PawContact.Free; parted[leg] = Vector3.Zero; since[leg] = 1; }
            if (paws == Planting.Set) return;
        }
        bool standing = paws == Planting.Stepped, seen = Root.Visible;
        var toModel = root.AffineInverse();
        bool stepping = false;
        for (int leg = 0; leg < 4; leg++) stepping |= contacts[leg] == PawContact.Stepping;
        var pose = shown;
        var halves = new Halves(pose);
        for (int leg = 0; leg < 4; leg++) {
            var half = halves.Of(leg);
            var frame = PawFrame(half.At, pose.Leg(leg), leg);
            // The paw bone as placed in the world.
            var placed = root * frame;
            float lowest = Lowest(placed);
            var toes = placed * SoleOutline[ToePoint];
            bool down = lowest < DownBy;
            // Where the paw bone goes, moved from where the pose has it.
            Vector3 shift;
            switch (contacts[leg]) {
                case PawContact.Down when lowest > UpBy:
                    contacts[leg] = PawContact.Free; parted[leg] = toModel.Basis * HeldShift(leg, toes, lowest); since[leg] = 0;
                    shift = parted[leg];
                    break;
                case PawContact.Down when standing && !stepping && new Vector2(anchor[leg].X - toes.X, anchor[leg].Z - toes.Z).Length() > StepAfter:
                    contacts[leg] = PawContact.Stepping; parted[leg] = toModel.Basis * HeldShift(leg, toes, lowest); since[leg] = 0; stepping = true;
                    shift = parted[leg];
                    break;
                case PawContact.Down:
                    shift = toModel.Basis * HeldShift(leg, toes, lowest);
                    break;
                default: {
                        bool steps = contacts[leg] == PawContact.Stepping;
                        since[leg] = Math.Min(1, since[leg] + shownDt / (steps ? StepSeconds : PartSeconds));
                        float left = 1 - Mathf.SmoothStep(0, 1, since[leg]);
                        // It lifts clear of the ground on its way, as high as it has to go, rather than dragging along it.
                        shift = parted[leg] * left + Vector3.Up * (steps ? StepLift : Math.Min(StepLift, parted[leg].Length())) * MathF.Sin(MathF.PI * since[leg]);
                        // A paw on its way down lands where it has come to, once it is there.
                        if (down && (!steps || since[leg] >= 1) && Lowest(Shifted(placed, root, shift)) < DownBy) {
                            contacts[leg] = PawContact.Down; anchor[leg] = (toes + root.Basis * shift) with { Y = 0 };
                            if (steps) stepping = false;
                            shift = toModel.Basis * HeldShift(leg, toes, lowest);
                        }
                        break;
                    }
            }
            // Nothing goes into the ground.
            float under = Lowest(Shifted(placed, root, shift));
            if (under < 0) shift += toModel.Basis * Vector3.Up * -under;
            if (shift.LengthSquared() < Hair * Hair) continue;
            var (lower, paw) = Slant(half, pose.Leg(leg));
            var target = frame.Origin + shift;
            var (solved, reached) = Reached(half, leg, target, lower, paw);
            shown = shown.WithLeg(leg, solved);
            if (seen) ApplyLeg(leg, solved);
            // A paw its leg cannot keep where it stands leaves it, from where the leg holds it.
            if (contacts[leg] == PawContact.Down && reached.DistanceTo(target) > LetGo) {
                contacts[leg] = standing ? PawContact.Stepping : PawContact.Free; parted[leg] = reached - frame.Origin; since[leg] = 0;
            }
        }
    }

    /// <summary>A paw bone <paramref name="placed"/> in the world, moved by <paramref name="shift"/> in the model placed at <paramref name="root"/>.</summary>
    private static Transform3D Shifted(Transform3D placed, Transform3D root, Vector3 shift) => placed with { Origin = placed.Origin + root.Basis * shift };

    /// <summary>How far, in the space the model is placed in, a held paw moves from where its pose has it: its toe pads onto where they stand and its sole onto the ground.</summary>
    private Vector3 HeldShift(int leg, Vector3 toes, float lowest) => new(anchor[leg].X - toes.X, -lowest, anchor[leg].Z - toes.Z);

    /// <summary>Which point of <see cref="SoleOutline"/> is the toe pads.</summary>
    private const int ToePoint = 4;
    /// <summary>
    /// How near the ground, in metres, a paw's pose brings it to set it down, and how far up to lift it again; how far a paw
    /// that is down may be from where a standing pose has it before it steps there, and how far from where its leg can reach
    /// before it lets go.
    /// </summary>
    private const float DownBy = .01f, UpBy = .02f, StepAfter = .04f, LetGo = .01f;
    /// <summary>Seconds a lifted paw takes to come from where it left the ground onto its pose's path, and a standing step takes, and how high a step lifts it.</summary>
    private const float PartSeconds = .12f, StepSeconds = .22f, StepLift = .05f;
    /// <summary>How far, in metres, the model may move in a frame and still be the same body walking on; further, it was put down somewhere new.</summary>
    private const float Jump = .5f;
    /// <summary>How near, in metres, a paw has to be to where its pose has it to be left there as it is.</summary>
    private const float Hair = .001f;

    /// <summary>A turn by these degrees, as the posing maths uses it.</summary>
    private static Basis Turn(float pitch, float yaw = 0, float roll = 0) => Basis.FromEuler(new Vector3(Mathf.DegToRad(pitch), Mathf.DegToRad(yaw), Mathf.DegToRad(roll)));
    /// <summary>A turn by these degrees, as the skeleton takes it.</summary>
    private static Quaternion Rotation(float pitch, float yaw = 0, float roll = 0) => Turn(pitch, yaw, roll).GetRotationQuaternion();

    /// <summary>The loin's offset and turn, and each half of the spine's turn and slide against it, shared by posing and the leg solver.</summary>
    private static (Vector3 At, Basis Turn) Spine(WolfPose p, int bone) => bone switch {
        WolfShape.Loin => (new Vector3(p.Shift, p.Lift, p.Surge), Turn(p.Pitch, 0, p.Roll)),
        WolfShape.Chest => (new Vector3(0, 0, -.0012f * p.Flex), Turn(p.Flex / 2, p.Bend / 2)),
        _ => (new Vector3(0, 0, .0012f * p.Flex), Turn(-p.Flex / 2, -p.Bend / 2))
    };

    private static Transform3D Rest(int bone) {
        var (parent, at) = WolfShape.Bones[bone];
        return new Transform3D(Basis.Identity, parent >= 0 ? at - WolfShape.Bones[parent].At : at);
    }

    /// <summary>A bone's pose relative to its parent: its rest offset moved by <paramref name="by"/> and turned.</summary>
    private static Transform3D Posed(int bone, Vector3 by, Basis turn) => new(turn, Rest(bone).Origin + by);

    /// <summary>Where the chest or pelvis sits in the model under <paramref name="p"/>.</summary>
    private static Transform3D Frame(WolfPose p, bool hind) {
        var (loinAt, loinTurn) = Spine(p, WolfShape.Loin);
        int half = hind ? WolfShape.Pelvis : WolfShape.Chest;
        var (at, turn) = Spine(p, half);
        return Posed(WolfShape.Loin, loinAt, loinTurn) * Posed(half, at, turn);
    }

    /// <summary>
    /// The chest or the pelvis under a pose, as the legs that hang from it are solved: where it sits in the model, the way
    /// back from the model into it, and how many radians forward its straight down leans.
    /// </summary>
    private readonly record struct Half(Transform3D At, Transform3D Back, float Lean) {
        public static Half Of(WolfPose pose, bool hind) {
            var at = Frame(pose, hind);
            var down = at.Basis * Vector3.Down;
            return new Half(at, at.AffineInverse(), MathF.Atan2(down.Z, -down.Y));
        }
    }

    /// <summary>The chest and the pelvis under a pose, worked out once for all four legs: a leg's pose changes neither.</summary>
    private readonly record struct Halves(Half Chest, Half Pelvis) {
        public Halves(WolfPose pose) : this(Half.Of(pose, false), Half.Of(pose, true)) { }
        public Half Of(int leg) => leg >= 2 ? Pelvis : Chest;
    }

    /// <summary>
    /// Solves a leg as <see cref="Reach(WolfPose, int, Vector3, float, float)"/> does, then corrects it until the paw bone is at
    /// <paramref name="paw"/>: the plain solve leaves a paw that stands out to the side of its shoulder or hip a few centimetres off.
    /// </summary>
    internal static WolfLeg ReachTo(WolfPose body, int leg, Vector3 paw, float lowerAngle, float pawAngle) => Reached(Half.Of(body, leg >= 2), leg, paw, lowerAngle, pawAngle).Leg;

    private static WolfLeg ReachTo(in Halves halves, int leg, Vector3 paw, float lowerAngle, float pawAngle) => Reached(halves.Of(leg), leg, paw, lowerAngle, pawAngle).Leg;

    /// <summary>Solves and corrects a leg as <see cref="ReachTo(WolfPose, int, Vector3, float, float)"/> does, and where its paw bone comes to.</summary>
    private static (WolfLeg Leg, Vector3 Paw) Reached(in Half half, int leg, Vector3 paw, float lowerAngle, float pawAngle) {
        var aim = paw;
        var solved = Reach(half, leg, aim, lowerAngle, pawAngle);
        var reached = PawFrame(half.At, solved, leg).Origin;
        for (int pass = 0; pass < 3; pass++) {
            var miss = paw - reached;
            if (miss.LengthSquared() < Close * Close) break;
            aim += miss;
            solved = Reach(half, leg, aim, lowerAngle, pawAngle);
            reached = PawFrame(half.At, solved, leg).Origin;
        }
        return (solved, reached);
    }
    /// <summary>How near, in metres, a solved paw has to come to where it was asked to be.</summary>
    private const float Close = .002f;

    /// <summary>
    /// Solves a leg so its paw bone reaches <paramref name="paw"/> in the model, with the lowest segment and the paw at the given angles forward of straight down.
    /// The foreleg's elbow bends back and the hind stifle forward, as a dog's do, and no joint bends past a dog's range: where the
    /// lowest segment's angle would take the wrist or hock past it, the segment takes the nearest angle that does not, and a paw out
    /// of reach is reached for as far as the leg goes.
    /// </summary>
    internal static WolfLeg Reach(WolfPose body, int leg, Vector3 paw, float lowerAngle, float pawAngle) => Reach(Half.Of(body, leg >= 2), leg, paw, lowerAngle, pawAngle);

    private static WolfLeg Reach(in Halves halves, int leg, Vector3 paw, float lowerAngle, float pawAngle) => Reach(halves.Of(leg), leg, paw, lowerAngle, pawAngle);

    /// <summary>Solves a leg as <see cref="Reach(WolfPose, int, Vector3, float, float)"/> does, hanging from <paramref name="half"/>.</summary>
    private static WolfLeg Reach(in Half half, int leg, Vector3 paw, float lowerAngle, float pawAngle) {
        bool hind = leg >= 2;
        var local = half.Back * paw;
        var pivot = WolfShape.Pivot(leg) - WolfShape.Bones[hind ? WolfShape.Pelvis : WolfShape.Chest].At;
        float lean = half.Lean;
        var lengths = WolfShape.Lengths(leg);
        float tz = local.Z - pivot.Z, ty = local.Y - pivot.Y, a3 = Mathf.DegToRad(lowerAngle) - lean;
        if (Bent(a3, tz, ty, lengths, hind, out float a1, out float a2) > 0) {
            a3 = NearestBend(a3, tz, ty, lengths, hind);
            Bent(a3, tz, ty, lengths, hind, out a1, out a2);
        }
        float pawAt = Mathf.DegToRad(pawAngle) - lean;
        float splay = MathF.Atan2(paw.X - WolfShape.Pivot(leg).X, Math.Max(.2f, WolfShape.Pivot(leg).Y - paw.Y));
        return new WolfLeg(Mathf.RadToDeg(a1), Mathf.RadToDeg(Mathf.AngleDifference(a1, a2)), Mathf.RadToDeg(Mathf.AngleDifference(a2, a3)), Mathf.RadToDeg(Mathf.AngleDifference(a3, pawAt)), Mathf.RadToDeg(splay));
    }

    /// <summary>
    /// How far, in degrees forward of straight, each joint of a leg opens and folds at most, from the range of motion of a dog's
    /// joints (Jaegger, Marcellin-Little and Levine 2002): the elbow stops 15 degrees short of straight and the stifle 18, the
    /// wrist opens 16 past straight and folds 148 back, and the hock stops 16 short of straight and folds 141.
    /// </summary>
    private const float ElbowOpen = 15, ElbowFolded = 144, StifleOpen = -18, StifleFolded = -138, WristOpen = 16, WristFolded = -148, HockOpen = 16, HockFolded = 141;

    /// <summary>
    /// Solves the two upper segments of a leg in its shoulder's or hip's frame for the lowest segment at <paramref name="a3"/>
    /// radians from straight down, with the paw bone <paramref name="tz"/> ahead of the shoulder or hip and <paramref name="ty"/>
    /// above it; returns how many radians that bends the wrist or hock past its range, 0 when it does not.
    /// </summary>
    private static float Bent(float a3, float tz, float ty, Vector3 lengths, bool hind, out float a1, out float a2) {
        float jz = tz - lengths.Z * MathF.Sin(a3), jy = ty + lengths.Z * MathF.Cos(a3), d = MathF.Sqrt(jz * jz + jy * jy);
        float reach = Math.Clamp(d, Span(lengths, hind ? -StifleFolded : ElbowFolded), Span(lengths, hind ? -StifleOpen : ElbowOpen));
        float toward = MathF.Atan2(jz, -jy), spread = MathF.Acos(Math.Clamp((lengths.X * lengths.X + reach * reach - lengths.Y * lengths.Y) / (2 * lengths.X * reach), -1, 1));
        a1 = hind ? toward + spread : toward - spread;
        float ez = lengths.X * MathF.Sin(a1), ey = -lengths.X * MathF.Cos(a1);
        a2 = MathF.Atan2(reach * MathF.Sin(toward) - ez, reach * MathF.Cos(toward) + ey);
        float lower = Mathf.RadToDeg(Mathf.AngleDifference(a2, a3)), open = hind ? HockOpen : WristFolded, folded = hind ? HockFolded : WristOpen;
        return Mathf.DegToRad(Math.Max(0, Math.Max(open - lower, lower - folded)));
    }

    /// <summary>How far apart a leg's shoulder or hip and the joint above its lowest segment are with the joint between them bent this many degrees from straight.</summary>
    private static float Span(Vector3 lengths, float bend) => MathF.Sqrt(lengths.X * lengths.X + lengths.Y * lengths.Y + 2 * lengths.X * lengths.Y * MathF.Cos(Mathf.DegToRad(bend)));

    /// <summary>
    /// The angle of a leg's lowest segment nearest <paramref name="want"/> at which the wrist or hock stays within its range; with
    /// none, the one that comes nearest.
    /// </summary>
    private static float NearestBend(float want, float tz, float ty, Vector3 lengths, bool hind) {
        // The wrist's or hock's bend moves nearly one for one with the lowest segment's angle, so a few steps along it find the edge of the range.
        float near = want, open = Mathf.DegToRad(hind ? HockOpen : WristFolded), folded = Mathf.DegToRad(hind ? HockFolded : WristOpen);
        for (int pass = 0; pass < 6; pass++) {
            Bent(near, tz, ty, lengths, hind, out _, out float a2);
            float bend = Mathf.AngleDifference(a2, near), edge = Math.Clamp(bend, open, folded);
            if (Math.Abs(edge - bend) < 1e-4f) return near;
            near += edge - bend;
        }
        if (Bent(near, tz, ty, lengths, hind, out _, out _) == 0) return near;
        float best = want, least = float.MaxValue;
        for (int k = 1; k <= BendSteps; k++) {
            float found = float.NaN;
            for (int side = -1; side <= 1; side += 2) {
                float at = want + side * k * BendStep, miss = Bent(at, tz, ty, lengths, hind, out _, out _);
                if (miss > 0) {
                    if (miss < least) (least, best) = (miss, at);
                    continue;
                }
                // The edge of the range lies between this angle and the one before it.
                float inside = at, outside = want + side * (k - 1) * BendStep;
                for (int pass = 0; pass < 12; pass++) {
                    float mid = (inside + outside) / 2;
                    if (Bent(mid, tz, ty, lengths, hind, out _, out _) > 0) outside = mid; else inside = mid;
                }
                if (float.IsNaN(found) || Math.Abs(inside - want) < Math.Abs(found - want)) found = inside;
            }
            if (!float.IsNaN(found)) return found;
        }
        // Past the range at every angle: the nearest it comes.
        float lo = best - BendStep, hi = best + BendStep;
        for (int pass = 0; pass < 12; pass++) {
            float a = lo + (hi - lo) / 3, b = hi - (hi - lo) / 3;
            if (Bent(a, tz, ty, lengths, hind, out _, out _) < Bent(b, tz, ty, lengths, hind, out _, out _)) hi = b; else lo = a;
        }
        return (lo + hi) / 2;
    }
    /// <summary>The radians between the angles of a lowest segment the search for one a leg can take tries, and how many it tries each way.</summary>
    private const float BendStep = .05f;
    private const int BendSteps = 64;

    /// <summary>How far a full snarl lifts the lips, in metres, how many degrees it curls them up at the front, and how many it lowers the brows.</summary>
    private const float LipLift = .028f, LipCurl = 6, BrowDrop = 10;

    private void Apply(WolfPose p) {
        for (int bone = WolfShape.Loin; bone <= WolfShape.Pelvis; bone++) {
            var (at, turn) = Spine(p, bone);
            skeleton.SetBonePosePosition(bone, Rest(bone).Origin + at);
            skeleton.SetBonePoseRotation(bone, turn.GetRotationQuaternion());
        }
        skeleton.SetBonePoseRotation(WolfShape.Neck, Rotation(p.Neck, p.NeckTurn));
        skeleton.SetBonePoseRotation(WolfShape.Head, Rotation(p.Head, p.HeadTurn, p.HeadTilt));
        skeleton.SetBonePoseRotation(WolfShape.Jaw, Rotation(p.Jaw));
        skeleton.SetBonePoseRotation(WolfShape.EarL, Rotation(-p.Ears, 0, -p.Ears * .3f));
        skeleton.SetBonePoseRotation(WolfShape.EarR, Rotation(-p.Ears, 0, p.Ears * .3f));
        // A snarl lifts the lips, most at the front, and lowers the inner end of each brow over its eye.
        skeleton.SetBonePosePosition(WolfShape.Lip, Rest(WolfShape.Lip).Origin + new Vector3(0, LipLift * p.Snarl, 0));
        skeleton.SetBonePoseRotation(WolfShape.Lip, Rotation(-LipCurl * p.Snarl));
        skeleton.SetBonePoseRotation(WolfShape.BrowL, Rotation(0, 0, BrowDrop * p.Snarl));
        skeleton.SetBonePoseRotation(WolfShape.BrowR, Rotation(0, 0, -BrowDrop * p.Snarl));
        // The tail rests pointing straight back; most of the lift is at the root and the curl and wag carry on along it.
        skeleton.SetBonePoseRotation(WolfShape.Tail, Rotation(p.Tail, p.Wag));
        skeleton.SetBonePoseRotation(WolfShape.Tail + 1, Rotation(p.Curl * .5f, p.Wag * .6f));
        skeleton.SetBonePoseRotation(WolfShape.Tail + 2, Rotation(p.Curl, p.Wag * .4f));
        for (int leg = 0; leg < 4; leg++) ApplyLeg(leg, p.Leg(leg));
    }

    private void ApplyLeg(int leg, WolfLeg l) {
        int bone = WolfShape.Leg(leg);
        // The pose's angles swing forward; a positive turn about +X swings a hanging bone back.
        skeleton.SetBonePoseRotation(bone, Rotation(-l.Upper, 0, l.Splay));
        skeleton.SetBonePoseRotation(bone + 1, Rotation(-l.Middle));
        skeleton.SetBonePoseRotation(bone + 2, Rotation(-l.Lower));
        skeleton.SetBonePoseRotation(bone + 3, Rotation(-l.Paw));
    }
}
