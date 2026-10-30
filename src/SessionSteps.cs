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

    /// <summary>Another port for the guest to join on, such as a proxy that adds lag: --net-check guest --port 24575.</summary>
    private static int CheckPort => Launch.Value("--port") is string port ? int.Parse(port) : Net.DefaultPort;

    private static string GuestSave => $"user://{Launch.FilePrefix}guest-127-0-0-1-{CheckPort}.json";

    private static string SignalPath(string name) => $"user://{Launch.FilePrefix}signal-{name}";

    /// <summary>Tells the other network check process that this one reached a point, for the times the game itself cannot carry the news.</summary>
    private static void Signal(string name) {
        using var file = Godot.FileAccess.Open(SignalPath(name), Godot.FileAccess.ModeFlags.Write);
        file.StoreString(name);
    }

    /// <summary>Waits for the other network check process to reach a point, and clears the signal so it can be sent again.</summary>
    private async Task Signaled(string name) {
        await Until(() => Godot.FileAccess.FileExists(SignalPath(name)), $"the other process signalling {name}", 90);
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SignalPath(name)));
    }

    private async Task SurvivalReviewShot(string name) {
        if (Launch.Value("--review-captures") is not string folder) return;
        System.IO.Directory.CreateDirectory(folder);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, name + ".png")) == Error.Ok,
            "The rendered survival review capture must save successfully.");
    }

    /// <summary>Join game on the start menu, the address typed into the popup and the port as it comes; tried again until the host is up.</summary>
    private async Task JoinFromStart() {
        for (ulong until = Time.GetTicksMsec() + 45000; !world.Remote;) {
            if (Time.GetTicksMsec() > until) Require(false, $"joining the host never happened: {joinStatus.Text}");
            await Click(FindButton(screen, "Join game")!);
            var port = (LineEdit)screen.FindChild("port", true, false);
            Require(port.Text == Net.DefaultPort.ToString(), "The join popup must come with the port filled in.");
            await Click(port); await KeyStroke(Key.End);
            while (port.Text != "") await KeyStroke(Key.Backspace);
            await Type(CheckPort.ToString());
            var address = (LineEdit)screen.FindChild("address", true, false);
            // The popup offers the address of the last game joined.
            if (address.Text == "") {
                await Click(address); await Type("127.0.0.1");
            } else Require(address.Text == "127.0.0.1", "The join popup must offer the last address joined.");
            await Click(FindButton(screen, "Join")!);
            for (ulong wait = Time.GetTicksMsec() + 4000; Time.GetTicksMsec() < wait && !world.Remote && joinStatus.Text == "Connecting…";) await Frames(1);
            if (!world.Remote) {
                await Click(FindButton(screen, "Back · Esc")!);
                await Until(() => FindButton(screen, "Join game") != null, "the start menu after backing out of a failed join", 5);
            }
        }
        await Until(() => !backdrop.Visible, "the join popup closing");
    }

    private async Task LeaveGame() {
        await CloseWithEsc(); await KeyStroke(Key.Escape);
        Require(menu.Visible && joinButton.Text == "Leave game" && saveButton.Disabled, "A guest's pause menu must offer to leave and not to save the host's world.");
        await Click(joinButton);
        Require(!world.Remote && backdrop.Visible && Godot.FileAccess.FileExists(GuestSave), "Leaving must save the guest's player and return to the start menu.");
    }
}
