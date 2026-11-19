using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;
using static WorkerSteps;
using static CabinSteps;

/// <summary>Checks of cabin doors: walking round to the door, stepping in and out, the queue at the door, knocking, standing aside, waking in a cabin and keeping its door spot clear.</summary>
public static class CabinChecks {
    public static void Run(GameData data) {
        Test(CheckPlayerBehindACabinWalksRoundItToTheDoor, data);
        Test(CheckSteppingInTakesLongerAtAWalkThanAtASprint, data);
        Test(CheckWolvesCannotReachAPlayerHalfWayIn, data);
        Test(CheckSteppingOutIsSafeOnlyInTheDoorway, data);
        Test(CheckACabinHoldsTwoPlayersAndTheThirdKnocksOnTheDoor, data);
        Test(CheckAPlayerAtALockedDoorKnocksUntilSomeoneInsideTurnsTheKey, data);
        Test(CheckSteppingOutOfALockedCabinUnlocksIt, data);
        Test(CheckOnlyAPlayerInsideTurnsTheKey, data);
        Test(CheckAPlayerKnockingOnACabinDoorStepsAsideForOneComingOutThenGoesIn, data);
        Test(CheckAPlayerAtACabinDoorIsBusyAndCanDoNothingElse, data);
        Test(CheckAPlayerInsideACabinCanStillEat, data);
        Test(CheckOnlyAPlayerKnockingOnTheDoorGoesInWhenSomeoneStepsOut, data);
        Test(CheckAPlayerRespawnsInTheNearestCabinWithAPlaceLeft, data);
        Test(CheckAPlayerDoesNotRespawnInACabinSomeoneIsTuggedOutOfTheDoorOf, data);
        Test(CheckACabinCannotBeBuiltWithItsDoorOnGroundNobodyCanStandOn, data);
        Test(CheckNothingCanBeBuiltOnACabinsDoorSpot, data);
        Test(CheckSteppingOutOfACabinWhoseDoorIsBlockedLandsOnOpenGround, data);
        Test(CheckACabinNobodyIsLeftInsideIsUnlocked, data);
    }

