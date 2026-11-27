using System;
using ChopChopMeow;
using Godot;
using System.Linq;
using static Checks;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

namespace ChopChopMeow.Tests;

/// <summary>Focused bottle-state checks; called from the deterministic simulation checks.</summary>
public static class WaterChecks {
    public static void Run(GameData data) {
        Test(CheckBottlesPourTheLitersTheyHold, data);
        Test(CheckBottleStatesAreDefinedInData, data);
        Test(CheckFilledVesselsRevealTheirContents, data);
        Test(CheckBottleGivesThreeLiters, data);
        Test(CheckBottleKeepsPartialStateAcrossSave, data);
        Test(CheckBottleRecipeMakesEmptyBottleFromSteelAndWood, data);
        Test(CheckRecipesReturnReusableBottles, data);
        Test(CheckTakingWaterOutNeedsABottleWithRoom, data);
        Test(CheckQueuedBatchPoursWaterFromBottles, data);
        Test(CheckQueuedBatchSaysWhatItIsMissing, data);
        Test(CheckPileTakesBottlesOfWaterApartFromEmptyBottles, data);
        Test(CheckQueuedBatchCountsOnlyCarriedFuel, data);
        Test(CheckFullBottleQueuedForOneLiterKeepsTheRest, data);
        Test(CheckSwitchingRecipeTakesWaterIntoBottles, data);
        Test(CheckStationTakesBottlesForItsWater, data);
        Test(CheckABucketPouredByHandFillsAStationsWaterToAStack, data);
        Test(CheckDismantledStationFillsBottles, data);
    }

    private static void CheckBottlesPourTheLitersTheyHold(GameData data) {
        for (int liters = 1; liters <= Water.Capacity; liters++) {
            var world = new World(data, false);
            var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
            world.SetRecipe(table, "dough");
            string item = GiveWater(world, liters);
            Require(world.LoadInput(table, item, 1) == 1 && table.Inventory[Water.Liquid] == liters && table.Inventory[item] == 0,
                $"a bottle with {liters} liters must pour all {liters} into the table");
            Require(world.Backpack[item] == 0 && world.Backpack[Water.Empty] == 1, "the poured bottle must stay in the backpack, empty");
        }
        var empty = new World(data, false);
        var cold = empty.Add(ThingKind.Cooktable, empty.Player + Vector2.Right);
        empty.SetRecipe(cold, "dough"); Give(empty, Water.Empty, 1);
        Require(empty.LoadInput(cold, Water.Empty, 1) == 0 && cold.Inventory.Count == 0 && empty.Backpack[Water.Empty] == 1, "an empty bottle must pour nothing and stay in the backpack");

        // Dough takes 3 liters: a two-liter and a one-liter bottle together make a batch.
        var batch = new World(data, false);
        var table2 = batch.Add(ThingKind.Cooktable, batch.Player + Vector2.Right);
        batch.SetRecipe(table2, "dough");
        GiveWater(batch, 2); GiveWater(batch, 1); Give(batch, "flour", 1);
        batch.LoadBatch(table2);
        Require(table2.Inventory[Water.Liquid] == 3 && table2.Inventory["flour"] == 1, "two part-drunk bottles must load a batch together");
        Require(batch.Backpack[Water.Empty] == 2, "both bottles must stay in the backpack, empty");
        string full = GiveWater(batch);
        Require(batch.Transfer(batch.Backpack, table2.Inventory, full, 1) == 1 && table2.Inventory[Water.Liquid] == 6 && batch.Backpack[Water.Empty] == 3,
            "a bottle moved into the table by hand must pour too");
        var chest = TakeEverything(batch, batch.Add(ThingKind.Chest, batch.Player + Vector2.Left));
        string twoLiters = GiveWater(batch, 2);
        Require(batch.Transfer(batch.Backpack, chest.Inventory, twoLiters, 1) == 1 && Vessels.Count(chest.Inventory, Water.Empty, Water.Liquid, 2) == 1, "bottles must store in ordinary storage as they are");
    }

