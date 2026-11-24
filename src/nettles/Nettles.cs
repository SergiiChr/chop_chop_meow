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
    private const int Seed = 6131;
    // Seeded, and started again with each world, so a new valley's nettles come up in the same places every time.
    private Random random = new(Seed);

    internal void Reset() => random = new Random(Seed);

    /// <summary>Brings up the night's nettles once night has fallen.</summary>
    internal void Tick() {
        var s = world.S;
        if (!world.Night) { s.NettlesUp = false; return; }
        if (s.NettlesUp || world.QuietNights) return;
        s.NettlesUp = true;
        for (int grown = 0; grown < PerNight && s.Things.Of(ThingKind.Nettle).Count < Most; grown++)
            if (world.UnseenSpot(random, Bare) is Vector2 spot) world.Add(ThingKind.Nettle, spot, 1);
    }

    /// <summary>
    /// Whether a spot is ground to stand on with nothing on it or within <see cref="Room"/> of it:
    /// no thing, yard, gravel path, beach, lake shore or den. A firebug in the air is not in the way.
    /// </summary>
    private bool Bare(Vector2 p) {
        if (!world.CanStand(p) || Terrain.Beach(p) || Terrain.InLake(p, Room) || WolfPatrols.NearDen(p, Den.Clearance) || world.GravelPaths.Covers(p)) return false;
        foreach (var t in world.S.Things) if (!t.Roams && World.Within(t, p, Room)) return false;
        return true;
    }
}
