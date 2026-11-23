using Godot;

namespace ChopChopMeow;

/// <summary>
/// What the remove selector points at: a building, a gravel path tile, a steel pipe tile, or nothing.
/// </summary>
public readonly record struct Removal {
    /// <summary>Nothing to take down.</summary>
    public static readonly Removal None = default;
    /// <summary>The building's id, or 0 when it points at none.</summary>
    public int Building { get; private init; }
    public Vector2I? Tile { get; private init; }
    public Vector2I? Pipe { get; private init; }

    public static Removal Of(Thing building) => new() { Building = building.Id };
    public static Removal Of(Vector2I tile) => new() { Tile = tile };
    public static Removal OfPipe(Vector2I tile) => new() { Pipe = tile };
}
