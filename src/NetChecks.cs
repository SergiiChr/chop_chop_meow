using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

/// <summary>
/// A real two-process session on this machine: build.py starts a host and a guest with --net-check host and --net-check guest.
/// Both drive their menus and the world through Input.ParseInputEvent, and each prints its own pass marker.
/// </summary>
public partial class Game {
    private static readonly Vector2 NetOre = Terrain.Spawn + new Vector2(2.5f, 2), NetCoal = Terrain.Spawn + new Vector2(-1.5f, 3.5f), NetChest = Terrain.Spawn + new Vector2(.5f, 3), NetHut = Terrain.Spawn + new Vector2(-3.5f, -3), NetAnvil = Terrain.Spawn + new Vector2(1.2f, -.6f);
    /// <summary>The way the guest steps before it swings: left on screen, away from the anvil.</summary>
    private static readonly Vector2 NetSwingAim = PlayerControl.KeyDirection(-1, 0);

    private async void RunNetCheck(bool hosting) {
        try {
            if (hosting) await Test(HostNetCheck, slow: true); else await Test(GuestNetCheck, slow: true);
            GD.Print(hosting ? "NET-HOST PASS" : "NET-GUEST PASS"); Quit();
        } catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    /// <summary>The path tile the host lays before the guest joins, and the tile corner to corner with it that the guest lays.</summary>
    private static readonly Vector2I NetPath = Terrain.TileAt(Terrain.Spawn + new Vector2(3, -2)), NetLaid = NetPath + new Vector2I(1, -1);

    private async Task HostNetCheck() {
        await Frames(4);
        await Click(FindButton(startMenu.Screen, "New game")!);
        await Until(() => world.Remote && !startMenu.Visible, "the new game's server taking this window's player");
        await CloseWithEsc();
        await CheckActionsFinishWhenTheServerAnswers();
        RemoveThings(Hosted, t => t.Pos.DistanceTo(Terrain.Spawn) < 6);
        Hosted.Add(ThingKind.LooseCoal, NetOre, 2); Hosted.Add(ThingKind.LooseCoal, NetCoal, 2);
        var chest = Hosted.Add(ThingKind.Chest, NetChest); var hut = Hosted.Add(ThingKind.Hut, NetHut);
        Hosted.Add(ThingKind.Anvil, NetAnvil).Inventory.Add("steel_bar", 20);
        GravelPathSteps.LayPath(Hosted, NetPath);
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
        Require(menu.Visible && netStatus.Text == $"Hosting on port {Net.DefaultPort} · 0 guests", "While hosting, the pause menu must say so.");
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
        await CheckAGuestsFightIsDecidedOnTheHost(Guest());
        await Until(() => !Hosted.GravelPaths.Laid(NetPath) && Hosted.GravelPaths.Laid(NetLaid), "the guest taking up the host's path tile and laying it beside where it was", 90);
        await Until(() => world.GravelPaths.Laid(NetLaid) && !world.GravelPaths.Laid(NetPath), "the host's window showing the tile the guest laid");
        Require(world.Backpack["gravel"] == 0, "Gravel from a tile the guest takes up must not reach the host's backpack.");
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

    /// <summary>The host's half of the fight: two wolves on the guest, one where the guest's chop is aimed and one to its side.</summary>
    private async Task CheckAGuestsFightIsDecidedOnTheHost(PlayerState guest) {
        var stood = GD.StrToVar(await Signaled("fight")).AsVector2();
        await Until(() => guest.Pos.DistanceTo(stood) < .05f, "the host hearing where the guest stopped");
        // By day wolves are calmed and sent home at once.
        RunUntilNight(Hosted); ShutDens(Hosted); SetNeeds(guest, 100, 100, 100);
        var ahead = PlaceWolf(Hosted, new Wolf { Pos = guest.Pos + NetSwingAim * 1.2f, Target = guest.Id });
        var beside = PlaceWolf(Hosted, new Wolf { Pos = guest.Pos + PlayerControl.KeyDirection(0, 1) * 1.2f, Target = guest.Id });
        await Until(() => view.OtherAnimation(guest.Id) == ActorAction.SteelAxeSwing, "the host seeing the guest chop", 10);
        await Until(() => ahead.Health < Hosted.Data.WolfHealth, "the guest's chop landing on the wolf it was aimed at", 5);
        // A steel axe takes 2.2 to 3.2 off a wolf's 8.
        Require(ahead.Health is > 4.7f and < 5.9f, $"One chop of a steel axe must take 2.2 to 3.2 off the wolf ahead, which has {ahead.Health} left.");
        Require(beside.Health == 8, "A chop must miss a wolf standing to the side of it.");
        await Until(() => guest.Health <= 85, "a wolf's snap taking 15 health off a guest who stands still", 10);
        await Signaled("fought");
        RemoveWolves(Hosted, wolf => true); SetNeeds(guest, health: 100); RunUntilMorning(Hosted); NourishEveryone(Hosted);
        Signal("fight-over");
    }

    /// <summary>A window sends its player's actions and hears the result later, so what follows one must not need it at once.</summary>
    private async Task CheckActionsFinishWhenTheServerAnswers() {
        var me = Hosted.Players.Single();
        Give(me, "wood", world.Data.Buildings[ThingKind.Chest].Cost["wood"]); Give(me, Inventory.Coins, 100); Give(me, "stone");
        await Until(() => world.Backpack.Has(world.Data.Buildings[ThingKind.Chest].Cost) && world.Backpack["stone"] == 1, "this window's backpack showing what the server gave it");
        await KeyStroke(Key.B); await DoubleClick(Named("build_chest")!);
        var spot = FreeSpotNearby(ThingKind.Chest);
        int chests = world.Things.Count(t => t.Kind == ThingKind.Chest);
        await Click(view.Screen(spot));
        Require(player.Mode.Placing == null, "Placing the last chest the backpack pays for must end placement.");
        await Until(() => world.Things.Count(t => t.Kind == ThingKind.Chest) == chests + 1, "the placed chest coming back from the server");
        // A held stack put down reaches the same action, and the UI checks cover it; only the sound after it is under test here.
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
        await Frames(4);
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
        await CheckAGuestFightsWolvesThroughTheHost();

        // The host's gravel path comes up for the guest's remove selector, and goes down again where the guest lays it.
        Require(world.GravelPaths.Laid(NetPath), "Joining must bring the host's gravel path into the guest's world.");
        await KeyStroke(Key.B); await Click(Named("remove")!);
        await Click(view.Screen(NetPath));
        await Until(() => !world.GravelPaths.Laid(NetPath) && world.Backpack["gravel"] == 4, "the host taking up the path tile the guest clicked with the remove selector");
        await KeyStroke(Key.Escape);
        await KeyStroke(Key.B); await PickUp(Named("build_gravel_path")!);
        await Click(view.Screen(NetLaid));
        await Until(() => world.GravelPaths.Laid(NetLaid) && world.Backpack["gravel"] == 0, "the host laying the path tile the guest placed");
        await CloseWithEsc();

        await RightClick(NetOre);
        await Until(() => world.Backpack["coal"] == 2 && world.Things.All(t => t.Pos != NetOre), "the host handing over the loose coal the guest walked to");
        await Click(view.Screen(NetChest, .3f));
        var opened = world.Find(view.Selection.Thing);
        Require(opened?.Kind == ThingKind.Chest, "Clicking the chest beside the coal must select that chest.");
        Require(Shows(opened), "Clicking the chest beside the coal must open its page.");
        Require(world.Near(opened), "Clicking the chest beside the coal must leave the player in reach of it.");
        await PickUp(Cell(1, cell => cell.TooltipText.StartsWith(world.Data.Name("coal"))));
        await RightClick(Cell(0, cell => cell.TooltipText == ""));
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty && opened!.Inventory["coal"] == 1 && world.Backpack["coal"] == 1, "a right click with coal held storing exactly one coal");
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

    /// <summary>
    /// The guest's half of the fight: Space swings the way the guest last walked, the host decides what the chop touches,
    /// and the guest's window shows the wolf it hurt and the bite it took.
    /// </summary>
    private async Task CheckAGuestFightsWolvesThroughTheHost() {
        // Space swings the way the guest last walked: a step to the left on screen aims the chop at a wolf there.
        await Holding(Key.A, () => Frames(8));
        await Frames();
        Signal("fight", GD.VarToStr(world.Player));
        await Until(() => world.Wolves.Count == 2, "the host's two wolves showing up beside the guest", 30);
        await KeyStroke(Key.Space);
        await Until(() => world.Wolves.Any(wolf => wolf.Health is > 4.7f and < 5.9f), "the guest's window showing the wolf its chop hurt", 10);
        await Until(() => sounds.LastVariant("wolf_hit") >= 0, "the guest hearing its chop land", 5);
        await Until(() => world.Health <= 85, "the guest's window showing the bite it took", 10);
        Signal("fought");
        await Signaled("fight-over");
        await Until(() => world.Wolves.Count == 0 && world.Health == 100, "the guest's window showing the wolves gone and its health back");
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
