using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Godot;

namespace ChopChopMeow;

/// <summary>What the host sends: the world, everyone in it, and what the workers are doing, which saves leave out.</summary>
internal sealed class Snapshot
{
    public SaveState State { get; set; } = new();
    public List<PlayerState> Players { get; set; } = new();
    public Dictionary<int, WorkerWorkState> Work { get; set; } = new();
}

/// <summary>
/// Host-client multiplayer over ENet; the connection handling comes from This Scalpel Is Mine.
/// The host's <see cref="World"/> is the only simulation. Ten times a second it sends each guest the whole world, compressed.
/// A guest merges it into its copy in place, so open panels and the view keep their objects, and glides what moves.
/// A guest walks its own player and streams where it stands; any other action goes to the host as a <see cref="Command"/>.
/// Joining sends the guest's player as saved on its machine, which the host checks before letting it in.
/// </summary>
public partial class Net : Node
{
    public const int DefaultPort = 24565;
    public const int MaxGuests = 3;
    /// <summary>ENet drops a peer once a reliable packet stays unanswered this long (or TimeoutMinMsec after TimeoutLimit resends).</summary>
    private const int TimeoutLimit = 64, TimeoutMinMsec = 15000, TimeoutMaxMsec = 45000;
    /// <summary>Joining gives up on a host that never answers after this long, instead of waiting out the full timeout.</summary>
    private const float ConnectTimeout = 10;
    private const float SnapshotInterval = .1f, InputInterval = .05f;
    private const int HostId = 1;
    /// <summary>Raised whenever the messages change, so mismatched builds refuse each other.</summary>
    private const int Protocol = 1;
    /// <summary>Furthest a guest can walk between two of its updates; anything further is ignored.</summary>
    private const float MaxStep = 3;

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
    private bool welcomed;
    private float sendClock;
    private SceneTreeTimer? connectTimer;
    // A guest's copy glides workers, wolves and the other players toward where the host last put them.
    private readonly Dictionary<object, Vector2> goals = new();
    // The host glides each guest toward where it says it stands, while no respawn or other move by the simulation has come since.
    private readonly Dictionary<int, (Vector2 At, int Warp)> walks = new();

