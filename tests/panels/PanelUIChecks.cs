using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunPanelUIChecks() {
        await TestClean(CheckPanelPagesRefreshWithoutAllocating);
        await TestClean(CheckMouseWheelOverPanelDoesNotZoom);
        await TestClean(CheckOpeningAPageLeavesTheViewStill);
        await TestClean(CheckBuildingClicksLeaveTheBackpackAsItWas);
        await TestClean(CheckToolsWindowUnrollsFromItsRoll);
    }

    /// <summary>A click on a building or a worker, or the crafting menu, leaves the backpack open or closed as it was, storage opens it, and Tab shows or hides it under the open page.</summary>
    private async Task CheckBuildingClicksLeaveTheBackpackAsItWas() {
        await CloseWithEsc();
        var bench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1.5f, 0));
        var chest = Hosted.Add(ThingKind.Chest, world.Player + new Vector2(1.5f, 0));
        await ClickOn(bench);
        Require(Shows(bench) && !bag.Visible, "A station's page must open without the backpack.");
        await KeyStroke(Key.C);
        Require(panel.Page == Page.Craft && !bag.Visible, "The crafting menu must open without the backpack.");
        var worker = AddWorker(Hosted, world.Player + new Vector2(-2, 2));
        await Frames(); await Click(view.Screen(worker.Pos, 1));
        Require(view.Selection.Worker == worker.Id && !bag.Visible, "A worker's page must open without the backpack.");
        Hosted.Fire(worker);
        await ClickOn(bench);
        await KeyStroke(Key.Tab);
        Require(Shows(bench) && bag.Visible, "Tab must show the backpack under a station's page and keep the page open.");
        await KeyStroke(Key.Tab);
        Require(Shows(bench) && !bag.Visible, "Tab must hide the backpack again and keep the station's page open.");
        await ClickOn(chest);
        Require(Shows(chest) && bag.Visible, "A chest's page must open the backpack.");
        await ClickOn(bench);
        Require(Shows(bench) && bag.Visible, "A station opened while the backpack is open must keep it open.");
        RemoveThings(Hosted, t => t == bench || t == chest); await CloseWithEsc();
    }

    /// <summary>A building's Tools window opens rolled up below the page's title, a click on the roll unrolls it and another rolls it back, and each building opens it rolled up.</summary>
    private async Task CheckToolsWindowUnrollsFromItsRoll() {
        await CloseWithEsc();
        var first = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-1.5f, 0));
        var second = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(1.5f, 0));
        await ClickOn(first);
        var window = panel.ToolsWindow;
        await Until(() => window.Visible && window.Size.X <= ToolsRolledWidth, "a station's Tools window opening rolled up", 2);
        Require(window.GetGlobalRect().Position.Y >= panel.HeadingTitle.GetGlobalRect().End.Y, "The Tools window must start below the page's title.");
        await UnrollTools();
        var roll = ToolsRoll;
        Require(window.Size.X > ToolsRolledWidth && roll.GetGlobalRect().Position.X - window.GetGlobalRect().Position.X < 10, "The Tools roll must stay on the unrolled window's left edge.");
        Require(window.GetGlobalRect().End.X <= panel.GetGlobalRect().Position.X, "The unrolled Tools window must stand left of the page.");
        await RollUpTools();
        await UnrollTools();
        await ClickOn(second);
        await Until(() => Shows(second) && window.Visible, "the second station's page opening", 2);
        Require(!window.Unrolled && window.Size.X <= ToolsRolledWidth, "The next station must open its Tools window rolled up.");
        RemoveThings(Hosted, t => t == first || t == second); await CloseWithEsc();
    }

    /// <summary>
    /// Every panel page, refreshed again and again while what it shows stays the same, allocates nothing:
    /// the panel refreshes several times a second while it is open.
    /// </summary>
    private async Task CheckPanelPagesRefreshWithoutAllocating() {
        var at = world.Player;
        var chest = Hosted.Add(ThingKind.Chest, at + new Vector2(2, 0)); chest.Inventory.Add("wood", 40); chest.Inventory.Add("coal", 12);
        var pile = Hosted.Add(ThingKind.Pile, at + new Vector2(-4, 2)); pile.Inventory.Add("stone", 9);
        var forge = Hosted.Add(ThingKind.Reinforced, at + new Vector2(-2, 0)); forge.Inventory.Add("iron_bar", 4); forge.Inventory.Add("coal", 6); forge.Heat = 85; forge.Processing = true; forge.Progress = 2;
        var furnace = Hosted.Add(ThingKind.Furnace, at + new Vector2(0, 2)); furnace.Inventory.Add("iron_ore", 4);
        var fire = Hosted.Add(ThingKind.Campfire, at + new Vector2(0, -2)); fire.Burn = 120;
        var hut = Hosted.Add(ThingKind.Hut, at + new Vector2(4, -3));
        var sign = Hosted.Add(ThingKind.Signpost, at + new Vector2(-4, -3));
        // A tree inside the plot crushes it when the tree's next day of growth comes due.
        // So the plot goes where no tree stands.
        var farmAt = new[] { new Vector2(5, 3), new Vector2(7, 3), new Vector2(5, 6), new Vector2(7, 6), new Vector2(3, 6) }
            .Select(offset => at + offset)
            .First(spot => !world.Things.Any(t => t.Kind == ThingKind.Tree
                && (t.Pos - spot).Abs() is { X: < Farms.HalfSize + 1, Y: < Farms.HalfSize + 1 }));
        var farm = Hosted.Add(ThingKind.Farm, farmAt);
        var packer = Hosted.Add(ThingKind.Packer, at + new Vector2(-5, 4)); Hosted.SetPacking(packer, "berries"); packer.Inventory.Add("berries", 6); packer.Inventory.Add("crate", 2);
        var tree = world.Things.Where(t => t.Kind == ThingKind.Tree).MinBy(t => t.Pos.DistanceTo(at))!;
        var worker = AddWorker(Hosted, at + Vector2.One); worker.Name = "Panel Check"; worker.Wage = 5;
        Hosted.Assign(worker, forge); worker.Bag.Add("coal", 2);
        var pages = new (string Name, Action Open)[] {
            ("inventory", () => panel.Open(Page.Backpack)), ("craft", () => panel.Open(Page.Craft)), ("workers", () => panel.Open(Page.Crew)), ("diary", () => panel.Open(Page.Diary)),
            ("chest", () => panel.Select(Shown(chest))), ("pile", () => panel.Select(Shown(pile))), ("bellows furnace", () => panel.Select(Shown(forge))), ("furnace", () => panel.Select(Shown(furnace))),
            ("campfire", () => panel.Select(Shown(fire))), ("cabin", () => panel.Select(Shown(hut))), ("signpost", () => panel.Select(Shown(sign))), ("farm", () => panel.Select(Shown(farm))), ("packing station", () => panel.Select(Shown(packer))),
            ("tree", () => panel.Select(tree)), ("worker", () => panel.Select(world.Workers.First(w => w.Id == worker.Id))),
        };
        await MeasurePanelRefreshAllocations(pages);
        Hosted.Fire(worker);
        var placed = new[] { chest, pile, forge, furnace, fire, hut, sign, farm, packer };
        RemoveThings(Hosted, t => placed.Contains(t) || t.Kind == ThingKind.Grave);
        await Frames();
    }

    private async Task CheckMouseWheelOverPanelDoesNotZoom() {
        float zoom = view.Zoom;
        await CloseWithEsc();
        foreach (var (page, key) in new[] { (Page.Backpack, Key.Tab), (Page.Build, Key.B), (Page.Craft, Key.C) }) {
            await KeyStroke(key);
            // The backpack shows only its drawer, so the wheel goes over that.
            var box = layer.Transform * (page == Page.Backpack ? bag.GetGlobalRect() : panel.GetGlobalRect());
            foreach (var spot in new[] { box.GetCenter(), box.Position + new Vector2(30, 30), box.End - new Vector2(30, 30) }) await Wheel(spot);
            Require(view.Zoom == zoom, $"The mouse wheel over the {page} panel must not zoom the world.");
        }
        await CloseWithEsc();
        await Wheel(GetViewport().GetVisibleRect().GetCenter());
        Require(view.Zoom < zoom, "The mouse wheel over the world must still zoom.");
        await Wheel(GetViewport().GetVisibleRect().GetCenter(), MouseButton.WheelUp);
    }

    /// <summary>Opening a building's page leaves the world where it shows on screen, so the second click of a double click lands on the same building.</summary>
    private async Task CheckOpeningAPageLeavesTheViewStill() {
        var chest = Hosted.Add(ThingKind.Chest, ClearSpotNearby());
        var shut = view.Screen(chest.Pos);
        await ClickOn(chest);
        await Until(() => Shows(chest), "the chest page after a click on the chest", 2);
        Require(view.Screen(chest.Pos).DistanceTo(shut) < 1, "Opening a page must leave the world where it shows on screen.");
        RemoveThings(Hosted, t => t == chest);
        await CloseWithEsc();
    }
}
