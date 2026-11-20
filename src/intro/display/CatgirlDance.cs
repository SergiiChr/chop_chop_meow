using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The dances on the web page in the intro film: the side catgirls' dance in step, and the middle one's solo.
/// </summary>
internal static class CatgirlDance {
    /// <summary>Seconds per beat of the dance, at 135 beats per minute.</summary>
    public const float Beat = 4f / 9;
    private const int PhraseBeats = 12;
    /// <summary>Seconds by which a tail's tip and tied hair follow the hips.</summary>
    private const float Lag = .15f;

    /// <summary>Both dances repeat on the same beat.</summary>
    public const float SoloSeconds = PhraseBeats * Beat;

    /// <summary>
    /// Holds each drawing for two to four frames at the film's drawing rate.
    /// The opening and eye gesture have four poses to a beat, and the quicker figures have five.
    /// </summary>
    public static float PoseTime(float time) {
        float beat = Mathf.PosMod((time + .5f / WorldView.IntroFps) / Beat, PhraseBeats);
        int whole = (int)beat;
        int poses = whole < 4 || whole == 7 ? 4 : 5;
        int pose = (int)((beat - whole) * poses);
        return (whole + pose / (float)poses) * Beat;
    }

    private static readonly Vector3 LeftOut = new(1, -.1f, 0), RightOut = new(-1, -.1f, 0);
    private static readonly Vector3 LeftUp = new(1, .65f, -.1f), RightUp = new(-1, .65f, -.1f);
    private static readonly Vector3 LeftDown = new(1, -1, 0), RightDown = new(-1, -1, 0);
    private const HandShape Open = HandShape.Open, Fist = HandShape.Fist, Point = HandShape.Point, Ring = HandShape.Ring;

    /// <summary>A hand's position in the chest's frame, the direction its elbow points, and how its wrist bends.</summary>
    private readonly record struct Gesture(Vector3 Hand, Vector3 Elbow, HandShape Shape = Open, float Wrist = 0) {
        public Gesture Toward(Gesture to, float blend) =>
            new(Hand.Lerp(to.Hand, blend), Elbow.Lerp(to.Elbow, blend), blend < .5f ? Shape : to.Shape, Mathf.Lerp(Wrist, to.Wrist, blend));
    }

    /// <summary>
    /// A pose in the dance. Feet are ankle positions on the stage, the hips give the rise and shift,
    /// and the chest and head turn over them. Her left and the viewer's right are +X.
    /// </summary>
    private readonly record struct Step(Gesture Left, Gesture Right, Vector3 FootL, Vector3 FootR, Vector3 Hips, Vector3 Chest, Vector3 Head,
        float Tilt = 0, float Turn = 0, float Facing = 0, float Toes = 5) {
        public Step Toward(Step to, float blend) =>
            new(Left.Toward(to.Left, blend), Right.Toward(to.Right, blend), FootL.Lerp(to.FootL, blend), FootR.Lerp(to.FootR, blend),
                Hips.Lerp(to.Hips, blend), Chest.Lerp(to.Chest, blend), Head.Lerp(to.Head, blend),
                Mathf.Lerp(Tilt, to.Tilt, blend), Mathf.Lerp(Turn, to.Turn, blend), Mathf.Lerp(Facing, to.Facing, blend), Mathf.Lerp(Toes, to.Toes, blend));
    }

    private const float AnkleHeight = .05f;
    private static readonly Vector3 CloseL = new(.071f, AnkleHeight, .008f), CloseR = new(-.069f, AnkleHeight, 0);
    private static readonly Vector3 WideL = new(.283f, AnkleHeight, -.012f), WideR = new(-.236f, AnkleHeight, .012f);

