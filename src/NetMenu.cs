using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Hosting the current game, and what happens around joining and leaving.</summary>
public partial class Game {
    private void ConnectNet() {
        net.Said += Notify;
        net.Heard += PlayWorldSound;
        net.Acted += view.Acted;
        net.Joined += OnJoined;
        net.Disconnected += ShowReconnecting;
        net.Reconnecting += left => startMenu.ShowRetry(left);
        net.Left += reason => { startMenu.LeftReason = reason; DropWorld(); startMenu.ShowStart(); };
        net.Failed += reason => startMenu.ShowJoinFailure(reason);
    }

    private void HostGame() {
        var error = net.Host();
        if (error != Error.Ok) { Notify($"Could not host on port {Net.DefaultPort}: {error}."); return; }
        var addresses = IP.GetLocalAddresses().Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254.")).Take(2).ToArray();
        Notify($"Hosting on port {Net.DefaultPort}. Others join with {(addresses.Length > 0 ? string.Join(" or ", addresses) : "this computer's address")}.");
        CloseMenu();
    }

    /// <summary>A line for the pause menu while the game is online.</summary>
    private string NetStatus() => net.Hosting ? $"Hosting on port {Net.DefaultPort} · {net.Guests} {(net.Guests == 1 ? "guest" : "guests")}"
        : world.Remote ? "Playing in the host's world. Your player saves on this computer." : "";

    /// <summary>Shown while a dropped guest tries to get back in, in place of the host's world, which it can no longer act on.</summary>
    private void ShowReconnecting() { DropWorld(); startMenu.ShowReconnecting(); }

    /// <summary>Replaces the world with a new one and closes everything that showed the old one.</summary>
    private void DropWorld() { world.NewGame(); panel.Close(); ending.Visible = false; blackHole.End(); }

    private void OnJoined(bool back) {
        Settings.Store("net", "address", net.Address);
        saves.Slot = 0; state.Selected = 0; state.WorkerId = 0; gathering = null; state.Entering = null; lifting = null; state.AssigningWorker = 0; ending.Joined(back);
        view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0; shownHealth = world.S.Health;
        startMenu.Close(); menu.Visible = false; ending.Visible = false; panel.Close();
        Notify(back ? "Reconnected to the host's settlement." : "Joined the host's settlement. Your belongings stay yours when you leave.");
    }
}
