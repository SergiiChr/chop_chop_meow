using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Checks of building: where a building goes, what it costs, and dismantling it.</summary>
public static class BuildingChecks {
    public static void Run(GameData data) {
        Test(CheckDismantlingABuildingReturnsItsCostAndContents, data);
        Test(CheckBuildingsKnockIntoPlaceAndClatterDown, data);
        Test(CheckDismantlingWhatDoesNotFitLeavesItInABox, data);
        Test(CheckBuildingPlacementTakesANearbyClearTileAndSpendsItsCost, data);
    }

    private static void CheckDismantlingABuildingReturnsItsCostAndContents(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        Give(world, "wood", 8); Give(world, "stone", 30);
        var chest = world.Construction.Build(ThingKind.Chest, world.Player + new Vector2(2, 0))!;
        world.Transfer(world.Backpack, chest.Inventory, "stone", 30);
        Require(world.Backpack["wood"] == 0 && world.Backpack["stone"] == 0, "building the chest and filling it should empty the backpack");
        Require(world.Construction.Dismantle(chest), "the player should dismantle a chest in reach");
        Require(world.Find(chest.Id) == null, "a dismantled chest should be gone");
        Require(world.Backpack["wood"] == 8 && world.Backpack["stone"] == 30, "dismantling a chest should return its 8 wood and the 30 stone in it");
        Require(!world.Things.Any(t => t.Kind == ThingKind.Grave), "with room in the backpack nothing should go into a box");

        Give(world, "wood", 12); Give(world, "stone", 8);
        var cabin = world.Construction.Build(ThingKind.Hut, world.Player + new Vector2(-4, 0))!;
        StepInside(world, cabin);
        Require(!world.Construction.Dismantle(cabin) && world.Find(cabin.Id) != null, "a cabin someone shelters in should not come down");
    }

    private static void CheckBuildingsKnockIntoPlaceAndClatterDown(GameData data) {
        var world = EmptyWorld(data);
        var heard = Listen(world);
        Give(world, data.Buildings[ThingKind.Chest].Cost);
        var chest = BuildAt(world, ThingKind.Chest, new Vector2(22, 20));
        Require(heard.SequenceEqual(new[] { "place_building" }), "a building should knock into place as it goes up");
        Give(world, data.Buildings[ThingKind.Campfire].Cost);
        heard.Clear(); BuildAt(world, ThingKind.Campfire, new Vector2(17, 23));
        Require(heard.SequenceEqual(new[] { "place_building", "start_fire" }), "a campfire should knock into place and catch fire");
        world.Player = new Vector2(20, 20);
        heard.Clear(); world.Construction.Dismantle(chest);
        Require(heard.SequenceEqual(new[] { "take_down_building" }), "a building taken down should clatter apart");
    }

    private static void CheckDismantlingWhatDoesNotFitLeavesItInABox(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        Give(world, "planks", 10);
        var bench = world.Construction.Build(ThingKind.Bench, world.Player + new Vector2(2, 0))!;
        Give(world, "stone", 24 * 64);
        Require(world.Backpack.Room("planks") == 0, "the fixture should fill the backpack");
        Require(world.Construction.Dismantle(bench), "the player should dismantle a bench in reach");
        var box = world.Things.Single(t => t.Kind == ThingKind.Grave);
        Require(box.Inventory["planks"] == 10 && box.Pos == bench.Pos, "the planks that do not fit should wait in a recovery box where the bench stood");
    }

    private static void CheckBuildingPlacementTakesANearbyClearTileAndSpendsItsCost(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 8);
        Require(world.Construction.Build(ThingKind.Chest, Terrain.Spawn) == null,
                "building placement must reject the player arrival tile");
        var chest = world.Construction.Build(ThingKind.Chest, Terrain.Spawn + new Vector2(2, 0));
        Require(chest != null, "building placement should accept a nearby clear tile");
        Require(chest.Kind == ThingKind.Chest, "building placement should build the chosen kind");
        Require(world.Backpack["wood"] == 0, "building placement should spend its cost");
    }
}
