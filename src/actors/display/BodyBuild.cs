using Godot;
using static ChopChopMeow.ActorShape;

namespace ChopChopMeow;

/// <summary>
/// A person's shape: the rings the hips, trunk, legs and arms are drawn from, how far out the shoulders sit, how thick
/// the neck is and how big the nose.
/// <see cref="HumanShape"/> holds the game's build and the slim one of the intro film's catgirls.
/// </summary>
/// <param name="Belly">
/// The trunk below the waist, drawn apart from the chest so the chest can turn on it; none when the trunk is one piece.
/// </param>
/// <param name="Neck">How thick the neck is, as a share of the game's.</param>
/// <param name="Nose">The nose's radius, in metres.</param>
internal sealed record BodyBuild(Ring[] Hips, Ring[] Chest, Ring[]? Belly, Ring[] Leg, Ring[] LeftArm, Ring[] RightArm, float ShoulderOut, float Neck, float Nose) {
    /// <summary>
    /// Where a shoulder sits on the chest, from the hips: shrugged up and hunched forward by these many metres.
    /// </summary>
    public Vector3 Shoulder(bool left, float up = 0, float forward = 0) => new(left ? ShoulderOut : -ShoulderOut, ActorRig.ShoulderUp + up, forward);
}
