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
    public float ShinLength { get; init; } = ActorAnimation.Shin;
    public float HipSide { get; init; } = ActorAnimation.LegSide;
    public float ShoulderHeight { get; init; } = ActorRig.ShoulderUp;
    public float ShoulderDepth { get; init; }
    public float UpperArmLength { get; init; } = ActorRig.UpperArm;
    public float GripLength { get; init; } = ActorRig.Forearm;
    public float WaistHeight { get; init; } = .21f;
    public Vector3 HeadOffset { get; init; } = new(0, .56f, 0);
    public Vector3 HeadScale { get; init; } = Vector3.One;
    public Ring[]? RightLeg { get; init; }
    /// <summary>One skinned profile from collar to pelvis, with its origin above the waist.</summary>
    public Ring[]? Trunk { get; init; }

    /// <summary>Where a thigh joins the pelvis.</summary>
    public Vector3 Hip(bool left) => new(left ? HipSide : -HipSide, -.02f, 0);

    /// <summary>
    /// Where a shoulder sits on the chest, from the hips: shrugged up and hunched forward by these many metres.
    /// </summary>
    public Vector3 Shoulder(bool left, float up = 0, float forward = 0) => new(left ? ShoulderOut : -ShoulderOut, ShoulderHeight + up, ShoulderDepth + forward);
}
