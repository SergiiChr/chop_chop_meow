namespace ChopChopMeow;

/// <summary>
/// One sampled frame of a performance run: its times, memory, drawing, the camp's work and where on the route it was,
/// as an index into its legs, or -1 between the sprints.
/// </summary>
internal readonly record struct PerformanceSample(double Seconds, double FrameMs, double ProcessMs, double RenderCpuMs, double GpuMs,
    long Allocated, int Gen0, int Gen1, int Gen2, long ResidentBytes, double VideoBytes, int DrawCalls, int Primitives,
    int WorkersActive, int MachinesActive, PerformanceRun.Part Part, int Leg, long Pipelines);
