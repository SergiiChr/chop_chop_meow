using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

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
            Require(hosted.Count == copied.Count && hosted.All(pair => copied.TryGetValue(pair.Key, out var entry) && entry.AsSpan().SequenceEqual(pair.Value)), "the guest's merged entries should match the host's");

        var packing = PackingLine(data, out var stock);
        // A crate fills about every twenty seconds and goes to the post, whose cart leaves once six are there; a hundred seconds see several crates and a cart.
        // The warm-up lasts until a cart has been out and back: the first crate carried out costs a one-off 98 KB in about one process in a hundred, which must not land in the measured runs.
        // Opening a crate names it and gives it contents, and a cart leaving or coming back builds its order: those rare events are what this budget allows (measured 0.6 bytes a tick).
        Check("tick: packing for the post", () => { packing.Tick(1 / 60f); Fed(packing); stock.Inventory.Add("berries", 64 - stock.Inventory["berries"]); stock.Inventory.Add("crate", 64 - stock.Inventory["crate"]); }, maxBytesPerRun: 1, warmup: 12000, runs: 6000);

        var mine = WorkingMine(data, out var shaft);
        // Emptying the output each tick keeps the crew digging underground instead of hauling.
        Check("tick: miners underground", () => { mine.Tick(1 / 60f); Fed(mine); shaft.Output!.Clear(); }, warmup: 1200);
        Require(mine.Workers.All(w => w.WorkState == WorkerWorkState.Underground), "the mine crew should be underground");

        var night = new World(data);
        RunUntilPhase(night, data.NightStarts + .05f);
        // Long enough for the pack to gather around the player; the player is kept on their feet so the hunt never ends.
        Check("tick: night, wolves hunting", () => { night.Tick(1 / 60f); Fed(night); FreePlayer(night); }, warmup: 3600);
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
        Require(bytes <= maxBytesPerRun * runs, $"Allocation budget exceeded: {report}");
    }

}
