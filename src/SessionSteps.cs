using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>Starts and leaves games for the UI and network checks.</summary>
public partial class Game {
    private static void RemoveCheckFiles() {
        var signals = DirAccess.GetFilesAt("user://").Where(name => name.StartsWith($"{Launch.FilePrefix}signal-")).Select(name => $"user://{name}");
        foreach (var path in Enumerable.Range(0, Saves.SlotCount + 1).Select(Saves.SlotPath).Append(Settings.Path).Concat(signals)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    /// <summary>The world of the server this window runs for the check, which the check sets up directly.</summary>
    private World Hosted => hosted?.World ?? throw new InvalidOperationException("No server is running in this window.");

    /// <summary>
    /// Passes everything on the line to the server inside this window and brings its answer back, at once.
    /// The UI checks do this rather than wait, so they keep to their time limit: they check what happens, and network and server checks check how long it takes.
    /// </summary>
    private void Settle() {
        if (!Launch.Has("--ui-test") || hosted is not Server server) return;
        ulong until = Time.GetTicksMsec() + 5000;
        do {
            Multiplayer.Poll(); server.Multiplayer.Poll();
            server.Net.SendUpdates();
            server.Multiplayer.Poll(); Multiplayer.Poll();
        } while ((net.Connecting || net.ShownUpdate != server.Net.LastUpdate) && Time.GetTicksMsec() < until);
    }

    /// <summary>Puts the player in front of the first iron cliff, where a mine is marked, and gives the cliff.</summary>
    private async Task<Cliff> StandAtIronCliff() {
        var cliff = World.CliffOf(ThingKind.Iron);
        SetupSteps.Warp(Hosted, World.MineSpot(cliff) + cliff.Face * 3); await Frames(4);
        return cliff;
    }

    /// <summary>This window's copy of something a check placed on the server.</summary>
    private Thing Shown(Thing thing) => world.Find(thing.Id) ?? throw new InvalidOperationException($"This window does not show the {thing.Kind}.");

    /// <summary>Another port for the guest to join on, such as a proxy that adds lag: --net-check guest --port 24575.</summary>
    private static int CheckPort => Launch.Value("--port") is string port ? int.Parse(port) : Net.DefaultPort;

    private static string SignalPath(string name) => $"user://{Launch.FilePrefix}signal-{name}";

    /// <summary>Tells the other network check process that this one reached a point, for the times the game itself cannot carry the news.</summary>
    private static void Signal(string name, string? said = null) {
        using var file = Godot.FileAccess.Open(SignalPath(name), Godot.FileAccess.ModeFlags.Write);
        file.StoreString(said ?? name);
    }

    /// <summary>Waits for the other network check process to reach a point, clears the signal so it can be sent again, and returns what it said.</summary>
    private async Task<string> Signaled(string name) {
        await Until(() => Godot.FileAccess.FileExists(SignalPath(name)) && Godot.FileAccess.GetFileAsString(SignalPath(name)) != "", $"the other process signalling {name}", 90);
        string said = Godot.FileAccess.GetFileAsString(SignalPath(name));
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SignalPath(name)));
        return said;
    }

