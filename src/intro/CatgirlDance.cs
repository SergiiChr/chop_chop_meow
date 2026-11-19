using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A catgirl at one moment of a dance, with what a <see cref="ChopChopMeow.Pose"/> cannot hold.
/// </summary>
/// <param name="RollL">The turn of the left upper arm about its own length (<see cref="ActorRig.RollArm"/>), in degrees.</param>
/// <param name="Shift">How far she has stepped to her left, in metres.</param>
/// <param name="Swing">How far she sways to her left, from -1 to 1.</param>
/// <param name="Trail">The same a moment ago, for the tail's tip and tied hair that follow.</param>
/// <param name="Chest">The chest on the belly: a lean forward, a twist toward her left and a tip toward her right shoulder, in degrees.</param>
/// <param name="Hips">The hips with all they carry: a tuck forward, a turn toward her left and a tilt lifting the left hip, in degrees.</param>
/// <param name="SplayL">Degrees the left thigh tips out sideways.</param>
/// <param name="Head">The head on top of the pose: a nod down, a turn toward her left and a tilt toward her right shoulder, in degrees.</param>
/// <param name="WristL">The left hand at the wrist: a bend toward its palm, a twist of the forearm and a tip toward the thumb, in degrees.</param>
/// <param name="ShoulderL">How far the left shoulder is shrugged up and hunched forward, in metres.</param>
/// <param name="Facing">Degrees her whole body is turned toward her left.</param>
/// <param name="Lift">How far off the floor she is, in metres, for a jump.</param>
internal readonly record struct DanceFrame(Pose Pose, float RollL, float RollR, HandShape HandL, HandShape HandR, float Shift, float Swing, float Trail,
    Vector3 Chest = default, Vector3 Hips = default, float SplayL = 0, float SplayR = 0, Vector3 Head = default, Vector3 WristL = default, Vector3 WristR = default,
    Vector2 ShoulderL = default, Vector2 ShoulderR = default, float Facing = 0, float Lift = 0);

/// <summary>The dances on the web page in the intro film: one the catgirls at the sides do in step, and the solo of the one between them.</summary>
internal static class CatgirlDance {
    /// <summary>Seconds a beat of the dance at the sides lasts.</summary>
    private const float Beat = .5f;
    /// <summary>Seconds by which a tail's tip and tied hair follow the hips.</summary>
    private const float Lag = .15f;

    private static float Sway(float time) => MathF.Sin(time / Beat * MathF.PI);

    /// <summary>
    /// A dance of eight beats: bouncing on the knees with the hips swaying, the fists up by the chin like paws, pawing in turn.
    /// Over its second half both arms wave overhead with open hands.
    /// </summary>
    public static DanceFrame Chorus(float time) {
        float beat = time / Beat, sway = Sway(time), knee = 10 + 26 * MathF.Abs(sway);
        var paws = new Pose(Lean: 6, Sway: 9 * sway, Twist: 10 * sway, Head: -4,
            RaiseL: 62 + 22 * sway, OutL: 24, ElbowL: 128, RaiseR: 62 - 22 * sway, OutR: 24, ElbowR: 128,
            SwingL: knee / 2, KneeL: knee, SwingR: knee / 2, KneeR: knee);
        var waving = paws with { RaiseL = 158, OutL = 14 + 18 * sway, ElbowL = 24, RaiseR = 158, OutR = 14 - 18 * sway, ElbowR = 24 };
        float bar = Mathf.PosMod(beat, 8), up = Mathf.SmoothStep(3.6f, 4, bar) - Mathf.SmoothStep(7.6f, 8, bar);
        var hands = up > .5f ? HandShape.Open : HandShape.Fist;
        // The hips swing out under the swaying chest, and the head tips with them.
        return new DanceFrame(Pose.Lerp(paws, waving, up), 0, 0, hands, hands, 0, sway, Sway(time - Lag), Hips: new Vector3(0, 0, -5 * sway), Head: new Vector3(0, 0, 8 * sway));
    }

