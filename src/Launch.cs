using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The command line, read once: fetching it from the engine allocates a fresh array each time.</summary>
public static class Launch {
    public static readonly string[] Args = OS.GetCmdlineUserArgs();
    // Review fixtures and UI checks keep their own files so they never touch the player's saves or settings.
    public static readonly string FilePrefix = Has("--performance-run") ? "performance-" : Has("--showcase") ? "review-" : Has("--ui-test") ? "ui-check-" : Has("--net-check") ? "net-check-" : "";

    public static bool Has(string option) => Args.Contains(option);

    /// <summary>The launch argument after <paramref name="option"/>, or null when it is missing.</summary>
    public static string? Value(string option) {
        int at = Array.IndexOf(Args, option);
        return at >= 0 && at + 1 < Args.Length ? Args[at + 1] : null;
    }
}
