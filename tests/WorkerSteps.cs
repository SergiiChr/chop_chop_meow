using ChopChopMeow;
using Godot;

/// <summary>Adding workers to a world for the simulation checks.</summary>
internal static class WorkerSteps {
    /// <summary>Adds a worker directly at a spot: hiring needs a signpost and coins, starts them at the post and counts as the hiring goal.</summary>
    public static Worker AddWorker(World world, Vector2 position) {
        var worker = new Worker { Id = world.S.NextId++, Name = $"Worker {world.Workers.Count + 1}", Pos = position };
        world.S.Workers.Add(worker);
        return worker;
    }

    public static void Hire(World world, Thing target) => world.Assign(AddWorker(world, target.Pos + new Vector2(1, 1)), target);
}
