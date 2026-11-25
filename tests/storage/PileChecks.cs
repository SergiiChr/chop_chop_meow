using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;

/// <summary>Checks of piles: one resource each, set by their filter, snapping together, and compost.</summary>
public static class PileChecks {
    public static void Run(GameData data) {
        Test(CheckPilesSnapTogetherHoldOneResourceAndMakeCompost, data);
        Test(CheckEmptiedPileKeepsItsFilterWhichChangesOnlyWhileEmpty, data);
        Test(CheckOnlyAnEmptyPileWithNoFilterTurnsIntoCompost, data);
        Test(CheckTenderFeedsCompostWithoutFood, data);
        Test(CheckCompostKeepsTheBottlesATenderBrought, data);
        Test(CheckPileOfPackedCratesTakesOnlyTheSameGood, data);
        Test(CheckPileOfMixedCratesTakesAnyMixedCrate, data);
    }

    /// <summary>Piles snap into grids and hold one resource, the first put in, and compost turns leaves and food into fertilizer.</summary>
    private static void CheckPilesSnapTogetherHoldOneResourceAndMakeCompost(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin + new Vector2(0, 2);
        Give(world, "wood", 40);
        var pile = world.Construction.Build(ThingKind.Pile, origin);
        Require(pile != null, "a pile should be built");
        Require(world.Backpack["wood"] == 32, "a pile should cost 8 wood");
        Require(pile!.Inventory.Slots == World.PileStacks, "a pile should hold 14 stacks");
        Require(TakeEverything(world, world.Add(ThingKind.Chest, origin + new Vector2(0, 8))).Inventory.Slots == 5, "a chest should hold a third of a pile, 5 stacks");
        Require(world.Construction.Snap(ThingKind.Pile, origin + new Vector2(3.4f, .4f)) == origin + new Vector2(3, 0) && world.Construction.Snap(ThingKind.Pile, origin + new Vector2(9, 0)) == origin + new Vector2(9, 0),
                "a pile placed beside another should snap edge to edge, and one far away should not");
        var east = world.Construction.Build(ThingKind.Pile, origin + new Vector2(3.3f, 0));
        Require(east != null, "a pile beside another should build");
        Require(world.Construction.PlacementBlock(ThingKind.Pile, origin + new Vector2(2, 0)) != null && east.Pos == origin + new Vector2(3, 0), "piles must not overlap but may touch");
        Require(world.CanStand(origin), "a pile should be walkable");

        world.Player = origin;
        Require(world.Transfer(world.Backpack, pile!.Inventory, "wood", 10) == 10 && world.HoldBlock(pile, "stone") == "This is a wood pile.", "the first resource in should be the only one the pile takes");
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && pile.Inventory.OutputFilter.SequenceEqual(new[] { "wood" }), "the first resource in should set the pile's filters, in and out");
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, origin + new Vector2(0, 5)));
        Require(world.DepositFor("wood", chest.Pos) == pile && world.DepositFor("stone", origin) != pile, "workers should prefer the matching pile and keep other goods out of it");

        Require(world.SetCompost(east, true), "compost should be set on the pile");
        Require(east.Inventory.InputFilter.Contains("leaves"), "compost should take leaves");
        Require(east.Inventory.InputFilter.Contains("crushed_berries"), "compost should take crushed berries");
        Require(!east.Inventory.InputFilter.Contains("stone"), "compost should refuse stone");
        Require(east.Inventory.OutputFilter.SequenceEqual(new[] { "fertilizer" }), "compost should give only fertilizer");
        east.Inventory.Add("leaves", 6); east.Inventory.Add("burnt_steak", 2);
        Require(world.HoldBlock(east, "stone") != null && !world.SetCompost(east, false), "compost should refuse stone and stay compost while mixed");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 1 && east.Inventory.Count == 5, "4 inputs should rot into 1 fertilizer in an in-game hour");
        SafeTick(world, data.DaySeconds / 24 + .5f);
        Require(east.Inventory["fertilizer"] == 2 && east.Inventory.Items.Keys.All(k => k == "fertilizer"), "compost should keep going while it has 4 inputs");
        Require(world.SourceFor("fertilizer", origin) == east, "workers should be able to take the fertilizer out");

        var station = TakeEverything(world, world.Add(ThingKind.Toolstation, origin + new Vector2(-4, 4)));
        station.Inventory.Add("rake", 1);
        var leaves = world.Add(ThingKind.LeafSpot, origin + new Vector2(-4, -4), World.LeavesPerSpot);
        var worker = AddWorker(world, station.Pos);
        world.Assign(worker, leaves);
        world.Player = new Vector2(40, 40);
        SafeTick(world, 40);
        Require(east.Inventory["leaves"] + east.Inventory["fertilizer"] * 4 > 8, "raked leaves should go to the compost, which takes them explicitly");
    }

    /// <summary>A pile's filter stays when the pile empties, and is set, taken off or cleared only while the pile is empty.</summary>
    private static void CheckEmptiedPileKeepsItsFilterWhichChangesOnlyWhileEmpty(GameData data) {
        var world = EmptyWorld(data);
        var origin = new Vector2(20, 20);
        world.Player = origin;
        Give(world, "wood", 11);
        var pile = world.Add(ThingKind.Pile, origin);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, origin + new Vector2(0, 5)));
        var said = new List<string>();
        world.Notify += said.Add;
        world.Transfer(world.Backpack, pile.Inventory, "wood", 10); world.Tick(.1f);
        world.SetFilter(pile, "wood", false, false); world.ClearFilters(pile);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && said.Count(m => m == "Can't change filter on a non-empty pile.") == 2,
            "a pile holding something should refuse to have its filter taken off or cleared");
        pile.Inventory.Remove("wood", 10);
        world.Tick(.1f);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }) && world.HoldBlock(pile, "stone") == "This is a wood pile." && world.DepositFor("wood", chest.Pos) == pile,
            "an emptied pile should keep its filter and still ask for its resource");
        world.SetFilter(pile, "stone", false, true);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "wood" }), "an empty pile should keep its resource until it is taken off");
        world.SetFilter(pile, "wood", false, false);
        Require(pile.Inventory.InputFilter.Count == 0 && world.Holds(pile, "stone"), "taking an empty pile's filter off should free it");
        world.SetFilter(pile, "stone", false, true);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { "stone" }) && pile.Inventory.OutputFilter.SequenceEqual(new[] { "stone" }) && world.HoldBlock(pile, "wood") == "This is a stone pile.",
            "a filter set on an empty pile should make it a pile of that resource, in and out");
        Require(world.DepositFor("stone", chest.Pos) == pile, "workers should bring stone to an empty pile set to stone");
        Require(world.Transfer(world.Backpack, pile.Inventory, "wood", 1) == 0, "a pile set to stone should refuse wood put in by hand");
        world.ClearFilters(pile);
        Require(pile.Inventory.InputFilter.Count == 0, "clearing an empty pile's filter should free it");
        world.SetFilter(pile, World.CrateOf("raw_meat"), false, true); world.SetFilter(pile, World.CrateOf("raw_meat"), false, false);
        world.SetFilter(pile, World.CrateOf("raw_meat"), false, true); world.SetFilter(pile, "stone", false, false);
        Require(pile.Inventory.InputFilter.SequenceEqual(new[] { World.CrateOf("raw_meat") }) && world.HoldBlock(pile, "stone") == "This is a crate of raw meat pile.",
            "a pile set to crates of a good should name that good, and keep it when another resource is taken off");
    }

    /// <summary>Only an empty pile with no filter turns into compost.</summary>
    private static void CheckOnlyAnEmptyPileWithNoFilterTurnsIntoCompost(GameData data) {
        var world = EmptyWorld(data);
        var pile = world.Add(ThingKind.Pile, new Vector2(20, 20));
        var said = new List<string>();
        world.Notify += said.Add;
        world.SetFilter(pile, "wood", false, true);
        Require(!world.SetCompost(pile, true) && said.Contains("Take the pile's filter off before composting."), "an empty pile with a filter should not turn into compost");
        world.ClearFilters(pile); pile.Inventory.Add("leaves", 2);
        Require(!world.SetCompost(pile, true) && said.Contains("Empty the pile before composting."), "a pile holding leaves should not turn into compost");
        pile.Inventory.Remove("leaves", 2);
        Require(world.SetCompost(pile, true), "an empty pile with no filter should turn into compost");
    }

    /// <summary>A tender feeds compost from storage, skipping food when told to, and carries its fertilizer back.</summary>
    private static void CheckTenderFeedsCompostWithoutFood(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        var compost = world.Add(ThingKind.Pile, new Vector2(16, 24));
        Require(world.SetCompost(compost, true), "an empty pile should turn into compost");
        world.SetSkipFood(compost, true);
        compost.Inventory.Add("fertilizer", 2);
        chest.Inventory.Add("leaves", 8); chest.Inventory.Add("berries", 6);
        var tender = AddWorker(world, new Vector2(17, 22));
        world.SetDuty(tender, compost, JobKind.Tend, true);
        SafeTick(world, 40);
        Require(chest.Inventory["fertilizer"] == 2, "a tender should carry fertilizer to storage");
        Require(compost.Inventory["leaves"] > 0, "a tender should bring leaves to the compost");
        Require(chest.Inventory["berries"] == 6, "a tender should leave food in storage when food deliveries are skipped");
        Require(compost.Inventory["berries"] == 0, "a tender should not bring food to the compost when food deliveries are skipped");
    }

    private static void CheckCompostKeepsTheBottlesATenderBrought(GameData data) {
        var world = EmptyWorld(data);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(20, 20)));
        var compost = world.Add(ThingKind.Pile, new Vector2(16, 24));
        Require(world.SetCompost(compost, true), "an empty pile should turn into compost");
        chest.Inventory.Add(BerryJam.Bottle, 1);
        chest.Inventory.Add(BerryJam.Bottles[2], 1);
        chest.Inventory.Add("leaves", 2);
        var tender = AddWorker(world, new Vector2(17, 22));
        world.SetDuty(tender, compost, JobKind.Tend, true);

        Work(world, 65, () => compost.Inventory["fertilizer"] == 1);
        Require(compost.Inventory["fertilizer"] == 1 && compost.Inventory[BerryJam.Bottle] == 0 && compost.Inventory[BerryJam.Bottles[2]] == 0,
            "the two bottles' jam and two leaves should make one fertilizer");
        world.Player = compost.Pos + Vector2.Down;
        Require(world.Transfer(compost.Inventory, world.Backpack, Water.Empty, 2) == 2,
            "the player should be able to recover both empty bottles after the jam composts");
    }

    /// <summary>A pile of packed crates takes more crates of the same good, but no crate of another good.</summary>
    private static void CheckPileOfPackedCratesTakesOnlyTheSameGood(GameData data) {
        var world = EmptyWorld(data);
        string crate = PackCrate(world, ("bread", data.CrateCapacity));
        var pile = world.Add(ThingKind.Pile, new Vector2(30, 30));
        pile.Inventory.Add(crate, 1); Drop(world, crate, 1);
        world.Tick(.01f);
        string otherBread = PackCrate(world, ("bread", 2)), berryCrate = PackCrate(world, ("berries", 2));
        Require(world.Holds(pile, otherBread), "a pile of bread crates should hold another bread crate");
        Require(pile.Inventory.Accepts(otherBread),
            "a pile of bread crates should accept another bread crate into its inventory");
        Require(!world.Holds(pile, berryCrate), "a pile of bread crates should not hold a crate of berries");
        Require(!world.Holds(pile, PackCrate(world, ("bread", 1), ("berries", 1))), "a pile of bread crates should not hold a crate of mixed goods");
    }

    /// <summary>A pile set by a crate of mixed goods takes any other crate of mixed goods, and stays set after the crate leaves.</summary>
    private static void CheckPileOfMixedCratesTakesAnyMixedCrate(GameData data) {
        var world = EmptyWorld(data);
        string first = PackCrate(world, ("bread", 2), ("berries", 2));
        var pile = world.Add(ThingKind.Pile, new Vector2(30, 30));
        pile.Inventory.Add(first, 1); Drop(world, first, 1);
        world.Tick(.01f);
        Require(world.PileFilter(pile) == World.MixedCrate && world.FilterName(World.MixedCrate) == "Mixed crate", "a crate of mixed goods should set a pile to mixed crates");
        pile.Inventory.Remove(first, 1);
        world.Tick(.01f);
        string other = PackCrate(world, ("wood", 1), ("stone", 1));
        Require(world.Holds(pile, other) && pile.Inventory.Accepts(other) && pile.Inventory.AllowsWithdrawal(other),
            "an emptied pile of mixed crates should still take and give any other crate of mixed goods");
        Require(!world.Holds(pile, PackCrate(world, ("bread", 2))), "a pile of mixed crates should not hold a crate of one good");
    }
}
