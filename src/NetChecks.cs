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
        world.Add(ThingKind.LooseCoal, NetOre, 2); world.Add(ThingKind.LooseCoal, NetCoal, 2);
        var chest = world.Add(ThingKind.Chest, NetChest); var hut = world.Add(ThingKind.Hut, NetHut);
        await KeyStroke(Key.Escape);
        await Click(hostButton);
        Require(net.Hosting && !menu.Visible && !paused, "Host in the pause menu must open the current game to guests and close the menu.");
        await KeyStroke(Key.Escape);
        float before = world.S.Time;
        await Until(() => world.S.Time > before, "the world going on behind the host's pause menu", 5);
        Require(menu.Visible && netLine.Text.StartsWith("Hosting on port 24565"), "While hosting, the pause menu must say so.");
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
        // The guest's line drops with the host, and it returns once the same port opens again.
        await Signaled("joined");
        net.Leave();
        await KeyStroke(Key.Escape); await Click(hostButton);
        await Until(() => world.S.Players.Count == 2 && Guest().Backpack["coal"] == 2, "the guest reconnecting by itself with its coal", 90);
        await Until(() => world.S.Players.Count == 1, "the guest leaving again");
        await Until(() => world.S.Players.Count == 2, "the guest coming back with a broken save");
        Require(Guest().Backpack.Count == 0 && Guest().Pos == Terrain.Spawn, "A broken guest save must bring the guest in with an empty backpack at the clearing.");
        // The host drops out again while the guest stops trying, joins by hand, and drops for good when this check ends.
        await Signaled("joined");
        net.Leave();
        await Signaled("stopped");
        await KeyStroke(Key.Escape); await Click(hostButton);
        await Until(() => world.S.Players.Count == 2, "the guest joining by hand after it stopped trying", 90);
        await Signaled("joined");
    }

    private async Task GuestNetCheck() {
        RemoveCheckFiles(); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(GuestSave)); await Frames(4);
        await JoinFromStart();
        Require(!backdrop.Visible && world.S.Players.Count == 2 && world.S.Things.Any(t => t.Kind == ThingKind.Chest && t.Pos == NetChest), "Joining must replace the start menu with the host's world.");
        Require(world.S.Backpack.Count == 0 && world.Player == Terrain.Spawn, "A first-time guest must start with an empty backpack at the clearing.");
        await Until(() => view.ShowsPlayer(1), "the host's figure appearing");

        await RightClick(NetOre);
        await Until(() => world.S.Backpack["coal"] == 2 && world.S.Things.All(t => t.Pos != NetOre), "the host handing over the loose coal the guest walked to");
        await Click(view.Screen(NetChest, .3f));
        Require(page == "inspect" && world.Find(selected)?.Kind == ThingKind.Chest && world.Near(world.Find(selected)!), "Clicking the chest beside the coal must open it.");
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
        await Until(() => world.S.Sheltered == world.S.Things.Single(t => t.Kind == ThingKind.Hut).Id, "the host letting the guest into the cabin it walked to");

        await LeaveGame();
        await JoinFromStart();
        Require(world.S.Backpack["coal"] == 2, "A guest must come back with what it carried when it left.");
        Signal("joined");
        await Until(() => backdrop.Visible && HasLabel(screen, "RECONNECTING"), "a dropped guest being told it is reconnecting", 60);
        await Until(() => world.Remote && !backdrop.Visible, "a dropped guest getting back in by itself", 90);
        Require(world.S.Backpack["coal"] == 2, "A guest must reconnect with what it carried when the line dropped.");
        await LeaveGame();
        using (var file = Godot.FileAccess.Open(GuestSave, Godot.FileAccess.ModeFlags.Write)) file.StoreString("{ broken");
        await JoinFromStart();
        Require(world.S.Backpack.Count == 0 && world.Player == Terrain.Spawn, "A broken save must bring the guest in with an empty backpack at the clearing.");
        Signal("joined");
        await Until(() => backdrop.Visible && HasLabel(screen, "RECONNECTING"), "the guest being told it is reconnecting again", 60);
        await Click(FindButton(screen, "Stop · Esc")!);
        Require(!net.Retrying && FindButton(screen, "Join game") != null, "Stopping must end the tries and return to the start menu.");
        Signal("stopped");
        net.ReconnectLimit = 3;
        await JoinFromStart();
        Signal("joined");
        await Until(() => backdrop.Visible && HasLabel(screen, "Lost the connection to the host") && FindButton(screen, "Join game") != null, "a guest giving up on a host that stays gone and saying why", 90);
        Require(!net.Retrying && !world.Remote, "A guest that gave up must stop trying.");
    }

    /// <summary>Another port for the guest to join on, such as a proxy that adds lag: --net-check guest --port 24575.</summary>
    private static int CheckPort => LaunchValue("--port") is string port ? int.Parse(port) : Net.DefaultPort;

    private static string GuestSave => $"user://{FilePrefix}guest-127-0-0-1-{CheckPort}.json";

}
