using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>
/// A real two-process session on this machine: build.py starts a host and a guest with --net-check host and --net-check guest.
/// Both drive their menus and the world through Input.ParseInputEvent, and each prints its own pass marker.
/// </summary>
public partial class Game {
    private static readonly Vector2 NetOre = Terrain.Spawn + new Vector2(2.5f, 2), NetCoal = Terrain.Spawn + new Vector2(-1.5f, 3.5f), NetChest = Terrain.Spawn + new Vector2(.5f, 3), NetHut = Terrain.Spawn + new Vector2(-3.5f, -3);

    private async Task Until(Func<bool> done, string what, double seconds = 60) {
        for (ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000); !done();) {
            Require(Time.GetTicksMsec() <= until, $"{what} never happened.");
            await Frames(1);
        }
    }

    private async Task Type(string text) {
        foreach (char c in text) {
            Input.ParseInputEvent(new InputEventKey { Keycode = (Key)c, Unicode = c, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = (Key)c, Unicode = c, Pressed = false });
            await Frames(1);
        }
    }

    private async Task RightClick(Vector2 at) {
        var point = GetViewport().GetFinalTransform() * view.Screen(at, .2f);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = point, GlobalPosition = point, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = point, GlobalPosition = point, Pressed = false });
        await Frames();
    }

    private async void RunNetCheck(bool hosting) {
        try {
            if (hosting) await HostNetCheck(); else await GuestNetCheck();
            GD.Print(hosting ? "NET-HOST PASS" : "NET-GUEST PASS"); Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task HostNetCheck() {
        RemoveCheckFiles(); await Frames(4);
        await Click(FindButton(screen, "New game")!); ClosePanel();
        world.S.Things.RemoveAll(t => t.Pos.DistanceTo(Terrain.Spawn) < 6);
        world.Add("loose_coal", NetOre, 2); world.Add("loose_coal", NetCoal, 2);
        var chest = world.Add("chest", NetChest); var hut = world.Add("hut", NetHut);
        await KeyStroke(Key.Escape);
        await Click(hostButton);
        Require(net.Hosting && !menu.Visible && !paused, "Host in the pause menu must open the current game to guests and close the menu.");
        await KeyStroke(Key.Escape);
        float before = world.S.Time; await Seconds(.4);
        Require(menu.Visible && world.S.Time > before && netLine.Text.StartsWith("Hosting on port 24565"), "While hosting, the pause menu must say so and the world must keep going.");
        await KeyStroke(Key.Escape);

        PlayerState Guest() => world.S.Players.Single(p => p != world.Me);
        await Until(() => world.S.Players.Count == 2, "a guest joining", 45);
        Require(Guest().Backpack.Count == 0 && Guest().Pos == Terrain.Spawn, "A first-time guest must start with an empty backpack at the clearing.");
        await Until(() => view.ShowsPlayer(Guest().Id), "the guest's figure appearing");
        await Until(() => chest.Inventory["coal"] == 2 && Guest().Backpack["coal"] == 2, "the guest storing the first loose coal and gathering the second", 90);
        Require(world.S.Backpack["coal"] == 0, "What the guest gathers must not reach the host's backpack.");
        await Until(() => Guest().Sheltered == hut.Id, "the guest walking into the cabin with one click");
        await Until(() => world.S.Players.Count == 1, "the guest leaving");
        await Until(() => world.S.Players.Count == 2 && Guest().Backpack["coal"] == 2, "the guest coming back with its coal");
        await Until(() => world.S.Players.Count == 1, "the guest leaving again");
        await Until(() => world.S.Players.Count == 2, "the guest coming back with a broken save");
        Require(Guest().Backpack.Count == 0 && Guest().Pos == Terrain.Spawn, "A broken guest save must bring the guest in with an empty backpack at the clearing.");
        await Until(() => world.S.Players.Count == 1, "the guest leaving for good");
    }

    private async Task GuestNetCheck() {
        RemoveCheckFiles(); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(GuestSave)); await Frames(4);
        await JoinFromStart();
        Require(!backdrop.Visible && world.S.Players.Count == 2 && world.S.Things.Any(t => t.Kind == "chest" && t.Pos == NetChest), "Joining must replace the start menu with the host's world.");
        Require(world.S.Backpack.Count == 0 && world.Player == Terrain.Spawn, "A first-time guest must start with an empty backpack at the clearing.");
        await Until(() => view.ShowsPlayer(1), "the host's figure appearing");

        await RightClick(NetOre);
        await Until(() => world.S.Backpack["coal"] == 2 && world.S.Things.All(t => t.Pos != NetOre), "the host handing over the loose coal the guest walked to");
        await Click(view.Screen(NetChest, .3f));
        Require(page == "inspect" && world.Find(selected)?.Kind == "chest" && world.Near(world.Find(selected)!), "Clicking the chest beside the coal must open it.");
        await Click(Cell(1, cell => cell.TooltipText.StartsWith(world.Data.Name("coal"))));
        await Until(() => world.Find(selected)!.Inventory["coal"] == 1 && world.S.Backpack["coal"] == 1, "a normal storage click moving exactly one coal");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Ctrl, Keycode = Key.Ctrl, Pressed = true });
        await Click(Cell(1, cell => cell.TooltipText.StartsWith(world.Data.Name("coal"))));
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Ctrl, Keycode = Key.Ctrl, Pressed = false });
        await Until(() => world.Find(selected)!.Inventory["coal"] == 2 && world.S.Backpack["coal"] == 0, "the stored coal showing up in the host's chest");
        ClosePanel(); await RightClick(NetCoal);
        await Until(() => world.S.Backpack["coal"] == 2, "the host handing over the second loose coal");
        // The host checks reach from where the guest stood when it clicked, not from where its glide has got to.
        ClosePanel(); await Click(view.Screen(NetHut, .8f));
        await Until(() => world.S.Sheltered == world.S.Things.Single(t => t.Kind == "hut").Id, "the host letting the guest into the cabin it walked to");

        await LeaveGame();
        await JoinFromStart();
        Require(world.S.Backpack["coal"] == 2, "A guest must come back with what it carried when it left.");
        await LeaveGame();
        using (var file = Godot.FileAccess.Open(GuestSave, Godot.FileAccess.ModeFlags.Write)) file.StoreString("{ broken");
        await JoinFromStart();
        Require(world.S.Backpack.Count == 0 && world.Player == Terrain.Spawn, "A broken save must bring the guest in with an empty backpack at the clearing.");
        await LeaveGame();
    }

    /// <summary>Another port for the guest to join on, such as a proxy that adds lag: --net-check guest --port 24575.</summary>
    private static int CheckPort => LaunchValue("--port") is string port ? int.Parse(port) : Net.DefaultPort;

    private static string GuestSave => $"user://{FilePrefix}guest-127-0-0-1-{CheckPort}.json";

    /// <summary>Join game on the start menu, the address typed into the popup and the port as it comes; tried again until the host is up.</summary>
    private async Task JoinFromStart() {
        for (ulong until = Time.GetTicksMsec() + 45000; !world.Remote;) {
            if (Time.GetTicksMsec() > until) throw new TimeoutException($"Network check failed: could not join: {joinStatus?.Text}");
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
