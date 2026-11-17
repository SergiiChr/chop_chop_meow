using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The parts of a performance run, in order; the warmup is not sampled.</summary>
    private enum PerformancePart { Warmup, Day, Settle, Night }
    private bool performanceRunning;
    private ulong performanceStart, performancePrevious;
    private int performanceCount, performanceLeg, performanceStop, performanceHeartbeat, performanceFirsts;
    private PerformancePart performancePart;
    private string performanceFolder = "";
    private Vector2 performanceLastPosition, performanceStuckFrom;
    private double performanceWalked, performanceStuckSince, performanceSettleStarted, performanceCampReached = double.NaN;
    private readonly List<object> performanceEvents = new();
    private readonly Dictionary<int, (float Progress, int Batches)> performanceMachines = new();
    private Key performanceAlongKey = Key.None, performanceAcrossKey = Key.None;
    private PerformanceSample[] performanceSamples = Array.Empty<PerformanceSample>();
    private ResidentMemory? residentMemory;
    private static readonly Performance.Monitor[] PipelineMonitors = {
        Performance.Monitor.PipelineCompilationsCanvas, Performance.Monitor.PipelineCompilationsMesh, Performance.Monitor.PipelineCompilationsSurface,
        Performance.Monitor.PipelineCompilationsDraw, Performance.Monitor.PipelineCompilationsSpecialization,
    };
    /// <summary>Seconds without real progress toward a leg's end after which the run stops as stuck.</summary>
    private const double StuckSeconds = 1;
    /// <summary>Rendering pipelines compiled so far, summed over every compilation group.</summary>
    private static long PipelineCompilations() {
        double total = 0;
        foreach (var monitor in PipelineMonitors) total += Performance.GetMonitor(monitor);
        return (long)total;
    }
    private readonly record struct PerformanceSample(double Seconds, double FrameMs, double ProcessMs, double RenderCpuMs, double GpuMs,
        long Allocated, int Gen0, int Gen1, int Gen2, long ResidentBytes, double VideoBytes, int DrawCalls, int Primitives,
        int WorkersActive, int MachinesActive, PerformancePart Part, int Leg, long Pipelines);

    /// <summary>
    /// Reads the process's resident memory from Linux's /proc/self/statm without allocating,
    /// so sampling it every frame leaves the measured frames alone.
    /// Godot's own memory monitors count only in debug builds.
    /// </summary>
    private sealed class ResidentMemory : IDisposable {
        private readonly FileStream file = new("/proc/self/statm", FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite, bufferSize: 0);
        private readonly byte[] text = new byte[128];
        public long Bytes() {
            file.Position = 0;
            var line = text.AsSpan(0, file.Read(text));
            // The line lists the process's total pages, then its resident pages.
            Utf8Parser.TryParse(line[(line.IndexOf((byte)' ') + 1)..], out long pages, out _);
            return pages * System.Environment.SystemPageSize;
        }
        public void Dispose() => file.Dispose();
    }

    /// <summary>Starts sampling, once this window has joined the server playing the fixture.</summary>
    private void StartPerformanceRun() {
        performanceFolder = Launch.Value("--performance-run") ?? "build/performance";
        Directory.CreateDirectory(performanceFolder);
        startMenu.Close(); panel.Close();
        Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        ApplyResolution(new Vector2I(2560, 1440));
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        // A building across the sprint would have the player slide around it, off the route both runs compare.
        foreach (var (at, leg) in PerformanceStage.Path())
            if (!world.CanStand(at)) { FailPerformanceRun($"The performance route's {PerformanceStage.Route[leg].Name} leg crosses blocked ground at {at}."); return; }
        performanceSamples = new PerformanceSample[500_000];
        residentMemory = new ResidentMemory();
        foreach (var thing in world.Things)
            if (thing.Kind is ThingKind.Reinforced or ThingKind.Furnace or ThingKind.Bench) performanceMachines[thing.Id] = (thing.Progress, 0);
        world.Player = PerformanceStage.Start;
        performanceLastPosition = world.Player;
        performanceStart = performancePrevious = Time.GetTicksUsec();
        performanceRunning = true;
        GD.Print($"PERFORMANCE START: {PerformanceStage.WarmupSeconds:0}s warmup on the beach, then sprints ocean -> camp -> pond -> top corner by day from 13:30 and at night from midnight with a lit torch; 2560x1440, uncapped, vsync off; GPU {RenderingServer.GetVideoAdapterName()}");
    }

    /// <summary>
    /// Holds exactly the movement keys <paramref name="along"/> and <paramref name="across"/> down, with Shift while either
    /// is, pressing and letting go of each only when it changes, as a player would; none releases them all.
    /// </summary>
    private void HoldPerformanceKeys(Key along = Key.None, Key across = Key.None) {
        bool sprint = along != Key.None || across != Key.None;
        if (sprint != (performanceAlongKey != Key.None || performanceAcrossKey != Key.None)) PressPerformanceKey(Key.Shift, sprint);
        if (along != performanceAlongKey) { PressPerformanceKey(performanceAlongKey, false); PressPerformanceKey(along, true); performanceAlongKey = along; }
        if (across != performanceAcrossKey) { PressPerformanceKey(performanceAcrossKey, false); PressPerformanceKey(across, true); performanceAcrossKey = across; }
    }

    private static void PressPerformanceKey(Key key, bool pressed) {
        if (key != Key.None) Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed });
    }

    /// <summary>Stops the run without results, saying why, and quits with a failure.</summary>
    private void FailPerformanceRun(string reason) {
        performanceRunning = false;
        HoldPerformanceKeys();
        GD.PushError($"PERFORMANCE FAILED: {reason}");
        Quit(1);
    }

    /// <summary>Counts the player's progress from where they are now, for telling when they are stuck.</summary>
    private void ResetStuck(double seconds) { performanceStuckFrom = world.Player; performanceStuckSince = seconds; }

    /// <summary>
    /// Sprints the player toward the current stop, moving on to the next stop or leg once there and letting go of the keys
    /// once the route is done; false when the player is stuck.
    /// </summary>
    private bool SprintRoute(double seconds) {
        if (performanceLeg == PerformanceStage.Route.Length) { HoldPerformanceKeys(); return true; }
        var (name, stops) = PerformanceStage.Route[performanceLeg];
        var (along, across) = PerformanceStage.Heading(world.Player, stops[performanceStop]);
        if (along == 0 && across == 0) {
            if (++performanceStop == stops.Length) {
                performanceEvents.Add(new { seconds, part = performancePart.ToString(), reached = name, position = new[] { world.Player.X, world.Player.Y } });
                if (performanceLeg == 0 && performancePart == PerformancePart.Day) performanceCampReached = seconds;
                performanceLeg++; performanceStop = 0;
            }
            ResetStuck(seconds);
            return SprintRoute(seconds);
        }
        HoldPerformanceKeys(along > 0 ? Key.S : along < 0 ? Key.W : Key.None, across > 0 ? Key.D : across < 0 ? Key.A : Key.None);
        if (world.Player.DistanceTo(performanceStuckFrom) > .5f) ResetStuck(seconds);
        else if (seconds - performanceStuckSince > StuckSeconds) {
            FailPerformanceRun($"The player got stuck at {world.Player} on the {name} leg.");
            return false;
        }
        return true;
    }

    /// <summary>The part of the run that follows <paramref name="part"/> once it is done: a sprint once the warmup ends, the settle once the day sprint reaches the corner, and so on; the same part while it is not done.</summary>
    private PerformancePart NextPerformancePart(PerformancePart part, double seconds) => part switch {
        PerformancePart.Warmup when seconds >= 0 => PerformancePart.Day,
        PerformancePart.Day when performanceLeg == PerformanceStage.Route.Length => PerformancePart.Settle,
        PerformancePart.Settle when seconds - performanceSettleStarted >= PerformanceStage.SettleSeconds => PerformancePart.Night,
        _ => part,
    };

    // Called only for the developer benchmark, after the normal game update. Arrays and input events are prepared outside steady sampling.
    private void SamplePerformance() {
        ulong now = Time.GetTicksUsec();
        double seconds = (now - performanceStart) / 1_000_000.0 - PerformanceStage.WarmupSeconds;
        if (performancePart == PerformancePart.Night && performanceLeg == PerformanceStage.Route.Length || performanceCount == performanceSamples.Length) { FinishPerformanceRun(); return; }
        var part = NextPerformancePart(performancePart, seconds);
        if (part != performancePart) {
            performancePart = part;
            performanceLeg = performanceStop = 0;
            ResetStuck(seconds);
            if (part == PerformancePart.Settle) {
                performanceSettleStarted = seconds;
                HoldPerformanceKeys();
                world.Player = PerformanceStage.Start;
                world.UseTorch(world.Player + Terrain.Sideways);
            }
            performanceLastPosition = world.Player;
            performanceEvents.Add(new { seconds, part = part.ToString(), position = new[] { world.Player.X, world.Player.Y }, phase = world.DayPhase });
        }
        if (part is (PerformancePart.Day or PerformancePart.Night) && !SprintRoute(seconds)) return;
        // The frame that finishes a sprint belongs to its last leg; the settle has none.
        int leg = part == PerformancePart.Settle ? 0 : Math.Min(performanceLeg, PerformanceStage.Route.Length - 1);
        performanceWalked += performanceLastPosition.DistanceTo(world.Player); performanceLastPosition = world.Player;
        ShowFirsts(seconds);
        int activeWorkers = 0, activeMachines = 0;
        foreach (var worker in world.Workers)
            if (worker.WorkState is WorkerWorkState.Gathering or WorkerWorkState.Pumping or WorkerWorkState.Hammering or WorkerWorkState.Digging or WorkerWorkState.Underground) activeWorkers++;
        foreach (var thing in world.Things) {
            if (thing.Processing) activeMachines++;
            if (performanceMachines.TryGetValue(thing.Id, out var previous)) {
                int batches = previous.Batches + (previous.Progress > .01f && thing.Progress < previous.Progress ? 1 : 0);
                performanceMachines[thing.Id] = (thing.Progress, batches);
            }
        }
        if (part != PerformancePart.Warmup) {
            var viewport = GetViewport().GetViewportRid();
            performanceSamples[performanceCount++] = new PerformanceSample(seconds, (now - performancePrevious) / 1000.0,
                Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000,
                RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport), RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport),
                GC.GetAllocatedBytesForCurrentThread(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                residentMemory!.Bytes(), Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),
                (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame), (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                activeWorkers, activeMachines, part, leg,
                PipelineCompilations());
        }
        performancePrevious = now;
        int heartbeat = (int)((seconds + PerformanceStage.WarmupSeconds) / 30);
        if (heartbeat > performanceHeartbeat) {
            performanceHeartbeat = heartbeat;
            string where = part == PerformancePart.Settle ? "" : $", leg {PerformanceStage.Route[leg].Name}";
            GD.Print($"PERFORMANCE {seconds:0}s: {part}{where}, clock {world.Hour:0.0}, {activeWorkers} actively working, {activeMachines} processing, {performanceWalked:0.0} units walked");
        }
    }

    /// <summary>Records each first appearance among the run's events once the server has put it in front of the player.</summary>
    private void ShowFirsts(double seconds) {
        if (performanceFirsts == PerformanceStage.FirstTimes.Length || !(seconds >= performanceCampReached + PerformanceStage.FirstTimes[performanceFirsts].After)) return;
        var (_, name) = PerformanceStage.FirstTimes[performanceFirsts++];
        performanceEvents.Add(new { seconds, first = name, pipelines = PipelineCompilations() });
    }

    private void FinishPerformanceRun() {
        performanceRunning = false;
        residentMemory?.Dispose(); residentMemory = null;
        HoldPerformanceKeys();
        using (var csv = new StreamWriter(Path.Combine(performanceFolder, "frames.csv"))) {
            csv.WriteLine("seconds,frame_ms,process_ms,render_cpu_ms,gpu_ms,allocated_total,gen0_total,gen1_total,gen2_total,resident_bytes,video_bytes,draw_calls,primitives,workers_active,machines_active,part,leg,pipelines_total");
            for (int i = 0; i < performanceCount; i++) {
                var s = performanceSamples[i];
                csv.WriteLine(FormattableString.Invariant($"{s.Seconds:F6},{s.FrameMs:F4},{s.ProcessMs:F4},{s.RenderCpuMs:F4},{s.GpuMs:F4},{s.Allocated},{s.Gen0},{s.Gen1},{s.Gen2},{s.ResidentBytes},{s.VideoBytes},{s.DrawCalls},{s.Primitives},{s.WorkersActive},{s.MachinesActive},{s.Part.ToString().ToLowerInvariant()},{PerformanceStage.Route[s.Leg].Name},{s.Pipelines}"));
            }
        }
        var stations = world.Things.Where(t => performanceMachines.ContainsKey(t.Id)).Select(t => new {
            t.Id,
            t.Kind,
            t.Recipe,
            batchesObserved = performanceMachines[t.Id].Batches,
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
            frames = performanceCount,
            walkedUnits = performanceWalked,
            events = performanceEvents,
            stations,
            workers = world.Workers.Select(w => new { w.Name, workState = w.WorkState.ToString(), w.Status, w.WageDebt }).ToArray(),
        };
        File.WriteAllText(Path.Combine(performanceFolder, "run.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"PERFORMANCE COMPLETE: {performanceCount} frames, {performanceWalked:0.0} units walked. Results: {performanceFolder}");
        Capture(Path.GetFullPath(Path.Combine(performanceFolder, "corner.png")));
    }
}
