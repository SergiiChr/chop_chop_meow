using Godot;

namespace ChopChopMeow;

/// <summary>
/// What the remove selector points at: a building by its id, or a gravel path tile, or with neither nothing it takes down.
/// </summary>
public readonly record struct Removal(int Thing, Vector2I? Tile) {
    /// <summary>Nothing to take down.</summary>
    public static readonly Removal None = default;
}
