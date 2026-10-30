using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ChopChopMeow;

/// <summary>Names one entry of a world update: a thing, worker, wolf, trade order or player by its id, or one of the two shared entries.</summary>
public readonly record struct EntryKey(byte Prefix, int Id) {
    public const byte Thing = (byte)'t', Worker = (byte)'w', Wolf = (byte)'f', Order = (byte)'o', Player = (byte)'p', State = (byte)'s', Work = (byte)'k';
    /// <summary>A changed entry that carries only its changed fields is named with this in front.</summary>
    public const byte Patched = (byte)'~';
    /// <summary>Longest name: a patch mark, a prefix, a colon and an int.</summary>
    public const int MaxName = 14;

    /// <summary>Writes the name, such as "t:12", and returns its length.</summary>
    public int Format(Span<byte> name, bool patched) {
        int at = 0;
        if (patched) name[at++] = Patched;
        name[at++] = Prefix; name[at++] = (byte)':';
        Utf8Formatter.TryFormat(Id, name[at..], out int written);
        return at + written;
    }

    public static bool TryParse(ReadOnlySpan<byte> name, out EntryKey key, out bool patched) {
        patched = name.Length > 0 && name[0] == Patched;
        if (patched) name = name[1..];
        key = default;
        if (name.Length < 3 || name[1] != (byte)':' || !Utf8Parser.TryParse(name[2..], out int id, out int used) || used != name.Length - 2) return false;
        key = new EntryKey(name[0], id);
        return true;
    }
}

/// <summary>The world as entries of compact JSON that can be compared one by one. Arrays are never changed once made, so snapshots share the unchanged ones.</summary>
public sealed class Snapshot {
    public readonly Dictionary<EntryKey, byte[]> Entries = new();
}

/// <summary>
/// Turns the world into snapshots and the difference between two snapshots into a compressed update, and back.
/// It reuses its buffers and snapshot tables, and an entry that has not changed since the last snapshot keeps its array, so only what changed allocates.
/// </summary>
public sealed class WorldUpdates {
    private static readonly JsonSerializerOptions Compact = new(GameData.Json) { WriteIndented = false };
    // Lists merged by identity, the fog each machine keeps itself, and the player properties, which the players list carries.
    // The world's crate contents share a name with a player's, which only a guest's own save uses, and stay in.
    public static readonly HashSet<string> Shared = new[] { nameof(SaveState.Things), nameof(SaveState.Workers), nameof(SaveState.Wolves), nameof(SaveState.Orders), nameof(SaveState.Explored), nameof(SaveState.Remembered) }
        .Concat(typeof(PlayerState).GetProperties().Select(p => p.Name).Where(n => n != nameof(SaveState.Crates))).ToHashSet();
    // The world's own fields, without the lists and players that have entries of their own, or the fog, which each machine works out from where everyone stands.
    private static readonly JsonSerializerOptions Rest = new(Compact) {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver {
            Modifiers = { info => { if (info.Type == typeof(SaveState)) foreach (var p in info.Properties.Where(p => Shared.Contains(p.Name)).ToList()) info.Properties.Remove(p); } }
        }
    };
    private static readonly JsonTypeInfo<SaveState> StateInfo = (JsonTypeInfo<SaveState>)Rest.GetTypeInfo(typeof(SaveState));

    private readonly Stack<Snapshot> spare = new();
    private readonly ArrayBufferWriter<byte> entry = new(4096), update = new(1 << 16), merged = new(4096);
    private readonly Utf8JsonWriter entryWriter, updateWriter, mergedWriter;
    private byte[] packed = new byte[1 << 16], unpacked = new byte[1 << 16];
    private readonly List<Range> nowFields = new(), thenFields = new(), patchFields = new();
    // The snapshot taken last, whose arrays the next one keeps where nothing changed.
    private Snapshot? last;

    public WorldUpdates() {
        var options = new JsonWriterOptions { SkipValidation = true };
        entryWriter = new Utf8JsonWriter(entry, options); updateWriter = new Utf8JsonWriter(update, options); mergedWriter = new Utf8JsonWriter(merged, options);
    }

    /// <summary>A snapshot no update needs any more goes back to be reused.</summary>
    public void Release(Snapshot snapshot) {
        if (snapshot == last) last = null;
        snapshot.Entries.Clear();
        spare.Push(snapshot);
    }

    public Snapshot Empty() {
        var snapshot = spare.Count > 0 ? spare.Pop() : new Snapshot();
        snapshot.Entries.Clear();
        return snapshot;
    }

