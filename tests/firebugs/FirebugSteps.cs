using System.Collections.Generic;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Firebugs in checks: a night with one firebug, and tiring it out.</summary>
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
    /// The player stands as far from a firebug as the hoop passes, 1.15 m, with the firebug straight to their left, and
    /// swings the net ahead, then waits until the swing has landed.
    /// </summary>
    public static void SwingWithFirebugOnTheLeft(World world, Thing bug) {
        world.Player = world.Firebugs.At(bug) + new Vector2(-1.15f, 0);
        var aim = new Vector2(0, 1);
        world.SwingNet(BodySide.Out(aim, BodySide.Left).X > 0 ? aim : -aim);
        SafeTick(world, ActorAnimation.Contact(ActorAction.NetSwing) + 2 * Frame, Frame);
    }

    /// <summary>The player stands where the hoop passes, 1.15 m from a firebug, and swings the net at it, then waits until the swing has landed.</summary>
    public static void SwingAt(World world, Thing bug, float from = 1.15f) {
        world.Player = world.Firebugs.At(bug) + new Vector2(-from, 0);
        world.SwingNet(world.Firebugs.At(bug) - world.Player);
        SafeTick(world, ActorAnimation.Contact(ActorAction.NetSwing) + 2 * Frame, Frame);
    }
}
