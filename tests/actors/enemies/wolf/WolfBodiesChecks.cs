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

/// <summary>Checks of wolves at a player keeping their distance from them and their bodies apart.</summary>
public static class WolfBodiesChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfClosingInStopsAtBitingDistanceRatherThanOnThePlayer, data);
        Test(CheckAWolfClosingInKeepsOffWhereAnotherTugsThePlayer, data);
        Test(CheckTwoWolvesAtOnePlayerKeepTheirBodiesApart, data);
    }

    private static void CheckAWolfClosingInStopsAtBitingDistanceRatherThanOnThePlayer(GameData data) {
        var world = NightWorld(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3, 0) });
        float nearest = float.MaxValue;
        for (int i = 0; i < 60; i++) { world.Tick(.05f); nearest = Math.Min(nearest, wolf.Pos.DistanceTo(world.Player)); }
        Require(world.Health < 100, "a wolf closing in should still bite a player standing still");
        Require(nearest > .8f, $"a wolf should stop at biting distance rather than stand on the player, not come {nearest:0.00} m from them");
    }
    private static void CheckAWolfClosingInKeepsOffWhereAnotherTugsThePlayer(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        world.Tick(.05f);
        Require(world.Door == Doorway.Tugged, "a wolf should tug the player out of the doorway");
        // A second wolf waits just past where the tug ends, its last bite still fresh.
        var other = PlaceWolf(world, new Wolf { Pos = Doorways.DoorPoint(hut) + Doorways.OutOfDoor * 2, Attack = 5 });
        float nearest = float.MaxValue;
        for (float time = 0; time < DoorTug.TugSeconds - .1f; time += .05f) {
            world.Tick(.05f);
            nearest = Math.Min(nearest, other.Pos.DistanceTo(Doorways.DoorPoint(hut) + Doorways.OutOfDoor * (DoorTug.TugDistance * DoorTug.TugProgress(world.Me.DoorTime / DoorTug.TugSeconds))));
        }
        Require(nearest > .8f, $"a wolf closing in should keep off where another wolf has tugged the player, not come {nearest:0.00} m from them");
    }
    private static void CheckTwoWolvesAtOnePlayerKeepTheirBodiesApart(GameData data) {
        var world = NightWorld(data);
        // Both close in on the player's right side, nearly along the same line.
        var first = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3, 0), Attack = 5 });
        var second = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3.5f, .3f), Attack = 5 });
        for (int i = 0; i < 60; i++) world.Tick(.05f);
        var a = WolfBodies.Body(first, world.Player); var b = WolfBodies.Body(second, world.Player);
        float gap = float.MaxValue;
        // Sample points along each body, nose to tail.
        for (int i = 0; i <= 20; i++) for (int j = 0; j <= 20; j++) gap = Math.Min(gap, a.Tail.Lerp(a.Nose, i / 20f).DistanceTo(b.Tail.Lerp(b.Nose, j / 20f)));
        Require(gap >= 2 * WolfBodies.WolfHalfWidth - .01f, $"two wolves at one player should keep their bodies apart, not come {gap:0.00} m from each other");
    }
}
