using Godot;
using static ChopChopMeow.ActorShape;

namespace ChopChopMeow;

/// <summary>
/// A person's shape: the rings the body is drawn from, how far out the shoulders sit, and the neck's and nose's size.
/// <see cref="HumanShape"/> holds the game's build and the slim one of the intro film's catgirls.
/// </summary>
/// <param name="Neck">How thick the neck is, as a share of the game's.</param>
/// <param name="Nose">The nose's radius, in metres.</param>
internal sealed record BodyBuild(Ring[] Hips, Ring[] Chest, Ring[] Leg, Ring[] LeftArm, Ring[] RightArm, float ShoulderOut, float Neck, float Nose,
    Ring[]? Head = null) {
    public float Shin { get; init; } = ActorAnimation.Shin;
    public float LegSide { get; init; } = ActorAnimation.LegSide;
    public float ShoulderUp { get; init; } = ActorRig.ShoulderUp;
    /// <summary>How far ahead of the hips the shoulders sit; behind them when below zero.</summary>
    public float ShoulderDepth { get; init; }
    public float UpperArm { get; init; } = ActorRig.UpperArm;
    public float Forearm { get; init; } = ActorRig.Forearm;
    /// <summary>How high above the hips the chest turns on the belly.</summary>
    public float Waist { get; init; } = .21f;
    public Vector3 HeadOffset { get; init; } = new(0, .56f, 0);
    public Vector3 HeadScale { get; init; } = Vector3.One;
    /// <summary>The right leg's rings where they differ from the left one's.</summary>
    public Ring[]? RightLeg { get; init; }
    /// <summary>The trunk that joins the limbs into one surface; the game's build has none and draws its parts apart.</summary>
    public JoinedTrunk? Trunk { get; init; }

    /// <summary>Where a thigh joins the pelvis.</summary>
    public Vector3 Hip(bool left) => new(left ? LegSide : -LegSide, -.02f, 0);

    /// <summary>Where a shoulder sits on the chest, from the hips.</summary>
    public Vector3 Shoulder(bool left) => new(left ? ShoulderOut : -ShoulderOut, ShoulderUp, ShoulderDepth);
}
