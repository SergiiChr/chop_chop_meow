using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ChopChopMeow;

public partial class Game {
    private bool performanceRunning;
    private ulong performanceStart, performancePrevious;
    private int performanceCount, performanceSite = -1, performanceKey = -1, performanceHeartbeat;
    private bool performanceSettlement;
    private string performanceFolder = "";
    private Vector2 performanceLastPosition;
    private double performanceWalked;
    private readonly List<object> performanceEvents = new();
    private readonly Dictionary<int, (float Progress, int Batches)> performanceMachines = new();
    private PerformanceSample[] performanceSamples = Array.Empty<PerformanceSample>();
    private static readonly Key[] PerformanceKeys = { Key.D, Key.S, Key.A, Key.W };
    private static readonly string[] PerformanceSites = { "forest", "lake", "coast", "iron", "tin", "coal", "upper-valley", "forest-edge" };
    private readonly record struct PerformanceSample(double Seconds, double FrameMs, double ProcessMs, double RenderCpuMs, double GpuMs,
        long Allocated, int Gen0, int Gen1, int Gen2, double StaticBytes, double VideoBytes, int DrawCalls, int Primitives,
        int WorkersActive, int MachinesActive, int Site, bool Night, bool Settlement, long Pipelines);

    private void StartPerformanceRun() {
        int at = Array.IndexOf(launchArgs, "--performance-run");
        if (at < 0) return;
        performanceFolder = at + 1 < launchArgs.Length ? launchArgs[at + 1] : "build/performance";
        Directory.CreateDirectory(performanceFolder);
        HideScreen(); ClosePanel(); paused = false;
        Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        DisplayServer.WindowSetSize(new Vector2I(2560, 1440));
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        performanceSamples = new PerformanceSample[500_000];
        foreach (var thing in world.S.Things)
            if (thing.Kind is "reinforced" or "furnace" or "bench") performanceMachines[thing.Id] = (thing.Progress, 0);
        performanceLastPosition = world.Player;
        performanceStart = performancePrevious = Time.GetTicksUsec();
        performanceRunning = true;
        GD.Print($"PERFORMANCE START: 15s warmup, 240s complete day/night lighting cycle (clock 3.5x, simulation normal rate), 120s daytime production; 2560x1440, uncapped, vsync off; GPU {RenderingServer.GetVideoAdapterName()}");
    }

