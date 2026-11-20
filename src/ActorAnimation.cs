using System;
using Godot;

namespace ChopChopMeow;

public enum ActorAction {
    Idle,
    Walk,
    Jog,
    Run,
    Chop,
    Mine,
    SteelChop,
    SteelMine,
    Gather,
    Carry,
    Pump,
    Hammer,
    Attack,
    Kindle,
    TorchSwing,
    Sit,
    Perch,
    Mauled,
    Devoured,
    Protest,
    PickUp,
    Pack,
    Craft,
    Grind,
    Dig,
    AxeSwing,
    SteelAxeSwing,
    Knock,
    Brake,
    /// <summary>Tugged back by the waist by a wolf, bent over and stepping back with each tug.</summary>
    Tugged,
    /// <summary>Knocked or yanked off the feet backward by a wolf, falling onto the back.</summary>
    FallBack,
    /// <summary>An axe swept sideways in the air, from behind the left shoulder round the front to the right at chest height; it takes turns with the overhead swing.</summary>
    AxeSweep,
    SteelAxeSweep
}

/// <summary>
/// Joint angles in degrees for the humanoid rig.
/// Raise lifts an arm forward from hanging (90 is horizontal, 180 overhead), Out swings it sideways (negative crosses the body),
/// and Swing moves a thigh forward while Knee folds the shin back.
/// Turn splays a thigh outward and Roll turns it about its own length, so a bent knee folds the shin sideways across the body.
/// Hips sets the hip height above the ground, as for sitting or running; zero keeps the lower foot on the ground instead.
/// Sway tips the torso sideways over the hips; positive tips it toward the right side.
/// Foot tips the toes down from square to the shin, and negative pulls them up.
/// SplayL and SplayR tip each thigh sideways away from the body, and negative draws that foot in under it.
/// Grip turns an axe, pick, hammer or sign in the right fist about its own length: at 90 a blade that cuts downward at the end of a level arm cuts toward the right.
/// </summary>
public readonly record struct Pose(
    float Lean = 0, float Twist = 0, float Head = 0,
    float RaiseL = 6, float OutL = 7, float ElbowL = 12,
    float RaiseR = 6, float OutR = 7, float ElbowR = 12,
    float SwingL = 0, float KneeL = 0, float SwingR = 0, float KneeR = 0,
    float TurnL = 0, float RollL = 0, float TurnR = 0, float RollR = 0, float Hips = 0, float Sway = 0, float FootL = 0, float FootR = 0, float SplayL = 0, float SplayR = 0, float Grip = 0) {
    /// <summary>Blends two poses; set hips blend by how far they sit above or below a standing foot, so a foot on or above the ground in both poses stays there.</summary>
    public static Pose Lerp(Pose a, Pose b, float t) {
        if (t <= 0) return a;
        var blend = new Pose(
            Mathf.Lerp(a.Lean, b.Lean, t), Mathf.Lerp(a.Twist, b.Twist, t), Mathf.Lerp(a.Head, b.Head, t),
            Mathf.Lerp(a.RaiseL, b.RaiseL, t), Mathf.Lerp(a.OutL, b.OutL, t), Mathf.Lerp(a.ElbowL, b.ElbowL, t),
            Mathf.Lerp(a.RaiseR, b.RaiseR, t), Mathf.Lerp(a.OutR, b.OutR, t), Mathf.Lerp(a.ElbowR, b.ElbowR, t),
            Mathf.Lerp(a.SwingL, b.SwingL, t), Mathf.Lerp(a.KneeL, b.KneeL, t), Mathf.Lerp(a.SwingR, b.SwingR, t), Mathf.Lerp(a.KneeR, b.KneeR, t),
            Mathf.Lerp(a.TurnL, b.TurnL, t), Mathf.Lerp(a.RollL, b.RollL, t), Mathf.Lerp(a.TurnR, b.TurnR, t), Mathf.Lerp(a.RollR, b.RollR, t), 0, Mathf.Lerp(a.Sway, b.Sway, t),
            Mathf.Lerp(a.FootL, b.FootL, t), Mathf.Lerp(a.FootR, b.FootR, t), Mathf.Lerp(a.SplayL, b.SplayL, t), Mathf.Lerp(a.SplayR, b.SplayR, t), Mathf.Lerp(a.Grip, b.Grip, t));
        return a.Hips == 0 && b.Hips == 0 ? blend : blend with { Hips = blend.Standing + Mathf.Lerp(a.Hips > 0 ? a.Hips - a.Standing : 0, b.Hips > 0 ? b.Hips - b.Standing : 0, t) };
    }

    /// <summary>The hip height above the ground: Hips when set, otherwise standing on the lower foot.</summary>
    public float HipHeight => Hips > 0 ? Hips : Standing;

    /// <summary>The hip height that puts the lower foot on the ground.</summary>
    private float Standing => Math.Max(ActorAnimation.Reach(SwingL, KneeL), ActorAnimation.Reach(SwingR, KneeR)) + ActorAnimation.Sole;
}

/// <summary>Pure actor animation selection, timing and keyframed poses.</summary>
public static class ActorAnimation {
    /// <summary>Seconds per loop; player action pulses last exactly one loop.</summary>
    public static float Duration(ActorAction action) => action switch {
        ActorAction.Idle => 3,
        ActorAction.Walk or ActorAction.Carry => WalkCycle,
        ActorAction.Jog => Jogging.Cycle,
        ActorAction.Run => Sprinting.Cycle,
        ActorAction.SteelChop or ActorAction.SteelMine or ActorAction.SteelAxeSwing or ActorAction.SteelAxeSweep => .8f,
        ActorAction.Gather => 1.2f,
        ActorAction.Hammer => .6f,
        ActorAction.Attack => .875f,
        ActorAction.Knock => Doorways.KnockSeconds / Doorways.Knocks,
        ActorAction.Brake => Momentum.BrakeSeconds,
        ActorAction.Tugged => 2 * DoorTug.TugSeconds / DoorTug.Tugs,
        ActorAction.FallBack => FallBackSeconds,
        ActorAction.Kindle => World.KindleSeconds,
        ActorAction.TorchSwing => .6f,
        ActorAction.Sit or ActorAction.Perch => 4,
        ActorAction.Mauled => .5f,
        ActorAction.Devoured => 1.4f,
        ActorAction.Protest => 1.6f,
        ActorAction.PickUp => .9f,
        ActorAction.Pack => 2.4f,
        ActorAction.Craft => 2.4f,
        ActorAction.Grind => 1.8f,
        ActorAction.Dig => 1.1f,
        _ => 1
    };

    /// <summary>How far an action's clock moves on: a walk's, jog's or run's by the ground covered, so its planted feet keep their place, and the rest by the time passed.</summary>
    public static float Advance(ActorAction action, float seconds, float metres) =>
        Stride(action) > 0 ? metres / Stride(action) * Duration(action) : seconds;

    /// <summary>
    /// Seconds into a swing at which it lands: the punch at full stretch, the torch crossing in front, the axe at the bottom of
    /// its overhead stroke or as its sweep crosses the front; zero for anything else.
    /// </summary>
    public static float Contact(ActorAction action) => action switch {
        ActorAction.Attack => .32f * Duration(action),
        ActorAction.TorchSwing => .3f * Duration(action),
        ActorAction.AxeSwing or ActorAction.SteelAxeSwing or ActorAction.AxeSweep or ActorAction.SteelAxeSweep => .5f * Duration(action),
        _ => 0
    };

    /// <summary>Seconds left until a swing with <paramref name="remaining"/> seconds still to play lands; zero once it has.</summary>
    public static float UntilContact(ActorAction action, float remaining) => Math.Max(0, Contact(action) - (Duration(action) - remaining));

    /// <summary>Position within the current loop, from 0 inclusive to 1 exclusive.</summary>
    public static float Phase(ActorAction action, float elapsed) {
        if (!float.IsFinite(elapsed)) return 0;
        float phase = elapsed / Duration(action) % 1;
        return phase < 0 ? phase + 1 : phase;
    }

    /// <summary>The ground direction a body facing <paramref name="yaw"/> faces.</summary>
    public static Vector2 Forward(float yaw) => new(MathF.Sin(yaw), MathF.Cos(yaw));
    /// <summary>Turns world-plane movement into a rig yaw in radians; the rig faces +Z, which is world +Y.</summary>
    public static float Yaw(Vector2 movement, float previous) =>
        movement.LengthSquared() < 0.0001f ? previous : MathF.Atan2(movement.X, movement.Y);

    /// <summary>The tool an action shows in hand, or null for empty hands; the mortar is its pestle in the right hand and its bowl in the left.</summary>
    public static string? Tool(ActorAction action) => action switch {
        ActorAction.Chop or ActorAction.SteelChop or ActorAction.AxeSwing or ActorAction.SteelAxeSwing or ActorAction.AxeSweep or ActorAction.SteelAxeSweep => "steel_axe",
        ActorAction.Mine or ActorAction.SteelMine => "steel_pick",
        ActorAction.Hammer => "hammer",
        ActorAction.Protest => "protest_sign",
        ActorAction.Grind => "mortar",
        ActorAction.Dig => "steel_shovel",
        _ => null
    };

    // Standing to work with the left boot ahead and the right behind (StanceFeet), and squatting low on the balls of both feet (SquatFeet).
    // A pose done in place gives only its hip height: OnFeet bends the legs to keep both boots where they stand.
    private static readonly Pose Stance = new(Hips: .805f);
    private static readonly Pose Crouch = new(Lean: 38, Head: 18, Hips: .49f);