    /// <summary>
    /// A moment the solo passes through.
    /// Each hand has a place in the chest's space, with its elbow swung toward a direction; her left is +X.
    /// </summary>
    /// <param name="Hip">Degrees the hips tilt, lifting the left one: negative puts her weight on her right leg.</param>
    /// <param name="HipTurn">Degrees the hips turn toward her left under the chest.</param>
    /// <param name="Tip">Degrees the chest tips on the belly toward her right shoulder.</param>
    /// <param name="Twist">Degrees the chest twists on the belly toward her left.</param>
    /// <param name="SplayL">Degrees the left thigh tips out sideways.</param>
    /// <param name="Kick">How far her right foot is kicked up behind her, from 0 to 1.</param>
    /// <param name="Nod">Degrees the head nods down, turns toward her left (<paramref name="Turn"/>) and tilts toward her right shoulder (<paramref name="Tilt"/>).</param>
    /// <param name="Toes">Degrees both feet turn out.</param>
    /// <param name="WristL">Degrees the left hand bends toward its palm; negative cocks it back.</param>
    /// <param name="ShrugL">How far the left shoulder is lifted, in metres.</param>
    /// <param name="Facing">Degrees her whole body is turned toward her left.</param>
    /// <param name="Lift">How far off the floor she is, in metres.</param>
    /// <param name="Bounce">Degrees her knees dip between beats.</param>
    private readonly record struct Step(float At, Vector3 HandL, Vector3 ElbowL, HandShape ShapeL, Vector3 HandR, Vector3 ElbowR, HandShape ShapeR,
        float Hip = 0, float HipTurn = 0, float Tip = 0, float Twist = 0, float SplayL = 0, float SplayR = 0, float KneeL = 0, float KneeR = 0, float Kick = 0,
        float Lean = 0, float Nod = 0, float Turn = 0, float Tilt = 0, float Toes = 0, float WristL = 0, float WristR = 0, float ShrugL = 0, float ShrugR = 0,
        float Facing = 0, float Lift = 0, float Bounce = 5) {
        /// <summary>The step part of the way to <paramref name="to"/>; a hand takes its new shape half way.</summary>
        public Step Toward(Step to, float blend) {
            float Mix(float from, float next) => Mathf.Lerp(from, next, blend);
            return new(Mix(At, to.At), HandL.Lerp(to.HandL, blend), ElbowL.Lerp(to.ElbowL, blend), blend < .5f ? ShapeL : to.ShapeL,
                HandR.Lerp(to.HandR, blend), ElbowR.Lerp(to.ElbowR, blend), blend < .5f ? ShapeR : to.ShapeR,
                Mix(Hip, to.Hip), Mix(HipTurn, to.HipTurn), Mix(Tip, to.Tip), Mix(Twist, to.Twist), Mix(SplayL, to.SplayL), Mix(SplayR, to.SplayR),
                Mix(KneeL, to.KneeL), Mix(KneeR, to.KneeR), Mix(Kick, to.Kick), Mix(Lean, to.Lean), Mix(Nod, to.Nod), Mix(Turn, to.Turn), Mix(Tilt, to.Tilt),
                Mix(Toes, to.Toes), Mix(WristL, to.WristL), Mix(WristR, to.WristR), Mix(ShrugL, to.ShrugL), Mix(ShrugR, to.ShrugR),
                Mix(Facing, to.Facing), Mix(Lift, to.Lift), Mix(Bounce, to.Bounce));
        }

        /// <summary>The same step the other way round: what her left side did, her right side does.</summary>
        public Step Mirrored(float at) => new(at, Mirror(HandR), Mirror(ElbowR), ShapeR, Mirror(HandL), Mirror(ElbowL), ShapeL,
            -Hip, -HipTurn, -Tip, -Twist, SplayR, SplayL, KneeR, KneeL, Kick, Lean, Nod, -Turn, -Tilt, Toes, WristR, WristL, ShrugR, ShrugL, -Facing, Lift, Bounce);
    }

    private static Vector3 Mirror(Vector3 left) => left with { X = -left.X };

