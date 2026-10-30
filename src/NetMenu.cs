using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Hosting the current game, the join popup, and what happens around joining and leaving.</summary>
public partial class Game {
    private Label joinStatus = null!;
    private Label reconnectStatus = null!;
    /// <summary>Why the last joined game ended, shown once on the start menu.</summary>
    private string leftReason = "";

    private void ConnectNet() {
        net.Said += Notify;
        net.Heard += PlayWorldSound;
        net.Acted += view.Acted;
        net.Joined += OnJoined;
        net.Disconnected += ShowReconnecting;
        net.Reconnecting += left => { if (GodotObject.IsInstanceValid(reconnectStatus)) reconnectStatus.Text = $"Lost the connection to the host. Trying again for {left} more {(left == 1 ? "second" : "seconds")}."; };
        net.Left += reason => { leftReason = reason; world.NewGame(); ShowStart(); };
        net.Failed += reason => { if (GodotObject.IsInstanceValid(joinStatus)) joinStatus.Text = reason; };
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

    /// <summary>The join popup: the host's address and port, which starts out as the port a host opens.</summary>
    /// <param name="inGame">Opened from the pause menu: the game in progress is autosaved before the host's world replaces it.</param>
    private void ShowJoin(Action back, bool inGame) {
        OpenScreen("JOIN A GAME", () => { if (net.Connecting) net.Leave(); back(); });
        var address = new LineEdit { Name = "address", Text = Setting("net", "address", ""), PlaceholderText = "Host address, such as 192.168.1.20", CustomMinimumSize = new Vector2(0, 38) };
        var port = new LineEdit { Name = "port", Text = Net.DefaultPort.ToString(), CustomMinimumSize = new Vector2(0, 38) };
        foreach (var (text, field) in new[] { ("Address", address), ("Port", port) }) {
            var row = new HBoxContainer(); screen.AddChild(row);
            var label = Label(text); label.CustomMinimumSize = new Vector2(80, 0); row.AddChild(label);
            field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(field);
        }
        joinStatus = Label("", 14, Muted); screen.AddChild(joinStatus);
        var join = Button("Join", () => {
            if (!int.TryParse(port.Text.Trim(), out int number) || number is < 1 or > 65535) { joinStatus.Text = "The port is a number from 1 to 65535."; return; }
            if (address.Text.Trim() == "") { joinStatus.Text = "Enter the host's address."; return; }
            if (inGame && !world.Remote) Save(0);
            var error = net.Join(address.Text.Trim(), number);
            joinStatus.Text = error == Error.Ok ? "Connecting…" : $"Could not connect: {error}.";
        });
        join.Name = "join"; screen.AddChild(join);
        address.TextSubmitted += _ => join.EmitSignal(BaseButton.SignalName.Pressed);
        AddScreenButton("Back · Esc", () => screenBack?.Invoke());
        address.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Shown while a dropped guest tries to get back in, in place of the host's world, which it can no longer act on.</summary>
    private void ShowReconnecting() {
        world.NewGame(); ClosePanel(); ending.Visible = false;
        OpenScreen("RECONNECTING", () => { net.StopRetrying(); ShowStart(); });
        reconnectStatus = Label("Lost the connection to the host. Trying again…", 14, Muted); screen.AddChild(reconnectStatus);
        AddScreenButton("Stop · Esc", () => screenBack?.Invoke());
    }

    private void OnJoined() {
        StoreSetting("net", "address", net.Address);
        slot = 0; selected = 0; workerId = 0; gathering = null; entering = null; lifting = null; assigningWorker = 0; endingSeen = false;
        view.BuildMode = null; view.Selected = 0; view.SelectedWorker = 0; shownHealth = world.S.Health;
        HideScreen(); menu.Visible = false; ending.Visible = false; ClosePanel();
        Notify(net.Retrying ? "Reconnected to the host's settlement." : "Joined the host's settlement. Your belongings stay yours when you leave.");
    }
}
