using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Exercises survival interactions through the same UI and input path a player uses.</summary>
    private async Task CheckSurvivalActionsWorkThroughTheInventoryUI() {
        string savedWorld = world.Serialize();
        try {
            await CloseWithEsc();
            EmptyBackpack(world);
            SetHotbarSlot(world, 8, "water");
            await CheckHoldingRightMouseOnShoreRefillsRetainedBottle();
            await CheckHotbarDrinkTakesOneSipAndKeepsEmptyBottle();
            await CheckDragPlacesStackInExactSlotAndSortOrdersByName();
            await CheckClickMovesOneUnitAndCtrlClickMovesRemainingStack();
            await CheckPartialBottleIsRefusedByRecipeWithWarning();
            await CheckSkipToMorningOnlyForDownedPlayerWithoutCabin();
        } finally {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Pressed = false });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false });
            world.Deserialize(savedWorld);
            await CloseWithEsc();
            await Frames(2);
        }
    }
    /// <summary>A real source-ring pick and a held right mouse button refill the starter bottle.</summary>
    private async Task CheckHoldingRightMouseOnShoreRefillsRetainedBottle() {
        RemoveThings(world, t => t.Kind == ThingKind.Water);
        world.Add(ThingKind.Water, world.Player + Vector2.Right * 20, 9999);
        Drop(world, "water");
        Drop(world, "water_1");
        Drop(world, "water_2");
        Give(world, "empty_bottle", 1);
        var shore = Terrain.WaterPoint(world.Player);
        world.Player = shore + (shore - Terrain.Lake).Normalized() * 1.8f;
        await Frames(4);
        var shorePoint = view.Screen(shore, .8f);
        Require(view.Pick(shorePoint)?.Kind == ThingKind.Water, "The visible shoreline ring must pick the water source.");
        var windowPoint = GetViewport().GetFinalTransform() * shorePoint;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Until(() => world.Carried("water") == 1 && world.Carried("empty_bottle") == 0, "holding right mouse on the shoreline filling the retained bottle", world.Data.GatherSeconds + 3);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
        await Frames(3);

        await KeyStroke(Key.Tab); await Frames(3); await SurvivalReviewShot("filled-bottle"); await CloseWithEsc();
    }
    /// <summary>Drinking through hotbar slot nine consumes one sip at a time and keeps the empty bottle.</summary>
    private async Task CheckHotbarDrinkTakesOneSipAndKeepsEmptyBottle() {
        for (int expected = 3; expected >= 1; expected--) {
            await Until(() => hotbar.Counts[8].Text == $"{expected}/3", $"the bottle badge showing {expected}/3 before a sip", 2);
            await Click(hotbar.Buttons[8]);
            await ClickGround();
            await Until(() => hotbar.Counts[8].Text == $"{expected - 1}/3", "a hotbar drink removing exactly one sip", 2);
        }
        Require(world.Carried("empty_bottle") == 1, "Three hotbar sips must leave the empty bottle in inventory.");
    }
    /// <summary>Distinct stacks show the slot drag lands on the exact target and leaves other cells alone.</summary>
    private async Task CheckDragPlacesStackInExactSlotAndSortOrdersByName() {
        await CloseWithEsc();
        var bagItems = world.Backpack;
        bagItems.Clear();
        bagItems.Add("wood", 7); bagItems.Add("stone", 3); bagItems.Add("berries", 5);
        await KeyStroke(Key.Tab); await Frames(3);
        ItemSlot wood = Cell(0, c => c.TooltipText.StartsWith("Wood ·", StringComparison.Ordinal));
        var woodIndex = Array.IndexOf(bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().ToArray(), wood);
        ItemSlot target = bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().First(c => c.TooltipText == "");
        var targetIndex = Array.IndexOf(bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().ToArray(), target);
        Require(woodIndex >= 0 && targetIndex >= 0 && targetIndex != woodIndex, "The inventory must expose source and empty destination slots.");
        await DragTo(wood, target);
        Require(bagItems.Layout[targetIndex].Item == "wood", "Dragging a stack into an empty backpack slot must put the item in that slot.");
        Require(bagItems.Layout[targetIndex].Count == 7, "Dragging a stack into an empty backpack slot must keep its quantity.");
        Require(bagItems["stone"] == 3, "Dragging a stack into an empty backpack slot must leave the stone alone.");
        Require(bagItems["berries"] == 5, "Dragging a stack into an empty backpack slot must leave the berries alone.");
        await Click((Control)bag.FindChild("sort_bag", true, false));
        var ordered = bagItems.Layout.Where(s => s.Item != "").Select(s => s.Item).ToArray();
        Require(ordered.SequenceEqual(ordered.OrderBy(id => id, StringComparer.Ordinal)), "The backpack Sort button must order stacks by item name.");

        await SurvivalReviewShot("sorted-inventory");
    }
    /// <summary>A normal click moves one unit; Ctrl-click moves the entire remaining stack.</summary>
    private async Task CheckClickMovesOneUnitAndCtrlClickMovesRemainingStack() {
        await CloseWithEsc();
        Give(world, "coal", 5);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.Right);
        chest.Inventory.Add("coal", 5);
        await ClickOn(chest); await Frames(3);
        await Click(Cell(0, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal)));
        Require(chest.Inventory["coal"] == 4 && world.Carried("coal") == 6,
            "Clicking a storage stack must move exactly one item from storage to the backpack.");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = true });
        try { await Click(Cell(0, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal))); } finally { Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false }); }
        Require(chest.Inventory["coal"] == 0 && world.Carried("coal") == 10,
            "Ctrl-clicking a storage stack must take all remaining units.");
        await Click(Cell(1, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal)));
        Require(chest.Inventory["coal"] == 1 && world.Carried("coal") == 9,
            "Clicking one unit in the backpack must store exactly one unit.");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = true });
        try { await Click(Cell(1, c => c.TooltipText.StartsWith("Mined coal ·", StringComparison.Ordinal))); } finally { Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false }); }
        Require(chest.Inventory["coal"] == 10 && world.Carried("coal") == 0,
            "Ctrl-clicking a backpack stack must store all remaining units.");
    }
    /// <summary>A recipe water slot refuses a partial bottle, by click or by drag, and shows the full-bottle warning.</summary>
    private async Task CheckPartialBottleIsRefusedByRecipeWithWarning() {
        await CloseWithEsc();
        EmptyBackpack(world);
        Give(world, "water_2", 1);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough");
        await ClickOn(table); await Frames(3);
        await Click(Cell(1, c => c.TooltipText.StartsWith(world.Data.Name("water_2") + " ·", StringComparison.Ordinal)));
        Require(waterWarning.Text == Water.FullBottleWarning, "Trying a partial bottle must show the full-bottle warning text.");
        Require(waterWarning.GetGlobalRect().Position.X < 50, "The full-bottle warning must sit at the left of the startMenu.Screen.");
        Require(waterWarning.Size.X > 300, "The full-bottle warning must be wide enough to read.");
        Require(waterWarning.Size.Y < 50, "The full-bottle warning must be a single short line.");
        Require(waterWarning.GetGlobalRect().End.Y < bag.GetGlobalRect().Position.Y, "The full-bottle warning must sit above the backpack.");
        var partial = Cell(1, c => c.TooltipText.StartsWith(world.Data.Name("water_2") + " ·", StringComparison.Ordinal));
        await DragTo(partial, (Control)ui.FindChild("input_water", true, false));
        Require(table.Inventory["water_2"] == 0 && world.Carried("water_2") == 1 && waterWarning.Text == Water.FullBottleWarning,
            "Dragging a partial bottle onto a recipe water slot must warn without using it.");
        await SurvivalReviewShot("water-warning");
    }
    /// <summary>A downed player without a cabin may choose dawn; any cabin restores normal respawning.</summary>
    private async Task CheckSkipToMorningOnlyForDownedPlayerWithoutCabin() {
        await CloseWithEsc();
        RemoveThings(world, t => t.Kind == ThingKind.Hut);
        RunUntilPhase(world, .9f);
        world.Die();
        await Until(() => recovery.Visible, "a downed player's Skip to morning prompt", 2);
        Require(recovery.Visible && recovery.CanSkip, "A downed player without a cabin must see an enabled Skip to morning prompt.");
        await SurvivalReviewShot("recovery-prompt");
        float previousDay = world.Day;
        await Click((Control)recovery.FindChild("skip_morning", true, false));
        Require(world.RespawnIn == 0 && world.Day > previousDay && world.Time % world.Data.DaySeconds < world.Data.DaySeconds * .1f,
            "Skipping morning must advance the simulated world to dawn and complete respawn.");
        world.Add(ThingKind.Hut, world.Player + Vector2.Right);
        world.Die();
        await Frames(2);
        Require(!recovery.Visible, "A downed player with a cabin must not be offered Skip to morning.");
    }
}
