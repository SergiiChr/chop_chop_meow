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
    /// The sizes of the clusters of firebugs out now: firebugs that came out within two metres of another of a cluster belong to it.
    /// Fails when one is larger than three.
    /// Two clusters that come out within about four metres of each other count as one, so it is only for a few clusters at once.
    /// </summary>
    public static List<int> ClusterSizes(World world) {
        var left = world.ThingsOf(ThingKind.Firebug).ToList();
        var sizes = new List<int>();
        while (left.Count > 0) {
            var cluster = new List<Thing> { left[0] };
            left.RemoveAt(0);
            for (int i = 0; i < cluster.Count; i++) {
                cluster.AddRange(left.Where(bug => bug.Pos.DistanceTo(cluster[i].Pos) <= 2));
                left.RemoveAll(cluster.Contains);
            }
            Require(cluster.Count <= 3, $"a cluster should have at most 3 firebugs: {cluster.Count} came out near {cluster[0].Pos}");
            sizes.Add(cluster.Count);
        }
        return sizes;
    }

    /// <summary>
    /// Ticks across the moment a whole hour of the night comes and gives the firebugs that came out then.
    /// Fails unless they are one cluster: one to three firebugs within two metres of each other.
    /// </summary>
    public static List<Thing> HourlyCluster(World world, int hour) {
        RunUntilPhase(world, world.Data.NightStarts + (hour - .1f) / 24);
        var before = world.ThingsOf(ThingKind.Firebug).Select(bug => bug.Id).ToHashSet();
        RunUntilPhase(world, world.Data.NightStarts + (hour + .1f) / 24);
        var cluster = world.ThingsOf(ThingKind.Firebug).Where(bug => !before.Contains(bug.Id)).ToList();
        Require(cluster.Count is >= 1 and <= 3, $"one to three firebugs should come out {hour} hours into the night: {cluster.Count}");
        Require(cluster.All(bug => cluster.All(other => bug.Pos.DistanceTo(other.Pos) <= 2)),
            $"the firebugs that come out {hour} hours into the night should be within 2 m of each other: {string.Join(", ", cluster.Select(bug => bug.Pos))}");
        return cluster;
    }
}
