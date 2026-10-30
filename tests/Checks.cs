using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

/// <summary>Shared assertion for simulation, model and UI checks.</summary>
internal static class Checks {
    /// <summary>Throws with <paramref name="message"/>, prefixed by the calling check file's name, when <paramref name="condition"/> is false.</summary>
    public static void Require([DoesNotReturnIf(false)] bool condition, string message, [CallerFilePath] string file = "") {
        if (!condition) throw new InvalidOperationException($"{Path.GetFileNameWithoutExtension(file)} failed: {message}");
    }
}
