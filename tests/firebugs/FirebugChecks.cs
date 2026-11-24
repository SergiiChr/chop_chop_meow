using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static GatheringSteps;
using static FirebugSteps;

/// <summary>Checks of firebugs and the bug net: when and where firebugs come out, how they fly from a player, catching one with a swing, and the worker's job.</summary>
public static class FirebugChecks {
    public static void Run(GameData data) {
        Test(CheckRopeWoodAndAWoodPartMakeABugNetWithEitherHammer, data);
        Test(CheckEightFirebugsComeOutAtNightfallAndOneMoreEachHour, data);
        Test(CheckFirebugsComeOutAwayFromBuildingsAndOutOfSight, data);
        Test(CheckFirebugHoversWhereItIsUntilAPlayerComesNear, data);
        Test(CheckFirebugDartsFromAPlayerThenRests, data);
        Test(CheckNetSwingCatchesARestingFirebug, data);
        Test(CheckSwingThatLandsAfterTheRestCatchesNothing, data);
        Test(CheckMissedSwingSendsARestingFirebugDarting, data);
        Test(CheckNetSwingNeedsANet, data);
        Test(CheckFullBackpackCatchesNoFirebug, data);
        Test(CheckHoldingOnToAFirebugCatchesNothing, data);
        Test(CheckFirebugJobNeedsANetAtAToolStation, data);
        Test(CheckFirebugsAndNettlesComeBackFromASave, data);
    }