    private static void CheckBottleStatesAreDefinedInData(GameData data) {
        Require(data.ThirstHours == 24, "a full thirst gauge should last one game day");
        var bottle = data.VesselOf(Water.Empty, Water.Liquid);
        Require(data.Items.ContainsKey(Water.Empty) && bottle != null && bottle.Capacity == Water.Capacity, "data should define the bottle and the three liters of water it holds");
        for (int liters = 0; liters <= Water.Capacity; liters++) {
            string item = liters == 0 ? Water.Empty : Vessels.Holding(Water.Empty, 1, Water.Capacity, Water.Liquid, liters).Key;
            Require(new Inventory().StackOf(item) == 1, $"a bottle holding {liters} L should take an individual inventory slot");
            Require(!data.IsFood(item) && bottle!.Hunger == 0, $"a bottle holding {liters} L should not count as food");
        }
        // CheckBottleGivesThreeLiters drinks from the bottle at every fill.
        Require(bottle!.Thirst > 0, "a bottle holding water should be drunk from");
        Require(!data.IsConsumable("empty_bottle"), "an empty bottle should not be consumable");
    }

    private static void CheckFilledVesselsRevealTheirContents(GameData data) {
        var world = new World(data);
        Require(!world.Had(Water.Liquid), "An empty starter bottle must not reveal water as discovered.");
        var lake = world.ThingsOf(ThingKind.Water).Single();
        var shore = Terrain.WaterPoint(world.Player);
        world.Player = shore + (shore - Terrain.Lake).Normalized() * 1.8f;
        world.Gather(lake, world.GatherTime(ThingKind.Water, null));
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1 && world.Had(Water.Liquid),
            "Filling a bottle should discover the water it contains without needing to cook with it.");
    }

    private static void CheckBottleGivesThreeLiters(GameData data) {
        var world = new World(data, false);
        SetNeeds(world, thirst: 0);
        string bottle = GiveWater(world);
        world.Consume(bottle);
        Require(world.Thirst > 0, "first liter should be consumed");
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 2) == 1, "first liter should leave two liters");
        Require(world.Thirst > 33.32f, "first liter should restore at least one third thirst");
        Require(world.Thirst < 33.34f, "first liter should restore no more than one third thirst");
        world.Consume(bottle);
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 1) == 1, "second liter should leave one liter");
        world.Consume(bottle);
        Require(world.Backpack["empty_bottle"] == 1, "third liter should leave the same bottle empty");
        Require(world.Thirst > 99.99f && world.Thirst <= 100, "three liters should restore a full thirst gauge");
        SetNeeds(world, thirst: 50);
        world.Consume(Water.Empty);
        Require(world.Thirst == 50 && world.Backpack["empty_bottle"] == 1, "an empty bottle should not produce a free liter");
        Require(world.Backpack.UsedSlots == 1, "each liter should preserve one physical bottle slot");
    }

    private static void CheckBottleKeepsPartialStateAcrossSave(GameData data) {
        var world = new World(data, false);
        GiveWater(world, 2);
        var loaded = new World(data, false);
        loaded.Deserialize(world.Serialize());
        Require(Vessels.Count(loaded.Backpack, Water.Empty, Water.Liquid, 2) == 1 && Vessels.Count(loaded.Backpack, Water.Empty, Water.Liquid, 3) == 0, "save and load should preserve the bottle's partial state");
    }

    private static void CheckBottleRecipeMakesEmptyBottleFromSteelAndWood(GameData data) {
        var recipe = data.Recipes["water_bottle"];
        Require(recipe.Output == "empty_bottle", "the bottle recipe should create an empty bottle");
        Require(recipe.Amount == 1, "the bottle recipe should create one bottle");
        Require(recipe.Name == "Steel water bottle", "the crafting list should call the recipe Steel water bottle");
        Require(recipe.Tool == "hammer", "the bottle recipe should need the hammer");
        Require(recipe.Tier == 2, "the bottle recipe should need a steel hammer");
        Require(recipe.Inputs.TryGetValue("steel_sheet", out int steel) && steel == 1 && !recipe.Inputs.ContainsKey("tin_sheet"),
            "the bottle recipe should use one steel sheet and no tin");
        Require(recipe.Inputs.TryGetValue("wood_parts", out int parts), "the bottle recipe should use wood parts");
        Require(parts == 1, "the bottle recipe should use one wood part");
        Require(data.Market.Buys.ContainsKey("water"), "full bottles should remain purchasable");
    }

    private static void CheckRecipesReturnReusableBottles(GameData data) {
        var world = new World(data, false);
        var bench = world.Add(ThingKind.Bench, world.Player + Vector2.Right * 1.4f);
        bench.Tools!.Add("steel_hammer", 1);
        Give(world, "steel_sheet", 1); Give(world, "wood_parts", 1);
        world.QueueBatch(bench, "water_bottle");
        for (int i = 0; i < 100; i++) world.Tick(.25f, true);
        Require(bench.Inventory["empty_bottle"] == 1 && Vessels.Count(bench.Inventory, Water.Empty, Water.Liquid, 3) == 0,
            "the actual bench recipe must make exactly one empty bottle without creating water");
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Left * 1.4f);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 3);
        Give(world, "flour", 1); world.QueueBatch(table, "dough");
        for (int i = 0; i < 100; i++) world.Tick(.25f, true);
        Require(table.Inventory[Water.Empty] == 0, "cooking must leave no bottle on the table: the bottle stayed with whoever poured it");
        Require(table.Inventory[Water.Liquid] == 0, "cooking must use the water");
        Require(table.Inventory["dough"] > 0, "cooking must make its dough");

        world = new World(data, false);
        var shore = Terrain.WaterPoint(Terrain.Spawn);
        var outward = (shore - Terrain.Lake).Normalized();
        var lake = world.Add(ThingKind.Water, shore, 9999);
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, shore + outward * 5)); chest.Inventory.Add("empty_bottle", 1);
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, lake);
        for (int i = 0; i < 240; i++) world.Tick(.25f, true);
        Require(Vessels.Count(chest.Inventory, Water.Empty, Water.Liquid, 3) == 1, "an assigned collector must deliver the refilled bottle");
        Require(chest.Inventory["empty_bottle"] == 0, "an assigned collector must refill the existing bottle");
        Require(worker.Bag.Count == 0, "an assigned collector must not generate more containers");
    }

    private static void CheckTakingWaterOutNeedsABottleWithRoom(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 5);
        GiveVessel(world, Water.Empty, Water.Liquid, 3, 1);
        string message = ""; world.Notify = text => message = text;
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 1) == 0 && message == "You need an empty water vessel.",
            "taking water out with only full bottles must be refused with the reason");
        Require(table.Inventory[Water.Liquid] == 5, "refused water must stay on the table");
        GiveVessel(world, Water.Empty, Water.Liquid, 2, 1); Give(world, Water.Empty, 1);
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 1) == 1 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 2 && world.Backpack[Water.Empty] == 1,
            "one liter must top up the fullest bottle with room first");
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 10) == 3 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 3 && table.Inventory[Water.Liquid] == 1,
            "taking more must fill bottles while they have room, and leave the rest on the table");
        Require(world.Backpack[Water.Liquid] == 0, "water must never lie loose in the backpack");
    }

    /// <summary>A station's water slot holds one stack of liters, and a bucket poured by hand fills it to that and keeps the rest.</summary>
    private static void CheckABucketPouredByHandFillsAStationsWaterToAStack(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 60);
        EmptyBackpack(world); string bucket = GiveVessel(world, Bucket.Item, Water.Liquid, Bucket.Capacity);
        world.Transfer(world.Backpack, table.Inventory, bucket, 1);
        Require(table.Inventory[Water.Liquid] == 64 && Vessels.Count(world.Backpack, Bucket.Item, Water.Liquid, 6) == 1,
            $"a full bucket poured into a table holding 60 liters should fill it to 64 and keep 6: the table holds {table.Inventory[Water.Liquid]}");
    }

    private static void CheckDismantledStationFillsBottles(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 2);
        EmptyBackpack(world); Give(world, Water.Empty, 1);
        string message = ""; world.Notify = text => message = text;
        Require(world.Construction.Dismantle(table), "the table should come down");
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 2) == 1 && world.Backpack[Water.Liquid] == 0 && world.Things.All(t => t.Inventory[Water.Liquid] == 0),
            "a dismantled station's water must fill the bottles carried");
        var spill = new World(data, false);
        var full = spill.Add(ThingKind.Cooktable, spill.Player + Vector2.Right);
        spill.SetRecipe(full, "dough"); full.Inventory.Add(Water.Liquid, 5);
        EmptyBackpack(spill); Give(spill, Water.Empty, 1);
        string said = ""; spill.Notify = text => said = text;
        spill.Construction.Dismantle(full);
        Require(Vessels.Count(spill.Backpack, Water.Empty, Water.Liquid, 3) == 1 && said == "The cooking table is down. Everything is back in your backpack. 2L of water spilled.",
            $"water beyond the bottles carried must spill, and say so: \"{said}\"");
    }

    private static void CheckQueuedBatchPoursWaterFromBottles(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        GiveVessel(world, Water.Empty, Water.Liquid, 3, 1); Give(world, "flour", 1);
        string message = ""; world.Notify = text => message = text;
        world.QueueBatch(table, "dough");
        Require(table.Inventory[Water.Liquid] == 3 && table.Inventory["flour"] == 1, $"queuing dough must pour 3 liters from the full bottle: {message}");
        Require(world.Backpack[Water.Empty] == 1 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 0, "the bottle must stay in the backpack, empty");
    }

    private static void CheckSwitchingRecipeTakesWaterIntoBottles(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 2);
        string message = ""; world.Notify = text => message = text;
        world.QueueBatch(table, "crushed_berries");
        Require(table.Recipe == "dough" && table.Inventory[Water.Liquid] == 2 && message == "You need an empty water vessel.",
            $"turning a station holding water to another recipe with no bottle to take it must be refused: {message}");
        Give(world, Water.Empty, 1);
        world.QueueBatch(table, "crushed_berries");
        Require(table.Recipe == "crushed_berries" && table.Inventory[Water.Liquid] == 0, "with a bottle, the station must turn to the other recipe");
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 2) == 1 && world.Backpack[Water.Liquid] == 0, "its water must go into the bottle, never loose into the backpack");
    }

    private static void CheckStationTakesBottlesForItsWater(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough");
        string full = GiveWater(world), oneLiter = GiveWater(world, 1);
        Require(world.Takes(table, full) && world.Takes(table, oneLiter), "a station on a water recipe must take bottles that hold water");
        Require(!world.Takes(table, Water.Empty), "a station must not take an empty bottle");
    }

    private static void CheckQueuedBatchSaysWhatItIsMissing(GameData data) {
        // Each case carries goods and, when its liters are above 0, one bottle holding that much water.
        foreach (var (recipe, kind, carried, bottleLiters, said) in new (string, ThingKind, (string, int)[], int, string)[] {
                 ("charcoal", ThingKind.Furnace, [("wood", 1), ("coal", 1)], 0, "Missing 1 wood."),
                 ("charcoal", ThingKind.Furnace, [("wood", 2)], 0, "Missing fuel (wood, coal or charcoal)."),
                 ("steel_bar", ThingKind.Reinforced, [("coal", 1)], 0, "Missing 2 iron bar, 1 coal and fuel (coal)."),
                 ("meat_stew", ThingKind.Cooktable, [("raw_meat", 1), ("coal", 1)], 0, "Missing 1L of water."),
                 ("dough", ThingKind.Cooktable, [("flour", 1)], 1, "Missing 2L of water.") }) {
            var world = new World(data, false);
            var station = world.Add(kind, world.Player + Vector2.Right);
            foreach (var (item, count) in carried) Give(world, item, count);
            if (bottleLiters > 0) GiveWater(world, bottleLiters);
            string message = ""; world.Notify = text => message = text;
            world.QueueBatch(station, recipe);
            Require(message == said, $"queuing {recipe} short of a batch should say \"{said}\", not \"{message}\"");
            Require(!station.Processing, $"{recipe} short of a batch should not start");
        }
    }

    /// <summary>A pile of water takes bottles holding any of it and no empty bottle, and a pile of empty bottles takes only those.</summary>
    /// <summary>A pile of bottles of water takes them at any fill but no empty one, and a pile of empty bottles none holding water; none names bare water.</summary>
    private static void CheckPileTakesBottlesOfWaterApartFromEmptyBottles(GameData data) {
        var world = new World(data, false);
        var water = world.Add(ThingKind.Pile, world.Player + Vector2.Right);
        var empties = world.Add(ThingKind.Pile, world.Player + Vector2.Left);
        string full = GiveWater(world); Give(world, Water.Empty, 2); string oneLiter = GiveWater(world, 1), twoLiters = GiveWater(world, 2); Give(world, "wood", 1);
        Require(world.Transfer(world.Backpack, water.Inventory, full, 1) == 1, "a pile should take a full bottle");
        Require(world.Transfer(world.Backpack, water.Inventory, Water.Empty, 1) == 0, "a pile of bottles of water should refuse an empty bottle before its filter is set");
        world.Tick(.1f);
        Require(world.PileFilter(water) == World.HoldingOf(Water.Empty, Water.Liquid), "a full bottle should set the pile's filter to bottles of water at any fill");
        Require(world.Transfer(world.Backpack, water.Inventory, oneLiter, 1) == 1, "a pile of bottles of water should take a part-full bottle");
        Require(world.HoldBlock(water, Water.Empty) == "This is a bottle of water pile." && world.HoldBlock(water, "wood") == "This is a bottle of water pile.",
            "a pile of bottles of water should take no empty bottle and nothing else");
        Require(world.Transfer(world.Backpack, empties.Inventory, Water.Empty, 2) == 2, "a pile should take empty bottles");
        world.Tick(.1f);
        Require(empties.Inventory.InputFilter.SequenceEqual(new[] { Water.Empty }) && world.HoldBlock(empties, twoLiters) == "This is an empty bottle pile.",
            "a pile of empty bottles should take no bottle holding water");
        empties.Inventory.Remove(Water.Empty, 2);
        world.SetFilter(empties, Water.Empty, false, false); world.SetFilter(empties, Water.Liquid, false, true);
        Require(empties.Inventory.InputFilter.Count == 0, "a pile's filter should never name bare water");
        world.SetFilter(empties, twoLiters, false, true);
        Require(world.PileFilter(empties) == World.HoldingOf(Water.Empty, Water.Liquid), "picking a part-full bottle for an empty pile should set it to bottles of water at any fill");
    }

    private static void CheckQueuedBatchCountsOnlyCarriedFuel(GameData data) {
        var world = new World(data, false);
        var furnace = world.Add(ThingKind.Furnace, world.Player + Vector2.Right);
        world.SetRecipe(furnace, "charcoal"); furnace.Inventory.Add("wood", 2);
        Give(world, "wood", 1);
        Require(world.BatchShort(furnace, "charcoal") == "Missing fuel (wood, coal or charcoal).",
            $"wood in the input slots is not fuel, and the carried wood fills the input slots first: {world.BatchShort(furnace, "charcoal")}");
    }

    private static void CheckFullBottleQueuedForOneLiterKeepsTheRest(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        GiveVessel(world, Water.Empty, Water.Liquid, 3, 1); Give(world, "raw_meat", 1); Give(world, "coal", 1);
        world.QueueBatch(table, "meat_stew");
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 2) == 1 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 0, "meat stew should pour 1 liter and leave the bottle holding 2");
    }
}
