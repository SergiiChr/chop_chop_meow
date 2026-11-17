using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The performance run's sprint: its stops, the movement keys toward a stop, and the path those keys walk.</summary>
internal static class PerformanceRoute {
    /// <summary>Where both sprints start: on the beach below the fixture's camp.</summary>
    public static readonly Vector2 Start = Terrain.Point(Terrain.Coast(Terrain.Across(Terrain.Spawn)) - 2, Terrain.Across(Terrain.Spawn));
    /// <summary>The pond's shore facing the camp, where the sprint touches the pond.</summary>
    private static readonly Vector2 Pond = Terrain.WaterPoint(Terrain.Spawn);
    /// <summary>Units from the pond's middle, along or across the valley, that the sprint keeps clear of it by.</summary>
    private const float PondClearance = Terrain.LakeRadius * 1.2f;
    /// <summary>Across the valley, the line clear of the pond on its left that the sprint follows up past it.</summary>
    private static readonly float BesidePond = Terrain.Across(Terrain.Lake) - PondClearance;
    /// <summary>Where the sprint stops in the camp: between the two furnaces seaward of the fixture's middle.</summary>
    public static readonly Vector2 Camp = Terrain.Spawn + Terrain.Point(2, .35f);
    /// <summary>The valley's top corner, where the sprint ends.</summary>
    public static readonly Vector2 Corner = Terrain.Point(22, 0);
    /// <summary>
    /// The sprint's legs, each through its stops: up into the camp, across to the pond's shore, then round the pond's
    /// left side, up past it and on to the valley's top corner.
    /// Out of the camp the sprint first steps three units across toward the pond, so its diagonal clears the bench.
    /// </summary>
    public static readonly (string Name, Vector2[] Stops)[] Legs = {
        ("ocean-camp", [Camp]),
        ("camp-pond", [Camp + Terrain.Point(0, 3), Pond]),
        ("pond-corner", [Terrain.Point(Terrain.Along(Pond), BesidePond), Terrain.Point(Terrain.Along(Terrain.Lake) - PondClearance, BesidePond), Corner]),
    };
    /// <summary>Units along and across the valley within which a stop counts as reached.</summary>
    private const float AxisReach = .15f;

    /// <summary>
    /// The movement keys a player holds toward <paramref name="to"/>, as 1, 0 or -1 each: S or W up or down the valley
    /// until level with it, and D or A across until in line, both at once on a diagonal.
    /// </summary>
    public static (int Along, int Across) Heading(Vector2 from, Vector2 to) {
        var offset = to - from;
        int along = Math.Abs(Terrain.Along(offset)) > AxisReach ? Math.Sign(Terrain.Along(offset)) : 0;
        int across = Math.Abs(Terrain.Across(offset)) > AxisReach ? Math.Sign(Terrain.Across(offset)) : 0;
        return (along, across);
    }

    /// <summary>Every point the sprint passes, one tenth of a unit apart, with the leg each belongs to.</summary>
    public static IEnumerable<(Vector2 At, int Leg)> Path() {
        var at = Start;
        for (int leg = 0; leg < Legs.Length; leg++)
            foreach (var stop in Legs[leg].Stops)
                for (var heading = Heading(at, stop); heading != (0, 0); heading = Heading(at, stop)) {
                    at += PlayerControl.KeysDirection(heading.Across, heading.Along) * .1f;
                    yield return (at, leg);
                }
    }
}
