using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A catgirl at one moment of a dance: her pose, the turn of each upper arm about its own length
/// (<see cref="ActorRig.RollArm"/>), her hands, how far she has stepped to her left in metres, and how far she sways
/// to her left from -1 to 1, now and a moment ago, for what trails behind her.
/// </summary>
internal readonly record struct DanceFrame(Pose Pose, float RollL, float RollR, HandShape HandL, HandShape HandR, float Shift, float Swing, float Trail);

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
        return new DanceFrame(Pose.Lerp(paws, waving, up), 0, 0, hands, hands, 0, sway, Sway(time - Lag));
    }

    /// <summary>
    /// A moment the solo passes through.
    /// Each hand has a place in the torso's space, with its elbow swung toward a direction; her left is +X.
    /// </summary>
    /// <param name="Sway">Degrees the torso tips toward her right.</param>
    /// <param name="Splay">Degrees each thigh tips out from the body, for a wide stance.</param>
    /// <param name="Knees">Degrees both knees bend.</param>
    /// <param name="Kick">How far her right foot is kicked up behind her, from 0 to 1.</param>
    private readonly record struct Step(float At, Vector3 HandL, Vector3 ElbowL, HandShape ShapeL, Vector3 HandR, Vector3 ElbowR, HandShape ShapeR,
        float Sway = 0, float Splay = 0, float Knees = 0, float Kick = 0, float Lean = 0);

    private static Vector3 Mirror(Vector3 left) => left with { X = -left.X };

    // Where a left hand goes, and the way its elbow swings; a right hand takes the mirror of each.
    private static readonly Vector3 Out = new(1, -.1f, 0), Down = new(.3f, -1, -.3f), High = new(1, .4f, .1f), Tucked = new(1, -.5f, 0);
    private static readonly Vector3 Flexed = new(.27f, .68f, .04f), Low = new(.4f, .1f, .05f), Overhead = new(.1f, .86f, .04f), AtEye = new(.05f, .69f, .15f),
        AtShoulder = new(.13f, .52f, .1f), Sky = new(.17f, .99f, .02f), BeforeFace = new(0, .63f, .24f), AtChest = new(.04f, .42f, .15f),
        AtMouth = new(.02f, .6f, .14f), ByCheek = new(.1f, .69f, .14f), ByShoulder = new(.2f, .62f, .1f), Across = new(-.03f, .56f, .16f), Raised = new(.21f, .76f, .06f);

    private const HandShape Open = HandShape.Open, Fist = HandShape.Fist, Point = HandShape.Point, Ring = HandShape.Ring;

    /// <summary>
    /// The solo: fists flexed by the head with a shimmy, a ring of finger and thumb at one eye and then the other, both
    /// arms low, a finger at the sky and down before the face, a foot kicked up behind, and hands by the face.
    /// The last step is the first again, so it loops.
    /// </summary>
    private static readonly Step[] SoloSteps = {
        new(0, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, 5, 13, 8),
        new(.2f, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, -5, 13, 8),
        new(.4f, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, 5, 13, 8),
        new(.7f, Overhead, High, Open, Mirror(Low), Mirror(Down), Open, -8, 13, 4),
        new(1, AtEye, High, Ring, Mirror(Low), Mirror(Down), Open, -9, 13, 4),
        new(1.35f, AtEye, High, Ring, Mirror(Low), Mirror(Down), Open, -9, 13, 4),
        new(1.6f, Flexed, Out, Fist, Mirror(Low), Mirror(Down), Open, -3, 13, 6),
        new(1.9f, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, 5, 13, 8),
        new(2.1f, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, -5, 13, 8),
        new(2.3f, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, 4, 13, 8),
        new(2.55f, Low, Down, Open, Mirror(AtShoulder), Mirror(Tucked), Fist, 7, 13, 4),
        new(2.85f, Low, Down, Open, Mirror(AtEye), Mirror(High), Ring, 9, 13, 4),
        new(3.15f, Low, Down, Open, Mirror(AtEye), Mirror(High), Ring, 9, 13, 4),
        new(3.45f, Low, Down, Open, Mirror(Low), Mirror(Down), Open, 0, 5, 6),
        new(3.75f, Sky, Out, Point, Mirror(Low), Mirror(Down), Open, -2),
        new(4.1f, Sky, Out, Point, Mirror(Low), Mirror(Down), Open, -2),
        new(4.4f, BeforeFace, Tucked, Point, Mirror(Low), Mirror(Down), Open, -6, Lean: 4),
        new(4.9f, BeforeFace, Tucked, Point, Mirror(Low), Mirror(Down), Open, -3, Lean: 4),
        new(5.2f, AtChest, Tucked, Fist, Mirror(Low), Mirror(Down), Open),
        new(5.5f, AtMouth, Tucked, Fist, Mirror(Low), Mirror(Down), Open, -5, Kick: 1),
        new(6.1f, AtMouth, Tucked, Fist, Mirror(Low), Mirror(Down), Open, -5, Kick: 1),
        new(6.4f, ByCheek, Tucked, Point, Mirror(Low), Mirror(Down), Open, 5, Knees: 22),
        new(6.7f, ByShoulder, Tucked, Fist, Mirror(Across), Mirror(Tucked), Fist, -6, Knees: 10),
        new(7, Raised, Out, Open, Mirror(Low), Mirror(Down), Open, 6, 6, 4),
        new(7.3f, Flexed, Out, Fist, Mirror(Flexed), Mirror(Out), Fist, 5, 13, 8),
    };

    /// <summary>Seconds the solo runs before it comes round again.</summary>
    public static float SoloSeconds => SoloSteps[^1].At;

    /// <summary>The solo of the catgirl in the middle at a moment of its loop.</summary>
    public static DanceFrame Solo(float time) {
        var (pose, shift, sway) = SoloBody(time, out var step);
        pose = IntroPoses.Folding(pose, true, step.HandL, step.ElbowL, out float rollL, ActorRig.CatgirlShoulderOut);
        pose = IntroPoses.Folding(pose, false, step.HandR, step.ElbowR, out float rollR, ActorRig.CatgirlShoulderOut);
        return new DanceFrame(pose, rollL, rollR, step.ShapeL, step.ShapeR, shift, sway, SoloBody(time - Lag, out _).Sway);
    }

    /// <summary>Her body without the arms, with how far she steps and sways to her left, and the blend of steps the arms are solved from.</summary>
    private static (Pose Pose, float Shift, float Sway) SoloBody(float time, out Step step) {
        float at = Mathf.PosMod(time, SoloSteps[^1].At);
        int next = Array.FindIndex(SoloSteps, key => key.At > at);
        Step from = SoloSteps[next - 1], to = SoloSteps[next];
        float blend = Mathf.SmoothStep(from.At, to.At, at);
        // A hand takes its new shape half way to the next step.
        step = new Step(at, from.HandL.Lerp(to.HandL, blend), from.ElbowL.Lerp(to.ElbowL, blend), blend < .5f ? from.ShapeL : to.ShapeL,
            from.HandR.Lerp(to.HandR, blend), from.ElbowR.Lerp(to.ElbowR, blend), blend < .5f ? from.ShapeR : to.ShapeR,
            Mathf.Lerp(from.Sway, to.Sway, blend), Mathf.Lerp(from.Splay, to.Splay, blend), Mathf.Lerp(from.Knees, to.Knees, blend),
            Mathf.Lerp(from.Kick, to.Kick, blend), Mathf.Lerp(from.Lean, to.Lean, blend));
        var pose = new Pose(Lean: step.Lean, Sway: step.Sway, Head: -2, Splay: step.Splay,
            SwingL: step.Knees / 2, KneeL: step.Knees, SwingR: step.Knees / 2 - 12 * step.Kick, KneeR: step.Knees + 105 * step.Kick);
        // Splayed legs reach less far down, so the hips come down with them and the feet stay on the floor.
        pose = pose with { Hips = (pose.HipHeight - ActorAnimation.Sole) * MathF.Cos(Mathf.DegToRad(step.Splay)) + ActorAnimation.Sole };
        // The torso tips toward her right for a positive sway, and she steps a little the other way under it.
        return (pose, step.Sway * .004f, -step.Sway / 9);
    }
}
