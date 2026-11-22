using System;
using Godot;

namespace ChopChopMeow;

/// <summary>One stroke of the water pump and the water it supplies while its pressure rises and falls.</summary>
internal static class WaterPumps {
    internal const float StrokeSeconds = 1;
    private const float StrokePressure = 10, LossPerSecond = 10;

    /// <summary>A single operator works the handle from the pump's bank side.</summary>
    internal static bool Work(World world, Worker w, Thing pump, float dt) {
        foreach (var other in world.Workers)
            if (other != w && other.Assignment == pump.Id && other.WorkState == WorkerWorkState.Pumping) {
                w.Status = "The pump is in use";
                return false;
            }
        if (!world.HandsFree(w, dt)) return true;
        w.Status = "Pumping water";
        if (world.Walk(w, pump.ToWorld(World.BellowsStand), dt, .1f, exact: true)) {
            w.WorkState = WorkerWorkState.Pumping;
            if (pump.PumpStrokeLeft == 0) Stroke(pump);
        }
        return true;
    }

    internal static void Stroke(Thing pump) {
        pump.Pressure = Math.Min(100, pump.Pressure + StrokePressure);
        pump.PumpStrokeLeft = StrokeSeconds;
    }

    /// <summary>Liters supplied during this tick. The handle holds pressure through its stroke, then it bleeds away.</summary>
    internal static float Advance(Thing pump, float dt) {
        float working = Math.Min(dt, pump.PumpStrokeLeft);
        pump.PumpStrokeLeft -= working;
        if (working > 0 && Mathf.IsZeroApprox(pump.PumpStrokeLeft)) {
            pump.PumpStrokeLeft = 0;
            pump.Rounds++;
        }
        float falling = Math.Min(dt - working, pump.Pressure / LossPerSecond);
        float startRate = pump.FlowRate;
        pump.Pressure = Math.Max(0, pump.Pressure - falling * LossPerSecond);
        return working * startRate + falling * (startRate + pump.FlowRate) / 2;
    }
}
