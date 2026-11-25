using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static CabinSteps;
using static DoorCheckSteps;

/// <summary>
/// Checks of wolves at the door of a cabin with someone inside: scratching, going away from a locked door, forcing an
/// unlocked one and taking a player out, waiting behind another wolf, and waking sleepers.
/// </summary>
public static class DoorCheckChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfScratchesAtALockedDoorAndGoesAway, data);
        Test(CheckAWolfComesBackToALockedCabinOnlyAfterLeavingItsSmell, data);
        Test(CheckAWolfForcesAnUnlockedDoorAndTugsThePlayerOut, data);
        Test(CheckTheKeyTurnsWhileAWolfScratchesButNotOnceItPries, data);
        Test(CheckNobodyStepsOutWhileAWolfIsAtTheDoor, data);
        Test(CheckAWolfTakesOutTheLastPlayerWhoCameIn, data);
        Test(CheckAPassingWolfComesToAnOccupiedCabinWithoutAHowl, data);
        Test(CheckAWolfThatHuntedAPlayerFollowsThemToTheDoor, data);
        Test(CheckOtherWolvesWaitBehindTheOneAtTheDoorAndJoinIn, data);
        Test(CheckAWolfAtADoorTurnsToAPlayerOutInTheOpen, data);
        Test(CheckSleepBehindAnUnlockedDoorStopsWhenAWolfScratches, data);
        Test(CheckWolvesDoNotSlowSleepBehindALockedDoor, data);
        Test(CheckNobodySleepsWhileAWolfForcesTheDoor, data);
        Test(CheckAHitKnocksAWolfOffTheDoorItForces, data);
        Test(CheckAWolfLeavesAloneEveryLockedCabinItTriedWhileItSmellsThem, data);
        Test(CheckAWolfGivesUpOnACabinWhoseDoorItCannotReach, data);
    }

    private static void CheckAWolfScratchesAtALockedDoorAndGoesAway(GameData data) {
        var (world, hut, heard) = ShelteredNight(data, locked: true);
        var wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 3.5f);
        Require(wolf.DoorCheck == DoorCheckStage.Scratching && Heard(heard, "door_scratch") == 1, $"a wolf should scratch at a door for 3.6 s with one scratching sound, not {wolf.DoorCheck}: heard {string.Join(", ", heard)}");
        DoorCheckTick(world, .2f);
        Require(wolf.DoorCheck == DoorCheckStage.None && wolf.Target == 0 && wolf.Mode == WolfMode.Patrol, $"a wolf should go away from a locked door once it has scratched, not {wolf.DoorCheck}");
        float gap = wolf.Pos.DistanceTo(hut.Pos);
        DoorCheckTick(world, 3);
        Require(wolf.Pos.DistanceTo(hut.Pos) > gap + 2 && wolf.DoorCheck == DoorCheckStage.None, "a wolf that left a locked door should walk on away from the cabin");
        Require(world.Sheltered == hut.Id && world.Health == 100 && hut.Locked, "a player behind a locked door should stay inside, unhurt");
    }

    private static void CheckAWolfComesBackToALockedCabinOnlyAfterLeavingItsSmell(GameData data) {
        var (world, hut, heard) = ShelteredNight(data, locked: true);
        var wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 3.7f);
        // Still within smell of the cabin, 30 m, it leaves the cabin alone.
        wolf.Pos = hut.Pos + new Vector2(0, 12);
        DoorCheckTick(world, 1);
        Require(wolf.DoorCheck == DoorCheckStage.None, "a wolf that left a locked door should leave that cabin alone while it is within smell of it");
        wolf.Pos = hut.Pos + new Vector2(0, 34);
        DoorCheckTick(world, .1f);
        wolf.Pos = hut.Pos + new Vector2(0, 12);
        DoorCheckTick(world, .1f);
        Require(wolf.DoorCheck == DoorCheckStage.Coming && wolf.DoorCheckCabin == hut.Id, $"a wolf that went out of smell of a cabin and found it again should come back to its door, not {wolf.DoorCheck}");
        Require(!heard.Contains("wolf_howl") && !heard.Contains("wolf_howl_group"), "a wolf should not howl at a cabin");
    }

    /// <summary>The stages take 3.6, 1.5, 2.5, 2 and 0.4 seconds, about ten in all; a bite takes 15 health.</summary>
    private static void CheckAWolfForcesAnUnlockedDoorAndTugsThePlayerOut(GameData data) {
        var (world, hut, heard) = ShelteredNight(data, locked: false);
        var wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 3.5f);
        Require(wolf.DoorCheck == DoorCheckStage.Scratching, $"3.5 s in, a wolf should still scratch, not {wolf.DoorCheck}");
        DoorCheckTick(world, .3f);
        Require(wolf.DoorCheck == DoorCheckStage.Prying, $"3.8 s in, a wolf at an unlocked door should pry at its edge, not {wolf.DoorCheck}");
        Require(Heard(heard, "door_rattle") == 1, $"the door should rattle once in the wolf's first pry: heard {string.Join(", ", heard)}");
        DoorCheckTick(world, 1.1f);
        Require(wolf.DoorCheck == DoorCheckStage.Prying, $"4.9 s in, a wolf should still pry, not {wolf.DoorCheck}");
        DoorCheckTick(world, .5f);
        Require(wolf.DoorCheck == DoorCheckStage.Opening, $"5.4 s in, a wolf should push the door open, not {wolf.DoorCheck}");
        Require(Heard(heard, "door_rattle") == 3, $"the door should rattle once in each of the wolf's three pries: heard {string.Join(", ", heard)}");
        DoorCheckTick(world, 2.1f);
        Require(wolf.DoorCheck == DoorCheckStage.Opening && !heard.Contains("wolf_growl_approaching"), $"7.5 s in, a wolf should still push the door open without a growl, not {wolf.DoorCheck}");
        DoorCheckTick(world, .5f);
        Require(wolf.DoorCheck == DoorCheckStage.Staring && Heard(heard, "wolf_growl_approaching") == 1, $"8 s in, a wolf should stare in with one growl, not {wolf.DoorCheck}");
        DoorCheckTick(world, 1.6f);
        Require(wolf.DoorCheck == DoorCheckStage.Staring, $"9.6 s in, a wolf should still stare, not {wolf.DoorCheck}");
        DoorCheckTick(world, .45f);
        Require(wolf.DoorCheck == DoorCheckStage.Launching && world.Sheltered == hut.Id && world.Health == 100, $"10.05 s in, a wolf should be launching at a player still inside and unhurt, not {wolf.DoorCheck}");
        DoorCheckTick(world, .45f);
        Require(world.Sheltered == 0 && world.Door == Doorway.Tugged && wolf.Tugging && wolf.Target == world.Me.Id, $"10.5 s in, the wolf should have the player out of the cabin by the waist, not {world.Door}");
        Require(world.Health == 85 && Heard(heard, "tug_scream") == 1 && Heard(heard, "wolf_bite") == 1, $"a wolf taking a player out should bite them once, and they scream: {world.Health} health, heard {string.Join(", ", heard)}");
        for (int i = 0; i < 60 && world.Door == Doorway.Tugged; i++) DoorCheckTick(world, .05f);
        Require(world.Sheltered == 0 && world.Door == Doorway.None && !wolf.Tugging, $"a healthy player tugged out of their cabin should be let go and left standing outside, not {world.Door}");
        Require(world.Player.DistanceTo(Doorways.DoorPoint(hut) + new Vector2(0, 1)) < .01f, $"a wolf should tug a player a metre out from the door, not to {world.Player - Doorways.DoorPoint(hut)}");
        DoorCheckTick(world, .5f);
        Require(world.Sheltered == 0 && wolf.DoorCheck == DoorCheckStage.None && wolf.Target == world.Me.Id, $"the wolf should go on hunting the player it let go, who stays outside, not {wolf.DoorCheck}");
    }

    private static void CheckTheKeyTurnsWhileAWolfScratchesButNotOnceItPries(GameData data) {
        var (world, hut, heard) = ShelteredNight(data, locked: false);
        var wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 2);
        world.Doorways.TurnKey();
        Require(hut.Locked && Heard(heard, "door_lock") == 1, "the key should lock the door while a wolf scratches at it");
        DoorCheckTick(world, 1.8f);
        Require(wolf.DoorCheck == DoorCheckStage.None && world.Sheltered == hut.Id && world.Health == 100, $"a wolf should go away from a door locked while it scratched, not {wolf.DoorCheck}");

        (world, hut, heard) = ShelteredNight(data, locked: false);
        wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 3.9f);
        Require(wolf.DoorCheck == DoorCheckStage.Prying, $"a wolf should pry at an unlocked door after scratching, not {wolf.DoorCheck}");
        world.Doorways.TurnKey();
        Require(!hut.Locked && !heard.Contains("door_lock"), "the key should do nothing once a wolf has its snout in the door");
    }

    private static void CheckNobodyStepsOutWhileAWolfIsAtTheDoor(GameData data) {
        var (world, hut, _) = ShelteredNight(data, locked: true);
        var wolf = ScratchingWolf(world, hut);
        world.Doorways.Leave();
        Require(world.Sheltered == hut.Id && world.Door == Doorway.None && hut.Locked, "a player should not step out, nor the door unlock, while a wolf scratches at it");
        DoorCheckTick(world, 3.7f);
        Require(wolf.DoorCheck == DoorCheckStage.None, "the wolf should have gone from the locked door");
        world.Doorways.Leave();
        Require(world.Door == Doorway.Leaving, "a player should step out once the wolf has gone");
    }

    private static void CheckAWolfTakesOutTheLastPlayerWhoCameIn(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        StepInside(world, hut, guest);
        StepInside(world, hut);
        var wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 10.5f);
        Require(world.Door == Doorway.Tugged && world.Sheltered == 0 && wolf.Target == world.Me.Id, $"the wolf should take out the player who came in last, not leave them {world.Door}");
        Require(guest.Sheltered == hut.Id && guest.Health == 100 && guest.Door == Doorway.None, "the player who came in first should stay inside, unhurt");
        world.As(guest, () => world.Doorways.Leave());
        Require(guest.Sheltered == hut.Id, "the player left inside should not step out while the wolf tugs the other out");
    }

    private static void CheckAPassingWolfComesToAnOccupiedCabinWithoutAHowl(GameData data) {
        var world = NightWorld(data);
        var heard = Listen(world);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = hut.Pos + new Vector2(60, 0);
        // 25 m off, within smell (30 m) and out of sight (15 m).
        var wolf = PatrolWolf(world, new Wolf { Pos = hut.Pos + new Vector2(0, 25) });
        DoorCheckTick(world, .5f);
        Require(wolf.DoorCheck == DoorCheckStage.None, "a wolf should pass a cabin with nobody inside");
        // The player steps in with the wolf out of smell of them, then the wolf comes by again.
        wolf.Pos = hut.Pos + new Vector2(0, 45);
        StepInside(world, hut);
        wolf.Pos = hut.Pos + new Vector2(0, 25);
        DoorCheckTick(world, .1f);
        Require(wolf.DoorCheck == DoorCheckStage.Coming && wolf.DoorCheckCabin == hut.Id && wolf.Target == 0, $"a wolf within smell of a cabin with someone inside should come to its door, not {wolf.DoorCheck}");
        float gap = wolf.Pos.DistanceTo(Doorways.DoorPoint(hut));
        DoorCheckTick(world, 2);
        Require(wolf.Pos.DistanceTo(Doorways.DoorPoint(hut)) < gap - 4, "a wolf coming to a door should close in on it");
        Require(!heard.Contains("wolf_howl") && !heard.Contains("wolf_howl_group"), $"a wolf should not howl at a cabin: heard {string.Join(", ", heard)}");
    }

    private static void CheckAWolfThatHuntedAPlayerFollowsThemToTheDoor(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        var wolf = PlaceWolf(world, new Wolf { Pos = Doorways.DoorPoint(hut) + new Vector2(0, 9) });
        StepInside(world, hut);
        DoorCheckTick(world, .1f);
        Require(wolf.Target == 0 && wolf.DoorCheck == DoorCheckStage.Coming && wolf.DoorCheckCabin == hut.Id, $"a wolf hunting a player who got inside should go to the cabin's door, not {wolf.DoorCheck}");
        for (int i = 0; i < 100 && wolf.DoorCheck == DoorCheckStage.Coming; i++) DoorCheckTick(world, .05f);
        // It scratches at the half of the door that opens, 0.2 m to the right of the door point.
        Require(wolf.DoorCheck == DoorCheckStage.Scratching && wolf.Pos.DistanceTo(Doorways.DoorPoint(hut) + new Vector2(.2f, 0)) < .1f, $"the wolf should scratch at the door once it is there, not {wolf.DoorCheck} at {wolf.Pos - Doorways.DoorPoint(hut)}");
    }

    private static void CheckOtherWolvesWaitBehindTheOneAtTheDoorAndJoinIn(GameData data) {
        var (world, hut, _) = ShelteredNight(data, locked: false);
        var door = Doorways.DoorPoint(hut);
        var first = ScratchingWolf(world, hut);
        var second = PatrolWolf(world, new Wolf { Pos = door + new Vector2(1, 6) });
        var third = PatrolWolf(world, new Wolf { Pos = door + new Vector2(-1, 7) });
        DoorCheckTick(world, 3);
        Require(second.DoorCheck == DoorCheckStage.Coming && third.DoorCheck == DoorCheckStage.Coming && first.DoorCheck == DoorCheckStage.Scratching, $"wolves coming to a door another scratches at should wait, not {second.DoorCheck} and {third.DoorCheck}");
        foreach (var waiting in new[] { second, third })
            Require((waiting.Pos - door).Dot(Doorways.OutOfDoor) > 2 && waiting.Pos.DistanceTo(door) < 4, $"a waiting wolf should stand a few metres behind the one at the door, not {waiting.Pos - door} from it");
        Require(second.Pos.DistanceTo(third.Pos) > .8f, "waiting wolves should stand apart");
        DoorCheckTick(world, 7.5f);
        Require(world.Door == Doorway.Tugged && first.Tugging, $"the wolf at the door should take the player out, not leave them {world.Door}");
        Require(second.Target == world.Me.Id && third.Target == world.Me.Id && second.DoorCheck == DoorCheckStage.None, "the wolves that waited should go for the player once they are out");
        // The player is out of the door 0.6 s into the tug; until then nobody can get to their sides.
        Require(second.Side == 0 && third.Side == 0, $"no wolf should take the side of a player still in the cabin's door: sides {second.Side} and {third.Side}");
        DoorCheckTick(world, .7f);
        Require(second.Side != 0 && third.Side == -second.Side, $"the two wolves that waited should each take a side of the player once they are out of the door: sides {second.Side} and {third.Side}");
    }

    private static void CheckAWolfAtADoorTurnsToAPlayerOutInTheOpen(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.As(guest, () => world.Player = hut.Pos + new Vector2(60, 0));
        StepInside(world, hut);
        var wolf = ScratchingWolf(world, hut);
        // The guest comes within its sight, 15 m.
        world.As(guest, () => world.Player = Doorways.DoorPoint(hut) + new Vector2(0, 8));
        DoorCheckTick(world, .1f);
        Require(wolf.DoorCheck == DoorCheckStage.None && wolf.Target == guest.Id, $"a wolf scratching at a door should go for a player who comes within its sight, not stay {wolf.DoorCheck}");
    }

    private static void CheckSleepBehindAnUnlockedDoorStopsWhenAWolfScratches(GameData data) {
        var (world, hut, heard) = ShelteredNight(data, locked: false);
        var told = new List<string>();
        world.Notify += told.Add;
        var wolf = PatrolWolf(world, new Wolf { Pos = Doorways.DoorPoint(hut) + new Vector2(0, 20) });
        int day = world.Day;
        float time = world.Time;
        Nourished(world);
        world.Sleep.Start(hut);
        for (int i = 0; i < 120 && !told.Contains("You hear something"); i++) world.Tick(1f / 60);
        Require(world.Day == day && world.Sleep.Place == hut.Id && told.Contains("You hear something"),
            "a wolf coming within sight should stop the skip but leave the cabin sleeper asleep");
        for (int i = 0; i < 240 && world.Sleep.Place != 0; i++) DoorCheckTick(world, .05f);
        // A wolf trots 3 m a second, so it covers the 20 m in about 7 seconds.
        Require(world.Time - time < 12, $"sleep should stop as the wolf reaches the door, not {world.Time - time:0} s later");
        Require(wolf.DoorCheck == DoorCheckStage.Scratching && wolf.DoorCheckTime == 0, $"sleep should stop as the wolf starts scratching, not with it {wolf.DoorCheck} for {wolf.DoorCheckTime} s");
        Require(world.Sleep.Place == 0 && world.Sheltered == hut.Id && told.Contains("You are awakened by the noise outside."), $"the sleeper should wake inside and be told why: {string.Join(" | ", told)}");
        DoorCheckTick(world, .05f);
        Require(Heard(heard, "door_scratch") == 1, $"the sleeper should hear the scratching from its start: heard {string.Join(", ", heard)}");
    }

    private static void CheckWolvesDoNotSlowSleepBehindALockedDoor(GameData data) {
        var (world, hut, _) = ShelteredNight(data, locked: true);
        ScratchingWolf(world, hut);
        SetNeeds(world, 100, 100, 60);
        var told = new List<string>();
        world.Notify += told.Add;
        float time = world.Time;
        int day = world.Day;
        world.Sleep.Start(hut);
        world.Tick(1f / 60);
        Require(world.Time > time + 1f / 60 && !told.Contains("You hear something"),
            "a wolf outside a locked door should not warn the sleeper or slow time");
        Require(world.Sleep.Place == hut.Id && world.Sheltered == hut.Id && world.Health > 60 && hut.Locked,
            "the sleeper should stay inside and heal even as the wolf scratches at the locked door");
        UntilAllAwake(world);
        Require(!told.Contains("You hear something"), "the wolf should not trigger the warning later in the same sleep");
        Require(world.Day == day + 1 && world.Sheltered == hut.Id && hut.Locked,
            "the player should still wake inside the locked cabin at dawn");
    }

    /// <summary>A wolf walks at 1.5 m a second, so 12 seconds after its second scratch it is still within the 30 m it smells both cabins from.</summary>
    private static void CheckAWolfLeavesAloneEveryLockedCabinItTriedWhileItSmellsThem(GameData data) {
        var (world, hut, heard) = ShelteredNight(data, locked: true);
        var guest = world.Arrive("guest", 2);
        var second = world.Add(ThingKind.Hut, hut.Pos + new Vector2(8, 0));
        StepInside(world, second, guest);
        world.As(guest, world.Doorways.TurnKey);
        Require(second.Locked, "the guest inside the second cabin should lock its door");
        var wolf = ScratchingWolf(world, hut);
        for (float time = 0; time < 25 && Heard(heard, "door_scratch") < 2; time += .05f) DoorCheckTick(world, .05f);
        Require(Heard(heard, "door_scratch") == 2 && wolf.DoorCheckCabin == second.Id, $"a wolf sent away from one locked cabin should try the other one beside it: heard {string.Join(", ", heard)}");
        DoorCheckTick(world, 12);
        Require(wolf.Pos.DistanceTo(hut.Pos) < data.SmellRadius && wolf.Pos.DistanceTo(second.Pos) < data.SmellRadius, "the wolf should still be within smell of both cabins");
        Require(Heard(heard, "door_scratch") == 2 && wolf.DoorCheck == DoorCheckStage.None, $"a wolf should leave alone both locked cabins it tried, not scratch {Heard(heard, "door_scratch")} times and be {wolf.DoorCheck}");
    }

    private static void CheckAWolfGivesUpOnACabinWhoseDoorItCannotReach(GameData data) {
        var world = NightWorld(data);
        // A cabin from an older save, with its door in the lake.
        var hut = world.Add(ThingKind.Hut, ShoreForACabin(world));
        SetSheltered(world, hut);
        var from = new[] { new Vector2(0, -5), new Vector2(-5, 0), new Vector2(5, 0), new Vector2(-4, -4), new Vector2(4, -4) }.Select(way => hut.Pos + way).First(world.CanStand);
        var wolf = PatrolWolf(world, new Wolf { Pos = from });
        DoorCheckTick(world, 4);
        Require(wolf.DoorCheck == DoorCheckStage.None && wolf.Mode == WolfMode.Patrol, $"a wolf with no way to a cabin's door should give up on it, not stay {wolf.DoorCheck}");
        var at = wolf.Pos;
        DoorCheckTick(world, 3);
        Require(wolf.DoorCheck == DoorCheckStage.None && wolf.Pos.DistanceTo(at) > 2, $"a wolf that gave up on a cabin should walk on and leave it alone, not be {wolf.DoorCheck} {wolf.Pos.DistanceTo(at):0.0} m on");
    }

    private static void CheckAHitKnocksAWolfOffTheDoorItForces(GameData data) {
        var (world, hut, _) = ShelteredNight(data, locked: false);
        // The guest waits out of the wolf's smell until it has its snout in the door.
        var guest = world.Arrive("guest", 2);
        world.As(guest, () => world.Player = Doorways.DoorPoint(hut) + new Vector2(60, 0));
        var wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 3.9f);
        Require(wolf.DoorCheck == DoorCheckStage.Prying, $"a wolf should pry at an unlocked door after scratching, not {wolf.DoorCheck}");
        Give(guest, "steel_axe", 1);
        world.As(guest, () => { world.Player = wolf.Pos + new Vector2(1, 0); world.Attack(Vector2.Left); });
        world.Tick(.45f);
        Require(wolf.DoorCheck == DoorCheckStage.None, $"a wolf hit while it forces a door should be knocked off it, not go on {wolf.DoorCheck}");
        DoorCheckTick(world, 8);
        Require(world.Sheltered == hut.Id && world.Health == 100, "the player inside should be left alone by a wolf knocked off the door");
    }

    private static void CheckNobodySleepsWhileAWolfForcesTheDoor(GameData data) {
        var (world, hut, _) = ShelteredNight(data, locked: false);
        var told = new List<string>();
        world.Notify += told.Add;
        var wolf = ScratchingWolf(world, hut);
        DoorCheckTick(world, 3.9f);
        Require(wolf.DoorCheck == DoorCheckStage.Prying, $"a wolf should pry at an unlocked door after scratching, not {wolf.DoorCheck}");
        float time = world.Time;
        Nourished(world);
        world.Sleep.Start(hut);
        Require(world.Time == time && wolf.DoorCheck == DoorCheckStage.Prying, $"no time should pass in sleep while a wolf forces the door, not {world.Time - time:0} s with the wolf {wolf.DoorCheck}");
        Require(world.Sheltered == hut.Id && world.Health == 100 && told.Contains("You are awakened by the noise outside."), $"the player should stay inside, unhurt, and be told why: {string.Join(" | ", told)}");
    }
}
