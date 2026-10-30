using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Fixtures;

/// <summary>
/// Holds code that runs every tick to its allocation budget: every byte it allocates is garbage the runtime collects later,
/// in a pause that stalls a frame. Counts the calling thread's bytes, so it holds the same on any machine.
/// </summary>
public static class AllocationChecks {
    public static void Run(GameData data) {
        var valley = new World(data);
        float angle = 0;
        // A small circle the warm-up walks once, so the measured laps cross ground already seen: seeing something for the first time is a rare event that may allocate.
        Check("tick: fresh valley, player walking", () => {
            angle += .11f;
            valley.MovePlayer(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * .07f);
            valley.Tick(1 / 60f);
            Fed(valley);
        });

        var settlement = Settlement(data);
        // Twenty seconds first, so every worker has been through its trips once and the status and cost caches hold what they will show.
        // A store taking an item it has not held before grows its table once; that rare event is the one byte per tick allowed here (measured 0.2 to 0.5).
        Check("tick: working settlement", () => { settlement.Tick(1 / 60f); Fed(settlement); }, maxBytesPerRun: 1, warmup: 1200);

        // The host sends an update ten times a second, against one the guest confirmed a few updates back, as over a long connection, and the guest merges it.
        // Only entries that changed are new arrays, on the host and again in the guest's merge: about seven worker and station entries an update in a working settlement (measured 12.2 KB an update).
        var hosting = Settlement(data);
        var host = new WorldUpdates(); var guest = new WorldUpdates();
        var sent = new Queue<(Snapshot Host, Snapshot Guest)>();
        Check("update: working settlement", () => {
            for (int i = 0; i < 6; i++) hosting.Tick(1 / 60f);
            Fed(hosting);
            var now = host.Capture(hosting);
            var known = sent.Count > 3 ? sent.Dequeue() : default;
            var copy = guest.Merge(known.Guest, host.Delta(now, known.Host));
            if (known.Host != null) { host.Release(known.Host); guest.Release(known.Guest); }
            sent.Enqueue((now, copy));
        }, maxBytesPerRun: 14000, warmup: 200, runs: 300);
        foreach (var (hosted, copied) in sent)
            if (hosted.Count != copied.Count || hosted.Any(pair => !copied.TryGetValue(pair.Key, out var entry) || !entry.AsSpan().SequenceEqual(pair.Value)))
                throw new InvalidOperationException("Update check failed: the guest's merged entries should match the host's.");

        var packing = PackingLine(data, out var stock);
        // A crate fills about every twenty seconds and goes to the post, whose cart leaves once six are there; a hundred seconds see several crates and a cart.
        // Opening a crate names it and gives it contents, and a cart leaving or coming back builds its order: those rare events are what this budget allows (measured 1.7 bytes a tick).
        Check("tick: packing for the post", () => { packing.Tick(1 / 60f); Fed(packing); stock.Inventory.Add("berries", 64 - stock.Inventory["berries"]); stock.Inventory.Add("crate", 64 - stock.Inventory["crate"]); }, maxBytesPerRun: 2, warmup: 1200, runs: 6000);

        var mine = WorkingMine(data, out var shaft);
        // Emptying the output each tick keeps the crew digging underground instead of hauling.
        Check("tick: miners underground", () => { mine.Tick(1 / 60f); Fed(mine); shaft.Output!.Clear(); }, warmup: 1200);
        if (mine.S.Workers.Any(w => w.WorkState != WorkerWorkState.Underground)) throw new InvalidOperationException("Allocation check failed: the mine crew should be underground.");

        var night = new World(data);
        night.S.Time = data.DaySeconds * (data.NightStarts + .05f);
        // Long enough for the pack to gather around the player; the player is kept on their feet so the hunt never ends.
        Check("tick: night, wolves hunting", () => { night.Tick(1 / 60f); Fed(night); night.S.Mauled = false; foreach (var w in night.S.Wolves) { w.Leap = 0; w.Feeding = w.Pinning = false; } }, warmup: 3600);
    }

