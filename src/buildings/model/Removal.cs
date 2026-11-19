using Godot;

namespace ChopChopMeow;

/// <summary>
/// What the remove selector points at: a building, a gravel path tile, or nothing it takes down.
/// It is one of the three, never a building and a tile at once.
/// </summary>
public readonly record struct Removal {
    /// <summary>Nothing to take down.</summary>
    public static readonly Removal None = default;
    /// <summary>The building's id, or 0 when it points at none.</summary>
    public int Building { get; private init; }
    public Vector2I? Tile { get; private init; }

    public static Removal Of(Thing building) => new() { Building = building.Id };
    public static Removal Of(Vector2I tile) => new() { Tile = tile };
}
