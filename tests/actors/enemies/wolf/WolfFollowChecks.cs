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
        Test(CheckFollowingWolvesGoingOneWaySpreadRoundThePlayerAndKeepCircling, data);
        Test(CheckEachFollowingWolfWalksItsOwnWayRound, data);
        Test(CheckAWolfWalkedAtStandsBracedWhereItIs, data);
        Test(CheckAWolfFollowingAloneRunsOutOfSightAndLeavesThePlayerAlone, data);
        Test(CheckAmongOthersAWolfWalkedAtGrowlsThenThePackInSightTurnsOnThePlayerForGood, data);
        Test(CheckAWolfWhoseFellowFollowersAreOutOfSightRunsAsOneAlone, data);
    }

    /// <summary>Only followers within the player's line of sight count as others: one left out of sight does not make a wolf stand its ground.</summary>
    private static void CheckAWolfWhoseFellowFollowersAreOutOfSightRunsAsOneAlone(GameData data) {
        var (world, wolf) = Followed(data, 1);
        var far = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(0, data.SightRadius + 25), Follow = FollowStage.Circling });
        var heard = Listen(world);
        WalkTo(world, wolf, 2.6f);
        WolfTick(world, 1, wolf, far);
        Require(!heard.Contains("wolf_growl_approaching"), "a wolf whose fellow followers are out of sight should stand silent, as one alone does");
        WalkTo(world, wolf, 1.9f);
        WolfTick(world, .2f, wolf, far);
        Require(wolf.Follow == FollowStage.Routed && far.Following, "a wolf whose fellow followers are out of sight should run when the player comes within 2 m");
    }

    /// <summary>
    /// Wolves going clockwise walk the lane outside the circle and the others the one inside it, nearer the player.
    /// </summary>
    private static void CheckFollowingWolvesGoingOneWaySpreadRoundThePlayerAndKeepCircling(GameData data) {
        foreach (int way in new[] { 1, -1 }) {
            var world = TorchNight(data);
            // All three come from the east, within the swing that scares them, and circle the same way.
            var wolves = new[] { 0f, 12, -12 }.Select(degrees => PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(6, 0).Rotated(Mathf.DegToRad(degrees)), Round = way })).ToArray();
            world.UseTorch(Vector2.Right);
            Require(wolves.All(w => w.Following), "a torch waved at three wolves should put all three in follow mode");
            WolfTick(world, 45, wolves);
            float[] bearings = wolves.Select(w => Mathf.RadToDeg((w.Pos - world.Player).Angle())).OrderBy(b => b).ToArray();
            float[] gaps = { bearings[1] - bearings[0], bearings[2] - bearings[1], 360 - (bearings[2] - bearings[0]) };
            Require(gaps.All(gap => gap is > 80 and < 160), $"three following wolves going the way {way} should spread about a third of a circle apart, not {string.Join(", ", gaps.Select(g => $"{g:0}"))} degrees");
            Require(wolves.All(w => Math.Abs(w.Pos.DistanceTo(world.Player) - World.StalkDistance) <= WolfFollow.Wander + .2f),
                $"following wolves should keep about {World.StalkDistance} m off, not {string.Join(", ", wolves.Select(w => $"{w.Pos.DistanceTo(world.Player):0.0}"))} m");
            var before = wolves.Select(w => w.Pos).ToArray();
            WolfTick(world, 3, wolves);
            Require(wolves.All(w => w.Follow == FollowStage.Circling), $"following wolves going the way {way} should not stand braced round a player who stands still");
            Require(wolves.Zip(before).All(pair => pair.First.Pos.DistanceTo(pair.Second) > 1.5f), $"following wolves going the way {way} should keep circling, not hold their places");
            Require(world.Health == 100, "following wolves should not bite");
        }
    }

    /// <summary>A following wolf is given its way round the player at random, and goes round them that way at a walk, however far off they get.</summary>
    private static void CheckEachFollowingWolfWalksItsOwnWayRound(GameData data) {
        var (world, _) = Followed(data, 4);
        var wolves = world.Wolves.ToArray();
        Require(wolves.All(w => w.Round is 1 or -1), "each following wolf should be given a way round the player");
        Require(wolves.Any(w => w.Round == 1) && wolves.Any(w => w.Round == -1), "following wolves should not all be given the same way round");
        // Wolves going the same way first spread apart, which can take one of them back a little.
        WolfTick(world, 30, wolves);
        var from = wolves.Select(w => (w.Pos - world.Player).Angle()).ToArray();
        WolfTick(world, 3, wolves);
        for (int i = 0; i < wolves.Length; i++)
            Require(Mathf.AngleDifference(from[i], (wolves[i].Pos - world.Player).Angle()) * wolves[i].Round > .2f, $"a following wolf given the way {wolves[i].Round} should go round the player that way");
        // The player is suddenly 12 m off: the wolves come after them at a walk, 1.5 m a second.
        world.Player += new Vector2(0, 12);
        var left = wolves.Select(w => w.Pos).ToArray();
        WolfTick(world, 2, wolves);
        float walk = data.WolfTrot / 2;
        Require(wolves.Zip(left).All(pair => pair.First.Pos.DistanceTo(pair.Second) is > 2 and <= 2 * 1.02f * 1.5f) && walk == 1.5f,
            $"following wolves should come after a player who got away at a walk, not {string.Join(", ", wolves.Zip(left).Select(pair => $"{pair.First.Pos.DistanceTo(pair.Second) / 2:0.0}"))} m a second");
    }

    private static void CheckAWolfWalkedAtStandsBracedWhereItIs(GameData data) {
        var (world, wolf) = Followed(data, 1);
        WalkTo(world, wolf, 3);
        var stood = wolf.Pos;
        WolfTick(world, 2, wolf);
        Require(wolf.Follow == FollowStage.Braced && wolf.Pos == stood, "a following wolf the player has walked up to should stand braced where it is");
        world.Player += (world.Player - wolf.Pos).Normalized() * 2;
        WolfTick(world, 2, wolf);
        Require(wolf.Follow == FollowStage.Circling && wolf.Pos != stood, "a braced wolf should circle again once the player has backed off");
    }

    private static void CheckAWolfFollowingAloneRunsOutOfSightAndLeavesThePlayerAlone(GameData data) {
        var (world, wolf) = Followed(data, 1);
        var heard = Listen(world);
        WalkTo(world, wolf, 2.5f);
        WolfTick(world, 1, wolf);
        Require(wolf.Following && !heard.Contains("wolf_growl_approaching"), "a wolf following alone should stand silent until the player is 2 m off");
        WalkTo(world, wolf, 1.9f);
        WolfTick(world, .2f, wolf);
        Require(wolf.Follow == FollowStage.Routed && world.Health == 100, "a wolf following alone should run when the player comes within 2 m");
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

    private static void CheckAmongOthersAWolfWalkedAtGrowlsThenThePackInSightTurnsOnThePlayerForGood(GameData data) {
        var (world, wolf) = Followed(data, 2);
        var other = world.Wolves.Single(w => w != wolf);
        // A third follows from well beyond the player's line of sight, where it was left behind, and comes on at a walk.
        var far = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(0, data.SightRadius + 25), Follow = FollowStage.Circling });
        var all = new[] { wolf, other, far };
        var heard = Listen(world);
        WalkTo(world, wolf, 3.3f);
        WolfTick(world, 1, all);
        Require(wolf.Follow == FollowStage.Braced && !heard.Contains("wolf_growl_approaching"), "a wolf standing its ground should not growl until the player is 3 m off");
        WalkTo(world, wolf, 2.6f);
        WolfTick(world, WolfFollow.GrowlEvery + .5f, all);
        Require(DoorCheckSteps.Heard(heard, "wolf_growl_approaching") == 2, $"a wolf standing its ground should growl again and again from 3 m, not {DoorCheckSteps.Heard(heard, "wolf_growl_approaching")} times in 4 seconds");
        Require(wolf.Follow == FollowStage.Braced && world.Health == 100, "a growling wolf should still hold back while the player keeps 2 m off");
        WalkTo(world, wolf, 1.9f);
        WolfTick(world, .2f, all);
        Require(wolf.Follow == FollowStage.Bold && other.Follow == FollowStage.Bold, "among others, a wolf the player comes within 2 m of should set every wolf in sight on them");
        Require(far.Following && far.Pos.DistanceTo(world.Player) > data.SightRadius, "a wolf out of the player's line of sight should keep following");
        WolfTick(world, 2, all);
        Require(world.Health < 100, "wolves that turned on the player should attack");
        world.UseTorch(wolf.Pos - world.Player);
        Require(wolf.Follow == FollowStage.Bold, "a wolf that turned on the player should never be scared by a torch again");
    }
}
