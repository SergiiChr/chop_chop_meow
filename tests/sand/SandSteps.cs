using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class SandSteps {
    public static readonly Vector2 SandSquare = new(166, 170);

    public static void EquipBeachShovel(World world) {
        Give(world, "steel_shovel");
        world.SelectInHand("steel_shovel");
        world.Player = SandSquare + Vector2.Left;
    }

    public static void DigSand(World world, int attempt) {
        Require(world.Holes.Begin(SandSquare, attempt), "the shovel should start digging the dry sand square");
        world.Tick(6);
        Require(world.Holes.Finish(attempt), "the completed shovel work should strip the square");
    }

    public static void RunBeachClock(World world, float time) {
        while (world.Time < time) {
            NourishEveryone(world);
            world.Tick(Math.Min(15, time - world.Time), true);
        }
    }
}
