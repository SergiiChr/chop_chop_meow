using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The UI checks of gravel and gravel paths: taking a tile up, walking on gravel, and what gravel sounds like.
    /// </summary>
    private async Task RunGravelPathUIChecks() {
        await TestClean(CheckRemoveSelectorTakesUpAGravelPathTile);
        await TestClean(CheckGravelMovedByHandRattlesLikeStone);
        await TestClean(CheckGravelIsFasterUnderfootAndCrunches);
        await TestClean(CheckABenchCrushingRocksSoundsLikeMiningStone);
        await TestClean(CheckStumpAPathTileIsLaidOnGoesWithoutBeingThrownUp);
    }

    /// <summary>A gravel path tile cannot be clicked in normal play; the remove selector turns it red and takes it up, giving its gravel back, and Esc puts the selector away.</summary>
    private async Task CheckRemoveSelectorTakesUpAGravelPathTile() {
        await CloseWithEsc();
        Give(Hosted, "gravel", 4);
        var tile = SpotsNearby().Select(Terrain.TileAt)
            .First(t => Hosted.GravelPaths.Block(t) == null && view.Pick(view.Screen(t)) == null && view.PickWorker(view.Screen(t)) == null && Uncovered(view.Screen(t)));
        Hosted.Construction.Build(ThingKind.GravelPath, tile); await Frames();
        Require(world.GravelPaths.Laid(tile), "The window must see a path tile laid on the server.");
        int gravel = world.Backpack["gravel"];
        await Click(view.Screen(tile));
        Require(!panel.Visible, $"A click on a gravel path tile must open no page, not {panel.Page} for thing {panel.Selection.Thing}.");
        await KeyStroke(Key.B);
        await Click(Named("remove")!);
        await Hover(view.Screen(tile));
        Require(view.RemovalShown.Tile == tile, "A path tile in reach under the remove selector must turn red.");
        await Click(view.Screen(tile));
        await Until(() => !world.GravelPaths.Laid(tile), "a click with the remove selector taking the path tile up", 2);
        Require(world.Backpack["gravel"] == gravel + 4, "Taking a path tile up must give its 4 gravel back.");
        await KeyStroke(Key.Escape);
        Require(player.Mode is ClickMode.Normal, "Esc must put the remove selector away.");
        Drop(Hosted, "gravel", 4); await Frames();
    }

    /// <summary>Gravel taken out of a chest by hand rattles like stone.</summary>
    private async Task CheckGravelMovedByHandRattlesLikeStone() {
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(2, -2)); chest.Inventory.Add("gravel", 3);
        await ClickOn(chest);
        int soundsBefore = sounds.Played;
        await PickUp(await Showing(0, c => c.TooltipText.StartsWith("Gravel", StringComparison.Ordinal)));
        await Click(BagCell(c => c.Visible && c.Icon == null));
        await Until(() => cursor.Empty && world.Backpack["gravel"] == 3 && sounds.Heard("wood_rocks_inventory", soundsBefore), "the chest's gravel landing in the backpack with the sound stone makes", 2);
        Drop(Hosted, "gravel", 3); RemoveThings(Hosted, t => t == chest); await CloseWithEsc();
    }

    /// <summary>On a gravel path the player moves 1.2 times as fast as on grass and their steps crunch.</summary>
    private async Task CheckGravelIsFasterUnderfootAndCrunches() {
        await CloseWithEsc();
        // The D key walks toward higher x and lower y, so the path is a chain of tiles corner to corner that way.
        var home = world.Player;
        var start = Terrain.TileAt(home); var way = new Vector2I(1, -1);
        float grass = await WalkingSpeed(start);
        // From the middle of the path to be, every tile is within reach.
        Warp(Hosted, start + way * 2); await Frames(4);
        for (int i = 0; i <= 4; i++) GravelPathSteps.LayPath(Hosted, start + way * i);
        await Frames();
        Require(world.GravelPaths.Covers(start + new Vector2(2.5f, -2.5f)), "The window must see the path laid on the server, joined corner to corner.");
        int soundsBefore = sounds.Played;
        float gravel = await WalkingSpeed(start);
        Require(sounds.Heard("footsteps_gravel", soundsBefore), "Steps on a gravel path must crunch.");
        Require(Math.Abs(grass - 4.2f) < .3f && Math.Abs(gravel - 5.04f) < .3f, $"The player must walk 4.2 tiles a second on grass and 5.04 on gravel, not {grass} and {gravel}.");
        for (int i = 0; i <= 4; i++) Hosted.GravelPaths.TakeUp(start + way * i);
        Drop(Hosted, "gravel", 20); Warp(Hosted, home); await Frames(4);
    }

    /// <summary>A bench crushing rocks rings like a pickaxe on stone under each hammer blow, in place of its wooden knock.</summary>
    private async Task CheckABenchCrushingRocksSoundsLikeMiningStone() {
        int soundsBefore = sounds.Played;
        var bench = Hosted.Add(ThingKind.Bench, Hosted.Player + new Vector2(1.2f, 0));
        bench.Tools!.Add("steel_hammer", 1); Give(Hosted, "stone", 6); Hosted.QueueBatch(bench, "gravel", 6);
        await Until(() => bench.Processing && view.PlayerAnimation == ActorAction.Hammer && sounds.Heard("mine_ore", soundsBefore), "the idle player crushing rocks at the bench beside it with the sound of mining stone", 4);
        RemoveThings(Hosted, t => t == bench);
    }

    /// <summary>A stump a gravel path tile is laid on just goes, as one under a building does.</summary>
    private async Task CheckStumpAPathTileIsLaidOnGoesWithoutBeingThrownUp() {
        var tile = Terrain.TileAt(Hosted.Player + new Vector2(2, 2));
        var stump = Hosted.Add(ThingKind.Stump, tile, World.StumpWood); stump.Progress = GatherRules.DigSeconds * .8f;
        await Frames(2);
        Require(view.PropOf(stump.Id) is DugProp { Shown: true }, "A stump in sight must be drawn.");
        GravelPathSteps.LayPath(Hosted, tile);
        await Frames(2);
        Require(world.GravelPaths.Laid(tile) && world.Find(stump.Id) == null, "A path tile must go down on a stump and clear it away.");
        Require(view.Departing == 0 && view.PropOf(stump.Id) is not { Shown: true }, "A stump a path tile is laid on must just go, not be thrown up through the gravel.");
        Hosted.GravelPaths.TakeUp(tile); Drop(Hosted, "gravel", 4); await Frames(2);
    }
}
