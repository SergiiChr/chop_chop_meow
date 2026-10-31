using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

public static class PondChecks {
    public static void Run(GameData data) {
        for (int side = 0; side < 24; side++) {
            float angle = side * MathF.Tau / 24;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var world = new World(data, false);
            var pond = world.Add(ThingKind.Water, Terrain.WaterPoint(Terrain.Spawn), 9999);
            world.Player = Terrain.WaterPoint(Terrain.Lake + direction * 30) + direction;
            Give(world, "empty_bottle", 1);
            Require(world.Near(pond) && Terrain.WaterRing(world.Player), "Every part of the pond shore must collect water.");
            world.Gather(pond, data.GatherSeconds / 2);
            Require(world.Backpack["water"] == 0, "A bottle must take the whole gathering time to fill.");
            world.Gather(pond, data.GatherSeconds / 2);
            Require(world.Backpack["water"] == 1 && world.Backpack.Count == 1, "Collecting must fill the existing bottle exactly once.");
            Drop(world, "water", 1); SetNeeds(world, thirst: 0);
            world.Gather(pond, data.GatherSeconds / 2);
            Require(world.Thirst == 0, "Direct drinking must take as long as filling a bottle.");
            world.Gather(pond, data.GatherSeconds / 2);
            Require(Math.Abs(world.Thirst - Water.SipThirst) <= .01 && world.Backpack.Count == 0, "Direct drinking must grant one sip and no bottle.");
        }
        Require(!Terrain.WaterRing(Terrain.Lake) && !Terrain.WaterRing(Terrain.Spawn), "The collection ring must not cover the lake center or distant ground.");
    }
}