    private static readonly Step Paws = new(
        new(new(.139f, .466f, .214f), LeftDown, Fist, 11), new(new(-.146f, .43f, .209f), RightDown, Fist, 7),
        new(.132f, AnkleHeight, -.008f), new(-.138f, AnkleHeight, .013f), new(.011f, .842f, -.018f),
        new(1, -4, 3), new(-3, 3, -4), Tilt: -3, Toes: 7);
    private static readonly Step OpenPaws = Paws with {
        Left = new(new(.127f, .374f, .239f), new(.27f, -1, .2f), Open, -26),
        Right = new(new(-.128f, .377f, .237f), new(-.27f, -1, .2f), Open, -24),
        Hips = new(-.013f, .834f, -.009f),
        Chest = new(3, 2, -3),
        Head = new(-1, -2, 4),
    };
    private static readonly Step OneHigh = Paws with {
        Left = new(new(.142f, .631f, .1f), LeftOut, Open, -16),
        Hips = new(-.037f, .838f, -.018f),
        Chest = new(-2, 6, 8),
        Head = new(1, -5, -6),
        Tilt = 4,
    };
    private static readonly Step OtherHigh = Paws with {
        Right = new(new(-.15f, .64f, .093f), RightOut, Open, -20),
        Hips = new(.034f, .838f, -.011f),
        Chest = new(-2, -7, -8),
        Head = new(0, 5, 6),
        Tilt = -4,
    };
    private static readonly Step BothHigh = Paws with {
        Left = new(new(.214f, .686f, .07f), LeftUp, Open, -10),
        Right = new(new(-.211f, .679f, .077f), RightUp, Open, -12),
        Hips = new(.004f, .855f, -.009f),
        Chest = new(-3, 2, -1),
        Head = new(-4, -2, 1),
        Tilt = 1,
    };

    /// <summary>The side dancers open their hands, alternate overhead gestures, then come back to their paws.</summary>
    private static readonly (float At, Step Step)[] ChorusSteps = {
        (0, Paws),
        (.5f * Beat, Paws with { Head = new(2, 4, -4), Left = new(new(.152f, .493f, .203f), LeftDown, Fist, 16) }),
        (Beat, OpenPaws),
        (1.5f * Beat, OpenPaws with { Right = Paws.Right }),
        (2 * Beat, OneHigh),
        (2.5f * Beat, OneHigh with { Left = new(new(.126f, .648f, .093f), LeftUp, Open, -25) }),
        (3 * Beat, OneHigh with { Hips = new(-.051f, .831f, -.017f), Head = new(4, -8, -7) }),
        (3.5f * Beat, Paws with { Hips = new(-.035f, .832f, -.033f), Left = OneHigh.Left }),
        (4 * Beat, BothHigh),
        (4.4f * Beat, BothHigh with { Left = OneHigh.Left }),
        (5 * Beat, OtherHigh),
        (5.6f * Beat, Paws with { Right = OtherHigh.Right, Head = new(2, 4, 3) }),
        (6 * Beat, OneHigh),
        (6.4f * Beat, OneHigh with { Right = new(new(-.234f, .205f, .188f), RightDown, Open, 6) }),
        (7 * Beat, OtherHigh with { Left = new(new(.234f, .222f, .189f), LeftDown, Open, 2), Head = new(1, 2, 8) }),
        (7.5f * Beat, OtherHigh with { Left = OpenPaws.Left }),
        (8 * Beat, BothHigh),
        (8.4f * Beat, BothHigh with { Left = OneHigh.Left, Head = new(-3, -5, 4) }),
        (8.8f * Beat, Paws with { Right = OtherHigh.Right }),
        (9.2f * Beat, OpenPaws),
        (9.6f * Beat, Paws with { Right = OpenPaws.Right }),
        (10 * Beat, OneHigh),
        (10.6f * Beat, OtherHigh with { Left = OneHigh.Left }),
        (11 * Beat, OtherHigh with { Left = new(new(.23f, .23f, .189f), LeftDown, Open, 7) }),
        (11.4f * Beat, OtherHigh with { Left = OpenPaws.Left }),
        (SoloSeconds, Paws),
    };

