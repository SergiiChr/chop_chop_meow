using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>Starts and leaves games for the UI and network checks.</summary>
public partial class Game {
    private static void RemoveCheckFiles() {
        foreach (var path in Enumerable.Range(0, SlotCount + 1).Select(SlotPath).Append(SettingsPath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    private async Task SurvivalReviewShot(string name) {
        if (LaunchValue("--review-captures") is not string folder) return;
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
            port.Text = CheckPort.ToString();
            var address = (LineEdit)screen.FindChild("address", true, false);
            // The popup offers the address of the last game joined.
            if (address.Text == "") await Type("127.0.0.1");
            else Require(address.Text == "127.0.0.1", "The join popup must offer the last address joined.");
            await Click(FindButton(screen, "Join")!);
            for (ulong wait = Time.GetTicksMsec() + 4000; Time.GetTicksMsec() < wait && !world.Remote && joinStatus.Text == "Connecting…";) await Frames(1);
            if (!world.Remote) { await Click(FindButton(screen, "Back · Esc")!); await Seconds(1); }
        }
        await Until(() => !backdrop.Visible, "the join popup closing");
    }

    private async Task LeaveGame() {
        ClosePanel(); await KeyStroke(Key.Escape);
        Require(menu.Visible && joinButton.Text == "Leave game" && saveButton.Disabled, "A guest's pause menu must offer to leave and not to save the host's world.");
        await Click(joinButton);
        Require(!world.Remote && backdrop.Visible && Godot.FileAccess.FileExists(GuestSave), "Leaving must save the guest's player and return to the start menu.");
    }
}