    private static void CheckPlayerBehindACabinWalksRoundItToTheDoor(GameData data) {
        var world = EmptyWorld(data);
        var hut = world.Add(ThingKind.Hut, new Vector2(20, 20));
        // The door faces +Y, so this is straight behind the cabin.
        world.Player = hut.Pos + new Vector2(0, -3);
        float widest = 0;
        for (int i = 0; i < 600 && Doorways.TowardDoor(hut, world.Player) is var way && way != Vector2.Zero; i++) {
            var before = world.Player;
            world.MovePlayer(way * data.MoveSpeed(false) / 60);
            Require(world.Player != before, $"the walk to the door should never stall against the cabin, at {world.Player}");
            widest = Math.Max(widest, Math.Abs(world.Player.X - hut.Pos.X));
        }
        Require(world.Player.DistanceTo(Doorways.DoorPoint(hut)) < .2f, $"the walk should end at the cabin's door, not at {world.Player}");
        Require(widest > 1.5f, "the walk should go round the side of the cabin");
        Require(world.Doorways.Enter(hut, false) && world.Door == Doorway.Entering, "a player at the door should step in");
    }
    private static void CheckSteppingInTakesLongerAtAWalkThanAtASprint(GameData data) {
        var world = EmptyWorld(data);
        var hut = world.Add(ThingKind.Hut, new Vector2(20, 20));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        world.Tick(1.25f);
        Require(world.Sheltered == 0, "stepping in at a walk should take longer than 1.25 s");
        world.Tick(.1f);
        Require(world.Sheltered == hut.Id, "stepping in at a walk should be done by 1.35 s");
        world.Doorways.Leave();
        Require(world.Door == Doorway.Leaving && world.Player == Doorways.DoorPoint(hut), "stepping out should start at the door");
        world.Tick(1.4f);
        Require(world.Door == Doorway.Leaving, "stepping out should take longer than 1.4 s");
        world.Tick(.15f);
        Require(world.Door == Doorway.None && world.Sheltered == 0, "stepping out should be done by 1.55 s");
        Require(world.Doorways.Enter(hut, true), "a player who sprinted to the door should step in");
        world.Tick(.65f);
        Require(world.Sheltered == 0, "stepping in at a sprint should take longer than 0.65 s");
        world.Tick(.1f);
        Require(world.Sheltered == hut.Id, "stepping in at a sprint should be done by 0.75 s");
    }
    private static void CheckWolvesCannotReachAPlayerHalfWayIn(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        world.Tick(Doorways.EnterSeconds / 2 + .05f);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        world.Tick(.5f);
        Require(world.Health == 100 && world.Door == Doorway.Entering && wolf.Target == 0, "a wolf should give up on a player past the first half of stepping in");
    }
    private static void CheckSteppingOutIsSafeOnlyInTheDoorway(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        StepInside(world, hut);
        world.Doorways.Leave();
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        world.Tick(.5f);
        Require(world.Health == 100 && wolf.Target == 0, "a wolf should not reach a player in the first half of stepping out");
        world.Tick(.3f);
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        world.Tick(.3f);
        Require(world.Health < 100 && world.Door == Doorway.Leaving, "a wolf should bite a player in the second half of stepping out, who walks on out");
    }
    private static void CheckACabinHoldsTwoPlayersAndTheThirdKnocksOnTheDoor(GameData data) {
        var (world, guest, _) = Hosted(data);
        var third = world.Arrive("third", 3);
        var heard = Listen(world);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "the host at the door should step in");
        world.Tick(.2f);
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut) + new Vector2(.3f, 0); Require(world.Doorways.Enter(hut, false), "the guest at the door should start after the host"); });
        Require(guest.Door == Doorway.Following, $"a player at the door behind someone stepping in should follow them, not {guest.Door}");
        world.As(guest, () => world.MoveTo(guest.Pos + Vector2.Right));
        Require(guest.Pos == Doorways.DoorPoint(hut) + new Vector2(.3f, 0), "a player following someone in should not walk away");
        world.Tick(.2f);
        world.As(third, () => { world.Player = Doorways.DoorPoint(hut) + new Vector2(-.3f, 0); Require(world.Doorways.Enter(hut, false), "the third player at the door should start after the guest"); });
        Require(third.Door == Doorway.Following, $"a third player at the door should follow the two ahead, not {third.Door}");
        world.Tick(Doorways.EnterSeconds);
        Require(world.Sheltered == hut.Id && guest.Door == Doorway.Entering && third.Door == Doorway.Following, $"the second player should step in once the first is inside, not {guest.Door}, with the third still behind, not {third.Door}");
        world.Tick(Doorways.EnterSeconds + .05f);
        Require(guest.Sheltered == hut.Id, "a cabin should hold a second player");
        Require(third.Door == Doorway.Knocking && third.Sheltered == 0, $"the door of a full cabin should shut on the third player, who knocks on it, not {third.Door}");
        world.Tick(.4f);
        Require(!heard.Contains("door_knock"), "a player left outside should knock only once the door has swung shut");
        world.Tick(1.1f);
        Require(third.Door == Doorway.None && third.Sheltered == 0 && heard.Count(name => name == "door_knock") == 3, $"the third player should knock 3 times over a second and stand free: heard {string.Join(", ", heard)}");
        world.As(third, () => Require(world.Doorways.Enter(hut, false), "a player at the door of a full cabin should knock on it"));
        Require(third.Door == Doorway.Knocking, "a player at the door of a full cabin should knock on it");
    }
    private static void CheckACabinNobodyIsLeftInsideIsUnlocked(GameData data) {
        var (world, guest, _) = Hosted(data);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        StepInside(world, hut, guest);
        world.As(guest, world.Doorways.TurnKey);
        Require(hut.Locked, "the guest inside should lock the door");
        world.Depart(guest.Id);
        Require(!hut.Locked, "a cabin whose only player left the game should be unlocked");
        // A save loaded without whoever locked a cabin has it locked with nobody inside.
        hut.Locked = true;
        StepInside(world, hut);
        Require(world.Sheltered == hut.Id && !hut.Locked, "a player stepping into a cabin locked with nobody inside should find its door unlocked");
    }
    private static void CheckAPlayerAtALockedDoorKnocksUntilSomeoneInsideTurnsTheKey(GameData data) {
        var (world, guest, _) = Hosted(data);
        var heard = Listen(world);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        StepInside(world, hut);
        world.Doorways.TurnKey();
        Require(hut.Locked && heard.Count(name => name == "door_lock") == 1, $"turning the key from inside should lock the door with one lock sound: heard {string.Join(", ", heard)}");
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut); Require(world.Doorways.Enter(hut, false), "a player at a locked door should knock on it"); });
        Require(guest.Door == Doorway.Knocking, $"a player at the locked door of a cabin with a place left should knock on it, not {guest.Door}");
        world.Tick(Doorways.KnockLead + Doorways.KnockSeconds + .05f);
        Require(guest.Door == Doorway.None && guest.Sheltered == 0, "a player who knocked on a locked door should stand free outside");
        world.Doorways.TurnKey();
        Require(!hut.Locked && heard.Count(name => name == "door_lock") == 2, $"turning the key again should unlock the door with the same sound: heard {string.Join(", ", heard)}");
        world.As(guest, () => Require(world.Doorways.Enter(hut, false), "a player at an unlocked door should step in"));
        Require(guest.Door == Doorway.Entering, $"a player at the unlocked door of a cabin with a place left should step in, not {guest.Door}");
        world.Tick(Doorways.EnterSeconds + .05f);
        Require(guest.Sheltered == hut.Id && world.Sheltered == hut.Id, "both players should be inside the cabin");
    }
    private static void CheckSteppingOutOfALockedCabinUnlocksIt(GameData data) {
        var (world, guest, _) = Hosted(data);
        var heard = Listen(world);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        StepInside(world, hut);
        StepInside(world, hut, guest);
        world.As(guest, () => world.Doorways.TurnKey());
        Require(hut.Locked, "either player inside should be able to lock the door");
        world.Doorways.Leave();
        Require(world.Door == Doorway.Leaving && !hut.Locked, "stepping out of a locked cabin should unlock its door and step out");
        Require(heard.Count(name => name == "door_lock") == 2, $"the key should be heard turning back: heard {string.Join(", ", heard)}");
        world.Tick(Doorways.LeaveSeconds + .05f);
        Require(!hut.Locked && guest.Sheltered == hut.Id, "the door should stay unlocked behind the one who stepped out, with the other still inside");
    }
    private static void CheckOnlyAPlayerInsideTurnsTheKey(GameData data) {
        var (world, guest, _) = Hosted(data);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        world.Player = Doorways.DoorPoint(hut);
        world.Doorways.TurnKey();
        Require(!hut.Locked, "a player outside a cabin should not lock its door");
        StepInside(world, hut);
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut); world.Doorways.TurnKey(); });
        Require(!hut.Locked, "a player at the door of a cabin someone else is in should not lock it");
        world.Doorways.TurnKey();
        Require(hut.Locked, "the player inside should lock the door");
        SetNeeds(world, health: 0);
        world.Tick(.05f);
        Require(world.Dead && !hut.Locked, "a cabin whose only player has fallen should be unlocked");
    }
    private static void CheckAPlayerKnockingOnACabinDoorStepsAsideForOneComingOutThenGoesIn(GameData data) {
        var (world, guest, _) = Hosted(data);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        StepInside(world, hut);
        world.Doorways.TurnKey();
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut); Require(world.Doorways.Enter(hut, false) && guest.Door == Doorway.Knocking, "a player at the locked door of a cabin someone is in should knock on it"); });
        world.Tick(.2f);
        world.Doorways.Leave();
        Require(guest.Door == Doorway.Aside, $"a player knocking on the door should step aside for someone stepping out, not {guest.Door}");
        Require((guest.Pos - Doorways.DoorPoint(hut)).Dot(Doorways.AsideWay) > .5f, "a player stepping aside should stand to the side of the door");
        Require(world.Player.DistanceTo(Doorways.DoorPoint(hut) - Doorways.AsideWay * Doorways.MakeWay) < .01f, "someone stepping out past a player standing aside should keep to the other side of the door");
        world.Tick(Doorways.LeaveSeconds / 2 - .1f);
        Require(guest.Door == Doorway.Aside && world.Door == Doorway.Leaving, "a player standing aside should wait until the one stepping out is through the doorway");
        world.Tick(.2f);
        Require(world.Door == Doorway.Leaving && guest.Door == Doorway.Entering, $"once the other is through the doorway, a player standing aside should squeeze past them in, not {guest.Door}");
        world.Tick(Doorways.EnterSeconds + .05f);
        Require(guest.Sheltered == hut.Id && world.Sheltered == 0, "the player who stood aside should end up inside, and the one who came out outside");
    }
    private static void CheckAPlayerAtACabinDoorIsBusyAndCanDoNothingElse(GameData data) {
        var (world, guest, _) = Hosted(data);
        var told = new List<string>();
        world.Relay += (_, message) => told.Add(message);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        StepInside(world, hut);
        world.Doorways.TurnKey();
        world.As(guest, () => {
            world.Player = Doorways.DoorPoint(hut);
            Require(world.Doorways.Enter(hut, false) && guest.Door == Doorway.Knocking, "a player at the locked door of a cabin someone is in should knock on it");
            var stone = world.Add(ThingKind.LooseStone, world.Player + new Vector2(.5f, 0), 1);
            int stones = world.Backpack["stone"];
            for (int i = 0; i < 4; i++) world.Gather(stone, .25f);
            Require(world.Backpack["stone"] == stones && world.Find(stone.Id) != null, "a player knocking on a cabin door should not pick anything up");
            SetNeeds(world, hunger: 20); Give(world, "berries", 3);
            world.Consume("berries");
            Require(world.Backpack["berries"] == 3, "a player knocking on a cabin door should not eat");
            Give(world, data.Buildings[ThingKind.Chest].Cost);
            Require(world.Construction.Build(ThingKind.Chest, world.Player + new Vector2(-3, 2)) == null, "a player knocking on a cabin door should not build");
        });
        Require(told.Count > 0 && told.All(message => message == "You are busy."), $"a player at a cabin door should be told they are busy, not {string.Join(" | ", told)}");
    }
    private static void CheckAPlayerInsideACabinCanStillEat(GameData data) {
        var world = EmptyWorld(data);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        StepInside(world, hut);
        SetNeeds(world, hunger: 20); Give(world, "berries", 3);
        world.Consume("berries");
        Require(world.Backpack["berries"] == 2, "a player inside a cabin should still eat");
    }
    private static void CheckOnlyAPlayerKnockingOnTheDoorGoesInWhenSomeoneStepsOut(GameData data) {
        var (world, guest, _) = Hosted(data);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        StepInside(world, hut);
        // The guest stands at the door without ever clicking the cabin.
        world.As(guest, () => world.Player = Doorways.DoorPoint(hut));
        world.Doorways.Leave();
        Require(guest.Door == Doorway.None && guest.Pos == Doorways.DoorPoint(hut), $"a player standing at a cabin's door without knocking on it should be left where they are, not {guest.Door}");
        Require(world.Player.DistanceTo(guest.Pos) >= Doorways.ClearOf, $"someone stepping out should end clear of a player standing at the door, not {world.Player.DistanceTo(guest.Pos):0.00} m from them");
        world.Tick(Doorways.LeaveSeconds + .05f);
        Require(guest.Sheltered == 0, "a player who never clicked a cabin should not end up inside it");
    }
    private static void CheckAPlayerRespawnsInTheNearestCabinWithAPlaceLeft(GameData data) {
        var (world, guest, _) = Hosted(data);
        var third = world.Arrive("third", 3);
        var full = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -6));
        var locked = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -14));
        world.Add(ThingKind.Hut, world.Player + new Vector2(0, -22));
        StepInside(world, full, guest);
        StepInside(world, locked, third);
        world.As(third, () => world.Doorways.TurnKey());
        world.Player = full.Pos + new Vector2(4, 0);
        SetNeeds(world, health: 0);
        SafeTick(world, data.RespawnSeconds + 1);
        Require(world.Sheltered == full.Id, "a player should respawn in the nearest cabin, which has a place left beside the one inside");
        world.Doorways.Leave();
        world.Tick(Doorways.LeaveSeconds + .05f);
        StepInside(world, full, world.Arrive("fourth", 4));
        world.Player = full.Pos + new Vector2(4, 0);
        SetNeeds(world, health: 0);
        SafeTick(world, data.RespawnSeconds + 1);
        Require(world.Sheltered == locked.Id && locked.Locked, "with the nearest cabin full, a player should respawn in the next one with a place left, which stays locked");
    }
    private static void CheckAPlayerDoesNotRespawnInACabinSomeoneIsTuggedOutOfTheDoorOf(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var near = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -6));
        var far = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -14));
        // Someone inside leaves the near cabin one place, which the guest at its door takes.
        StepInside(world, near, world.Arrive("third", 3));
        world.Player = near.Pos + new Vector2(4, 0);
        SetNeeds(world, health: 0);
        world.Tick(.05f);
        Require(world.Dead, "a player with no health left should fall");
        for (int i = 0; i < 4000 && world.RespawnIn > .9f; i++) { world.Tick(.05f); NourishEveryone(world); }
        // The guest, who came to a door after the fallen player last did, is tugged out of the near cabin's doorway as the fallen player wakes.
        world.As(guest, () => { world.Player = Doorways.DoorPoint(near); Require(world.Doorways.Enter(near, false), "the guest at the door should step in"); });
        PlaceWolf(world, new Wolf { Pos = guest.Pos + new Vector2(.8f, 0), Target = guest.Id });
        for (int i = 0; i < 14 && guest.Door == Doorway.Entering; i++) { world.Tick(.05f); NourishEveryone(world); }
        for (int i = 0; i < 20 && world.Dead; i++) { world.Tick(.05f); NourishEveryone(world); }
        Require(!world.Dead && guest.Door == Doorway.Tugged, $"the fallen player should wake while the guest is tugged out, not with the guest {guest.Door}");
        Require(world.Sheltered == far.Id, "a player should not respawn in a cabin someone is being tugged out of the door of");
    }
    private static void CheckACabinCannotBeBuiltWithItsDoorOnGroundNobodyCanStandOn(GameData data) {
        var world = EmptyWorld(data);
        var shore = ShoreForACabin(world);
        world.Player = shore + new Vector2(-2.5f, -1.5f);
        Give(world, data.Buildings[ThingKind.Hut].Cost);
        Require(world.Construction.PlacementBlock(ThingKind.Hut, shore) != null && world.Construction.Build(ThingKind.Hut, shore) == null, "a cabin should not go up with its door where nobody can stand");
    }
    private static void CheckNothingCanBeBuiltOnACabinsDoorSpot(GameData data) {
        var world = EmptyWorld(data);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(0, -6));
        // A bench just in front of the door, clear of the cabin itself.
        var spot = Doorways.DoorPoint(hut) + Doorways.OutOfDoor * .6f;
        world.Player = spot + new Vector2(2.5f, 0);
        Give(world, data.Buildings[ThingKind.Bench].Cost);
        Require(world.Construction.PlacementBlock(ThingKind.Bench, spot + Doorways.OutOfDoor * 2) == null, "a bench well clear of the cabin's door should go up");
        Require(world.Construction.PlacementBlock(ThingKind.Bench, spot) != null && world.Construction.Build(ThingKind.Bench, spot) == null, "a bench should not go up on a cabin's door spot");
    }
    private static void CheckSteppingOutOfACabinWhoseDoorIsBlockedLandsOnOpenGround(GameData data) {
        var world = EmptyWorld(data);
        // A cabin from an older save, built before its door had to be clear.
        var hut = world.Add(ThingKind.Hut, ShoreForACabin(world));
        SetSheltered(world, hut);
        world.Doorways.Leave();
        world.Tick(Doorways.LeaveSeconds + .05f);
        Require(world.Sheltered == 0 && world.CanStand(world.Player), $"a player stepping out of a cabin whose door is blocked should land on open ground, not at {world.Player}");
    }
}
