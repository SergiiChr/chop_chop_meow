using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The intro film's own poses, for what nobody does in the game: sitting at a desk, yelling, swinging a bat, riding a train and shivering.</summary>
internal static class IntroPoses {
    /// <summary>A value eased through keys sorted by time, held before the first and after the last; <paramref name="blend"/> mixes two of them.</summary>
    public static T Keyed<T>(float time, Func<T, T, float, T> blend, ReadOnlySpan<(float At, T Value)> keys) {
        for (int i = 0; i + 1 < keys.Length; i++)
            if (time < keys[i + 1].At) return blend(keys[i].Value, keys[i + 1].Value, Mathf.SmoothStep(keys[i].At, keys[i + 1].At, time));
        return keys[^1].Value;
    }

    /// <summary>A pose eased through keys sorted by time.</summary>
    public static Pose Keyed(float time, params (float At, Pose Pose)[] keys) => Keyed<Pose>(time, Pose.Lerp, keys);

    /// <summary>Sitting upright on a seat of <see cref="IntroSets.SeatTop"/> with the feet flat and the hands in the lap.</summary>
    public static readonly Pose Seated = new(Lean: 4, Head: 4, RaiseL: 30, OutL: 2, ElbowL: 58, RaiseR: 30, OutR: 2, ElbowR: 58,
        SwingL: 88, KneeL: 86, SwingR: 88, KneeR: 86, Hips: IntroSets.SeatTop + .085f);

    /// <summary>Typing at a desk: both hands on the keyboard, fingers going, eyes on the screen.</summary>
    public static Pose Typing(float time) {
        float a = MathF.Sin(time * 17), b = MathF.Sin(time * 13 + 1.7f), nod = MathF.Sin(time * .9f);
        return Seated with { Lean = 9 + nod, Head = 2 - nod, RaiseL = 46 + a * 2.5f, OutL = -6, ElbowL = 56 - a * 4, RaiseR = 46 + b * 2.5f, OutR = -6, ElbowR = 56 - b * 4 };
    }

    /// <summary>Slumped over the desk with the cheek on the right fist, the left hand prodding one key now and then.</summary>
    public static Pose Bored(float time) {
        float breath = MathF.Sin(time * 1.3f), tap = MathF.Pow(Math.Max(0, MathF.Sin(time * 4.2f)), 6);
        return Seated with {
            Lean = 17 + breath * .8f,
            Sway = 12,
            Twist = -6,
            Head = 6,
            RaiseR = 78,
            OutR = -10,
            ElbowR = 150,
            RaiseL = 50 - tap * 5,
            OutL = -8,
            ElbowL = 50 + tap * 9
        };
    }

    /// <summary>Shrinking in the chair from someone standing over the left shoulder.</summary>
    public static Pose Cowering(float time) {
        float flinch = MathF.Sin(time * 9) * 1.2f;
        return Seated with { Lean = -6 + flinch, Sway = 10, Twist = 34, Head = -14, RaiseL = 22, OutL = 4, ElbowL = 96, RaiseR = 26, OutR = -4, ElbowR = 92 };
    }

    /// <summary>Bent over someone seated, jabbing a finger down at them while the other fist shakes.</summary>
    public static Pose Yelling(float time) {
        float jab = MathF.Sin(time * 11), shake = MathF.Sin(time * 23), rock = MathF.Sin(time * 5.5f);
        return new Pose(Lean: 24 + rock * 4, Head: -4 + jab * 4, Twist: -8 + rock * 3,
            RaiseR: 62 + jab * 9, OutR: -6, ElbowR: 18 - jab * 12,
            RaiseL: 96 + shake * 5, OutL: 28, ElbowL: 96 + shake * 7,
            SwingL: 14, KneeL: 12, SwingR: -12, KneeR: 6);
    }