    /// <summary>The world as entries: each thing, worker, wolf, trade order and player under its id, the rest of the world, and what each worker is doing.</summary>
    public Snapshot Capture(World world) {
        var s = world.S;
        world.NumberWolvesAndOrders();
        var snapshot = Empty();
        Write(snapshot, new EntryKey(EntryKey.State, 0), s, StateInfo);
        entry.ResetWrittenCount(); entryWriter.Reset(entry);
        entryWriter.WriteStartObject();
        Span<byte> name = stackalloc byte[EntryKey.MaxName];
        foreach (var w in s.Workers) {
            Utf8Formatter.TryFormat(w.Id, name, out int length);
            entryWriter.WriteNumber(name[..length], (int)w.WorkState);
        }
        entryWriter.WriteEndObject();
        Keep(snapshot, new EntryKey(EntryKey.Work, 0));
        foreach (var t in s.Things) Write(snapshot, new EntryKey(EntryKey.Thing, t.Id), t, UpdateJson.Default.Thing);
        foreach (var w in s.Workers) Write(snapshot, new EntryKey(EntryKey.Worker, w.Id), w, UpdateJson.Default.Worker);
        foreach (var w in s.Wolves) Write(snapshot, new EntryKey(EntryKey.Wolf, w.Id), w, UpdateJson.Default.Wolf);
        foreach (var o in s.Orders) Write(snapshot, new EntryKey(EntryKey.Order, o.Id), o, UpdateJson.Default.TradeOrder);
        foreach (var p in s.Players) Write(snapshot, new EntryKey(EntryKey.Player, p.Id), p, UpdateJson.Default.PlayerState);
        last = snapshot;
        return snapshot;
    }

    private void Write<T>(Snapshot snapshot, EntryKey key, T value, JsonTypeInfo<T> info) {
        entry.ResetWrittenCount(); entryWriter.Reset(entry);
        JsonSerializer.Serialize(entryWriter, value, info);
        Keep(snapshot, key);
    }

    /// <summary>Stores the entry just written, as the last snapshot's array when it holds the same bytes.</summary>
    private void Keep(Snapshot snapshot, EntryKey key) {
        entryWriter.Flush();
        var written = entry.WrittenSpan;
        snapshot.Entries[key] = last != null && last.Entries.TryGetValue(key, out var before) && before.AsSpan().SequenceEqual(written) ? before : written.ToArray();
    }

    /// <summary>
    /// The entries of <paramref name="now"/> that differ from <paramref name="known"/>, all of them without it, with null for each entry that is gone, compressed.
    /// A changed entry only carries its changed fields, under its name with <see cref="EntryKey.Patched"/> in front.
    /// The span is valid until the next call.
    /// </summary>
    public ReadOnlySpan<byte> Delta(Snapshot now, Snapshot? known) {
        update.ResetWrittenCount(); updateWriter.Reset(update);
        updateWriter.WriteStartObject();
        Span<byte> name = stackalloc byte[EntryKey.MaxName];
        foreach (var (key, value) in now.Entries) {
            byte[]? before = null;
            if (known != null && known.Entries.TryGetValue(key, out before) && (ReferenceEquals(before, value) || before.AsSpan().SequenceEqual(value))) continue;
            if (before != null && WritePatch(key, value, before)) continue;
            updateWriter.WritePropertyName(name[..key.Format(name, false)]);
            updateWriter.WriteRawValue(value, skipInputValidation: true);
        }
        if (known != null)
            foreach (var key in known.Entries.Keys)
                if (!now.Entries.ContainsKey(key)) updateWriter.WriteNull(name[..key.Format(name, false)]);
        updateWriter.WriteEndObject();
        updateWriter.Flush();
        var json = update.WrittenSpan;
        int room = BrotliEncoder.GetMaxCompressedLength(json.Length);
        if (packed.Length < room) packed = new byte[room];
        if (!BrotliEncoder.TryCompress(json, packed, out int written, quality: 1, window: 22)) throw new InvalidOperationException("An update did not compress.");
        return packed.AsSpan(0, written);
    }

    /// <summary>Writes only the changed fields of an object entry, unless a field it had is gone or it is not an object.</summary>
    private bool WritePatch(EntryKey key, byte[] now, byte[] then) {
        if (!Fields(now, nowFields) || !Fields(then, thenFields)) return false;
        foreach (var field in thenFields) if (Find(now, nowFields, Name(then, field)) < 0) return false;
        Span<byte> name = stackalloc byte[EntryKey.MaxName];
        updateWriter.WriteStartObject(name[..key.Format(name, true)]);
        foreach (var field in nowFields) {
            var fieldName = Name(now, field);
            int was = Find(then, thenFields, fieldName);
            if (was >= 0 && Value(then, thenFields[was]).SequenceEqual(Value(now, field))) continue;
            updateWriter.WritePropertyName(fieldName);
            updateWriter.WriteRawValue(Value(now, field), skipInputValidation: true);
        }
        updateWriter.WriteEndObject();
        return true;
    }