    // Each action's key poses are made once: every worker and player is posed every frame, and a list made per call is garbage.
    // An axe swung in the air goes up overhead and comes straight down in front.
    private static readonly Pose AxeSwingReady = Stance with { Lean = 6, RaiseL = 50, OutL = -24, ElbowL = 35, RaiseR = 55, OutR = -16, ElbowR = 25 };
    private static readonly (float At, Pose Pose)[] AxeSwingKeys = {
        (0, AxeSwingReady),
        (.3f, AxeSwingReady with { Lean = -8, Twist = 24, RaiseL = 150, OutL = -26, ElbowL = 60, RaiseR = 168, OutR = -10, ElbowR = 45 }),
        (AxeSwingStrike, AxeSwingReady with { Lean = 18, Twist = -12, RaiseL = 80, OutL = -26, ElbowL = 14, RaiseR = 86, OutR = -12, ElbowR = 4 }),
        (.66f, AxeSwingReady with { Lean = 20, Twist = -14, RaiseL = 76, OutL = -26, ElbowL = 14, RaiseR = 80, OutR = -12, ElbowR = 6 }),
    };
    // Felling: the axe is wound round to the left at shoulder height, then swung across, almost level, into the trunk's flank.
    // The shoulders turn through the stroke and the grip turns the blade to cut sideways.
    private static readonly Pose ChopReady = TwoHanded(Stance with { Lean = 6, Twist = 14, RaiseR = 42, OutR = -30, ElbowR = 40, Grip = 35 });
    private static readonly (float At, Pose Pose)[] ChopKeys = {
        (0, ChopReady),
        (.34f, TwoHanded(Stance with { Twist = 48, Sway = -4, RaiseR = 66, OutR = -62, ElbowR = 30, Grip = 75 })),
        (ChopStrike, TwoHanded(Stance with { Lean = 19, Twist = -14, Sway = 2, RaiseR = 77, OutR = -39, ElbowR = 28, Grip = 90, Hips = .796f })),
        (.66f, TwoHanded(Stance with { Lean = 20, Twist = -16, Sway = 2, RaiseR = 74, OutR = -40, ElbowR = 30, Grip = 90, Hips = .796f })),
    };
    // An axe swept sideways in the air: wound far round behind the left shoulder with the arms out level, then swung wide
    // across the front at chest height and on round to the right in one stroke, the shoulders turning all the way with it.
    private static readonly Pose SweepReady = TwoHanded(Stance with { Lean = 4, Twist = 20, RaiseR = 60, OutR = -35, ElbowR = 35, Grip = 60 });
    private static readonly (float At, Pose Pose)[] SweepKeys = {
        (0, SweepReady),
        (.34f, TwoHanded(Stance with { Lean = -2, Twist = 75, Sway = -5, RaiseR = 74, OutR = -72, ElbowR = 18, Grip = 85 })),
        (.6f, TwoHanded(Stance with { Lean = 4, Twist = -60, Sway = 4, RaiseR = 82, OutR = -15, ElbowR = 10, Grip = 90, Hips = .799f })),
        (.74f, TwoHanded(Stance with { Lean = 4, Twist = -55, Sway = 3, RaiseR = 80, OutR = -18, ElbowR = 16, Grip = 90, Hips = .801f })),
    };
    private static readonly Pose MineGuard = Stance with { Lean = 8, RaiseL = 45, OutL = -24, ElbowL = 40, RaiseR = 50, OutR = -14, ElbowR = 30 };
    /// <summary>
    /// The shares of a loop at which a felling axe bites the trunk, a pickaxe the rock and a hammer its work, and at which an axe swung overhead in the air is brought up short.
    /// The stroke arrives there at its full speed and stops.
    /// </summary>
    public const float ChopStrike = .52f, MineStrike = .55f, HammerStrike = .5f, AxeSwingStrike = .52f;

    /// <summary>Seconds a player knocked or yanked off their feet backward takes to land on their back and lie there under the wolf.</summary>
    public const float FallBackSeconds = .8f;

    // Knocked or yanked back off the feet: the trunk is jerked upright and back, the head snaps forward, the arms fling up
    // for the door and the front foot slips out ahead; the knees fold as the body drops onto its seat and back, the arms thrown
    // wide at the landing, and then it lies fighting the wolf as Mauled has it. The rig tips back as FallBackView.FallBack has it.
    private static readonly (float At, Pose Pose)[] FallBackKeys = {
        (0, new Pose(Lean: 12, Head: -10, RaiseL: 80, OutL: 14, ElbowL: 30, RaiseR: 85, OutR: 12, ElbowR: 26, SwingL: 6, KneeL: 16, SwingR: 6, KneeR: 16, SplayL: 3, SplayR: 3)),
        (.18f, new Pose(Lean: -8, Head: 26, RaiseL: 140, OutL: 20, ElbowL: 30, RaiseR: 128, OutR: 24, ElbowR: 40, SwingL: 38, KneeL: 8, FootL: -20, SwingR: 4, KneeR: 34, SplayL: 5, SplayR: 5)),
        (.45f, new Pose(Lean: -14, Head: 32, RaiseL: 125, OutL: 34, ElbowL: 40, RaiseR: 105, OutR: 38, ElbowR: 48, SwingL: 70, KneeL: 42, FootL: -10, SwingR: 58, KneeR: 70, SplayL: 6, SplayR: 6)),
        (.7f, new Pose(Lean: 0, Head: 18, RaiseL: 150, OutL: 46, ElbowL: 34, RaiseR: 145, OutR: 50, ElbowR: 30, SwingL: 26, KneeL: 40, SwingR: 20, KneeR: 48, SplayL: 6, SplayR: 6)),
        (.999f, new Pose(Head: -26, RaiseL: 128, OutL: -8, ElbowL: 55, RaiseR: 128, OutR: -8, ElbowR: 55, SwingL: 10, KneeL: 22, SwingR: 10, KneeR: 22)),
    };
    private static readonly (float At, Pose Pose)[] MineKeys = {
        (0, MineGuard),
        (.28f, MineGuard with { Lean = -6, RaiseL = 160, OutL = -24, ElbowL = 50, RaiseR = 175, OutR = -10, ElbowR = 35 }),
        (MineStrike, MineGuard with { Lean = 32, Head = 10, RaiseL = 34, OutL = -26, ElbowL = 10, RaiseR = 36, OutR = -12, ElbowR = 0, Hips = .79f }),
        (.68f, MineGuard with { Lean = 30, Head = 10, RaiseL = 32, OutL = -26, ElbowL = 12, RaiseR = 34, OutR = -12, ElbowR = 2, Hips = .793f }),
    };
    // Reach with one hand, close it on the find and pull it in while the other hand reaches.
    private static readonly (float At, Pose Pose)[] GatherKeys = {
        (0, Crouch with { RaiseR = 66, ElbowR = 6, OutR = -6, RaiseL = 24, ElbowL = 70 }),
        (.22f, Crouch with { Lean = 44, RaiseR = 50, ElbowR = 34, OutR = -10, RaiseL = 30, ElbowL = 60 }),
        (.5f, Crouch with { RaiseL = 66, ElbowL = 6, OutL = -6, RaiseR = 24, ElbowR = 70 }),
        (.72f, Crouch with { Lean = 44, RaiseL = 50, ElbowL = 34, OutL = -10, RaiseR = 30, ElbowR = 60 }),
    };
    // The gathering crouch once: drop down, reach, close the hand on the piece and stand with it held up to stow.
    private static readonly (float At, Pose Pose)[] PickUpKeys = {
        (0, new Pose()),
        (.32f, Crouch with { Lean = 44, RaiseR = 64, ElbowR = 8, OutR = -6, RaiseL = 26, ElbowL = 60 }),
        (.48f, Crouch with { Lean = 42, RaiseR = 52, ElbowR = 40, OutR = -10, RaiseL = 26, ElbowL = 60 }),
        (.78f, new Pose(Lean: 4, Head: 8, RaiseR: 36, OutR: -14, ElbowR: 105)),
    };
    // A quick shove down on the lever, a short hold, then a slower lift as the bellows refill.
    private static readonly Pose PumpLever = Stance with { Lean = 16, RaiseL = 78, OutL = -12, ElbowL = 42, RaiseR = 78, OutR = -12, ElbowR = 42 };
    private static readonly Pose PumpPressed = PumpLever with { Lean = 32, RaiseL = 44, ElbowL = 6, RaiseR = 44, ElbowR = 6, Hips = .787f };
    private static readonly (float At, Pose Pose)[] PumpKeys = { (0, PumpLever), (.35f, PumpPressed), (.5f, PumpPressed with { Lean = 30, Hips = .792f }) };
    private static readonly Pose HammerHold = Stance with { Lean = 15, RaiseL = 48, OutL = -16, ElbowL = 52, RaiseR = 70, OutR = -8, ElbowR = 50 };
    private static readonly (float At, Pose Pose)[] HammerKeys = {
        (0, HammerHold),
        (.22f, HammerHold with { Lean = 8, RaiseR = 120, ElbowR = 105 }),
        (HammerStrike, HammerHold with { Lean = 18, RaiseR = 58, ElbowR = 22 }),
        (.7f, HammerHold with { Lean = 18, RaiseR = 60, ElbowR = 24 }),
    };
    // Both hands rummage in the heap of goods, lift a piece across, and settle it into the crate on the other side.
    private static readonly Pose PackTable = Stance with { Lean = 8, Head = 20, RaiseL = 50, OutL = -14, ElbowL = 46, RaiseR = 50, OutR = -14, ElbowR = 46 };
    private static readonly Pose PackHeap = PackTable with { Twist = -20, Sway = -3, RaiseL = 46, OutL = -22, ElbowL = 40, RaiseR = 56, OutR = -2, ElbowR = 50 };
    private static readonly Pose PackCrate = PackTable with { Twist = 20, Sway = 3, RaiseL = 56, OutL = -2, ElbowL = 50, RaiseR = 46, OutR = -22, ElbowR = 40 };
    private static readonly (float At, Pose Pose)[] PackKeys = {
        (0, PackHeap),
        (.12f, PackHeap with { RaiseL = 40, ElbowL = 52, RaiseR = 62, ElbowR = 38 }),
        (.24f, PackHeap with { RaiseL = 50, ElbowL = 34, RaiseR = 52, ElbowR = 58 }),
        (.36f, PackHeap),
        (.5f, PackTable with { Lean = 4, RaiseL = 70, ElbowL = 84, RaiseR = 70, ElbowR = 84 }),
        (.62f, PackCrate with { Lean = 14, RaiseL = 50, ElbowL = 30, RaiseR = 46, ElbowR = 28 }),
        (.74f, PackCrate with { Lean = 14, RaiseL = 56, ElbowL = 44, RaiseR = 42, ElbowR = 36 }),
        (.86f, PackCrate),
        (.94f, PackTable with { Lean = 6, RaiseL = 62, ElbowL = 70, RaiseR = 62, ElbowR = 70 }),
    };
    private static readonly Pose Fists = Stance with { Twist = 10, RaiseL = 48, OutL = -14, ElbowL = 112, RaiseR = 44, OutR = -14, ElbowR = 115 };
    // The fist is drawn back as the shoulders wind up, then thrown out straight.
    private static readonly (float At, Pose Pose)[] AttackKeys = {
        (0, Fists),
        (.18f, Fists with { Twist = 18, RaiseR = 40, OutR = -8, ElbowR = 128 }),
        (.32f, Fists with { Lean = 10, Twist = -26, RaiseR = 90, OutR = -8, ElbowR = 0 }),
        (.5f, Fists with { Lean = 8, Twist = -20, RaiseR = 86, OutR = -8, ElbowR = 6 }),
    };
    // A wide forehand sweep of the torch arm across the body, bent over at the waist with the knees giving, so the flame
    // passes low enough to reach a wolf.
    private static readonly Pose TorchUp = Stance with { RaiseL = 30, OutL = 4, ElbowL = 60, RaiseR = 20, OutR = 10, ElbowR = 30 };
    private static readonly Pose TorchLow = TorchUp with { Hips = .75f, Head = -20 };
    private static readonly (float At, Pose Pose)[] TorchSwingKeys = {
        (0, TorchUp),
        (.2f, TorchLow with { Twist = 22, Lean = 34, RaiseL = 46, OutL = 50, ElbowL = 24 }),
        (.45f, TorchLow with { Twist = -24, Lean = 50, RaiseL = 58, OutL = -34, ElbowL = 8 }),
        (.65f, TorchLow with { Twist = -16, Lean = 40, RaiseL = 52, OutL = -26, ElbowL = 18 }),
    };
    // Cross-legged on the ground with hands on the knees, or on a rock with knees up and forearms on them.
    // Now and then a bite goes up to the mouth.
    private static readonly (float At, Pose Pose)[] SitKeys = Eating(new Pose(Lean: 6, Head: 2, RaiseL: 30, OutL: 22, ElbowL: 34, RaiseR: 30, OutR: 22, ElbowR: 34,
        SwingL: 86, KneeL: 150, TurnL: 40, RollL: 90, SwingR: 82, KneeR: 150, TurnR: 40, RollR: 90, Hips: .15f));
    private static readonly (float At, Pose Pose)[] PerchKeys = Eating(new Pose(Lean: -6, Head: 4, RaiseL: 52, OutL: -10, ElbowL: 48, RaiseR: 52, OutR: -10, ElbowR: 48, Hips: .315f));
    private static (float At, Pose Pose)[] Eating(Pose seated) {
        var bite = seated with { Head = seated.Head + 6, Lean = seated.Lean + 6, RaiseR = 40, OutR = -24, ElbowR = 140 };
        return new[] { (0, seated), (.55f, seated), (.68f, bite), (.8f, bite), (.92f, seated) };
    }

