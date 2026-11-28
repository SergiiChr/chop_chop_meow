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

    /// <summary>
    /// At the desk, the left hand on the keys and the eyes on the screen; the film puts the right hand on the mouse.
    /// </summary>
    public static readonly Pose Browsing = Seated with { Lean = 12, Head = 6, RaiseL = 46, OutL = -6, ElbowL = 56 };

    /// <summary>Sitting bolt upright with the hands in the lap, facing straight ahead.</summary>
    public static readonly Pose Staring = Seated with { Lean = 2, Head = 0 };

    /// <summary>Shrinking in the chair from someone standing over the left shoulder.</summary>
    public static readonly Pose Cowering = Seated with { Lean = -6, Sway = 10, Twist = 34, Head = -14, RaiseL = 22, OutL = 4, ElbowL = 96, RaiseR = 26, OutR = -4, ElbowR = 92 };

    /// <summary>He asks for an explanation, indicates the screen, waits, then leans in for the last words.</summary>
    public static Pose Yelling(float time) {
        var waiting = new Pose(Lean: 8, Head: 9, Twist: -7, RaiseR: 13, OutR: 7, ElbowR: 29,
            RaiseL: 16, OutL: 11, ElbowL: 41, SwingL: 14, KneeL: 12, SwingR: -12, KneeR: 6);
        var question = waiting with {
            Lean = 17,
            Sway = 4,
            Twist = 9,
            Head = 3,
            RaiseR = 54,
            OutR = 26,
            ElbowR = 66,
            ArmRollR = -42,
            RaiseL = 11,
            ElbowL = 45
        };
        var indicate = waiting with {
            Lean = 27,
            Twist = -20,
            Sway = -3,
            Head = 9,
            RaiseL = 76,
            OutL = 20,
            ElbowL = 12,
            RaiseR = 17,
            OutR = 13,
            ElbowR = 68
        };
        var insist = question with {
            Lean = 31,
            Twist = 15,
            Head = 0,
            Sway = 7,
            RaiseR = 65,
            ElbowR = 34,
            OutR = 9,
            RaiseL = 31,
            OutL = 13,
            ElbowL = 69
        };
        return Keyed(time, (0, question with { RaiseR = 23, ElbowR = 73 }), (.38f, question),
            (.83f, question with { Head = 11, Lean = 21, ElbowR = 54 }), (1.18f, waiting),
            (1.52f, indicate), (1.87f, indicate with { Lean = 29, Head = 1, ElbowL = 21 }),
            (2.28f, waiting with { Head = 1, Lean = 3 }), (2.74f, waiting with { Head = 6 }),
            (3.12f, question with { RaiseR = 28, ElbowR = 102 }), (3.58f, insist),
            (4.08f, insist with { Head = 6, ElbowR = 56 }), (4.8f, waiting), (5.5f, question), (6, waiting));
    }

    /// <summary>Thrown back on the heels with both hands up.</summary>
    public static Pose Aghast(float time) {
        float shake = MathF.Sin(time * 19);
        return new Pose(Lean: -12, Head: -6, RaiseL: 118 + shake * 3, OutL: 34, ElbowL: 92, RaiseR: 118 - shake * 3, OutR: 34, ElbowR: 92, SwingL: -8, KneeL: 8, SwingR: 12, KneeR: 14);
    }

    private static readonly Pose BatStance = new(Hips: .765f, RaiseR: 45, ElbowR: 55, OutR: 20, RaiseL: 40, ElbowL: 86);

    /// <summary>When each of the two blows lands, in seconds into the monitor shot.</summary>
    public const float FirstBlow = 19f / WorldView.IntroFps, SecondBlow = 43f / WorldView.IntroFps;

    /// <summary>Raises the bat twice, then follows each blow with a turn away from the desk.</summary>
    public static Pose Smashing(float time) {
        var ready = BatStance with { Lean = 8, Head = 5 };
        var raised = ready with { Lean = -4.31f, Twist = 7.39f, Head = -4, Hips = .752f, HipShift = -.0523f };
        var firstContact = ready with { Lean = 20, Twist = -17, Hips = .744f, HipShift = .160f };
        var secondContact = ready with { Lean = 29, Twist = -9, Hips = .660f, HipShift = .176f };
        var side = ready with { Lean = 25, Twist = 25, Head = 4, Hips = .68f, HipShift = .113f };
        var pose = Keyed(time, (0, ready), (.18f, raised with { Lean = -10 }), (.41f, raised),
            (.45f, raised), (FirstBlow, firstContact), (.76f, side), (.89f, side with { Lean = 13 }),
            (35f / WorldView.IntroFps, raised), (SecondBlow, secondContact), (1.57f, side with { Hips = .645f }),
            (1.74f, side with { Lean = 14, Hips = .706f }), (2.04f, ready));
        return ActorAnimation.OnFeet(pose, .19f, -.28f);
    }

    /// <summary>Loads the back leg, moves the hips onto the front leg, and leans away as the barrel coasts past.</summary>
    public static Pose OfficeSwing(float time) {
        var ready = BatStance with { Lean = 4, Twist = -12, Head = 3 };
        var pose = Keyed(time, (0, ready),
            (.22f, ready with { Lean = -7, Twist = -23, Sway = -5, Head = -3, Hips = .725f, HipShift = -.071f }),
            (.36f, ready with { Lean = 0, Twist = -16, Sway = -3, Hips = .738f, HipShift = -.03f }),
            (.50f, ready with { Lean = 19, Twist = 12, Sway = 5, Hips = .731f, HipShift = .147f }),
            (.63f, ready with { Lean = 16, Twist = 35, Sway = 6, Hips = .686f, HipShift = .121f }),
            (.81f, ready with { Lean = 7, Twist = 52, Sway = 7, Head = -3, Hips = .739f, HipShift = .09f }),
            (1, ready with { Lean = 3, Twist = 50, Sway = 5, Head = 0, Hips = .763f, HipShift = .062f }));
        return ActorAnimation.OnFeet(pose, .18f, -.27f);
    }

    /// <summary>The boss recoils from the blow with his hands coming away from his face.</summary>
    public static Pose BossReeling(float recoil) => Pose.Lerp(Aghast(0),
        new Pose(Lean: -20, Twist: 14, Head: -10, Sway: -9, RaiseL: 99, OutL: 61, ElbowL: 55,
            RaiseR: 91, OutR: 34, ElbowR: 64, SwingL: -14, KneeL: 20, SwingR: 26, KneeR: 19), recoil);

    /// <summary>He curls onto his side, with one knee drawn up and his fists tucked against his face.</summary>
    public static Pose BossOnFloor(float fall) {
        var curled = new Pose(Lean: 18, Twist: -24, Head: 7, Sway: 8, Hips: .73f,
            SwingL: 108, KneeL: 116, TurnL: -12, FootL: 46,
            SwingR: 29, KneeR: 37, SplayR: 2, FootR: 13);
        (curled, float leftRoll) = ArmReach.WithHandAt(curled, true, new Vector3(.126f, .578f, .177f),
            new Vector3(.36f, -.92f, -.22f));
        (curled, float rightRoll) = ArmReach.WithHandAt(curled, false, new Vector3(-.104f, .614f, .074f),
            new Vector3(-.42f, -.8f, -.5f));
        curled = curled with { ArmRollL = leftRoll, ArmRollR = rightRoll };
        return Pose.Lerp(BossReeling(1), curled, Mathf.SmoothStep(0, .87f, fall));
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