    // Where a left hand goes, and the way its elbow swings; a right hand takes the mirror of each.
    private static readonly Vector3 Out = new(1, -.1f, 0), Down = new(.3f, -1, -.3f), High = new(1, .4f, .1f), Tucked = new(1, -.5f, 0), Back = new(.6f, -.3f, -1);
    private static readonly Vector3 Flexed = new(.27f, .68f, .04f), Low = new(.4f, .1f, .05f), Overhead = new(.1f, .86f, .04f), AtEye = new(.05f, .69f, .15f),
        Sky = new(.17f, .99f, .02f), BeforeFace = new(0, .63f, .24f), AtChest = new(.05f, .42f, .15f), AtMouth = new(.02f, .6f, .14f),
        ByCheek = new(.1f, .69f, .14f), ByShoulder = new(.2f, .62f, .1f), Raised = new(.21f, .76f, .06f), OnHip = new(.19f, .16f, .02f),
        Swept = new(.34f, .2f, .2f), Wide = new(.5f, .52f, .06f);

    private const HandShape Open = HandShape.Open, Fist = HandShape.Fist, Point = HandShape.Point, Ring = HandShape.Ring;
    /// <summary>Seconds a bar of the solo lasts: four beats.</summary>
    private const float Bar = 2;

    // The steps the solo is made of, each for the left side; the other side is its mirror.
    /// <summary>Fists flexed by the head over a wide stance with the toes out, the chest twisted one way and the hips turned the other, shoulders up.</summary>
    private static Step Shimmy(float at, float twist) =>
        new(at, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, Hip: -twist * .25f, HipTurn: -twist * .5f, Twist: twist, SplayL: 13, SplayR: 13, KneeL: 8, KneeR: 8,
            Tilt: twist * .35f, Toes: 10, WristL: 25, WristR: 25, ShrugL: .012f + twist * .0008f, ShrugR: .012f - twist * .0008f);

    /// <summary>The hips pop out to her right as the left arm arcs over her head and the right arm drops, its hand cocked back.</summary>
    private static readonly Step Pop = new(0, Overhead, High, Open, Mirror(Low), Mirror(Down), Open, Hip: -7, HipTurn: 6, Tip: 11, SplayL: 23, SplayR: 3,
        Tilt: -6, Toes: 8, WristL: 30, WristR: -45, ShrugL: .02f);
    /// <summary>The left hand rings her left eye: weight on the right leg, hips turned toward the hand, chest tipped back over them, head leaning into it.</summary>
    private static readonly Step Framed = new(0, AtEye, High, Ring, Mirror(Low), Mirror(Down), Open, Hip: -9, HipTurn: 10, Tip: 15, Twist: -8, SplayL: 27, SplayR: 1,
        Turn: 8, Tilt: -11, Toes: 8, WristR: -45, ShrugL: .02f);
    /// <summary>Both hands flick down to her sides, cocked back, as the knees dip and the head nods.</summary>
    private static readonly Step Flick = new(0, Low, Down, Open, Mirror(Low), Mirror(Down), Open, SplayL: 8, SplayR: 8, KneeL: 22, KneeR: 22, Lean: 4, Nod: 8,
        WristL: -55, WristR: -55, Toes: 6, Bounce: 0);
    /// <summary>Both fists gathered at her chest, knees bent and feet together, before a burst.</summary>
    private static readonly Step Gathered = new(0, AtChest, Tucked, Fist, Mirror(AtChest), Mirror(Tucked), Fist, KneeL: 22, KneeR: 22, Lean: 6, Nod: 8,
        ShrugL: .015f, ShrugR: .015f, Bounce: 0);
    /// <summary>A finger at the sky from a straight left arm, weight on the right leg and the left knee loose, head thrown up toward the hand.</summary>
    private static readonly Step Reached = new(0, Sky, Out, Point, Mirror(Low), Mirror(Down), Open, Hip: -6, Tip: 7, SplayL: 8, KneeL: 14, Nod: -12, Turn: 8,
        WristR: -45, ShrugL: .03f, Bounce: 2);
    /// <summary>Turned a quarter toward her left with the near hand on her hip and the far one by her cheek, hips pushed back.</summary>
    private static readonly Step Posed = new(0, OnHip, Back, Fist, Mirror(ByCheek), Mirror(Tucked), Open, Hip: -6, Tip: 9, Twist: -10, SplayL: 4, SplayR: 6, KneeL: 16,
        Turn: -24, Tilt: -8, WristR: 20, Facing: 38);
    /// <summary>On her left leg with the right foot kicked up behind, a fist at her mouth and the other arm out low.</summary>
    private static readonly Step Kicked = new(0, AtMouth, Tucked, Fist, Mirror(Low), Mirror(Down), Open, Hip: 7, Tip: -10, Kick: 1, Tilt: -7, WristR: -45, ShrugL: .015f, Bounce: 3);
    /// <summary>The left hand up beside her head, open, waving at the wrist; the right on her hip.</summary>
    private static Step Waving(float at, float wrist) =>
        new(at, Raised, Out, Open, Mirror(OnHip), Mirror(Back), Fist, Hip: wrist * .15f, Tip: -wrist * .2f, SplayL: 6, SplayR: 6, Tilt: -wrist * .2f, WristL: wrist);

