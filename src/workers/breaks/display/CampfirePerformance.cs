using System;
using Godot;
using static ChopChopMeow.CampfireInstrumentShape;

namespace ChopChopMeow;

/// <summary>Props in the rig's own frame and where each hand meets them as the beat passes.</summary>
internal static class CampfirePerformance {
    internal readonly record struct Hold(Transform3D Prop, Vector3 Left, Vector3 Right, bool RightPad = false);

    public static Hold Guitar(Vector3 hips, bool perched, float playing, float beat) {
        float lean = perched ? -23 : -12;
        var turn = new Basis(Vector3.Back, Mathf.DegToRad(23)) * new Basis(Vector3.Right, Mathf.DegToRad(lean));
        // On the rock the knee is higher and farther in than when the legs cross on the ground.
        var lap = perched ? new Vector3(-.095f, .281f, .22f) : new Vector3(-.135f, .221f, .22f);
        var guitar = new Transform3D(turn, hips + lap);
        // Only the middle of the downstroke brushes the strings. At either end the hand curves clear of the bouts.
        const float downstroke = .15f;
        float across = beat < downstroke || beat > 1 - downstroke
            ? 1 - 2 * Mathf.SmoothStep(-downstroke, downstroke, beat < downstroke ? beat : beat - 1)
            : 2 * Mathf.SmoothStep(downstroke, 1 - downstroke, beat) - 1;
        float away = Mathf.SmoothStep(downstroke, .48f, beat) * (1 - Mathf.SmoothStep(.57f, 1 - downstroke, beat));
        float clearance = perched ? .034f : .027f;
        Vector3 strum = new(GuitarSoundHole, across * .104f, GuitarStrings + clearance + .065f * across * across + .07f * away);
        Vector3 rest = new(-.051f, .042f, GuitarStrings + .037f);
        return new Hold(guitar, guitar * GuitarNeckGrip, guitar * rest.Lerp(strum, playing), RightPad: true);
    }

    public static Hold Harmonica(Vector3 hips, Transform3D head, float playing, float beat) {
        // The narrow edge with the holes stays against the lips as the instrument slides between notes.
        var mouth = head * new Vector3(.018f * MathF.Sin(beat * Mathf.Tau), .074f, .106f - HarmonicaMouthpiece);
        var rest = hips + new Vector3(0, .126f, .27f);
        var handArc = Vector3.Back * (.035f * MathF.Sin(playing * Mathf.Pi));
        var basis = new Basis(Vector3.Right, Mathf.DegToRad(-28)).Slerp(head.Basis.Orthonormalized(), playing);
        var harmonica = new Transform3D(basis, rest.Lerp(mouth, playing) + handArc);
        return new Hold(harmonica, harmonica * new Vector3(HarmonicaGrip, -.012f, .02f),
            harmonica * new Vector3(-HarmonicaGrip, -.012f, .02f));
    }

    public static Hold Drum(Vector3 hips, bool perched, float playing, float beat) {
        // The base meets the thighs when perched or the crossed shins on the ground.
        var lap = perched ? new Vector3(0, .213f, .276f) : new Vector3(0, .09f, .237f);
        var drum = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(10)), hips + lap);
        const float high = .48f;
        float travel = beat < high ? 1 - beat / high : (beat - high) / (1 - high);
        float lift = .155f * (1 - travel * travel);
        // One hand steadies the rim. The other rebounds from the skin and accelerates into the next hit.
        var rim = new Vector3(-.107f, DrumHead + .008f, .042f);
        var strike = new Vector3(-.05f, DrumHead + .008f + lift, .039f - lift * .24f);
        return new Hold(drum, drum * new Vector3(DrumRadius - .005f, DrumHead - .022f, 0),
            drum * rim.Lerp(strike, playing), RightPad: true);
    }

    /// <summary>How far the face dips on the beat and rises again.</summary>
    public static float Nod(float beat) => 11 * (.5f + .5f * MathF.Cos(beat * Mathf.Tau));
}
