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

        // A stack held from a chest is checked every tick: it must still be there, in reach and in the same box, without allocating.
        var holding = new World(data);
        var chest = TakeEverything(holding, holding.Add(ThingKind.Chest, holding.Player + Vector2.One));
        Give(holding, "wood", 10); holding.Transfer(holding.Backpack, chest.Inventory, "wood", 10);
        holding.Hold(chest.Inventory, 0, 10);
        Require(holding.Held != null, "the allocation fixture should hold the chest's wood");
        Check("tick: fresh valley, stack held from a chest", () => { holding.Tick(1 / 60f); Fed(holding); });
        Require(holding.Held != null, "the held stack should stay held through the ticks");

        // A worker walking to a bench asks on every tick whether the ground underfoot is gravel; the path outlasts the check's eleven seconds.
        var pathStart = new Vector2I(80, 80);
        var (paved, walker) = GravelPathSteps.WorkerSentAlongARow(data, pathStart, 36, 40);
        Check("tick: worker walking a gravel path", () => { paved.Tick(1 / 60f); Fed(paved); });
        Require(walker.Pos.X > pathStart.X + 25 && paved.GravelPaths.Covers(walker.Pos), "the worker should have walked the path through the ticks and still be on it");

        PlumbingAllocationChecks.CheckPumpingIntoTwoBarrelsStaysWithinAllocationBudget(data);

        var settlement = Settlement(data);
        // Twenty seconds first, so every worker has been through its trips once and the status and cost caches hold what they will show.
        // A store taking an item it has not held before grows its table once (measured 0.2 to 0.5 bytes a tick).
        // A woodcutter's first tree falls during the measured runs and leaves a stump, a new thing: those rare events are what this budget allows (measured 6.2).
        Check("tick: working settlement", () => { settlement.Tick(1 / 60f); Fed(settlement); }, maxBytesPerRun: 7, warmup: 1200);

        // A chest's hauler with nothing to carry looks on every tick for a place asking for what the chest holds, and for storage or a station giving out what its filter names.
        var storing = new World(data);
        var store = TakeEverything(storing, storing.Add(ThingKind.Chest, storing.Player + new Vector2(3, 0)));
        store.Inventory.Add("wood", 10);
        storing.SetFilter(store, "stone", false, true);
        TakeEverything(storing, storing.Add(ThingKind.Chest, storing.Player + new Vector2(-3, 0))).Inventory.Add("planks", 10);
        WorkerSteps.Bench(storing, storing.Player + new Vector2(0, 4), "crate");
        var storeHauler = WorkerSteps.Staff(storing, store, JobKind.Haul);
        Check("tick: chest hauler with nothing to carry", () => { storing.Tick(1 / 60f); Fed(storing); });
        Require(storeHauler.Bag.Count == 0 && store.Inventory["wood"] == 10, "the chest's hauler should have had nothing to carry through the ticks");

        var resting = FundedWorld(data);
        var fire = resting.Add(ThingKind.Campfire, resting.Player + new Vector2(4, 0));
        var idle = new[] { AddWorker(resting, fire.Pos + new Vector2(2, 2)), AddWorker(resting, fire.Pos + new Vector2(-2, 2)) };
        Check("tick: idle workers sitting at a campfire", () => { resting.Tick(1 / 60f); Fed(resting); }, warmup: 1200);
        Require(idle.All(w => w.WorkState is WorkerWorkState.Resting or WorkerWorkState.Perching) && fire.Burn > 0, "the idle workers should sit at the campfire, which burns with no wood");

        var stuck = FundedWorld(data);
        var stoneOnly = stuck.Add(ThingKind.Chest, stuck.Player + new Vector2(3, 0));
        stuck.SetFilter(stoneOnly, "stone", true, true);
        var holder = AddWorker(stuck, stuck.Player + new Vector2(0, 3));
        holder.Bag.Add("wood", 6);
        Check("tick: worker holding a load nothing takes", () => { stuck.Tick(1 / 60f); Fed(stuck); }, warmup: 600);
        Require(holder.Bag["wood"] == 6 && holder.Status.Contains("no accepting storage"), "the worker should hold the wood nothing takes through the ticks");

        // The host sends an update thirty times a second, against one the guest confirmed a few updates back, as over a long connection, and the guest merges it.
        // Only entries that changed are new arrays, on the host and again in the guest's merge: about seven worker and station entries an update in a working settlement (measured 10.5 KB an update).
        var hosting = Settlement(data);
        // Two hundred path tiles lie off to one side: they ride in an entry of their own, which an update that lays none does not write again.
        GravelPathSteps.LayWhereFree(hosting, new Vector2I(60, 80), 200);
        // A hundred packed crates lie in chests off to the other side, with an entry of their own too.
        PlayerSteps.StoreCrates(hosting, new Vector2(60, 110), 100);
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
        }, maxBytesPerRun: 12200, warmup: 200, runs: 300);
        foreach (var (hosted, copied) in sent)
            Require(hosted.Count == copied.Count && hosted.All(pair => copied.TryGetValue(pair.Key, out var entry) && entry.AsSpan().SequenceEqual(pair.Value)), "the guest's merged entries should match the host's");

        var packing = PackingLine(data, out var stock);
        // A crate fills about every twenty seconds and goes to the post, whose cart leaves once six are there; a hundred seconds see several crates and a cart.
        // The warm-up lasts until a cart has been out and back: the first crate carried out costs a one-off 98 KB in about one process in a hundred, which must not land in the measured runs.
        // Opening a crate names it and gives it contents, and a cart leaving or coming back builds its order: those rare events are what this budget allows (measured 1.1 bytes a tick).
        // The measured runs cross dusk, so the dens stay shut: wolves coming out are not packing.
        Check("tick: packing for the post", () => { ShutDens(packing); packing.Tick(1 / 60f); Fed(packing); stock.Inventory.Add("berries", 64 - stock.Inventory["berries"]); stock.Inventory.Add("crate", 64 - stock.Inventory["crate"]); }, maxBytesPerRun: 2, warmup: 12000, runs: 6000);

        var kitchen = WaterKitchen(data, out var pantry);
        var table = kitchen.Things.First(t => t.Kind == ThingKind.Cooktable);
        // Bottles go round: poured into the table, carried back to the pile, refilled at the lake; the pile keeps its filters for every fill.
        // The table's water is kept under ten liters: filled to its stack of 64, it would need no bottle for the rest of the check.
        // The warm-up covers a bottle coming back part full, whose status is built once.
        Check("tick: cooking with water bottles", () => {
            kitchen.Tick(1 / 60f); Fed(kitchen); pantry.Inventory.Add("raw_meat", 64 - pantry.Inventory["raw_meat"]); pantry.Inventory.Add("wood", 64 - pantry.Inventory["wood"]);
            table.Inventory.Remove(Water.Liquid, Math.Max(0, table.Inventory[Water.Liquid] - 9));
        }, warmup: 6000, runs: 3000);
        Require(kitchen.Things.Sum(t => t.Inventory["meat_stew"]) > 0 && kitchen.Workers.All(w => !w.Status.Contains("no accepting storage")), "the water kitchen should cook stew from poured water and store its bottles");

        var mine = WorkingMine(data, out var shaft);
        // Emptying the output each tick keeps the crew digging underground instead of hauling.
        Check("tick: miners underground", () => { mine.Tick(1 / 60f); Fed(mine); shaft.Output!.Clear(); }, warmup: 1200);
        Require(mine.Workers.All(w => w.WorkState == WorkerWorkState.Underground), "the mine crew should be underground");

        var dusk = new World(data);
        RunUntilPhase(dusk, data.DuskStarts + .01f);
        // Every den lets its wolf out on the first tick; after that they patrol, smell and howl without allocating.
        // Patrolling wolves walk slowly, so the warm-up runs long enough for their first-time costs to land before the measured runs.
        Check("tick: dusk, wolves patrolling", () => { dusk.Tick(1 / 60f); Fed(dusk); FreePlayer(dusk); }, warmup: 2400);

        var night = new World(data);
        RunUntilPhase(night, data.NightStarts + .05f);
        ShutDens(night);
        // A pack that has smelled the player closes in; the player is kept on their feet so the hunt never ends.
        for (int i = 0; i < 3; i++) PlaceWolf(night, new Wolf { Pos = night.Slide(night.Player, Vector2.Right.Rotated(i * 2.1f) * 15) });
        Check("tick: night, wolves hunting", () => { night.Tick(1 / 60f); Fed(night); FreePlayer(night); }, warmup: 600);
        // Wolves a torch scared off circle the player in follow mode, each given its way round on the first tick.
        var (torchlit, _) = WolfSteps.Followed(data, 3);
        Check("tick: night, wolves following a torch", () => { torchlit.Tick(1 / 60f); Fed(torchlit); }, warmup: 600);
        // Wolves come to the locked door of the cabin the player is in: one scratches at it while the other waits, then both patrol away.
        var (indoors, hut, _) = DoorCheckSteps.ShelteredNight(data, locked: true);
        for (int i = 0; i < 2; i++) PatrolWolf(indoors, new Wolf { Pos = Doorways.DoorPoint(hut) + new Vector2(2 * i - 1, 7) });
        Check("tick: night, wolves at a cabin's door", () => { indoors.Tick(1 / 60f); Fed(indoors); }, warmup: 300);
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
