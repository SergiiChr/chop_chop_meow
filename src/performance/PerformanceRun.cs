using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The window's half of a performance run: it sprints the player along <see cref="PerformanceRoute"/> by day and again
/// at night with a lit torch, samples every frame and writes the results.
/// </summary>
/// <param name="world">The window's world, joined to the server playing the fixture.</param>
/// <param name="viewport">The viewport whose frames it measures.</param>
/// <param name="folder">Where it writes its frames, run summary and final screenshot.</param>
internal sealed class PerformanceRun(World world, Viewport viewport, string folder) {
    /// <summary>The parts of a performance run, in order; the warmup is not sampled.</summary>
    public enum Part { Warmup, Day, Settle, Night }
    /// <summary>Where a run stands after a frame.</summary>
    public enum State { Running, Finished, Failed }

    private ulong start, previous;
    private int count, leg, stop, heartbeat, firsts;
    private Part part;
    private Vector2 lastPosition, stuckFrom;
    private double walked, stuckSince, settleStarted, campReached = double.NaN;
    private readonly List<object> events = new();
    private readonly Dictionary<int, (float Progress, int Batches)> machines = new();
    private Key alongKey = Key.None, acrossKey = Key.None;
    private PerformanceSample[] samples = Array.Empty<PerformanceSample>();
    private ResidentMemory? residentMemory;
    private static readonly Performance.Monitor[] PipelineMonitors = {
        Performance.Monitor.PipelineCompilationsCanvas, Performance.Monitor.PipelineCompilationsMesh, Performance.Monitor.PipelineCompilationsSurface,
        Performance.Monitor.PipelineCompilationsDraw, Performance.Monitor.PipelineCompilationsSpecialization,
    };
    /// <summary>Seconds without real progress toward a leg's end after which the run stops as stuck.</summary>
    private const double StuckSeconds = 1;

    /// <summary>The screenshot the window takes once the run has finished.</summary>
    public string Screenshot => Path.GetFullPath(Path.Combine(folder, "corner.png"));

    /// <summary>A part's name in the run's results.</summary>
    private static string Name(Part part) => part.ToString().ToLowerInvariant();

