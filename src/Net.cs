using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Godot;

namespace ChopChopMeow;

/// <summary>The fog the host has uncovered so far, which a joining guest starts from; after that each machine works it out itself.</summary>
internal sealed class Fog {
    public byte[] Explored { get; set; } = [];
    public Dictionary<int, Thing> Remembered { get; set; } = new();
}

/// <summary>
/// Host-client multiplayer over ENet; the connection handling comes from This Scalpel Is Mine.
/// The host's <see cref="World"/> is the only simulation. Ten times a second it sends each guest what changed since the last update that guest confirmed.
/// Updates are unreliable, so a lost one never holds up the next; the next simply carries its changes too.
/// A guest merges them into its copy in place, so open panels and the view keep their objects, and glides what moves.
/// A guest walks its own player and streams where it stands; any other action goes to the host as a <see cref="Command"/>.
/// Joining sends the guest's player as saved on its machine, which the host checks before letting it in.
/// Leaving asks the host for the guest's player as the host last had it, so nothing in flight is lost or kept twice.
/// </summary>
public partial class Net : Node {
    public const int DefaultPort = 24565;
    public const int MaxGuests = 3;
    /// <summary>ENet drops a peer once a reliable packet stays unanswered this long (or TimeoutMinMsec after TimeoutLimit resends).</summary>
    private const int TimeoutLimit = 64, TimeoutMinMsec = 15000, TimeoutMaxMsec = 45000;
    /// <summary>Joining gives up on a host that never answers after this long, instead of waiting out the full timeout.</summary>
    private const float ConnectTimeout = 10;
    private const float SnapshotInterval = .1f, InputInterval = .05f;
    private const int HostId = 1;
    /// <summary>Raised whenever the messages change, so mismatched builds refuse each other.</summary>
    private const int Protocol = 3;
    /// <summary>Furthest anything glides; anything further jumps.</summary>
    private const float MaxStep = 3;
    /// <summary>Updates both sides remember; a guest that confirmed none of the last this many gets the whole world again.</summary>
    private const int History = 64;
    /// <summary>Largest update sent unreliably; ENet splits anything bigger, and losing any piece loses all of it.</summary>
    private const int PacketSize = 1200;
    /// <summary>A guest saves its player this often while it changes, so a crash loses little.</summary>
    private const float SaveInterval = 2;
    /// <summary>How long leaving waits for the host to hand back the guest's player.</summary>
    private const int FarewellMsec = 2000;
    // Updates and the steady streams go on their own channels: unreliable messages wait behind a lost reliable one on the same channel.
    private const int StreamChannel = 1, BulkChannel = 2;

    public World World = null!;
    /// <summary>Put in front of the guest save file names, so checks never touch a player's saves.</summary>
    public string FilePrefix = "";
    public event Action<string>? Said;
    public event Action<string, Vector2>? Heard;
    public event Action<int, ActorAction, Vector2>? Acted;
    public event Action? Joined;
    /// <summary>A joined game ended; the reason says why.</summary>
    public event Action<string>? Left;
    public event Action<string>? Failed;

    private string address = "";
    private bool welcomed, leaving, unsaved;
    private float sendClock, saveClock;
    private string? farewell;
    // Guest: counts what it sends, so the host can tell a position report from before an action.
    private int stamp;
    private SceneTreeTimer? connectTimer;
    // A guest's copy glides workers, wolves and the other players toward where the host last put them.
    private readonly Dictionary<object, Vector2> goals = new();
    // The host glides each guest toward where it says it stands, while no respawn or other move by the simulation has come since.
    private readonly Dictionary<int, (Vector2 At, int Warp)> walks = new();
    // Host: the latest stamp heard from each guest.
    private readonly Dictionary<int, int> heard = new();
    // Host: the world by entry as each recent update had it, and the last update each guest confirmed.
    private int sequence;
    private readonly Dictionary<int, Dictionary<string, string>> sent = new();
    private readonly Dictionary<int, int> acked = new();
    // Guest: the world by entry as recent updates had it, and the update its copy shows now.
    private readonly Dictionary<int, Dictionary<string, string>> received = new();
    private Dictionary<string, string> applied = new();
    private int appliedSequence;
    private Dictionary<int, WorkerWorkState> work = new();

