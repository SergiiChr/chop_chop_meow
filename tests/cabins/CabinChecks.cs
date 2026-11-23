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
        Test(CheckPlayerWalksAroundABenchThatOnlyClipsTheWayToTheCabin, data);
        Test(CheckASealedCabinSearchCanBeCancelledWhileItIsRunning, data);
        Test(CheckRemovingAWallWhileTheCabinSearchRunsOpensTheWayIn, data);
        Test(CheckARotatedCabinHasItsDoorOnTheTurnedSide, data);
        Test(CheckARotatedCabinKeepsItsDoorAndLadderClear, data);
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
        Test(CheckSleepingPassesTheNightForTheWorld, data);
        Test(CheckSleepNeedsFoodAndWater, data);
        Test(CheckSleepHealsOnlyWhileFed, data);
        Test(CheckCabinShelterAndChasingWolfGivesUpInLight, data);
        Test(CheckSleepWaitsForEveryone, data);
        Test(CheckCabinOnlyAcceptsNewHaulDutiesWithABarrelMounted, data);
        Test(CheckBootsStandOnACabinsStepsAndTheBodyClimbsThemWithoutJumps);
    }

    private static void CheckCabinOnlyAcceptsNewHaulDutiesWithABarrelMounted(GameData data) {
        var world = EmptyWorld(data);
        var cabin = world.Add(ThingKind.Hut, world.Player + Vector2.Down * 4);
        var worker = AddWorker(world, world.Player);
        world.SetDuty(worker, cabin, JobKind.Haul, true);
        Require(!World.HasDuty(worker, cabin.Id, JobKind.Haul), "a cabin with no roof barrel cannot take a new hauling duty");

        Give(world, Barrel.Item);
        Require(world.Transfer(world.Backpack, cabin.Inventory, Barrel.Item, 1) == 1, "the player mounts a barrel on the roof");
        world.SetDuty(worker, cabin, JobKind.Haul, true);
        Require(World.HasDuty(worker, cabin.Id, JobKind.Haul), "an empty barrel on the roof is enough to assign a hauling duty");

        Require(world.Transfer(cabin.Inventory, world.Backpack, Barrel.Item, 1) == 1, "the player takes the barrel back");
        world.SetDuty(worker, cabin, JobKind.Haul, false);
        Require(!World.HasDuty(worker, cabin.Id, JobKind.Haul), "the worker can give up an existing duty even after the barrel comes off");
    }

    private static void CheckPlayerBehindACabinWalksRoundItToTheDoor(GameData data) {
        var world = EmptyWorld(data);
        var hut = world.Add(ThingKind.Hut, new Vector2(20, 20));
        // The door faces +Y, so this is straight behind the cabin.
        world.Player = hut.Pos + new Vector2(0, -3);
        float widest = 0;
        var approach = new CabinApproach(world);
        approach.Start(hut);
        for (int i = 0; i < 600 && approach.Direction() != Vector2.Zero; i++) {
            var before = world.Player;
            approach.Move(data.MoveSpeed(false) / 60);
            Require(world.Player != before, $"the walk to the door should never stall against the cabin, at {world.Player}");
            widest = Math.Max(widest, Math.Abs(world.Player.X - hut.Pos.X));
        }
        Require(world.Player.DistanceTo(Doorways.DoorPoint(hut)) < .2f, $"the walk should end at the cabin's door, not at {world.Player}");
        Require(widest > 1.5f, "the walk should go round the side of the cabin");
        Require(world.Door == Doorway.Entering, "a player at the door should step in");
    }
    private static void CheckPlayerWalksAroundABenchThatOnlyClipsTheWayToTheCabin(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn;
        world.Add(ThingKind.Bench, at);
        var cabin = world.Add(ThingKind.Hut, at + new Vector2(1, -7));
        // The line to the door crosses a thin sliver of the bench's round footprint.
        world.Player = at + new Vector2(.5982241f, .0672975f);
        var approach = new CabinApproach(world);
        approach.Start(cabin);
        for (int i = 0; i < 240 && approach.Direction() != Vector2.Zero; i++) approach.Move(.07f);
        Require(world.Door == Doorway.Entering, "the player should detour around the edge of the bench and enter the cabin");
    }

    private static void CheckASealedCabinSearchCanBeCancelledWhileItIsRunning(GameData data) {
        var world = EmptyWorld(data);
        var at = Terrain.Spawn;
        var center = at + Vector2.Up * 12;
        var cabin = world.Add(ThingKind.Hut, center);
        for (int i = 0; i < 12; i++) world.Add(ThingKind.Hut, center + Vector2.FromAngle(i * Mathf.Tau / 12) * 5);
        for (int y = 3; y <= 23; y += 2)
            for (int x = -15; x <= 15; x += 2) world.Add(ThingKind.Bench, at + new Vector2(x, y));
        Require(world.CanStand(Doorways.DoorPoint(cabin)), "inside the ring, the door itself should be open");
        var approach = new CabinApproach(world);
        approach.Start(cabin);
        Require(approach.Direction() == Vector2.Zero && approach.Active, "a wide search should yield with cabin entry active");
        Require(approach.Cancel(), "cancel should stop an in-progress route search");
        approach.Start(cabin);
        for (int frame = 0; frame < 200 && approach.Active; frame++)
            Require(approach.Direction() == Vector2.Zero, "no route should cross the walls around the cabin");
        Require(!approach.Active && world.Player == at, "the full search should release the player with the door unreachable");
    }

    private static void CheckRemovingAWallWhileTheCabinSearchRunsOpensTheWayIn(GameData data) {
        var world = EmptyWorld(data);
        var center = Terrain.Spawn + Vector2.Up * 12;
        var cabin = world.Add(ThingKind.Hut, center);
        var wall = world.Add(ThingKind.Hut, center + Vector2.Down * 5);
        for (int i = 0; i < 12; i++)
            if (i != 3) world.Add(ThingKind.Hut, center + Vector2.FromAngle(i * Mathf.Tau / 12) * 5);
        var approach = new CabinApproach(world);
        approach.Start(cabin);
        Require(approach.Direction() == Vector2.Zero && approach.Active, "the cabin search should be in progress when the wall comes down");
        RemoveThings(world, t => t == wall);
        for (int frame = 0; frame < 500 && approach.Active; frame++)
            if (approach.Direction() != Vector2.Zero) approach.Move(.1f);
        Require(world.Door == Doorway.Entering, "the pending approach should take the opening where the wall was removed");
    }

    private static void CheckARotatedCabinHasItsDoorOnTheTurnedSide(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(24, 20);
        Give(world, data.Buildings[ThingKind.Hut].Cost);
        var hut = world.Construction.Build(ThingKind.Hut, new Vector2(20, 20), Mathf.Pi / 2);
        Require(hut != null, "a quarter-turned cabin should go up on open ground");
        var doorstep = new Vector2(18, 19.66f);
        world.Player = doorstep;
        Require(world.Doorways.Enter(hut!, false), "a player by the rotated door should step in");
        world.Tick(Doorways.EnterSeconds + .05f);
        world.Doorways.Leave();
        world.Tick(Doorways.LeaveSeconds + .05f);
        Require(world.Sheltered == 0 && world.Player.DistanceTo(doorstep) < .01f, "the player should step out through the rotated door");
        var save = world.Serialize();
        Require(world.Construction.Dismantle(hut!), "the cabin should come down after the player steps outside");
        world.Deserialize(save);
        world.Player = doorstep;
        Require(world.Find(hut!.Id) is Thing loaded && world.Doorways.Enter(loaded, false), "loading should put the door back on the same side");
    }

    private static void CheckARotatedCabinKeepsItsDoorAndLadderClear(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(23, 22);
        Give(world, data.Buildings[ThingKind.Hut].Cost);
        Give(world, data.Buildings[ThingKind.Bench].Cost);
        Require(world.Construction.Build(ThingKind.Hut, new Vector2(20, 20), Mathf.Pi / 2) != null, "the rotated cabin should go up");
        world.Player = new Vector2(16, 22);
        Require(world.Construction.PlacementBlock(ThingKind.Bench, new Vector2(17.2f, 19.66f)) == "Leave the cabin's door clear.", "a bench should leave room in front of the rotated door");
        Require(world.Construction.PlacementBlock(ThingKind.Bench, new Vector2(18.9f, 22.7f)) == "Leave the cabin's ladder clear.", "a bench should leave room at the rotated ladder");
        Require(world.Construction.PlacementBlock(ThingKind.Bench, new Vector2(19.66f, 23)) == null, "the ground in front of the unrotated door should be free");
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
        WolfSteps.SnapTick(world, .64f);
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

    private static void CheckSleepingPassesTheNightForTheWorld(GameData data) {
        var world = EmptyWorld(data);
        world.Player = Terrain.Spawn;
        RunUntilPhase(world, data.NightStarts + .01f);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0));
        var sign = world.Add(ThingKind.Signpost, world.Player + new Vector2(1, 2));
        var furnace = world.Add(ThingKind.Furnace, world.Player + new Vector2(-2, 1));
        furnace.Processing = true;
        var order = new TradeOrder { Goods = new() { ["wood"] = 1 }, Buying = false, Value = 12, Signpost = sign.Id };
        PlaceOrder(world, order);
        var worker = AddWorker(world, sign.Pos);
        SetCoins(world, (int)worker.Wage + 3);
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(8, 0) });
        float priorTime = world.Time;
        float priorHunger = world.Hunger;
        world.Sleep(hut);
        Require(world.Time > priorTime && world.Day > 1,
                "sleep should advance the world clock through dawn");
        Require(world.Orders.Count == 0, "trade settlement should finish the order");
        Require(sign.Wages![Inventory.Coins] == 12 - worker.Wage,
            "the daily wage should come out of the wage slot during sleep");
        Require(world.Coins == worker.Wage + 3, "the worker's wage should reach the player");
        Require(worker.WageDebt == 0,
                "a funded worker should receive the normal daily wage during sleep");
        Require(!furnace.Processing && furnace.Inventory["iron_bar"] > 0,
                "furnace production should continue while the player sleeps");
        Require(world.Wolves.Count == 0 && world.Hunger < priorHunger,
                "nighttime sleep should advance survival needs and clear wolves at dawn");
    }

    private static void CheckSleepNeedsFoodAndWater(GameData data) {
        Require(Math.Abs(100 / data.HungerPerSecond - data.DaySeconds) < .01f && Math.Abs(100 / data.ThirstPerSecond - data.DaySeconds) < .01f,
                "a full stomach should last one in-game day and a full water droplet one day");
        foreach (var (hunger, thirst, reason) in new[] { (10f, 100f, "hungry"), (100f, 10f, "thirsty") }) {
            var world = NightWorld(data);
            var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0));
            SetNeeds(world, hunger, thirst);
            float time = world.Time;
            world.Sleep(hut);
            Require(world.Time == time && world.SleepBlock()?.Contains(reason) == true,
                    $"sleep must be refused when the player is too {reason} to last until morning");
        }
        var early = EmptyWorld(data);
        early.Add(ThingKind.Hut, early.Player + new Vector2(2, 0));
        RunUntilPhase(early, (World.SleepHour - 6 - .2f) / 24);
        Require(early.SleepBlock()?.Contains("10 pm") == true, "sleep must be refused before 10 pm");
        RunUntilPhase(early, (World.SleepHour - 6 + .2f) / 24);
        Require(!early.Night && early.SleepBlock() == null, "sleep must be allowed from 10 pm, before night falls");
    }

    /// <summary>Health comes back only in sleep, while hunger and thirst both stay above 45%, and the cabin foretells what a night leaves.</summary>
    private static void CheckSleepHealsOnlyWhileFed(GameData data) {
        var awake = EmptyWorld(data);
        SetNeeds(awake, 100, 100, 50);
        SafeTick(awake, 60);
        Require(awake.Health == 50, "a fed player should not heal while awake");

        var rested = NightWorld(data);
        var hut = rested.Add(ThingKind.Hut, rested.Player + new Vector2(2, 0));
        SetNeeds(rested, 100, 100, 50);
        float foretold = rested.HealthAtDawn();
        rested.Sleep(hut);
        // At most eight hours of sleep, 280 seconds, heal .12 a second: a fed night brings back up to 34.
        Require(rested.Health > 60 && rested.Health < 84, $"a fed night's sleep should heal a good part of the way: {rested.Health:0.0}");
        Require(Math.Abs(rested.Health - foretold) < .2f, $"the cabin's dawn health {foretold:0.0} should be what sleep leaves, {rested.Health:0.0}");

        var hungry = NightWorld(data);
        hut = hungry.Add(ThingKind.Hut, hungry.Player + new Vector2(2, 0));
        SetNeeds(hungry, 50, 100, 50);
        hungry.Sleep(hut);
        // Hunger falls from 50 to 45 in 5% of a day, 42 seconds, which heal 5.
        Require(Math.Abs(hungry.Health - 55) < .3f, $"sleep should heal only while hunger stays above 45%: {hungry.Health:0.0}");
    }

    private static void CheckCabinShelterAndChasingWolfGivesUpInLight(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(2, 0));
        var wolf = new Wolf { Pos = world.Player + new Vector2(-4, 0) };
        PlaceWolf(world, wolf);
        SafeTick(world, .5f);
        StepInside(world, hut);
        world.Doorways.TurnKey();
        SetNeeds(world, health: 40);
        SafeTick(world, 1);
        Require(wolf.Target == 0, "a wolf must stop hunting a player who is inside a cabin");
        Require(wolf.Leap <= 0, "a wolf must stop its leap when the player is inside a cabin");
        Require(world.Health >= 40, "a wolf must not hurt a player inside a cabin");
        var inside = world.Player;
        world.MovePlayer(new Vector2(0, .1f));
        Require(world.Sheltered == hut.Id && world.Player == inside, "moving should not take the player back outside");
        // A chaser that caught up stands right on the player; it must still give up once it has scratched at the locked door.
        var caught = new Wolf { Pos = world.Player, Target = world.Me.Id };
        PlaceWolf(world, caught);
        SafeTick(world, 10);
        Require(caught.Target == 0 && wolf.Target == 0 && caught.Mode == WolfMode.Patrol && caught.DoorCheck == DoorCheckStage.None && wolf.DoorCheck == DoorCheckStage.None, "wolves should give up on the player and patrol on once they have been inside behind a locked door long enough");
        Require(world.Health >= 40, "a wolf must not hurt a player behind a locked door");
        world.Doorways.Leave();
        Require(world.Sheltered == 0, "stepping outside should leave the cabin");
        world.Player = hut.Pos + new Vector2(12, 0);
        SetNeeds(world, health: -1);
        for (int i = 0; i < 200 && world.Sheltered == 0; i++) { world.Tick(.25f); SetNeeds(world, health: Math.Max(world.Health, 1)); }
        Require(world.Sheltered == hut.Id, "a fallen player should wake inside the nearest cabin");
        Require(world.Player.DistanceTo(hut.Pos) < 3, "a fallen player should wake beside that cabin");
        Require(world.CanStand(world.Player), "a fallen player should wake on standable ground");

        var lit = NightWorld(data);
        var chaser = new Wolf { Pos = lit.Player + new Vector2(-5, 0) };
        PlaceWolf(lit, chaser);
        SafeTick(lit, .5f);
        lit.Add(ThingKind.Campfire, lit.Player + new Vector2(2, 0)).Burn = 300;
        float gap = chaser.Pos.DistanceTo(lit.Player);
        SafeTick(lit, .5f);
        Require(chaser.Pos.DistanceTo(lit.Player) < gap, "a wolf already chasing should follow the player briefly into firelight");
        SafeTick(lit, 4);
        gap = chaser.Pos.DistanceTo(lit.Player);
        SafeTick(lit, .5f);
        Require(chaser.Pos.DistanceTo(lit.Player) > gap, "a chasing wolf should give up and back away after a few seconds in the light");
    }

    private static void CheckSleepWaitsForEveryone(GameData data) {
        var (world, guest, _) = Hosted(data);
        RunUntilNight(world);
        var hut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(3, 0));
        StepInside(world, hut);
        Require(world.SleepBlock() == "Everyone has to be inside a cabin to sleep.", $"sleeping should wait for a guest still outside: {world.SleepBlock()}");
        // The guest sleeps in a cabin of their own, and both lock their doors against the night's wolves.
        var guestHut = world.Add(ThingKind.Hut, Terrain.Spawn + new Vector2(3, 6));
        StepInside(world, guestHut, guest);
        world.Doorways.TurnKey();
        world.Run(guest, new Command(Verb.TurnKey, 0, 0, "", 0, default));
        Require(hut.Locked && guestHut.Locked, "each player should lock the door of the cabin they are in");
        SetNeeds(guest, hunger: 1);
        Require(world.SleepBlock() == "Someone is too hungry or thirsty to sleep.", "sleeping should wait for a hungry guest");
        SetNeeds(guest, hunger: 100);
        AddWorker(world, Terrain.Spawn).Wage = 10;
        Give(world, Inventory.Coins, 50); Give(guest, Inventory.Coins, 50);
        int day = world.Day;
        world.Run(guest, new Command(Verb.Sleep, guestHut.Id, 0, "", 0, default));
        Require(world.Day == day + 1, "with everyone inside, a guest's sleep should move to the next day");
        Require(!world.Night, "with everyone inside, a guest's sleep should end the night");
        Require(guest.Hunger < 100, "with everyone inside, a guest's sleep should use up the guest's hunger");
        Require(world.Backpack[Inventory.Coins] == 40 && guest.Backpack[Inventory.Coins] == 50, "the host's backpack should pay the wages even when a guest sleeps");
    }

    /// <summary>Frames a second the steps check goes through, as the game's recordings show them.</summary>
    private const int Fps = 60;
    /// <summary>How near the ground a boot is that a pose has standing on it, in metres.</summary>
    private const float OnTheGround = .001f;

    /// <summary>
    /// A cabin's door has two treads 14 cm high and then the floor, 40, 62 and 80 cm in from where a player stands at it,
    /// as the cabin model builds them; a player walks, runs and is tugged over them and knocks on the door from the lower one.
    /// No boot sinks into them, a boot the pose puts on the ground stands on them, and the body takes at least three frames
    /// to climb or come down a tread's height.
    /// </summary>
    private static void CheckBootsStandOnACabinsStepsAndTheBodyClimbsThemWithoutJumps() {
        ReadOnlySpan<float> edges = stackalloc float[] { .4f, .62f, .8f };
        const float rise = .14f;
        // How far in from the door point the hips go, facing in or out, over how many seconds, whether the lowest boot must stay on the
        // ground, and whether a boot the pose stands on the ground may hang over a tread it is dragged off while the body falls onto it.
        foreach (var (action, from, to, facingIn, seconds, grounded, dragged) in new[] {
            (ActorAction.Walk, 0f, 1.8f, true, 1.5f, true, false), (ActorAction.Walk, 1.8f, 0f, false, 1.5f, true, false), (ActorAction.Jog, 0f, 1.8f, true, .7f, false, false),
            (ActorAction.Walk, .1f, .5f, true, .3f, true, false), (ActorAction.Knock, .5f, .5f, true, 1f, true, false), (ActorAction.Tugged, .7f, -.3f, true, 1.5f, true, true) }) {
            var up = new Vector2(0, facingIn ? 1 : -1);
            float deepest = 0, highest = 0, hovering = 0, lift = 0, speed = 0, top = float.NaN, jump = 0;
            int frames = (int)(seconds * Fps);
            for (int i = 0; i <= frames; i++) {
                float depth = Mathf.Lerp(from, to, i / (float)frames), travelled = Math.Abs(to - from) * i / frames, time = seconds * i / frames;
                var pose = ActorAnimation.PoseAt(action, ActorAnimation.Stride(action) > 0 ? ActorAnimation.Advance(action, 0, travelled) : time, from != to);
                var flat = pose;
                pose = StairSteps.OnSteps(pose, depth, up, facingIn != to > from, edges, rise, ref lift, ref speed, i == 0 ? StairSteps.Settle : 1f / Fps);
                if (!float.IsNaN(top)) jump = Math.Max(jump, Math.Abs(lift + pose.HipHeight - top));
                top = lift + pose.HipHeight;
                float lowest = float.MaxValue;
                for (int side = 0; side < 2; side++) {
                    var (toe, heel) = side == 0 ? ActorAnimation.SoleOf(pose.SwingL, pose.KneeL, pose.FootL) : ActorAnimation.SoleOf(pose.SwingR, pose.KneeR, pose.FootR);
                    var (flatToe, flatHeel) = side == 0 ? ActorAnimation.SoleOf(flat.SwingL, flat.KneeL, flat.FootL) : ActorAnimation.SoleOf(flat.SwingR, flat.KneeR, flat.FootR);
                    float boot = float.MaxValue;
                    foreach (var point in new[] { toe, heel }) {
                        // Facing out, a point ahead of the hips is further out from the door.
                        float clear = lift + pose.HipHeight - point.Y - StairSteps.StepGround(depth + up.Y * point.X, edges, rise);
                        deepest = Math.Min(deepest, clear); boot = Math.Min(boot, clear);
                    }
                    lowest = Math.Min(lowest, boot);
                    if (!dragged && flat.HipHeight - Math.Max(flatToe.Y, flatHeel.Y) < OnTheGround) hovering = Math.Max(hovering, boot);
                }
                if (grounded) highest = Math.Max(highest, lowest);
            }
            Require(deepest > -.003f, $"{action} from {from} m to {to} m in from a cabin's door should never sink a boot into its steps: {deepest:0.000} m");
            Require(highest < .01f, $"{action} from {from} m to {to} m in from a cabin's door should keep a boot on the steps: the lowest rose {highest:0.000} m off them");
            Require(hovering < .01f, $"{action} from {from} m to {to} m in from a cabin's door should stand a boot on the ground on the steps: one hung {hovering:0.000} m over them");
            Require(jump < rise / 3, $"{action} from {from} m to {to} m in from a cabin's door should take three frames or more over a tread's height: the body moved {jump:0.000} m in one");
        }
    }
}