    // Called only for the developer benchmark, after the normal game update. Arrays and input events are prepared outside steady sampling.
    private void SamplePerformance(double delta) {
        ulong now = Time.GetTicksUsec();
        double elapsed = (now - performanceStart) / 1_000_000.0;
        double seconds = elapsed - 15;
        if (seconds >= 360 || performanceCount == performanceSamples.Length) { FinishPerformanceRun(); return; }
        world.S.Hunger = world.S.Thirst = world.S.Health = 100;
        if (seconds >= 240 && !performanceSettlement) {
            performanceSettlement = true; world.S.Time = world.Data.DaySeconds * .22f;
            performanceSite = -1; performanceLastPosition = world.Player;
        }
        // Only the clock is accelerated. Movement, production, particles and simulation still receive their normal frame delta.
        if (!performanceSettlement && seconds >= 0) world.S.Time += Math.Min((float)delta, .1f) * 2.5f;
        if (seconds < 0) world.S.Time = world.Data.DaySeconds * .12f;
        int site = performanceSettlement ? 0 : Math.Clamp((int)(Math.Max(0, seconds) / 30), 0, 7);
        if (site != performanceSite) {
            performanceSite = site;
            var target = site switch {
                1 => Terrain.WaterPoint(Terrain.Lake + Terrain.Seaward) + Terrain.Seaward * 3,
                2 => Terrain.Point(Terrain.Coast(Terrain.Across(World.Spawn)) - 6, Terrain.Across(World.Spawn)),
                3 or 4 or 5 => World.MineSpot(World.CliffOf(site == 3 ? "iron" : site == 4 ? "tin" : "coal")) + World.CliffOf(site == 3 ? "iron" : site == 4 ? "tin" : "coal").Face * 4,
                6 => Terrain.Point(125, -18),
                7 => World.Spawn + new Vector2(14, 4),
                _ => World.Spawn,
            };
            for (int ring = 0; ring < 10; ring++) {
                var candidate = target + Terrain.Seaward * ring;
                if (world.CanStand(candidate)) { world.Player = candidate; break; }
            }
            performanceLastPosition = world.Player;
            performanceEvents.Add(new { seconds, site = PerformanceSites[site], position = new[] { world.Player.X, world.Player.Y }, phase = world.DayPhase });
        }
        performanceWalked += performanceLastPosition.DistanceTo(world.Player); performanceLastPosition = world.Player;
        int key = (int)(Math.Max(0, elapsed) / 2) % PerformanceKeys.Length;
        if (key != performanceKey) {
            if (performanceKey >= 0) Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = PerformanceKeys[performanceKey], Keycode = PerformanceKeys[performanceKey], Pressed = false });
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = PerformanceKeys[key], Keycode = PerformanceKeys[key], Pressed = true });
            performanceKey = key;
        }
        int activeWorkers = 0, activeMachines = 0;
        foreach (var worker in world.S.Workers)
            if (worker.WorkState is WorkerWorkState.Gathering or WorkerWorkState.Pumping or WorkerWorkState.Hammering or WorkerWorkState.Digging or WorkerWorkState.Underground) activeWorkers++;
        foreach (var thing in world.S.Things) {
            if (thing.Processing) activeMachines++;
            if (performanceMachines.TryGetValue(thing.Id, out var previous)) {
                int batches = previous.Batches + (previous.Progress > .01f && thing.Progress < previous.Progress ? 1 : 0);
                performanceMachines[thing.Id] = (thing.Progress, batches);
            }
        }
        if (seconds >= 0) {
            var viewport = GetViewport().GetViewportRid();
            performanceSamples[performanceCount++] = new PerformanceSample(seconds, (now - performancePrevious) / 1000.0,
                Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000,
                RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport), RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport),
                GC.GetAllocatedBytesForCurrentThread(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                Performance.GetMonitor(Performance.Monitor.MemoryStatic), Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),
                (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame), (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                activeWorkers, activeMachines, performanceSite, world.Night, performanceSettlement,
                (long)(Performance.GetMonitor(Performance.Monitor.PipelineCompilationsCanvas) + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsMesh) + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSurface) + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsDraw) + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSpecialization)));
        }
        performancePrevious = now;
        int heartbeat = (int)(elapsed / 30);
        if (heartbeat > performanceHeartbeat) {
            performanceHeartbeat = heartbeat;
            GD.Print($"PERFORMANCE {seconds:0}s: {(performanceSettlement ? "production" : PerformanceSites[site])}, day phase {world.DayPhase:0.000}, {activeWorkers} actively working, {activeMachines} processing, {performanceWalked:0.0} units walked");
        }
    }

    private void FinishPerformanceRun() {
        performanceRunning = false;
        if (performanceKey >= 0) Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = PerformanceKeys[performanceKey], Keycode = PerformanceKeys[performanceKey], Pressed = false });
        using (var csv = new StreamWriter(Path.Combine(performanceFolder, "frames.csv"))) {
            csv.WriteLine("seconds,frame_ms,process_ms,render_cpu_ms,gpu_ms,allocated_total,gen0_total,gen1_total,gen2_total,static_bytes,video_bytes,draw_calls,primitives,workers_active,machines_active,site,night,settlement,pipelines_total");
            for (int i = 0; i < performanceCount; i++) {
                var s = performanceSamples[i];
                csv.WriteLine(FormattableString.Invariant($"{s.Seconds:F6},{s.FrameMs:F4},{s.ProcessMs:F4},{s.RenderCpuMs:F4},{s.GpuMs:F4},{s.Allocated},{s.Gen0},{s.Gen1},{s.Gen2},{s.StaticBytes},{s.VideoBytes},{s.DrawCalls},{s.Primitives},{s.WorkersActive},{s.MachinesActive},{PerformanceSites[s.Site]},{s.Night},{s.Settlement},{s.Pipelines}"));
            }
        }
        var stations = world.S.Things.Where(t => performanceMachines.ContainsKey(t.Id)).Select(t => new {
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
            cycleClockMultiplier = 3.5,
            warmupSeconds = 15,
            measuredSeconds = 360,
            frames = performanceCount,
            walkedUnits = performanceWalked,
            events = performanceEvents,
            stations,
            workers = world.S.Workers.Select(w => new { w.Name, workState = w.WorkState.ToString(), w.Status, w.WageDebt }).ToArray(),
        };
        File.WriteAllText(Path.Combine(performanceFolder, "run.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"PERFORMANCE COMPLETE: {performanceCount} frames, {performanceWalked:0.0} units walked. Results: {performanceFolder}");
        Capture(Path.GetFullPath(Path.Combine(performanceFolder, "settlement.png")));
    }
}
