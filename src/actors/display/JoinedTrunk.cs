using Godot;
using static ChopChopMeow.ActorShape;

namespace ChopChopMeow;

/// <summary>
/// The trunk of a body drawn as one surface (<see cref="JoinedHuman"/>), and where its limbs meet it.
/// Each meeting place is a distance from the joint it belongs to, so a ring moved a little keeps its part.
/// </summary>
/// <param name="Rings">
/// The trunk from the top of the neck down to the rim of the pelvis the legs hang from, in heights above the hips.
/// </param>
/// <param name="ShoulderAbove">How far above the shoulder joint the trunk's rings open for the arm.</param>
/// <param name="ShoulderBelow">How far below the shoulder joint the opening reaches.</param>
/// <param name="LimbSeam">
/// How far from the shoulder or hip a limb's own rings begin; nearer the joint, the trunk's opening reaches out to them.
/// </param>
/// <param name="Crotch">
/// The line the two leg openings share, from the front of the pelvis under to its back, from midway between the hip joints.
/// </param>
internal sealed record JoinedTrunk(Ring[] Rings, float ShoulderAbove, float ShoulderBelow, float LimbSeam, Vector3[] Crotch);
