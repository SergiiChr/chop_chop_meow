using Godot;

namespace ChopChopMeow;

/// <summary>A small cave at the foot of the mountains, <see cref="Face"/> pointing out into the valley; its wolves come out at dusk and go back in before dawn.</summary>
public readonly record struct Den(Vector2 Mouth, Vector2 Face) {
    /// <summary>The den is a mine outcrop at this size.</summary>
    public const float Scale = .5f;
    /// <summary>Open ground in front of the mouth, where the wolves come out and go back in.</summary>
    public Vector2 Door => Mouth + Face * 1.5f;
    /// <summary>Where a mine's cart would stand: a mine's tunnel opens 2.2 m in from it, so at half size the den's opens at <see cref="Mouth"/>.</summary>
    public Vector2 Spot => Mouth + Face * 1.1f;
    /// <summary>How far from the door nothing may be built, so the wolves always have a way out.</summary>
    public const float Clearance = 4;
}