    /// <summary>Thrown back on the heels with both hands up.</summary>
    public static Pose Aghast(float time) {
        float shake = MathF.Sin(time * 19);
        return new Pose(Lean: -12, Head: -6, RaiseL: 118 + shake * 3, OutL: 34, ElbowL: 92, RaiseR: 118 - shake * 3, OutR: 34, ElbowR: 92, SwingL: -8, KneeL: 8, SwingR: 12, KneeR: 14);
    }

    private static readonly Pose BatStance = new(SwingL: 18, KneeL: 18, SwingR: -12, KneeR: 12);
    private static readonly Pose BatReady = BatStance with { Lean = 6, RaiseL = 52, OutL = -24, ElbowL = 40, RaiseR = 58, OutR = -16, ElbowR = 30 };
    private static readonly Pose BatHigh = BatStance with { Lean = -12, Twist = 18, Head = -6, RaiseL = 156, OutL = -24, ElbowL = 62, RaiseR = 172, OutR = -8, ElbowR = 52 };
    private static readonly Pose BatDown = BatStance with { Lean = 30, Twist = -10, Head = 12, RaiseL = 62, OutL = -26, ElbowL = 10, RaiseR = 66, OutR = -12, ElbowR = 2, KneeL = 30, KneeR = 22 };

    /// <summary>When each of the two blows lands, in seconds into the swing.</summary>
    public const float FirstBlow = .62f, SecondBlow = 1.42f;

    /// <summary>Two overhead blows with a bat held in both hands, then standing over the wreck, breathing hard.</summary>
    public static Pose Smashing(float time) {
        float heave = MathF.Sin(time * 7);
        var spent = BatStance with { Lean = 14 + heave * 2, Head = 8, RaiseL = 14, OutL = 10, ElbowL = 14, RaiseR = 20, OutR = 12, ElbowR = 16 };
        return Keyed(time, (0, BatReady), (.42f, BatHigh), (FirstBlow, BatDown), (.82f, BatDown with { Lean = 26 }), (1.22f, BatHigh with { Twist = 24 }),
            (SecondBlow, BatDown with { Lean = 34 }), (1.62f, BatDown), (1.95f, spent));
    }

    /// <summary>Riding by the window on the left: the left elbow on the sill, the chin in that hand.</summary>
    public static Pose Riding(float time) {
        float breath = MathF.Sin(time * 1.2f);
        return Seated with { Lean = 5 + breath * .6f, Sway = -8, Twist = 10, Head = 4, RaiseL = 40, OutL = 12, ElbowL = 150, RaiseR = 24, OutR = 0, ElbowR = 62 };
    }

    /// <summary>A long breath in with the shoulders lifting, then out with the head dropping; <paramref name="progress"/> runs 0 to 1.</summary>
    public static Pose Sigh(float progress) {
        var stand = new Pose(Lean: 1, Head: 0);
        return Keyed(progress, (0, stand), (.42f, stand with { Lean = -6, Head = -8, RaiseL = 2, OutL = 13, RaiseR = 2, OutR = 13 }),
            (.8f, stand with { Lean = 7, Head = 16, RaiseL = 8, OutL = 5, ElbowL = 6, RaiseR = 8, OutR = 5, ElbowR = 6 }), (1, stand with { Lean = 4, Head = 8 }));
    }

    /// <summary>Cross-legged by a fire, hunched and shivering; the film crosses the arms over the chest. <paramref name="scared"/> pulls him in tighter.</summary>
    public static Pose Shivering(float time, float scared) {
        float shiver = MathF.Sin(time * 41) * (.5f + .5f * MathF.Sin(time * 2.3f));
        return new Pose(Lean: 16 + scared * 6 + shiver * .7f, Head: 12 - scared * 8, Twist: shiver * 1.2f,
            SwingL: 86, KneeL: 150, TurnL: 40, RollL: 90, SwingR: 82, KneeR: 150, TurnR: 40, RollR: 90, Hips: .15f);
    }

}
