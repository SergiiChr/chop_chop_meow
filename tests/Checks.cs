using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ChopChopMeow;
using Godot;

/// <summary>Shared assertion and time limit for simulation, model, UI and network checks.</summary>
internal static class Checks {
    /// <summary>How long one check may run, and one tagged slow; past it the process stops, since the check has locked up.</summary>
    public const double TestLimit = 20, SlowTestLimit = 180;

    private static readonly Lock running = new();
    private static string current = "";
    private static double limit;
    private static string waiting = "";
    private static readonly Stopwatch clock = new();
    // Checks the running test from another thread, so it catches a check stuck in a loop on the main thread too.
    private static readonly System.Threading.Timer watchdog = new(_ => StopIfOverdue(), null, 500, 500);

    /// <summary>Throws with <paramref name="message"/>, prefixed by the calling check file's name, when <paramref name="condition"/> is false.</summary>
    public static void Require([DoesNotReturnIf(false)] bool condition, string message, [CallerFilePath] string file = "") {
        if (!condition) throw new InvalidOperationException($"{Path.GetFileNameWithoutExtension(file)} failed: {message}");
    }

    /// <summary>Runs one check within <see cref="TestLimit"/>, or <see cref="SlowTestLimit"/> when tagged slow, and logs how long it took.</summary>
    public static void Test(Action check, bool slow = false, [CallerArgumentExpression(nameof(check))] string name = "") {
        Start(name, slow);
        try { check(); } finally { Finish(); }
    }
    public static void Test(Action<GameData> check, GameData data, bool slow = false, [CallerArgumentExpression(nameof(check))] string name = "") =>
        Test(() => check(data), slow, name);
    public static async Task Test(Func<Task> check, bool slow = false, [CallerArgumentExpression(nameof(check))] string name = "") {
        Start(name, slow);
        try { await check(); } finally { Finish(); }
    }
    public static async Task<T> Test<T>(Func<Task<T>> check, bool slow = false, [CallerArgumentExpression(nameof(check))] string name = "") {
        Start(name, slow);
        try { return await check(); } finally { Finish(); }
    }

    /// <summary>What the running check waits for, named when it runs out of time; empty when it waits for nothing.</summary>
    public static void WaitingFor(string what) { lock (running) waiting = what; }

    private static void Start(string name, bool slow) {
        lock (running) { current = name; waiting = ""; limit = slow ? SlowTestLimit : TestLimit; clock.Restart(); }
    }
    private static void Finish() {
        string name; double seconds;
        lock (running) { name = current; seconds = clock.Elapsed.TotalSeconds; current = ""; }
        GD.Print($"test {seconds:0.00}s: {name}");
    }
    private static void StopIfOverdue() {
        lock (running) {
            if (current == "" || clock.Elapsed.TotalSeconds <= limit) return;
            // Godot buffers its own output, so the message goes straight to the process's output before it stops.
            Console.WriteLine($"ERROR: {current} ran past its {limit}s limit{(waiting == "" ? "" : $" waiting for {waiting}")}");
            Console.Out.Flush();
            Process.GetCurrentProcess().Kill();
        }
    }
}
