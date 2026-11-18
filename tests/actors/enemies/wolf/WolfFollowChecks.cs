using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WolfSteps;
using static WorldSteps;

/// <summary>
/// Checks for follow mode: wolves a torch wave scared off circling the player, and what one does when the player walks at it.
/// </summary>
public static class WolfFollowChecks {
    public static void Run(GameData data) {
        Test(CheckFollowingWolvesSpreadRoundThePlayerAndKeepCircling, data);
        Test(CheckAWolfWalkedAtStandsBracedWhereItIs, data);
        Test(CheckAWolfFollowingAloneRunsOutOfSightAndLeavesThePlayerAlone, data);
        Test(CheckAmongOthersAWolfWalkedAtGrowlsThenTurnsOnThePlayerForGood, data);
    }

    private static void CheckFollowingWolvesSpreadRoundThePlayerAndKeepCircling(GameData data) {
        var world = TorchNight(data);
        // All three come from the east, within the swing that scares them.
        var wolves = new[] { 0f, 12, -12 }.Select(degrees => PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(6, 0).Rotated(Mathf.DegToRad(degrees)) })).ToArray();
        world.UseTorch(Vector2.Right);
        Require(wolves.All(w => w.Scared), "a torch waved at three wolves should put all three in follow mode");
        WolfTick(world, 14, wolves);
        float[] bearings = wolves.Select(w => Mathf.RadToDeg((w.Pos - world.Player).Angle())).OrderBy(b => b).ToArray();
        float[] gaps = { bearings[1] - bearings[0], bearings[2] - bearings[1], 360 - (bearings[2] - bearings[0]) };
        Require(gaps.All(gap => gap is > 80 and < 160), $"three following wolves should spread about a third of a circle apart, not {string.Join(", ", gaps.Select(g => $"{g:0}"))} degrees");
        Require(wolves.All(w => Math.Abs(w.Pos.DistanceTo(world.Player) - World.StalkDistance) <= WolfFollow.Wander + .2f),
            $"following wolves should keep about {World.StalkDistance} m off, not {string.Join(", ", wolves.Select(w => $"{w.Pos.DistanceTo(world.Player):0.0}"))} m");
        var before = wolves.Select(w => w.Pos).ToArray();
        WolfTick(world, 3, wolves);
        Require(wolves.Zip(before).All(pair => pair.First.Pos.DistanceTo(pair.Second) > 1.5f), "following wolves should keep circling, not hold their places");
        Require(world.Health == 100, "following wolves should not bite");
    }

    private static void CheckAWolfWalkedAtStandsBracedWhereItIs(GameData data) {
        var (world, wolf) = Followed(data, 1);
        WalkTo(world, wolf, 3);
        var stood = wolf.Pos;
        WolfTick(world, 2, wolf);
        Require(wolf.Braced && wolf.Scared && wolf.Pos == stood, "a following wolf the player has walked up to should stand braced where it is");
        world.Player += (world.Player - wolf.Pos).Normalized() * 2;
        WolfTick(world, 2, wolf);
        Require(!wolf.Braced && wolf.Pos != stood, "a braced wolf should circle again once the player has backed off");
    }

    private static void CheckAWolfFollowingAloneRunsOutOfSightAndLeavesThePlayerAlone(GameData data) {
        var (world, wolf) = Followed(data, 1);
        var heard = Listen(world);
        WalkTo(world, wolf, 2.5f);
        WolfTick(world, 1, wolf);
        Require(wolf.Scared && !heard.Contains("wolf_growl_approaching"), "a wolf following alone should stand silent until the player is 2 m off");
        WalkTo(world, wolf, 1.9f);
        WolfTick(world, .2f, wolf);
        Require(wolf.Routed && world.Health == 100, "a wolf following alone should run when the player comes within 2 m");
        WolfTick(world, 4, wolf);
        Require(wolf.Target == 0 && wolf.Mode == WolfMode.Patrol && wolf.Pos.DistanceTo(world.Player) > data.SightRadius,
            $"a wolf that ran should patrol again once out of the player's line of sight, not hunt {wolf.Target} from {wolf.Pos.DistanceTo(world.Player):0.0} m");
        WolfTick(world, 3, wolf);
        Require(wolf.Target == 0, "a wolf that ran should leave the player alone while it stays within smell of them");
        wolf.Pos = world.Player + new Vector2(data.SmellRadius + 5, 0);
        WolfTick(world, .2f, wolf);
        wolf.Pos = world.Player + new Vector2(data.SmellRadius - 5, 0);
        WolfTick(world, .2f, wolf);
        Require(wolf.Target == world.Me.Id, "a wolf that ran should smell the player again once it has been out of smell range of them");
    }

    private static void CheckAmongOthersAWolfWalkedAtGrowlsThenTurnsOnThePlayerForGood(GameData data) {
        var (world, wolf) = Followed(data, 2);
        var other = world.Wolves.Single(w => w != wolf);
        var both = new[] { wolf, other };
        var heard = Listen(world);
        WalkTo(world, wolf, 3.3f);
        WolfTick(world, 1, both);
        Require(wolf.Braced && !heard.Contains("wolf_growl_approaching"), "a wolf standing its ground should not growl until the player is 3 m off");
        WalkTo(world, wolf, 2.6f);
        WolfTick(world, WolfFollow.GrowlEvery + .5f, both);
        Require(heard.Count(sound => sound == "wolf_growl_approaching") == 2, $"a wolf standing its ground should growl again and again from 3 m, not {heard.Count(sound => sound == "wolf_growl_approaching")} times in 4 seconds");
        Require(wolf.Scared && world.Health == 100, "a growling wolf should still hold back while the player keeps 2 m off");
        WalkTo(world, wolf, 1.9f);
        WolfTick(world, 2, both);
        Require(wolf.Bold && !wolf.Scared && world.Health < 100, "among others, a wolf the player comes within 2 m of should turn on them");
        Require(other.Scared && !other.Bold, "only the wolf walked at should turn on the player; the other should keep following");
        world.UseTorch(wolf.Pos - world.Player);
        Require(!wolf.Scared, "a wolf that stood its ground should never be scared by a torch again");
    }

    /// <summary>A torch night with wolves in follow mode, settled on their circle; returns one of them.</summary>
    private static (World World, Wolf Wolf) Followed(GameData data, int count) {
        var world = TorchNight(data);
        var wolves = Enumerable.Range(0, count).Select(i => PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(World.StalkDistance, 0).Rotated(i * Mathf.Tau / count), Scared = true })).ToArray();
        WolfTick(world, 1, wolves);
        return (world, wolves[0]);
    }

    /// <summary>Puts the player a given distance from a wolf, on the line between them, as if they had walked at it.</summary>
    private static void WalkTo(World world, Wolf wolf, float distance) => world.Player = wolf.Pos + (world.Player - wolf.Pos).Normalized() * distance;
}
