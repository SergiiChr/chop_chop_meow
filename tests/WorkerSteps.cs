using ChopChopMeow;
using Godot;
using static WorldSteps;

/// <summary>Adding workers to a world for the simulation checks.</summary>
internal static class WorkerSteps {
    /// <summary>Adds a worker directly, without a hiring offer or its payment.</summary>
    public static Worker AddWorker(World world, Vector2 position) {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Worker {world.S.Workers.Count + 1}", Pos = position };
        world.S.Workers.Add(worker);
        return worker;
    }

    public static void Hire(World world, Thing target) => world.Assign(AddWorker(world, target.Pos + new Vector2(1, 1)), target);
}
