using System;
using Godot;

namespace ChopChopMeow;

public enum ActorAction {
    Idle,
    Walk,
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
    Craft
}

/// <summary>
/// Joint angles in degrees for the humanoid rig.
/// Raise lifts an arm forward from hanging (90 is horizontal, 180 overhead), Out swings it sideways (negative crosses the body),
/// and Swing moves a thigh forward while Knee folds the shin back.
/// Turn splays a thigh outward and Roll turns it about its own length, so a bent knee folds the shin sideways across the body.
/// Hips sets the hip height above the ground, as for sitting or running; zero keeps the lower foot on the ground instead.
/// Sway tips the torso sideways over the hips; positive tips it toward the right side.
/// </summary>
public readonly record struct Pose(
    float Lean = 0, float Twist = 0, float Head = 0,
    float RaiseL = 6, float OutL = 7, float ElbowL = 12,
    float RaiseR = 6, float OutR = 7, float ElbowR = 12,
    float SwingL = 0, float KneeL = 0, float SwingR = 0, float KneeR = 0,
    float TurnL = 0, float RollL = 0, float TurnR = 0, float RollR = 0, float Hips = 0, float Sway = 0) {
    /// <summary>Blends two poses; set hips blend by how far they sit above or below a standing foot, so a foot on or above the ground in both poses stays there.</summary>
    public static Pose Lerp(Pose a, Pose b, float t) {
        var blend = new Pose(
            Mathf.Lerp(a.Lean, b.Lean, t), Mathf.Lerp(a.Twist, b.Twist, t), Mathf.Lerp(a.Head, b.Head, t),
            Mathf.Lerp(a.RaiseL, b.RaiseL, t), Mathf.Lerp(a.OutL, b.OutL, t), Mathf.Lerp(a.ElbowL, b.ElbowL, t),
            Mathf.Lerp(a.RaiseR, b.RaiseR, t), Mathf.Lerp(a.OutR, b.OutR, t), Mathf.Lerp(a.ElbowR, b.ElbowR, t),
            Mathf.Lerp(a.SwingL, b.SwingL, t), Mathf.Lerp(a.KneeL, b.KneeL, t), Mathf.Lerp(a.SwingR, b.SwingR, t), Mathf.Lerp(a.KneeR, b.KneeR, t),
            Mathf.Lerp(a.TurnL, b.TurnL, t), Mathf.Lerp(a.RollL, b.RollL, t), Mathf.Lerp(a.TurnR, b.TurnR, t), Mathf.Lerp(a.RollR, b.RollR, t), 0, Mathf.Lerp(a.Sway, b.Sway, t));
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
        ActorAction.Walk or ActorAction.Carry => .8f,
        ActorAction.Run => .55f,
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
        _ => 1
    };

    /// <summary>Position within the current loop, from 0 inclusive to 1 exclusive.</summary>
    public static float Phase(ActorAction action, float elapsed) {
        if (!float.IsFinite(elapsed)) return 0;
        float phase = elapsed / Duration(action) % 1;
        return phase < 0 ? phase + 1 : phase;
    }

    /// <summary>Turns world-plane movement into a rig yaw in radians; the rig faces +Z, which is world +Y.</summary>
    public static float Yaw(Vector2 movement, float previous) =>
        movement.LengthSquared() < 0.0001f ? previous : MathF.Atan2(movement.X, movement.Y);

    /// <summary>The tool mesh an action shows in the right hand, or null for empty hands.</summary>
    public static string? Tool(ActorAction action) => action switch {
        ActorAction.Chop or ActorAction.SteelChop => "steel_axe",
        ActorAction.Mine or ActorAction.SteelMine => "steel_pick",
        ActorAction.Hammer => "hammer",
        ActorAction.Protest => "protest_sign",
        _ => null
    };

    private static readonly Pose Stance = new(SwingL: 16, KneeL: 16, SwingR: -10, KneeR: 10);
    private static readonly Pose Crouch = new(Lean: 38, Head: 18, SwingL: 72, KneeL: 118, SwingR: 50, KneeR: 112);

    /// <summary>Swings that play out in full on the move: the arms and body keep swinging while the legs keep walking.</summary>
    public static bool SwingsOnTheMove(ActorAction action) => action is ActorAction.TorchSwing or ActorAction.Attack or ActorAction.Chop or ActorAction.SteelChop;

    public static Pose PoseAt(ActorAction action, float elapsed, bool moving) {
        if (moving && SwingsOnTheMove(action)) {
            var legs = PoseAt(ActorAction.Walk, elapsed, true);
            return PoseAt(action, elapsed, false) with { SwingL = legs.SwingL, KneeL = legs.KneeL, SwingR = legs.SwingR, KneeR = legs.KneeR, Sway = legs.Sway };
        }
        float p = Phase(action, elapsed);
        float wave = MathF.Sin(p * Mathf.Tau);
        switch (action) {
            case ActorAction.Walk:
                // Loose arms trail the legs a little and fold at the elbow as they come forward; the shoulders turn with them.
                float arm = MathF.Sin((p - .06f) * Mathf.Tau);
                var loose = Stride(wave, p) with { Twist = wave * 7, RaiseL = 4 - arm * 30, OutL = 9 + Math.Max(0, arm) * 4, ElbowL = 14 + Math.Max(0, -arm) * 28 };
                return loose with { RaiseR = 4 + arm * 30, OutR = 9 + Math.Max(0, -arm) * 4, ElbowR = 14 + Math.Max(0, arm) * 28 };
            case ActorAction.Run: {
                    // Each foot lands ahead, the leg straightens under the body and drives off behind, then the heel folds up high as it swings through.
                    // The hips rise and fall smoothly twice a stride, lowest over the planted foot and highest after each push-off, when both feet are off the ground;
                    // a planted knee bends just enough to keep its foot on the ground under them.
                    float hips = RunHips + RunBob * MathF.Cos(Mathf.Tau * 2 * (p - .7f));
                    var (swingL, kneeL) = RunLeg(p - .25f, hips);
                    var (swingR, kneeR) = RunLeg(p - .75f, hips);
                    float drive = MathF.Cos(p * Mathf.Tau * 2);
                    return new Pose(Lean: 15 - drive * 2.5f, Head: -7 + drive * 2, Twist: wave * 9, Sway: MathF.Cos(p * Mathf.Tau) * 2,
                        RaiseL: 22 - wave * 52, OutL: 10, ElbowL: 92 + wave * 12, RaiseR: 22 + wave * 52, OutR: 10, ElbowR: 92 - wave * 12,
                        SwingL: swingL, KneeL: kneeL, SwingR: swingR, KneeR: kneeR, Hips: hips);
                }
            case ActorAction.Craft: {
                    // Squatting on the heels, leaning over the work in both hands, which keep turning and fitting it.
                    float a = MathF.Sin(p * Mathf.Tau * 3), b = MathF.Sin(p * Mathf.Tau * 2 + 1.3f), c = MathF.Sin(p * Mathf.Tau * 5 + .4f);
                    return new Pose(Lean: 30 + b * 2, Head: 24 + a * 3, Twist: a * 4,
                        RaiseL: 48 + a * 9, OutL: -22 + c * 6, ElbowL: 78 + b * 16, RaiseR: 50 - b * 8, OutR: -22 - c * 5, ElbowR: 76 + a * 14,
                        SwingL: 98, KneeL: 150, SwingR: 92, KneeR: 146, TurnL: 14, TurnR: 14);
                }
            case ActorAction.Carry:
                var carry = new Pose(Lean: -4, RaiseL: 8, OutL: 0, ElbowL: 82, RaiseR: 8, OutR: 0, ElbowR: 82);
                return moving ? Stride(wave, p) with { Lean = carry.Lean, RaiseL = carry.RaiseL, OutL = carry.OutL, ElbowL = carry.ElbowL, RaiseR = carry.RaiseR, OutR = carry.OutR, ElbowR = carry.ElbowR } : carry;
            case ActorAction.Chop or ActorAction.SteelChop:
                var ready = Stance with { Lean = 6, RaiseL = 50, OutL = -24, ElbowL = 35, RaiseR = 55, OutR = -16, ElbowR = 25 };
                return Keys(p, (0, ready),
                    (.38f, ready with { Lean = -8, Twist = 24, RaiseL = 150, OutL = -26, ElbowL = 60, RaiseR = 168, OutR = -10, ElbowR = 45 }),
                    (.52f, ready with { Lean = 18, Twist = -12, RaiseL = 80, OutL = -26, ElbowL = 14, RaiseR = 86, OutR = -12, ElbowR = 4 }),
                    (.66f, ready with { Lean = 20, Twist = -14, RaiseL = 76, OutL = -26, ElbowL = 14, RaiseR = 80, OutR = -12, ElbowR = 6 }));
            case ActorAction.Mine or ActorAction.SteelMine:
                var guard = Stance with { Lean = 8, RaiseL = 45, OutL = -24, ElbowL = 40, RaiseR = 50, OutR = -14, ElbowR = 30 };
                return Keys(p, (0, guard),
                    (.4f, guard with { Lean = -6, RaiseL = 160, OutL = -24, ElbowL = 50, RaiseR = 175, OutR = -10, ElbowR = 35 }),
                    (.55f, guard with { Lean = 32, Head = 10, RaiseL = 34, OutL = -26, ElbowL = 10, RaiseR = 36, OutR = -12, ElbowR = 0, KneeL = 28, KneeR = 22 }),
                    (.68f, guard with { Lean = 30, Head = 10, RaiseL = 32, OutL = -26, ElbowL = 12, RaiseR = 34, OutR = -12, ElbowR = 2, KneeL = 26, KneeR = 20 }));
            case ActorAction.Gather:
                // Reach with one hand, close it on the find and pull it in while the other hand reaches.
                return Keys(p, (0, Crouch with { RaiseR = 66, ElbowR = 6, OutR = -6, RaiseL = 24, ElbowL = 70 }),
                    (.22f, Crouch with { Lean = 44, RaiseR = 50, ElbowR = 34, OutR = -10, RaiseL = 30, ElbowL = 60 }),
                    (.5f, Crouch with { RaiseL = 66, ElbowL = 6, OutL = -6, RaiseR = 24, ElbowR = 70 }),
                    (.72f, Crouch with { Lean = 44, RaiseL = 50, ElbowL = 34, OutL = -10, RaiseR = 30, ElbowR = 60 }));
            case ActorAction.PickUp:
                // The gathering crouch once: drop down, reach, close the hand on the piece and stand with it held up to stow.
                return Keys(p, (0, new Pose()),
                    (.32f, Crouch with { Lean = 44, RaiseR = 64, ElbowR = 8, OutR = -6, RaiseL = 26, ElbowL = 60 }),
                    (.48f, Crouch with { Lean = 42, RaiseR = 52, ElbowR = 40, OutR = -10, RaiseL = 26, ElbowL = 60 }),
                    (.78f, new Pose(Lean: 4, Head: 8, RaiseR: 36, OutR: -14, ElbowR: 105)));
            case ActorAction.Pump:
                var lever = Stance with { Lean = 16, RaiseL = 78, OutL = -12, ElbowL = 42, RaiseR = 78, OutR = -12, ElbowR = 42 };
                // A quick shove down on the lever, a short hold, then a slower lift as the bellows refill.
                var pressed = lever with { Lean = 32, RaiseL = 44, ElbowL = 6, RaiseR = 44, ElbowR = 6, KneeL = 30, KneeR = 24 };
                return Keys(p, (0, lever), (.35f, pressed), (.5f, pressed with { Lean = 30, KneeL = 27, KneeR = 21 }));
            case ActorAction.Hammer:
                var hold = Stance with { Lean = 15, RaiseL = 48, OutL = -16, ElbowL = 52, RaiseR = 70, OutR = -8, ElbowR = 50 };
                return Keys(p, (0, hold),
                    (.35f, hold with { Lean = 8, RaiseR = 150, ElbowR = 96 }),
                    (.5f, hold with { Lean = 18, RaiseR = 58, ElbowR = 22 }),
                    (.7f, hold with { Lean = 18, RaiseR = 60, ElbowR = 24 }));
            case ActorAction.Pack:
                // Both hands rummage in the heap of goods, lift a piece across, and settle it into the crate on the other side.
                var table = Stance with { Lean = 8, Head = 20, RaiseL = 50, OutL = -14, ElbowL = 46, RaiseR = 50, OutR = -14, ElbowR = 46 };
                var heap = table with { Twist = -20, Sway = -3, RaiseL = 46, OutL = -22, ElbowL = 40, RaiseR = 56, OutR = -2, ElbowR = 50 };
                var crate = table with { Twist = 20, Sway = 3, RaiseL = 56, OutL = -2, ElbowL = 50, RaiseR = 46, OutR = -22, ElbowR = 40 };
                return Keys(p, (0, heap),
                    (.12f, heap with { RaiseL = 40, ElbowL = 52, RaiseR = 62, ElbowR = 38 }),
                    (.24f, heap with { RaiseL = 50, ElbowL = 34, RaiseR = 52, ElbowR = 58 }),
                    (.36f, heap),
                    (.5f, table with { Lean = 4, RaiseL = 70, ElbowL = 84, RaiseR = 70, ElbowR = 84 }),
                    (.62f, crate with { Lean = 14, RaiseL = 50, ElbowL = 30, RaiseR = 46, ElbowR = 28 }),
                    (.74f, crate with { Lean = 14, RaiseL = 56, ElbowL = 44, RaiseR = 42, ElbowR = 36 }),
                    (.86f, crate),
                    (.94f, table with { Lean = 6, RaiseL = 62, ElbowL = 70, RaiseR = 62, ElbowR = 70 }));
            case ActorAction.Attack:
                var fists = Stance with { Twist = 10, RaiseL = 48, OutL = -14, ElbowL = 112, RaiseR = 44, OutR = -14, ElbowR = 115 };
                return Keys(p, (0, fists),
                    (.32f, fists with { Lean = 10, Twist = -26, RaiseR = 90, OutR = -8, ElbowR = 0 }),
                    (.5f, fists with { Lean = 8, Twist = -20, RaiseR = 86, OutR = -8, ElbowR = 6 }));
            case ActorAction.Kindle:
                // The left hand holds the torch head forward while the right strikes steel down across flint four times.
                var kneel = Stance with { Lean = 14, Head = 14, RaiseL = 40, OutL = -10, ElbowL = 70, RaiseR = 52, OutR = -20, ElbowR = 80 };
                float strike = MathF.Sin(p * Mathf.Tau * 4);
                return kneel with { RaiseR = 52 + strike * 18, ElbowR = 80 - strike * 34, Twist = strike * 4, RaiseL = 40 + MathF.Sin(p * Mathf.Tau) * 4 };
            case ActorAction.TorchSwing:
                // A wide forehand sweep of the torch arm across the body.
                var torchUp = Stance with { RaiseL = 30, OutL = 4, ElbowL = 60, RaiseR = 20, OutR = 10, ElbowR = 30 };
                return Keys(p, (0, torchUp),
                    (.2f, torchUp with { Twist = 22, Lean = -4, RaiseL = 70, OutL = 50, ElbowL = 30 }),
                    (.45f, torchUp with { Twist = -24, Lean = 10, RaiseL = 95, OutL = -40, ElbowL = 8 }),
                    (.65f, torchUp with { Twist = -16, Lean = 6, RaiseL = 80, OutL = -30, ElbowL = 20 }));
            case ActorAction.Sit or ActorAction.Perch:
                // Cross-legged on the ground with hands on the knees, or on a rock with knees up and forearms on them.
                // Now and then a bite goes up to the mouth.
                var seated = action == ActorAction.Sit
                    ? new Pose(Lean: 6, Head: 2, RaiseL: 30, OutL: 22, ElbowL: 34, RaiseR: 30, OutR: 22, ElbowR: 34,
                        SwingL: 86, KneeL: 150, TurnL: 40, RollL: 90, SwingR: 82, KneeR: 150, TurnR: 40, RollR: 90, Hips: .15f)
                    : new Pose(Lean: -6, Head: 4, RaiseL: 52, OutL: -10, ElbowL: 48, RaiseR: 52, OutR: -10, ElbowR: 48, SwingL: 112, KneeL: 100, SwingR: 108, KneeR: 96);
                var bite = seated with { Head = seated.Head + 6, Lean = seated.Lean + 6, RaiseR = 40, OutR = -24, ElbowR = 140 };
                var eating = Keys(p, (0, seated), (.55f, seated), (.68f, bite), (.8f, bite), (.92f, seated));
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

    /// <summary>
    /// The legs of a walk: the torso tips over the planted leg and dips forward at each step.
    /// The planted knee gives a little as the foot takes the weight and straightens to push off; the other folds to swing through.
    /// </summary>
    private static Pose Stride(float wave, float phase) {
        float lift = MathF.Cos(phase * Mathf.Tau), step = MathF.Cos(phase * Mathf.Tau * 2);
        return new Pose(Lean: 5 + step * 1.5f, Head: -step * 1.5f, Sway: lift * 3.5f,
            SwingL: wave * 30, SwingR: -wave * 30, KneeL: WalkKnee(phase - .25f), KneeR: WalkKnee(phase - .75f));
    }

    /// <summary>A walking knee from the moment its foot lands: a small give under the weight, straight at push-off, then folded through the swing.</summary>
    private static float WalkKnee(float sinceStrike) {
        float q = Mathf.PosMod(sinceStrike, 1);
        return q < .6f ? 6 + 12 * MathF.Sin(MathF.PI * q / .6f) : 6 + 52 * MathF.Sin(MathF.PI * (q - .6f) / .4f);
    }

    /// <summary>Thigh and shin lengths and the height of the foot under the ankle, in metres.</summary>
    public const float Thigh = .38f, Shin = .37f, Sole = .07f;
    // A run carries the hips a hand lower than standing, rising and falling two centimetres about that.
    private const float RunHips = Thigh + Shin + Sole - .1f, RunBob = .02f;

    /// <summary>How far below the hip a leg's ankle is, for a thigh swung forward and a knee folded back by these angles.</summary>
    public static float Reach(float swing, float knee) => Thigh * MathF.Cos(Mathf.DegToRad(swing)) + Shin * MathF.Cos(Mathf.DegToRad(swing - knee));

    /// <summary>
    /// A running leg from the moment its foot lands ahead: it sweeps back to push off by four tenths of the stride, then folds high and reaches forward again.
    /// Its knee bends at least enough to keep the foot from going below the ground with the hips at <paramref name="hips"/>.
    /// </summary>
    private static (float Swing, float Knee) RunLeg(float sinceStrike, float hips) {
        float q = Mathf.PosMod(sinceStrike, 1);
        float swing = 40 * MathF.Cos(q * Mathf.Tau);
        float knee = q < .4f ? 8 + 22 * MathF.Sin(MathF.PI * q / .4f) : 8 + 108 * MathF.Sin(MathF.PI * (q - .4f) / .6f);
        // The knee angle that puts the ankle exactly at the ground: the shin's cosine to the vertical from what the thigh leaves of the reach.
        float shin = (hips - Sole - Thigh * MathF.Cos(Mathf.DegToRad(swing))) / Shin;
        if (shin < 1) knee = Math.Max(knee, swing + Mathf.RadToDeg(MathF.Acos(Math.Max(-1, shin))));
        return (swing, knee);
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
    private static Pose Keys(float phase, params (float At, Pose Pose)[] keys) {
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
        bool crafting = false) {
        if (moving) return sprinting ? ActorAction.Run : ActorAction.Walk;
        if (!gathering || resourceKind == null) return crafting ? ActorAction.Craft : ActorAction.Idle;

        return resourceKind switch {
            ThingKind.Tree => backpack["steel_axe"] > 0 ? ActorAction.SteelChop : ActorAction.Gather,
            ThingKind.Stone or ThingKind.Iron or ThingKind.Coal or ThingKind.Tin => backpack["steel_pick"] > 0 ? ActorAction.SteelMine : ActorAction.Gather,
            ThingKind.Berries or ThingKind.Water or ThingKind.WolfCorpse or ThingKind.Leaves => ActorAction.Gather,
            _ => ActorAction.Idle
        };
    }

    /// <summary>What someone working at a station does there.</summary>
    public static ActorAction StationAction(ThingKind station) => station == ThingKind.Packer ? ActorAction.Pack : ActorAction.Hammer;

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
