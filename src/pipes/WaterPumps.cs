using System;
using Godot;

namespace ChopChopMeow;

/// <summary>One stroke of the water pump and the water it supplies while its pressure rises and falls.</summary>
internal static class WaterPumps {
    internal const float StrokeSeconds = 1;
    private const float StrokePressure = 10, LossPerSecond = 10;

    internal static void Stroke(Thing pump) {
        pump.Pressure = Math.Min(100, pump.Pressure + StrokePressure);
        pump.PumpStrokeLeft = StrokeSeconds;
    }

    /// <summary>Liters supplied during this tick. The handle holds pressure through its stroke, then it bleeds away.</summary>
    internal static float Advance(Thing pump, float dt) {
        float working = Math.Min(dt, pump.PumpStrokeLeft);
        pump.PumpStrokeLeft -= working;
        float falling = Math.Min(dt - working, pump.Pressure / LossPerSecond);
        float startRate = pump.FlowRate;
        pump.Pressure = Math.Max(0, pump.Pressure - falling * LossPerSecond);
        return working * startRate + falling * (startRate + pump.FlowRate) / 2;
    }
}
