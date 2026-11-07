using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Runs a game for its players: the world, the updates it sends them and its saves, with no window and no player of its own.
/// The window that starts a game runs one in a process of its own, or inside itself for the checks, and joins it like any other player.
/// The game ends when that window's player leaves, or when nobody has joined for a while.
/// </summary>
public partial class Server : Node {
    private const float AutosaveSeconds = 300, LonelySeconds = 30;
    /// <summary>A steady rate, so the simulation's steps do not depend on how the engine paces a process with no window.</summary>
    private const int TicksPerSecond = 60;
    /// <summary>Ports to try; the first is the one guests expect, and the rest let a second game run on this computer.</summary>
    private const int GamePorts = 16;
    /// <summary>Plays a performance run's camp, for a window recording one.</summary>
    private const string PerformanceFlag = "--performance-fixture";
    public World World { get; private set; } = null!;
    /// <summary>The port it took.</summary>
    public int Port { get; private set; }
    public Net Net { get; private set; } = null!;
    private float autosave, lonely;
    private bool ended;
    /// <summary>The game is over: the owner left or nobody came.</summary>
    public event Action? Ended;

    /// <summary>The world a game starts from: the one in save slot <paramref name="slot"/>, or a new one when that is negative, or a performance run's camp.</summary>
    public static World Load(GameData data, int slot) => Launch.Has(PerformanceFlag) ? PerformanceFixture.Create(data)
        : slot >= 0 ? Saves.Read(data, slot).World ?? throw new InvalidOperationException($"Cannot load slot {slot}.") : new World(data);

    /// <summary>
    /// Serves a world under <paramref name="parent"/> on the first free port.
    /// The network node takes the name and place a window's has, so their messages find each other.
    /// </summary>
    public static Server Start(Node parent, World world, string owner) {
        world.Vacate(); world.Claim(owner);
        var net = new Net { Name = "Net", World = world, Saver = n => Saves.Write(world, n) };
        world.Sound = net.Echo;
        world.PlayerActed += (action, at) => net.Echo(world.ActorId, action, at);
        parent.AddChild(net);
        var server = new Server { Name = "Server", World = world, Net = net };
        parent.AddChild(server);
        net.OwnerLeft += server.End;
        server.Port = Enumerable.Range(Net.DefaultPort, GamePorts).FirstOrDefault(port => Free(port) && net.Serve(port, owner) == Error.Ok);
        if (server.Port == 0) throw new InvalidOperationException("Every port a game can use is taken.");
        return server;
    }

    /// <summary>Whether no game holds a port: ENet logs an error on a port that is taken, which is nothing unusual here.</summary>
    private static bool Free(int port) {
        using var probe = new PacketPeerUdp();
        bool free = probe.Bind(port) == Error.Ok;
        probe.Close();
        if (!free) Net.Log($"Server: port {port} is taken, trying the next.");
        return free;
    }

    /// <summary>Runs as the whole process, started by a window with the arguments <see cref="Arguments"/> makes, and tells it the port it took.</summary>
    public static void Run(Node main, GameData data) {
        Engine.MaxFps = TicksPerSecond;
        var server = Start(main, Load(data, int.Parse(Launch.Value("--slot") ?? "-1")), Launch.Value("--owner") ?? "");
        server.Ended += () => main.GetTree().Quit();
        if (Launch.Has(PerformanceFlag)) main.AddChild(new PerformanceStage(server.World));
        using var file = Godot.FileAccess.Open(Launch.Value("--port-file") ?? throw new InvalidOperationException("A server needs a file to give its port in."), Godot.FileAccess.ModeFlags.Write);
        file.StoreString($"{server.Port}");
    }

    /// <summary>The game's own arguments for a server process: the save slot to load or -1 for a new world, the owner's key and the file to give its port in.</summary>
    public static string[] Arguments(int slot, string owner, string portFile) => ["--server", "--slot", $"{slot}", "--owner", owner, "--port-file", portFile, "--file-prefix", Launch.FilePrefix, .. Launch.Has("--performance-run") ? new[] { PerformanceFlag } : []];

    public override void _Process(double delta) {
        float dt = Math.Min((float)delta, .1f);
        if (World.Players.Count == 0) {
            if ((lonely += dt) >= LonelySeconds && !ended) { Net.Log($"Server: nobody has been in the game for {LonelySeconds:0} seconds, which ends it."); End(); }
            return;
        }
        lonely = 0;
        if (Net.Held) return;
        World.Tick(dt);
        if ((autosave += dt) >= AutosaveSeconds) { autosave = 0; Autosave(); }
    }

    /// <summary>Writes the autosave slot and tells the owner how it went.</summary>
    public void Autosave() => Net.TellOwner(Saves.Write(World, 0));

    /// <summary>Ends the game now.</summary>
    public void End() {
        if (ended) return;
        ended = true;
        Net.Leave();
        Ended?.Invoke();
    }
}
