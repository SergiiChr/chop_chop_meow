using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
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
public sealed class Snapshot : Dictionary<EntryKey, byte[]> {
    /// <summary>Whether two entries hold the same bytes, shared arrays first.</summary>
    public static bool Same(byte[] a, ReadOnlySpan<byte> b) => a.AsSpan() == b || a.AsSpan().SequenceEqual(b);
}

/// <summary>
/// Turns the world into snapshots and the difference between two snapshots into a compressed update, and back.
/// It reuses its buffers and snapshot tables, and an entry that has not changed since the last snapshot keeps its array, so only what changed allocates.
/// </summary>
public sealed class WorldUpdates {
    private static readonly JsonSerializerOptions Compact = new(GameData.Json) { WriteIndented = false };
    // Lists merged by identity, the fog each machine keeps itself, every player of the save and whose it is, and the player properties, which the players list carries.
    public static readonly HashSet<string> Shared = new[] { nameof(SaveState.Things), nameof(SaveState.Workers), nameof(SaveState.Wolves), nameof(SaveState.Orders), nameof(SaveState.Explored), nameof(SaveState.Remembered), nameof(SaveState.Party), nameof(SaveState.Owner) }
        .Concat(SaveState.ActorFields).ToHashSet();
    // The world's own fields, without the lists and players that have entries of their own, or the fog, which each machine works out from where everyone stands.
    private static readonly JsonSerializerOptions Rest = new(Compact) {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver {
            Modifiers = {
                info => {
                    if (info.Type != typeof(SaveState)) return;
                    foreach (var p in info.Properties.Where(p => Shared.Contains(p.Name)).ToList()) info.Properties.Remove(p);
                    // A whole fog map for every update's state would be wasted: a state entry does not carry one.
                    info.CreateObject = static () => new SaveState([]);
                }
            }
        }
    };
    private static readonly JsonTypeInfo<SaveState> StateInfo = (JsonTypeInfo<SaveState>)Rest.GetTypeInfo(typeof(SaveState));

