using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Steel pipe tiles in their own layer under the ground, buildings and paths.</summary>
public sealed class SteelPipes(World world) {
    public const double Capacity = 1;
    private const int East = 1 << 0, South = 1 << 2, West = 1 << 4, North = 1 << 6;
    private static readonly (Vector2I Offset, int Port, int Opposite)[] SideDirections = [
        (Vector2I.Right, East, West), (Vector2I.Down, South, North),
        (Vector2I.Left, West, East), (Vector2I.Up, North, South)
    ];

    /// <summary>Adjacent tiles and their reciprocal ports, clockwise from east.</summary>
    internal static ReadOnlySpan<(Vector2I Offset, int Port, int Opposite)> Sides => SideDirections;
    private Dictionary<int, PipeTile>? seen;
    private int revision;

    /// <summary>A number that changes when pipes are laid or removed or the pipe layer is loaded.</summary>
    public int Revision {
        get {
            var pipes = world.S.SteelPipes;
            if (!ReferenceEquals(seen, pipes)) { seen = pipes; revision++; }
            return revision;
        }
    }

    /// <summary>The map indices of all tiles with steel pipes.</summary>
    public Dictionary<int, PipeTile>.KeyCollection Tiles => world.S.SteelPipes.Keys;

    /// <summary>Whether a pipe is already laid on this tile.</summary>
    public bool Laid(Vector2I tile) => Terrain.OnMap(tile) && world.S.SteelPipes.ContainsKey(Terrain.TileIndex(tile));

    private PipeTile? PipeAt(Vector2I tile) =>
        Terrain.OnMap(tile) && world.S.SteelPipes.TryGetValue(Terrain.TileIndex(tile), out var pipe) ? pipe : null;

    /// <summary>The rotation of the pipe on this tile in radians from east toward south, or null if there is none.</summary>
    public float? RotationAt(Vector2I tile) => PipeAt(tile)?.Rotation;

    /// <summary>The shape of the pipe on this tile, or null if there is none.</summary>
    public PipeShape? ShapeAt(Vector2I tile) => PipeAt(tile)?.Shape;

    /// <summary>Open pipe ends on the east, south, west and north, at bits 0, 2, 4 and 6.</summary>
    public static int PortMask(float rotation, PipeShape shape) {
        int ports = shape switch {
            PipeShape.Tee => East | South | West,
            PipeShape.Cross => East | South | West | North,
            _ => East | West
        };
        int shift = 2 * Mathf.RoundToInt(rotation / (Mathf.Pi / 2));
        return ((ports << shift) | (ports >> (8 - shift))) & 0xff;
    }

    /// <summary>Directions shared by this laid pipe and the pipes next to it, as bits clockwise from east.</summary>
    public int ConnectionMask(Vector2I tile) =>
        PipeAt(tile) is PipeTile pipe ? PreviewMask(tile, pipe.Rotation, pipe.Shape) : 0;

    /// <summary>Directions that laying this pipe on the tile would join to its neighbors, as bits clockwise from east.</summary>
    public int PreviewMask(Vector2I tile, float rotation, PipeShape shape) {
        if (!Terrain.OnMap(tile)) return 0;
        int ports = PortMask(rotation, shape), links = 0;
        foreach (var (offset, port, opposite) in Sides) {
            if ((ports & port) == 0 || PipeAt(tile + offset) is not PipeTile other) continue;
            if ((PortMask(other.Rotation, other.Shape) & opposite) != 0) links |= port;
        }
        return links;
    }

    /// <summary>Lays a pipe under a tile that construction has accepted.</summary>
    internal void Lay(Vector2I tile, float rotation, PipeShape shape) {
        world.S.SteelPipes.Add(Terrain.TileIndex(tile), new(rotation, shape));
        revision++;
    }

    /// <summary>Removes a pipe from an occupied tile that construction has accepted.</summary>
    internal void Remove(Vector2I tile) {
        int index = Terrain.TileIndex(tile);
        if (!world.S.SteelPipes.Remove(index)) return;
        world.S.PipeWater.Remove(index);
        revision++;
    }

    /// <summary>Liters standing in the pipe at this map index, or zero when it is empty.</summary>
    internal double WaterAt(int index) => world.S.PipeWater.GetValueOrDefault(index);

    internal void StoreWater(int index, double liters) {
        if (liters > 0) world.S.PipeWater[index] = liters;
        else world.S.PipeWater.Remove(index);
    }

    /// <summary>Whether stored water is in laid pipes within their capacity.</summary>
    internal static bool ValidWater(Dictionary<int, double>? water, Dictionary<int, PipeTile> layout) {
        if (water == null) return false;
        foreach (var (index, liters) in water)
            if (!layout.ContainsKey(index) || !(liters > 0 && liters <= Capacity)) return false;
        return true;
    }

    /// <summary>Whether every saved or synchronized pipe names a map tile, a valid quarter-turn, and a known shape.</summary>
    internal static bool ValidLayout(Dictionary<int, PipeTile>? pipes) {
        if (pipes == null) return false;
        foreach (var (tile, pipe) in pipes)
            if ((uint)tile >= Terrain.Size * Terrain.Size || !Construction.IsQuarterTurn(pipe.Rotation) || !Enum.IsDefined(pipe.Shape)) return false;
        return true;
    }
}