    /// <summary>
    /// The solo, four bars of four beats.
    /// One: a fast shimmy, the hips pop out, a hand rings one eye, both hands flick down and she hops back into the flex.
    /// Two: the same the other way round, then she gathers her fists and bursts into a finger at the sky.
    /// Three: the finger sweeps down before her face, a quarter turn into a pose, back to the front, a hop and a kick.
    /// Four: she lands low, poses the other way, waves, sweeps both arms wide and down, and rises into the first step again.
    /// </summary>
    private static readonly Step[] SoloSteps = {
        Shimmy(0, 14), Shimmy(.125f, -14), Shimmy(.25f, 14), Shimmy(.375f, -14),
        Pop with { At = .5f }, Framed with { At = .75f }, Framed with { At = 1.05f }, Framed with { At = 1.3f, Tilt = -4, Nod = 5, ShrugL = .035f },
        Flick with { At = 1.5f },
        Shimmy(1.75f, 10) with { Lift = .035f, KneeL = 0, KneeR = 0 }, Shimmy(1.875f, 0),

        Shimmy(Bar, -14), Shimmy(Bar + .125f, 14), Shimmy(Bar + .25f, -14), Shimmy(Bar + .375f, 14),
        Pop.Mirrored(Bar + .5f), Framed.Mirrored(Bar + .75f), Framed.Mirrored(Bar + 1.05f), (Framed with { Tilt = -4, Nod = 5, ShrugL = .035f }).Mirrored(Bar + 1.3f),
        Gathered with { At = Bar + 1.5f },
        Reached with { At = Bar + 1.75f }, Reached with { At = Bar * 2 + .1f },

        new(Bar * 2 + .35f, BeforeFace, Tucked, Point, Mirror(Low), Mirror(Down), Open, Hip: 5, Tip: -8, SplayR: 7, KneeR: 10, Lean: 5, Tilt: 8, WristL: 20, WristR: -30),
        Posed with { At = Bar * 2 + .6f }, Posed with { At = Bar * 2 + .9f, Hip = -9, Tip = 13, ShrugR = .02f },
        Gathered with { At = Bar * 2 + 1.15f, KneeL = 16, KneeR = 16 },
        Gathered with { At = Bar * 2 + 1.3f, KneeL = 0, KneeR = 0, Lean = 0, Nod = 0, Lift = .05f },
        Kicked with { At = Bar * 2 + 1.5f }, Kicked with { At = Bar * 3 },

        new(Bar * 3 + .2f, ByCheek, Tucked, Point, Mirror(Low), Mirror(Down), Open, Twist: 10, KneeL: 24, KneeR: 24, Tilt: -8, WristR: -30, Bounce: 0),
        Posed.Mirrored(Bar * 3 + .45f), (Posed with { Hip = -9, Tip = 13, ShrugR = .02f }).Mirrored(Bar * 3 + .75f),
        Waving(Bar * 3 + 1, 30), Waving(Bar * 3 + 1.125f, -30), Waving(Bar * 3 + 1.25f, 30), Waving(Bar * 3 + 1.375f, -30),
        new(Bar * 3 + 1.55f, Wide, Out, Open, Mirror(Wide), Mirror(Out), Open, SplayL: 10, SplayR: 10, Nod: -6, WristL: -20, WristR: -20, ShrugL: .02f, ShrugR: .02f),
        Flick with { At = Bar * 3 + 1.75f, HandL = Swept, HandR = Mirror(Swept), SplayL = 12, SplayR = 12 },
        Shimmy(Bar * 4 - .1f, 0) with { Lift = .03f, KneeL = 0, KneeR = 0 },
        Shimmy(Bar * 4, 14),
    };

