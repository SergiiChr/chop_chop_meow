using System;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>
/// One window playing on the real server process, the way a player does: the UI and network checks run the server inside the window.
/// build.py starts it with --server-check, and the server writes to the same log, so its errors fail the check too.
/// </summary>
public partial class Game {
    /// <summary>What the frame budget assumes: a window limited as players limit theirs, so a frame holds a real share of the server's updates.</summary>
    private const int CheckFrameRate = 60;
    /// <summary>What real play costs: mostly the entries each update changes, which are new arrays. A third more garbage than this fails.</summary>
    private const long ServerWalkingBudget = 1250;

    private async void RunServerCheck() {
        try {
            Engine.MaxFps = CheckFrameRate;
            RemoveCheckFiles(); await Frames(4);
            await CheckNewGameRunsOnAServerOfItsOwn();
            await CheckPlayerComesBackThroughEveryLoad();
            await CheckSecondGameTakesAnotherPort();
            await CheckFailedJoinKeepsTheGameRunning();
            await CheckCancelledJoinKeepsTheGameRunning();
            await CheckRefusedJoinKeepsTheGameRunning();
            await CheckJoiningYourOwnGameKeepsItYours();
            await CheckWalkingStaysWithinAllocationBudget(ServerWalkingBudget);
            await LeaveAndAwaitServerEnd();
            await CheckServerDyingEndsTheGame();
            GD.Print("SERVER-TEST PASS"); Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    /// <summary>
    /// Walking for a few hundred real frames, through Godot input, allocates no more than its budget on the main thread:
    /// every byte is garbage the runtime collects later, in a pause that stalls a frame.
    /// </summary>
    private async Task CheckWalkingStaysWithinAllocationBudget(long maxBytesPerFrame) {
        var key = new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = true };
        Input.ParseInputEvent(key);
        await Frames(120);
        long before = GC.GetAllocatedBytesForCurrentThread(); ulong first = Engine.GetProcessFrames();
        await Frames(300);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before; ulong played = Math.Max(1, Engine.GetProcessFrames() - first);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = false });
        await Frames();
        string report = $"frames: walking: {bytes} B over {played} frames, {bytes / (double)played:0.#} B per frame (budget {maxBytesPerFrame} B)";
        GD.Print(report);
        Require(bytes <= maxBytesPerFrame * (long)played, $"Allocation budget exceeded: {report}");
    }
    private async Task CheckNewGameRunsOnAServerOfItsOwn() {
        await Click(FindButton(startMenu.Screen, "New game")!);
        Require(HasLabel(startMenu.Screen, "STARTING"), "A game must say it is starting while its server comes up.");
        await StartedGame();
        Require(OS.IsProcessRunning(serverProcess) && net.Port == Net.DefaultPort, "A new game must run on a server process of its own, on the port guests expect.");
        await CloseWithEsc();
        Require(await PlayerWalks(), "The player must walk on the server.");
        float from = world.Time; await Seconds(2);
        Require(world.Time - from is > 1.6f and < 2.4f, $"The server's clock must keep real time: {world.Time - from} s in 2 s.");
    }

    private async Task CheckPlayerComesBackThroughEveryLoad() {
        var spot = world.Player;
        for (int load = 0; load < 2; load++) {
            await KeyStroke(Key.Escape); await Click(menu.SaveButton); await Click(FindButton(startMenu.Screen, Saves.SlotLabel(1))!);
            // The save goes first on the line, so the server writes it before it hears the owner leave.
            await LeaveAndAwaitServerEnd();
            Require(Saves.Read(data, 1).Error == null, "A save the server writes must load.");
            await Click(FindButton(startMenu.Screen, "Load game")!); await Click(FindButton(startMenu.Screen, Saves.SlotLabel(1))!);
            await StartedGame();
            Require(world.Player.DistanceTo(spot) < 1, $"Load {load + 1} must bring the player back where they stood, not at {world.Player}.");
            await CloseWithEsc();
        }
    }

    private async Task CheckSecondGameTakesAnotherPort() {
        await LeaveAndAwaitServerEnd();
        using var other = new PacketPeerUdp();
        Require(other.Bind(Net.DefaultPort) == Error.Ok, "The check must hold the port another game would.");
        await Click(FindButton(startMenu.Screen, "New game")!);
        await StartedGame();
        other.Close();
        Require(net.Port == Net.DefaultPort + 1, "A game must not join whatever already holds its port.");
        await CloseWithEsc();
    }

    private async Task CheckFailedJoinKeepsTheGameRunning() {
        var spot = world.Player;
        int server = serverProcess, port = net.Port;
        await KeyStroke(Key.Escape); await Click(menu.SaveButton); await Click(FindButton(startMenu.Screen, Saves.SlotLabel(2))!);
        await Until(() => Saves.SlotUsed(2), "the server writing the save");
        await Click(menu.JoinButton);
        await JoinFromPopup("9");
        await Until(() => !world.Remote, "the window stepping out of its game to join");
        await BackInOwnGame("the player coming back to their game", 30);
        Require(serverProcess == server && OS.IsProcessRunning(server) && net.Port == port, "A failed join must leave the game on the server it was on.");
        Require(world.Player.DistanceTo(spot) < 1, "A failed join must leave the player where they were.");
        Require(Saves.SlotUsed(0), "Joining from a game must autosave it first.");
        await CloseWithEsc();
        await KeyStroke(Key.F5);
        await Until(() => messages.Shown.Contains("Settlement saved · Slot 2."), "F5 saving to the slot the game was using before the failed join", 5);
    }

    private async Task CheckCancelledJoinKeepsTheGameRunning() {
        int server = serverProcess;
        await KeyStroke(Key.Escape); await Click(menu.JoinButton);
        await JoinFromPopup("9");
        Require(net.Connecting, "The check must back out while the join is under way.");
        await Click(FindButton(startMenu.Screen, "Back · Esc")!);
        await BackInOwnGame("the player coming back after cancelling", 5);
        Require(serverProcess == server && OS.IsProcessRunning(server), "Cancelling a join must leave the game on the server it was on.");
        Require(messages.Shown.Contains("Join cancelled. You are back in your game."), "Cancelling a join must say the player is back.");
        await CloseWithEsc();
    }

    private async Task CheckRefusedJoinKeepsTheGameRunning() {
        var (stranger, port) = await StartStrangersServer();
        await KeyStroke(Key.Escape); await Click(menu.JoinButton);
        await JoinFromPopup(port.ToString());
        await BackInOwnGame("the player coming back after a host refused them", 15);
        Require(messages.Shown.Contains("The host has not opened the game to other players. You are back in your game."), "A refused join must say why and that the player is back.");
        using (var server = System.Diagnostics.Process.GetProcessById(stranger)) server.Kill();
        await CloseWithEsc();
    }

    /// <summary>The owner may type their own game's address into the join popup, which brings them back to it.</summary>
    private async Task CheckJoiningYourOwnGameKeepsItYours() {
        await KeyStroke(Key.Escape); await Click(menu.JoinButton);
        await JoinFromPopup(net.Port.ToString());
        await Until(() => world.Remote && !startMenu.Visible, "the player joining their own game", 15);
        Require(net.Owning, "A player who joins the game they started must still own it, so they can save it.");
        await CloseWithEsc();
        await KeyStroke(Key.F5);
        await Until(() => messages.Shown.Contains("Settlement saved · Slot 2."), "F5 still saving the player's slot in their own game", 5);
    }

    private async Task CheckServerDyingEndsTheGame() {
        await Click(FindButton(startMenu.Screen, "New game")!);
        await StartedGame();
        // Godot forgets a process it kills itself, which a server that crashes is not.
        using (var server = System.Diagnostics.Process.GetProcessById(serverProcess)) server.Kill();
        await Until(() => startMenu.Visible && HasLabel(startMenu.Screen, Net.ServerStopped), "the start menu saying the server stopped", 10);
    }
}