    private static readonly Step Reach = new(
        new(new(.101f, .775f, .007f), LeftUp, Point), new(new(-.027f, .362f, .419f), RightUp, Open, 19),
        CloseL, CloseR, new(0, .867f, 0), new(0, 0, -1), new(0, -2, -2));
    private static readonly Step FaceFrame = new(
        new(new(.055f, .425f, .126f), new(.16f, -1, -.04f), Open, -17),
        new(new(-.02f, .66f, .097f), RightUp, Open, -31),
        CloseL, CloseR, new(-.007f, .862f, 0), new(0, -4, -2), new(3, 8, -8));
    private static readonly Step Crossed = FaceFrame with {
        FootL = new Vector3(.045f, AnkleHeight, -.104f),
        Hips = new Vector3(-.081f, .811f, -.024f),
        Chest = new Vector3(-4, 12, -6),
        Head = new Vector3(7, 3, -12),
        Tilt = 7,
        Turn = -15,
        Facing = -7,
        Toes = -18,
    };

    private static readonly Step Flex = new(
        new(new(.212f, .581f, .048f), LeftUp, Fist, 18), new(new(-.264f, .491f, .114f), RightOut, Fist, 15),
        WideL, WideR, new(-.025f, .835f, -.015f), new(1, -4, 4), new(-3, -7, 3), Tilt: 4, Toes: 11);
    private static readonly Step EyeRing = Flex with {
        Left = new(new(-.014f, .537f, .16f), LeftUp, Ring, 12),
        Right = new(new(-.365f, .107f, .048f), RightDown, Open, -24),
        Hips = new(-.093f, .822f, -.015f),
        Chest = new(-2, 10, 12),
        Head = new(3, -8, 8),
        Tilt = 8,
        Turn = 6,
        Facing = -5,
    };
    private static readonly Step OppositeFlex = Flex with {
        Left = new(new(.264f, .495f, .114f), LeftOut, Fist, 15),
        Right = new(new(-.208f, .577f, .057f), RightUp, Fist, 18),
        Hips = new(.087f, .83f, -.015f),
        Chest = new(0, 12, -10),
        Head = new(4, 8, -6),
        Tilt = -7,
        Turn = -4,
        Facing = 4,
    };

    private static readonly Step OtherEye = OppositeFlex with {
        Left = new(new(.345f, .114f, .077f), LeftDown, Open, -25),
        Right = new(new(.01f, .537f, .161f), RightUp, Ring, 13),
        Hips = new(.086f, .823f, -.02f),
        Chest = new(-2, -10, -12),
        Head = new(3, 10, -9),
        Tilt = -8,
        Turn = -6,
        Facing = 5,
    };

