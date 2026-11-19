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
/// <param name="HipTilt">Degrees the hips tilt, lifting the left one.</param>
/// <param name="SplayL">Degrees the left thigh tips out sideways.</param>
/// <param name="Head">The head on top of the pose: a nod down, a turn toward her left and a tilt toward her right shoulder, in degrees.</param>
/// <param name="WristL">Degrees the left hand bends toward its palm at the wrist.</param>
internal readonly record struct DanceFrame(Pose Pose, float RollL, float RollR, HandShape HandL, HandShape HandR, float Shift, float Swing, float Trail,
    Vector3 Chest = default, float HipTilt = 0, float SplayL = 0, float SplayR = 0, Vector3 Head = default, float WristL = 0, float WristR = 0);

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
        return new DanceFrame(Pose.Lerp(paws, waving, up), 0, 0, hands, hands, 0, sway, Sway(time - Lag), HipTilt: -5 * sway, Head: new Vector3(0, 0, 8 * sway));
    }

    /// <summary>
    /// A moment the solo passes through.
    /// Each hand has a place in the chest's space, with its elbow swung toward a direction; her left is +X.
    /// </summary>
    /// <param name="Hip">Degrees the hips tilt, lifting the left one: negative puts her weight on her right leg.</param>
    /// <param name="Tip">Degrees the chest tips on the belly toward her right shoulder.</param>
    /// <param name="Twist">Degrees the chest twists on the belly toward her left.</param>
    /// <param name="SplayL">Degrees the left thigh tips out sideways.</param>
    /// <param name="Kick">How far her right foot is kicked up behind her, from 0 to 1.</param>
    /// <param name="Nod">Degrees the head nods down, turns toward her left (<paramref name="Turn"/>) and tilts toward her right shoulder (<paramref name="Tilt"/>).</param>
    /// <param name="Toes">Degrees both feet turn out.</param>
    /// <param name="WristL">Degrees the left hand bends toward its palm.</param>
    private readonly record struct Step(float At, Vector3 HandL, Vector3 ElbowL, HandShape ShapeL, Vector3 HandR, Vector3 ElbowR, HandShape ShapeR,
        float Hip = 0, float Tip = 0, float Twist = 0, float SplayL = 0, float SplayR = 0, float KneeL = 0, float KneeR = 0, float Kick = 0, float Lean = 0,
        float Nod = 0, float Turn = 0, float Tilt = 0, float Toes = 0, float WristL = 0, float WristR = 0) {
        /// <summary>The step part of the way to <paramref name="to"/>; a hand takes its new shape half way.</summary>
        public Step Toward(Step to, float blend) => new(Mathf.Lerp(At, to.At, blend),
            HandL.Lerp(to.HandL, blend), ElbowL.Lerp(to.ElbowL, blend), blend < .5f ? ShapeL : to.ShapeL,
            HandR.Lerp(to.HandR, blend), ElbowR.Lerp(to.ElbowR, blend), blend < .5f ? ShapeR : to.ShapeR,
            Mathf.Lerp(Hip, to.Hip, blend), Mathf.Lerp(Tip, to.Tip, blend), Mathf.Lerp(Twist, to.Twist, blend),
            Mathf.Lerp(SplayL, to.SplayL, blend), Mathf.Lerp(SplayR, to.SplayR, blend), Mathf.Lerp(KneeL, to.KneeL, blend), Mathf.Lerp(KneeR, to.KneeR, blend),
            Mathf.Lerp(Kick, to.Kick, blend), Mathf.Lerp(Lean, to.Lean, blend), Mathf.Lerp(Nod, to.Nod, blend), Mathf.Lerp(Turn, to.Turn, blend),
            Mathf.Lerp(Tilt, to.Tilt, blend), Mathf.Lerp(Toes, to.Toes, blend), Mathf.Lerp(WristL, to.WristL, blend), Mathf.Lerp(WristR, to.WristR, blend));
    }

    private static Vector3 Mirror(Vector3 left) => left with { X = -left.X };

    // Where a left hand goes, and the way its elbow swings; a right hand takes the mirror of each.
    private static readonly Vector3 Out = new(1, -.1f, 0), Down = new(.3f, -1, -.3f), High = new(1, .4f, .1f), Tucked = new(1, -.5f, 0);
    private static readonly Vector3 Flexed = new(.27f, .68f, .04f), Low = new(.4f, .1f, .05f), Overhead = new(.1f, .86f, .04f), AtEye = new(.05f, .69f, .15f),
        AtShoulder = new(.13f, .52f, .1f), Sky = new(.17f, .99f, .02f), BeforeFace = new(0, .63f, .24f), AtChest = new(.04f, .42f, .15f),
        AtMouth = new(.02f, .6f, .14f), ByCheek = new(.1f, .69f, .14f), ByShoulder = new(.2f, .62f, .1f), Across = new(-.03f, .56f, .16f), Raised = new(.21f, .76f, .06f);

    private const HandShape Open = HandShape.Open, Fist = HandShape.Fist, Point = HandShape.Point, Ring = HandShape.Ring;

    /// <summary>
    /// The solo: fists flexed by the head while the chest shimmies over a wide stance, a ring of finger and thumb at one
    /// eye and then the other with the weight on the far leg, both arms low, a finger at the sky and down before the face,
    /// a foot kicked up behind, and hands by the face.
    /// The last step is the first again, so it loops.
    /// </summary>
    private static readonly Step[] SoloSteps = {
        Shimmy(0, 14), Shimmy(.2f, -14), Shimmy(.4f, 14),
        new(.7f, Overhead, High, Open, Mirror(Low), Mirror(Down), Open, Hip: -6, Tip: 10, SplayL: 22, SplayR: 4, Tilt: -6, Toes: 8, WristR: -30),
        new(1, AtEye, High, Ring, Mirror(Low), Mirror(Down), Open, Hip: -8, Tip: 14, SplayL: 26, SplayR: 2, Turn: 8, Tilt: -10, Toes: 8, WristR: -40),
        new(1.35f, AtEye, High, Ring, Mirror(Low), Mirror(Down), Open, Hip: -8, Tip: 14, SplayL: 26, SplayR: 2, Turn: 8, Tilt: -10, Toes: 8, WristR: -40),
        new(1.6f, Flexed, Out, Fist, Mirror(Low), Mirror(Down), Open, Hip: -3, Tip: 5, SplayL: 16, SplayR: 10, KneeL: 6, KneeR: 6, Toes: 10),
        Shimmy(1.9f, 14), Shimmy(2.1f, -14), Shimmy(2.3f, 12),
        new(2.55f, Low, Down, Open, Mirror(AtShoulder), Mirror(Tucked), Fist, Hip: 6, Tip: -10, SplayL: 4, SplayR: 22, Tilt: 6, Toes: 8, WristL: -30),
        new(2.85f, Low, Down, Open, Mirror(AtEye), Mirror(High), Ring, Hip: 8, Tip: -14, SplayL: 2, SplayR: 26, Turn: -8, Tilt: 10, Toes: 8, WristL: -40),
        new(3.15f, Low, Down, Open, Mirror(AtEye), Mirror(High), Ring, Hip: 8, Tip: -14, SplayL: 2, SplayR: 26, Turn: -8, Tilt: 10, Toes: 8, WristL: -40),
        new(3.45f, Low, Down, Open, Mirror(Low), Mirror(Down), Open, SplayL: 5, SplayR: 5, KneeL: 12, KneeR: 12, Nod: 6, WristL: -50, WristR: -50),
        new(3.75f, Sky, Out, Point, Mirror(Low), Mirror(Down), Open, Hip: -5, Tip: 6, SplayL: 7, KneeL: 14, Nod: -10, Turn: 8, WristR: -40),
        new(4.1f, Sky, Out, Point, Mirror(Low), Mirror(Down), Open, Hip: -5, Tip: 6, SplayL: 7, KneeL: 14, Nod: -10, Turn: 8, WristR: -40),
        new(4.4f, BeforeFace, Tucked, Point, Mirror(Low), Mirror(Down), Open, Hip: 5, Tip: -8, SplayR: 7, KneeR: 10, Lean: 5, Tilt: 8, WristR: -30),
        new(4.9f, BeforeFace, Tucked, Point, Mirror(Low), Mirror(Down), Open, Hip: 5, Tip: -8, SplayR: 7, KneeR: 10, Lean: 5, Tilt: 8, WristR: -30),
        new(5.2f, AtChest, Tucked, Fist, Mirror(Low), Mirror(Down), Open),
        new(5.5f, AtMouth, Tucked, Fist, Mirror(Low), Mirror(Down), Open, Hip: 6, Tip: -9, Kick: 1, Tilt: -6, WristR: -40),
        new(6.1f, AtMouth, Tucked, Fist, Mirror(Low), Mirror(Down), Open, Hip: 6, Tip: -9, Kick: 1, Tilt: -6, WristR: -40),
        new(6.4f, ByCheek, Tucked, Point, Mirror(Low), Mirror(Down), Open, Twist: 10, KneeL: 22, KneeR: 22, Tilt: -8),
        new(6.7f, ByShoulder, Tucked, Fist, Mirror(Across), Mirror(Tucked), Fist, Hip: -5, Tip: 8, Twist: -10, KneeL: 10, KneeR: 10, Tilt: 6),
        new(7, Raised, Out, Open, Mirror(Low), Mirror(Down), Open, Hip: 5, Tip: -6, SplayL: 6, SplayR: 6, KneeL: 4, KneeR: 4, Tilt: -4),
        Shimmy(7.3f, 14),
    };

    /// <summary>Fists flexed by the head over a wide stance with the toes out, the chest twisted one way and the hips tilted the other.</summary>
    private static Step Shimmy(float at, float twist) =>
        new(at, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, Hip: -twist * .3f, Twist: twist, SplayL: 13, SplayR: 13, KneeL: 8, KneeR: 8, Tilt: twist * .35f, Toes: 10);

    /// <summary>Seconds the solo runs before it comes round again.</summary>
    public static float SoloSeconds => SoloSteps[^1].At;

    /// <summary>The solo of the catgirl in the middle at a moment of its loop.</summary>
    public static DanceFrame Solo(float time) {
        var step = SoloStep(time);
        float tilt = Mathf.DegToRad(step.Hip), swingR = step.KneeR / 2 - 12 * step.Kick, kneeR = step.KneeR + 105 * step.Kick;
        // Each leg reaches the floor less far the more it tips out, and the tilt lifts one hip joint and lowers the other.
        // The hips sit where the leg that reaches furthest down stands on the floor.
        float lift = ActorAnimation.LegSide * MathF.Sin(tilt);
        float standL = ActorAnimation.Reach(step.KneeL / 2, step.KneeL) * MathF.Cos(Mathf.DegToRad(step.SplayL) + tilt) - lift;
        float standR = ActorAnimation.Reach(swingR, kneeR) * MathF.Cos(Mathf.DegToRad(step.SplayR) - tilt) + lift;
        var pose = new Pose(Lean: step.Lean, SwingL: step.KneeL / 2, KneeL: step.KneeL, SwingR: swingR, KneeR: kneeR, TurnL: step.Toes, TurnR: step.Toes,
            Hips: Math.Max(standL, standR) + ActorAnimation.Sole);
        pose = IntroPoses.Folding(pose, true, step.HandL, step.ElbowL, out float rollL, ActorRig.CatgirlShoulderOut);
        pose = IntroPoses.Folding(pose, false, step.HandR, step.ElbowR, out float rollR, ActorRig.CatgirlShoulderOut);
        // Her sway to her left is where her hips have gone: out from under the chest, the way the far hip lifts.
        return new DanceFrame(pose, rollL, rollR, step.ShapeL, step.ShapeR, step.Hip * -.006f, step.Hip / -8, SoloStep(time - Lag).Hip / -8,
            new Vector3(0, step.Twist, step.Tip), step.Hip, step.SplayL, step.SplayR, new Vector3(step.Nod, step.Turn, step.Tilt), step.WristL, step.WristR);
    }

    /// <summary>The blend of the two steps the solo is between at a moment of its loop.</summary>
    private static Step SoloStep(float time) {
        float at = Mathf.PosMod(time, SoloSeconds);
        int next = Array.FindIndex(SoloSteps, key => key.At > at);
        Step from = SoloSteps[next - 1], to = SoloSteps[next];
        return from.Toward(to, Mathf.SmoothStep(from.At, to.At, at));
    }
}