    // The trunk leans this many degrees over the mortar's bowl, which the left fist holds this far from the hips in the trunk's own frame.
    private const float GrindLean = 8;
    private static readonly Vector3 BowlHand = new(0, .2f, .4f);
    /// <summary>
    /// How far above the left fist the mortar's bowl stands, how far up from its foot the right fist grips the pestle, and the degrees the pestle leans toward that hand.
    /// </summary>
    public const float BowlOverFist = .04f, PestleGrip = .16f, PestleLean = 25;
    // The pestle hand circles this far out, which swings the pestle's foot about half as far again round the bowl's floor, and lifts this high to pound.
    private const float GrindCircle = .02f, PoundLift = .06f;
    /// <summary>Grinding with the pestle's foot resting in the middle of the bowl's floor; the rig turns the bowl level and the pestle onto its lean in this pose.</summary>
    public static readonly Pose GrindRest = Grinding(Vector2.Zero, 0);

    /// <summary>
    /// Standing with the mortar in hand: the left fist holds the bowl out in front and the right holds the pestle in it.
    /// The pestle hand is <paramref name="aside"/>, in metres to the left and ahead, and <paramref name="lift"/> above where it rests the pestle's foot in the middle of the bowl's floor.
    /// </summary>
    private static Pose Grinding(Vector2 aside, float lift) {
        float tilt = Mathf.DegToRad(PestleLean);
        // Measured upright, then turned into the leaning trunk's frame.
        var toFist = new Vector3(aside.X - PestleGrip * MathF.Sin(tilt), BowlOverFist + CookShape.BowlFloor + lift + PestleGrip * MathF.Cos(tilt), aside.Y);
        var bowl = ArmReach.ArmTo(BowlHand, left: true, ActorRig.Palm);
        var pestle = ArmReach.ArmTo(BowlHand + toFist.Rotated(Vector3.Right, -Mathf.DegToRad(GrindLean)), left: false, ActorRig.Palm);
        return Stance with { Lean = GrindLean, RaiseL = bowl.Raise, OutL = bowl.Out, ElbowL = bowl.Elbow, RaiseR = pestle.Raise, OutR = pestle.Out, ElbowR = pestle.Elbow };
    }

    /// <summary>How far past the right fist, along the handle it holds, the left fist grips in a two-handed swing, in metres.</summary>
    private const float SecondGrip = .06f;
    /// <summary>
    /// A pose with the left fist on the handle of what the right fist holds, just past the right fist.
    /// A handle out of the left arm's reach gets the arm stretched toward it.
    /// </summary>
    private static Pose TwoHanded(Pose pose) {
        var (raise, outward, elbow) = ArmReach.ArmTo(ArmReach.FistInTrunk(pose, false) + ArmReach.ForearmInTrunk(pose, false) * SecondGrip, left: true);
        return pose with { RaiseL = raise, OutL = outward, ElbowL = elbow };
    }

    // ---------- Digging ----------

    /// <summary>From the top of a shovel's handle, which the right fist holds: how far down the shaft the left fist grips it, and how far it is to the blade's tip, in metres.</summary>
    public const float ShovelGrip = .25f, ShovelLength = .95f;
    /// <summary>The shares of a dig's loop at which the blade goes into the ground and at which the soil leaves it.</summary>
    public const float DigStrike = .22f, DigToss = .76f;
    // The front boot's heel and the back boot's toe stay this far ahead of the hips for the whole dig, so neither foot slides.
    private const float DigFrontHeel = .12f, DigBackToe = -.15f;

    /// <summary>
    /// A moment of a dig: the trunk's lean and turn in degrees and the height of the hips, where the blade's tip is from the spot under the hips, in metres to the left, up and ahead,
    /// and how far the shaft leans from straight down toward ahead, <paramref name="Pitch"/>, turned to the left by <paramref name="Yaw"/>, in degrees.
    /// </summary>
    private readonly record struct DigKey(float At, float Lean, float Twist, float Hips, Vector3 Tip, float Pitch, float Yaw);

    // The blade is driven into the ground ahead, the weight goes onto it, the handle is pushed down to lever the load out, and the load is heaved up and thrown aside to the left.
    // The handle stays ahead of the right side of the chest and below the shoulders, with the shaft crossing toward the left hand out in front:
    // held at the middle of the chest the right arm folds into the torso, and held higher the handle is in front of the face.
    // The blade goes in short of where a digger stands from a stump or a bush, so it enters the ground at its foot.
    private static readonly DigKey[] DigKeys = {
        new(0, 10, 0, .74f, new(.15f, .3f, .73f), 30, 41),
        new(DigStrike, 54, 0, .7f, new(.1f, -.08f, .78f), 26, 43),
        new(.32f, 58, 0, .65f, new(.1f, -.13f, .8f), 28, 43),
        new(.55f, 45, 0, .684f, new(.12f, .09f, .94f), 40, 33),
        new(.64f, 34, 9, .627f, new(.4f, .26f, 1.01f), 52, 44),
        new(.72f, 33, 24, .671f, new(.52f, .33f, .98f), 52, 45),
        new(.82f, 26, 22, .769f, new(.79f, .65f, .79f), 72, 70),
        new(.92f, 7, 10, .71f, new(.34f, .46f, .87f), 48, 42),
    };

    // How much of a turn and reach toward the blade's spot each dig key takes: all of it while the blade goes in and levers, less as it heaves, none as it throws.
    private static readonly float[] DigAim = { 1, 1, 1, 1, .6f, .3f, 0, .5f };
    // Of a reach further out to the blade's spot, the share the hands take; the shaft leans the rest of the way.
    private const float DigHandsAhead = .3f;
    // The least and most a dug shaft leans from straight down, and the most the trunk leans to keep the hands on it, in degrees.
    private const float DigPitchLeast = 5, DigPitchMost = 70, DigLeanMost = 75;
    // How far a hand reaches from its shoulder holding the shaft, short of the arm's full length.
    private const float DigArm = .53f;