    private async Task SurvivalReviewShot(string name) {
        if (Launch.Value("--review-captures") is not string folder) return;
        System.IO.Directory.CreateDirectory(folder);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, name + ".png")) == Error.Ok,
            "The rendered survival review capture must save successfully.");
    }

    /// <summary>What the join popup says, or nothing while it is closed.</summary>
    private string JoinStatus() => startMenu.Screen.FindChild("join_status", true, false) is Label status ? status.Text : "";

    /// <summary>Join game on the start menu, the address typed into the popup and the port as it comes; tried again until the host is up.</summary>
    private async Task JoinFromStart() {
        for (ulong until = Time.GetTicksMsec() + 45000; !world.Remote;) {
            if (Time.GetTicksMsec() > until) Require(false, $"joining the host never happened: {JoinStatus()}");
            await Click(FindButton(startMenu.Screen, "Join game")!);
            var port = (LineEdit)startMenu.Screen.FindChild("port", true, false);
            Require(port.Text == Net.DefaultPort.ToString(), "The join popup must come with the port filled in.");
            var address = (LineEdit)startMenu.Screen.FindChild("address", true, false);
            // The popup offers the address of the last game joined.
            Require(address.Text is "" or "127.0.0.1", "The join popup must offer the last address joined.");
            await JoinFromPopup(CheckPort.ToString());
            for (ulong wait = Time.GetTicksMsec() + 4000; Time.GetTicksMsec() < wait && !world.Remote && JoinStatus() == "Connecting…";) await Frames(1);
            if (!world.Remote) {
                await Click(FindButton(startMenu.Screen, "Back · Esc")!);
                await Until(() => FindButton(startMenu.Screen, "Join game") != null, "the start menu after backing out of a failed join", 5);
            }
        }
        await Until(() => !startMenu.Visible, "the join popup closing");
    }

    /// <summary>Joins until the server turns this window away for <paramref name="reason"/>; it may not be up yet. No input reads the refusal once the popup is closed, so this calls the join directly.</summary>
    private async Task JoinUntilRefused(string reason) {
        string heard = ""; void Refused(string why) => heard = why;
        net.Failed += Refused;
        for (ulong until = Time.GetTicksMsec() + 45000; heard != reason;) {
            Require(Time.GetTicksMsec() < until, $"the server never refused this window: {heard}");
            heard = ""; net.Join("127.0.0.1", CheckPort);
            await Until(() => heard != "", "the server answering this window");
        }
        net.Failed -= Refused;
    }

    /// <summary>Types a port into the open join popup and presses Join; the address stays as it is, or becomes this computer.</summary>
    private async Task JoinFromPopup(string to) {
        var port = (LineEdit)startMenu.Screen.FindChild("port", true, false);
        await Click(port); await KeyStroke(Key.End);
        for (ulong wait = Time.GetTicksMsec() + 5000; port.Text != "" && Time.GetTicksMsec() < wait;) await KeyStroke(Key.Backspace);
        Require(port.Text == "", "Backspace must clear the port field.");
        await Type(to);
        var address = (LineEdit)startMenu.Screen.FindChild("address", true, false);
        if (address.Text == "") { await Click(address); await Type("127.0.0.1"); }
        await Click(FindButton(startMenu.Screen, "Join")!);
    }

    /// <summary>Waits for a game this window started to have this window's player.</summary>
    private async Task StartedGame() {
        await Until(() => world.Remote && !startMenu.Visible, "the game's server taking this window's player", 30);
    }

    /// <summary>Waits for this window to be back in the game it owns.</summary>
    private async Task BackInOwnGame(string what, double seconds) => await Until(() => net.Owning && world.Remote && !startMenu.Visible, what, seconds);

    /// <summary>Leaves a game this window started, and waits for its server to end.</summary>
    private async Task LeaveAndAwaitServerEnd() {
        int server = serverProcess;
        ShowStart();
        await Until(() => !OS.IsProcessRunning(server), "the server ending with its owner", 10);
    }

    /// <summary>
    /// Plants a tree among the tallest or the shortest at a spot in the player's sight and fells it.
    /// Returns the seconds on the view's clock from the sound of it breaking to its crash, and how far over its trunk was, in degrees, at each.
    /// </summary>
    private async Task<(float Seconds, float LeanAtBreak, float LeanAtCrash)> FellInSight(Vector2 at, bool tall) {
        var tree = SetupSteps.PlantTree(Hosted, at, tall);
        await Frames(4);
        // The model outlives its tree while it falls, so it is taken before the tree leaves the world.
        var falling = (TreeProp)view.PropOf(tree.Id)!;
        var stump = SetupSteps.Fell(Hosted, tree);
        await Until(() => sounds.Last == (tall ? "tree_bend" : "tree_crack"), tall ? "a tall tree tearing at length as it breaks" : "a short tree cracking as it breaks", 2);
        // The fall is timed on the view's own clock, which a slow frame does not stretch the way it stretches real time.
        float brokeAt = view.Elapsed, leanAtBreak = falling.Sway.RotationDegrees.X;
        await Until(() => sounds.Last == "tree_crash", "a felled tree crashing", 4);
        var fall = (view.Elapsed - brokeAt, leanAtBreak, falling.Sway.RotationDegrees.X);
        SetupSteps.RemoveThings(Hosted, t => t == stump); await Frames(2);
        return fall;
    }

    /// <summary>The closest place a building can go.</summary>
    private Vector2 FreeSpotNearby(ThingKind kind) {
        var spots = from x in Enumerable.Range(-3, 7) from y in Enumerable.Range(-3, 7) select world.Player.Round() + new Vector2(x, y);
        return spots.Where(spot => world.PlacementBlock(kind, spot) == null).MinBy(spot => spot.DistanceTo(world.Player));
    }

    /// <summary>Starts a game on this computer that someone else owns, which has not been opened to anyone, and returns its server's process and port.</summary>
    private async Task<(int Process, int Port)> StartStrangersServer() {
        int process = RunServer(-1, "stranger", SignalPath("stranger-port"));
        return (process, int.Parse(await Signaled("stranger-port")));
    }

    private async Task LeaveGame() {
        int host = world.Players.First(p => p != world.Me).Id;
        await CloseWithEsc(); await KeyStroke(Key.Escape);
        Require(menu.Visible, "A guest's Esc must open the pause menu.");
        Require(menu.JoinButton.Text == "Leave game", "A guest's pause menu must offer to leave.");
        Require(menu.SaveButton.Disabled, "A guest's pause menu must not offer to save the host's world.");
        await Click(menu.JoinButton);
        Require(!world.Remote, "Leaving must end the guest's connection.");
        Require(startMenu.Visible, "Leaving must return to the start menu.");
        await Frames();
        Require(!view.ShowsPlayer(host), "The start menu must not show the players of the game left behind.");
    }
}
