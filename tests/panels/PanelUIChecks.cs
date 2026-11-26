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
}