    private static readonly JsonSerializerOptions Compact = new(GameData.Json) { WriteIndented = false };
    // Updates leave out the fog, which each machine works out from where everyone stands.
    private static readonly JsonSerializerOptions Light = new(Compact)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { info => { if (info.Type == typeof(SaveState)) foreach (var p in info.Properties.Where(p => p.Name is nameof(SaveState.Explored) or nameof(SaveState.Remembered)).ToList()) info.Properties.Remove(p); } }
        }
    };

    public bool Online => Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer;
    /// <summary>Kept apart from IsServer, which errors once a lost connection has closed the peer.</summary>
    public bool Hosting { get; private set; }
    public bool Connecting => Online && !Hosting && !welcomed;
    public int Guests => Hosting ? World.S.Players.Count - 1 : 0;

    public override void _Ready()
    {
        Multiplayer.PeerDisconnected += id => { walks.Remove((int)id); World.RemoveGuest((int)id); };
        Multiplayer.ConnectedToServer += () => { connectTimer = null; RpcId(HostId, MethodName.Hello, Protocol, LoadGuest()); };
        Multiplayer.ConnectionFailed += () => Fail("Could not reach the host.");
        Multiplayer.ServerDisconnected += () => End("Lost the connection to the host.");
        Multiplayer.PeerConnected += id => { if (Hosting && Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet) TolerateLag(enet.GetPeer((int)id)); };
    }

    private static void TolerateLag(ENetPacketPeer peer) => peer.SetTimeout(TimeoutLimit, TimeoutMinMsec, TimeoutMaxMsec);

    /// <summary>Opens the current game to guests.</summary>
    public Error Host(int port = DefaultPort)
    {
        Leave();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, MaxGuests);
        if (error != Error.Ok) return error;
        Multiplayer.MultiplayerPeer = peer; Hosting = true;
        World.Relay = Tell;
        return Error.Ok;
    }

    public Error Join(string ip, int port = DefaultPort)
    {
        Leave();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(ip, port);
        if (error != Error.Ok) return error;
        if (peer.GetPeer(HostId) is { } host) TolerateLag(host);
        Multiplayer.MultiplayerPeer = peer;
        address = $"{ip}:{port}";
        // The connect attempt itself would wait out the whole lag timeout; a host that never answers fails sooner.
        var timer = GetTree().CreateTimer(ConnectTimeout); connectTimer = timer;
        timer.Timeout += () => { if (timer == connectTimer && Connecting) Fail($"The host didn't answer within {ConnectTimeout:0} seconds."); };
        return Error.Ok;
    }

    /// <summary>Ends hosting or a joined game; a guest saves its player first.</summary>
    public void Leave()
    {
        SaveGuest();
        if (Hosting) foreach (var guest in World.S.Players.Where(p => p != World.Me).ToList()) World.RemoveGuest(guest.Id);
        if (Online) Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer(); Hosting = false;
        World.Host = null; World.Relay = null; World.LocalId = 1;
        welcomed = false; connectTimer = null; goals.Clear(); walks.Clear();
    }

    private void Fail(string reason) { Leave(); Failed?.Invoke(reason); }

    private void End(string reason) { Leave(); Left?.Invoke(reason); }

    // ---------- Joining ----------

    private string GuestPath => $"user://{FilePrefix}guest-{string.Concat(address.Select(c => char.IsLetterOrDigit(c) ? c : '-'))}.json";

    /// <summary>The player this machine saved for the host it joins, or empty.</summary>
    private string LoadGuest() => Godot.FileAccess.FileExists(GuestPath) ? Godot.FileAccess.GetFileAsString(GuestPath) : "";

    /// <summary>A guest keeps its own player, and what it carries, for the next time it joins this host.</summary>
    public void SaveGuest()
    {
        if (!welcomed || !World.Remote) return;
        using var file = Godot.FileAccess.Open(GuestPath, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(World.ExportPlayer(World.Me));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Hello(int protocol, string saved)
    {
        int id = Multiplayer.GetRemoteSenderId();
        if (!Hosting || World.S.Players.Any(p => p.Id == id)) return;
        if (protocol != Protocol) { RpcId(id, MethodName.Refused, "The host runs a different version of the game."); return; }
        World.AddGuest(id, saved == "" ? null : saved);
        RpcId(id, MethodName.Welcome, id, Pack(Compact));
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Refused(string reason) => Fail(reason);

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Welcome(int id, byte[] packed)
    {
        var snapshot = Unpack(packed);
        World.Mirror(snapshot.State, snapshot.Players, id);
        Work(snapshot);
        World.Host = Send;
        welcomed = true;
        Joined?.Invoke();
    }

    // ---------- Running ----------

    public override void _Process(double delta)
    {
        if (!Online) return;
        float dt = Math.Min((float)delta, .1f);
        foreach (var (owner, goal) in goals)
        {
            if (owner is PlayerState p) p.Pos = Glide(p.Pos, goal, dt);
            else if (owner is Worker w) w.Pos = Glide(w.Pos, goal, dt);
            else if (owner is Wolf wolf) wolf.Pos = Glide(wolf.Pos, goal, dt);
        }
        foreach (var p in World.S.Players)
            if (walks.TryGetValue(p.Id, out var walk) && walk.Warp == p.Warp) World.As(p, () => World.MoveTo(Glide(p.Pos, walk.At, dt)));
        if (World.Remote) World.Look();
        sendClock += dt;
        if (Hosting && sendClock >= SnapshotInterval)
        {
            sendClock = 0;
            var guests = World.S.Players.Where(p => p != World.Me).ToList();
            if (guests.Count > 0) { var packed = Pack(Light); foreach (var p in guests) RpcId(p.Id, MethodName.Refresh, packed); }
        }
        else if (welcomed && sendClock >= InputInterval)
        {
            sendClock = 0;
            var me = World.Me;
            RpcId(HostId, MethodName.Walked, me.Pos, me.Warp, me.GatherTarget, me.Held);
        }
    }

    /// <summary>Most of the way there each frame, so ten updates a second read as steady movement; a long way off is a jump.</summary>
    private static Vector2 Glide(Vector2 from, Vector2 to, float dt) => from.DistanceTo(to) > MaxStep ? to : from.Lerp(to, 1 - MathF.Exp(-dt * 12));

    private void Send(Command c) => RpcId(HostId, MethodName.Do, c.Verb, c.Target, c.Other, c.Item, c.Count, c.At);

    private PlayerState? Guest() => World.S.Players.Find(p => p.Id == Multiplayer.GetRemoteSenderId() && p != World.Me);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void Walked(Vector2 at, int warp, int gathering, string held)
    {
        if (!Hosting || Guest() is not PlayerState p) return;
        // A guest that has not yet seen its respawn still walks where it died.
        if (warp == p.Warp && at.DistanceTo(p.Pos) <= MaxStep) walks[p.Id] = (at, warp);
        p.GatherTarget = gathering; p.Held = held;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Do(string verb, int target, int other, string item, int count, Vector2 at)
    {
        if (Hosting && Guest() is PlayerState p) World.Run(p, new Command(verb, target, other, item, count, at));
    }

    /// <summary>Host: passes a message on to one guest, or all of them.</summary>
    private void Tell(PlayerState? to, string message)
    {
        foreach (var p in World.S.Players.Where(p => p != World.Me && (to == null || p == to))) RpcId(p.Id, MethodName.Told, message);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Told(string message) => Said?.Invoke(message);

    /// <summary>Host: lets the guests hear a sound of the world.</summary>
    public void Echo(string sound, Vector2 at)
    {
        if (Hosting) Rpc(MethodName.Hear, sound, at);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void Hear(string sound, Vector2 at) { if (welcomed) Heard?.Invoke(sound, at); }

    /// <summary>Host: lets the guests see a player swing, pump or light a torch.</summary>
    public void Echo(int player, ActorAction action, Vector2 at)
    {
        if (Hosting) Rpc(MethodName.See, player, (int)action, at);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void See(int player, int action, Vector2 at) { if (welcomed) Acted?.Invoke(player, (ActorAction)action, at); }

    // ---------- Snapshots ----------

    private byte[] Pack(JsonSerializerOptions options)
    {
        var snapshot = new Snapshot { State = World.S, Players = World.S.Players, Work = World.S.Workers.ToDictionary(w => w.Id, w => w.WorkState) };
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest)) JsonSerializer.Serialize(brotli, snapshot, options);
        return output.ToArray();
    }

    private static Snapshot Unpack(byte[] packed)
    {
        using var brotli = new BrotliStream(new MemoryStream(packed), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<Snapshot>(brotli, Compact) ?? throw new InvalidDataException("Empty snapshot");
    }

    private void Work(Snapshot snapshot)
    {
        foreach (var w in World.S.Workers) w.WorkState = snapshot.Work.GetValueOrDefault(w.Id);
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Refresh(byte[] packed)
    {
        if (!welcomed) return;
        var incoming = Unpack(packed);
        var s = World.S;
        CopyExcept(s, incoming.State, Shared);
        var things = s.Things.ToDictionary(t => t.Id);
        Merge(s.Things, incoming.State.Things, t => things.GetValueOrDefault(t.Id));
        var workers = s.Workers.ToDictionary(w => w.Id);
        Merge(s.Workers, incoming.State.Workers, w => workers.GetValueOrDefault(w.Id), (w, from) => { goals[w] = w.Pos; w.Pos = from; });
        var wolves = s.Wolves.ToList();
        Merge(s.Wolves, incoming.State.Wolves, w => Claim(wolves, o => o.Pos.DistanceTo(w.Pos) < MaxStep, o => o.Pos.DistanceSquaredTo(w.Pos)), (w, from) => { goals[w] = w.Pos; w.Pos = from; });
        var orders = s.Orders.ToList();
        Merge(s.Orders, incoming.State.Orders, o => Claim(orders, f => f.Signpost == o.Signpost && f.Buying == o.Buying && f.Crates == o.Crates && Math.Abs(f.Elapsed - o.Elapsed) < 2, f => Math.Abs(f.Elapsed - o.Elapsed)));
        var me = World.Me;
        // This machine walks its own player and keeps its hotbar; only a move the simulation made, such as a respawn, comes back.
        var own = (me.Pos, me.Warp, me.HotbarSlots, me.ActiveHotbar, me.GatherTarget, me.Held);
        var players = s.Players.ToDictionary(p => p.Id);
        Merge(s.Players, incoming.Players, p => players.GetValueOrDefault(p.Id), (p, from) => { if (p != me) { goals[p] = p.Pos; p.Pos = from; } });
        if (me.Warp == own.Warp) me.Pos = own.Pos;
        (me.HotbarSlots, me.ActiveHotbar, me.GatherTarget, me.Held) = (own.HotbarSlots, own.ActiveHotbar, own.GatherTarget, own.Held);
        foreach (var gone in goals.Keys.Where(o => o is Worker w ? !s.Workers.Contains(w) : o is Wolf wolf ? !s.Wolves.Contains(wolf) : o is PlayerState p && !s.Players.Contains(p)).ToList()) goals.Remove(gone);
        s.Actor = me;
        Work(incoming);
        World.UpdateLunchFire();
    }

    // ---------- Merging into a guest's copy ----------

    // Lists merged by identity, the fog each machine keeps itself, and the player properties, which the players list carries.
    private static readonly HashSet<string> Shared = new[] { nameof(SaveState.Things), nameof(SaveState.Workers), nameof(SaveState.Wolves), nameof(SaveState.Orders), nameof(SaveState.Explored), nameof(SaveState.Remembered) }
        .Concat(typeof(PlayerState).GetProperties().Select(p => p.Name)).ToHashSet();

    /// <summary>The first match left in <paramref name="pool"/>, the closest by <paramref name="distance"/>, taken out of it.</summary>
    private static T? Claim<T>(List<T> pool, Func<T, bool> matches, Func<T, float> distance) where T : class
    {
        var found = pool.Where(matches).MinBy(distance);
        if (found != null) pool.Remove(found);
        return found;
    }

    /// <summary>
    /// Makes <paramref name="target"/> hold what <paramref name="source"/> holds, reusing the objects it already had for the same things.
    /// <paramref name="moved"/> sees each reused object with its position before the update.
    /// </summary>
    private static void Merge<T>(List<T> target, List<T> source, Func<T, T?> previous, Action<T, Vector2>? moved = null) where T : class
    {
        var merged = new List<T>(source.Count);
        foreach (var item in source)
        {
            if (previous(item) is not T old) { merged.Add(item); continue; }
            var from = Position(old);
            CopyExcept(old, item, null);
            moved?.Invoke(old, from);
            merged.Add(old);
        }
        target.Clear(); target.AddRange(merged);
    }

    private static Vector2 Position(object o) => o switch { Worker w => w.Pos, Wolf w => w.Pos, PlayerState p => p.Pos, _ => Vector2.Zero };

    private static readonly Dictionary<Type, PropertyInfo[]> properties = new();

    /// <summary>Copies every saved property; inventories are copied into the ones already there, which open panels and drags hold on to.</summary>
    private static void CopyExcept(object target, object source, HashSet<string>? skip)
    {
        var type = target.GetType();
        if (!properties.TryGetValue(type, out var list))
            properties[type] = list = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always }).ToArray();
        foreach (var property in list)
        {
            if (skip?.Contains(property.Name) == true) continue;
            var value = property.GetValue(source);
            if (value is Inventory inventory && property.GetValue(target) is Inventory existing) CopyExcept(existing, inventory, null);
            else property.SetValue(target, value);
        }
    }
}
