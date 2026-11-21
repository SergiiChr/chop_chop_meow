using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Steel pipe tiles in their own layer under the ground, buildings and paths.</summary>
public sealed class SteelPipes(World world) {
    public const double Capacity = 1;
    /// <summary>One step to a neighboring tile, clockwise from east in ground coordinates.</summary>
    private static readonly Vector2I[] Directions = [
        new(1, 0), new(1, 1), new(0, 1), new(-1, 1),
        new(-1, 0), new(-1, -1), new(0, -1), new(1, -1)
    ];
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

    /// <summary>Directions in which the pipe can connect, as bits clockwise from east.</summary>
    public static int PortMask(float rotation, PipeShape shape) {
        int ports = shape switch {
            PipeShape.Tee => 0b0001_1111,
            PipeShape.Cross => 0b1111_1111,
            _ => 0b1011_1011
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
        for (int i = 0; i < Directions.Length; i++) {
            if ((ports & (1 << i)) == 0 || PipeAt(tile + Directions[i]) is not PipeTile other) continue;
            if ((PortMask(other.Rotation, other.Shape) & (1 << ((i + 4) % 8))) != 0) links |= 1 << i;
        }
        return links;
    }

    internal static Vector2I Offset(int direction) => Directions[direction];

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
