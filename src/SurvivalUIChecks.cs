using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The save slot that keeps the game while the survival checks do what cannot be undone.</summary>
    private const int SurvivalSlot = 3;

    /// <summary>Exercises survival interactions through the same UI and input path a player uses.</summary>
    private async Task CheckSurvivalActionsWorkThroughTheInventoryUI() {
        Saves.Write(Hosted, SurvivalSlot);
        try {
            await CloseWithEsc();
            EmptyBackpack(Hosted);
            SetHotbarSlot(Hosted, 8, "water");
            await CheckHoldingRightMouseOnShoreRefillsRetainedBottle();
            await CheckHotbarDrinkTakesOneLiterAndKeepsEmptyBottle();
            await CheckHeldStackLandsInExactSlotAndSortOrdersByName();
            await CheckRightClickPutsOneUnitAndCtrlClickMovesRemainingStack();
            await CheckPartDrunkBottlePoursItsLitersIntoTheWaterInputSlot();
            await CheckWaterInputSlotShakesWithoutRoomInABottleAndFillsOne();
            await CheckSkipToMorningOnlyForDownedPlayerWithoutCabin();
        } finally {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = 0, Pressed = false });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false });
            LoadSlot(SurvivalSlot);
            await Frames(2);
            await CloseWithEsc();
            await Frames(2);
        }
    }
    /// <summary>Holding the right mouse button at the shoreline refills the starter bottle.</summary>
    private async Task CheckHoldingRightMouseOnShoreRefillsRetainedBottle() {
        RemoveThings(Hosted, t => t.Kind == ThingKind.Water);
        Hosted.Add(ThingKind.Water, world.Player + Vector2.Right * 20, 9999);
        Drop(Hosted, "water");
        Drop(Hosted, "water_1");
        Drop(Hosted, "water_2");
        Give(Hosted, "empty_bottle", 1);
        var shore = Terrain.WaterPoint(world.Player);
        Warp(Hosted, shore + (shore - Terrain.Lake).Normalized() * 1.8f);
        await Frames(4);
        var shorePoint = view.Screen(shore, .8f);
        await Hover(shorePoint);
        Require(world.Find(view.Hover)?.Kind == ThingKind.Water, "Pointing at the shoreline should show the water target.");
        await SurvivalReviewShot("water-source");
        var windowPoint = GetViewport().GetFinalTransform() * shorePoint;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, ButtonMask = MouseButtonMask.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = true });
        await Until(() => world.Backpack["water"] == 1 && world.Backpack["empty_bottle"] == 0, "holding right mouse on the shoreline filling the retained bottle", world.Data.GatherSeconds + 3);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = windowPoint, GlobalPosition = windowPoint, Pressed = false });
        await Frames(3);

        await KeyStroke(Key.Tab); await Frames(3); await SurvivalReviewShot("filled-bottle"); await CloseWithEsc();
    }
    /// <summary>Drinking through hotbar slot nine consumes one liter at a time and keeps the empty bottle.</summary>
    private async Task CheckHotbarDrinkTakesOneLiterAndKeepsEmptyBottle() {
        for (int expected = 3; expected >= 1; expected--) {
            await Until(() => hotbar.Counts[8].Text == $"{expected}/3", $"the bottle badge showing {expected}/3 before a liter", 2);
            await KeyStroke(Key.Key9);
            await ClickGround();
            await Until(() => hotbar.Counts[8].Text == $"{expected - 1}/3", "a hotbar drink removing exactly one liter", 2);
        }
        Require(world.Backpack["empty_bottle"] == 1, "Three hotbar drinks must leave the empty bottle in inventory.");
    }
    /// <summary>Distinct stacks show a held stack lands on the exact cell it is put on and leaves other cells alone.</summary>
    private async Task CheckHeldStackLandsInExactSlotAndSortOrdersByName() {
        await CloseWithEsc();
        EmptyBackpack(Hosted);
        Give(Hosted, "wood", 7); Give(Hosted, "stone", 3); Give(Hosted, "berries", 5);
        await KeyStroke(Key.Tab); await Frames(3);
        ItemSlot wood = Cell(0, c => c.TooltipText.StartsWith("Wood ·", StringComparison.Ordinal));
        var woodIndex = Array.IndexOf(bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().ToArray(), wood);
        ItemSlot target = bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().First(c => c.TooltipText == "");
        var targetIndex = Array.IndexOf(bag.FindChildren("*", "Button", true, false).OfType<ItemSlot>().ToArray(), target);
        Require(woodIndex >= 0, "The inventory must expose a wood slot.");
        Require(targetIndex >= 0, "The inventory must expose an empty destination slot.");
        Require(targetIndex != woodIndex, "The source and destination slots must differ.");
        await Carry(wood, target);
        await Until(() => world.Backpack.Layout[targetIndex].Item == "wood", "a held stack put on an empty backpack slot landing in that slot", 2);
        Require(world.Backpack.Layout[targetIndex].Count == 7, "A held stack put on an empty backpack slot must keep its quantity.");
        Require(world.Backpack["stone"] == 3, "A held stack put on an empty backpack slot must leave the stone alone.");
        Require(world.Backpack["berries"] == 5, "A held stack put on an empty backpack slot must leave the berries alone.");
        await Click((Control)bag.FindChild("sort_bag", true, false));
        var ordered = world.Backpack.Layout.Where(s => s.Item != "").Select(s => s.Item).ToArray();
        Require(ordered.SequenceEqual(ordered.OrderBy(id => id, StringComparer.Ordinal)), "The backpack Sort button must order stacks by item name.");

        await SurvivalReviewShot("sorted-inventory");
    }
    /// <summary>A right click with a stack held puts down one unit, Shift-click moves the stack clicked and Ctrl-click every unit of the item.</summary>
    private async Task CheckRightClickPutsOneUnitAndCtrlClickMovesRemainingStack() {
        await CloseWithEsc();
        Give(Hosted, "coal", 5);
        var chest = Hosted.Add(ThingKind.Chest, world.Player + Vector2.Right);
        chest.Inventory.Add("coal", 5);
        await ClickOn(chest); await Frames(3);
        await PickUp(Cell(0, c => c.TooltipText.StartsWith("Coal ·", StringComparison.Ordinal)));
        await RightClick(Cell(1, c => c.TooltipText.StartsWith("Coal ·", StringComparison.Ordinal)));
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty && chest.Inventory["coal"] == 4 && world.Backpack["coal"] == 6,
            "a right click with a storage stack held putting exactly one item into the backpack", 2);
        await Holding(Key.Ctrl, () => Click(Cell(0, c => c.TooltipText.StartsWith("Coal ·", StringComparison.Ordinal))));
        Require(chest.Inventory["coal"] == 0 && world.Backpack["coal"] == 10,
            "Ctrl-clicking a storage stack must take all remaining units.");
        await PickUp(Cell(1, c => c.TooltipText.StartsWith("Coal ·", StringComparison.Ordinal)));
        await RightClick(Cell(0, c => c.TooltipText == ""));
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty && chest.Inventory["coal"] == 1 && world.Backpack["coal"] == 9,
            "a right click with a backpack stack held storing exactly one unit", 2);
        await Holding(Key.Ctrl, () => Click(Cell(1, c => c.TooltipText.StartsWith("Coal ·", StringComparison.Ordinal))));
        Require(chest.Inventory["coal"] == 10 && world.Backpack["coal"] == 0,
            "Ctrl-clicking a backpack stack must store all remaining units.");
        Give(Hosted, "coal", 70); await Frames(3);
        await Holding(Key.Shift, () => Click(Cell(1, c => c.TooltipText.StartsWith("Coal ·", StringComparison.Ordinal) && ((Label)c.GetChild(0)).Text == "6")));
        Require(chest.Inventory["coal"] == 16 && world.Backpack["coal"] == 64, "Shift-clicking a backpack stack must store just that stack.");
        await Holding(Key.Shift, () => Click(Cell(0, c => c.TooltipText.StartsWith("Coal ·", StringComparison.Ordinal))));
        Require(chest.Inventory["coal"] == 0 && world.Backpack["coal"] == 80, "Shift-clicking a storage stack must take that stack.");
    }
    /// <summary>A part-drunk bottle put on a station by Shift or held onto its water input slot pours the liters it holds and stays in the backpack, emptied.</summary>
    private async Task CheckPartDrunkBottlePoursItsLitersIntoTheWaterInputSlot() {
        await CloseWithEsc();
        EmptyBackpack(Hosted);
        Give(Hosted, Water.TwoLiters, 1);
        var table = Hosted.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        Hosted.SetRecipe(table, "dough");
        await ClickOn(table); await Frames(3);
        await Holding(Key.Shift, () => Click(BagCellOf(Water.TwoLiters)));
        await Until(() => table.Inventory[Water.Liquid] == 2, "Shift on a two-liter bottle pouring two liters of water", 2);
        Require(world.Backpack[Water.Empty] == 1 && world.Backpack[Water.TwoLiters] == 0, "The poured bottle must stay in the backpack, empty.");
        Give(Hosted, Water.OneLiter, 1); await Frames(2);
        await Carry(BagCellOf(Water.OneLiter), (Control)ui.FindChild($"input_{Water.Liquid}", true, false));
        await Until(() => table.Inventory[Water.Liquid] == 3, "a one-liter bottle put on the water input slot pouring its liter", 2);
        Require(world.Backpack[Water.Empty] == 2 && table.Inventory[Water.OneLiter] + table.Inventory[Water.Empty] == 0, "A bottle put on the input slot must pour and stay in the backpack, empty.");
        await SurvivalReviewShot("water-pour");
    }
    /// <summary>Shift on a station's water with no bottle with room in the backpack is refused, says why and shakes the input slot; with one it takes a liter into it.</summary>
    private async Task CheckWaterInputSlotShakesWithoutRoomInABottleAndFillsOne() {
        await CloseWithEsc();
        EmptyBackpack(Hosted); RemoveThings(Hosted, t => t.Kind == ThingKind.Cooktable);
        var table = Hosted.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        Hosted.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 1);
        await ClickOn(table); await Frames(3);
        var slot = (ItemSlot)ui.FindChild($"input_{Water.Liquid}", true, false);
        await Holding(Key.Shift, () => Click(slot)); await Frames(3);
        Require(slot.Shaking, "A refused Shift on the water input slot must shake it.");
        Require(messages.Shown.Contains("You need an empty water vessel."), "A refused Shift on the water input slot must say it needs an empty vessel.");
        Require(table.Inventory[Water.Liquid] == 1 && world.Backpack.Count == 0, "Refused water must stay on the table.");
        await Until(() => !slot.Shaking, "the water input slot coming to rest", 2);
        Give(Hosted, Water.Empty, 1); await Frames(2);
        await Holding(Key.Shift, () => Click(slot));
        await Until(() => world.Backpack[Water.OneLiter] == 1, "Shift on the water input slot taking one liter into the empty bottle", 2);
        Require(table.Inventory[Water.Liquid] == 0 && world.Backpack[Water.Empty] == 0, "The water must leave the table in the bottle.");
        RemoveThings(Hosted, t => t == table); EmptyBackpack(Hosted); await CloseWithEsc();
    }
    /// <summary>A downed player without a cabin may choose dawn; any cabin restores normal respawning.</summary>
    private async Task CheckSkipToMorningOnlyForDownedPlayerWithoutCabin() {
        await CloseWithEsc();
        RemoveThings(Hosted, t => t.Kind == ThingKind.Hut);
        RunUntilPhase(Hosted, .9f);
        Hosted.Die();
        await Until(() => recovery.Visible, "a downed player's Skip to morning prompt", 2);
        Require(recovery.Visible && recovery.FindChild("skip_morning", true, false) is Button { Disabled: false }, "A downed player without a cabin must see an enabled Skip to morning prompt.");
        await SurvivalReviewShot("recovery-prompt");
        float previousDay = world.Day;
        await Click((Control)recovery.FindChild("skip_morning", true, false));
        Require(world.RespawnIn == 0, "Skipping morning must complete respawn.");
        Require(world.Day > previousDay, "Skipping morning must advance the world to the next day.");
        Require(world.Time % world.Data.DaySeconds < world.Data.DaySeconds * .1f,
            "Skipping morning must stop at dawn.");
        Hosted.Die();
        await Until(() => recovery.Visible, "a player downed again still without a cabin being offered Skip to morning", 2);
        Hosted.Add(ThingKind.Hut, world.Player + Vector2.Right);
        await Until(() => !recovery.Visible, "a new cabin taking Skip to morning away from a downed player", 2);
        Require(world.RespawnIn > 0, "Skip to morning must go because of the cabin, with the player still down.");
    }
}