    private static readonly JsonSerializerOptions Compact = new(GameData.Json) { WriteIndented = false };
    // Lists merged by identity, the fog each machine keeps itself, and the player properties, which the players list carries.
    // The world's crate contents share a name with a player's, which only a guest's own save uses, and stay in.
    private static readonly HashSet<string> Shared = new[] { nameof(SaveState.Things), nameof(SaveState.Workers), nameof(SaveState.Wolves), nameof(SaveState.Orders), nameof(SaveState.Explored), nameof(SaveState.Remembered) }
        .Concat(typeof(PlayerState).GetProperties().Select(p => p.Name).Where(n => n != nameof(SaveState.Crates))).ToHashSet();
    // The world's own fields, without the lists and players that have entries of their own, or the fog, which each machine works out from where everyone stands.
    private static readonly JsonSerializerOptions Rest = new(Compact) {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver {
            Modifiers = { info => { if (info.Type == typeof(SaveState)) foreach (var p in info.Properties.Where(p => Shared.Contains(p.Name)).ToList()) info.Properties.Remove(p); } }
        }
    };

    public bool Online => Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer;
    /// <summary>Kept apart from IsServer, which errors once a lost connection has closed the peer.</summary>
    public bool Hosting { get; private set; }
    public bool Connecting => Online && !Hosting && !welcomed;
    /// <summary>The host's address as typed, set once joined, for the popup to offer next time.</summary>
    public string Address { get; private set; } = "";
    public int Guests => Hosting ? World.S.Players.Count - 1 : 0;

    public override void _Ready() {
        // Guests only hear about each other from the host's messages and updates.
        if (Multiplayer is SceneMultiplayer scene) scene.ServerRelay = false;
        Multiplayer.PeerDisconnected += id => { if (Hosting) Forget((int)id); };
        Multiplayer.ConnectedToServer += () => { connectTimer = null; RpcId(HostId, MethodName.Hello, Protocol, LoadGuest()); };
        Multiplayer.ConnectionFailed += () => Fail("Could not reach the host.");
        Multiplayer.ServerDisconnected += () => End("Lost the connection to the host.");
        Multiplayer.PeerConnected += id => { if (Hosting && Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet) TolerateLag(enet.GetPeer((int)id)); };
    }

    private static void TolerateLag(ENetPacketPeer peer) => peer.SetTimeout(TimeoutLimit, TimeoutMinMsec, TimeoutMaxMsec);

