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
        new(new(.164f, .609f, .243f), LeftDown, Fist, 11), new(new(-.172f, .564f, .238f), RightDown, Fist, 7),
        new(.132f, AnkleHeight, -.008f), new(-.138f, AnkleHeight, .013f), new(.011f, .792f, -.018f),
        new(1, -4, 3), new(-3, 3, -4), Tilt: -3, Toes: 7);
    private static readonly Step OpenPaws = Paws with {
        Left = new(new(.149f, .495f, .272f), new(.27f, -1, .2f), Open, -26),
        Right = new(new(-.151f, .499f, .269f), new(-.27f, -1, .2f), Open, -24),
        Hips = new(-.013f, .784f, -.009f),
        Chest = new(3, 2, -3),
        Head = new(-1, -2, 4),
    };
    private static readonly Step OneHigh = Paws with {
        Left = new(new(.167f, .812f, .114f), LeftOut, Open, -16),
        Hips = new(-.037f, .788f, -.018f),
        Chest = new(-2, 6, 8),
        Head = new(1, -5, -6),
        Tilt = 4,
    };
    private static readonly Step OtherHigh = Paws with {
        Right = new(new(-.177f, .824f, .106f), RightOut, Open, -20),
        Hips = new(.034f, .788f, -.011f),
        Chest = new(-2, -7, -8),
        Head = new(0, 5, 6),
        Tilt = -4,
    };
    private static readonly Step BothHigh = Paws with {
        Left = new(new(.252f, .88f, .079f), LeftUp, Open, -10),
        Right = new(new(-.248f, .872f, .087f), RightUp, Open, -12),
        Hips = new(.004f, .805f, -.009f),
        Chest = new(-3, 2, -1),
        Head = new(-4, -2, 1),
        Tilt = 1,
    };

    /// <summary>The side dancers open their hands, alternate overhead gestures, then come back to their paws.</summary>
    private static readonly (float At, Step Step)[] ChorusSteps = {
        (0, Paws),
        (.5f * Beat, Paws with { Head = new(2, 4, -4), Left = new(new(.179f, .642f, .231f), LeftDown, Fist, 16) }),
        (Beat, OpenPaws),
        (1.5f * Beat, OpenPaws with { Right = Paws.Right }),
        (2 * Beat, OneHigh),
        (2.5f * Beat, OneHigh with { Left = new(new(.148f, .833f, .106f), LeftUp, Open, -25) }),
        (3 * Beat, OneHigh with { Hips = new(-.051f, .781f, -.017f), Head = new(4, -8, -7) }),
        (3.5f * Beat, Paws with { Hips = new(-.035f, .782f, -.033f), Left = OneHigh.Left }),
        (4 * Beat, BothHigh),
        (4.5f * Beat, BothHigh with { Left = OneHigh.Left }),
        (5 * Beat, OtherHigh),
        (5.6f * Beat, Paws with { Right = OtherHigh.Right, Head = new(2, 4, 3) }),
        (6 * Beat, OneHigh),
        (6.5f * Beat, OneHigh with { Right = new(new(-.275f, .287f, .214f), RightDown, Open, 6) }),
        (7 * Beat, OtherHigh with { Left = new(new(.275f, .308f, .215f), LeftDown, Open, 2), Head = new(1, 2, 8) }),
        (7.5f * Beat, OtherHigh with { Left = OpenPaws.Left }),
        (8 * Beat, BothHigh),
        (8.4f * Beat, BothHigh with { Left = OneHigh.Left, Head = new(-3, -5, 4) }),
        (8.8f * Beat, Paws with { Right = OtherHigh.Right }),
        (9.2f * Beat, OpenPaws),
        (9.7f * Beat, Paws with { Right = OpenPaws.Right }),
        (10 * Beat, OneHigh),
        (10.6f * Beat, OtherHigh with { Left = OneHigh.Left }),
        (11 * Beat, OtherHigh with { Left = new(new(.27f, .317f, .215f), LeftDown, Open, 7) }),
        (11.4f * Beat, OtherHigh with { Left = OpenPaws.Left }),
        (SoloSeconds, Paws),
    };

    private static readonly Step Reach = new(
        new(new(.119f, .99f, .008f), LeftUp, Point), new(new(-.032f, .48f, .476f), RightUp, Open, 19),
        CloseL, CloseR, new(0, .817f, 0), new(0, 0, -1), new(0, -2, -2));
    private static readonly Step FaceFrame = new(
        new(new(.065f, .558f, .143f), new(.16f, -1, -.04f), Open, -17),
        new(new(-.024f, .848f, .11f), RightUp, Open, -31),
        CloseL, CloseR, new(-.007f, .812f, 0), new(0, -4, -2), new(3, 8, -8));
    private static readonly Step Crossed = FaceFrame with {
        FootL = new Vector3(.045f, AnkleHeight, -.104f),
        Hips = new Vector3(-.081f, .761f, -.024f),
        Chest = new Vector3(-4, 12, -6),
        Head = new Vector3(7, 3, -12),
        Tilt = 7,
        Turn = -15,
        Facing = -7,
        Toes = -18,
    };

    private static readonly Step Flex = new(
        new(new(.25f, .75f, .055f), LeftUp, Fist, 18), new(new(-.31f, .64f, .13f), RightOut, Fist, 15),
        WideL, WideR, new(-.025f, .785f, -.015f), new(1, -4, 4), new(-3, -7, 3), Tilt: 4, Toes: 11);
    private static readonly Step EyeRing = Flex with {
        Left = new(new(-.017f, .696f, .182f), LeftUp, Ring, 12),
        Right = new(new(-.43f, .166f, .055f), RightDown, Open, -24),
        Hips = new(-.093f, .772f, -.015f),
        Chest = new(-2, 10, 12),
        Head = new(3, -8, 8),
        Tilt = 8,
        Turn = 6,
        Facing = -5,
    };
    private static readonly Step OppositeFlex = Flex with {
        Left = new(new(.31f, .645f, .13f), LeftOut, Fist, 15),
        Right = new(new(-.245f, .746f, .065f), RightUp, Fist, 18),
        Hips = new(.087f, .780f, -.015f),
        Chest = new(0, 12, -10),
        Head = new(4, 8, -6),
        Tilt = -7,
        Turn = -4,
        Facing = 4,
    };

    private static readonly Step OtherEye = OppositeFlex with {
        Left = new(new(.406f, .174f, .087f), LeftDown, Open, -25),
        Right = new(new(.012f, .696f, .183f), RightUp, Ring, 13),
        Hips = new(.086f, .773f, -.02f),
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
            Left = new(new(.205f, .965f, .04f), LeftUp, Point, -9),
            Right = new(new(-.108f, .47f, .472f), RightOut, Open, 40),
        }),
        (.5f * Beat, Reach with {
            Left = new(new(.236f, .936f, .04f), LeftUp, Open),
            Right = new(new(-.24f, .399f, .435f), RightDown, Open, 6),
        }),
        (.75f * Beat, Reach with {
            Left = new(new(.448f, .599f, .071f), LeftDown, Open, 17),
            Right = new(new(-.535f, .596f, .159f), RightUp, Open, -13),
            Chest = new(-3, -4, 3),
            Head = new(-1, -3, 4),
        }),
        (Beat, Reach with {
            Left = new(new(.648f, .248f, .035f), LeftDown, Open),
            Right = new(new(-.63f, .698f, .047f), RightUp, Open, -3),
            Head = new(-4, -2, 1),
        }),
        (1.25f * Beat, FaceFrame with {
            Left = new(new(.46f, .261f, .087f), LeftDown, Open),
            Right = new(new(-.295f, .884f, .066f), RightUp, Open, -10),
        }),
        (1.5f * Beat, FaceFrame),
        (2 * Beat, FaceFrame with { Head = new(0, 7, -5), Chest = new(0, -4, -1) }),
        (1.04f, FaceFrame with {
            Right = new(new(-.048f, .913f, .077f), RightUp, Open, -16),
            Head = new(-7, 7, 2),
        }),
        (1.20f, Crossed with { FootL = new(.053f, .067f, -.075f), FootR = CloseR, Hips = new(-.068f, .772f, -.02f) }),
        (3 * Beat, Crossed),
        (1.55f, Crossed with {
            Hips = new(-.08f, .765f, -.022f), Head = new(6, 10, -13),
            Right = new(new(-.051f, .849f, .106f), RightUp, Open, -25),
        }),
        (3.75f * Beat, Crossed with { Head = new(5, 5, -11), Hips = new(-.065f, .756f, -.022f) }),
        (4 * Beat, Crossed with {
            Left = new(new(.147f, .671f, .176f), LeftUp, Fist, 10), Hips = new(-.053f, .779f, -.021f),
        }),
        (4.2f * Beat, Flex with {
            FootL = new(.173f, .074f, -.044f), FootR = Crossed.FootR, Hips = new(-.044f, .788f, -.015f),
            Left = new(new(.181f, .762f, .175f), LeftUp, Fist, 24), Right = new(new(-.42f, .47f, .194f), RightOut, Fist, 22),
        }),
        (1.99f, Flex with { FootR = Crossed.FootR, Hips = new(.03f, .779f, -.014f), Right = new(new(-.27f, .673f, .20f), RightOut, Fist, 35) }),
        (2.11f, Flex with {
            FootR = new(-.147f, .077f, .018f), Hips = new(-.027f, .77f, -.016f), Chest = new(3, -7, 4),
            Left = new(new(.184f, .674f, .102f), LeftOut, Fist, 20), Right = new(new(-.154f, .677f, .163f), RightOut, Fist, 22),
        }),
        (5 * Beat, Flex with { Right = new(new(-.30f, .70f, .12f), RightDown, Fist, 12), Head = new(1, -12, 4) }),
        (2.33f, Flex with {
            Hips = new(-.044f, .779f, -.015f), Chest = new(2, -8, 6),
            Right = new(new(-.313f, .574f, .20f), RightOut, Fist, 32), Head = new(-1, -7, 6),
        }),
        (2.43f, Flex with { Right = new(new(-.255f, .677f, .14f), RightDown, Fist, 17), Head = new(2, -9, 4) }),
        (2.5f, Flex with {
            Hips = new(-.085f, .767f, -.015f), Left = new(new(.162f, .684f, .115f), LeftUp, Fist, 11),
            Right = new(new(-.175f, .661f, .142f), RightOut, Fist, 17), Head = new(5, -7, 8), Tilt = 7,
        }),
        (6 * Beat, Flex with {
            Hips = new(-.074f, .775f, -.015f), Head = new(2, -6, 5), Tilt = 6,
            Right = new(new(-.37f, .652f, .092f), RightDown, Open, 5), Left = new(new(.24f, .727f, .073f), LeftUp, Fist, 18),
        }),
        (6.4f * Beat, EyeRing with {
            Left = new(new(.053f, .833f, .121f), LeftUp, Open, 27),
            Right = new(new(-.41f, .284f, .102f), RightDown, Open, -20),
        }),
        (7 * Beat, EyeRing),
        (3.17f, EyeRing with {
            Left = new(new(-.034f, .655f, .205f), LeftOut, Ring, 15),
            Chest = new(-1, 9, 7), Head = new(8, -9, 4),
        }),
        (3.37f, EyeRing with { Hips = new(-.092f, .771f, -.015f), Head = new(6, -7, 9) }),
        (8 * Beat, Flex with {
            Hips = new(.016f, .785f, -.015f), Left = new(new(.204f, .795f, .068f), LeftUp, Fist, 20),
            Right = new(new(-.41f, .277f, .115f), RightDown, Open, 18), Head = new(-7, 7, -8),
        }),
        (3.73f, OppositeFlex with {
            Left = new(new(.254f, .518f, .182f), LeftUp, Fist, 35),
            Right = new(new(-.16f, .685f, .115f), RightUp, Fist, 28), Head = new(8, 10, -10),
        }),
        (3.88f, OppositeFlex with {
            Left = new(new(.258f, .744f, .075f), LeftDown, Fist, 17),
            Right = new(new(-.204f, .689f, .10f), RightUp, Fist, 26),
        }),
        (9 * Beat, OppositeFlex),
        (9.32f * Beat, OppositeFlex with {
            Hips = new(.063f, .775f, -.018f), Chest = new(4, 15, -5), Head = new(6, 10, -7),
            Left = new(new(.31f, .612f, .219f), LeftOut, Fist, 28), Right = new(new(-.151f, .674f, .123f), RightUp, Fist, 21),
        }),
        (9.64f * Beat, OppositeFlex with {
            Hips = new(.039f, .784f, -.016f), Head = new(-5, 10, 0),
            Left = new(new(.301f, .724f, .103f), LeftOut, Fist, 10), Right = new(new(-.267f, .709f, .10f), RightOut, Fist, 14),
        }),
        (10 * Beat, OppositeFlex with {
            Hips = new(.028f, .783f, -.016f), Chest = new(1, -8, -2), Head = new(1, 4, -3),
            Left = new(new(.275f, .468f, .195f), LeftDown, Open, -18), Right = new(new(-.28f, .732f, .087f), RightOut, Open, 11),
        }),
        (10.4f * Beat, OtherEye with {
            Left = new(new(.36f, .266f, .098f), LeftDown, Open, -19),
            Right = new(new(-.13f, .851f, .123f), RightUp, Open, 22),
            Hips = new(.052f, .781f, -.019f), Head = new(-3, 9, -6),
        }),
        (11 * Beat, OtherEye),
        (11.25f * Beat, OtherEye with { Right = new(new(.02f, .676f, .198f), RightOut, Ring, 9), Head = new(6, 9, -6) }),
        (11.43f * Beat, OppositeFlex with { FootR = new(-.141f, .076f, .01f), Hips = new(.026f, .772f, -.012f) }),
        (11.56f * Beat, OppositeFlex with { FootR = CloseR, Hips = new(-.017f, .785f, -.01f) }),
        (11.76f * Beat, Reach with {
            FootL = new(.163f, .077f, .005f), Hips = new(-.01f, .787f, -.006f),
            Left = new(new(.21f, .816f, .14f), LeftUp, Open), Right = new(new(-.29f, .41f, .15f), RightDown, Open, 24),
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
        float reach = ActorAnimation.Thigh + build.ShinLength - .001f;
        // Carry the body higher by the added shin length while the feet stay on their choreographed marks.
        float wantedHeight = step.Hips.Y + (build.ShinLength - ActorAnimation.Shin);
        float height = Math.Min(wantedHeight, Math.Min(StandingHeight(leftFoot, reach), StandingHeight(rightFoot, reach)));
        var left = PlaceFoot(hips.Inverse() * (leftFoot - Vector3.Up * height), step.Toes, build.ShinLength);
        var right = PlaceFoot(hips.Inverse() * (rightFoot - Vector3.Up * height), -step.Toes, build.ShinLength);
        var pose = new Pose(SwingL: left.Swing, KneeL: left.Knee, FootL: left.Swing - left.Knee,
            SwingR: right.Swing, KneeR: right.Knee, FootR: right.Swing - right.Knee,
            Hips: height, SplayL: left.Splay, SplayR: -right.Splay, TurnL: step.Toes, TurnR: step.Toes);
        (pose, float rollL) = ArmReach.WithHandAt(pose, true, HandTarget(step.Left.Hand), step.Left.Elbow, build);
        (pose, float rollR) = ArmReach.WithHandAt(pose, false, HandTarget(step.Right.Hand), step.Right.Elbow, build);
        var root = facing * shift;
        return new DanceFrame(pose, rollL, rollR, step.Left.Shape, step.Right.Shape, root.X, step.Hips.X / .09f, trailing / .09f,
            step.Chest, new Vector3(0, step.Turn, step.Tilt), step.Head,
            new Vector3(step.Left.Wrist, 0, 0), new Vector3(step.Right.Wrist, 0, 0), Facing: step.Facing, Depth: root.Z);
    }

    /// <summary>Fits the gestures to the shorter chest and narrower head, including the eye rings.</summary>
    private static Vector3 HandTarget(Vector3 hand) => new(hand.X * .85f, hand.Y * .81f - .027f, hand.Z * .88f);

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
