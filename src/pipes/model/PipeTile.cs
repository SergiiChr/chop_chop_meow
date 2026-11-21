namespace ChopChopMeow;

/// <summary>A laid pipe's shape and its clockwise rotation from east, in radians.</summary>
public readonly record struct PipeTile(float Rotation, PipeShape Shape);
