using Godot;

namespace ChopChopMeow;

/// <summary>A catgirl at one moment of a dance, with what a <see cref="ChopChopMeow.Pose"/> cannot hold.</summary>
/// <param name="RollL">
/// The turn of the left upper arm about its own length (<see cref="ActorRig.RollArm"/>), in degrees.
/// </param>
/// <param name="Shift">How far she has stepped to her left, in metres.</param>
/// <param name="Swing">How far she sways to her left: 1 is a full sway, and a hard pop of the hips goes further.</param>
/// <param name="Trail">The same a moment ago, for the tail's tip and tied hair that follow.</param>
/// <param name="Chest">
/// The chest on the belly: a lean forward, a twist toward her left and a tip toward her right shoulder, in degrees.
/// </param>
/// <param name="Hips">
/// The hips with all they carry: a tuck forward, a turn toward her left and a tilt lifting the left hip, in degrees.
/// </param>
/// <param name="Head">
/// The head on top of the pose: a nod down, a turn toward her left and a tilt toward her right shoulder, in degrees.
/// </param>
/// <param name="WristL">
/// The left hand at the wrist: a bend toward its palm, a twist of the forearm and a tip toward the thumb, in degrees.
/// </param>
/// <param name="ShoulderL">How far the left shoulder is shrugged up and hunched forward, in metres.</param>
/// <param name="Facing">Degrees her whole body is turned toward her left.</param>
/// <param name="Depth">How far she has stepped toward the camera, in metres.</param>
internal readonly record struct DanceFrame(Pose Pose, float RollL, float RollR, HandShape HandL, HandShape HandR, float Shift, float Swing, float Trail,
    Vector3 Chest = default, Vector3 Hips = default, Vector3 Head = default, Vector3 WristL = default, Vector3 WristR = default,
    Vector2 ShoulderL = default, Vector2 ShoulderR = default, float Facing = 0, float Depth = 0);
