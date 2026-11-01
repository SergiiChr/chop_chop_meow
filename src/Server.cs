using System;
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
    public World World { get; private set; } = null!;
    private Net net = null!;
    private float autosave, lonely;
    private bool ended;
    /// <summary>The game is over: the owner left or nobody came.</summary>
    public event Action? Ended;

    /// <summary>
    /// Serves a world under <paramref name="parent"/>: a new one, or the one in save slot <paramref name="slot"/> when that is not negative.
    /// The network node takes the name and place a window's has, so their messages find each other.
    /// </summary>
    public static Server Start(Node parent, GameData data, int slot, string owner, int port) {
        var world = slot >= 0 ? Saves.Read(data, slot).World ?? throw new InvalidOperationException($"Cannot load slot {slot}.") : new World(data);
        world.Vacate(); world.Claim(owner);
        var net = new Net { Name = "Net", World = world, Saver = n => Saves.Write(world, n) };
        world.Sound = net.Echo;
        world.PlayerActed += (action, at) => net.Echo(world.ActorId, action, at);
        parent.AddChild(net);
        var server = new Server { Name = "Server", World = world, net = net };
        parent.AddChild(server);
        net.OwnerLeft += server.End;
        var error = net.Serve(port, owner);
        if (error != Error.Ok) throw new InvalidOperationException($"Cannot serve on port {port}: {error}.");
        return server;
    }

    /// <summary>Runs as the whole process, started by a window with the arguments <see cref="Arguments"/> makes.</summary>
    public static void Run(Node main, GameData data) {
        Engine.MaxFps = TicksPerSecond;
        var server = Start(main, data, int.Parse(Launch.Value("--slot") ?? "-1"), Launch.Value("--owner") ?? "", int.Parse(Launch.Value("--server") ?? $"{Net.DefaultPort}"));
        server.Ended += () => main.GetTree().Quit();
        GD.Print("SERVER READY");
    }

    /// <summary>The game's own arguments for a server process: its port, the save slot to load or -1 for a new world, and the owner's key.</summary>
    public static string[] Arguments(int port, int slot, string owner) => ["--server", $"{port}", "--slot", $"{slot}", "--owner", owner, "--file-prefix", Launch.FilePrefix];

    public override void _Process(double delta) {
        float dt = Math.Min((float)delta, .1f);
        if (World.Players.Count == 0) {
            if ((lonely += dt) >= LonelySeconds) End();
            return;
        }
        lonely = 0;
        if (!net.Held) World.Tick(dt);
        if ((autosave += dt) >= AutosaveSeconds) { autosave = 0; GD.Print(Saves.Write(World, 0)); }
    }

    /// <summary>Ends the game now.</summary>
    public void Stop() => End();

    private void End() {
        if (ended) return;
        ended = true;
        net.Leave();
        Ended?.Invoke();
    }
}
