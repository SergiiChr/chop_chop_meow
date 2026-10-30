using System;
using System.Linq;
using static Checks;

namespace ChopChopMeow;

/// <summary>Stable inventory positions, explicit slot moves and player sorting.</summary>
public static class InventoryLayoutChecks {
    public static void Run(GameData data) {
        Inventory.Unstacked.Add("water"); Inventory.Unstacked.Add("water_2");
        Inventory.Unstacked.Add("water_1"); Inventory.Unstacked.Add("empty_bottle");
        Inventory.Unstacked.Add("steel_axe");
        var bag = new Inventory { Slots = 6, StackSize = 4 };
        bag.Add("wood", 7); bag.Add("stone", 2);
        var stacks = new System.Collections.Generic.List<(string Item, int Count)>();
        bag.FillStacks(stacks);
        Require(stacks.Take(3).SequenceEqual(new[] { ("wood", 4), ("wood", 3), ("stone", 2) }), "new stacks should occupy successive free cells");
        Require(bag.MoveSlotTo(0, bag, 3) == 4, "a stack should move to a selected empty cell");
        bag.FillStacks(stacks);
        Require(stacks[0] == ("", 0), "moving a stack should leave its source empty");
        Require(stacks[1] == ("wood", 3), "moving a stack should leave the untouched stack alone");
        Require(stacks[3] == ("wood", 4), "moving a stack should land in its exact target cell");
        Require(bag.MoveSlotTo(3, bag, 1, 1) == 1, "a split should merge into the selected matching stack");
        bag.FillStacks(stacks);
        Require(stacks[1] == ("wood", 4) && stacks[3] == ("wood", 3), "a split should leave the remainder in its source cell");
        Require(bag.Items["wood"] == 7, "aggregate item access should track slot changes");
        bag.Sort(); bag.FillStacks(stacks);
        Require(stacks.Where(s => s.Item == "wood").Select(s => s.Count).SequenceEqual(new[] { 4, 3 }), "sort should pack compatible stacks and keep the item together");

        var box = new Inventory { Slots = 3, StackSize = 4 };
        Require(bag.MoveSlotTo(0, box, 1) == 2, "a stack should move between inventories to the selected cell");
        Require(box["stone"] + box["wood"] > 0, "destination aggregate counts should update");
        Require(bag.Count + box.Count == 9, "moving between inventories should preserve total units");

        var fragmented = new Inventory { Slots = 2, StackSize = 10 };
        fragmented.Add("wood", 2);
        Require(fragmented.MoveSlotTo(0, fragmented, 1, 1) == 1, "a partial stack should split into an explicit free cell");
        Require(fragmented.UsedSlots == 2 && fragmented.Room("wood") == 18, "room and used slots should account for both partial physical stacks");

        var blocked = new Inventory { Slots = 1, StackSize = 4 };
        blocked.Add("stone", 1);
        Require(bag.MoveSlotTo(1, blocked, 0) == 0, "a cross-inventory move must not swap an unrelated full destination stack");
        Require(blocked["stone"] == 1 && bag["wood"] == 7, "a refused cross-inventory move must preserve both inventories");

        var limited = new Inventory { Slots = 2, StackSize = 4, Capacity = 1 };
        Require(bag.MoveSlotTo(1, limited, 0) == 1, "a move into a capacity-limited inventory should report only the amount that fits");
        Require(limited.Count == 1 && bag.Count + box.Count + limited.Count == 9, "a partial cross-inventory move should conserve all units");

        var source = new Inventory { Slots = 2, StackSize = 4 };
        var destination = new Inventory { Slots = 1, StackSize = 4 };
        source.Add("wood", 4); destination.Add("wood", 3);
        Require(source.MoveSlotTo(0, destination, 0) == 1, "a cross-inventory merge should move only the target stack's available room");
        Require(source["wood"] == 3 && destination["wood"] == 4, "a partial cross-inventory merge should keep both aggregate counts accurate");

        var replacement = new Inventory { Slots = 3, StackSize = 4 };
        replacement.Add("berries", 3); replacement.Add("stone", 2);
        Require(replacement.Replace("berries", "planks"), "replacement should find the first old-item stack");
        replacement.FillStacks(stacks);
        Require(stacks[0] == ("planks", 3), "replacement should preserve the original cell and quantity");
        Require(replacement["berries"] == 0, "replacement should leave no berries in the aggregate count");
        Require(replacement["planks"] == 3, "replacement should count every plank in the aggregate count");
        replacement.Remove("stone", 2); replacement.Add("wood", 2);
        replacement.FillStacks(stacks);
        Require(stacks[1] == ("wood", 2), "removing then adding should reuse the vacated physical cell");

        string saved = System.Text.Json.JsonSerializer.Serialize(replacement);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<Inventory>(saved)!;
        loaded.FillStacks(stacks);
        Require(stacks[0] == ("planks", 3) && stacks[1] == ("wood", 2), "serialized layout should retain positions across load");
        Require(loaded.PreferredSlot == -1, "preferred UI cell should not persist in a save");

        var bottles = new Inventory { Slots = 4, StackSize = 64 };
        bottles.Add("water", 2);
        Require(bottles.UsedSlots == 2, "unstacked bottle states should use two slots");
        Require(bottles.Layout[0].Count == 1, "the first bottle should sit alone in its cell");
        Require(bottles.Layout[1].Count == 1, "the second bottle should sit alone in its cell");
        bottles.PreferredSlot = 1;
        Require(bottles.Remove("water", 1) == 1, "removal should take one bottle");
        Require(bottles.Layout[0].Item == "water", "removal should leave the first bottle in its cell");
        Require(bottles.Layout[1].Item == "", "removal should empty the clicked duplicate's cell");
        bottles.Add("water_1", 2); bottles.PreferredSlot = 2;
        Require(bottles.Replace("water_1", "empty_bottle"), "replacement should prefer the clicked matching duplicate stack");
        Require(bottles.Layout[1].Item == "water_1" && bottles.Layout[2].Item == "empty_bottle", "replacement should preserve the preferred bottle's cell and count");

        var overflow = new Inventory { Slots = 3, StackSize = 2 };
        overflow.Add("wood", 3); overflow.Add("stone", 2);
        overflow.Slots = 1;
        Require(overflow["wood"] == 3, "shrinking slot limits must preserve the wood");
        Require(overflow["stone"] == 2, "shrinking slot limits must preserve the stone");
        Require(overflow.UsedSlots == 3, "shrinking slot limits must keep counting the overflowing stacks");
        Require(overflow.AggregateSlotsNeeded == 3 && overflow.Room("wood") == 0, "overflow stack requirements should be exposed without accepting more items");
    }
}
