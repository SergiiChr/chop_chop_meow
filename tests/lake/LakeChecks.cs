using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of the lake: water from anywhere on its shore, and its shallows, where people wade but nothing is worked from.</summary>
public static class LakeChecks {
    public static void Run(GameData data) {
        Test(CheckEveryPartOfTheShoreFillsABottleOrQuenchesThirst, data);
        Test(CheckEveryoneWadesTwoMetresIntoTheLake, data);
        Test(CheckBuildingsAreWorkedFromDryGroundOnly, data);
    }

    private static void CheckEveryPartOfTheShoreFillsABottleOrQuenchesThirst(GameData data) {
        for (int side = 0; side < 24; side++) {
            float angle = side * MathF.Tau / 24;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var world = new World(data, false);
            var lake = world.Add(ThingKind.Water, Terrain.WaterPoint(Terrain.Spawn), 9999);
            world.Player = Terrain.WaterPoint(Terrain.Lake + direction * 30) + direction;
            Give(world, "empty_bottle", 1);
            Require(world.Near(lake) && Terrain.WaterRing(world.Player), "Every part of the lake shore must collect water.");
            world.Gather(lake, data.GatherSeconds / 2);
            Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 0, "A bottle must take the whole gathering time to fill.");
            world.Gather(lake, data.GatherSeconds / 2);
            Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1 && world.Backpack.Count == 1, "Collecting must fill the existing bottle exactly once.");
            DropVessels(world, Water.Empty, Water.Liquid, Water.Capacity); SetNeeds(world, thirst: 0);
            world.Gather(lake, data.GatherSeconds / 2);
            Require(world.Thirst == 0, "Direct drinking must take as long as filling a bottle.");
            world.Gather(lake, data.GatherSeconds / 2);
            Require(Math.Abs(world.Thirst - Water.LiterThirst) <= .01 && world.Backpack.Count == 0, "Direct drinking must grant one liter and no bottle.");
        }
        Require(!Terrain.WaterRing(Terrain.Lake) && !Terrain.WaterRing(Terrain.Spawn), "The collection ring must not cover the lake center or distant ground.");
    }

    private static void CheckEveryoneWadesTwoMetresIntoTheLake(GameData data) {
        var world = EmptyWorld(data);
        foreach (float angle in new[] { 0f, 1.7f, 3.4f, 5.1f }) {
            var way = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float shore = Terrain.Shore(angle);
            Require(world.CanStand(Terrain.Lake + way * (shore - Terrain.Wade + .1f)), $"anyone should stand in the shallows {Terrain.Wade} m in from the shore");
            Require(!world.CanStand(Terrain.Lake + way * (shore - Terrain.Wade - .1f)), $"nobody should wade further than {Terrain.Wade} m in");
        }
    }

    private static void CheckBuildingsAreWorkedFromDryGroundOnly(GameData data) {
        var world = EmptyWorld(data);
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var intake = (Vector2)Terrain.TileAt(bank + Vector2.Down * 1.1f);
        world.Player = intake + new Vector2(0, -3);
        Give(world, "steel_bar", 4); Give(world, "stone", 10); Give(world, "wood_parts", 4);
        // Turned along the shore, the pump's handle comes in the shallows, where people wade.
        var handle = intake + World.BellowsStand.Rotated(Mathf.Pi / 2);
        Require(world.CanStand(handle) && Terrain.InLake(handle), "a handle turned along the shore should come in the shallows");
        Require(world.Construction.PlacementBlock(ThingKind.Pump, intake, rotation: Mathf.Pi / 2) == "Turn the handle toward open ground on the bank.",
            "a pump's handle should be worked from dry ground, not the shallows");
        // A furnace on dry ground just up the bank, turned with its bellows toward the lake.
        var furnace = bank + Vector2.Up;
        world.Player = furnace + new Vector2(3, -1);
        var bellows = furnace + World.BellowsStand.Rotated(Mathf.Pi);
        Require(world.CanStand(bellows) && Terrain.InLake(bellows), "a furnace's bellows turned toward the lake should come in the shallows");
        Require(world.Construction.PlacementBlock(ThingKind.Reinforced, furnace, rotation: Mathf.Pi) == "Leave dry, open ground at the furnace's bellows.",
            "a furnace's bellows should be worked from dry ground, not the shallows");
    }
}