    private static void CheckRopeWoodAndAWoodPartMakeABugNetWithEitherHammer(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "nettle_rope", 10); Give(world, "wood", 2); Give(world, "wood_parts", 2);
        var bench = BenchLeftWithAMallet(world, "bug_net");
        Require(bench.Inventory["bug_net"] == 1 && world.Backpack["nettle_rope"] == 5 && world.Backpack["wood"] == 1 && world.Backpack["wood_parts"] == 1,
            $"5 rope, 1 wood and 1 wood part should make a bug net with a mallet: {bench.Inventory["bug_net"]}");
        SwapMalletForSteelHammer(world, bench);
        world.LoadBatch(bench);
        SafeTick(world, 20);
        Require(bench.Inventory["bug_net"] == 2, "a steel hammer should make a bug net as well");
    }

    /// <summary>Night lasts 7.2 hours, so seven more come out after the first eight.</summary>
    private static void CheckEightFirebugsComeOutAtNightfallAndOneMoreEachHour(GameData data) {
        var world = EmptyWorld(data);
        RunUntilPhase(world, data.NightStarts - .01f);
        Require(world.ThingsOf(ThingKind.Firebug).Count == 0, "no firebug should be out before dark");
        RunUntilNight(world);
        Require(world.ThingsOf(ThingKind.Firebug).Count == 8, $"8 firebugs should come out at nightfall: {world.ThingsOf(ThingKind.Firebug).Count}");
        RunUntilPhase(world, data.NightStarts + 1.1f / 24);
        Require(world.ThingsOf(ThingKind.Firebug).Count == 9, $"a ninth firebug should come out an hour into the night: {world.ThingsOf(ThingKind.Firebug).Count}");
        RunUntilPhase(world, data.NightStarts + 2.1f / 24);
        Require(world.ThingsOf(ThingKind.Firebug).Count == 10, $"a tenth firebug should come out two hours into the night: {world.ThingsOf(ThingKind.Firebug).Count}");
        RunUntilPhase(world, .999f);
        Require(world.ThingsOf(ThingKind.Firebug).Count == 15, $"15 firebugs should be out by the end of the night: {world.ThingsOf(ThingKind.Firebug).Count}");
        RunUntilDawn(world);
        SafeTick(world, Step);
        Require(world.ThingsOf(ThingKind.Firebug).Count == 0, "every firebug should be gone at dawn");
    }

    private static void CheckFirebugsComeOutAwayFromBuildingsAndOutOfSight(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + new Vector2(60, 0));
        var fire = world.Add(ThingKind.Campfire, world.Player + new Vector2(0, 60));
        RunUntilNight(world);
        foreach (var bug in world.ThingsOf(ThingKind.Firebug)) {
            Require(bug.Pos.DistanceTo(world.Player) > data.SightRadius, $"a firebug should come out where no player sees it, not {bug.Pos.DistanceTo(world.Player):0.0} m off");
            Require(bug.Pos.DistanceTo(chest.Pos) >= 25 && bug.Pos.DistanceTo(fire.Pos) >= 25, $"a firebug should come out at least 25 m from every building: {bug.Pos}");
            Require(world.CanStand(bug.Pos), $"a firebug should come out over ground to stand on: {bug.Pos}");
        }
    }

    private static void CheckFirebugHoversWhereItIsUntilAPlayerComesNear(GameData data) {
        var (world, bug, _) = FirebugNight(data);
        Vector2 place = bug.Pos, hovering = world.Firebugs.At(bug);
        SafeTick(world, 3);
        Require(bug.Pos == place, "a firebug ten metres from the player should stay where it is");
        Require(world.Firebugs.At(bug) != hovering && world.Firebugs.At(bug).DistanceTo(place) <= Firebugs.HoverRadius, $"a firebug should hover within {Firebugs.HoverRadius} m of its place: {world.Firebugs.At(bug).DistanceTo(place):0.00} m");
    }

    /// <summary>A dart lasts 1.2 s at 5.46 m a second, between the player's walk of 4.2 and sprint of 6.72: 6.55 m.</summary>
    private static void CheckFirebugDartsFromAPlayerThenRests(GameData data) {
        var (world, bug, _) = FirebugNight(data);
        var from = bug.Pos;
        TireOut(world, bug);
        Require(Mathf.IsEqualApprox(bug.Pos.DistanceTo(from), 6.552f, .01f), $"a dart should carry a firebug 6.55 m: {bug.Pos.DistanceTo(from):0.000} m");
        Require(bug.Pos.DistanceTo(world.Player) > 8, $"a firebug should dart away from the player: {bug.Pos.DistanceTo(world.Player):0.0} m off");
        var rest = bug.Pos;
        world.Player = bug.Pos + new Vector2(1, 0);
        RunFrames(world, 1.5f);
        Require(bug.Pos == rest, "a firebug should rest for 1.6 s whoever stands beside it");
        RunFrames(world, .3f);
        Require(bug.Pos != rest, "a rested firebug should dart again from a player beside it");
    }

    private static void CheckNetSwingCatchesARestingFirebug(GameData data) {
        var (world, bug, _) = FirebugNight(data);
        TireOut(world, bug);
        SwingAt(world, bug);
        Require(world.Find(bug.Id) == null && world.Backpack["firebug"] == 1, "a net swung at a resting firebug 1.15 m off should catch it");
        Require(world.Had("firebug"), "the settlement should know firebugs once one is caught");
    }

    /// <summary>The swing lands 0.4 s after it starts; started 0.1 s before the rest ends, it gives the firebug 0.3 s to dart 1.6 m off.</summary>
    private static void CheckSwingThatLandsAfterTheRestCatchesNothing(GameData data) {
        var (world, bug, _) = FirebugNight(data);
        TireOut(world, bug);
        RunFrames(world, 1.5f);
        SwingAt(world, bug);
        Require(world.Find(bug.Id) != null && world.Backpack["firebug"] == 0, "a firebug that has rested should dart off before a late swing lands");
    }

    /// <summary>The hoop goes round 0.94 to 1.41 m from the player, so a firebug half a metre off is inside its sweep, and one 3 m off beyond it.</summary>
    private static void CheckMissedSwingSendsARestingFirebugDarting(GameData data) {
        var (world, bug, _) = FirebugNight(data);
        TireOut(world, bug);
        var rest = bug.Pos;
        SwingAt(world, bug, from: 3);
        SafeTick(world, Step);
        Require(world.Find(bug.Id) != null && bug.Pos != rest, "a swing that falls short should send a resting firebug darting");
        TireOut(world, bug);
        rest = bug.Pos;
        SwingAt(world, bug, from: .5f);
        SafeTick(world, Step);
        Require(world.Find(bug.Id) != null && bug.Pos != rest, "a swing that goes round a firebug too close to the player should send it darting");
    }

    private static void CheckNetSwingNeedsANet(GameData data) {
        var (world, bug, said) = FirebugNight(data);
        TireOut(world, bug);
        Drop(world, "bug_net");
        SwingAt(world, bug);
        Require(world.Find(bug.Id) != null && said.Contains("No bug net in your backpack."), $"a swing without a net should catch nothing and say why: {string.Join(" | ", said)}");
    }

    private static void CheckFullBackpackCatchesNoFirebug(GameData data) {
        var (world, bug, said) = FirebugNight(data);
        SetBackpackSlots(world, 2);
        Give(world, "wood", 64);
        TireOut(world, bug);
        SwingAt(world, bug);
        Require(world.Find(bug.Id) != null && world.Backpack["firebug"] == 0 && said.Any(line => line.Contains("Backpack full")), $"a full backpack should leave the firebug free and say so: {string.Join(" | ", said)}");
    }

    private static void CheckHoldingOnToAFirebugCatchesNothing(GameData data) {
        var (world, bug, _) = FirebugNight(data);
        TireOut(world, bug);
        Require(world.GatherBlock(bug)?.Contains("Swing") == true, $"a firebug should ask for a swing of the net: {world.GatherBlock(bug)}");
        HoldGather(world, bug, 1);
        Require(world.Find(bug.Id) != null && world.Backpack["firebug"] == 0, "holding on to a firebug should not catch it");
        Drop(world, "bug_net");
        Require(world.GatherBlock(bug)?.Contains("bug net") == true, $"a firebug should ask for a bug net: {world.GatherBlock(bug)}");
    }

    /// <summary>Workers are off the map while firebugs are out, so the job is checked as far as being given and finding its tool.</summary>
    private static void CheckFirebugJobNeedsANetAtAToolStation(GameData data) {
        var (world, bug, _) = FirebugNight(data);
        var rack = world.Add(ThingKind.Toolstation, world.Player + new Vector2(2, 0));
        var catcher = AddWorker(world, world.Player);
        Require(world.Jobs(bug).Contains(JobKind.Gather), "a firebug should offer the gather job");
        world.Assign(catcher, bug);
        Require(World.HasDuty(catcher, bug.Id, JobKind.Gather), "a worker should take the job of catching firebugs");
        Require(!world.CanWork(catcher, ThingKind.Firebug), "a catcher should not be able to work with no bug net in a tool station");
        rack.Inventory.Add("bug_net", 1);
        SafeTick(world, Step);
        Require(world.CanWork(catcher, ThingKind.Firebug), "a catcher should be able to work once a tool station holds a bug net");
    }

    private static void CheckFirebugsAndNettlesComeBackFromASave(GameData data) {
        var world = EmptyWorld(data);
        RunUntilNight(world);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.ThingsOf(ThingKind.Firebug).Count == 8 && loaded.ThingsOf(ThingKind.Nettle).Count == 3, $"a save made at night should keep its 8 firebugs and 3 nettles: {loaded.ThingsOf(ThingKind.Firebug).Count}, {loaded.ThingsOf(ThingKind.Nettle).Count}");
        SafeTick(loaded, 5);
        Require(loaded.ThingsOf(ThingKind.Firebug).Count == 8 && loaded.ThingsOf(ThingKind.Nettle).Count == 3, "a loaded night should bring out no second lot of firebugs or nettles");
    }
}
