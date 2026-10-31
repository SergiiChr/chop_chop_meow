using ChopChopMeow;
using static SetupSteps;
using Godot;

/// <summary>Adding workers to a world for the simulation checks.</summary>
internal static class WorkerSteps {
    public static void Hire(World world, Thing target) => world.Assign(AddWorker(world, target.Pos + new Vector2(1, 1)), target);
}
