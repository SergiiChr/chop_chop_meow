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
        CheckBottlesPourTheLitersTheyHold(data);
        CheckBottleStatesAreDefinedInData(data);
        CheckBottleGivesThreeLiters(data);
        CheckBottlesRefillFromWaterAndCanBeDrunkDirectly(data);
        CheckBottleKeepsPartialStateAcrossSave(data);
        CheckBottleRecipeMakesEmptyBottleFromTinAndWood(data);
        CheckRecipesReturnReusableBottles(data);
        CheckTakingWaterOutNeedsABottleWithRoom(data);
        CheckQueuedBatchPoursWaterFromBottles(data);
        CheckQueuedBatchSaysWhatItIsMissing(data);
        CheckPileHoldsBottlesAtEveryFill(data);
        CheckQueuedBatchCountsOnlyCarriedFuel(data);
        CheckFullBottleQueuedForOneLiterKeepsTheRest(data);
        CheckSwitchingRecipeTakesWaterIntoBottles(data);
        CheckStationTakesBottlesForItsWater(data);
        CheckHaulerPoursWaterAndStoresTheEmptyBottle(data);
        CheckHaulerTakesLeftoverWaterOutInBottles(data);
        CheckDismantledStationFillsBottles(data);
    }

    private static void CheckBottlesPourTheLitersTheyHold(GameData data) {
        foreach (var (item, liters) in new[] { (Water.OneLiter, 1), (Water.TwoLiters, 2), (Water.Full, 3) }) {
            var world = new World(data, false);
            var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
            world.SetRecipe(table, "dough");
            Give(world, item, 1);
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
        Give(batch, Water.TwoLiters, 1); Give(batch, Water.OneLiter, 1); Give(batch, "flour", 1);
        batch.LoadBatch(table2);
        Require(table2.Inventory[Water.Liquid] == 3 && table2.Inventory["flour"] == 1, "two part-drunk bottles must load a batch together");
        Require(batch.Backpack[Water.Empty] == 2, "both bottles must stay in the backpack, empty");
        Give(batch, Water.Full, 1);
        Require(batch.Transfer(batch.Backpack, table2.Inventory, Water.Full, 1) == 1 && table2.Inventory[Water.Liquid] == 6 && batch.Backpack[Water.Empty] == 3,
            "a bottle moved into the table by hand must pour too");
        var chest = batch.Add(ThingKind.Chest, batch.Player + Vector2.Left);
        Give(batch, Water.TwoLiters, 1);
        Require(batch.Transfer(batch.Backpack, chest.Inventory, Water.TwoLiters, 1) == 1 && chest.Inventory[Water.TwoLiters] == 1, "bottles must store in ordinary storage as they are");
    }

    private static void CheckBottleStatesAreDefinedInData(GameData data) {
        Require(data.ThirstHours == 24, "a full thirst gauge should last one game day");
        foreach (string item in Water.BottleItems) {
            Require(data.Items.ContainsKey(item), $"data should define bottle state {item}");
            Require(Inventory.Unstacked.Contains(item), $"bottle state {item} should take an individual inventory slot");
            Require(!data.IsFood(item), $"bottle state {item} should not count as food");
        }
        Require(data.IsConsumable("water"), "a full bottle should be consumable");
        Require(data.IsConsumable("water_2"), "a two-liter bottle should be consumable");
        Require(data.IsConsumable("water_1"), "a one-liter bottle should be consumable");
        Require(!data.IsConsumable("empty_bottle"), "an empty bottle should not be consumable");
    }

    private static void CheckBottleGivesThreeLiters(GameData data) {
        var world = new World(data, false);
        SetNeeds(world, thirst: 0);
        Require(Give(world, "water", 1) == 1, "a filled bottle should fit");
        Require(Water.DrinkLiter(world, "water"), "first liter should be consumed");
        Require(world.Backpack["water_2"] == 1, "first liter should leave two liters");
        Require(world.Thirst > 33.32f, "first liter should restore at least one third thirst");
        Require(world.Thirst < 33.34f, "first liter should restore no more than one third thirst");
        Require(Water.DrinkLiter(world, "water_2") && world.Backpack["water_1"] == 1, "second liter should leave one liter");
        Require(Water.DrinkLiter(world, "water_1") && world.Backpack["empty_bottle"] == 1, "third liter should leave the same bottle empty");
        Require(world.Thirst > 99.99f && world.Thirst <= 100, "three liters should restore a full thirst gauge");
        Require(!Water.DrinkLiter(world, "empty_bottle") && world.Backpack["empty_bottle"] == 1, "an empty bottle should not produce a free liter");
        Require(world.Backpack.UsedSlots == 1, "each liter should preserve one physical bottle slot");
    }

    private static void CheckBottlesRefillFromWaterAndCanBeDrunkDirectly(GameData data) {
        var world = new World(data, false);
        var lake = new Thing { Kind = ThingKind.Water };
        SetBackpackSlots(world, 1);
        Give(world, "empty_bottle", 1);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.RefilledBottle, "gathering with an empty bottle should refill it");
        Require(world.Backpack["water"] == 1, "refilling should give one full bottle");
        Require(world.Backpack.Count == 1, "refilling should not create bottles");
        Require(world.Backpack.UsedSlots == 1, "refilling should reuse its slot");
        Require(Water.CollectWater(world, lake) == WaterCollectResult.NoChange, "a full bottle should not refill into an extra bottle");

        var priority = new World(data, false);
        Give(priority, "water_1", 1);
        Give(priority, "empty_bottle", 1);
        Water.CollectWater(priority, lake);
        Require(priority.Backpack["water"] == 1, "an empty bottle should refill first");
        Require(priority.Backpack["water_1"] == 1, "the partial bottle should stay partial");
        Require(priority.Backpack["empty_bottle"] == 0, "no empty bottle should remain after the refill");

        Drop(world, "water", 1);
        SetNeeds(world, thirst: 25);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceLiter, "gathering without a bottle should drink directly from the source");
        Require(world.Thirst > 58.32f, "a direct source liter should restore at least one third thirst");
        Require(world.Thirst < 58.34f, "a direct source liter should restore no more than one third thirst");
        Require(world.Backpack.Count == 0, "a direct source liter should not create a bottle");
    }

    private static void CheckBottleKeepsPartialStateAcrossSave(GameData data) {
        var world = new World(data, false);
        Give(world, "water_2", 1);
        var loaded = new World(data, false);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Backpack["water_2"] == 1 && loaded.Backpack["water"] == 0, "save and load should preserve the bottle's partial state");
    }

    private static void CheckBottleRecipeMakesEmptyBottleFromTinAndWood(GameData data) {
        var recipe = data.Recipes["water_bottle"];
        Require(recipe.Output == "empty_bottle", "the bottle recipe should create an empty bottle");
        Require(recipe.Amount == 1, "the bottle recipe should create one bottle");
        Require(recipe.Tool == "hammer", "the bottle recipe should need the hammer");
        Require(recipe.Tier == 2, "the bottle recipe should need the tin-can hammer tier");
        Require(recipe.Inputs.TryGetValue("tin_bar", out int tin), "the bottle recipe should use tin bar");
        Require(tin == 1, "the bottle recipe should use one tin bar");
        Require(recipe.Inputs.TryGetValue("wood_parts", out int parts), "the bottle recipe should use wood parts");
        Require(parts == 1, "the bottle recipe should use one wood part");
        Require(data.Market.Buys.ContainsKey("water"), "full bottles should remain purchasable");
    }

    private static void CheckRecipesReturnReusableBottles(GameData data) {
        var world = new World(data, false);
        var bench = world.Add(ThingKind.Bench, world.Player + Vector2.Right * 2);
        bench.Tools!.Add("steel_hammer", 1); world.SetRecipe(bench, "water_bottle");
        bench.Inventory.Add("tin_bar", 1); bench.Inventory.Add("wood_parts", 1);
        for (int i = 0; i < 100; i++) world.Tick(.25f, true);
        Require(bench.Inventory["empty_bottle"] == 1 && bench.Inventory["water"] == 0,
            "the actual bench recipe must make exactly one empty bottle without creating water");
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Left * 2);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 3); table.Inventory.Add("flour", 1);
        for (int i = 0; i < 100; i++) world.Tick(.25f, true);
        Require(table.Inventory[Water.Empty] == 0, "cooking must leave no bottle on the table: the bottle stayed with whoever poured it");
        Require(table.Inventory[Water.Liquid] == 0, "cooking must use the water");
        Require(table.Inventory["dough"] > 0, "cooking must make its dough");

        world = new World(data, false);
        var shore = Terrain.WaterPoint(Terrain.Spawn);
        var outward = (shore - Terrain.Lake).Normalized();
        var pond = world.Add(ThingKind.Water, shore, 9999);
        var chest = world.Add(ThingKind.Chest, shore + outward * 5); chest.Inventory.Add("empty_bottle", 1);
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, pond);
        for (int i = 0; i < 240; i++) world.Tick(.25f, true);
        Require(chest.Inventory["water"] == 1, "an assigned collector must deliver the refilled bottle");
        Require(chest.Inventory["empty_bottle"] == 0, "an assigned collector must refill the existing bottle");
        Require(worker.Bag.Count == 0, "an assigned collector must not generate more containers");
    }

    private static void CheckTakingWaterOutNeedsABottleWithRoom(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 5);
        Give(world, Water.Full, 1);
        string message = ""; world.Notify = text => message = text;
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 1) == 0 && message == "You need an empty water vessel.",
            "taking water out with only full bottles must be refused with the reason");
        Require(table.Inventory[Water.Liquid] == 5, "refused water must stay on the table");
        Give(world, Water.TwoLiters, 1); Give(world, Water.Empty, 1);
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 1) == 1 && world.Backpack[Water.Full] == 2 && world.Backpack[Water.Empty] == 1,
            "one liter must top up the fullest bottle with room first");
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 10) == 3 && world.Backpack[Water.Full] == 3 && table.Inventory[Water.Liquid] == 1,
            "taking more must fill bottles while they have room, and leave the rest on the table");
        Require(world.Backpack[Water.Liquid] == 0, "water must never lie loose in the backpack");
    }

    private static void CheckHaulerPoursWaterAndStoresTheEmptyBottle(GameData data) {
        var world = new World(data, false);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        chest.Inventory.Add(Water.Full, 1); chest.Inventory.Add("flour", 4);
        var table = world.Add(ThingKind.Cooktable, new Vector2(26, 22));
        world.SetRecipe(table, "dough");
        var hauler = AddWorker(world, new Vector2(24, 21));
        world.SetDuty(hauler, table, "haul", true); world.SetDuty(hauler, table, "craft", true);
        SafeTick(world, 40);
        Require(chest.Inventory["dough"] == 3 && table.Inventory[Water.Full] + table.Inventory[Water.Empty] == 0,
            "a hauler must pour a full bottle into the table for its dough, leaving no bottle there");
        Require(chest.Inventory[Water.Empty] == 1 && hauler.Bag.Count == 0, "a hauler must store the emptied bottle again");
    }

    private static void CheckHaulerTakesLeftoverWaterOutInBottles(GameData data) {
        var world = new World(data, false);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        chest.Inventory.Add(Water.Empty, 1);
        var table = world.Add(ThingKind.Cooktable, new Vector2(26, 22));
        world.SetRecipe(table, "berry_jam"); table.Inventory.Add(Water.Liquid, 1);
        var hauler = AddWorker(world, new Vector2(24, 21));
        world.SetDuty(hauler, table, "haul", true);
        SafeTick(world, 40);
        Require(table.Inventory[Water.Liquid] == 0, "a hauler must clear water the table's recipe no longer takes");
        Require(chest.Inventory[Water.OneLiter] == 1 && chest.Inventory[Water.Empty] == 0 && hauler.Bag.Count == 0,
            "the leftover liter must go back to storage in the empty bottle the hauler fetched");
    }

    private static void CheckDismantledStationFillsBottles(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 2);
        EmptyBackpack(world); Give(world, Water.Empty, 1);
        string message = ""; world.Notify = text => message = text;
        Require(world.Dismantle(table), "the table should come down");
        Require(world.Backpack[Water.TwoLiters] == 1 && world.Backpack[Water.Liquid] == 0 && world.Things.All(t => t.Inventory[Water.Liquid] == 0),
            "a dismantled station's water must fill the bottles carried");
        var spill = new World(data, false);
        var full = spill.Add(ThingKind.Cooktable, spill.Player + Vector2.Right);
        spill.SetRecipe(full, "dough"); full.Inventory.Add(Water.Liquid, 5);
        EmptyBackpack(spill); Give(spill, Water.Empty, 1);
        string said = ""; spill.Notify = text => said = text;
        spill.Dismantle(full);
        Require(spill.Backpack[Water.Full] == 1 && said == "The cooking table is down. Everything is back in your backpack. 2L of water spilled.",
            $"water beyond the bottles carried must spill, and say so: \"{said}\"");
    }

    private static void CheckQueuedBatchPoursWaterFromBottles(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        Give(world, Water.Full, 1); Give(world, "flour", 1);
        string message = ""; world.Notify = text => message = text;
        world.QueueBatch(table, "dough");
        Require(table.Inventory[Water.Liquid] == 3 && table.Inventory["flour"] == 1, $"queuing dough must pour 3 liters from the full bottle: {message}");
        Require(world.Backpack[Water.Empty] == 1 && world.Backpack[Water.Full] == 0, "the bottle must stay in the backpack, empty");
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
        Require(world.Backpack[Water.TwoLiters] == 1 && world.Backpack[Water.Liquid] == 0, "its water must go into the bottle, never loose into the backpack");
    }

    private static void CheckStationTakesBottlesForItsWater(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough");
        Require(world.Takes(table, Water.Full) && world.Takes(table, Water.OneLiter), "a station on a water recipe must take bottles that hold water");
        Require(!world.Takes(table, Water.Empty), "a station must not take an empty bottle");
    }

    private static void CheckQueuedBatchSaysWhatItIsMissing(GameData data) {
        foreach (var (recipe, kind, carried, said) in new (string, ThingKind, (string, int)[], string)[] {
                 ("charcoal", ThingKind.Furnace, [("wood", 1), ("coal", 1)], "Missing 1 wood."),
                 ("charcoal", ThingKind.Furnace, [("wood", 2)], "Missing fuel (wood, coal or charcoal)."),
                 ("steel_bar", ThingKind.Reinforced, [("coal", 1)], "Missing 2 iron bar, 1 coal and fuel (coal)."),
                 ("meat_stew", ThingKind.Cooktable, [("raw_meat", 1), ("coal", 1)], "Missing 1L of water."),
                 ("dough", ThingKind.Cooktable, [("flour", 1), (Water.OneLiter, 1)], "Missing 2L of water.") }) {
            var world = new World(data, false);
            var station = world.Add(kind, world.Player + Vector2.Right);
            foreach (var (item, count) in carried) Give(world, item, count);
            string message = ""; world.Notify = text => message = text;
            world.QueueBatch(station, recipe);
            Require(message == said, $"queuing {recipe} short of a batch should say \"{said}\", not \"{message}\"");
            Require(!station.Processing, $"{recipe} short of a batch should not start");
        }
    }

    private static void CheckPileHoldsBottlesAtEveryFill(GameData data) {
        var world = new World(data, false);
        var pile = world.Add(ThingKind.Pile, world.Player + Vector2.Right);
        Give(world, Water.Full, 1); Give(world, Water.Empty, 1); Give(world, Water.OneLiter, 1); Give(world, "wood", 1);
        Require(world.Transfer(world.Backpack, pile.Inventory, Water.Full, 1) == 1, "a pile should take a full bottle");
        world.Tick(.1f);
        Require(world.Transfer(world.Backpack, pile.Inventory, Water.Empty, 1) == 1 && world.Transfer(world.Backpack, pile.Inventory, Water.OneLiter, 1) == 1,
            "a pile of bottles should take them empty and part-full too");
        Require(world.Transfer(world.Backpack, pile.Inventory, "wood", 1) == 0, "a pile of bottles should take nothing else");
        world.Tick(.1f);
        Require(pile.Inventory.Accepts(Water.Empty) && pile.Inventory.AllowsWithdrawal(Water.Full), "workers should store and fetch bottles at any fill there");
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
        Give(world, Water.Full, 1); Give(world, "raw_meat", 1); Give(world, "coal", 1);
        world.QueueBatch(table, "meat_stew");
        Require(world.Backpack[Water.TwoLiters] == 1 && world.Backpack[Water.Full] == 0, "meat stew should pour 1 liter and leave the bottle holding 2");
    }
}
