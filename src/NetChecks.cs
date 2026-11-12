using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

/// <summary>
/// A real two-process session on this machine: build.py starts a host and a guest with --net-check host and --net-check guest.
/// Both drive their menus and the world through Input.ParseInputEvent, and each prints its own pass marker.
/// </summary>
public partial class Game {
    private static readonly Vector2 NetOre = Terrain.Spawn + new Vector2(2.5f, 2), NetCoal = Terrain.Spawn + new Vector2(-1.5f, 3.5f), NetChest = Terrain.Spawn + new Vector2(.5f, 3), NetHut = Terrain.Spawn + new Vector2(-3.5f, -3), NetAnvil = Terrain.Spawn + new Vector2(2.5f, -1.5f);

    private async void RunNetCheck(bool hosting) {
        try {
            if (hosting) await Test(HostNetCheck, slow: true); else await Test(GuestNetCheck, slow: true);
            GD.Print(hosting ? "NET-HOST PASS" : "NET-GUEST PASS"); Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task HostNetCheck() {
        RemoveCheckFiles(); await Frames(4);
        await Click(FindButton(startMenu.Screen, "New game")!);
        await Until(() => world.Remote && !startMenu.Visible, "the new game's server taking this window's player");
        await CloseWithEsc();
        await CheckActionsFinishWhenTheServerAnswers();
        RemoveThings(Hosted, t => t.Pos.DistanceTo(Terrain.Spawn) < 6);
        Hosted.Add(ThingKind.LooseCoal, NetOre, 2); Hosted.Add(ThingKind.LooseCoal, NetCoal, 2);
        var chest = Hosted.Add(ThingKind.Chest, NetChest); var hut = Hosted.Add(ThingKind.Hut, NetHut);
        Hosted.Add(ThingKind.Anvil, NetAnvil).Inventory.Add("steel_bar", 20);
        await KeyStroke(Key.Escape);
        await Seconds(.3f);
        float before = Hosted.Time;
        await Seconds(.5f);
        Require(Hosted.Time == before, "The world must stand still while its only player has the pause menu open.");
        // The server keeps the clock; this window shows it a few updates behind.
        await Until(() => world.Time == before, "this window showing the clock where the server stopped it");
        await Signaled("refused");
        // Each process may make its own key when the check starts, so the guest cannot work this one out.
        Signal("host-key", net.Key);
        await Click(menu.HostButton);
        Require(net.Opened && !menu.Visible, "Host in the pause menu must open the game to other computers and close the menu.");
        Require(await PlayerWalks(), "After hosting the player must be able to walk again.");
        await KeyStroke(Key.Escape);
        var netStatus = (Label)menu.FindChild("net_status", true, false);
        Require(menu.Visible && netStatus.Text == "Hosting on port 24565 · 0 guests", "While hosting, the pause menu must say so.");
        // The guest joins only now, so the count above cannot already include it.
        Signal("hosting");

        PlayerState Guest() => Hosted.Players.Single(p => p.Key != net.Key);
        await Until(() => Hosted.Players.Count == 2, "a guest joining", 45);
        await Until(() => netStatus.Text.EndsWith("· 1 guest"), "the open pause menu counting the guest who joined");
        float playing = Hosted.Time;
        await Until(() => Hosted.Time > playing, "the world going on behind the host's pause menu while a guest plays", 5);
        await KeyStroke(Key.Escape);
        Require(HoldsNewcomerKit(Guest().Backpack) && Guest().Pos == Terrain.Spawn, "A first-time guest must start with an axe and a bottle at the clearing.");
        await Until(() => view.ShowsPlayer(Guest().Id), "the guest's figure appearing");
        await Until(() => view.OtherAnimation(Guest().Id) == ActorAction.Hammer, "the host seeing the guest hammer at the anvil beside it");
        Signal("saw-hammering");
        await Until(() => chest.Inventory["coal"] == 2 && Guest().Backpack["coal"] == 2, "the guest storing the first loose coal and gathering the second", 90);
        Require(world.Backpack["coal"] == 0, "What the guest gathers must not reach the host's backpack.");
        await Until(() => Guest().Sheltered == hut.Id, "the guest walking into the cabin with one click");
        await Until(() => Hosted.Players.Count == 1, "the guest leaving");
        await Until(() => Hosted.Players.Count == 2 && Guest().Backpack["coal"] == 2, "the guest coming back with its coal");
        // The save keeps the guest's player, so a guest whose line drops with the host comes back with its coal once the host loads it.
        await Signaled("joined");
        await KeyStroke(Key.Escape); await Click(menu.SaveButton); await Click(FindButton(startMenu.Screen, Saves.SlotLabel(1))!);
        await Until(() => Saves.SlotUsed(1), "the server writing the save");
        ShowStart();
        await LoadAndHost(1);
        await Until(() => Hosted.Players.Count == 2 && Guest().Backpack["coal"] == 2, "the guest reconnecting by itself with its coal", 90);
        await Until(() => Hosted.Players.Count == 1, "the guest leaving again");
        await Until(() => Hosted.Players.Count == 2, "the guest joining again");
        // The host drops out again while the guest stops trying, joins by hand, and drops for good when this check ends.
        await Signaled("joined");
        ShowStart();
        await Signaled("stopped");
        await LoadAndHost(1);
        await Until(() => Hosted.Players.Count == 2, "the guest joining by hand after it stopped trying", 90);
        await Signaled("joined");
    }

    /// <summary>A window sends its player's actions and hears the result later, so what follows one must not need it at once.</summary>
    private async Task CheckActionsFinishWhenTheServerAnswers() {
        var me = Hosted.Players.Single();
        Give(me, "wood", world.Data.Buildings[ThingKind.Chest].Cost["wood"]); Give(me, Inventory.Coins, 100); Give(me, "stone");
        await Until(() => world.Backpack.Has(world.Data.Buildings[ThingKind.Chest].Cost) && world.Backpack["stone"] == 1, "this window's backpack showing what the server gave it");
        await KeyStroke(Key.B); await Click(Named("build_chest")!);
        var spot = FreeSpotNearby(ThingKind.Chest);
        int chests = world.Things.Count(t => t.Kind == ThingKind.Chest);
        await Click(view.Screen(spot));
        Require(player.Mode.Placing == null, "Placing the last chest the backpack pays for must end placement.");
        await Until(() => world.Things.Count(t => t.Kind == ThingKind.Chest) == chests + 1, "the placed chest coming back from the server");
        // A drag reaches the same action, and the UI checks cover it; only the sound after it is under test here.
        string itemSound = Sounds.ForItem("stone");
        Require(sounds.LastVariant(itemSound) < 0, "Nothing may have played the item's sound before the move.");
        var placed = world.Things.Last(t => t.Kind == ThingKind.Chest);
        Require(world.Near(placed), "The chest must be in reach of the player who placed it.");
        kit.Run(() => world.Transfer(world.Backpack, placed.Inventory, "stone", 1));
        await Until(() => sounds.LastVariant(itemSound) >= 0, "the item's sound once the move comes back from the server", 1);
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        await Until(() => world.Find(sign.Id) != null, "this window showing the signpost");
        await Click(view.Screen(sign.Pos, .8f)); await Click(Named("hardhat")!);
        await Click(Named($"hire_{world.Data.HireOffers[0].Upfront:0}")!);
        await Until(() => panel.Selection.Worker != 0, "the panel opening on the worker the server hired");
        Hosted.Fire(Hosted.Workers[^1]);
        // The page closes by itself once this window hears of it; an Esc pressed just before that would land after it and open the pause menu.
        await Until(() => !panel.Visible, "the fired worker's page closing once this window hears they left", 5);
        await CloseWithEsc();
    }

    /// <summary>Loads a save slot from the start menu and opens the game to other computers.</summary>
    private async Task LoadAndHost(int slot) {
        await Click(FindButton(startMenu.Screen, "Load game")!); await Click(FindButton(startMenu.Screen, Saves.SlotLabel(slot))!);
        await Until(() => world.Remote && !startMenu.Visible, "the loaded game's server taking this window's player");
        await CloseWithEsc(); await KeyStroke(Key.Escape); await Click(menu.HostButton);
        Require(net.Opened, "Host must open the loaded game to other computers.");
    }

    private async Task GuestNetCheck() {
        await CheckFirstTimeGuestActsThroughTheHost();
        await CheckGuestComesBackFromLeavingAndDropping();
    }

    private async Task CheckFirstTimeGuestActsThroughTheHost() {
        RemoveCheckFiles(); await Frames(4);
        await JoinUntilRefused("The host has not opened the game to other players.");
        Signal("refused");
        await Signaled("hosting");
        await JoinFromStart();
        Require(!startMenu.Visible, "Joining must close the start menu.");
        Require(world.Players.Count == 2, "Joining must add the guest to the host's world.");
        Require(world.Things.Any(t => t.Kind == ThingKind.Chest && t.Pos == NetChest),
            "Joining must bring the host's chest into the guest's world.");
        Require(HoldsNewcomerKit(world.Backpack) && world.Player == Terrain.Spawn, "A first-time guest must start with an axe and a bottle at the clearing.");
        int hostId = world.Players.Single(p => p != world.Me).Id;
        await Until(() => view.ShowsPlayer(hostId), "the host's figure appearing");
        await Until(() => view.OtherAnimation(hostId) == ActorAction.Hammer, "the guest seeing the host hammer at the anvil beside it");
        await Signaled("saw-hammering");

        await RightClick(NetOre);
        await Until(() => world.Backpack["coal"] == 2 && world.Things.All(t => t.Pos != NetOre), "the host handing over the loose coal the guest walked to");
        await Click(view.Screen(NetChest, .3f));
        var opened = world.Find(view.Selection.Thing);
        Require(opened?.Kind == ThingKind.Chest, "Clicking the chest beside the coal must select that chest.");
        Require(Shows(opened), "Clicking the chest beside the coal must open its page.");
        Require(world.Near(opened), "Clicking the chest beside the coal must leave the player in reach of it.");
        await Click(Cell(1, cell => cell.TooltipText.StartsWith(world.Data.Name("coal"))));
        await Until(() => opened!.Inventory["coal"] == 1 && world.Backpack["coal"] == 1, "a normal storage click moving exactly one coal");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Ctrl, Keycode = Key.Ctrl, Pressed = true });
        await Click(Cell(1, cell => cell.TooltipText.StartsWith(world.Data.Name("coal"))));
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Ctrl, Keycode = Key.Ctrl, Pressed = false });
        await Until(() => opened!.Inventory["coal"] == 2 && world.Backpack["coal"] == 0, "the stored coal showing up in the host's chest");
        await CloseWithEsc(); await RightClick(NetCoal);
        await Until(() => world.Backpack["coal"] == 2, "the host handing over the second loose coal");
        // The host checks reach from where the guest stood when it clicked, not from where its glide has got to.
        await CloseWithEsc(); await Click(view.Screen(NetHut, .8f));
        await Until(() => world.Sheltered == world.Things.Single(t => t.Kind == ThingKind.Hut).Id, "the host letting the guest into the cabin it walked to");
    }

    private async Task CheckGuestComesBackFromLeavingAndDropping() {
        await LeaveGame();
        // Two windows on one computer share a key, which this check's host and guest do not: no input changes a window's key, so this one borrows the host's.
        string own = net.Key; net.Key = await Signaled("host-key");
        await JoinUntilRefused("This computer is already in this game.");
        net.Key = own;
        // The start menu covers the world and Esc ends a click mode before Join can be reached, so no input leaves either in place for a join.
        panel.Mark(world.Things.First()); player.Mode = new ClickMode.Build(ThingKind.Chest); await Frames();
        Require(view.Selection != default, "The guest must have something selected before it joins.");
        await JoinFromStart(); await Frames();
        Require(player.Mode is ClickMode.Normal, "Joining must end placement.");
        Require(view.Selection == default, "Joining must clear the selection.");
        Require(world.Backpack["coal"] == 2, "A guest must come back with what it carried when it left.");
        await CloseWithEsc(); await Click(view.Screen(NetChest, .3f));
        Require(view.Selection != default, "The guest must select the chest before the line drops.");
        await KeyStroke(Key.Key3);
        Require(player.Mode.Placing == ThingKind.Chest, "A plan's hotkey must enter placement before the line drops.");
        Signal("joined");
        await Until(() => startMenu.Visible && HasLabel(startMenu.Screen, "RECONNECTING"), "a dropped guest being told it is reconnecting", 60);
        await Frames();
        Require(player.Mode is ClickMode.Normal, "A lost connection must end placement.");
        Require(view.Selection == default, "A lost connection must clear the selection.");
        await Until(() => world.Remote && !startMenu.Visible, "a dropped guest getting back in by itself", 90);
        Require(world.Backpack["coal"] == 2, "A guest must reconnect with what it carried when the line dropped.");
        await LeaveGame();
        await JoinFromStart();
        Signal("joined");
        await Until(() => startMenu.Visible && HasLabel(startMenu.Screen, "RECONNECTING"), "the guest being told it is reconnecting again", 60);
        await Click(FindButton(startMenu.Screen, "Stop · Esc")!);
        Require(!net.Retrying && FindButton(startMenu.Screen, "Join game") != null, "Stopping must end the tries and return to the start menu.");
        Signal("stopped");
        net.ReconnectLimit = 3;
        await JoinFromStart();
        Signal("joined");
        await Until(() => startMenu.Visible && HasLabel(startMenu.Screen, "Lost the connection to the host") && FindButton(startMenu.Screen, "Join game") != null, "a guest giving up on a host that stays gone and saying why", 90);
        Require(!net.Retrying && !world.Remote, "A guest that gave up must stop trying.");
    }

}