    /// <summary>A share of a day starting at 06:00 as a clock time.</summary>
    private static string Clock(float phase) {
        int minutes = (int)MathF.Round(phase * GameData.MinutesPerDay + 6 * 60) % GameData.MinutesPerDay;
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    /// <summary>Rendering pipelines compiled so far, summed over every compilation group.</summary>
    private static long PipelineCompilations() {
        double total = 0;
        foreach (var monitor in PipelineMonitors) total += Performance.GetMonitor(monitor);
        return (long)total;
    }

    /// <summary>Starts sampling, with the frame rate uncapped and V-Sync off; fails when a building stands across the route.</summary>
    public State Start() {
        Directory.CreateDirectory(folder);
        Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        RenderingServer.ViewportSetMeasureRenderTime(viewport.GetViewportRid(), true);
        // A building across the sprint would have the player slide around it, off the route both runs compare.
        foreach (var (at, routeLeg) in PerformanceRoute.Path())
            if (!world.CanStand(at)) return Fail($"The performance route's {PerformanceRoute.Legs[routeLeg].Name} leg crosses blocked ground at {at}.");
        samples = new PerformanceSample[500_000];
        residentMemory = new ResidentMemory();
        foreach (var thing in world.Things)
            if (thing.Kind is ThingKind.Reinforced or ThingKind.Furnace or ThingKind.Bench) machines[thing.Id] = (thing.Progress, 0);
        world.Player = PerformanceRoute.Start;
        lastPosition = world.Player;
        start = previous = Time.GetTicksUsec();
        GD.Print($"PERFORMANCE START: {PerformanceStage.WarmupSeconds:0}s warmup on the beach, then sprints ocean -> camp -> lake -> top corner by day from {Clock(PerformanceStage.DayStart)} and at night from {Clock(PerformanceStage.NightStart)} with a lit torch; 2560x1440, uncapped, vsync off; GPU {RenderingServer.GetVideoAdapterName()}");
        return State.Running;
    }

    /// <summary>
    /// Holds exactly the movement keys <paramref name="along"/> and <paramref name="across"/> down, with Shift while either
    /// is, pressing and letting go of each only when it changes, as a player would; none releases them all.
    /// </summary>
    private void HoldKeys(Key along = Key.None, Key across = Key.None) {
        bool sprint = along != Key.None || across != Key.None;
        if (sprint != (alongKey != Key.None || acrossKey != Key.None)) PressKey(Key.Shift, sprint);
        if (along != alongKey) { PressKey(alongKey, false); PressKey(along, true); alongKey = along; }
        if (across != acrossKey) { PressKey(acrossKey, false); PressKey(across, true); acrossKey = across; }
    }

    private static void PressKey(Key key, bool pressed) {
        if (key != Key.None) Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed });
    }

    /// <summary>Stops the run without results, saying why.</summary>
    private State Fail(string reason) {
        HoldKeys();
        residentMemory?.Dispose(); residentMemory = null;
        GD.PushError($"PERFORMANCE FAILED: {reason}");
        return State.Failed;
    }

    /// <summary>Counts the player's progress from where they are now, for telling when they are stuck.</summary>
    private void ResetStuck(double seconds) { stuckFrom = world.Player; stuckSince = seconds; }

    /// <summary>
    /// Sprints the player toward the current stop, moving on to the next stop or leg once there and letting go of the keys
    /// once the route is done; the name of the leg the player is stuck on, or null.
    /// </summary>
    private string? Sprint(double seconds) {
        if (leg == PerformanceRoute.Legs.Length) { HoldKeys(); return null; }
        var (name, stops) = PerformanceRoute.Legs[leg];
        var (along, across) = PerformanceRoute.Heading(world.Player, stops[stop]);
        if (along == 0 && across == 0) {
            if (++stop == stops.Length) {
                events.Add(new { seconds, part = Name(part), reached = name, position = new[] { world.Player.X, world.Player.Y } });
                if (leg == 0 && part == Part.Day) campReached = seconds;
                leg++; stop = 0;
            }
            ResetStuck(seconds);
            return Sprint(seconds);
        }
        HoldKeys(along > 0 ? Key.S : along < 0 ? Key.W : Key.None, across > 0 ? Key.D : across < 0 ? Key.A : Key.None);
        if (world.Player.DistanceTo(stuckFrom) > .5f) ResetStuck(seconds);
        else if (seconds - stuckSince > StuckSeconds) return name;
        return null;
    }

    /// <summary>The part of the run that follows <paramref name="current"/> once it is done: a sprint once the warmup ends, the settle once the day sprint reaches the corner, and so on; the same part while it is not done.</summary>
    private Part Next(Part current, double seconds) => current switch {
        Part.Warmup when seconds >= 0 => Part.Day,
        Part.Day when leg == PerformanceRoute.Legs.Length => Part.Settle,
        Part.Settle when seconds - settleStarted >= PerformanceStage.SettleSeconds => Part.Night,
        _ => current,
    };

    /// <summary>Plays and samples one frame, after the normal game update; arrays and input events are prepared outside steady sampling.</summary>
    public State Sample() {
        ulong now = Time.GetTicksUsec();
        double seconds = (now - start) / 1_000_000.0 - PerformanceStage.WarmupSeconds;
        if (part == Part.Night && leg == PerformanceRoute.Legs.Length || count == samples.Length) return Finish();
        var next = Next(part, seconds);
        if (next != part) {
            part = next;
            leg = stop = 0;
            ResetStuck(seconds);
            if (part == Part.Settle) {
                settleStarted = seconds;
                HoldKeys();
                world.Player = PerformanceRoute.Start;
                world.UseTorch(world.Player + Terrain.Sideways);
            }
            // The server turns to night once it sees the player back on the beach, within the settle.
            if (part == Part.Night && !world.Night) return Fail("The night sprint would start in daylight: the server never turned the clock to midnight.");
            lastPosition = world.Player;
            events.Add(new { seconds, part = Name(part), position = new[] { world.Player.X, world.Player.Y }, phase = world.DayPhase });
        }
        if (part is (Part.Day or Part.Night) && Sprint(seconds) is string stuck) return Fail($"The player got stuck at {world.Player} on the {stuck} leg.");
        // The frame that finishes a sprint belongs to its last leg; the settle has none.
        int sampledLeg = part == Part.Settle ? -1 : Math.Min(leg, PerformanceRoute.Legs.Length - 1);
        walked += lastPosition.DistanceTo(world.Player); lastPosition = world.Player;
        RecordFirsts(seconds);
        int activeWorkers = 0, activeMachines = 0;
        foreach (var worker in world.Workers)
            if (worker.WorkState is WorkerWorkState.Gathering or WorkerWorkState.Pumping or WorkerWorkState.Hammering or WorkerWorkState.Digging or WorkerWorkState.Underground) activeWorkers++;
        foreach (var thing in world.Things) {
            if (thing.Processing) activeMachines++;
            if (machines.TryGetValue(thing.Id, out var before)) {
                int batches = before.Batches + (before.Progress > .01f && thing.Progress < before.Progress ? 1 : 0);
                machines[thing.Id] = (thing.Progress, batches);
            }
        }
        if (part != Part.Warmup) {
            var rid = viewport.GetViewportRid();
            samples[count++] = new PerformanceSample(seconds, (now - previous) / 1000.0,
                Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000,
                RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid), RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid),
                GC.GetAllocatedBytesForCurrentThread(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                residentMemory!.Bytes(), Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),
                (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame), (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                activeWorkers, activeMachines, part, sampledLeg,
                PipelineCompilations());
        }
        previous = now;
        int beat = (int)((seconds + PerformanceStage.WarmupSeconds) / 30);
        if (beat > heartbeat) {
            heartbeat = beat;
            string where = part == Part.Settle ? "" : $", leg {PerformanceRoute.Legs[sampledLeg].Name}";
            GD.Print($"PERFORMANCE {seconds:0}s: {part}{where}, clock {world.Hour:0.0}, {activeWorkers} actively working, {activeMachines} processing, {walked:0.0} units walked");
        }
        return State.Running;
    }

    /// <summary>Records each first appearance among the run's events once the server has put it in front of the player.</summary>
    private void RecordFirsts(double seconds) {
        if (firsts == PerformanceStage.FirstTimes.Length || !(seconds >= campReached + PerformanceStage.FirstTimes[firsts].After)) return;
        var (_, name) = PerformanceStage.FirstTimes[firsts++];
        events.Add(new { seconds, first = name, pipelines = PipelineCompilations() });
    }

    private State Finish() {
        residentMemory?.Dispose(); residentMemory = null;
        HoldKeys();
        using (var csv = new StreamWriter(Path.Combine(folder, "frames.csv"))) {
            csv.WriteLine("seconds,frame_ms,process_ms,render_cpu_ms,gpu_ms,allocated_total,gen0_total,gen1_total,gen2_total,resident_bytes,video_bytes,draw_calls,primitives,workers_active,machines_active,part,leg,pipelines_total");
            for (int i = 0; i < count; i++) {
                var s = samples[i];
                csv.WriteLine(FormattableString.Invariant($"{s.Seconds:F6},{s.FrameMs:F4},{s.ProcessMs:F4},{s.RenderCpuMs:F4},{s.GpuMs:F4},{s.Allocated},{s.Gen0},{s.Gen1},{s.Gen2},{s.ResidentBytes},{s.VideoBytes},{s.DrawCalls},{s.Primitives},{s.WorkersActive},{s.MachinesActive},{Name(s.Part)},{(s.Leg < 0 ? "" : PerformanceRoute.Legs[s.Leg].Name)},{s.Pipelines}"));
            }
        }
        var stations = world.Things.Where(t => machines.ContainsKey(t.Id)).Select(t => new {
            t.Id,
            t.Kind,
            t.Recipe,
            batchesObserved = machines[t.Id].Batches,
            t.Processing,
            t.Progress,
            inventory = t.Inventory.Items,
        }).ToArray();
        var result = new {
            gpu = RenderingServer.GetVideoAdapterName(),
            godot = Engine.GetVersionInfo()["string"].AsString(),
            viewport = "2560x1440",
            vsync = false,
            warmupSeconds = PerformanceStage.WarmupSeconds,
            settleSeconds = PerformanceStage.SettleSeconds,
            frames = count,
            walkedUnits = walked,
            events,
            stations,
            workers = world.Workers.Select(w => new { w.Name, workState = w.WorkState.ToString(), w.Status, w.WageDebt }).ToArray(),
        };
        File.WriteAllText(Path.Combine(folder, "run.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"PERFORMANCE COMPLETE: {count} frames, {walked:0.0} units walked. Results: {folder}");
        return State.Finished;
    }
}