    /// <summary>Seconds the solo runs before it comes round again.</summary>
    public static float SoloSeconds => SoloSteps[^1].At;

    /// <summary>The solo of the catgirl in the middle at a moment of its loop.</summary>
    public static DanceFrame Solo(float time) {
        var step = SoloStep(time);
        // Between beats the knees dip a little, so she bounces to the beat whatever the step.
        float dip = step.Bounce * (.5f - .5f * MathF.Cos(time / Beat * MathF.Tau));
        float kneeL = step.KneeL + dip, kneeR = step.KneeR + dip + 105 * step.Kick, swingR = (step.KneeR + dip) / 2 - 12 * step.Kick;
        // Each leg reaches the floor less far the more it tips out, and the tilt lifts one hip joint and lowers the other.
        // The hips sit where the leg that reaches furthest down stands on the floor.
        float tilt = Mathf.DegToRad(step.Hip), lift = ActorAnimation.LegSide * MathF.Sin(tilt);
        float standL = ActorAnimation.Reach(kneeL / 2, kneeL) * MathF.Cos(Mathf.DegToRad(step.SplayL) + tilt) - lift;
        float standR = ActorAnimation.Reach(swingR, kneeR) * MathF.Cos(Mathf.DegToRad(step.SplayR) - tilt) + lift;
        var pose = new Pose(Lean: step.Lean, SwingL: kneeL / 2, KneeL: kneeL, SwingR: swingR, KneeR: kneeR, TurnL: step.Toes, TurnR: step.Toes,
            Hips: Math.Max(standL, standR) + ActorAnimation.Sole);
        pose = IntroPoses.Folding(pose, true, step.HandL, step.ElbowL, out float rollL, ActorRig.CatgirlShoulderOut);
        pose = IntroPoses.Folding(pose, false, step.HandR, step.ElbowR, out float rollR, ActorRig.CatgirlShoulderOut);
        // Her sway to her left is where her hips have gone: out from under the chest, the way the far hip lifts.
        return new DanceFrame(pose, rollL, rollR, step.ShapeL, step.ShapeR, step.Hip * -.006f, step.Hip / -8, SoloStep(time - Lag).Hip / -8,
            new Vector3(0, step.Twist, step.Tip), new Vector3(0, step.HipTurn, step.Hip), step.SplayL, step.SplayR, new Vector3(step.Nod, step.Turn, step.Tilt),
            new Vector3(step.WristL, 0, 0), new Vector3(step.WristR, 0, 0), new Vector2(step.ShrugL, 0), new Vector2(step.ShrugR, 0), step.Facing, step.Lift);
    }

    /// <summary>The blend of the two steps the solo is between at a moment of its loop.</summary>
    private static Step SoloStep(float time) {
        float at = Mathf.PosMod(time, SoloSeconds);
        int next = Array.FindIndex(SoloSteps, key => key.At > at);
        Step from = SoloSteps[next - 1], to = SoloSteps[next];
        return from.Toward(to, Mathf.SmoothStep(from.At, to.At, at));
    }
}
