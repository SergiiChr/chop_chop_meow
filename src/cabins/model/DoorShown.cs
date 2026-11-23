using Godot;

namespace ChopChopMeow;

/// <summary>
/// A player at a cabin's door as their doorway stage shows them: where they stand, what they face, what they do, the
/// door point whose steps they stand on, whether they move backward, how far their shoulders turn and how far their right
/// hand has gone to the door frame and their arms drawn in to pass someone, each 0 to 1.
/// </summary>
public readonly record struct DoorShown(Vector2 At, Vector2 Facing, ActorAction Action, Vector2 Door, bool Backing, float Twist, float Grip, float Tuck, float Rotation = 0);
