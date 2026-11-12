using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The command line, read once: fetching it from the engine allocates a fresh array each time.</summary>
public static class Launch {
    public static readonly string[] Args = OS.GetCmdlineUserArgs();
    // Review fixtures, driven play and UI checks keep their own files so they never touch the player's saves or settings.
    // A server started by a window writes its saves where that window reads them.
    public static readonly string FilePrefix = Value("--file-prefix") ?? (Has("--drive") ? "playtest-" : Has("--performance-run") ? "performance-" : Has("--showcase") || Has("--render-intro") ? "review-" : Has("--ui-test") ? "ui-check-" : Has("--net-check") ? "net-check-" : Has("--server-check") ? "server-check-" : "");

    public static bool Has(string option) => Args.Contains(option);

    /// <summary>The launch argument after <paramref name="option"/>, or null when it is missing.</summary>
    public static string? Value(string option) {
        int at = Array.IndexOf(Args, option);
        return at >= 0 && at + 1 < Args.Length ? Args[at + 1] : null;
    }

    /// <summary>The number after <paramref name="option"/>, or <paramref name="fallback"/> when it is missing or not a number.</summary>
    public static float Number(string option, float fallback) =>
        float.TryParse(Value(option), System.Globalization.CultureInfo.InvariantCulture, out float number) ? number : fallback;

    /// <summary>The size after <paramref name="option"/>, written as 2560x1440; null when it is missing, with an error when it is malformed.</summary>
    public static Vector2I? Size(string option) {
        if (Value(option) is not string value) return null;
        var parts = value.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height)) return new Vector2I(width, height);
        GD.PushError($"{option} expects a size such as 2560x1440, not {value}");
        return null;
    }
}
