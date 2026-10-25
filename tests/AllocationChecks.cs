using System;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>
/// Holds code that runs every tick to its allocation budget: every byte it allocates is garbage the runtime collects later,
/// in a pause that stalls a frame. Counts the calling thread's bytes, so it holds the same on any machine.
/// </summary>
public static class AllocationChecks
{
    public static void Run(GameData data)
    {
        var valley = new World(data);
        float angle = 0;
        // A small circle the warm-up walks once, so the measured laps cross ground already seen: seeing something for the first time is a rare event that may allocate.
        Check("tick: fresh valley, player walking", () =>
        {
            angle += .11f;
            valley.MovePlayer(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * .07f);
            valley.Tick(1 / 60f);
            Fed(valley);
        });

        var settlement = Settlement(data);
        // Twenty seconds first, so every worker has been through its trips once and the status and cost caches hold what they will show.
        // A store taking an item it has not held before grows its table once; that rare event is the one byte per tick allowed here (measured 0.2 to 0.5).
        Check("tick: working settlement", () => { settlement.Tick(1 / 60f); Fed(settlement); }, maxBytesPerRun: 1, warmup: 1200);

        var night = new World(data);
        night.S.Time = data.DaySeconds * (data.NightStarts + .05f);
        // Long enough for the pack to gather around the player; the player is kept on their feet so the hunt never ends.
        Check("tick: night, wolves hunting", () => { night.Tick(1 / 60f); Fed(night); night.S.Mauled = false; foreach (var w in night.S.Wolves) { w.Leap = 0; w.Feeding = w.Pinning = false; } }, warmup: 3600);
    }

    /// <summary>
    /// Fails when <paramref name="work"/>, after <paramref name="warmup"/> runs, allocates more than <paramref name="maxBytesPerRun"/>
    /// bytes per run over <paramref name="runs"/> more. The total counts, so an allocation every few runs fails a budget of 0 too.
    /// </summary>
    public static void Check(string label, Action work, long maxBytesPerRun = 0, int warmup = 60, int runs = 600)
    {
        for (int i = 0; i < warmup; i++) work();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < runs; i++) work();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        string report = $"{label}: {bytes} B over {runs} runs, {(double)bytes / runs:0.#} B per run (budget {maxBytesPerRun} B)";
        GD.Print(report);
        if (bytes > maxBytesPerRun * runs) throw new InvalidOperationException($"Allocation budget exceeded: {report}");
    }

    private static void Fed(World world) { world.S.Hunger = 100; world.S.Thirst = 100; world.S.Health = 100; }

    /// <summary>A daytime camp in the generated valley: woodcutters, a bellows crew at a hot forge, a furnace, a campfire and storage.</summary>
    private static World Settlement(GameData data)
    {
        var world = new World(data);
        world.S.Time = data.DaySeconds * .3f;
        var at = world.Player;
        var chest = world.Add("chest", at + new Vector2(2, 0));
        world.Add("chest", at + new Vector2(2, -2));
        chest.Inventory.Add("iron_bar", 20); chest.Inventory.Add("coal", 30); chest.Inventory.Add("iron_ore", 20); chest.Inventory.Add("wood", 30);
        var rack = world.Add("toolstation", at + new Vector2(-2, 0));
        rack.Inventory.Add("iron_axe", 2);
        var fire = world.Add("campfire", at + new Vector2(0, 3)); fire.Inventory.Add("wood", 20);
        var furnace = world.Add("furnace", at + new Vector2(3, 3));
        var forge = world.Add("reinforced", at + new Vector2(-3, 3)); forge.Heat = 90;
        Worker Hire(Vector2 p) { var w = new Worker { Id = world.S.NextId++, Name = $"Worker {world.S.Workers.Count + 1}", Pos = p }; world.S.Workers.Add(w); return w; }
        foreach (var tree in world.S.Things.Where(t => t.Kind == "tree").OrderBy(t => t.Pos.DistanceTo(at)).Take(2))
            world.Assign(Hire(rack.Pos), tree);
        world.Assign(Hire(forge.Pos + World.BellowsStand), forge);
        world.Assign(Hire(chest.Pos), forge);
        world.Assign(Hire(chest.Pos), furnace);
        return world;
    }
}
