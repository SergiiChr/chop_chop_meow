using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// An impassable rock face whose ore deposits sit at its foot, with loose pieces scattered toward the clearing, and
/// the one spot where a mine can be marked at it.
/// </summary>
public readonly record struct Cliff(ThingKind Ore, Vector2 Center, Vector2 Face, float HalfLength) {
    public const float Depth = 1.4f;
    /// <summary>How far out from its cliff's spine a mine's spot stands.</summary>
    public const float MineOut = 4.2f;

    // Rock faces at the foot of both ranges, facing into the valley; the first iron and coal cliffs are the nearer ones, with loose coal below the coal cliff.
    // Iron and tin have no loose pieces: they wait for a pickaxe.
    public static readonly Cliff[] All = { FootCliff(ThingKind.Iron, 172, 1), FootCliff(ThingKind.Coal, 140, -1), FootCliff(ThingKind.Tin, 108, 1), FootCliff(ThingKind.Iron, 80, -1), FootCliff(ThingKind.Coal, 62, 1) };
    /// <summary>Ores found in the valley's cliffs; the end goal wants a working mine on each.</summary>
    public static readonly ThingKind[] Ores = All.Select(c => c.Ore).Distinct().ToArray();

    private static Cliff FootCliff(ThingKind ore, float along, int side) =>
        new(ore, Terrain.Point(along, side * (Terrain.HalfWidth(along, side) + .6f)), Terrain.Sideways * -side, 4.5f);
    /// <summary>The first cliff of an ore, the nearer one where there are two.</summary>
    public static Cliff Of(ThingKind ore) => All.First(c => c.Ore == ore);
    /// <summary>The cliff whose spine is nearest a point.</summary>
    public static Cliff Nearest(Vector2 p) {
        var nearest = All[0]; float best = nearest.DistanceTo(p);
        for (int i = 1; i < All.Length; i++) if (All[i].DistanceTo(p) is var distance && distance < best) (nearest, best) = (All[i], distance);
        return nearest;
    }

    public Vector2 Along => new(-Face.Y, Face.X);
    /// <summary>The cart's spot, outward from the cliff center, with the portal projecting from the mountain foot.</summary>
    public Vector2 MineSpot => Center + Face * MineOut;
    /// <summary>Distance to the cliff's spine, so the whole wall blocks movement rather than only its middle.</summary>
    public float DistanceTo(Vector2 p) => p.DistanceTo(Center + Along * Math.Clamp((p - Center).Dot(Along), -HalfLength, HalfLength));
}
