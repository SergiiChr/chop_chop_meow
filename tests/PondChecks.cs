using System;
using ChopChopMeow;
using Godot;

public static class PondChecks {
    public static void Run(GameData data) {
        for (int side = 0; side < 24; side++) {
            float angle = side * MathF.Tau / 24;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var world = new World(data, false);
            var pond = world.Add("water", Terrain.WaterPoint(World.Spawn), 9999);
            world.Player = Terrain.WaterPoint(Terrain.Lake + direction * 30) + direction;
            world.S.Backpack.Add("empty_bottle", 1);
            if (!world.Near(pond) || !Terrain.WaterRing(world.Player)) throw new Exception("Every part of the pond shore must collect water.");
            world.Gather(pond, data.GatherSeconds / 2);
            if (world.S.Backpack["water"] != 0) throw new Exception("A bottle must take the whole gathering time to fill.");
            world.Gather(pond, data.GatherSeconds / 2);
            if (world.S.Backpack["water"] != 1 || world.S.Backpack.Count != 1) throw new Exception("Collecting must fill the existing bottle exactly once.");
            world.S.Backpack.Remove("water", 1); world.S.Thirst = 0;
            world.Gather(pond, data.GatherSeconds / 2);
            if (world.S.Thirst != 0) throw new Exception("Direct drinking must take as long as filling a bottle.");
            world.Gather(pond, data.GatherSeconds / 2);
            if (Math.Abs(world.S.Thirst - Water.SipThirst) > .01 || world.S.Backpack.Count != 0) throw new Exception("Direct drinking must grant one sip and no bottle.");
        }
        if (Terrain.WaterRing(Terrain.Lake) || Terrain.WaterRing(World.Spawn)) throw new Exception("The collection ring must not cover the lake center or distant ground.");
    }
}