    /// <summary>The opening phrase: reach, sweep, hands around the face, crossed knees, then a wide stance and an eye ring.</summary>
    private static readonly (float At, Step Step)[] SoloSteps = {
        (0, Reach),
        (.25f * Beat, Reach with {
            Left = new(new(.174f, .755f, .035f), LeftUp, Point, -9),
            Right = new(new(-.092f, .354f, .415f), RightOut, Open, 40),
        }),
        (.5f * Beat, Reach with {
            Left = new(new(.201f, .731f, .035f), LeftUp, Open),
            Right = new(new(-.204f, .296f, .383f), RightDown, Open, 6),
        }),
        (.75f * Beat, Reach with {
            Left = new(new(.381f, .458f, .062f), LeftDown, Open, 17),
            Right = new(new(-.455f, .456f, .14f), RightUp, Open, -13),
            Chest = new(-3, -4, 3),
            Head = new(-1, -3, 4),
        }),
        (Beat, Reach with {
            Left = new(new(.551f, .174f, .031f), LeftDown, Open),
            Right = new(new(-.535f, .538f, .041f), RightUp, Open, -3),
            Head = new(-4, -2, 1),
        }),
        (1.25f * Beat, FaceFrame with {
            Left = new(new(.391f, .184f, .077f), LeftDown, Open),
            Right = new(new(-.251f, .689f, .058f), RightUp, Open, -10),
        }),
        (1.5f * Beat, FaceFrame),
        (2 * Beat, FaceFrame with { Head = new(0, 7, -5), Chest = new(0, -4, -1) }),
        (2.25f * Beat, FaceFrame with {
            Right = new(new(-.041f, .713f, .068f), RightUp, Open, -16),
            Head = new(-7, 7, 2),
        }),
        (2.75f * Beat, Crossed with { FootL = new(.053f, .067f, -.075f), FootR = CloseR, Hips = new(-.068f, .822f, -.02f) }),
        (3 * Beat, Crossed),
        (3.5f * Beat, Crossed with {
            Hips = new(-.08f, .815f, -.022f), Head = new(6, 10, -13),
            Right = new(new(-.043f, .661f, .093f), RightUp, Open, -25),
        }),
        (3.75f * Beat, Crossed with { Head = new(5, 5, -11), Hips = new(-.065f, .806f, -.022f) }),
        (4 * Beat, Crossed with {
            Left = new(new(.125f, .517f, .155f), LeftUp, Fist, 10), Hips = new(-.053f, .829f, -.021f),
        }),
        (4.2f * Beat, Flex with {
            FootL = new(.173f, .074f, -.044f), FootR = Crossed.FootR, Hips = new(-.044f, .838f, -.015f),
            Left = new(new(.154f, .59f, .154f), LeftUp, Fist, 24), Right = new(new(-.357f, .354f, .171f), RightOut, Fist, 22),
        }),
        (4.4f * Beat, Flex with { FootR = Crossed.FootR, Hips = new(.03f, .829f, -.014f), Right = new(new(-.23f, .518f, .176f), RightOut, Fist, 35) }),
        (4.8f * Beat, Flex with {
            FootR = new(-.147f, .077f, .018f), Hips = new(-.027f, .82f, -.016f), Chest = new(3, -7, 4),
            Left = new(new(.156f, .519f, .09f), LeftOut, Fist, 20), Right = new(new(-.131f, .521f, .143f), RightOut, Fist, 22),
        }),
        (5 * Beat, Flex with { Right = new(new(-.255f, .54f, .106f), RightDown, Fist, 12), Head = new(1, -12, 4) }),
        (5.2f * Beat, Flex with {
            Hips = new(-.044f, .829f, -.015f), Chest = new(2, -8, 6),
            Right = new(new(-.266f, .438f, .176f), RightOut, Fist, 32), Head = new(-1, -7, 6),
        }),
        (5.4f * Beat, Flex with { Right = new(new(-.217f, .521f, .123f), RightDown, Fist, 17), Head = new(2, -9, 4) }),
        (5.6f * Beat, Flex with {
            Hips = new(-.085f, .817f, -.015f), Left = new(new(.138f, .527f, .101f), LeftUp, Fist, 11),
            Right = new(new(-.149f, .508f, .125f), RightOut, Fist, 17), Head = new(5, -7, 8), Tilt = 7,
        }),
        (6 * Beat, Flex with {
            Hips = new(-.074f, .825f, -.015f), Head = new(2, -6, 5), Tilt = 6,
            Right = new(new(-.315f, .501f, .081f), RightDown, Open, 5), Left = new(new(.204f, .562f, .064f), LeftUp, Fist, 18),
        }),
        (6.4f * Beat, EyeRing with {
            Left = new(new(.045f, .648f, .106f), LeftUp, Open, 27),
            Right = new(new(-.348f, .203f, .09f), RightDown, Open, -20),
        }),
        (7 * Beat, EyeRing),
        (7.25f * Beat, EyeRing with {
            Left = new(new(-.029f, .504f, .18f), LeftOut, Ring, 15),
            Chest = new(-1, 9, 7), Head = new(8, -9, 4),
        }),
        (7.5f * Beat, EyeRing with { Hips = new(-.092f, .821f, -.015f), Head = new(6, -7, 9) }),
        (8 * Beat, Flex with {
            Hips = new(.016f, .835f, -.015f), Left = new(new(.173f, .617f, .06f), LeftUp, Fist, 20),
            Right = new(new(-.348f, .197f, .101f), RightDown, Open, 18), Head = new(-7, 7, -8),
        }),
        (8.4f * Beat, OppositeFlex with {
            Left = new(new(.216f, .393f, .16f), LeftUp, Fist, 35),
            Right = new(new(-.136f, .528f, .101f), RightUp, Fist, 28), Head = new(8, 10, -10),
        }),
        (8.8f * Beat, OppositeFlex with {
            Left = new(new(.219f, .576f, .066f), LeftDown, Fist, 17),
            Right = new(new(-.173f, .531f, .088f), RightUp, Fist, 26),
        }),
        (9 * Beat, OppositeFlex),
        (9.4f * Beat, OppositeFlex with {
            Hips = new(.063f, .825f, -.018f), Chest = new(4, 15, -5), Head = new(6, 10, -7),
            Left = new(new(.264f, .469f, .193f), LeftOut, Fist, 28), Right = new(new(-.128f, .519f, .108f), RightUp, Fist, 21),
        }),
        (9.6f * Beat, OppositeFlex with {
            Hips = new(.039f, .834f, -.016f), Head = new(-5, 10, 0),
            Left = new(new(.256f, .559f, .091f), LeftOut, Fist, 10), Right = new(new(-.227f, .547f, .088f), RightOut, Fist, 14),
        }),
        (10 * Beat, OppositeFlex with {
            Hips = new(.028f, .833f, -.016f), Chest = new(1, -8, -2), Head = new(1, 4, -3),
            Left = new(new(.234f, .352f, .172f), LeftDown, Open, -18), Right = new(new(-.238f, .566f, .077f), RightOut, Open, 11),
        }),
        (10.4f * Beat, OtherEye with {
            Left = new(new(.306f, .188f, .086f), LeftDown, Open, -19),
            Right = new(new(-.111f, .662f, .108f), RightUp, Open, 22),
            Hips = new(.052f, .831f, -.019f), Head = new(-3, 9, -6),
        }),
        (11 * Beat, OtherEye),
        (11.2f * Beat, OtherEye with { Right = new(new(.017f, .521f, .174f), RightOut, Ring, 9), Head = new(6, 9, -6) }),
        (11.4f * Beat, OppositeFlex with { FootR = new(-.141f, .076f, .01f), Hips = new(.026f, .822f, -.012f) }),
        (11.6f * Beat, OppositeFlex with { FootR = CloseR, Hips = new(-.017f, .835f, -.01f) }),
        (11.8f * Beat, Reach with {
            FootL = new(.163f, .077f, .005f), Hips = new(-.01f, .837f, -.006f),
            Left = new(new(.178f, .634f, .123f), LeftUp, Open), Right = new(new(-.246f, .305f, .132f), RightDown, Open, 24),
        }),
        (SoloSeconds, Reach),
    };

