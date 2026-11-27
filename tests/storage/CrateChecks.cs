using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;

/// <summary>Checks of wood crates: their slots, carrying one with goods inside, and the filters a packed crate keeps.</summary>
public static class CrateChecks {
    public static void Run(GameData data) {
        Test(CheckPlayerMovesItemsInAndOutOfACarriedCrate, data);
        Test(CheckCratesShowOneSlotPerItemTheyCanHold, data);
        Test(CheckAPackedCrateKeepsItsFiltersWhenPutDownAgain, data);
        Test(CheckAnEmptyCratePacksOnlyTheHeldUnits, data);
    }

    private static void CheckAnEmptyCratePacksOnlyTheHeldUnits(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "crate", 1); Give(world, "wood", 10);
        world.Hold(world.Backpack, SlotOf(world.Backpack, "wood"), 5);
        world.FromHeldStack(() => world.MoveInCrate(0, "wood", int.MaxValue, true));
        string packed = world.Backpack.Items.Keys.Single(World.IsPacked);
        Require(world.Packed(packed)!["wood"] == 5 && world.Backpack["wood"] == 5 && world.Held == null,
            "five held wood put on an empty crate should pack just those five");
    }

    private static void CheckPlayerMovesItemsInAndOutOfACarriedCrate(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(20, 20);
        Give(world, "crate", 1); Give(world, "berries", 12); Give(world, "wood", 2); Give(world, "crate", 1);
        var placed = world.PlaceCrate("crate", world.Player + new Vector2(0, 2))!;
        world.Transfer(world.Backpack, placed.Inventory, "berries", 12);
        Require(world.PickUpCrate(placed), "the filled crate should be picked up");
        string packed = world.Backpack.Items.Keys.Single(World.IsPacked);
        int id = World.PackedId(packed);

        Require(world.MoveInCrate(id, "berries", 3, packing: false) == 3 && world.Backpack["berries"] == 3, "taking 3 berries out of a carried crate should put them in the backpack");
        Require(world.CarriedCrate(id) is string berries && world.FilterEntryOf(berries) == World.CrateOf("berries") && world.Packed(berries)!["berries"] == 9, "a crate of berries should stay one with 9 left");
        Require(world.MoveInCrate(id, "wood", 2, packing: true) == 2 && world.Backpack["wood"] == 0, "packing 2 wood into the carried crate should take them from the backpack");
        Require(world.CarriedCrate(id) is string mixed && world.FilterEntryOf(mixed) == World.MixedCrate, "a crate of berries and wood should count as a mixed crate");
        Require(world.MoveInCrate(id, "crate", 1, packing: true) == 0 && world.Backpack["crate"] == 1, "a crate should never go into a crate");
        world.MoveInCrate(id, "berries", 9, packing: false); world.MoveInCrate(id, "wood", 2, packing: false);
        Require(world.CarriedCrate(id) == null && world.Backpack["crate"] == 2 && world.Packed(packed) == null, "an emptied crate should become a plain empty crate");
    }

    private static void CheckCratesShowOneSlotPerItemTheyCanHold(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(10, 10);
        var crate = TakeEverything(world, world.Add(ThingKind.Crate, new Vector2(11, 10))).Inventory;
        int cap = data.CrateCapacity;
        Require(crate.ShownSlots == cap, "an empty crate should show one slot per item it can hold");
        Require(crate.Add("berries", cap + 5) == cap, "a full stack should fill the crate only up to its capacity");
        Require(crate.ShownSlots == 1, "a full crate should show one slot");
        Require(crate.Room("wood") == 0, "a full crate should have no room for more");
        crate.Remove("berries", cap / 2);
        Require(crate.ShownSlots == 1 + cap - cap / 2, "half a stack leaves one single-item slot per free unit");
        crate.Remove("berries", cap);
        foreach (var item in new[] { "wood", "stone", "berries", "flour", "coal", "iron_ore", "planks", "wood_parts", "iron_bar", "steel_bar", "tin_bar", "steel_parts" }.Take(cap)) crate.Add(item, 1);
        Require(crate.UsedSlots == cap, "a crate should give each different item its own slot");
        Require(crate.Count == cap, "a crate should count as many different single items as it holds");
        Require(crate.Room("raw_meat") == 0, "a crate full of single items should have no room for more");

        // Packed crates keep their contents through the backpack, a save and placing them again.
        crate.Clear(); crate.Add("raw_meat", 5); crate.Add("steel_axe", 2);
        string packed = world.Backpack.Items.Keys.Any(World.IsCrate) ? "" : PackCrate(world);
        Require(world.Things.Count(t => t.Kind == ThingKind.Crate) == 1, "picking up should remove only the picked crate");
        var placed = world.Things.Single(t => t.Kind == ThingKind.Crate);
        Require(world.PickUpCrate(placed), "a filled crate should be picked up");
        string meat = world.Backpack.Items.Keys.Single(id => World.IsCrate(id) && id != packed);
        Require(world.Backpack.StackOf(meat) == 1, "a packed crate should be one unstackable item");
        Require(world.Describe(meat).Contains("5 Raw meat"), "a packed crate should name its contents");
        Require(world.Backpack["crate"] == 1, "an empty crate should be a plain crate");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Packed(meat)?["steel_axe"] == 2, "save/load should keep what a packed crate holds");
        loaded.Player = new Vector2(10, 10);
        var again = loaded.PlaceCrate(meat, new Vector2(12, 12));
        Require(again != null, "placing a packed crate should place a crate");
        Require(again.Inventory["raw_meat"] == 5, "placing a packed crate should put its contents back into the placed crate");
        Require(again.Inventory.Capacity == cap, "placing a packed crate should give it its full capacity");
        Require(loaded.Packed(meat) == null, "a loaded save should no longer hold the packed crate in the world");
        Require(loaded.Backpack[meat] == 0, "a loaded save should no longer hold the packed crate in the backpack");
        Require(world.DepositFor("wood", new Vector2(10, 10)) == null, "crates are manual storage that workers never fill");
    }

    private static void CheckAPackedCrateKeepsItsFiltersWhenPutDownAgain(GameData data) {
        var world = EmptyWorld(data);
        var crate = TakeEverything(world, world.Add(ThingKind.Crate, world.Player + new Vector2(1, 0)));
        crate.Inventory.Add("wood", 3);
        world.SetFilter(crate, "wood", false, true); world.SetFilter(crate, "stone", true, true);
        Require(world.PickUpCrate(crate), "a nearby crate should be picked up");
        string packed = world.Backpack.Items.Keys.Single(World.IsPacked);
        var placed = world.PlaceCrate(packed, world.Player + new Vector2(2, 0))!;
        Require(placed.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && placed.Inventory.OutputFilter.SequenceEqual(new[] { "stone" }),
            "a crate of goods put down again should keep the filters it had");
        world.Transfer(placed.Inventory, world.Backpack, "wood", 3);
        Require(world.PickUpCrate(placed), "the emptied crate should be picked up");
        var empty = world.PlaceCrate("crate", world.Player + new Vector2(2, 0))!;
        Require(empty.Inventory.InputFilter.Count == 0 && empty.Inventory.OutputFilter.SequenceEqual(new[] { Inventory.Everything }),
            "an empty crate put down again should have a new crate's filters: nothing in, everything out");
    }
}