    /// <summary>
    /// A dig key turned and reached out so the blade goes in <paramref name="share"/> of the way from where it is keyed to go in to
    /// <paramref name="spot"/>, metres to the left and ahead of the hips.
    /// The trunk, the hands and the shaft turn toward the spot; the hands go part of the way further out and the shaft runs from
    /// them to the blade, leaning further out or standing straighter as that takes, within how far a dug shaft leans.
    /// </summary>
    private static DigKey Aimed(DigKey key, float share, Vector2 spot) {
        var keyed = new Vector2(DigKeys[1].Tip.X, DigKeys[1].Tip.Z);
        if (share == 0 || spot == keyed) return key;
        // Measured as a body turns: toward the left from straight ahead.
        static float Bearing(Vector2 at) => MathF.Atan2(at.X, at.Y);
        static Vector2 Turned(Vector2 at, float by) => new(at.X * MathF.Cos(by) + at.Y * MathF.Sin(by), -at.X * MathF.Sin(by) + at.Y * MathF.Cos(by));
        float turn = Mathf.AngleDifference(Bearing(keyed), Bearing(spot)) * share, further = (spot.Length() - keyed.Length()) * share;
        float pitch = Mathf.DegToRad(key.Pitch), yaw = Mathf.DegToRad(key.Yaw) + turn;
        var tip = Turned(new Vector2(key.Tip.X, key.Tip.Z), turn);
        var outward = tip.Normalized();
        // Where the hands hold the top of the shaft, seen from above, and the blade's tip, both taken further out.
        var hands = tip - new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)) * (MathF.Sin(pitch) * ShovelLength) + outward * (further * DigHandsAhead);
        tip += outward * further;
        var reach = tip - hands;
        float least = MathF.Sin(Mathf.DegToRad(DigPitchLeast)) * ShovelLength, most = MathF.Sin(Mathf.DegToRad(DigPitchMost)) * ShovelLength;
        if (reach.Length() < least) reach = (reach == Vector2.Zero ? outward : reach.Normalized()) * least;
        if (reach.Length() > most) reach = reach.Normalized() * most;
        return key with {
            Twist = key.Twist + Mathf.RadToDeg(turn),
            Tip = new Vector3(tip.X, key.Tip.Y, tip.Y),
            Pitch = Mathf.RadToDeg(MathF.Asin(reach.Length() / ShovelLength)),
            Yaw = Mathf.RadToDeg(MathF.Atan2(reach.X, reach.Y))
        };
    }

    /// <summary>
    /// Digging with a shovel in both hands, the right on top of the handle and the left down the shaft.
    /// Both arms are solved from where the handle has to be for the blade's tip to be where the dig takes it, and the rig lays the shaft through both fists.
    /// The front foot stays flat and the back one on its toe, the heel lifting as the hips come down.
    /// </summary>
    /// <param name="spot">Where the blade goes into the ground, metres to the left and ahead of the hips, or null where it goes in as keyed.</param>
    private static Pose Dig(float p, Vector2? spot) {
        int from = DigKeys.Length - 1;
        while (from > 0 && p < DigKeys[from].At) from--;
        int to = (from + 1) % DigKeys.Length;
        var (a, b) = spot is Vector2 at ? (Aimed(DigKeys[from], DigAim[from], at), Aimed(DigKeys[to], DigAim[to], at)) : (DigKeys[from], DigKeys[to]);
        float t = Mathf.SmoothStep(0, 1, (p - a.At) / ((from + 1 < DigKeys.Length ? b.At : 1) - a.At));
        float lean = Mathf.Lerp(a.Lean, b.Lean, t), twist = Mathf.Lerp(a.Twist, b.Twist, t), height = Mathf.Lerp(a.Hips, b.Hips, t);
        float pitch = Mathf.DegToRad(Mathf.Lerp(a.Pitch, b.Pitch, t)), yaw = Mathf.DegToRad(Mathf.Lerp(a.Yaw, b.Yaw, t));
        var along = new Vector3(MathF.Sin(pitch) * MathF.Sin(yaw), -MathF.Cos(pitch), MathF.Sin(pitch) * MathF.Cos(yaw));
        var top = a.Tip.Lerp(b.Tip, t) - along * ShovelLength;
        var front = OnHeel(DigFrontHeel, height, 0);
        var back = OnToe(DigBackToe, height, Mathf.DegToRad(8 + (.78f - height) * 200));
        var hips = new Vector3(0, height, 0);
        // Measured from the ground under the hips, then turned into the leaning, twisted trunk's frame.
        Vector3 InTrunk(Vector3 point, float bent) => (point - hips).Rotated(Vector3.Up, -Mathf.DegToRad(twist)).Rotated(Vector3.Right, -Mathf.DegToRad(bent));
        var grip = top + along * ShovelGrip;
        // A blade sent further out than the hands reach from this lean bends the trunk further, just far enough for both to hold the shaft.
        float Short(float bent) => Math.Max(InTrunk(top, bent).DistanceTo(HumanShape.Standard.Shoulder(false)), InTrunk(grip, bent).DistanceTo(HumanShape.Standard.Shoulder(true))) - DigArm;
        if (Short(lean) > 0) {
            float within = DigLeanMost, beyond = lean;
            for (int i = 0; i < 16 && Short(within) < 0; i++) {
                float mid = (within + beyond) / 2;
                if (Short(mid) > 0) beyond = mid; else within = mid;
            }
            lean = within;
        }
        var right = ArmReach.ArmTo(InTrunk(top, lean), left: false, ActorRig.Palm);
        var left = ArmReach.ArmTo(InTrunk(grip, lean), left: true, ActorRig.Palm);
        return new Pose(Lean: lean, Twist: twist, Head: 14 + lean * .3f, Hips: height,
            RaiseL: left.Raise, OutL: left.Out, ElbowL: left.Elbow, RaiseR: right.Raise, OutR: right.Out, ElbowR: right.Elbow,
            SwingL: front.Swing, KneeL: front.Knee, FootL: front.Foot, SwingR: back.Swing, KneeR: back.Knee, FootR: back.Foot);
    }

    /// <summary>Swings that play out in full on the move: the arms and body keep swinging while the legs keep walking.</summary>
    public static bool SwingsOnTheMove(ActorAction action) => Contact(action) > 0;

    /// <summary>
    /// An action's pose <paramref name="elapsed"/> seconds in.
    /// A blow that lands on something, chopping, picking, hammering or digging, lands its tool's working end on <paramref name="mark"/>, a point in the rig's own frame, when one is given and within reach.
    /// </summary>
    public static Pose PoseAt(ActorAction action, float elapsed, bool moving, Vector3? mark = null) {
        if (moving && SwingsOnTheMove(action)) {
            // The swing keeps its own clock, which keeps the jog's pace at the player's speed.
            var legs = PoseAt(ActorAction.Jog, elapsed, true);
            return Posed(action, elapsed, false, null) with { SwingL = legs.SwingL, KneeL = legs.KneeL, FootL = legs.FootL, SwingR = legs.SwingR, KneeR = legs.KneeR, FootR = legs.FootR, Sway = legs.Sway, Hips = legs.Hips, SplayL = legs.SplayL, SplayR = legs.SplayR };
        }
        var pose = Posed(action, elapsed, moving, mark);
        return FeetOf(action) is Footing feet ? OnFeet(pose, feet) : pose;
    }

    /// <summary>An action's pose before its boots are stood on the ground; an action with a <see cref="Footing"/> gives only its hip height for the legs.</summary>
    private static Pose Posed(ActorAction action, float elapsed, bool moving, Vector3? mark) {
        float p = Phase(action, elapsed);
        float wave = MathF.Sin(p * Mathf.Tau);
        switch (action) {
            case ActorAction.Walk:
                return Walk(p);
            case ActorAction.Jog:
                return Run(Jogging, p);
            case ActorAction.Run:
                return Run(Sprinting, p);
            case ActorAction.Craft: {
                    // Squatting on the heels, leaning over the work in both hands, which keep turning and fitting it.
                    float a = MathF.Sin(p * Mathf.Tau * 3), b = MathF.Sin(p * Mathf.Tau * 2 + 1.3f), c = MathF.Sin(p * Mathf.Tau * 5 + .4f);
                    return new Pose(Lean: 30 + b * 2, Head: 24 + a * 3, Twist: a * 4,
                        RaiseL: 48 + a * 9, OutL: -22 + c * 6, ElbowL: 78 + b * 16, RaiseR: 50 - b * 8, OutR: -22 - c * 5, ElbowR: 76 + a * 14,
                        Hips: .28f, TurnL: 14, TurnR: 14);
                }
            case ActorAction.Carry:
                var carry = new Pose(Lean: -4, RaiseL: 8, OutL: 0, ElbowL: 82, RaiseR: 8, OutR: 0, ElbowR: 82);
                return moving ? Walk(p) with { Lean = carry.Lean, Head = -carry.Lean / 2, RaiseL = carry.RaiseL, OutL = carry.OutL, ElbowL = carry.ElbowL, RaiseR = carry.RaiseR, OutR = carry.OutR, ElbowR = carry.ElbowR } : carry;
            case ActorAction.Chop or ActorAction.SteelChop:
                return Keys(p, ChopKeys, ChopStrike, action, mark);
            case ActorAction.AxeSweep or ActorAction.SteelAxeSweep:
                return Keys(p, SweepKeys);
            case ActorAction.AxeSwing or ActorAction.SteelAxeSwing:
                return Keys(p, AxeSwingKeys, AxeSwingStrike);
            case ActorAction.Mine or ActorAction.SteelMine:
                return Keys(p, MineKeys, MineStrike, action, mark);
            case ActorAction.Gather:
                return Keys(p, GatherKeys);
            case ActorAction.PickUp:
                return Keys(p, PickUpKeys);
            case ActorAction.Pump:
                return Keys(p, PumpKeys);
            case ActorAction.Hammer:
                return Keys(p, HammerKeys, HammerStrike, action, mark);
            case ActorAction.Pack:
                return Keys(p, PackKeys);
            case ActorAction.Grind: {
                    // The pestle goes twice round the bowl's floor, the shoulders turning with it, then lifts and comes down in the middle.
                    const float rounds = .7f, top = .9f;
                    float angle = Mathf.Tau * 2 * p / rounds, across = MathF.Sin(angle);
                    float open = Mathf.SmoothStep(0, .1f, Math.Min(p, rounds - p));
                    float lift = Mathf.SmoothStep(rounds, top, p) - Mathf.SmoothStep(top, 1, p);
                    return Grinding(new Vector2(MathF.Cos(angle), across) * (GrindCircle * open), lift * PoundLift) with { Twist = across * open * 5, Head = 22 - lift * 5 };
                }
            case ActorAction.Dig:
                return Dig(p, mark is Vector3 spot ? new Vector2(spot.X, spot.Z) : null);
            case ActorAction.Attack:
                return Keys(p, AttackKeys);
            case ActorAction.Kindle:
                // The left hand holds the torch head forward while the right strikes steel down across flint four times.
                var kneel = Stance with { Lean = 14, Head = 14, RaiseL = 40, OutL = -10, ElbowL = 70, RaiseR = 52, OutR = -20, ElbowR = 80 };
                float strike = MathF.Sin(p * Mathf.Tau * 4);
                return kneel with { RaiseR = 52 + strike * 18, ElbowR = 80 - strike * 34, Twist = strike * 4, RaiseL = 40 + MathF.Sin(p * Mathf.Tau) * 4 };
            case ActorAction.TorchSwing:
                return Keys(p, TorchSwingKeys);
            case ActorAction.Brake:
                // A runner stopping hard plants the front heel well ahead with the leg nearly straight, sinks onto the bent back
                // leg and leans back against the run, arms thrown forward for balance, the weight rocking a little as it holds.
                return new Pose(Lean: -14 + wave * 2, Head: 10 + MathF.Cos(p * Mathf.Tau) * 2, RaiseL: 55, OutL: 18, ElbowL: 45, RaiseR: 40, OutR: 22, ElbowR: 55,
                    SwingL: BrakeFront.Swing, KneeL: BrakeFront.Knee, FootL: BrakeFront.Foot, SwingR: BrakeBack.Swing, KneeR: BrakeBack.Knee, FootR: BrakeBack.Foot, Hips: BrakeHips, SplayL: 4, SplayR: 4);
            case ActorAction.Tugged: {
                    // Tugged back by the waist: bent over at the hips with the arms reaching for the doorway and the head up to keep it in sight.
                    // Each tug, half the loop, jerks the hips back and the trunk lower; the front boot steps back in the air while the other stays
                    // where it stands, the hips passing back over it until it is in front.
                    float half = p * 2 % 1, back = Mathf.SmoothStep(0, 1, Math.Min(1, half / DoorTug.Heave)), heave = DoorTug.HeavePulse(half);
                    float struggle = MathF.Sin(half * Mathf.Tau), step = heave * heave;
                    var planted = Stood(TugBehind + TugStep * back, TugHips);
                    // The stepping boot stands on ground that rises under it and sinks again, its toes hanging as it swings back; it lifts and sets down gently.
                    var stepping = Stood(TugBehind + TugStep * (1 - back), TugHips - TugLift * step) + new LegAngles(0, 0, TugToes * step);
                    var (left, right) = p < .5f ? (stepping, planted) : (planted, stepping);
                    // Between tugs the player strains toward the door, the arms clawing at it.
                    return new Pose(Lean: 42 + 8 * heave - 3 * struggle, Head: -28 - 4 * heave, RaiseL: 105 - 10 * heave + 8 * struggle, OutL: 14, ElbowL: 22,
                        RaiseR: 115 - 10 * heave - 8 * struggle, OutR: 12, ElbowR: 16,
                        SwingL: left.Swing, KneeL: left.Knee, FootL: left.Foot, SwingR: right.Swing, KneeR: right.Knee, FootR: right.Foot, Hips: TugHips, SplayL: 3, SplayR: 3);
                }
            case ActorAction.Knock: {
                    // The right fist beats on a door at head height, landing as each loop starts: drawn back with the elbow bent, then thrown at the boards.
                    float drawn = p < .7f ? Mathf.SmoothStep(0, 1, p / .7f) : 1 - Mathf.SmoothStep(0, 1, (p - .7f) / .3f);
                    return Stance with { Lean = 7 - drawn * 3, Head = 4, RaiseR = 104 - drawn * 12, OutR = -6, ElbowR = 30 + drawn * 75 };
                }
            case ActorAction.Sit or ActorAction.Perch:
                var eating = Keys(p, action == ActorAction.Sit ? SitKeys : PerchKeys);
                return eating with { Lean = eating.Lean + wave * 1.2f, Head = eating.Head + wave * 1.5f };
            case ActorAction.FallBack:
                // A fall happens once: past its end, as while blending out of it, it holds its last pose.
                return Keys(Math.Min(elapsed / FallBackSeconds, .999f), FallBackKeys);
            // Both downed poses are drawn with the rig tipped onto its back, so Raise points the arms at the sky.
            case ActorAction.Mauled:
                // Pushing at the jaws over the throat with the head forced back while the legs kick low, under the wolf standing over them.
                return new Pose(Head: -32 + MathF.Cos(p * Mathf.Tau) * 6, Twist: wave * 8,
                    RaiseL: 128 + wave * 18, OutL: -8, ElbowL: 55 - wave * 20, RaiseR: 128 - wave * 18, OutR: -8, ElbowR: 55 + wave * 20,
                    SwingL: 10 + wave * 10, KneeL: 22 + wave * 14, SwingR: 10 - wave * 10, KneeR: 22 - wave * 14);
            case ActorAction.Devoured:
                // A limp body that only twitches as the pack tears at it.
                float tug = .7f * MathF.Sin(p * Mathf.Tau + .5f) + .3f * MathF.Sin(p * Mathf.Tau * 2 + 1.1f);
                return new Pose(Head: -38 + tug * 4, Twist: tug * 5, RaiseL: 18 + tug * 6, OutL: 62, ElbowL: 20, RaiseR: 14 - tug * 5, OutR: 58, ElbowR: 26,
                    SwingL: 10 + tug * 3, KneeL: 18, SwingR: 4, KneeR: 10 - tug * 3);
            case ActorAction.Protest:
                // The right forearm stays level, so the pole stays upright while the sign is shaken up and down.
                // The left hand crosses over to grip the pole below the right.
                float shake = (wave + 1) / 2;
                return new Pose(Lean: -3, Head: 4 - shake * 3, RaiseR: 30 + shake * 22, OutR: -4, ElbowR: 60 - shake * 22,
                    RaiseL: 34 + shake * 10, OutL: -34, ElbowL: 46 - shake * 6);
            default:
                float breath = wave;
                return new Pose(Lean: 1 + breath * .8f, Head: breath * 1.5f, OutL: 7 + breath, OutR: 7 + breath);
        }
    }

    // ---------- Walking, jogging and running ----------

    /// <summary>Metres a cycle of two steps carries the body in a way of moving, or zero for an action done in place.</summary>
    public static float Stride(ActorAction action) => action switch {
        ActorAction.Walk or ActorAction.Carry => WalkStride,
        ActorAction.Jog => Jogging.Stride,
        ActorAction.Run => Sprinting.Stride,
        _ => 0
    };

    /// <summary>Thigh and shin lengths and the height of the foot under the ankle, in metres.</summary>
    public const float Thigh = .38f, Shin = .37f, Sole = .07f;
    // The legs hang from this far below the hips, so the ankle is the rest of Sole above the bottom of the boot.
    // The boot's sole reaches this far ahead of the ankle to the toe and behind it to the heel, as HumanShape builds it.
    private const float LegDrop = .02f, Toe = .155f, Heel = .065f, AboveSole = Sole - LegDrop;
    internal const float Gravity = 9.8f;
    /// <summary>How far below the hip a leg's ankle is, for a thigh swung forward and a knee folded back by these angles.</summary>
    public static float Reach(float swing, float knee) => Ankle(swing, knee).Y;

    /// <summary>Where a leg's ankle is, ahead of the hip and below it, for a thigh swung forward and a knee folded back by these angles.</summary>
    internal static Vector2 Ankle(float swing, float knee) {
        float thigh = Mathf.DegToRad(swing), shin = Mathf.DegToRad(swing - knee);
        return new Vector2(Thigh * MathF.Sin(thigh) + Shin * MathF.Sin(shin), Thigh * MathF.Cos(thigh) + Shin * MathF.Cos(shin));
    }

    /// <summary>
    /// Where the toe and heel ends of a leg's boot sole are, as distances ahead of the hips and below them, for a thigh swung forward, a knee folded back and toes tipped down by these angles.
    /// </summary>
    public static (Vector2 Toe, Vector2 Heel) SoleOf(float swing, float knee, float foot) {
        float down = Mathf.DegToRad(foot - swing + knee);
        var ankle = Ankle(swing, knee) + new Vector2(0, LegDrop);
        var forward = new Vector2(MathF.Cos(down), MathF.Sin(down));
        var under = new Vector2(-MathF.Sin(down), MathF.Cos(down)) * AboveSole;
        return (ankle + forward * Toe + under, ankle - forward * Heel + under);
    }

    /// <summary>A leg's thigh swing, knee bend and foot tip in degrees, as in <see cref="Pose"/>.</summary>
    internal readonly record struct LegAngles(float Swing, float Knee, float Foot) {
        public static LegAngles operator +(LegAngles a, LegAngles b) => new(a.Swing + b.Swing, a.Knee + b.Knee, a.Foot + b.Foot);
        public static LegAngles operator -(LegAngles a, LegAngles b) => new(a.Swing - b.Swing, a.Knee - b.Knee, a.Foot - b.Foot);
        public static LegAngles operator *(LegAngles a, float by) => new(a.Swing * by, a.Knee * by, a.Foot * by);
    }

    /// <summary>
    /// A swinging leg's whole path from push-off to the next landing, through keys given as shares of the cycle since its foot landed, with how fast the angles change at each key.
    /// It leaves and arrives with the planted leg's angles and rates, so the leg never jerks at push-off or landing.
    /// </summary>
    private static (float At, LegAngles Leg, LegAngles Rate)[] SwingCurve(float stance, Func<float, LegAngles> planted, (float At, LegAngles Leg)[] keys) {
        const float step = .002f;
        var curve = new (float At, LegAngles Leg, LegAngles Rate)[keys.Length + 2];
        curve[0] = (stance, planted(stance), (planted(stance) - planted(stance - step)) * (1 / step));
        curve[^1] = (1, planted(0), (planted(step) - planted(0)) * (1 / step));
        for (int i = 0; i < keys.Length; i++) curve[i + 1] = (keys[i].At, keys[i].Leg, default);
        for (int i = 1; i < curve.Length - 1; i++) curve[i].Rate = (curve[i + 1].Leg - curve[i - 1].Leg) * (1 / (curve[i + 1].At - curve[i - 1].At));
        return curve;
    }

    /// <summary>
    /// The share of a cycle since a foot landed, from 0 up to but never 1.
    /// A moment a hair before the landing rounds to a whole cycle after it, which is the landing itself.
    /// </summary>
    private static float SinceLanding(float cycles) {
        float q = Mathf.PosMod(cycles, 1);
        return q < 1 ? q : 0;
    }

    /// <summary>A swinging leg a share <paramref name="q"/> of the cycle after its foot landed, on a smooth curve through its keys.</summary>
    private static LegAngles Swinging((float At, LegAngles Leg, LegAngles Rate)[] curve, float q) {
        int k = 0;
        while (q >= curve[k + 1].At) k++;
        var (from, to) = (curve[k], curve[k + 1]);
        float h = to.At - from.At, t = (q - from.At) / h, t2 = t * t, t3 = t2 * t;
        return from.Leg * (2 * t3 - 3 * t2 + 1) + from.Rate * ((t3 - 2 * t2 + t) * h) + to.Leg * (3 * t2 - 2 * t3) + to.Rate * ((t3 - t2) * h);
    }

    /// <summary>The thigh swing and knee bend, in degrees, that put the ankle <paramref name="ahead"/> of the hip and <paramref name="below"/> it, knee forward.</summary>
    internal static (float Swing, float Knee) LegTo(float ahead, float below) {
        float reach = Math.Min(MathF.Sqrt(ahead * ahead + below * below), Thigh + Shin - 1e-4f);
        float knee = MathF.Acos(Math.Clamp((Thigh * Thigh + Shin * Shin - reach * reach) / (2 * Thigh * Shin), -1, 1));
        float thigh = MathF.Acos(Math.Clamp((Thigh * Thigh + reach * reach - Shin * Shin) / (2 * Thigh * reach), -1, 1));
        return (Mathf.RadToDeg(MathF.Atan2(ahead, below) + thigh), 180 - Mathf.RadToDeg(knee));
    }

    /// <summary>The leg angles that put the boot's toe on the ground <paramref name="toe"/> ahead of the hips, with the foot tipped <paramref name="tilt"/> radians up about it.</summary>
    internal static LegAngles OnToe(float toe, float hips, float tilt) {
        var (swing, knee) = LegTo(toe - Toe * MathF.Cos(tilt) + AboveSole * MathF.Sin(tilt), hips - LegDrop - AboveSole * MathF.Cos(tilt) - Toe * MathF.Sin(tilt));
        return new LegAngles(swing, knee, Mathf.RadToDeg(tilt) + swing - knee);
    }

    /// <summary>The leg angles that put the boot's heel on the ground <paramref name="heel"/> ahead of the hips, with the toes tipped <paramref name="tilt"/> radians down about it.</summary>
    private static LegAngles OnHeel(float heel, float hips, float tilt) {
        var (swing, knee) = LegTo(heel + Heel * MathF.Cos(tilt) + AboveSole * MathF.Sin(tilt), hips - LegDrop - AboveSole * MathF.Cos(tilt) + Heel * MathF.Sin(tilt));
        return new LegAngles(swing, knee, Mathf.RadToDeg(tilt) + swing - knee);
    }

    /// <summary>
    /// How high above the ground the hips can stand for a leg bent <paramref name="knee"/> degrees to touch it with the toe,
    /// <paramref name="toe"/> ahead of them, the foot tipped <paramref name="tilt"/> radians down about the toe.
    /// </summary>
    internal static float ToeReach(float toe, float tilt, float knee) {
        float reach = Thigh * Thigh + Shin * Shin + 2 * Thigh * Shin * MathF.Cos(Mathf.DegToRad(knee));
        float ahead = toe - Toe * MathF.Cos(tilt) + AboveSole * MathF.Sin(tilt);
        return LegDrop + AboveSole * MathF.Cos(tilt) + Toe * MathF.Sin(tilt) + MathF.Sqrt(Math.Max(0, reach - ahead * ahead));
    }

    /// <summary>How far a leg hangs to the side of the hips' middle, toward the body's left for the left leg.</summary>
    public const float LegSide = .09f;

    // ---------- Standing in place ----------

    /// <summary>Where the boots stand in an action done in place: how far ahead of the hips the left boot's ankle is, and the right's, with the boots flat.</summary>
    private readonly record struct Footing(float Left, float Right);

    // Standing to work with the left boot ahead, squatting to gather, squatting on the heels to make something, sitting on a rock, and bending down from standing.
    private static readonly Footing StanceFeet = new(.105f, -.1925f), SquatFeet = new(.095f, -.036f), CraftFeet = new(.085f, .08f), PerchFeet = new(.43f, .44f), TogetherFeet = new(0, 0);

    /// <summary>Where an action done in place stands its boots, or null for one that moves its legs itself.</summary>
    private static Footing? FeetOf(ActorAction action) => action switch {
        ActorAction.Chop or ActorAction.SteelChop or ActorAction.AxeSwing or ActorAction.SteelAxeSwing or ActorAction.AxeSweep or ActorAction.SteelAxeSweep
            or ActorAction.Mine or ActorAction.SteelMine or ActorAction.Pump or ActorAction.Hammer or ActorAction.Pack or ActorAction.Grind
            or ActorAction.Attack or ActorAction.Kindle or ActorAction.TorchSwing or ActorAction.Knock => StanceFeet,
        ActorAction.Gather => SquatFeet,
        ActorAction.Craft => CraftFeet,
        ActorAction.Perch => PerchFeet,
        ActorAction.PickUp => TogetherFeet,
        _ => null
    };

    // A standing leg keeps its knee bent at least this much, and its shin leans at most this far over the foot, as far as an ankle bends under the body's weight.
    internal const float StoodKnee = 5, AnkleBend = 30;

    /// <summary>
    /// The leg angles that stand a boot on the ground, its ankle <paramref name="ankle"/> ahead of the hips when the boot is flat.
    /// The heel lifts about the toe by as much as the shin leans further over a flat foot than an ankle bends, as squatting low,
    /// and as far as it takes to bend the knee a little where the leg would be stretched straight or not reach, as with a boot far behind.
    /// </summary>
    private static LegAngles Stood(float ankle, float hips) {
        float toe = ankle + Toe;
        var flat = OnToe(toe, hips, 0);
        float tilt = Mathf.DegToRad(Math.Max(0, -AnkleBend - flat.Foot));
        if (flat.Knee < StoodKnee) tilt = Math.Max(tilt, HeelLift(toe, hips, StoodKnee));
        return tilt > 0 ? OnToe(toe, hips, tilt) : flat;
    }

    /// <summary>
    /// How far, in radians, a boot's heel lifts about its toe, <paramref name="toe"/> ahead of the hips, to leave the hip the reach of a leg bent <paramref name="knee"/> degrees at the knee.
    /// The ankle circles the toe as the heel lifts; a lift below zero means the leg is bent further than that with the boot flat.
    /// </summary>
    internal static float HeelLift(float toe, float hips, float knee) {
        float pivot = MathF.Sqrt(Toe * Toe + AboveSole * AboveSole);
        var centre = new Vector2(toe, hips - LegDrop);
        float reach = Thigh * Thigh + Shin * Shin + 2 * Thigh * Shin * MathF.Cos(Mathf.DegToRad(knee));
        float along = (centre.LengthSquared() + pivot * pivot - reach) / (2 * pivot * centre.Length());
        return MathF.Atan2(centre.Y, centre.X) - MathF.Acos(Math.Clamp(along, -1, 1)) - MathF.Atan2(AboveSole, Toe);
    }

    /// <summary>A pose with both legs bent to stand its boots on the ground where <paramref name="feet"/> has them, under hips at the pose's height.</summary>
    private static Pose OnFeet(Pose pose, Footing feet) {
        float hips = pose.HipHeight;
        var (left, right) = (Stood(feet.Left, hips), Stood(feet.Right, hips));
        return pose with { Hips = hips, SwingL = left.Swing, KneeL = left.Knee, FootL = left.Foot, SwingR = right.Swing, KneeR = right.Knee, FootR = right.Foot };
    }

    // A runner braking: the hips this high, the left heel dug in this far ahead of them with the toes up this many degrees,
    // and the right boot on its toe this far behind with the heel lifted this many degrees.
    private const float BrakeHips = .652f;
    private static readonly LegAngles BrakeFront = OnHeel(.44f, BrakeHips, -Mathf.DegToRad(25)), BrakeBack = OnToe(-.4f, BrakeHips, Mathf.DegToRad(55));

    // Tugged back by the waist: the hips this high, a boot this far behind them as a tug starts and the other a tug's distance ahead of it;
    // the stepping boot lifts this high, its toes hanging this many degrees.
    private const float TugHips = .765f, TugBehind = -.2f, TugStep = DoorTug.TugDistance / DoorTug.Tugs, TugLift = .09f, TugToes = 12;

    // ---------- Walking ----------

    // A brisk walk at a worker's pace: a cycle of two steps takes this long and covers this much ground.
    private const float WalkCycle = .66f, WalkStride = 1.485f;
    // Each foot is down for this share of the cycle, so both are down for a moment after each landing.
    // The heel lands this far ahead of the hips with the toes up this many degrees, and the foot rolls flat by this share of its time down.
    private const float WalkStance = .56f, WalkLanding = .251f, HeelStrikeTilt = 19, FootFlat = .16f;
    // The knee straightens to this bend in mid-stance, then from this share of the stance bends to this much at push-off; the heel lifts as the body passes over it.
    private const float StanceKnee = 5.5f, KneeGives = .74f, PushOffKnee = 30.5f;
    // The swinging leg after push-off, as a share of the cycle since its foot landed: the knee folds most, the toes clear the ground and the shin reaches out to land on the heel.
    private static readonly (float At, LegAngles Leg)[] WalkSwingKeys = {
        (.66f, new(-4, 54, 5)), (.74f, new(12, 62, -4)), (.8f, new(20, 52, -10)), (.87f, new(26, 30, -6)), (.94f, new(28, 8, 0))
    };
    private static readonly (float At, LegAngles Leg, LegAngles Rate)[] WalkSwing = SwingCurve(WalkStance, WalkPlanted, WalkSwingKeys);

    /// <summary>
    /// A walk, the left heel landing a quarter into the loop and the right three quarters in.
    /// The hips vault over each planted leg, highest over it and lowest while both feet are down, and the torso tips over the planted foot.
    /// The shoulders turn against the hips, and each arm swings with the opposite thigh, the elbow bending as it comes forward.
    /// </summary>
    private static Pose Walk(float p) {
        var left = WalkLeg(p - .25f);
        var right = WalkLeg(p - .75f);
        float settled = p - .25f - WalkStance / 2, lean = 4 + MathF.Cos(Mathf.Tau * 2 * settled);
        float raiseL = (right.Swing - 5) * .9f, raiseR = (left.Swing - 5) * .9f;
        return new Pose(Lean: lean, Head: 4 - lean, Twist: (left.Swing - right.Swing) * .15f, Sway: -3 * MathF.Cos(Mathf.Tau * settled),
            RaiseL: raiseL, OutL: 9, ElbowL: 20 + Math.Max(0, raiseL) * .9f, RaiseR: raiseR, OutR: 9, ElbowR: 20 + Math.Max(0, raiseR) * .9f,
            SwingL: left.Swing, KneeL: left.Knee, FootL: left.Foot, SwingR: right.Swing, KneeR: right.Knee, FootR: right.Foot,
            Hips: WalkHips(p - .25f), SplayL: -1.5f, SplayR: -1.5f);
    }

    /// <summary>The hip height a share <paramref name="sinceLanding"/> of the cycle after the left heel lands: highest over each planted leg, lowest while both feet are down.</summary>
    private static float WalkHips(float sinceLanding) =>
        .8f + .019f * MathF.Cos(2 * Mathf.Tau * (sinceLanding - WalkStance / 2 + .033f)) - .003f * MathF.Cos(4 * Mathf.Tau * (sinceLanding - .04f));

    /// <summary>A walking leg's angles a share <paramref name="sinceLanding"/> of the cycle after its heel lands.</summary>
    private static LegAngles WalkLeg(float sinceLanding) {
        float q = SinceLanding(sinceLanding);
        return q < WalkStance ? WalkPlanted(q) : Swinging(WalkSwing, q);
    }

    /// <summary>
    /// A planted walking leg a share <paramref name="q"/> of the cycle after its heel landed.
    /// The foot rolls down flat about the heel and stays flat while the knee straightens under the weight.
    /// Once the body has passed far enough over it that the knee would have to straighten past its bend, the heel lifts and the foot rolls onto its toe.
    /// </summary>
    private static LegAngles WalkPlanted(float q) {
        float u = q / WalkStance, hips = WalkHips(q), heel = WalkLanding - WalkStride * q;
        if (u < FootFlat) return OnHeel(heel, hips, -Mathf.DegToRad(HeelStrikeTilt) * (1 - MathF.Sin(MathF.PI / 2 * u / FootFlat)));
        float knee = StanceKnee + (PushOffKnee - StanceKnee) * Mathf.SmoothStep(0, 1, (u - KneeGives) / (1 - KneeGives));
        var flat = OnHeel(heel, hips, 0);
        float ahead = heel + Heel, below = hips - LegDrop - AboveSole;
        if (flat.Knee >= knee && ahead * ahead + below * below < (Thigh + Shin) * (Thigh + Shin)) return flat;
        float toe = heel + Heel + Toe;
        return OnToe(toe, hips, Math.Max(0, HeelLift(toe, hips, knee)));
    }

    // ---------- Running ----------

    /// <summary>A run's timing, landing, bounce and leg swing; a sprint and a jog differ only in these.</summary>
    private sealed class RunGait {
        /// <summary>Metres and seconds of a cycle of two steps at the speed the gait is timed for.</summary>
        public readonly float Stride, Cycle;
        /// <summary>The share of the cycle each foot is down, how far ahead of the hips its toe lands, and the hip height then.</summary>
        public readonly float Stance, Landing, HipHeight;
        /// <summary>Degrees the heel has lifted the foot by push-off.</summary>
        public readonly float PushOffTilt;
        /// <summary>How fast the hips come down from each flight under gravity.</summary>
        public readonly float LandingSpeed;
        /// <summary>The trunk's lean and the feet's draw toward one line, in degrees.</summary>
        public readonly float Lean, Splay;
        /// <summary>Degrees an arm raises for each degree of the opposite thigh's swing, and the thigh swing that leaves the arm hanging.</summary>
        public readonly float ArmGain, ArmPivot;
        public readonly (float At, LegAngles Leg, LegAngles Rate)[] Swing;

        public RunGait(float speed, float cycle, float stance, float landing, float hipHeight, float pushOffTilt, float lean, float splay, float armGain, float armPivot, (float At, LegAngles Leg)[] swing) {
            (Stride, Cycle, Stance, Landing, HipHeight, PushOffTilt) = (speed * cycle, cycle, stance, landing, hipHeight, pushOffTilt);
            (Lean, Splay, ArmGain, ArmPivot) = (lean, splay, armGain, armPivot);
            LandingSpeed = Gravity * (.5f - stance) * cycle / 2;
            Swing = SwingCurve(stance, q => Planted(this, q), swing);
        }
    }

    /// <summary>
    /// The sprint, Shift's run: each foot down for under a fifth of the cycle, about a tenth of a second, landing on the front of the foot.
    /// The swinging heel folds up under the hip, the knee drives forward and up, and the shin reaches out and sweeps back to land.
    /// </summary>
    private static readonly RunGait Sprinting = new(6.72f, .55f, .18f, .35f, .795f, 45, 13, -3.5f, 1.3f, 14.3f, new (float, LegAngles)[] {
        (.3f, new(-12, 86, 30)), (.5f, new(8, 138, 8)), (.62f, new(30, 136, -4)), (.78f, new(56, 96, -10)), (.9f, new(46, 58, -6))
    });

    /// <summary>
    /// The jog, the player's pace without Shift: longer on the ground and lower knees than the sprint, with a shorter flight and less lean.
    /// </summary>
    private static readonly RunGait Jogging = new(4.2f, .64f, .26f, .34f, .795f, 55, 7, -2.5f, 1, 8, new (float, LegAngles)[] {
        (.36f, new(-14, 72, 26)), (.52f, new(6, 108, 8)), (.64f, new(26, 106, -2)), (.8f, new(40, 66, -8)), (.92f, new(32, 36, -4))
    });

    /// <summary>
    /// A run, the left foot landing a quarter into the loop and the right three quarters in, as in a walk.
    /// The hips bounce as a ball would: a dip on the planted leg, then a rise and fall under gravity while both feet are off the ground.
    /// The feet land close to one line under the body.
    /// The trunk leans into the run, the shoulders turn against the hips, and each arm swings with the opposite thigh to balance it.
    /// </summary>
    private static Pose Run(RunGait gait, float p) {
        var left = RunLeg(gait, p - .25f);
        var right = RunLeg(gait, p - .75f);
        var (raiseL, outL, elbowL) = RunArm(gait, right.Swing);
        var (raiseR, outR, elbowR) = RunArm(gait, left.Swing);
        // Leaning furthest and tipping sideways over each planted foot as it takes the landing, with the head pitching against the lean to keep the eyes level.
        float settled = p - .25f - gait.Stance / 2;
        float lean = gait.Lean + 1.5f * MathF.Cos(Mathf.Tau * 2 * settled);
        return new Pose(Lean: lean, Head: -.4f * gait.Lean - (lean - gait.Lean) * .6f, Twist: (left.Swing - right.Swing) * .12f, Sway: -2 * MathF.Cos(Mathf.Tau * settled),
            RaiseL: raiseL, OutL: outL, ElbowL: elbowL, RaiseR: raiseR, OutR: outR, ElbowR: elbowR,
            SwingL: left.Swing, KneeL: left.Knee, FootL: left.Foot, SwingR: right.Swing, KneeR: right.Knee, FootR: right.Foot,
            Hips: RunHips(gait, p - .25f), SplayL: gait.Splay, SplayR: gait.Splay);
    }

    /// <summary>
    /// An arm swinging with the opposite thigh, further back than forward.
    /// The elbow bends most in front, bringing the hand up toward the chin, and opens behind; the arm drifts in toward the middle as it comes forward.
    /// </summary>
    private static (float Raise, float Out, float Elbow) RunArm(RunGait gait, float oppositeThigh) {
        float raise = (oppositeThigh - gait.ArmPivot) * gait.ArmGain;
        return (raise, 12 - Math.Max(0, raise) * .12f, 82 + raise * .45f);
    }

    /// <summary>
    /// The hip height a share <paramref name="sinceLanding"/> of the cycle after either foot lands.
    /// The hips dip on the planted leg as far as their landing speed carries them, then rise and fall in a parabola.
    /// </summary>
    private static float RunHips(RunGait gait, float sinceLanding) {
        float q = Mathf.PosMod(sinceLanding, .5f);
        if (q < gait.Stance) return gait.HipHeight - gait.LandingSpeed * gait.Stance * gait.Cycle / MathF.PI * MathF.Sin(MathF.PI * q / gait.Stance);
        float t = (q - gait.Stance) * gait.Cycle;
        return gait.HipHeight + gait.LandingSpeed * t - Gravity * t * t / 2;
    }

    /// <summary>A running leg's angles a share <paramref name="sinceLanding"/> of the cycle after its foot lands.</summary>
    private static LegAngles RunLeg(RunGait gait, float sinceLanding) {
        float q = SinceLanding(sinceLanding);
        return q < gait.Stance ? Planted(gait, q) : Swinging(gait.Swing, q);
    }

    /// <summary>
    /// A planted running leg a share <paramref name="q"/> of the cycle after landing: the toe of the boot stays where it landed while the hips pass over it.
    /// The foot lands nearly flat, settles flat, then the heel lifts and the foot rolls onto its toe to push off.
    /// </summary>
    private static LegAngles Planted(RunGait gait, float q) {
        float u = Math.Min(q / gait.Stance, 1);
        float tilt = u < .3f ? 5 * (1 - MathF.Sin(u / .3f * MathF.PI / 2)) : u < .5f ? 0 : gait.PushOffTilt * (u - .5f) * (u - .5f) / .25f;
        return OnToe(gait.Landing - gait.Stride * q, RunHips(gait, q), Mathf.DegToRad(tilt));
    }

    /// <summary>How long a change of action blends from the old pose into the new one.</summary>
    public const float BlendSeconds = .22f;

    /// <summary>The pose part way through a change of action, eased from the old action's pose at <paramref name="blend"/> 0 to the new one's at 1.</summary>
    public static Pose Blended(ActorAction from, float fromTime, bool fromMoving, ActorAction to, float time, bool moving, float blend, Vector3? mark = null) {
        var now = PoseAt(to, time, moving, mark);
        if (blend >= 1) return now;
        return Pose.Lerp(PoseAt(from, fromTime, fromMoving, mark), now, Mathf.SmoothStep(0, 1, blend));
    }

    /// <summary>
    /// Loops smoothly through keys sorted by phase; the last key eases back into the first.
    /// The key at <paramref name="strike"/>, if any, is a blow landing on something: the stroke into it speeds up all the way and stops there.
    /// </summary>
    /// <param name="mark">Where the blow at <paramref name="strike"/> lands <paramref name="aimed"/>'s tool, in the rig's frame: the blow and the keys after it move by as much as it takes.</param>
    private static Pose Keys(float phase, (float At, Pose Pose)[] keys, float strike = -1, ActorAction aimed = ActorAction.Idle, Vector3? mark = null) {
        var by = default(Pose);
        if (mark is Vector3 at)
            for (int k = 1; k < keys.Length; k++) if (keys[k].At == strike) by = Minus(Struck(keys[k - 1].Pose, keys[k].Pose, aimed, at), keys[k].Pose);
        bool moved = by != default;
        for (int i = 0; i < keys.Length; i++) {
            var from = keys[i];
            var (toAt, toPose) = i + 1 < keys.Length ? keys[i + 1] : (1f, keys[0].Pose);
            if (phase < from.At || phase >= toAt) continue;
            float t = (phase - from.At) / (toAt - from.At);
            var a = moved && from.At >= strike ? Plus(from.Pose, by) : from.Pose;
            var b = moved && toAt < 1 && toAt >= strike ? Plus(toPose, by) : toPose;
            return Pose.Lerp(a, b, toAt == strike ? 1 - MathF.Cos(t * MathF.PI / 2) : t * t * (3 - 2 * t));
        }
        return keys[0].Pose;
    }

    // ---------- Aiming a blow ----------

    /// <summary>The part of the tool that lands in a blow, in the frame of what the right fist holds, or null for an action that lands none.</summary>
    private static Vector3? WorkingEnd(ActorAction action) => action switch {
        ActorAction.Chop or ActorAction.SteelChop => ActorRig.AxeHeadPoint,
        ActorAction.Mine or ActorAction.SteelMine => ActorRig.PickPoint,
        ActorAction.Hammer => ActorRig.HammerFace,
        _ => null
    };

    /// <summary>How far the trunk may lean further back or in than a blow is keyed with, to bring its mark within the arms' reach, and turn either way to face it.</summary>
    private const float AimLeanBack = 12, AimLeanIn = 30, AimTurn = 40;
    /// <summary>Degrees the trunk leans further in for each metre a mark lies past the arms' reach, or back for each metre it lies too near.</summary>
    private const float LeanPerMetre = 90;
    /// <summary>The shares of the arm and tool's full length within which a blow lands without the trunk leaning: the elbow neither locked nor folded tight.</summary>
    private const float ReachMost = .97f, ReachLeast = .55f;

    /// <summary>
    /// A blow's key pose moved so the tool's working end lands on <paramref name="mark"/>, or as near it as the body goes.
    /// The right arm is solved to put the tool's end there, as a forearm as long as forearm and tool, the left fist following on
    /// the handle where it holds one; the trunk leans further in where the mark lies past the arm's reach, or back where it lies too near.
    /// </summary>
    private static Pose StrikeAt(Pose blow, ActorAction action, Vector3 mark) {
        if (WorkingEnd(action) is not Vector3 end) return blow;
        var held = new Basis(Vector3.Up, Mathf.DegToRad(blow.Grip)) * end;
        float along = ActorRig.Forearm - held.Y, palm = ActorRig.Palm + held.Z;
        float full = ActorRig.UpperArm + MathF.Sqrt(along * along + palm * palm);
        var shoulder = HumanShape.Standard.Shoulder(false);
        var pose = blow;
        var goal = mark;
        var (best, nearest) = (blow, float.MaxValue);
        for (int i = 0; i < 12; i++) {
            var inTrunk = ArmReach.TrunkBasis(pose).Inverse() * (goal - Vector3.Up * pose.HipHeight);
            float far = inTrunk.DistanceTo(shoulder) / full;
            float lean = far > ReachMost ? (far - ReachMost) * full * LeanPerMetre : far < ReachLeast ? (far - ReachLeast) * full * LeanPerMetre : 0;
            pose = pose with { Lean = Math.Clamp(pose.Lean + lean, blow.Lean - AimLeanBack, blow.Lean + AimLeanIn) };
            inTrunk = ArmReach.TrunkBasis(pose).Inverse() * (goal - Vector3.Up * pose.HipHeight);
            var (raise, outward, elbow) = ArmReach.ArmTo(inTrunk, left: false, palm, along);
            pose = pose with { RaiseR = raise, OutR = outward, ElbowR = elbow };
            // The tool's end sits a little to the side of the long forearm's line: the trunk turns that much further round to the
            // mark, and the arm aims part of what is left the other way, keeping the nearest try.
            var lands = ArmReach.InRightHand(pose, end);
            float round = Mathf.RadToDeg(Mathf.AngleDifference(MathF.Atan2(lands.X, lands.Z), MathF.Atan2(mark.X, mark.Z)));
            pose = pose with { Twist = Math.Clamp(pose.Twist + round, blow.Twist - AimTurn, blow.Twist + AimTurn) };
            var miss = mark - ArmReach.InRightHand(pose, end);
            if (miss.LengthSquared() < nearest) (best, nearest) = (pose, miss.LengthSquared());
            if (nearest < 1e-5f) break;
            goal += miss * .7f;
        }
        return action is ActorAction.Hammer ? best : TwoHanded(best);
    }

    /// <summary>How near a blow's own swing has to pass a mark for the blow to land there by being cut short, in metres.</summary>
    private const float OnTheSwing = .03f;

    /// <summary>
    /// The pose a blow lands on <paramref name="mark"/> in, swung from <paramref name="raised"/> down to <paramref name="blow"/>:
    /// the swing cut short where its tool's working end meets the mark, which is where it first meets what is worked, or, for a
    /// mark the swing passes by, the blow reached out to it (<see cref="StrikeAt"/>).
    /// </summary>
    private static Pose Struck(Pose raised, Pose blow, ActorAction action, Vector3 mark) {
        if (WorkingEnd(action) is not Vector3 end) return blow;
        float Off(float share) => ArmReach.InRightHand(Pose.Lerp(raised, blow, share), end).DistanceSquaredTo(mark);
        const int steps = 24;
        float best = 1, nearest = Off(1);
        for (int i = 0; i < steps; i++) if (Off(i / (float)steps) is float off && off < nearest) (best, nearest) = (i / (float)steps, off);
        // Halve the step round the nearest share until it is a hair wide.
        for (float step = .5f / steps; step > 1e-4f; step /= 2) {
            float before = best;
            if (before - step >= 0 && Off(before - step) is float early && early < nearest) (best, nearest) = (before - step, early);
            if (before + step <= 1 && Off(before + step) is float late && late < nearest) (best, nearest) = (before + step, late);
        }
        var swung = Pose.Lerp(raised, blow, best);
        if (nearest < OnTheSwing * OnTheSwing) return swung;
        // An arm swung back behind the trunk or folded tight to reach a mark close in is no blow: the swing passes as near as it goes.
        var reached = StrikeAt(blow, action, mark);
        return reached.RaiseR < ArmBehind || reached.ElbowR > (action is ActorAction.Chop or ActorAction.SteelChop ? ChopElbowMost : ElbowMost) ? swung : reached;
    }

    /// <summary>
    /// The least raise of a striking arm, in degrees from hanging, and the most its elbow folds, for a blow reaching out to a mark;
    /// both hands on an axe fold further to cut into a trunk close by.
    /// </summary>
    private const float ArmBehind = -10, ElbowMost = 120, ChopElbowMost = 140;

    /// <summary>
    /// Where a blow's tool's working end is <paramref name="share"/> of the way through its swing down from the key before the blow to the blow, from the rig's root; null for an action that lands none.
    /// What is worked stands in the swing's way; the blow lands where the swing first meets it.
    /// </summary>
    public static Vector3? Swing(ActorAction action, float share) {
        var (keys, strike) = action switch {
            ActorAction.Chop or ActorAction.SteelChop => (ChopKeys, ChopStrike),
            ActorAction.Mine or ActorAction.SteelMine => (MineKeys, MineStrike),
            ActorAction.Hammer => (HammerKeys, HammerStrike),
            _ => (null, 0f)
        };
        if (keys == null || WorkingEnd(action) is not Vector3 end) return null;
        for (int k = 1; k < keys.Length; k++) if (keys[k].At == strike) return ArmReach.InRightHand(Pose.Lerp(keys[k - 1].Pose, keys[k].Pose, share), end);
        return null;
    }

    /// <summary>A pose with <paramref name="by"/> added to its trunk, head, arms, grip and hip height, which are what a blow moves; the legs are stood again after.</summary>
    private static Pose Plus(Pose pose, Pose by) => pose with {
        Lean = pose.Lean + by.Lean,
        Twist = pose.Twist + by.Twist,
        Head = pose.Head + by.Head,
        Sway = pose.Sway + by.Sway,
        RaiseL = pose.RaiseL + by.RaiseL,
        OutL = pose.OutL + by.OutL,
        ElbowL = pose.ElbowL + by.ElbowL,
        RaiseR = pose.RaiseR + by.RaiseR,
        OutR = pose.OutR + by.OutR,
        ElbowR = pose.ElbowR + by.ElbowR,
        Hips = pose.HipHeight + by.Hips,
        Grip = pose.Grip + by.Grip
    };

    /// <summary>How far one pose's trunk, head, arms, grip and hip height are from another's, for <see cref="Plus"/>.</summary>
    private static Pose Minus(Pose pose, Pose from) => new(
        Lean: pose.Lean - from.Lean, Twist: pose.Twist - from.Twist, Head: pose.Head - from.Head, Sway: pose.Sway - from.Sway,
        RaiseL: pose.RaiseL - from.RaiseL, OutL: pose.OutL - from.OutL, ElbowL: pose.ElbowL - from.ElbowL,
        RaiseR: pose.RaiseR - from.RaiseR, OutR: pose.OutR - from.OutR, ElbowR: pose.ElbowR - from.ElbowR,
        Hips: pose.HipHeight - from.HipHeight, Grip: pose.Grip - from.Grip);


    /// <param name="crafting">Something is being made by hand, which the player squats over while standing still.</param>
    public static ActorAction PlayerAction(
        ThingKind? resourceKind,
        bool gathering,
        bool moving,
        Inventory backpack,
        bool sprinting = false,
        bool crafting = false,
        bool fitting = false) {
        if (moving) return sprinting ? ActorAction.Run : ActorAction.Jog;
        if (!gathering || resourceKind == null) return crafting ? ActorAction.Craft : ActorAction.Idle;

        return resourceKind switch {
            ThingKind.Tree => backpack["steel_axe"] > 0 ? ActorAction.SteelChop : ActorAction.Gather,
            ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin => backpack["steel_pick"] > 0 ? ActorAction.SteelMine : ActorAction.Gather,
            ThingKind.Berries or ThingKind.Water or ThingKind.WolfCorpse or ThingKind.Leaves => ActorAction.Gather,
            ThingKind.Stump or ThingKind.Bush => backpack["steel_shovel"] > 0 ? ActorAction.Dig : ActorAction.Idle,
            // A mine is dug with a pickaxe, then its frame and rails are hammered in.
            ThingKind.Mine => fitting ? ActorAction.Hammer : ActorAction.SteelMine,
            _ => ActorAction.Idle
        };
    }

    /// <summary>What someone working at a station does there.</summary>
    public static ActorAction StationAction(Thing station, GameData data) => station.Kind switch {
        ThingKind.Packer => ActorAction.Pack,
        // At a cooking table the mortar's recipes are ground, and dough is worked by hand as goods are packed.
        ThingKind.Cooktable => data.Recipes.TryGetValue(station.Recipe, out var r) && r.Tool == "mortar" ? ActorAction.Grind : ActorAction.Pack,
        _ => ActorAction.Hammer
    };

    public static ActorAction WorkerAction(Worker worker, Thing? assignment, bool moving, GameData data) {
        // The sign hides any load in the worker's bag, which stays there until the strike ends.
        if (worker.WageDebt > 0 && !moving) return ActorAction.Protest;
        // Every other worker sits on a rock rather than the ground.
        if (worker.WorkState == WorkerWorkState.Resting && !moving) return worker.Id % 2 == 0 ? ActorAction.Perch : ActorAction.Sit;
        // A gatherer filling their hands goes on working with what they already hold.
        if (!moving && worker.WorkState == WorkerWorkState.Gathering && (assignment?.IsResource == true || assignment?.Kind == ThingKind.Farm)) {
            return assignment.Kind switch {
                ThingKind.Tree => ActorAction.Chop,
                ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin => ActorAction.Mine,
                ThingKind.Stump or ThingKind.Bush => ActorAction.Dig,
                _ => ActorAction.Gather
            };
        }
        if (worker.Bag.Count > 0) return ActorAction.Carry;
        if (moving) return ActorAction.Walk;

        return worker.WorkState switch {
            WorkerWorkState.Pumping => ActorAction.Pump,
            WorkerWorkState.Hammering when assignment != null && (World.Worked(assignment.Kind) || assignment.Kind == ThingKind.Mine) => StationAction(assignment, data),
            WorkerWorkState.Digging => ActorAction.Mine,
            _ => ActorAction.Idle
        };
    }
}

