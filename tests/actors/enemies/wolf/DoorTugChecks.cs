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

/// <summary>Checks of a wolf tugging a player back out of a cabin's doorway, letting go, and taking a badly hurt one down.</summary>
public static class DoorTugChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfTugsAPlayerOutOfTheDoorwayAndTheyRunBackIn, data);
        Test(CheckAWoundedPlayerTuggedOutOfTheDoorwayIsPinned, data);
        Test(CheckPlayersTuggedOutOfACabinDoorGoBackInInTheOrderTheyCame, data);
        Test(CheckOnlyTheTuggingWolfTakesDownAWoundedPlayerItTugs, data);
        Test(CheckAWolfLetsGoOfAPlayerWhoLeavesTheGameWhileItTugsThem, data);
        Test(CheckHittingAWolfThatTugsAPlayerOutKnocksItBack, data);
        Test(CheckAWolfTugsAPlayerStandingAsideAtACabinDoorOffIt, data);
        Test(CheckAWolfTugsAPlayerOutOnlyAsFarAsOpenGroundGoes, data);
        Test(CheckASecondWolfTakesATuggedPlayersSideBitesOnceAndHoldsUntilTheTugEnds, data);
        Test(CheckOnlyOneWolfTakesEachSideOfATuggedPlayer, data);
    }

    /// <summary>Each bite takes 15 health: the tugging wolf's and the one a second wolf gives as it takes the player's arm leave 70 of 100.</summary>
    private static void CheckASecondWolfTakesATuggedPlayersSideBitesOnceAndHoldsUntilTheTugEnds(GameData data) {
        var world = NightWorld(data);
        var heard = Listen(world);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        var tugger = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        var second = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-2.5f, 1) });
        world.Tick(.05f);
        Require(world.Door == Doorway.Tugged && tugger.Tugging && world.Health == 85, $"the nearer wolf should bite the player and tug them out, not leave them {world.Door} at {world.Health} health");
        for (float time = 0; time < .8f && second.Side == 0; time += .05f) world.Tick(.05f);
        Require(second.Side == 1 && world.Health == 70, $"a second wolf reaching a tugged player from their left should take that side with one bite, not side {second.Side} at {world.Health} health");
        Require(heard.Count(name => name == "wolf_bite") == 2, $"each wolf taking hold should be heard biting once: heard {string.Join(", ", heard)}");
        while (world.Me.DoorTime < DoorTug.TugSeconds - .1f) world.Tick(.05f);
        Require(world.Door == Doorway.Tugged && second.Side == 1 && world.Health == 70, $"a wolf at a tugged player's side should hold on without biting again, not side {second.Side} at {world.Health} health");
        Require(second.Pos.DistanceTo(world.DoorTug.TuggedTo) is > .6f and < 1, $"a wolf holding a tugged player's arm should stand at their side, not {second.Pos.DistanceTo(world.DoorTug.TuggedTo):0.00} m from them");
        world.Tick(.15f);
        Require(second.Side == 0 && !tugger.Tugging && world.Door == Doorway.Entering, $"the wolves should let go as the tug ends, not side {second.Side} with the player {world.Door}");
    }

    private static void CheckOnlyOneWolfTakesEachSideOfATuggedPlayer(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        var others = new[] { new Vector2(-2.5f, 1), new Vector2(2.5f, 1), new Vector2(0, 3) }.Select(from => PlaceWolf(world, new Wolf { Pos = world.Player + from })).ToArray();
        world.Tick(.05f);
        for (float time = 0; time < 1; time += .05f) world.Tick(.05f);
        Require(world.Door == Doorway.Tugged, $"the player should still be tugged a second in, not {world.Door}");
        Require(others.Count(w => w.Side == 1) == 1 && others.Count(w => w.Side == -1) == 1, $"one wolf should hold each side of a tugged player: sides {string.Join(", ", others.Select(w => w.Side))}");
    }

    /// <summary>A wolf bite takes 15 health, which leaves a healthy player above half.</summary>
    private static void CheckAWolfTugsAPlayerOutOfTheDoorwayAndTheyRunBackIn(GameData data) {
        var world = NightWorld(data);
        var heard = Listen(world);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        for (float time = 0; time < .6f && world.Door == Doorway.Entering; time += .05f) world.Tick(.05f);
        Require(heard.Count(name => name == "tug_scream") == 1, $"a player should scream as a wolf grabs them: heard {string.Join(", ", heard)}");
        Require(world.Door == Doorway.Tugged && world.Health == 85, $"a wolf biting a player in the first half of stepping in should tug them out, not {world.Door} at {world.Health} health");
        var wolf = world.Wolves[0];
        world.Tick(1.4f);
        Require(world.Door == Doorway.Tugged && wolf.Tugging, "a wolf should hold a player it tugs out of a doorway for a second and a half");
        world.Tick(.1f);
        Require(heard.Count(name => name == "tug_scream") == 1, $"a tugged player should scream once, through the whole tug: heard {string.Join(", ", heard)}");
        Require(world.Door == Doorway.Entering && world.Me.DoorRush && !wolf.Tugging, "a player tugged out should be let go and step in again at a run");
        Require(world.Player.DistanceTo(Doorways.DoorPoint(hut) + new Vector2(0, 1)) < .01f, $"a wolf should tug a player a metre out from the door, not to {world.Player - Doorways.DoorPoint(hut)}");
        world.Tick(Doorways.RushSeconds);
        Require(world.Sheltered == hut.Id && !world.Mauled, "a player tugged out above half health should get inside");
    }
    private static void CheckAWoundedPlayerTuggedOutOfTheDoorwayIsPinned(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        SetNeeds(world, health: 45);
        var heard = Listen(world);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(2, 0) });
        for (float time = 0; time < .7f && world.Door == Doorway.Entering; time += .05f) world.Tick(.05f);
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
    private static void CheckPlayersTuggedOutOfACabinDoorGoBackInInTheOrderTheyCame(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "the host at the door should step in");
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut) + new Vector2(.3f, 0); Require(world.Doorways.Enter(hut, false), "the guest at the door should follow the host"); });
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-.8f, 0) });
        PlaceWolf(world, new Wolf { Pos = guest.Pos + new Vector2(.8f, 0), Target = guest.Id });
        world.Tick(.05f);
        Require(world.Door == Doorway.Tugged && guest.Door == Doorway.Tugged, $"wolves should tug both players out of the doorway, not {world.Door} and {guest.Door}");
        for (float time = 0; time < DoorTug.TugSeconds + .05f; time += .05f) world.Tick(.05f);
        Require(world.Door == Doorway.Entering && guest.Door == Doorway.Following, $"players tugged out of a cabin's door should go back in in the order they came, not {world.Door} and {guest.Door}");
    }
    private static void CheckOnlyTheTuggingWolfTakesDownAWoundedPlayerItTugs(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        SetNeeds(world, health: 60);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        var tugger = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        var other = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(-2.5f, 0) });
        world.Tick(.05f);
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
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut); Require(world.Doorways.Enter(hut, false), "the guest at the door should step in"); });
        var tugger = PlaceWolf(world, new Wolf { Pos = guest.Pos + new Vector2(.8f, 0), Target = guest.Id });
        world.Tick(.05f);
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
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        var tugger = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(2, 0) });
        for (float time = 0; time < .6f && world.Door == Doorway.Entering; time += .05f) world.Tick(.05f);
        Require(world.Door == Doorway.Tugged && tugger.Tugging, $"a wolf should tug the wounded player out of the doorway, not {world.Door}");
        world.Tick(.4f);
        Give(guest, "steel_axe", 1);
        world.As(guest, () => { world.Player = tugger.Pos + new Vector2(1, 0); world.Attack(); });
        world.Tick(.05f);
        Require(!tugger.Tugging && world.Door == Doorway.Entering && !world.Mauled, $"a wolf hit while it tugs a player out should let go, and the player should run back in, not {world.Door}");
    }
    private static void CheckAWolfTugsAPlayerStandingAsideAtACabinDoorOffIt(GameData data) {
        var world = NightWorld(data);
        var guest = world.Arrive("guest", 2);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        StepInside(world, hut);
        world.Doorways.TurnKey();
        world.As(guest, () => { world.Player = Doorways.DoorPoint(hut); Require(world.Doorways.Enter(hut, false) && guest.Door == Doorway.Knocking, "the guest should knock on the locked cabin's door"); });
        world.Doorways.Leave();
        Require(guest.Door == Doorway.Aside, "the guest should stand aside for the host stepping out");
        var wolf = PlaceWolf(world, new Wolf { Pos = guest.Pos + new Vector2(.8f, 0), Target = guest.Id });
        world.Tick(.05f);
        Require(guest.Door == Doorway.TuggedOff && wolf.Tugging, $"a wolf should tug a player standing aside at the door off it, not leave them {guest.Door}");
    }
    private static void CheckAWolfTugsAPlayerOutOnlyAsFarAsOpenGroundGoes(GameData data) {
        var world = NightWorld(data);
        // Water starts half a metre in front of the door.
        var hut = world.Add(ThingKind.Hut, ShoreForACabin(world, .5f));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        for (float time = 0; time < .6f && world.Door == Doorway.Entering; time += .05f) world.Tick(.05f);
        Require(world.Door == Doorway.Tugged, $"a wolf should tug the player out of the doorway, not {world.Door}");
        for (float time = 0; time < DoorTug.TugSeconds + .1f && world.Door == Doorway.Tugged; time += .05f) {
            world.Tick(.05f);
            Require(world.CanStand(wolf.Pos), $"a wolf tugging a player out should stand on open ground, not at {wolf.Pos - Doorways.DoorPoint(hut)} from the door");
        }
        Require(world.CanStand(world.Player), $"a player tugged out should end on ground they can stand on, not at {world.Player - Doorways.DoorPoint(hut)} from the door");
    }
}
