using System;
using System.Buffers.Text;
using System.IO;

namespace ChopChopMeow;

/// <summary>
/// Reads the process's resident memory from Linux's /proc/self/statm without allocating,
/// so sampling it every frame leaves the measured frames alone.
/// Godot's own memory monitors count only in debug builds.
/// </summary>
internal sealed class ResidentMemory : IDisposable {
    private readonly FileStream file = new("/proc/self/statm", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 0);
    private readonly byte[] text = new byte[128];
    public long Bytes() {
        file.Position = 0;
        var line = text.AsSpan(0, file.Read(text));
        // The line lists the process's total pages, then its resident pages.
        Utf8Parser.TryParse(line[(line.IndexOf((byte)' ') + 1)..], out long pages, out _);
        return pages * Environment.SystemPageSize;
    }
    public void Dispose() => file.Dispose();
}
