using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The beach squares a shovel has stripped and the tide that puts their sand back.</summary>
public sealed class Beach(World world) {
    public const string Sand = "sand";
    public const int SandPerSquare = 10;
    internal const float CrestMargin = .65f;
    private readonly List<int> flooded = [];

    internal float Tide => world.Moonlight;

    /// <summary>Distance the coast's waterline has moved inland at this point across the valley.</summary>
    private static float Shift(float across, float tide) =>
        Mathf.Lerp(Terrain.LowTideShift, Terrain.SandWidth(across) + CrestMargin, tide);

    internal static bool IsSand(Vector2 at) => Terrain.Beach(at) && !Terrain.Sea(at);

    private static bool Covered(Vector2 at, float tide) {
        float across = Terrain.Across(at);
        return Terrain.Along(at) - Terrain.Waterline(across) + Shift(across, tide) >= Terrain.SeaWander(at);
    }

    private static bool SquareIs(Vector2 at, float tide, bool wet) {
        for (float y = -.5f; y <= .5f; y++)
            for (float x = -.5f; x <= .5f; x++)
                if (Covered(at + new Vector2(x, y), tide) != wet) return false;
        return Covered(at, tide) == wet;
    }

    public bool Stripped(Vector2I tile) => Terrain.OnMap(tile) && world.S.StrippedSand.Contains(Terrain.TileIndex(tile));
    internal HashSet<int> StrippedSquares => world.S.StrippedSand;

    internal string? DigBlock(Vector2 at) {
        if (!SquareIs(at, Tide, wet: false)) return "Wait for the tide to uncover this sand.";
        if (Stripped(Terrain.TileAt(at))) return "The tide has not refilled this square yet.";
        return world.Backpack.Room(Sand) < SandPerSquare ? "Make room for 10 sand in your backpack." : null;
    }

    internal void Strip(Vector2 at) {
        world.S.StrippedSand.Add(Terrain.TileIndex(Terrain.TileAt(at)));
        world.S.Backpack.Add(Sand, SandPerSquare);
        world.Got(Sand);
    }

    internal void Tick(float before) {
        var squares = world.S.StrippedSand;
        if (squares.Count == 0) return;
        // A sleep or a large tick can pass high tide without ending at it.
        float cycle = world.Data.DaySeconds * world.Data.MoonCycleDays;
        float tide = MathF.Floor(before / cycle) < MathF.Floor(world.Time / cycle) ? 1 : Tide;
        foreach (int index in squares)
            if (SquareIs(Position(index), tide, wet: true)) flooded.Add(index);
        foreach (int index in flooded) squares.Remove(index);
        flooded.Clear();
    }

    internal static Vector2 Position(int index) => new(index % Terrain.Size, index / Terrain.Size);

    internal static bool Valid(HashSet<int>? squares) {
        if (squares == null) return false;
        foreach (int index in squares)
            if ((uint)index >= Terrain.Size * Terrain.Size || !IsSand(Position(index)) || !World.Open(Position(index))) return false;
        return true;
    }
}
