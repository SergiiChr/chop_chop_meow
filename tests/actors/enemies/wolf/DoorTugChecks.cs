using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChopChopMeow;
using Godot;
using static Checks;
using static WolfSteps;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;
using static WorkerSteps;
using static CabinSteps;

/// <summary>Checks of a wolf tugging a player back out of a cabin's doorway, letting go, and taking a badly hurt one down.</summary>
public static class DoorTugChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfTugsAPlayerOutOfTheDoorwayAndLeavesThemOutside, data);
        Test(CheckAWoundedPlayerTuggedOutOfTheDoorwayIsPinned, data);
        Test(CheckOnlyTheTuggingWolfTakesDownAWoundedPlayerItTugs, data);
        Test(CheckAWolfLetsGoOfAPlayerWhoLeavesTheGameWhileItTugsThem, data);
        Test(CheckHittingAWolfThatTugsAPlayerOutKnocksItBack, data);
        Test(CheckAWolfTugsAPlayerStandingAsideAtACabinDoorOffIt, data);
        Test(CheckAWolfCanBiteAPlayerOnTheKnockingStep, data);
        Test(CheckAFollowerStepsIntoTheCabinFromWhereTheyWaitedAside, data);
        Test(CheckAWolfTugsAPlayerOutOnlyAsFarAsOpenGroundGoes, data);
        Test(CheckASecondWolfTakesATuggedPlayersSideBitesOnceAndHoldsUntilTheTugEnds, data);
        Test(CheckOnlyOneWolfTakesEachSideOfATuggedPlayer, data);
        Test(CheckAWolfWhoseLastBiteIsFreshTakesATuggedPlayersArmAtOnceAndBitesWhenItCan, data);
        Test(CheckAWolfWithNoArmToTakeWaitsBesideATuggedPlayerAndSnapsOnceTheyAreLetGo, data);
        Test(CheckABodysLeftAndRightFollowFromTheWayItFaces);
        Test(CheckAWolfWithNoRoomBehindAPlayerItTuggedOutPinsThemFromTheSideItStandsOn, data);
    }

    /// <summary>On the map, north is -Y and east is +X: facing north, a body's left hand is to the west.</summary>
    private static void CheckABodysLeftAndRightFollowFromTheWayItFaces() {
        Vector2 north = new(0, -1), east = new(1, 0), at = new(10, 10);
        Require(BodySide.Out(north, BodySide.Left).IsEqualApprox(new Vector2(-1, 0)) && BodySide.Out(north, BodySide.Right).IsEqualApprox(east), $"a body facing north should have west on its left and east on its right, not {BodySide.Out(north, BodySide.Left)}");
        Require(BodySide.Out(east, BodySide.Left).IsEqualApprox(north), $"a body facing east should have north on its left, not {BodySide.Out(east, BodySide.Left)}");
        Require(BodySide.Of(at, north, at + new Vector2(-2, 5)) == BodySide.Left && BodySide.Of(at, north, at + new Vector2(.1f, -3)) == BodySide.Right, "a spot to the west of a body facing north should be on its left and one to the east on its right, however far ahead or behind");
        Require(BodySide.Free(at, north, at + east, leftTaken: false, rightTaken: true) == BodySide.Left, "something on a body's right should take its left when the right is taken");
        Require(BodySide.Free(at, north, at + east, leftTaken: true, rightTaken: true) == 0, "nothing should take a side of a body with both taken");
    }

    /// <summary>
    /// With a bench half a metre in front of the door, the wolf has no ground behind the player and stays at the side it
    /// came from. The player faces the door, up the map, so east is to their right and west to their left.
    /// </summary>
    private static void CheckAWolfWithNoRoomBehindAPlayerItTuggedOutPinsThemFromTheSideItStandsOn(GameData data) {
        foreach (int east in new[] { 1, -1 }) {
            var world = NightWorld(data);
            var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
            world.Player = Doorways.DoorPoint(hut);
            world.Add(ThingKind.Bench, world.Player + Doorways.OutOfDoor * 1.08f);
            SetNeeds(world, health: 45);
            var wolf = StepIntoBite(world, hut, new Vector2(east * .8f, 0));
            for (float time = 0; time < DoorTug.TugSeconds + .7f && !world.Mauled; time += .05f) world.Tick(.05f);
            Require(world.Mauled && wolf.Pinning, "a wounded player tugged out should be pinned by the wolf that tugged them");
            Require(wolf.LungeFrom.X * east > .9f, $"a wolf standing to the {(east > 0 ? "east" : "west")} of the player it tugged should pin them from that side, not from {wolf.LungeFrom}");
        }
    }

    /// <summary>Each bite takes 15 health: the tugging wolf's and the one a second wolf gives as it takes the player's arm leave 70 of 100.</summary>
    private static void CheckASecondWolfTakesATuggedPlayersSideBitesOnceAndHoldsUntilTheTugEnds(GameData data) {
        var (world, _, tugger) = TuggedAtNight(data);
        var heard = Listen(world);
        var second = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-2.5f, 1) });
        Require(world.Door == Doorway.Tugged && tugger.Tugging && world.Health == 85, $"the nearer wolf should bite the player and tug them out, not leave them {world.Door} at {world.Health} health");
        for (float time = 0; time < .8f && second.Side == 0; time += .05f) world.Tick(.05f);
        Require(second.Side == 1 && world.Health == 70, $"a second wolf reaching a tugged player from their left should take that side with one bite, not side {second.Side} at {world.Health} health");
        Require(heard.Count(name => name == "wolf_bite") == 1, $"the second wolf should be heard biting once: heard {string.Join(", ", heard)}");
        while (world.Me.DoorTime < DoorTug.TugSeconds - .1f) world.Tick(.05f);
        Require(world.Door == Doorway.Tugged && second.Side == 1 && world.Health == 70, $"a wolf at a tugged player's side should hold on without biting again, not side {second.Side} at {world.Health} health");
        Require(second.Pos.DistanceTo(world.DoorTug.TuggedTo) is > .6f and < 1, $"a wolf holding a tugged player's arm should stand at their side, not {second.Pos.DistanceTo(world.DoorTug.TuggedTo):0.00} m from them");
        world.Tick(.15f);
        Require(second.Side == 0 && !tugger.Tugging && world.Door == Doorway.None, $"the wolves should let go as the tug ends, not side {second.Side} with the player {world.Door}");
    }

    /// <summary>Each bite takes 15 health. The second wolf bit .6 s before it takes the arm, and bites again a second after that bite.</summary>
    private static void CheckAWolfWhoseLastBiteIsFreshTakesATuggedPlayersArmAtOnceAndBitesWhenItCan(GameData data) {
        var (world, _, _) = TuggedAtNight(data);
        var second = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-1, .2f), Attack = .35f });
        world.Tick(.05f);
        Require(world.Door == Doorway.Tugged && second.Side == 1 && world.Health == 85, $"a wolf whose last bite is fresh should take a tugged player's arm at once without biting, not side {second.Side} at {world.Health} health");
        for (float time = 0; time < .2f; time += .05f) world.Tick(.05f);
        Require(second.Side == 1 && world.Health == 85, $"a wolf holding an arm should not bite before a second has passed since its last bite, not leave {world.Health} health");
        for (float time = 0; time < .25f; time += .05f) world.Tick(.05f);
        Require(second.Side == 1 && world.Health == 70, $"a wolf holding an arm should bite as soon as a second has passed since its last bite, not leave {world.Health} health");
        while (world.Me.DoorTime < DoorTug.TugSeconds - .1f) world.Tick(.05f);
        Require(second.Side == 1 && world.Health == 70, $"a wolf holding an arm should bite only once, not leave {world.Health} health");
    }

    /// <summary>
    /// Each bite takes 15 health: the tugging wolf's and one at each arm leave 55 of 100.
    /// Let go, the player is left standing outside, where the waiting wolf's snap takes 15 more off one who stays put.
    /// </summary>
    private static void CheckAWolfWithNoArmToTakeWaitsBesideATuggedPlayerAndSnapsOnceTheyAreLetGo(GameData data) {
        var (world, hut, _) = TuggedAtNight(data);
        foreach (int side in new[] { -1, 1 }) PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(side, .2f) });
        var waiting = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.3f, 2.2f) });
        while (world.Me.DoorTime < DoorTug.TugSeconds - .1f) world.Tick(.05f);
        Require(world.Door == Doorway.Tugged && waiting.Side == 0 && world.Health == 55, $"a wolf with no arm to take should not bite a tugged player, who should have 55 health, not {world.Health}");
        Require(waiting.Pos.DistanceTo(world.DoorTug.TuggedTo) < 2, $"a wolf with no arm to take should wait beside the tugged player, not {waiting.Pos.DistanceTo(world.DoorTug.TuggedTo):0.00} m off");
        bool snapped = false;
        for (float time = 0; time < 1; time += .05f) { world.Tick(.05f); snapped |= waiting.Snap > 0; }
        Require(snapped, "the waiting wolf should snap at the player once they are let go");
        Require(world.Health <= 40 && world.Sheltered == 0, $"the waiting wolf's snap should land on a player who stays where they were let go, outside, not leave them {(world.Sheltered == hut.Id ? "inside" : "outside")} at {world.Health} health");
    }

    private static void CheckOnlyOneWolfTakesEachSideOfATuggedPlayer(GameData data) {
        var (world, _, _) = TuggedAtNight(data);
        var others = new[] { new Vector2(-2.5f, 1), new Vector2(2.5f, 1), new Vector2(0, 3) }.Select(from => PlaceWolf(world, new Wolf { Pos = world.Player + from })).ToArray();
        world.Tick(.05f);
        for (float time = 0; time < 1; time += .05f) world.Tick(.05f);
        Require(world.Door == Doorway.Tugged, $"the player should still be tugged a second in, not {world.Door}");
        Require(others.Count(w => w.Side == 1) == 1 && others.Count(w => w.Side == -1) == 1, $"one wolf should hold each side of a tugged player: sides {string.Join(", ", others.Select(w => w.Side))}");
        // The player faces the door, up the map, so their left is toward -X: each wolf holds the side it came from and stands on it.
        Require(others[0].Side == 1 && others[0].Pos.X < world.DoorTug.TuggedTo.X - .5f, $"the wolf that came from the player's left should hold their left side and stand there, not side {others[0].Side} at {others[0].Pos - world.DoorTug.TuggedTo}");
        Require(others[1].Side == -1 && others[1].Pos.X > world.DoorTug.TuggedTo.X + .5f, $"the wolf that came from the player's right should hold their right side and stand there, not side {others[1].Side} at {others[1].Pos - world.DoorTug.TuggedTo}");
    }

    /// <summary>A wolf bite takes 15 health, which leaves a healthy player above half.</summary>
    private static void CheckAWolfTugsAPlayerOutOfTheDoorwayAndLeavesThemOutside(GameData data) {
        var world = NightWorld(data);
        var heard = Listen(world);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        StepIntoBite(world, hut, Vector2.Right * .8f);
        UntilTugged(world);
        Require(heard.Count(name => name == "tug_scream") == 1, $"a player should scream as a wolf grabs them: heard {string.Join(", ", heard)}");
        Require(world.Door == Doorway.Tugged && world.Health == 85, $"a wolf biting a player in the first half of stepping in should tug them out, not {world.Door} at {world.Health} health");
        var wolf = world.Wolves[0];
        world.Tick(1.4f);
        Require(world.Door == Doorway.Tugged && wolf.Tugging, "a wolf should hold a player it tugs out of a doorway for a second and a half");
        world.Tick(.1f);
        Require(heard.Count(name => name == "tug_scream") == 1, $"a tugged player should scream once, through the whole tug: heard {string.Join(", ", heard)}");
        Require(world.Door == Doorway.None && !wolf.Tugging, $"a player tugged out should be let go and left standing free, not {world.Door}");
        Require(world.Player.DistanceTo(Doorways.DoorPoint(hut) + new Vector2(0, 1)) < .01f, $"a wolf should tug a player a metre out from the door, not to {world.Player - Doorways.DoorPoint(hut)}");
        world.Tick(Doorways.RushSeconds);
        Require(world.Sheltered == 0 && world.Door == Doorway.None && !world.Mauled, $"a player let go should stay outside until they go for the cabin themselves, not {world.Door}");
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false) && world.Door == Doorway.Entering, $"a player let go should step in once they are back at the door and go for the cabin, not {world.Door}");
    }
    private static void CheckAWoundedPlayerTuggedOutOfTheDoorwayIsPinned(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        SetNeeds(world, health: 45);
        var heard = Listen(world);
        StepIntoBite(world, hut, Vector2.Right * 2);
        UntilTugged(world);
        Require(world.Door == Doorway.Tugged && !world.Mauled, $"a wolf leaping at a wounded player stepping in should first tug them out, not {world.Door}");
        world.Tick(1.15f);
        Require(world.Door == Doorway.Tugged && !world.Mauled, "a wounded player should be tugged out through all three tugs before being pinned");
        Require(!heard.Contains("takedown_scream"), "a player should not scream at a takedown before it");
        // The third tug, done 1.2 s in, goes straight into the takedown.
        world.Tick(.1f);
        Require(world.Mauled && world.Sheltered == 0 && world.Player.DistanceTo(Doorways.DoorPoint(hut) + new Vector2(0, 1)) < .01f, "a wounded player tugged a metre out should then be pinned");
        Require(world.Door == Doorway.None, $"a player pinned outside a cabin should be out of its doorway, not {world.Door}");
        Require(heard.Contains("takedown_scream"), $"a player taken down should scream: heard {string.Join(", ", heard)}");
    }
    private static void CheckOnlyTheTuggingWolfTakesDownAWoundedPlayerItTugs(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        SetNeeds(world, health: 60);
        var tugger = StepIntoBite(world, hut, Vector2.Right * .8f);
        UntilTugged(world);
        var other = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-2.5f, 0) });
        Require(world.Door == Doorway.Tugged && world.Health <= 50, $"a bite should leave the player tugged out below half health, not {world.Door} at {world.Health}");
        for (float time = .05f; time < 1.15f; time += .05f) {
            world.Tick(.05f);
            Require(!world.Mauled && other.Leap == 0, $"no wolf should take down a player another wolf is tugging out, {time:0.00} s in");
        }
        world.Tick(.1f);
        Require(world.Mauled && tugger.Pinning && !tugger.Tugging, "the wolf tugging a wounded player out should take them down once they are out");
        float across = Math.Abs((tugger.Pos - world.Player).Dot(Doorways.OutOfDoor.Orthogonal()));
        Require(across > .5f && tugger.LungeFrom != Vector2.Zero, $"the wolf should step aside to pin a player it tugged out from beside them, not stand {across:0.00} m to the side");
    }
    private static void CheckAWolfLetsGoOfAPlayerWhoLeavesTheGameWhileItTugsThem(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.As(guest, () => world.Player = Doorways.DoorPoint(hut));
        var tugger = StepIntoBite(world, hut, Vector2.Right * .8f, guest);
        UntilTugged(world, guest);
        Require(guest.Door == Doorway.Tugged && tugger.Tugging, "a wolf should tug the guest out of the doorway");
        world.Depart(guest.Id);
        world.Tick(.05f);
        Require(!tugger.Tugging, "a wolf tugging a player out should let go of them once they leave the game");
    }
    private static void CheckHittingAWolfThatTugsAPlayerOutKnocksItBack(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        SetNeeds(world, health: 45);
        var tugger = StepIntoBite(world, hut, Vector2.Right * 2);
        UntilTugged(world);
        Require(world.Door == Doorway.Tugged && tugger.Tugging, $"a wolf should tug the wounded player out of the doorway, not {world.Door}");
        world.Tick(.4f);
        Give(guest, "steel_axe", 1);
        world.As(guest, () => { world.Player = tugger.Pos + new Vector2(1, 0); world.Attack(Vector2.Left); });
        world.Tick(.45f);
        Require(!tugger.Tugging && world.Door == Doorway.None && world.Sheltered == 0 && !world.Mauled, $"a wolf hit while it tugs a player out should let go and leave them standing free outside, not {world.Door}");
    }
    private static void CheckAWolfTugsAPlayerStandingAsideAtACabinDoorOffIt(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        StepInside(world, hut);
        var other = world.Arrive("other", 3);
        StepInside(world, hut, other);
        world.Doorways.TurnKey();
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut); Require(world.Doorways.Enter(hut, false) && guest.Door == Doorway.Knocking, "the guest should knock on the locked cabin's door"); });
        world.Doorways.Leave();
        Require(guest.Door == Doorway.Aside, "the guest should stand aside for the host stepping out");
        SnapTick(world, .46f);
        world.As(other, () => world.Doorways.Leave());
        var wolf = PlaceWolf(world, new Wolf { Pos = guest.Pos + new Vector2(.8f, 0), Target = guest.Id });
        UntilTugged(world, guest);
        Require(guest.Door == Doorway.TuggedOff && wolf.Tugging, $"a wolf should tug a player standing aside at the door off it, not leave them {guest.Door}");
    }
    private static void CheckAWolfCanBiteAPlayerOnTheKnockingStep(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        StepInside(world, hut);
        world.Doorways.TurnKey();
        var door = Doorways.DoorPoint(hut);
        world.As(guest, () => {
            world.Player = door;
            Require(world.Doorways.Enter(hut, false) && guest.Door == Doorway.Knocking, "the guest should knock at the locked door");
        });
        SnapTick(world, .52f);
        var wolf = PlaceWolf(world, new Wolf { Pos = door + Doorways.OutOfDoor * .87f, Target = guest.Id });
        UntilTugged(world, guest);
        Require(guest.Door == Doorway.TuggedOff && guest.Health < 100 && wolf.Tugging,
            "a bite should catch the exposed guest standing on the doorstep");
    }

    private static void CheckAFollowerStepsIntoTheCabinFromWhereTheyWaitedAside(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        var door = Doorways.DoorPoint(hut);
        world.Player = door;
        StepIntoBite(world, hut, Vector2.Right * .8f);
        world.As(guest, () => {
            world.Player = door + Vector2.Left * .12f;
            Require(world.Doorways.Enter(hut, false) && guest.Door == Doorway.Following, "the guest should follow the host");
        });
        UntilTugged(world);
        SnapTick(world, .65f);
        Require(guest.Door == Doorway.Following && Math.Abs(guest.DoorBody.X - door.X) > .6f,
            "the guest should wait off the path of a tugged player");
        var waited = guest.DoorBody;
        for (int i = 0; i < 24 && guest.Door == Doorway.Following; i++) world.Tick(.05f);
        Require(guest.Door == Doorway.Entering && guest.DoorFrom.DistanceTo(waited) < .1f,
            "the guest should step in from the side where they were waiting");
    }

    private static void CheckAWolfTugsAPlayerOutOnlyAsFarAsOpenGroundGoes(GameData data) {
        var world = NightWorld(data);
        // Water starts half a metre in front of the door.
        var hut = world.Add(ThingKind.Hut, ShoreForACabin(world, .5f));
        world.Player = Doorways.DoorPoint(hut);
        var wolf = StepIntoBite(world, hut, Vector2.Right * .8f);
        UntilTugged(world);
        Require(world.Door == Doorway.Tugged, $"a wolf should tug the player out of the doorway, not {world.Door}");
        for (float time = 0; time < DoorTug.TugSeconds + .1f && world.Door == Doorway.Tugged; time += .05f) {
            world.Tick(.05f);
            Require(world.CanStand(wolf.Pos), $"a wolf tugging a player out should stand on open ground, not at {wolf.Pos - Doorways.DoorPoint(hut)} from the door");
        }
        Require(world.CanStand(world.Player), $"a player tugged out should end on ground they can stand on, not at {world.Player - Doorways.DoorPoint(hut)} from the door");
    }
}
