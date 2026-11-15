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
        CheckBottlesPourTheUnitsTheyHold(data);
        CheckBottleStatesAreDefinedInData(data);
        CheckBottleGivesThreeUnits(data);
        CheckBottlesRefillFromWaterAndCanBeDrunkDirectly(data);
        CheckBottleKeepsPartialStateAcrossSave(data);
        CheckBottleRecipeMakesEmptyBottleFromTinAndWood(data);
        CheckRecipesReturnReusableBottles(data);
        CheckTakingWaterOutNeedsABottleWithRoom(data);
        CheckHaulerPoursWaterAndStoresTheEmptyBottle(data);
        CheckHaulerTakesLeftoverWaterOutInBottles(data);
        CheckDismantledStationFillsBottles(data);
    }

    private static void CheckBottlesPourTheUnitsTheyHold(GameData data) {
        foreach (var (item, units) in new[] { (Water.OneUnit, 1), (Water.TwoUnits, 2), (Water.Full, 3) }) {
            var world = new World(data, false);
            var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
            world.SetRecipe(table, "dough");
            Give(world, item, 1);
            Require(world.LoadInput(table, item, 1) == 1 && table.Inventory[Water.Liquid] == units && table.Inventory[item] == 0,
                $"a bottle with {units} units must pour all {units} into the table");
            Require(world.Backpack[item] == 0 && world.Backpack[Water.Empty] == 1, "the poured bottle must stay in the backpack, empty");
        }
        var empty = new World(data, false);
        var cold = empty.Add(ThingKind.Cooktable, empty.Player + Vector2.Right);
        empty.SetRecipe(cold, "dough"); Give(empty, Water.Empty, 1);
        Require(empty.LoadInput(cold, Water.Empty, 1) == 0 && cold.Inventory.Count == 0 && empty.Backpack[Water.Empty] == 1, "an empty bottle must pour nothing and stay in the backpack");

        // Dough takes 3 units: a two-unit and a one-unit bottle together make a batch.
        var batch = new World(data, false);
        var table2 = batch.Add(ThingKind.Cooktable, batch.Player + Vector2.Right);
        batch.SetRecipe(table2, "dough");
        Give(batch, Water.TwoUnits, 1); Give(batch, Water.OneUnit, 1); Give(batch, "flour", 1);
        batch.LoadBatch(table2);
        Require(table2.Inventory[Water.Liquid] == 3 && table2.Inventory["flour"] == 1, "two part-drunk bottles must load a batch together");
        Require(batch.Backpack[Water.Empty] == 2, "both bottles must stay in the backpack, empty");
        Give(batch, Water.Full, 1);
        Require(batch.Transfer(batch.Backpack, table2.Inventory, Water.Full, 1) == 1 && table2.Inventory[Water.Liquid] == 6 && batch.Backpack[Water.Empty] == 3,
            "a bottle moved into the table by hand must pour too");
        var chest = batch.Add(ThingKind.Chest, batch.Player + Vector2.Left);
        Give(batch, Water.TwoUnits, 1);
        Require(batch.Transfer(batch.Backpack, chest.Inventory, Water.TwoUnits, 1) == 1 && chest.Inventory[Water.TwoUnits] == 1, "bottles must store in ordinary storage as they are");
    }

    private static void CheckBottleStatesAreDefinedInData(GameData data) {
        Require(data.ThirstHours == 24, "a full thirst gauge should last one game day");
        foreach (string item in Water.BottleItems) {
            Require(data.Items.ContainsKey(item), $"data should define bottle state {item}");
            Require(Inventory.Unstacked.Contains(item), $"bottle state {item} should take an individual inventory slot");
            Require(!data.IsFood(item), $"bottle state {item} should not count as food");
        }
        Require(data.IsConsumable("water"), "a full bottle should be consumable");
        Require(data.IsConsumable("water_2"), "a two-unit bottle should be consumable");
        Require(data.IsConsumable("water_1"), "a one-unit bottle should be consumable");
        Require(!data.IsConsumable("empty_bottle"), "an empty bottle should not be consumable");
    }

    private static void CheckBottleGivesThreeUnits(GameData data) {
        var world = new World(data, false);
        SetNeeds(world, thirst: 0);
        Require(Give(world, "water", 1) == 1, "a filled bottle should fit");
        Require(Water.DrinkUnit(world, "water"), "first unit should be consumed");
        Require(world.Backpack["water_2"] == 1, "first unit should leave two units");
        Require(world.Thirst > 33.32f, "first unit should restore at least one third thirst");
        Require(world.Thirst < 33.34f, "first unit should restore no more than one third thirst");
        Require(Water.DrinkUnit(world, "water_2") && world.Backpack["water_1"] == 1, "second unit should leave one unit");
        Require(Water.DrinkUnit(world, "water_1") && world.Backpack["empty_bottle"] == 1, "third unit should leave the same bottle empty");
        Require(world.Thirst > 99.99f && world.Thirst <= 100, "three units should restore a full thirst gauge");
        Require(!Water.DrinkUnit(world, "empty_bottle") && world.Backpack["empty_bottle"] == 1, "an empty bottle should not produce a free unit");
        Require(world.Backpack.UsedSlots == 1, "each unit should preserve one physical bottle slot");
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
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceUnit, "gathering without a bottle should drink directly from the source");
        Require(world.Thirst > 58.32f, "a direct source unit should restore at least one third thirst");
        Require(world.Thirst < 58.34f, "a direct source unit should restore no more than one third thirst");
        Require(world.Backpack.Count == 0, "a direct source unit should not create a bottle");
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
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 1) == 0 && message == "You need a water bottle with room for the water.",
            "taking water out with only full bottles must be refused with the reason");
        Require(table.Inventory[Water.Liquid] == 5, "refused water must stay on the table");
        Give(world, Water.TwoUnits, 1); Give(world, Water.Empty, 1);
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 1) == 1 && world.Backpack[Water.Full] == 2 && world.Backpack[Water.Empty] == 1,
            "one unit must top up the fullest bottle with room first");
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
        world.SetDuty(hauler, table, "haul", true);
        SafeTick(world, 40);
        Require(chest.Inventory["dough"] == 1 && table.Inventory[Water.Full] + table.Inventory[Water.Empty] == 0,
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
        Require(chest.Inventory[Water.OneUnit] == 1 && chest.Inventory[Water.Empty] == 0 && hauler.Bag.Count == 0,
            "the leftover unit must go back to storage in the empty bottle the hauler fetched");
    }

    private static void CheckDismantledStationFillsBottles(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 2);
        EmptyBackpack(world); Give(world, Water.Empty, 1);
        Require(world.Dismantle(table), "the table should come down");
        Require(world.Backpack[Water.TwoUnits] == 1 && world.Backpack[Water.Liquid] == 0 && world.Things.All(t => t.Inventory[Water.Liquid] == 0),
            "a dismantled station's water must fill the bottles carried");
    }
}
