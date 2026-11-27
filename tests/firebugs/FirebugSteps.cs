using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Firebugs in checks: a night with one firebug, tiring it out, swinging at it and the clusters out tonight.</summary>
internal static class FirebugSteps {
    /// <summary>
    /// A night with no wolves and one firebug hovering ten metres from the player, who carries a bug net.
    /// Gives what the player is told from then on.
    /// </summary>
    public static (World World, Thing Bug, List<string> Said) FirebugNight(GameData data) {
        var world = NightWorld(data);
        RemoveThings(world, t => t.Kind == ThingKind.Firebug);
        var bug = world.Add(ThingKind.Firebug, world.Player + new Vector2(10, 0), 1);
        Give(world, "bug_net");
        return (world, bug, Told(world));
    }

    /// <summary>A frame of the game, which a firebug's dart and a swing's landing are timed to.</summary>
    public const float Frame = 1 / 60f;

    /// <summary>The night of <see cref="FirebugNight"/> with its firebug tired out: it has darted off from the player and come to rest.</summary>
    public static (World World, Thing Bug, List<string> Said) RestingFirebug(GameData data) {
        var night = FirebugNight(data);
        TireOut(night.World, night.Bug);
        return night;
    }

    /// <summary>
    /// The player steps up to three metres from a firebug, which darts off; returns on the frame it comes to rest.
    /// Fails when it does not dart or does not stop.
    /// </summary>
    public static void TireOut(World world, Thing bug) {
        var from = bug.Pos;
        world.Player = world.Firebugs.At(bug) + new Vector2(-3, 0);
        SafeTick(world, Firebugs.DartSeconds / 2, Frame);
        Require(bug.Pos.DistanceTo(from) > 2, $"a firebug should dart off from a player three metres away: it moved {bug.Pos.DistanceTo(from):0.0} m");
        for (int frames = 0; frames < 120; frames++) {
            var before = bug.Pos;
            SafeTick(world, Frame, Frame);
            if (bug.Pos == before) return;
        }
        Require(false, "a firebug should come to rest after its dart");
    }

    /// <summary>
    /// The player stands where the hoop passes, 1.15 m from a firebug, and swings the net at it, or a quarter turn to
    /// the right of it, then waits until the swing has landed.
    /// </summary>
    public static void SwingAt(World world, Thing bug, float from = 1.15f, bool aside = false) {
        world.Player = world.Firebugs.At(bug) + new Vector2(-from, 0);
        var toBug = world.Firebugs.At(bug) - world.Player;
        world.SwingNet(aside ? BodySide.Out(toBug, BodySide.Right) : toBug);
        SafeTick(world, ActorAnimation.Contact(ActorAction.NetSwing) + 2 * Frame, Frame);
    }

    /// <summary>
    /// The clusters of firebugs out now, in the order they came out, each with its first firebug first.
    /// A firebug within a metre of the cluster that came out just before it belongs to that cluster, while it has fewer than three.
    /// </summary>
    public static List<List<Thing>> Clusters(World world) {
        var clusters = new List<List<Thing>>();
        foreach (var bug in world.ThingsOf(ThingKind.Firebug).OrderBy(bug => bug.Id)) {
            if (clusters.Count > 0 && clusters[^1].Count < 3 && bug.Pos.DistanceTo(clusters[^1][0].Pos) <= 1) clusters[^1].Add(bug);
            else clusters.Add([bug]);
        }
        return clusters;
    }

    /// <summary>The sizes of <see cref="Clusters"/>, written out for a failure message.</summary>
    public static string SizesText(List<List<Thing>> clusters) => string.Join(", ", clusters.Select(cluster => cluster.Count));

    /// <summary>
    /// Runs a night of an empty world and gives the first cluster with more than one firebug that comes out on the hour:
    /// the hour, its first firebug's place and another's.
    /// Fails when no such cluster comes out.
    /// </summary>
    public static (int Hour, Vector2 First, Vector2 Other) HourlyPair(GameData data) {
        var world = EmptyWorld(data);
        for (int hour = 1; hour <= 7; hour++) {
            RunUntilPhase(world, data.NightStarts + (hour + .1f) / 24);
            var last = Clusters(world)[^1];
            if (last.Count > 1) return (hour, last[0].Pos, last[1].Pos);
        }
        Require(false, "a cluster of two or three firebugs should come out on one of the night's hours");
        return default;
    }
}