    /// <summary>Opens the current game to guests.</summary>
    public Error Host(int port = DefaultPort) {
        Leave();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, MaxGuests);
        if (error != Error.Ok) return error;
        Multiplayer.MultiplayerPeer = peer; Hosting = true;
        World.Relay = Tell;
        return Error.Ok;
    }

    public Error Join(string ip, int port = DefaultPort) {
        Leave();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(ip, port);
        if (error != Error.Ok) return error;
        if (peer.GetPeer(HostId) is { } host) TolerateLag(host);
        Multiplayer.MultiplayerPeer = peer;
        address = $"{ip}:{port}"; Address = ip;
        // The connect attempt itself would wait out the whole lag timeout; a host that never answers fails sooner.
        var timer = GetTree().CreateTimer(ConnectTimeout); connectTimer = timer;
        timer.Timeout += () => { if (timer == connectTimer && Connecting) Fail($"The host didn't answer within {ConnectTimeout:0} seconds."); };
        return Error.Ok;
    }

    /// <summary>Ends hosting or a joined game; a guest saves its player first, as the host has it when the line still works.</summary>
    public void Leave() {
        if (leaving) return;
        leaving = true;
        if (welcomed && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected) {
            // Actions already sent arrive before this, so the player that comes back has them all.
            farewell = null; RpcId(HostId, MethodName.Bye);
            for (ulong until = Time.GetTicksMsec() + FarewellMsec; farewell == null && Time.GetTicksMsec() < until; OS.DelayMsec(5)) Multiplayer.Poll();
        }
        SaveGuest(farewell);
        if (Hosting) foreach (var guest in World.S.Players.Where(p => p != World.Me).ToList()) World.RemoveGuest(guest.Id);
        if (Online) Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer(); Hosting = false;
        World.Host = null; World.Relay = null; World.LocalId = 1;
        welcomed = unsaved = false; connectTimer = null; farewell = null; stamp = 0; goals.Clear(); walks.Clear(); heard.Clear();
        sequence = 0; sent.Clear(); acked.Clear(); received.Clear(); applied = new(); appliedSequence = 0; work = new();
        leaving = false;
    }

    /// <summary>Host: a guest has gone, by saying goodbye or losing the line.</summary>
    private void Forget(int id) {
        walks.Remove(id); acked.Remove(id); heard.Remove(id); World.RemoveGuest(id);
    }

    private void Fail(string reason) { Leave(); Failed?.Invoke(reason); }

    private void End(string reason) { Leave(); Left?.Invoke(reason); }

    // ---------- Joining ----------

    private string GuestPath => $"user://{FilePrefix}guest-{string.Concat(address.Select(c => char.IsLetterOrDigit(c) ? c : '-'))}.json";

    /// <summary>The player this machine saved for the host it joins, or empty.</summary>
    private string LoadGuest() => Godot.FileAccess.FileExists(GuestPath) ? Godot.FileAccess.GetFileAsString(GuestPath) : "";

    /// <summary>A guest keeps its own player, and what it carries, for the next time it joins this host.</summary>
    /// <param name="player">The player as the host exported it; without it, as this machine's copy has it.</param>
    public void SaveGuest(string? player = null) {
        if (!welcomed || !World.Remote) return;
        using var file = Godot.FileAccess.Open(GuestPath, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(player ?? World.ExportPlayer(World.Me));
        unsaved = false; saveClock = 0;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Hello(int protocol, string saved) {
        int id = Multiplayer.GetRemoteSenderId();
        if (!Hosting || World.S.Players.Any(p => p.Id == id)) return;
        if (protocol != Protocol) { RpcId(id, MethodName.Refused, "The host runs a different version of the game."); return; }
        World.AddGuest(id, saved == "" ? null : saved);
        // The welcome is the guest's first update, which later ones build on.
        var entries = Entries();
        sent[++sequence] = entries; acked[id] = sequence;
        RpcId(id, MethodName.Welcome, id, sequence, Delta(entries, null), Pack(new Fog { Explored = World.S.Explored, Remembered = World.S.Remembered }));
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Refused(string reason) => Fail(reason);

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Welcome(int id, int update, byte[] packed, byte[] fog) {
        var uncovered = Unpack<Fog>(fog);
        // An empty world with a stand-in for this player, which the first update fills in.
        var state = new SaveState { Explored = uncovered.Explored, Remembered = uncovered.Remembered };
        state.Players[0].Id = id;
        World.Mirror(state, state.Players, id);
        welcomed = true;
        Take(update, 0, packed);
        World.Host = Send;
        Joined?.Invoke();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Bye() {
        if (!Hosting || Guest() is not PlayerState p) return;
        RpcId(p.Id, MethodName.Farewell, World.ExportPlayer(p));
        Forget(p.Id);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Farewell(string player) => farewell = player;

    // ---------- Running ----------

    public override void _Process(double delta) {
        if (!Online) return;
        float dt = Math.Min((float)delta, .1f);
        foreach (var (owner, goal) in goals) Place(owner, Glide(Position(owner), goal, dt));
        foreach (var p in World.S.Players)
            if (walks.TryGetValue(p.Id, out var walk) && walk.Warp == p.Warp) World.As(p, () => World.MoveTo(Glide(p.Pos, walk.At, dt)));
        if (World.Remote) World.Look();
        if (unsaved && (saveClock += dt) >= SaveInterval) SaveGuest();
        sendClock += dt;
        if (Hosting && sendClock >= SnapshotInterval) {
            sendClock = 0;
            if (World.S.Players.Count > 1) SendUpdates();
        } else if (welcomed && sendClock >= InputInterval) {
            sendClock = 0;
            var me = World.Me;
            RpcId(HostId, MethodName.Walked, ++stamp, me.Pos, me.Warp, me.GatherTarget, me.Held, me.Sprinting, appliedSequence);
        }
    }

    /// <summary>Most of the way there each frame, so ten updates a second read as steady movement; a long way off is a jump.</summary>
    private static Vector2 Glide(Vector2 from, Vector2 to, float dt) => from.DistanceTo(to) > MaxStep ? to : from.Lerp(to, 1 - MathF.Exp(-dt * 12));

    /// <summary>Sends an action with where this player stands, so the host checks its reach from there rather than from a position still gliding in.</summary>
    private void Send(Command c) => RpcId(HostId, MethodName.Do, ++stamp, World.Me.Pos, World.Me.Warp, c.Verb, c.Target, c.Other, c.Item, c.Count, c.At);

    private PlayerState? Guest() => World.S.Players.Find(p => p.Id == Multiplayer.GetRemoteSenderId() && p != World.Me);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = StreamChannel)]
    private void Walked(int order, Vector2 at, int warp, int gathering, string held, bool sprinting, int seen) {
        if (!Hosting || Guest() is not PlayerState p) return;
        if (sent.ContainsKey(seen) && seen > acked.GetValueOrDefault(p.Id)) acked[p.Id] = seen;
        // An action on the other channel may have overtaken this report and already put the guest further along.
        if (order < heard.GetValueOrDefault(p.Id)) return;
        heard[p.Id] = order;
        Stand(p, at, warp);
        p.GatherTarget = gathering; p.Held = held; p.Sprinting = sprinting;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Do(int order, Vector2 standing, int warp, string verb, int target, int other, string item, int count, Vector2 at) {
        if (!Hosting || Guest() is not PlayerState p) return;
        heard[p.Id] = Math.Max(order, heard.GetValueOrDefault(p.Id));
        // The action happened where the guest stood, so it goes there at once.
        if (Stand(p, standing, warp)) World.As(p, () => World.MoveTo(standing));
        World.Run(p, new Command(verb, target, other, item, count, at));
    }

    /// <summary>
    /// Where a guest says it stands, unless a respawn or other move by the simulation came since.
    /// A long way from the last report, after a stall on the line, is still where it stands: the glide jumps there.
    /// </summary>
    private bool Stand(PlayerState p, Vector2 at, int warp) {
        if (warp != p.Warp) return false;
        walks[p.Id] = (at, warp);
        return true;
    }

    /// <summary>Host: passes a message on to one guest, or all of them.</summary>
    private void Tell(PlayerState? to, string message) {
        foreach (var p in World.S.Players.Where(p => p != World.Me && (to == null || p == to))) RpcId(p.Id, MethodName.Told, message);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Told(string message) => Said?.Invoke(message);

    /// <summary>Host: lets the guests hear a sound of the world.</summary>
    public void Echo(string sound, Vector2 at) {
        if (Hosting) Rpc(MethodName.Hear, sound, at);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable, TransferChannel = StreamChannel)]
    private void Hear(string sound, Vector2 at) { if (welcomed) Heard?.Invoke(sound, at); }

    /// <summary>Host: lets the guests see a player swing, pump or light a torch.</summary>
    public void Echo(int player, ActorAction action, Vector2 at) {
        if (Hosting) Rpc(MethodName.See, player, (int)action, at);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable, TransferChannel = StreamChannel)]
    private void See(int player, int action, Vector2 at) { if (welcomed) Acted?.Invoke(player, (ActorAction)action, at); }

    // ---------- Updates ----------

    private static byte[] Pack<T>(T value) {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest)) JsonSerializer.Serialize(brotli, value, Compact);
        return output.ToArray();
    }

    private static T Unpack<T>(byte[] packed) {
        using var brotli = new BrotliStream(new MemoryStream(packed), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<T>(brotli, Compact) ?? throw new InvalidDataException("Empty message");
    }

    private void Work() {
        foreach (var w in World.S.Workers) w.WorkState = work.GetValueOrDefault(w.Id);
    }

    /// <summary>The world as entries a guest can compare one by one: each thing, worker, wolf, trade order and player under its id, and the rest in two shared entries.</summary>
    private Dictionary<string, string> Entries() {
        var s = World.S;
        World.NumberWolvesAndOrders();
        var entries = new Dictionary<string, string> {
            ["state"] = JsonSerializer.Serialize(s, Rest),
            ["work"] = JsonSerializer.Serialize(s.Workers.ToDictionary(w => w.Id, w => w.WorkState), Compact)
        };
        foreach (var t in s.Things) entries[$"t:{t.Id}"] = JsonSerializer.Serialize(t, Compact);
        foreach (var w in s.Workers) entries[$"w:{w.Id}"] = JsonSerializer.Serialize(w, Compact);
        foreach (var w in s.Wolves) entries[$"f:{w.Id}"] = JsonSerializer.Serialize(w, Compact);
        foreach (var o in s.Orders) entries[$"o:{o.Id}"] = JsonSerializer.Serialize(o, Compact);
        foreach (var p in s.Players) entries[$"p:{p.Id}"] = JsonSerializer.Serialize(p, Compact);
        return entries;
    }

    private void SendUpdates() {
        var entries = Entries();
        sent[++sequence] = entries;
        sent.Remove(sequence - History);
        foreach (var p in World.S.Players.Where(p => p != World.Me)) {
            int baseline = acked.GetValueOrDefault(p.Id);
            var known = sent.GetValueOrDefault(baseline);
            var delta = Delta(entries, known);
            if (known != null && delta.Length <= PacketSize) { RpcId(p.Id, MethodName.Refresh, sequence, baseline, delta); continue; }
            // Big updates go on their own reliable channel, so messages and actions never queue behind them.
            RpcId(p.Id, MethodName.RefreshReliably, sequence, known == null ? 0 : baseline, delta);
            // The whole world is sure to arrive, so later updates build on it rather than sending it again until the guest confirms it.
            if (known == null) acked[p.Id] = sequence;
        }
    }

    /// <summary>
    /// The entries that differ from <paramref name="known"/>, all of them without it, with null for each entry that is gone.
    /// A changed entry only carries its changed fields, under its key with <see cref="Patched"/> in front.
    /// </summary>
    private static byte[] Delta(Dictionary<string, string> entries, Dictionary<string, string>? known) {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest))
        using (var json = new Utf8JsonWriter(brotli)) {
            json.WriteStartObject();
            foreach (var (key, value) in entries) {
                var before = known?.GetValueOrDefault(key);
                if (before == value) continue;
                if (before != null && Fields(value) is { } now && Fields(before) is { } then && then.Keys.All(now.ContainsKey)) {
                    json.WriteStartObject(Patched + key);
                    foreach (var (field, raw) in now.Where(f => then[f.Key] != f.Value)) { json.WritePropertyName(field); json.WriteRawValue(raw, skipInputValidation: true); }
                    json.WriteEndObject();
                } else { json.WritePropertyName(key); json.WriteRawValue(value, skipInputValidation: true); }
            }
            foreach (var key in known?.Keys.Where(k => !entries.ContainsKey(k)) ?? []) json.WriteNull(key);
            json.WriteEndObject();
        }
        return output.ToArray();
    }

    private const char Patched = '~';

    /// <summary>An object entry's fields as raw JSON, or null for a list.</summary>
    private static Dictionary<string, string>? Fields(string entry) {
        using var document = JsonDocument.Parse(entry);
        return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.EnumerateObject().ToDictionary(f => f.Name, f => f.Value.GetRawText()) : null;
    }

    /// <param name="baseline">The update the changes are against, which this guest confirmed; zero when they are the whole world.</param>
    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = StreamChannel)]
    private void Refresh(int update, int baseline, byte[] packed) => Take(update, baseline, packed);

    /// <summary>The whole world again after a long stall, or a change too big for one packet.</summary>
    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = BulkChannel)]
    private void RefreshReliably(int update, int baseline, byte[] packed) => Take(update, baseline, packed);

    private void Take(int update, int baseline, byte[] packed) {
        if (!welcomed || update <= appliedSequence) return;
        // An update against one this machine has forgotten waits for the host to catch up with the confirmations.
        if (baseline != 0 && !received.ContainsKey(baseline)) return;
        var entries = baseline == 0 ? new Dictionary<string, string>() : new Dictionary<string, string>(received[baseline]);
        using (var brotli = new BrotliStream(new MemoryStream(packed), CompressionMode.Decompress))
        using (var changes = JsonDocument.Parse(brotli))
            foreach (var entry in changes.RootElement.EnumerateObject())
                if (entry.Value.ValueKind == JsonValueKind.Null) entries.Remove(entry.Name);
                else if (entry.Name[0] == Patched) entries[entry.Name[1..]] = Patch(entries[entry.Name[1..]], entry.Value);
                else entries[entry.Name] = entry.Value.GetRawText();
        received[update] = entries;
        foreach (var old in received.Keys.Where(k => k <= update - History).ToList()) received.Remove(old);
        Apply(entries);
        applied = entries; appliedSequence = update;
    }

    /// <summary>Brings the guest's copy from the entries it shows now to <paramref name="entries"/>, touching only what differs.</summary>
    private void Apply(Dictionary<string, string> entries) {
        var s = World.S;
        string? Changed(string key) => entries.TryGetValue(key, out var value) && applied.GetValueOrDefault(key) != value ? value : null;
        if (Changed("state") is string state) CopyExcept(s, Read<SaveState>(state), Shared);
        void Glides(object o, Vector2 from) { goals[o] = Position(o); Place(o, from); }
        Sync(s.Things, "t:", t => t.Id, entries, null);
        Sync(s.Workers, "w:", w => w.Id, entries, Glides);
        Sync(s.Wolves, "f:", w => w.Id, entries, Glides);
        Sync(s.Orders, "o:", o => o.Id, entries, null);
        if (Changed("work") is string working) work = Read<Dictionary<int, WorkerWorkState>>(working);
        var me = World.Me;
        // This machine walks its own player and keeps its hotbar; only a move the simulation made, such as a respawn, comes back.
        // The welcome's first update brings all of it, over the stand-in this copy started with.
        bool joining = applied.Count == 0;
        var own = (me.Pos, me.Warp, me.HotbarSlots, me.ActiveHotbar, me.GatherTarget, me.Held, me.Sprinting);
        unsaved |= Changed($"p:{me.Id}") != null;
        Sync(s.Players, "p:", p => p.Id, entries, (p, from) => { if (p != me) Glides(p, from); });
        if (!joining && me.Warp == own.Warp) me.Pos = own.Pos;
        if (!joining) (me.HotbarSlots, me.ActiveHotbar, me.GatherTarget, me.Held, me.Sprinting) = (own.HotbarSlots, own.ActiveHotbar, own.GatherTarget, own.Held, own.Sprinting);
        foreach (var gone in goals.Keys.Where(o => o is Worker w ? !s.Workers.Contains(w) : o is Wolf wolf ? !s.Wolves.Contains(wolf) : o is PlayerState p && !s.Players.Contains(p)).ToList()) goals.Remove(gone);
        s.Actor = me;
        Work();
        World.UpdateLunchFire();
    }

    private static string Patch(string entry, JsonElement changes) {
        var fields = JsonNode.Parse(entry)!.AsObject();
        foreach (var field in changes.EnumerateObject()) fields[field.Name] = JsonNode.Parse(field.Value.GetRawText());
        return fields.ToJsonString();
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Compact) ?? throw new InvalidDataException("Empty entry");

    /// <summary>
    /// Updates the members of <paramref name="list"/> whose entries changed, in place, adds new ones and drops those without an entry.
    /// <paramref name="moved"/> sees each updated member with its position before the update.
    /// </summary>
    private void Sync<T>(List<T> list, string prefix, Func<T, int> id, Dictionary<string, string> entries, Action<object, Vector2>? moved) where T : class {
        var have = list.ToDictionary(t => prefix + id(t));
        list.RemoveAll(t => !entries.ContainsKey(prefix + id(t)));
        foreach (var (key, json) in entries) {
            if (!key.StartsWith(prefix) || applied.GetValueOrDefault(key) == json && have.ContainsKey(key)) continue;
            var item = Read<T>(json);
            if (!have.TryGetValue(key, out var old)) { list.Add(item); continue; }
            var from = Position(old);
            CopyExcept(old, item, null);
            moved?.Invoke(old, from);
        }
    }

    // ---------- Merging into a guest's copy ----------

    private static Vector2 Position(object o) => o switch { Worker w => w.Pos, Wolf w => w.Pos, PlayerState p => p.Pos, _ => Vector2.Zero };

    private static void Place(object o, Vector2 at) {
        if (o is Worker w) w.Pos = at; else if (o is Wolf wolf) wolf.Pos = at; else if (o is PlayerState p) p.Pos = at;
    }

    private static readonly Dictionary<Type, PropertyInfo[]> properties = new();

    /// <summary>Copies every saved property; inventories are copied into the ones already there, which open panels and drags hold on to.</summary>
    private static void CopyExcept(object target, object source, HashSet<string>? skip) {
        var type = target.GetType();
        if (!properties.TryGetValue(type, out var list))
            properties[type] = list = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always }).ToArray();
        foreach (var property in list) {
            if (skip?.Contains(property.Name) == true) continue;
            var value = property.GetValue(source);
            if (value is Inventory inventory && property.GetValue(target) is Inventory existing) CopyExcept(existing, inventory, null);
            else property.SetValue(target, value);
        }
    }
}
