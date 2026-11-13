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
    Dig
}

/// <summary>
/// Joint angles in degrees for the humanoid rig.
/// Raise lifts an arm forward from hanging (90 is horizontal, 180 overhead), Out swings it sideways (negative crosses the body),
/// and Swing moves a thigh forward while Knee folds the shin back.
/// Turn splays a thigh outward and Roll turns it about its own length, so a bent knee folds the shin sideways across the body.
/// Hips sets the hip height above the ground, as for sitting or running; zero keeps the lower foot on the ground instead.
/// Sway tips the torso sideways over the hips; positive tips it toward the right side.
/// Foot tips the toes down from square to the shin, and negative pulls them up.
/// Splay tips both thighs sideways away from the body, and negative draws the feet in under it.
/// </summary>
public readonly record struct Pose(
    float Lean = 0, float Twist = 0, float Head = 0,
    float RaiseL = 6, float OutL = 7, float ElbowL = 12,
    float RaiseR = 6, float OutR = 7, float ElbowR = 12,
    float SwingL = 0, float KneeL = 0, float SwingR = 0, float KneeR = 0,
    float TurnL = 0, float RollL = 0, float TurnR = 0, float RollR = 0, float Hips = 0, float Sway = 0, float FootL = 0, float FootR = 0, float Splay = 0) {
    /// <summary>Blends two poses; set hips blend by how far they sit above or below a standing foot, so a foot on or above the ground in both poses stays there.</summary>
    public static Pose Lerp(Pose a, Pose b, float t) {
        if (t <= 0) return a;
        var blend = new Pose(
            Mathf.Lerp(a.Lean, b.Lean, t), Mathf.Lerp(a.Twist, b.Twist, t), Mathf.Lerp(a.Head, b.Head, t),
            Mathf.Lerp(a.RaiseL, b.RaiseL, t), Mathf.Lerp(a.OutL, b.OutL, t), Mathf.Lerp(a.ElbowL, b.ElbowL, t),
            Mathf.Lerp(a.RaiseR, b.RaiseR, t), Mathf.Lerp(a.OutR, b.OutR, t), Mathf.Lerp(a.ElbowR, b.ElbowR, t),
            Mathf.Lerp(a.SwingL, b.SwingL, t), Mathf.Lerp(a.KneeL, b.KneeL, t), Mathf.Lerp(a.SwingR, b.SwingR, t), Mathf.Lerp(a.KneeR, b.KneeR, t),
            Mathf.Lerp(a.TurnL, b.TurnL, t), Mathf.Lerp(a.RollL, b.RollL, t), Mathf.Lerp(a.TurnR, b.TurnR, t), Mathf.Lerp(a.RollR, b.RollR, t), 0, Mathf.Lerp(a.Sway, b.Sway, t),
            Mathf.Lerp(a.FootL, b.FootL, t), Mathf.Lerp(a.FootR, b.FootR, t), Mathf.Lerp(a.Splay, b.Splay, t));
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
        ActorAction.SteelChop or ActorAction.SteelMine => .8f,
        ActorAction.Gather => 1.2f,
        ActorAction.Hammer => .6f,
        ActorAction.Attack => .875f,
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

    /// <summary>Seconds into a swing at which it lands: the punch at full stretch, the torch crossing in front, or the axe at the bottom of its stroke; zero for anything else.</summary>
    public static float Contact(ActorAction action) => action switch {
        ActorAction.Attack => .32f * Duration(action),
        ActorAction.TorchSwing => .3f * Duration(action),
        ActorAction.Chop or ActorAction.SteelChop => .5f * Duration(action),
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

    /// <summary>Turns world-plane movement into a rig yaw in radians; the rig faces +Z, which is world +Y.</summary>
    public static float Yaw(Vector2 movement, float previous) =>
        movement.LengthSquared() < 0.0001f ? previous : MathF.Atan2(movement.X, movement.Y);

    /// <summary>The tool an action shows in hand, or null for empty hands; the mortar is its pestle in the right hand and its bowl in the left.</summary>
    public static string? Tool(ActorAction action) => action switch {
        ActorAction.Chop or ActorAction.SteelChop => "steel_axe",
        ActorAction.Mine or ActorAction.SteelMine => "steel_pick",
        ActorAction.Hammer => "hammer",
        ActorAction.Protest => "protest_sign",
        ActorAction.Grind => "mortar",
        ActorAction.Dig => "steel_shovel",
        _ => null
    };

    private static readonly Pose Stance = new(SwingL: 16, KneeL: 16, SwingR: -10, KneeR: 10);
    private static readonly Pose Crouch = new(Lean: 38, Head: 18, SwingL: 72, KneeL: 118, SwingR: 50, KneeR: 112);

    // Each action's key poses are made once: every worker and player is posed every frame, and a list made per call is garbage.
    private static readonly Pose ChopReady = Stance with { Lean = 6, RaiseL = 50, OutL = -24, ElbowL = 35, RaiseR = 55, OutR = -16, ElbowR = 25 };
    private static readonly (float At, Pose Pose)[] ChopKeys = {
        (0, ChopReady),
        (.38f, ChopReady with { Lean = -8, Twist = 24, RaiseL = 150, OutL = -26, ElbowL = 60, RaiseR = 168, OutR = -10, ElbowR = 45 }),
        (.52f, ChopReady with { Lean = 18, Twist = -12, RaiseL = 80, OutL = -26, ElbowL = 14, RaiseR = 86, OutR = -12, ElbowR = 4 }),
        (.66f, ChopReady with { Lean = 20, Twist = -14, RaiseL = 76, OutL = -26, ElbowL = 14, RaiseR = 80, OutR = -12, ElbowR = 6 }),
    };
    private static readonly Pose MineGuard = Stance with { Lean = 8, RaiseL = 45, OutL = -24, ElbowL = 40, RaiseR = 50, OutR = -14, ElbowR = 30 };
    private static readonly (float At, Pose Pose)[] MineKeys = {
        (0, MineGuard),
        (.4f, MineGuard with { Lean = -6, RaiseL = 160, OutL = -24, ElbowL = 50, RaiseR = 175, OutR = -10, ElbowR = 35 }),
        (.55f, MineGuard with { Lean = 32, Head = 10, RaiseL = 34, OutL = -26, ElbowL = 10, RaiseR = 36, OutR = -12, ElbowR = 0, KneeL = 28, KneeR = 22 }),
        (.68f, MineGuard with { Lean = 30, Head = 10, RaiseL = 32, OutL = -26, ElbowL = 12, RaiseR = 34, OutR = -12, ElbowR = 2, KneeL = 26, KneeR = 20 }),
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
    private static readonly Pose PumpPressed = PumpLever with { Lean = 32, RaiseL = 44, ElbowL = 6, RaiseR = 44, ElbowR = 6, KneeL = 30, KneeR = 24 };
    private static readonly (float At, Pose Pose)[] PumpKeys = { (0, PumpLever), (.35f, PumpPressed), (.5f, PumpPressed with { Lean = 30, KneeL = 27, KneeR = 21 }) };
    private static readonly Pose HammerHold = Stance with { Lean = 15, RaiseL = 48, OutL = -16, ElbowL = 52, RaiseR = 70, OutR = -8, ElbowR = 50 };
    private static readonly (float At, Pose Pose)[] HammerKeys = {
        (0, HammerHold),
        (.35f, HammerHold with { Lean = 8, RaiseR = 150, ElbowR = 96 }),
        (.5f, HammerHold with { Lean = 18, RaiseR = 58, ElbowR = 22 }),
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
    private static readonly (float At, Pose Pose)[] AttackKeys = {
        (0, Fists),
        (.32f, Fists with { Lean = 10, Twist = -26, RaiseR = 90, OutR = -8, ElbowR = 0 }),
        (.5f, Fists with { Lean = 8, Twist = -20, RaiseR = 86, OutR = -8, ElbowR = 6 }),
    };
    // A wide forehand sweep of the torch arm across the body.
    private static readonly Pose TorchUp = Stance with { RaiseL = 30, OutL = 4, ElbowL = 60, RaiseR = 20, OutR = 10, ElbowR = 30 };
    private static readonly (float At, Pose Pose)[] TorchSwingKeys = {
        (0, TorchUp),
        (.2f, TorchUp with { Twist = 22, Lean = -4, RaiseL = 70, OutL = 50, ElbowL = 30 }),
        (.45f, TorchUp with { Twist = -24, Lean = 10, RaiseL = 95, OutL = -40, ElbowL = 8 }),
        (.65f, TorchUp with { Twist = -16, Lean = 6, RaiseL = 80, OutL = -30, ElbowL = 20 }),
    };
    // Cross-legged on the ground with hands on the knees, or on a rock with knees up and forearms on them.
    // Now and then a bite goes up to the mouth.
    private static readonly (float At, Pose Pose)[] SitKeys = Eating(new Pose(Lean: 6, Head: 2, RaiseL: 30, OutL: 22, ElbowL: 34, RaiseR: 30, OutR: 22, ElbowR: 34,
        SwingL: 86, KneeL: 150, TurnL: 40, RollL: 90, SwingR: 82, KneeR: 150, TurnR: 40, RollR: 90, Hips: .15f));
    private static readonly (float At, Pose Pose)[] PerchKeys = Eating(new Pose(Lean: -6, Head: 4, RaiseL: 52, OutL: -10, ElbowL: 48, RaiseR: 52, OutR: -10, ElbowR: 48, SwingL: 112, KneeL: 100, SwingR: 108, KneeR: 96));
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
        var bowl = ArmTo(BowlHand, left: true, ActorRig.Palm);
        var pestle = ArmTo(BowlHand + toFist.Rotated(Vector3.Right, -Mathf.DegToRad(GrindLean)), left: false, ActorRig.Palm);
        return Stance with { Lean = GrindLean, RaiseL = bowl.Raise, OutL = bowl.Out, ElbowL = bowl.Elbow, RaiseR = pestle.Raise, OutR = pestle.Out, ElbowR = pestle.Elbow };
    }

    /// <summary>
    /// The arm raise, outward swing and elbow bend, in degrees, that put a fist at <paramref name="grip"/>, measured from the hips in the trunk's own frame.
    /// With <paramref name="palm"/> it is what the fist holds that goes there, that far ahead of the forearm's line.
    /// </summary>
    public static (float Raise, float Out, float Elbow) ArmTo(Vector3 grip, bool left, float palm = 0) {
        var reach = grip - new Vector3(left ? ActorRig.ShoulderOut : -ActorRig.ShoulderOut, ActorRig.ShoulderUp, 0);
        // A held thing ahead of the forearm's line is reached by a slightly longer forearm bent a little further.
        const float upper = ActorRig.UpperArm;
        float fore = MathF.Sqrt(ActorRig.Forearm * ActorRig.Forearm + palm * palm);
        // The elbow folds until the fist is that far from the shoulder, then the shoulder swings the bent arm onto the point.
        float bend = MathF.Acos(Math.Clamp((reach.LengthSquared() - upper * upper - fore * fore) / (2 * upper * fore), -1, 1));
        float along = upper + fore * MathF.Cos(bend), ahead = fore * MathF.Sin(bend);
        float outward = MathF.Asin(Math.Clamp((left ? reach.X : -reach.X) / along, -1, 1));
        float raise = Mathf.RadToDeg(MathF.Atan2(reach.Z, -reach.Y) - MathF.Atan2(ahead, along * MathF.Cos(outward)));
        return (Mathf.PosMod(raise + 180, 360) - 180, Mathf.RadToDeg(outward), Mathf.RadToDeg(bend - MathF.Atan2(palm, ActorRig.Forearm)));
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

    // The blade is driven into the ground ahead, the weight goes onto it, the handle is pushed down to lever the load out, and the load is thrown aside to the left.
    // The handle stays in front of the right shoulder and the shaft crosses the body toward the left hand: a folded arm reaches little to the side.
    // The blade goes in well short of where a digger stands from a stump or a bush, so it enters the ground at its foot.
    private static readonly DigKey[] DigKeys = {
        new(0, 10, 0, .78f, new(.08f, .3f, .66f), 28, 24),
        new(DigStrike, 46, 0, .665f, new(.08f, -.08f, .78f), 22, 26),
        new(.32f, 49, 0, .645f, new(.08f, -.13f, .79f), 19, 26),
        new(.55f, 48, 0, .665f, new(.08f, .06f, .9f), 48, 14),
        new(.72f, 26, 24, .74f, new(.5f, .42f, .8f), 66, 38),
        new(.82f, 20, 32, .76f, new(.7f, .58f, .62f), 68, 55),
        new(.92f, 12, 12, .775f, new(.36f, .36f, .76f), 50, 36),
    };

    /// <summary>
    /// Digging with a shovel in both hands, the right on top of the handle and the left down the shaft.
    /// Both arms are solved from where the handle has to be for the blade's tip to be where the dig takes it, and the rig lays the shaft through both fists.
    /// The front foot stays flat and the back one on its toe, the heel lifting as the hips come down.
    /// </summary>
    private static Pose Dig(float p) {
        int from = DigKeys.Length - 1;
        while (from > 0 && p < DigKeys[from].At) from--;
        var (a, b) = (DigKeys[from], DigKeys[(from + 1) % DigKeys.Length]);
        float t = Mathf.SmoothStep(0, 1, (p - a.At) / ((from + 1 < DigKeys.Length ? b.At : 1) - a.At));
        float lean = Mathf.Lerp(a.Lean, b.Lean, t), twist = Mathf.Lerp(a.Twist, b.Twist, t), height = Mathf.Lerp(a.Hips, b.Hips, t);
        float pitch = Mathf.DegToRad(Mathf.Lerp(a.Pitch, b.Pitch, t)), yaw = Mathf.DegToRad(Mathf.Lerp(a.Yaw, b.Yaw, t));
        var along = new Vector3(MathF.Sin(pitch) * MathF.Sin(yaw), -MathF.Cos(pitch), MathF.Sin(pitch) * MathF.Cos(yaw));
        var top = a.Tip.Lerp(b.Tip, t) - along * ShovelLength;
        var front = OnHeel(DigFrontHeel, height, 0);
        var back = OnToe(DigBackToe, height, Mathf.DegToRad(8 + (.78f - height) * 200));
        var hips = new Vector3(0, height, 0);
        // Measured from the ground under the hips, then turned into the leaning, twisted trunk's frame.
        Vector3 InTrunk(Vector3 point) => (point - hips).Rotated(Vector3.Up, -Mathf.DegToRad(twist)).Rotated(Vector3.Right, -Mathf.DegToRad(lean));
        var right = ArmTo(InTrunk(top), left: false, ActorRig.Palm);
        var left = ArmTo(InTrunk(top + along * ShovelGrip), left: true, ActorRig.Palm);
        return new Pose(Lean: lean, Twist: twist, Head: 14 + lean * .3f, Hips: height,
            RaiseL: left.Raise, OutL: left.Out, ElbowL: left.Elbow, RaiseR: right.Raise, OutR: right.Out, ElbowR: right.Elbow,
            SwingL: front.Swing, KneeL: front.Knee, FootL: front.Foot, SwingR: back.Swing, KneeR: back.Knee, FootR: back.Foot);
    }

    /// <summary>Swings that play out in full on the move: the arms and body keep swinging while the legs keep walking.</summary>
    public static bool SwingsOnTheMove(ActorAction action) => action is ActorAction.TorchSwing or ActorAction.Attack or ActorAction.Chop or ActorAction.SteelChop;

    public static Pose PoseAt(ActorAction action, float elapsed, bool moving) {
        if (moving && SwingsOnTheMove(action)) {
            // The swing keeps its own clock, which keeps the jog's pace at the player's speed.
            var legs = PoseAt(ActorAction.Jog, elapsed, true);
            return PoseAt(action, elapsed, false) with { SwingL = legs.SwingL, KneeL = legs.KneeL, FootL = legs.FootL, SwingR = legs.SwingR, KneeR = legs.KneeR, FootR = legs.FootR, Sway = legs.Sway, Hips = legs.Hips, Splay = legs.Splay };
        }
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
                        SwingL: 98, KneeL: 150, SwingR: 92, KneeR: 146, TurnL: 14, TurnR: 14);
                }
            case ActorAction.Carry:
                var carry = new Pose(Lean: -4, RaiseL: 8, OutL: 0, ElbowL: 82, RaiseR: 8, OutR: 0, ElbowR: 82);
                return moving ? Walk(p) with { Lean = carry.Lean, Head = -carry.Lean / 2, RaiseL = carry.RaiseL, OutL = carry.OutL, ElbowL = carry.ElbowL, RaiseR = carry.RaiseR, OutR = carry.OutR, ElbowR = carry.ElbowR } : carry;
            case ActorAction.Chop or ActorAction.SteelChop:
                return Keys(p, ChopKeys);
            case ActorAction.Mine or ActorAction.SteelMine:
                return Keys(p, MineKeys);
            case ActorAction.Gather:
                return Keys(p, GatherKeys);
            case ActorAction.PickUp:
                return Keys(p, PickUpKeys);
            case ActorAction.Pump:
                return Keys(p, PumpKeys);
            case ActorAction.Hammer:
                return Keys(p, HammerKeys);
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
                return Dig(p);
            case ActorAction.Attack:
                return Keys(p, AttackKeys);
            case ActorAction.Kindle:
                // The left hand holds the torch head forward while the right strikes steel down across flint four times.
                var kneel = Stance with { Lean = 14, Head = 14, RaiseL = 40, OutL = -10, ElbowL = 70, RaiseR = 52, OutR = -20, ElbowR = 80 };
                float strike = MathF.Sin(p * Mathf.Tau * 4);
                return kneel with { RaiseR = 52 + strike * 18, ElbowR = 80 - strike * 34, Twist = strike * 4, RaiseL = 40 + MathF.Sin(p * Mathf.Tau) * 4 };
            case ActorAction.TorchSwing:
                return Keys(p, TorchSwingKeys);
            case ActorAction.Sit or ActorAction.Perch:
                var eating = Keys(p, action == ActorAction.Sit ? SitKeys : PerchKeys);
                return eating with { Lean = eating.Lean + wave * 1.2f, Head = eating.Head + wave * 1.5f };
            // Both downed poses are drawn with the rig tipped onto its back, so Raise points the arms at the sky.
            case ActorAction.Mauled:
                // Pushing at the jaws over the throat with the head forced back while the legs kick.
                return new Pose(Head: -32 + MathF.Cos(p * Mathf.Tau) * 6, Twist: wave * 8,
                    RaiseL: 128 + wave * 18, OutL: -8, ElbowL: 55 - wave * 20, RaiseR: 128 - wave * 18, OutR: -8, ElbowR: 55 + wave * 20,
                    SwingL: 24 + wave * 22, KneeL: 40 + wave * 25, SwingR: 24 - wave * 22, KneeR: 40 - wave * 25);
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
    private const float Gravity = 9.8f;
    /// <summary>How far below the hip a leg's ankle is, for a thigh swung forward and a knee folded back by these angles.</summary>
    public static float Reach(float swing, float knee) => Ankle(swing, knee).Y;

    /// <summary>Where a leg's ankle is, ahead of the hip and below it, for a thigh swung forward and a knee folded back by these angles.</summary>
    private static Vector2 Ankle(float swing, float knee) {
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
    private readonly record struct LegAngles(float Swing, float Knee, float Foot) {
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

    /// <summary>A swinging leg a share <paramref name="q"/> of the cycle after its foot landed, on a smooth curve through its keys.</summary>
    private static LegAngles Swinging((float At, LegAngles Leg, LegAngles Rate)[] curve, float q) {
        int k = 0;
        while (q >= curve[k + 1].At) k++;
        var (from, to) = (curve[k], curve[k + 1]);
        float h = to.At - from.At, t = (q - from.At) / h, t2 = t * t, t3 = t2 * t;
        return from.Leg * (2 * t3 - 3 * t2 + 1) + from.Rate * ((t3 - 2 * t2 + t) * h) + to.Leg * (3 * t2 - 2 * t3) + to.Rate * ((t3 - t2) * h);
    }

    /// <summary>The thigh swing and knee bend, in degrees, that put the ankle <paramref name="ahead"/> of the hip and <paramref name="below"/> it, knee forward.</summary>
    private static (float Swing, float Knee) LegTo(float ahead, float below) {
        float reach = Math.Min(MathF.Sqrt(ahead * ahead + below * below), Thigh + Shin - 1e-4f);
        float knee = MathF.Acos(Math.Clamp((Thigh * Thigh + Shin * Shin - reach * reach) / (2 * Thigh * Shin), -1, 1));
        float thigh = MathF.Acos(Math.Clamp((Thigh * Thigh + reach * reach - Shin * Shin) / (2 * Thigh * reach), -1, 1));
        return (Mathf.RadToDeg(MathF.Atan2(ahead, below) + thigh), 180 - Mathf.RadToDeg(knee));
    }

    /// <summary>The leg angles that put the boot's toe on the ground <paramref name="toe"/> ahead of the hips, with the foot tipped <paramref name="tilt"/> radians up about it.</summary>
    private static LegAngles OnToe(float toe, float hips, float tilt) {
        var (swing, knee) = LegTo(toe - Toe * MathF.Cos(tilt) + AboveSole * MathF.Sin(tilt), hips - LegDrop - AboveSole * MathF.Cos(tilt) - Toe * MathF.Sin(tilt));
        return new LegAngles(swing, knee, Mathf.RadToDeg(tilt) + swing - knee);
    }

    /// <summary>The leg angles that put the boot's heel on the ground <paramref name="heel"/> ahead of the hips, with the toes tipped <paramref name="tilt"/> radians down about it.</summary>
    private static LegAngles OnHeel(float heel, float hips, float tilt) {
        var (swing, knee) = LegTo(heel + Heel * MathF.Cos(tilt) + AboveSole * MathF.Sin(tilt), hips - LegDrop - AboveSole * MathF.Cos(tilt) + Heel * MathF.Sin(tilt));
        return new LegAngles(swing, knee, Mathf.RadToDeg(tilt) + swing - knee);
    }

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
            Hips: WalkHips(p - .25f), Splay: -1.5f);
    }

    /// <summary>The hip height a share <paramref name="sinceLanding"/> of the cycle after the left heel lands: highest over each planted leg, lowest while both feet are down.</summary>
    private static float WalkHips(float sinceLanding) =>
        .8f + .019f * MathF.Cos(2 * Mathf.Tau * (sinceLanding - WalkStance / 2 + .033f)) - .003f * MathF.Cos(4 * Mathf.Tau * (sinceLanding - .04f));

    /// <summary>A walking leg's angles a share <paramref name="sinceLanding"/> of the cycle after its heel lands.</summary>
    private static LegAngles WalkLeg(float sinceLanding) {
        float q = Mathf.PosMod(sinceLanding, 1);
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
        // The ankle circles the toe as the heel lifts: find the lift that leaves the hip the reach of a leg bent by this much.
        float toe = heel + Heel + Toe, pivot = MathF.Sqrt(Toe * Toe + AboveSole * AboveSole);
        var centre = new Vector2(toe, hips - LegDrop);
        float reach = Thigh * Thigh + Shin * Shin + 2 * Thigh * Shin * MathF.Cos(Mathf.DegToRad(knee));
        float along = (centre.LengthSquared() + pivot * pivot - reach) / (2 * pivot * centre.Length());
        float tilt = MathF.Atan2(centre.Y, centre.X) - MathF.Acos(Math.Clamp(along, -1, 1)) - MathF.Atan2(AboveSole, Toe);
        return OnToe(toe, hips, Math.Max(0, tilt));
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
            Hips: RunHips(gait, p - .25f), Splay: gait.Splay);
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
        float q = Mathf.PosMod(sinceLanding, 1);
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
    public static Pose Blended(ActorAction from, float fromTime, bool fromMoving, ActorAction to, float time, bool moving, float blend) {
        var now = PoseAt(to, time, moving);
        if (blend >= 1) return now;
        return Pose.Lerp(PoseAt(from, fromTime, fromMoving), now, Mathf.SmoothStep(0, 1, blend));
    }

    /// <summary>Loops smoothly through keys sorted by phase; the last key eases back into the first.</summary>
    private static Pose Keys(float phase, (float At, Pose Pose)[] keys) {
        for (int i = 0; i < keys.Length; i++) {
            var from = keys[i];
            var (toAt, toPose) = i + 1 < keys.Length ? keys[i + 1] : (1f, keys[0].Pose);
            if (phase < from.At || phase >= toAt) continue;
            float t = (phase - from.At) / (toAt - from.At);
            return Pose.Lerp(from.Pose, toPose, t * t * (3 - 2 * t));
        }
        return keys[0].Pose;
    }

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
    public static ActorAction StationAction(ThingKind station) => station switch {
        ThingKind.Packer => ActorAction.Pack,
        // A cooking table is only worked for the mortar's recipes.
        ThingKind.Cooktable => ActorAction.Grind,
        _ => ActorAction.Hammer
    };

    public static ActorAction WorkerAction(Worker worker, Thing? assignment, bool moving) {
        // The sign hides any load in the worker's bag, which stays there until the strike ends.
        if (worker.WageDebt > 0 && !moving) return ActorAction.Protest;
        // Every other worker sits on a rock rather than the ground.
        if (worker.WorkState == WorkerWorkState.Resting && !moving) return worker.Id % 2 == 0 ? ActorAction.Perch : ActorAction.Sit;
        if (worker.Bag.Count > 0) return ActorAction.Carry;
        if (moving) return ActorAction.Walk;
        if (worker.WorkState == WorkerWorkState.Gathering && (assignment?.IsResource == true || assignment?.Kind == ThingKind.Farm)) {
            return assignment.Kind switch {
                ThingKind.Tree => ActorAction.Chop,
                ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin => ActorAction.Mine,
                ThingKind.Stump or ThingKind.Bush => ActorAction.Dig,
                _ => ActorAction.Gather
            };
        }

        return worker.WorkState switch {
            WorkerWorkState.Pumping => ActorAction.Pump,
            WorkerWorkState.Hammering when assignment != null && (World.Worked(assignment.Kind) || assignment.Kind == ThingKind.Mine) => StationAction(assignment.Kind),
            WorkerWorkState.Digging => ActorAction.Mine,
            _ => ActorAction.Idle
        };
    }
}