    /// <summary>
    /// The entries an update brings, starting from the ones of <paramref name="baseline"/>, or from nothing when the update is the whole world.
    /// Untouched entries keep the baseline's arrays.
    /// </summary>
    public Snapshot Merge(Snapshot? baseline, ReadOnlySpan<byte> update) {
        int length;
        while (!BrotliDecoder.TryDecompress(update, unpacked, out length)) unpacked = new byte[unpacked.Length * 2];
        var snapshot = Empty();
        if (baseline != null) foreach (var (key, value) in baseline.Entries) snapshot.Entries[key] = value;
        var json = unpacked.AsSpan(0, length);
        var reader = new Utf8JsonReader(json);
        reader.Read();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName) {
            if (!EntryKey.TryParse(reader.ValueSpan, out var key, out bool patched)) throw new JsonException("Unknown update entry.");
            reader.Read();
            int start = (int)reader.TokenStartIndex;
            bool gone = reader.TokenType == JsonTokenType.Null;
            reader.Skip();
            var value = json[start..(int)reader.BytesConsumed];
            if (gone) snapshot.Entries.Remove(key);
            else snapshot.Entries[key] = patched ? Patch(snapshot.Entries[key], value) : value.ToArray();
        }
        return snapshot;
    }

    /// <summary>An object entry with the fields of <paramref name="changes"/> replaced or added.</summary>
    private byte[] Patch(byte[] entry, ReadOnlySpan<byte> change) {
        if (!Fields(entry, thenFields) || !Fields(change, patchFields)) throw new JsonException("A patch must change an object entry.");
        merged.ResetWrittenCount(); mergedWriter.Reset(merged);
        mergedWriter.WriteStartObject();
        foreach (var field in thenFields) {
            var fieldName = Name(entry, field);
            int now = Find(change, patchFields, fieldName);
            mergedWriter.WritePropertyName(fieldName);
            mergedWriter.WriteRawValue(now >= 0 ? Value(change, patchFields[now]) : Value(entry, field), skipInputValidation: true);
        }
        foreach (var field in patchFields) {
            var fieldName = Name(change, field);
            if (Find(entry, thenFields, fieldName) >= 0) continue;
            mergedWriter.WritePropertyName(fieldName);
            mergedWriter.WriteRawValue(Value(change, field), skipInputValidation: true);
        }
        mergedWriter.WriteEndObject();
        mergedWriter.Flush();
        return merged.WrittenSpan.ToArray();
    }

    // Where a field's name and raw value lie in its entry.
    private readonly record struct Range(int NameStart, int NameLength, int ValueStart, int ValueLength);

    /// <summary>Lists an object entry's fields, or returns false when the entry is not an object.</summary>
    private static bool Fields(ReadOnlySpan<byte> json, List<Range> fields) {
        fields.Clear();
        var reader = new Utf8JsonReader(json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName) {
            int nameStart = (int)reader.TokenStartIndex + 1, nameLength = reader.ValueSpan.Length;
            reader.Read();
            int valueStart = (int)reader.TokenStartIndex;
            reader.Skip();
            fields.Add(new Range(nameStart, nameLength, valueStart, (int)reader.BytesConsumed - valueStart));
        }
        return true;
    }

    private static ReadOnlySpan<byte> Name(ReadOnlySpan<byte> json, Range field) => json.Slice(field.NameStart, field.NameLength);
    private static ReadOnlySpan<byte> Value(ReadOnlySpan<byte> json, Range field) => json.Slice(field.ValueStart, field.ValueLength);

    private static int Find(ReadOnlySpan<byte> json, List<Range> fields, ReadOnlySpan<byte> name) {
        for (int i = 0; i < fields.Count; i++) if (Name(json, fields[i]).SequenceEqual(name)) return i;
        return -1;
    }

    public static T Read<T>(byte[] json) => JsonSerializer.Deserialize<T>(json, Compact) ?? throw new JsonException("Empty entry");

    /// <summary>Packs a value whole, for the welcome's fog.</summary>
    public static byte[] Pack<T>(T value) {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, Compact);
        var output = new byte[BrotliEncoder.GetMaxCompressedLength(json.Length)];
        BrotliEncoder.TryCompress(json, output, out int written, quality: 1, window: 22);
        return output[..written];
    }

    public static T Unpack<T>(byte[] packed) {
        using var brotli = new BrotliStream(new System.IO.MemoryStream(packed), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<T>(brotli, Compact) ?? throw new JsonException("Empty message");
    }
}