    private readonly Stack<Snapshot> spare = new();
    private readonly ArrayBufferWriter<byte> entry = new(4096), update = new(1 << 16), merged = new(4096);
    private readonly Utf8JsonWriter entryWriter, updateWriter, mergedWriter;
    private byte[] packed = new byte[1 << 16], unpacked = new byte[1 << 16];
    /// <summary>Largest decompressed message a guest accepts.</summary>
    private const int MaxUpdate = 1 << 28;
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
        snapshot.Clear();
        spare.Push(snapshot);
    }

    public Snapshot Empty() => spare.Count > 0 ? spare.Pop() : new Snapshot();

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
        snapshot[key] = last != null && last.TryGetValue(key, out var before) && Snapshot.Same(before, written) ? before : written.ToArray();
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
        foreach (var (key, value) in now) {
            byte[]? before = null;
            if (known != null && known.TryGetValue(key, out before) && Snapshot.Same(before, value)) continue;
            if (before != null && WritePatch(key, value, before)) continue;
            updateWriter.WritePropertyName(name[..key.Format(name, false)]);
            updateWriter.WriteRawValue(value, skipInputValidation: true);
        }
        if (known != null)
            foreach (var key in known.Keys)
                if (!now.ContainsKey(key)) updateWriter.WriteNull(name[..key.Format(name, false)]);
        updateWriter.WriteEndObject();
        updateWriter.Flush();
        return Compress(update.WrittenSpan, ref packed);
    }

    /// <summary>Compresses into <paramref name="output"/>, growing it when needed, and returns the compressed part.</summary>
    private static ReadOnlySpan<byte> Compress(ReadOnlySpan<byte> json, ref byte[] output) {
        int room = BrotliEncoder.GetMaxCompressedLength(json.Length);
        if (output.Length < room) output = new byte[room];
        if (!BrotliEncoder.TryCompress(json, output, out int written, quality: 1, window: 22)) throw new InvalidOperationException("A message did not compress.");
        return output.AsSpan(0, written);
    }

    /// <summary>Decompresses into <paramref name="output"/>, growing it up to a limit no real message reaches, and returns the decompressed part.</summary>
    private static ReadOnlySpan<byte> Decompress(ReadOnlySpan<byte> packed, ref byte[] output) {
        int length;
        // A buffer too small and corrupt data both fail, so the buffer grows only up to the limit.
        while (!BrotliDecoder.TryDecompress(packed, output, out length)) {
            if (output.Length >= MaxUpdate) { output = new byte[1 << 16]; throw new JsonException("A message did not decompress."); }
            output = new byte[Math.Max(output.Length * 2, 1 << 16)];
        }
        return output.AsSpan(0, length);
    }

    /// <summary>
    /// Writes only the changed fields of an object entry, unless it is not an object or its fields are not the ones it had.
    /// Entries leave out fields at their default, so a field coming or going sends the whole entry, which keeps the guest's copy in the host's field order.
    /// </summary>
    private bool WritePatch(EntryKey key, byte[] now, byte[] then) {
        if (!Fields(now, nowFields) || !Fields(then, thenFields) || nowFields.Count != thenFields.Count) return false;
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
        var json = Decompress(update, ref unpacked);
        var snapshot = Empty();
        if (baseline != null) foreach (var (key, value) in baseline) snapshot[key] = value;
        var reader = new Utf8JsonReader(json);
        reader.Read();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName) {
            if (!EntryKey.TryParse(reader.ValueSpan, out var key, out bool patched)) throw new JsonException("Unknown update entry.");
            reader.Read();
            int start = (int)reader.TokenStartIndex;
            bool gone = reader.TokenType == JsonTokenType.Null;
            reader.Skip();
            var value = json[start..(int)reader.BytesConsumed];
            if (gone) snapshot.Remove(key);
            else if (!patched) snapshot[key] = value.ToArray();
            else snapshot[key] = snapshot.TryGetValue(key, out var entry) ? Patch(entry, value) : throw new JsonException("A patch names an entry the update builds on does not have.");
        }
        return snapshot;
    }

    /// <summary>An object entry with the fields of <paramref name="changes"/> replaced.</summary>
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

    /// <summary>The number properties of a type, which an update can set without reading a new object.</summary>
    private static class Numbers<T> where T : class {
        public delegate void Setter(T target, ref Utf8JsonReader value);
        public static readonly (byte[] Name, Setter Set)[] All = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } && (p.PropertyType == typeof(float) || p.PropertyType == typeof(int)))
            .Select(p => (Encoding.UTF8.GetBytes(p.Name), Make(p))).ToArray();

        private static Setter Make(PropertyInfo property) {
            if (property.PropertyType == typeof(float)) { var set = property.SetMethod!.CreateDelegate<Action<T, float>>(); return (T target, ref Utf8JsonReader value) => set(target, value.GetSingle()); }
            var whole = property.SetMethod!.CreateDelegate<Action<T, int>>();
            return (T target, ref Utf8JsonReader value) => whole(target, value.GetInt32());
        }

        /// <summary>The place in <see cref="All"/> of the number property with this name, or -1.</summary>
        public static int Of(ReadOnlySpan<byte> name) {
            for (int i = 0; i < All.Length; i++) if (All[i].Name.AsSpan().SequenceEqual(name)) return i;
            return -1;
        }
    }

    /// <summary>
    /// Brings <paramref name="target"/> from entry <paramref name="then"/> to <paramref name="now"/> in place, without allocating, when the same fields are there and only numbers differ.
    /// Entries leave out fields at their default, so a number reaching or leaving its default, or any other change, returns false and changes nothing: the entry must be read whole.
    /// </summary>
    public bool SetNumbers<T>(T target, byte[] then, byte[] now) where T : class {
        if (!Fields(now, nowFields) || !Fields(then, thenFields) || nowFields.Count != thenFields.Count) return false;
        foreach (var field in nowFields) {
            int was = Find(then, thenFields, Name(now, field));
            if (was < 0) return false;
            if (!Value(then, thenFields[was]).SequenceEqual(Value(now, field)) && Numbers<T>.Of(Name(now, field)) < 0) return false;
        }
        foreach (var field in nowFields) {
            if (Value(then, thenFields[Find(then, thenFields, Name(now, field))]).SequenceEqual(Value(now, field))) continue;
            var reader = new Utf8JsonReader(Value(now, field));
            reader.Read();
            Numbers<T>.All[Numbers<T>.Of(Name(now, field))].Set(target, ref reader);
        }
        return true;
    }

    /// <summary>The world's own fields from a state entry, in a new object without a fog map.</summary>
    public static SaveState ReadState(byte[] json) => JsonSerializer.Deserialize(json, StateInfo) ?? throw new JsonException("Empty state");

    public static T Read<T>(byte[] json) => JsonSerializer.Deserialize<T>(json, Compact) ?? throw new JsonException("Empty entry");

    /// <summary>Packs a value whole, for the welcome's fog.</summary>
    public static byte[] Pack<T>(T value) {
        var output = Array.Empty<byte>();
        return Compress(JsonSerializer.SerializeToUtf8Bytes(value, Compact), ref output).ToArray();
    }

    public static T Unpack<T>(byte[] packed) {
        var output = new byte[Math.Max(packed.Length * 4, 1 << 16)];
        return JsonSerializer.Deserialize<T>(Decompress(packed, ref output), Compact) ?? throw new JsonException("Empty message");
    }
}