    /// <summary>The solo of the catgirl in the middle at a moment of the dance.</summary>
    public static DanceFrame Solo(float time) => Perform(SoloSteps, PoseTime(time), 1);

    /// <summary>The side catgirls' response at the same moment of the dance.</summary>
    public static DanceFrame Chorus(float time) => Perform(ChorusSteps, PoseTime(time), 1.15f);

    private static DanceFrame Perform(ReadOnlySpan<(float At, Step Step)> steps, float time, float weight) {
        var step = Accented(steps, time, weight);
        float trailing = Accented(steps, time - Lag, weight).Hips.X;
        return PlaceDancer(step, trailing);
    }

    /// <summary>Weight drops into the beat, then rebounds. The chest, head and wrists follow the hips.</summary>
    private static Step Accented(ReadOnlySpan<(float At, Step Step)> steps, float time, float weight) {
        var step = IntroPoses.Keyed<Step>(Mathf.PosMod(time, SoloSeconds), static (from, to, blend) => from.Toward(to, blend), steps);
        float beat = time / Beat, phase = Mathf.PosMod(beat, 1);
        float down = 1 - Mathf.SmoothStep(0, .53f, phase) + Mathf.SmoothStep(.72f, 1, phase);
        float transfer = weight * MathF.Cos(MathF.PI * beat - .2f);
        float follow = weight * MathF.Cos(MathF.Tau * (beat - .19f));
        return step with {
            Hips = step.Hips + new Vector3(.017f * transfer, -.028f * down * weight, -.008f * down),
            Tilt = step.Tilt - 2.4f * transfer,
            Chest = step.Chest + new Vector3(3.8f * follow, -2.8f * transfer, 2.5f * transfer),
            Head = step.Head + new Vector3(3.5f * follow, 2.6f * transfer, -3.2f * transfer),
            Left = step.Left with { Wrist = step.Left.Wrist + 9 * follow },
            Right = step.Right with { Wrist = step.Right.Wrist + 9 * follow },
        };
    }

