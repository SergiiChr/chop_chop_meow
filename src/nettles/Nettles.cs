using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Nettles in <paramref name="world"/>: a few more come up each nightfall where no player sees, until the valley holds its most.
/// One is picked whole and never comes up again where it stood.
/// </summary>
public sealed class Nettles(World world) {
    /// <summary>Nettles that come up at each nightfall, and the most the valley holds.</summary>
    public const int PerNight = 3, Most = 10;
    /// <summary>Metres a nettle keeps from anything else on the ground.</summary>
    private const float Room = 2;
    /// <summary>Random spots tried for one nettle before the night goes without it.</summary>
    private const int Tries = 400;
    // Seeded, so a new valley's nettles come up in the same places every time.
    private readonly Random random = new(6131);

    /// <summary>Brings up the night's nettles once night has fallen.</summary>
    internal void Tick() {
        var s = world.S;
        if (!world.Night || s.NettleNight >= world.Day) return;
        s.NettleNight = world.Day;
        for (int grown = 0; grown < PerNight && s.Things.Of(ThingKind.Nettle).Count < Most; grown++)
            if (Spot() is Vector2 spot) world.Add(ThingKind.Nettle, spot, 1);
    }

    /// <summary>A random spot of open ground no player sees with nothing else by it, or null when the tries find none.</summary>
    private Vector2? Spot() {
        for (int i = 0; i < Tries; i++) {
            var p = new Vector2(random.Next(3, Terrain.Size - 3), random.Next(3, Terrain.Size - 3));
            if (world.Unseen(p) && world.Bare(p, Room)) return p;
        }
        return null;
    }
}
