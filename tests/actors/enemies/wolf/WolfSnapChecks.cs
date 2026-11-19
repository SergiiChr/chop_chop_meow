using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WolfSteps;
using static WorldSteps;

/// <summary>Checks for a wolf's bite: gathering itself, leaping to where the player is heading and snapping, and how a player gets out of it.</summary>
public static class WolfSnapChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfGathersItselfBeforeItBites, data);
        Test(CheckAPlayerWhoKeepsTheirWayIsBitten, data);
        Test(CheckASprintingPlayerWhoTurnsAsTheWolfLeapsIsMissed, data);
        Test(CheckAWalkingPlayerWhoTurnsIsStillBitten, data);
        Test(CheckABlowThatLandsFirstStopsTheBite, data);
    }

    private static void CheckAWolfGathersItselfBeforeItBites(GameData data) {
        var world = NightWorld(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.05f, 0) });
        var heard = Listen(world);
        SnapTick(world, .25f);
        Require(world.Health == 100 && WolfSnap.Winding(wolf), "a wolf at biting distance should gather itself for 0.3 seconds before it bites");
        SnapTick(world, .2f);
        Require(world.Health == 100 - data.WolfBite && heard.Contains("wolf_bite"), $"a wolf should bite a player who stands still as it lands, 0.38 seconds in: health {world.Health}");
        SnapTick(world, .5f);
        Require(world.Health == 100 - data.WolfBite, "a wolf should wait 0.6 seconds after a snap before it starts the next");
        SnapTick(world, .6f);
        Require(world.Health == 100 - 2 * data.WolfBite, "a wolf should bite again about a second after its last bite");
    }

    private static void CheckAPlayerWhoKeepsTheirWayIsBitten(GameData data) {
        foreach (bool sprinting in new[] { false, true })
            Require(HealthAfterASnap(data, sprinting, turns: false) == 100 - data.WolfBite, $"a wolf should leap to where a player {(sprinting ? "sprinting" : "walking")} straight on is heading and bite them");
    }

    private static void CheckASprintingPlayerWhoTurnsAsTheWolfLeapsIsMissed(GameData data) =>
        Require(HealthAfterASnap(data, sprinting: true, turns: true) == 100, "a sprinting player who turns aside as the wolf leaps should be missed");

    private static void CheckAWalkingPlayerWhoTurnsIsStillBitten(GameData data) =>
        Require(HealthAfterASnap(data, sprinting: false, turns: true) == 100 - data.WolfBite, "a walking player who turns aside as the wolf leaps should still be within reach of its jaws");

    /// <summary>A blow knocks a wolf back and leaves it reeling, so one timed to land while the wolf gathers itself costs it the bite.</summary>
    private static void CheckABlowThatLandsFirstStopsTheBite(GameData data) {
        var world = AxeNight(data);
        // The wolf starts its snap 0.15 seconds in, and the axe swung now comes down 0.4 seconds in, before the wolf leaps.
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(1.05f, 0), Attack = .15f });
        world.Attack(Vector2.Right);
        SnapTick(world, .3f);
        Require(WolfSnap.Winding(wolf), "the wolf should be gathering itself as the axe comes down");
        SnapTick(world, .5f);
        Require(world.Health == 100 && wolf.Health < data.WolfHealth, $"a blow that lands while a wolf gathers itself should stop its bite: health {world.Health}");
    }
}