    private static DanceFrame PlaceDancer(Step step, float trailing) {
        var facing = new Basis(Vector3.Up, Mathf.DegToRad(step.Facing));
        var tilt = new Vector3(0, Mathf.DegToRad(step.Turn), Mathf.DegToRad(step.Tilt));
        var hips = Basis.FromEuler(tilt);
        var build = HumanShape.Slim;
        var leftLeg = hips * build.Hip(true);
        var rightLeg = hips * build.Hip(false);
        var leftFoot = facing.Inverse() * step.FootL;
        var rightFoot = facing.Inverse() * step.FootR;
        var shift = step.Hips with { Y = 0 };
        leftFoot -= shift + leftLeg;
        rightFoot -= shift + rightLeg;
        float reach = ActorAnimation.Thigh + build.Shin - .001f;
        float height = Math.Min(step.Hips.Y, Math.Min(StandingHeight(leftFoot, reach), StandingHeight(rightFoot, reach)));
        var left = PlaceFoot(hips.Inverse() * (leftFoot - Vector3.Up * height), step.Toes, build.Shin);
        var right = PlaceFoot(hips.Inverse() * (rightFoot - Vector3.Up * height), -step.Toes, build.Shin);
        var pose = new Pose(SwingL: left.Swing, KneeL: left.Knee, FootL: left.Swing - left.Knee,
            SwingR: right.Swing, KneeR: right.Knee, FootR: right.Swing - right.Knee,
            Hips: height, SplayL: left.Splay, SplayR: -right.Splay, TurnL: step.Toes, TurnR: step.Toes);
        (pose, float rollL) = ArmReach.WithHandAt(pose, true, step.Left.Hand, step.Left.Elbow, build);
        (pose, float rollR) = ArmReach.WithHandAt(pose, false, step.Right.Hand, step.Right.Elbow, build);
        var root = facing * shift;
        return new DanceFrame(pose, rollL, rollR, step.Left.Shape, step.Right.Shape, root.X, step.Hips.X / .09f, trailing / .09f,
            step.Chest, new Vector3(0, step.Turn, step.Tilt), step.Head,
            new Vector3(step.Left.Wrist, 0, 0), new Vector3(step.Right.Wrist, 0, 0), Facing: step.Facing, Depth: root.Z);
    }

    private static float StandingHeight(Vector3 foot, float reach) =>
        foot.Y + MathF.Sqrt(Math.Max(0, reach * reach - foot.X * foot.X - foot.Z * foot.Z));

    /// <summary>Finds the thigh and knee bend that put an ankle at its mark, with the toes turned out.</summary>
    private static (float Splay, float Swing, float Knee) PlaceFoot(Vector3 ankle, float toes, float shin) {
        float turn = Mathf.DegToRad(toes);
        float sideways = new Vector2(ankle.X, ankle.Y).Length();
        float splay = MathF.Atan2(ankle.X, -ankle.Y) - MathF.Asin(Math.Clamp(ankle.Z * MathF.Tan(turn) / sideways, -1, 1));
        var ankleInPlane = new Basis(Vector3.Up, -turn) * (new Basis(Vector3.Back, -splay) * ankle);
        const float thigh = ActorAnimation.Thigh;
        float knee = MathF.Acos(Math.Clamp((ankle.LengthSquared() - thigh * thigh - shin * shin) / (2 * thigh * shin), -1, 1));
        float swing = MathF.Atan2(ankleInPlane.Z, -ankleInPlane.Y) + MathF.Atan2(shin * MathF.Sin(knee), thigh + shin * MathF.Cos(knee));
        return (Mathf.RadToDeg(splay), Mathf.RadToDeg(swing), Mathf.RadToDeg(knee));
    }

}
