using System;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>
/// One window playing on the real server process, the way a player does: the UI checks run their world in the window and the network check runs the server inside it.
/// build.py starts it with --server-check, and the server writes to the same log, so its errors fail the check too.
/// </summary>
public partial class Game {
    /// <summary>What the frame budget assumes: a window limited as players limit theirs, so a frame holds a real share of the server's updates.</summary>
    private const int CheckFrameRate = 60;
    /// <summary>What real play costs: mostly the entries each update changes, which are new arrays. A third more garbage than this fails.</summary>
    private const long ServerWalkingBudget = 1170;

    private async void RunServerCheck() {
        try {
            Engine.MaxFps = CheckFrameRate;
            RemoveCheckFiles(); await Frames(4);
            await CheckNewGameRunsOnAServerOfItsOwn();
            await CheckPlayerComesBackThroughEveryLoad();
            await CheckSecondGameTakesAnotherPort();
            await CheckFailedJoinBringsTheGameBack();
            await CheckWalkingStaysWithinAllocationBudget(ServerWalkingBudget);
            await CheckLeavingEndsTheServer();
            GD.Print("SERVER-TEST PASS"); Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task StartedGame() {
        await Until(() => world.Remote && !startMenu.Visible, "the game's server taking this window's player", 30);
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
            int server = serverProcess;
            ShowStart();
            await Until(() => !OS.IsProcessRunning(server), "the server ending with its owner", 10);
            Require(Saves.Read(data, 1).Error == null, "A save the server writes must load.");
            await Click(FindButton(startMenu.Screen, "Load game")!); await Click(FindButton(startMenu.Screen, Saves.SlotLabel(1))!);
            await StartedGame();
            Require(world.Player.DistanceTo(spot) < 1, $"Load {load + 1} must bring the player back where they stood, not at {world.Player}.");
            await CloseWithEsc();
        }
    }

    private async Task CheckSecondGameTakesAnotherPort() {
        ShowStart();
        await Until(() => !OS.IsProcessRunning(serverProcess), "the server ending with its owner", 10);
        using var other = new PacketPeerUdp();
        Require(other.Bind(Net.DefaultPort) == Error.Ok, "The check must hold the port another game would.");
        await Click(FindButton(startMenu.Screen, "New game")!);
        await StartedGame();
        other.Close();
        Require(net.Port == Net.DefaultPort + 1, "A game must not join whatever already holds its port.");
        await CloseWithEsc();
    }

    private async Task CheckFailedJoinBringsTheGameBack() {
        var spot = world.Player;
        await KeyStroke(Key.Escape); await Click(menu.JoinButton);
        await JoinFromPopup("9");
        await Until(() => net.Owning && world.Remote && !startMenu.Visible, "the game coming back from the autosave", 30);
        Require(world.Player.DistanceTo(spot) < 1, "A failed join must leave the player where they were.");
        await CloseWithEsc();
    }

    private async Task CheckLeavingEndsTheServer() {
        int server = serverProcess;
        ShowStart();
        await Until(() => !OS.IsProcessRunning(server), "the server ending with its owner", 10);
    }
}