    /// <summary>
    /// Fails when <paramref name="work"/>, after <paramref name="warmup"/> runs, allocates more than <paramref name="maxBytesPerRun"/>
    /// bytes per run over <paramref name="runs"/> more. The total counts, so an allocation every few runs fails a budget of 0 too.
    /// </summary>
    public static void Check(string label, Action work, long maxBytesPerRun = 0, int warmup = 60, int runs = 600) {
        for (int i = 0; i < warmup; i++) work();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < runs; i++) work();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        string report = $"{label}: {bytes} B over {runs} runs, {(double)bytes / runs:0.#} B per run (budget {maxBytesPerRun} B)";
        GD.Print(report);
        if (bytes > maxBytesPerRun * runs) throw new InvalidOperationException($"Allocation budget exceeded: {report}");
    }

    private static void Fed(World world) { world.S.Hunger = 100; world.S.Thirst = 100; world.S.Health = 100; }

    /// <summary>An open iron mine with a full crew of pickaxe-carrying miners and a chest beside it.</summary>
    private static World WorkingMine(GameData data, out Thing mine) {
        var world = new World(data, false);
        world.S.Time = data.DaySeconds * .3f;
        var cliff = World.CliffOf(ThingKind.Iron);
        world.Player = World.MineSpot(cliff) + cliff.Face * 3;
        mine = world.Add(ThingKind.Mine, World.MineSpot(cliff)); mine.MineStage = MineStage.Open;
        world.Add(ThingKind.Chest, mine.Pos + cliff.Face * 3);
        for (int i = 0; i < data.Mine.Workers; i++) {
            var w = new Worker { Id = world.S.NextId++, Name = $"Miner {i + 1}", Pos = mine.Pos + cliff.Face * 2, Tool = "steel_pick" };
            world.S.Workers.Add(w); world.Assign(w, mine);
        }
        return world;
    }

    /// <summary>A packing station packing berries, its packer, a chest of berries and empty crates, and a post asking for crates of berries that sells on its own.</summary>
    private static World PackingLine(GameData data, out Thing stock) {
        var world = new World(data, false);
        world.S.Time = data.DaySeconds * .3f;
        var at = Terrain.Spawn;
        world.Player = at + new Vector2(-20, 0);
        stock = world.Add(ThingKind.Chest, at + new Vector2(2, 0));
        var packer = world.Add(ThingKind.Packer, at); world.SetPacking(packer, "berries");
        var sign = world.Add(ThingKind.Signpost, at + new Vector2(0, 3));
        world.SetFilter(sign, World.CrateOf("berries"), false, true); world.SetAutoSell(sign, true);
        var w = new Worker { Id = world.S.NextId++, Name = "Packer", Pos = at + new Vector2(1, 1) };
        world.S.Workers.Add(w); world.Assign(w, packer);
        return world;
    }

    /// <summary>A daytime camp in the generated valley: woodcutters, a bellows crew at a hot forge, a furnace, a campfire and storage.</summary>
    private static World Settlement(GameData data) {
        var world = new World(data);
        world.S.Time = data.DaySeconds * .3f;
        var at = world.Player;
        var chest = world.Add(ThingKind.Chest, at + new Vector2(2, 0));
        world.Add(ThingKind.Chest, at + new Vector2(2, -2));
        chest.Inventory.Add("iron_bar", 20); chest.Inventory.Add("coal", 30); chest.Inventory.Add("iron_ore", 20); chest.Inventory.Add("wood", 30);
        var rack = world.Add(ThingKind.Toolstation, at + new Vector2(-2, 0));
        rack.Inventory.Add("steel_axe", 2);
        var fire = world.Add(ThingKind.Campfire, at + new Vector2(0, 3)); fire.Inventory.Add("wood", 20);
        var furnace = world.Add(ThingKind.Furnace, at + new Vector2(3, 3));
        var forge = world.Add(ThingKind.Reinforced, at + new Vector2(-3, 3)); forge.Heat = 90;
        foreach (var tree in world.S.Things.Where(t => t.Kind == ThingKind.Tree).OrderBy(t => t.Pos.DistanceTo(at)).Take(2))
            world.Assign(AddWorker(world, rack.Pos), tree);
        world.Assign(AddWorker(world, forge.Pos + World.BellowsStand), forge);
        world.Assign(AddWorker(world, chest.Pos), forge);
        world.Assign(AddWorker(world, chest.Pos), furnace);
        return world;
    }
}
