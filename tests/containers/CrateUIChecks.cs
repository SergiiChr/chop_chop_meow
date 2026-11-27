using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunCrateUIChecks() {
        await TestClean(CheckPackedCrateOpensFromTheBackpack);
        await TestClean(CheckEmptyCrateOpensAsStorageAndClosesWhenPickedUp);
        await TestClean(CheckStackLargerThanCrateFillsItAndRestStaysHeld);
        // A crate put into a crate ends the game on the start menu, so this check runs last.
        await TestClean(CheckCratePutIntoCrateStopsAtWarning);
    }

    /// <summary>Wood left on the cursor after filling one crate can be put into another after walking to it.</summary>
    private async Task CheckStackLargerThanCrateFillsItAndRestStaysHeld() {
        var home = Hosted.Player;
        Drop(Hosted, "wood", world.Backpack["wood"]);
        Require(Give(Hosted, "wood", 64) == 64, "The backpack must have room for a stack of wood.");
        var crates = new[] { Hosted.Add(ThingKind.Crate, world.Player + new Vector2(-1, 0)), Hosted.Add(ThingKind.Crate, world.Player + new Vector2(5, -5)) };
        int cap = world.Data.CrateCapacity;
        await ClickOn(crates[0]);
        await Carry(Cell(1, c => c.TooltipText.Contains("Wood · 64 total")), Cell(0, c => c.TooltipText == ""));
        await Until(() => crates[0].Inventory["wood"] == cap, "the held stack filling the crate", 2);
        Require(world.Held is { Item: "wood", Count: var rest } && rest == 64 - cap, "What did not fit into the crate must stay held.");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Until(() => !world.Near(crates[0]) && world.Near(crates[1]), "walking out of reach of the first crate and up to the second", 10);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        Require(!cursor.Empty, "Walking to the next crate must keep the rest held.");
        await ClickOn(crates[1]);
        await Until(() => crates[1].Inventory["wood"] == cap, "the held rest filling the next crate", 2);
        Require(world.Held is { Count: var left } && left == 64 - 2 * cap, "What still does not fit must stay held.");
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty, "letting go of the leftover wood", 2);
        Require(world.Backpack["wood"] == 64 - 2 * cap, "Letting go must leave the rest in the backpack.");
        foreach (var crate in crates) RemoveThings(Hosted, t => t == crate);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }
    /// <summary>A double click on a packed crate in the backpack opens it, and its contents move to the backpack and back by Shift-click.</summary>
    private async Task CheckPackedCrateOpensFromTheBackpack() {
        // Berries already carried would join the five taken out in one stack, which Shift would move back whole.
        int carried = Drop(Hosted, "berries"); Give(Hosted, "crate", 1); Give(Hosted, "berries", 5); Give(Hosted, "stone", 1);
        var placed = Hosted.PlaceCrate("crate", Hosted.Player + new Vector2(0, 2))!;
        // The stone keeps the crate from emptying, which would close its page, when the berries come out.
        Hosted.Transfer(Hosted.Backpack, placed.Inventory, "berries", 5); Hosted.Transfer(Hosted.Backpack, placed.Inventory, "stone", 1); Hosted.PickUpCrate(placed);
        int berries = Hosted.Backpack["berries"];
        await KeyStroke(Key.Tab);
        await DoubleClick(await Showing(0, c => c.TooltipText.StartsWith("Wood crate of")));
        await Until(() => panel.Visible && panel.HeadingTitle.Text == "Wood crate", "a double click on a packed crate in the backpack opening it", 2);
        await Holding(Key.Shift, async () => await Click(await Showing(0, c => c.TooltipText.StartsWith("Berries"))));
        await Until(() => world.Backpack["berries"] == berries + 5, "Shift on berries in the open crate taking the stack out into the backpack", 2);
        await Holding(Key.Shift, async () => await Click(await Showing(1, c => c.TooltipText.StartsWith("Berries"))));
        await Until(() => world.Backpack["berries"] == berries, "Shift on the berries in the backpack packing that stack back into the open crate", 2);
        await CloseWithEsc();
        Drop(Hosted, world.Backpack.Items.Keys.First(World.IsPacked)); Give(Hosted, "berries", carried); await Frames();
    }
    /// <summary>An empty crate opens as storage by double click, goods put in pack it, and picking up the open crate closes its page.</summary>
    private async Task CheckEmptyCrateOpensAsStorageAndClosesWhenPickedUp() {
        await CloseWithEsc();
        Give(Hosted, "crate", 1); Give(Hosted, "stone", 3);
        await KeyStroke(Key.Tab);
        await DoubleClick(await Showing(0, c => c.TooltipText.StartsWith("Wood crate ·")));
        await Until(() => panel.Page == Page.Crate && Cells(0).Any(), "a double click on an empty crate opening it as storage", 2);
        await Carry(BagCell(c => c.TooltipText.StartsWith("Stone")), Cell(0, c => c.TooltipText == ""));
        await Until(() => world.Backpack.Items.Keys.Any(World.IsPacked) && Cells(0).Any(c => c.TooltipText.StartsWith("Stone")),
            "the stone packing the empty crate, whose page shows it", 2);
        Require(world.Backpack["crate"] == 0 && world.Backpack["stone"] == 0, "Packing the empty crate must use it and all three stone.");
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood crate of")));
        await Until(() => panel.Page == Page.Backpack, "picking up the open crate closing its page for the backpack's", 2);
        await KeyStroke(Key.Escape); await CloseWithEsc();
        Drop(Hosted, world.Backpack.Items.Keys.First(World.IsPacked)); await Frames();
    }
    private async Task CheckCratePutIntoCrateStopsAtWarning() {
        var crate = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(1, 0));
        var box = Hosted.Add(ThingKind.Crate, world.Player); box.Inventory.Add("berries", 3); Hosted.PickUpCrate(box);
        string packed = Hosted.Backpack.Items.Keys.Single(World.IsPacked);
        // A crate selected on the hotbar only opens a clicked crate; the alert waits for a deliberate move.
        string lastSlot = world.HotbarItem(8); int active = world.ActiveHotbar;
        Give(Hosted, "crate", 1); SetHotbarSlot(Hosted, 8, "crate"); await KeyStroke(Key.Key9); await CloseWithEsc();
        await Click(view.Screen(crate.Pos, .4f));
        Require(!blackHole.Showing, "Clicking a crate with a crate selected on the hotbar must not show the nesting alert.");
        Require(Shows(crate), "Clicking a crate with a crate selected on the hotbar must select the clicked crate.");
        Require(crate.Inventory.Count == 0, "Clicking a crate with a crate selected on the hotbar must not put that crate into it.");
        Require(world.Backpack["crate"] == 1, "Clicking a crate with a crate selected on the hotbar must keep that crate in the backpack.");
        SetHotbarSlot(Hosted, 8, lastSlot); await KeyStroke(Key.Key1 + active); await CloseWithEsc();
        // Two empty crates go down one after the other without opening, and placement ends with the last.
        Give(Hosted, "crate", 1);
        await CloseWithEsc(); await KeyStroke(Key.Tab);
        await PickUp(Cell(0, c => c.TooltipText.StartsWith("Wood crate ·")));
        for (int placed = 1; placed <= 2; placed++) {
            var spot = SpotsNearby()
                .First(p => world.Construction.PlacementBlock(ThingKind.Crate, p, "crate") == null && Uncovered(view.Screen(p)));
            await Click(view.Screen(spot));
            await Until(() => world.Backpack["crate"] == 2 - placed && (world.Held?.Count ?? 0) == 2 - placed, "a click on open ground placing one held crate", 2);
            Require(panel.Page == Page.Backpack, "Placing a crate must not open it.");
            Require(player.Mode.Placing == (placed == 1 ? ThingKind.Crate : null),
                "Placing a held crate must stay in placement while crates remain held.");
        }
        RemoveThings(Hosted, t => t.Kind == ThingKind.Crate && t != crate && t.Inventory.Count == 0);
        await ClickOn(crate);
        await Carry(Cell(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        Require(blackHole.Showing, "A crate put into a crate must show the nesting alert.");
        Require(blackHole.Asking, "A crate put into a crate must show the nesting warning.");
        await Until(() => world.Backpack[packed] == 1, "the crate going back to the backpack", 2);
        Require(crate.Inventory.Count == 0, "A crate put into a crate must leave the target crate empty.");
        await Click((Control)blackHole.FindChild("anomaly_no", true, false));
        Require(!blackHole.Showing, "No must close the black hole question.");
        Require(world.Backpack[packed] == 1, "No must keep the packed crate in the backpack.");
        Require(world.Find(crate.Id) != null, "No must keep the placed crate on the ground.");
        await Carry(await Showing(1, c => c.TooltipText.StartsWith("Wood crate")), Cell(0, c => c.TooltipText == ""));
        await Click((Control)blackHole.FindChild("anomaly_yes", true, false));
        Require(ShowsChild(blackHole, "collapse") && !blackHole.Asking, "Yes must open the black hole.");
        await Until(() => blackHole.FindChild("game_over", true, false) is Control { Visible: true, Modulate.A: > .99f }, "the collapse ending on the game over screen", 10);
        await Click((Control)blackHole.FindChild("menu_after_collapse", true, false));
        Require(!blackHole.Showing, "Main menu must leave the black hole.");
        Require(startMenu.Visible, "Main menu must open the start menu.");
        Require(FindButton(startMenu.Screen, "New game") != null,
            "The start menu must offer New game after the black hole.");
    }
}
