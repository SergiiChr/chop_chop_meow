using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Gravel path tiles laid on the ground of <paramref name="world"/>, one map tile each: where one may go, laying and
/// taking one up, and how much faster walkers move on gravel.
/// A tile is no thing: buildings and yards go over tiles and tiles under them, neither minding the other.
/// </summary>
public sealed class GravelPaths(World world) {
    /// <summary>One flag a map tile, set where a path tile is laid; rebuilt from the save's list whenever that changes.</summary>
    private readonly bool[] laid = new bool[Terrain.Size * Terrain.Size];
    /// <summary>The save's list the flags were last built from, and how many tiles it held then.</summary>
    private List<int>? seen;
    private int seenCount;
    private int revision;

    /// <summary>A number that changes whenever a tile is laid or taken up, so a view redraws only then.</summary>
    public int Revision { get { Sync(); return revision; } }

    /// <summary>The tile a point lies on: tiles are centred on whole coordinates, where buildings stand.</summary>
    public static Vector2I TileAt(Vector2 p) => new((int)MathF.Round(p.X), (int)MathF.Round(p.Y));

    /// <summary>Whether a path tile is laid on a tile; none is off the map.</summary>
    public bool Laid(int x, int y) {
        Sync();
        return (uint)x < Terrain.Size && (uint)y < Terrain.Size && laid[y * Terrain.Size + x];
    }

    public bool Laid(Vector2I tile) => Laid(tile.X, tile.Y);

    /// <summary>A tile's place in the map's rows, which the save lists it by.</summary>
    private static int Index(Vector2I tile) => tile.Y * Terrain.Size + tile.X;

    /// <summary>Whether a point is on drawn gravel, which is where walkers are faster and footsteps crunch.</summary>
    public bool Covers(Vector2 p) => GravelOutline.Distance(this, p) <= 0;

    /// <summary>
    /// The laid tile whose gravel a point is on, or null off gravel: the tile the point lies on, or, on a strip between
    /// tiles, the nearest of them.
    /// </summary>
    public Vector2I? TileUnder(Vector2 p) {
        if (!Covers(p)) return null;
        var at = TileAt(p);
        if (Laid(at)) return at;
        Vector2I? nearest = null;
        for (int y = at.Y - 1; y <= at.Y + 1; y++)
            for (int x = at.X - 1; x <= at.X + 1; x++)
                if (Laid(x, y) && (nearest is not Vector2I best || p.DistanceSquaredTo(new Vector2(x, y)) < p.DistanceSquaredTo(best))) nearest = new Vector2I(x, y);
        return nearest;
    }

    /// <summary>How many times faster a walker at a point moves: <see cref="GameData.GravelSpeed"/> on gravel, 1 off it.</summary>
    public float SpeedAt(Vector2 p) => Covers(p) ? world.Data.GravelSpeed : 1;

    /// <summary>
    /// Why the acting player cannot lay a path tile at a point, or null: it is out of their reach, on water or cliffs,
    /// laid already, under a standing resource, or they lack the gravel.
    /// Buildings and yards refuse nothing.
    /// </summary>
    public string? Block(Vector2 p) {
        var tile = TileAt(p);
        if (world.Construction.TooFar(tile) is string far) return far;
        if (!World.Open(tile) || Terrain.InLake(tile, .8f)) return "Choose dry, open ground.";
        if (Laid(tile)) return "Gravel is already laid here.";
        foreach (var t in world.S.Things)
            if (t.IsResource && !t.IsLoose && !World.BuiltOver(t.Kind) && t.Kind != ThingKind.Water && t.Remaining > 0 && TileAt(t.Pos) == tile) return "A resource is in the way.";
        return world.S.Backpack.Has(world.Data.Buildings[ThingKind.GravelPath].Cost) ? null : "Missing materials in your backpack.";
    }

    /// <summary>Lays a tile that <see cref="Block"/> allows, clearing a stump or plain bush off it; <see cref="Construction.Build"/> takes its cost.</summary>
    internal void Lay(Vector2I tile) {
        Sync();
        world.ClearTile(tile);
        world.S.GravelPaths.Add(Index(tile));
        laid[Index(tile)] = true;
        Changed();
    }

    /// <summary>Why the acting player cannot take up a tile, or null: none is laid there, or it is out of reach.</summary>
    public string? TakeUpBlock(Vector2I tile) {
        if (!Laid(tile)) return "No gravel path here.";
        return world.Reaches(tile) ? null : "Walk closer first.";
    }

    /// <summary>
    /// Takes up a laid tile in reach: its gravel goes to the backpack, and what does not fit waits in a recovery box on it.
    /// </summary>
    public bool TakeUp(Vector2I tile) {
        if (TakeUpBlock(tile) is string refused) { world.Say(refused); return false; }
        if (world.Busy() || world.Forwarded(Verb.TakeUpPath, at: tile)) return true;
        world.S.GravelPaths.Remove(Index(tile));
        laid[Index(tile)] = false;
        Changed();
        world.Play("take_down_building", tile);
        if (!world.Construction.GiveBack(world.Data.Buildings[ThingKind.GravelPath].Cost, tile)) world.Say("The gravel did not fit in your backpack. It is in a box where the path lay.");
        return true;
    }

    private void Changed() { seenCount = world.S.GravelPaths.Count; revision++; }

    /// <summary>
    /// Brings the flags in step with the save's list: a new game, a load or a guest's update puts another list in its place.
    /// It allocates nothing, and the revision moves only when the tiles differ.
    /// </summary>
    private void Sync() {
        var tiles = world.S.GravelPaths;
        if (ReferenceEquals(tiles, seen) && tiles.Count == seenCount) return;
        bool same = tiles.Count == seenCount;
        if (same) foreach (int tile in tiles) if (!laid[tile]) { same = false; break; }
        seen = tiles; seenCount = tiles.Count;
        if (same) return;
        Array.Clear(laid);
        foreach (int tile in tiles) laid[tile] = true;
        revision++;
    }
}
