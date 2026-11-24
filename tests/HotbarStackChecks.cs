using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks for hotbar slots bound to the exact backpack stack put on them.</summary>
public static class HotbarStackChecks {
    public static void Run(GameData data) {
        Test(CheckAStackPutOnTheHotbarBindsThatExactStack, data);
        Test(CheckCtrlAndStoreAllLeaveTheBoundStackBehind, data);
        Test(CheckABoundStackMovedOutEmptiesItsSlot, data);
        Test(CheckAMergedStackTakesTheMovedStacksSlot, data);
        Test(CheckAStackHeldFromAChestMovesIntoTheBackpackOnTheHotbar, data);
        Test(CheckAWaitingSlotBindsTheFirstStackToArrive, data);
        Test(CheckABoundStackPutOnAnotherSlotMovesThere, data);
        Test(CheckABottleStaysBoundThroughSips, data);
        Test(CheckBindingsComeBackFromASave, data);
        Test(CheckAStackUsedUpPassesItsSlotToTheNextStack, data);
    }

    private static void CheckAStackUsedUpPassesItsSlotToTheNextStack(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "berries", 5);
        int next = SlotOf(world.Backpack, "berries"), last = SplitStack(world, next, 1);
        PutOnHotbar(world, last, 7);
        SetNeeds(world, hunger: 20);
        EatFrom(world, last, "berries");
        world.Tick(.1f);
        Require(world.HotbarStack(7) == next && world.HotbarCount(7) == 4, "eating the bound stack's last berry should bind the slot to the other berries at once");
        for (int i = 0; i < 4; i++) EatFrom(world, next, "berries");
        world.Tick(.1f);
        Require(world.HotbarItem(7) == "berries" && world.HotbarCount(7) == 0, "with no berries left, the slot should wait for them greyed");
        Give(world, "berries", 2); world.Tick(.1f);
        Require(world.HotbarCount(7) == 2, "berries coming back should bind to the waiting slot");
    }

    private static void CheckAStackPutOnTheHotbarBindsThatExactStack(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 10);
        int first = SlotOf(world.Backpack, "wood"), second = SplitStack(world, first, 4);
        PutOnHotbar(world, second, 0);
        Require(world.HotbarStack(0) == second && world.HotbarItem(0) == "wood" && world.HotbarCount(0) == 4,
            "the hotbar slot should be bound to the 4 wood put on it, not the other 6");
    }

    private static void CheckCtrlAndStoreAllLeaveTheBoundStackBehind(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        Give(world, "steel_pick", 3);
        PutOnHotbar(world, SlotOf(world.Backpack, "steel_pick"), 0);
        world.FromSpares(() => world.Transfer(world.Backpack, chest.Inventory, "steel_pick", 3));
        Require(chest.Inventory["steel_pick"] == 2 && world.HotbarCount(0) == 1, "a Ctrl batch of 3 pickaxes should leave the one bound to the hotbar");
        Give(world, "wood", 5);
        PutOnHotbar(world, SlotOf(world.Backpack, "wood"), 1);
        world.Transfer(world.Backpack, chest.Inventory, "steel_pick", 1);
        Require(chest.Inventory["steel_pick"] == 3 && world.HotbarItem(0) == "", "an unmarked move, as Shift or the hotbar's Ctrl sends, should take the bound pickaxe and empty its slot");
    }

    private static void CheckABoundStackMovedOutEmptiesItsSlot(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        Give(world, "berries", 6);
        int cell = SlotOf(world.Backpack, "berries");
        PutOnHotbar(world, cell, 2);
        world.TransferSlot(world.Backpack, chest.Inventory, cell, 0, "berries", 6);
        Give(world, "berries", 3);
        Require(world.HotbarItem(2) == "", "a hotbar slot whose stack left the backpack should be empty, even with berries back in the backpack");
    }

    private static void CheckAMergedStackTakesTheMovedStacksSlot(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "berries", 10);
        int moved = SlotOf(world.Backpack, "berries"), onto = SplitStack(world, moved, 4);
        PutOnHotbar(world, moved, 0);
        PutOnHotbar(world, onto, 1);
        world.TransferSlot(world.Backpack, world.Backpack, moved, onto, "berries", 6);
        Require(world.HotbarStack(0) == onto && world.HotbarCount(0) == 10 && world.HotbarItem(1) == "",
            "a bound stack merged into another should take its own hotbar slot along, and the other's slot should empty");
    }

    private static void CheckAStackHeldFromAChestMovesIntoTheBackpackOnTheHotbar(GameData data) {
        var world = EmptyWorld(data);
        var chest = world.Add(ThingKind.Chest, world.Player + Vector2.One);
        Give(world, "stone", 5);
        world.Transfer(world.Backpack, chest.Inventory, "stone", 5);
        PutOnHotbar(world, chest.Inventory, 0, 4);
        Require(chest.Inventory["stone"] == 0 && world.HotbarItem(4) == "stone" && world.HotbarCount(4) == 5,
            "stone held from a chest put on the hotbar should move into the backpack, bound there");
    }

    private static void CheckAWaitingSlotBindsTheFirstStackToArrive(GameData data) {
        var world = EmptyWorld(data);
        SetHotbarSlot(world, 5, "stone");
        Require(world.HotbarItem(5) == "stone" && world.HotbarCount(5) == 0, "a slot naming stone the player lacks should wait for it");
        Give(world, "stone", 2);
        world.Tick(.1f);
        Require(world.HotbarStack(5) == SlotOf(world.Backpack, "stone"), "the stone that comes into the backpack should bind to the waiting slot");
    }

    private static void CheckABoundStackPutOnAnotherSlotMovesThere(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 3);
        int cell = SlotOf(world.Backpack, "wood");
        PutOnHotbar(world, cell, 0);
        PutOnHotbar(world, cell, 1);
        Require(world.HotbarStack(1) == cell && world.HotbarItem(0) == "", "a stack put on a second hotbar slot should move there and leave the first empty");
    }

    private static void CheckABottleStaysBoundThroughSips(GameData data) {
        var world = EmptyWorld(data);
        Give(world, Water.Full, 1);
        PutOnHotbar(world, SlotOf(world.Backpack, Water.Full), 8);
        SetNeeds(world, thirst: 20);
        for (int liter = 0; liter < 3; liter++) world.Consume(world.HotbarItem(8));
        Require(world.HotbarItem(8) == Water.Empty, "a bound bottle should stay bound through every liter drunk, empty at the end");
    }

    private static void CheckBindingsComeBackFromASave(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "wood", 10);
        int cell = SplitStack(world, SlotOf(world.Backpack, "wood"), 3);
        PutOnHotbar(world, cell, 6);
        var loaded = new World(data, false);
        loaded.Deserialize(world.Serialize());
        Require(loaded.HotbarStack(6) == cell && loaded.HotbarCount(6) == 3, "a save should keep which exact stack a hotbar slot is bound to");
    }
}
