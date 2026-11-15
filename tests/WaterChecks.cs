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
        CheckRecipesTakeOnlyFullBottles(data);
        CheckBottleStatesAreDefinedInData(data);
        CheckBottleGivesThreeSips(data);
        CheckBottlesRefillFromWaterAndCanBeSippedDirectly(data);
        CheckBottleKeepsPartialStateAcrossSave(data);
        CheckBottleRecipeMakesEmptyBottleFromTinAndWood(data);
        CheckRecipesReturnReusableBottles(data);
        CheckTakingWaterOutNeedsAnEmptyBottle(data);
        CheckHaulerPoursWaterAndStoresTheEmptyBottle(data);
        CheckHaulerTakesLeftoverWaterOutInEmptyBottles(data);
        CheckDismantledStationFillsEmptyBottles(data);
    }

    private static void CheckRecipesTakeOnlyFullBottles(GameData data) {
        foreach (string item in new[] { "empty_bottle", "water_1", "water_2" }) {
            var world = new World(data, false);
            var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
            world.SetRecipe(table, "dough");
            Give(world, item, 1); Give(world, "flour", 1);
            string message = ""; world.Notify = text => message = text;
            Require(world.LoadInput(table, item, 1) == 0 && message == "Requires full water bottle",
                "partial/empty bottle input clicks must give the full-bottle warning");
            message = "";
            Require(world.Transfer(world.Backpack, table.Inventory, item, 1) == 0 && message == "Requires full water bottle",
                "direct recipe transfers must reject non-full bottles with the warning");
            message = ""; world.LoadBatch(table);
            Require(message == "Requires full water bottle", "batch loading must explain the requirement");
            Require(world.Backpack[item] == 1, "batch loading must retain the non-full bottle");
            Require(table.Inventory.Count == 0, "batch loading must not put the bottle on the table");
            var chest = world.Add(ThingKind.Chest, world.Player + Vector2.Left);
            Require(world.Transfer(world.Backpack, chest.Inventory, item, 1) == 1,
                "non-full bottles must remain transferable to ordinary storage");
            Give(world, Water.Full, 1);
            Require(world.LoadInput(table, Water.Full, 1) == 1 && table.Inventory[Water.Liquid] == 1 && table.Inventory[Water.Full] == 0,
                "a full bottle must pour its water into the table");
            Require(world.Backpack[Water.Full] == 0 && world.Backpack[Water.Empty] == 1,
                "the poured bottle must stay in the backpack, empty");
        }
    }

    private static void CheckBottleStatesAreDefinedInData(GameData data) {
        Require(data.ThirstHours == 24, "a full thirst gauge should last one game day");
        foreach (string item in Water.BottleItems) {
            Require(data.Items.ContainsKey(item), $"data should define bottle state {item}");
            Require(Inventory.Unstacked.Contains(item), $"bottle state {item} should take an individual inventory slot");
            Require(!data.IsFood(item), $"bottle state {item} should not count as food");
        }
        Require(data.IsConsumable("water"), "a full bottle should be consumable");
        Require(data.IsConsumable("water_2"), "a two-sip bottle should be consumable");
        Require(data.IsConsumable("water_1"), "a one-sip bottle should be consumable");
        Require(!data.IsConsumable("empty_bottle"), "an empty bottle should not be consumable");
    }

    private static void CheckBottleGivesThreeSips(GameData data) {
        var world = new World(data, false);
        SetNeeds(world, thirst: 0);
        Require(Give(world, "water", 1) == 1, "a filled bottle should fit");
        Require(Water.SipBottle(world, "water"), "first sip should be consumed");
        Require(world.Backpack["water_2"] == 1, "first sip should leave two sips");
        Require(world.Thirst > 33.32f, "first sip should restore at least one third thirst");
        Require(world.Thirst < 33.34f, "first sip should restore no more than one third thirst");
        Require(Water.SipBottle(world, "water_2") && world.Backpack["water_1"] == 1, "second sip should leave one sip");
        Require(Water.SipBottle(world, "water_1") && world.Backpack["empty_bottle"] == 1, "third sip should leave the same bottle empty");
        Require(world.Thirst > 99.99f && world.Thirst <= 100, "three sips should restore a full thirst gauge");
        Require(!Water.SipBottle(world, "empty_bottle") && world.Backpack["empty_bottle"] == 1, "an empty bottle should not produce a free sip");
        Require(world.Backpack.UsedSlots == 1, "each sip should preserve one physical bottle slot");
    }

    private static void CheckBottlesRefillFromWaterAndCanBeSippedDirectly(GameData data) {
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
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceSip, "gathering without a bottle should drink directly from the source");
        Require(world.Thirst > 58.32f, "a direct source sip should restore at least one third thirst");
        Require(world.Thirst < 58.34f, "a direct source sip should restore no more than one third thirst");
        Require(world.Backpack.Count == 0, "a direct source sip should not create a bottle");
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
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 1); table.Inventory.Add("flour", 1);
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

    private static void CheckTakingWaterOutNeedsAnEmptyBottle(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 2);
        string message = ""; world.Notify = text => message = text;
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 1) == 0 && message == "You need an empty bottle to take the water.",
            "taking water out without an empty bottle must be refused with the reason");
        Require(table.Inventory[Water.Liquid] == 2 && world.Backpack.Count == 0, "refused water must stay on the table");
        Give(world, Water.Empty, 1);
        Require(world.Transfer(table.Inventory, world.Backpack, Water.Liquid, 2) == 1, "one empty bottle must take out one water");
        Require(world.Backpack[Water.Full] == 1 && world.Backpack[Water.Empty] == 0 && world.Backpack[Water.Liquid] == 0 && table.Inventory[Water.Liquid] == 1,
            "taking water out must fill the empty bottle, never put loose water in the backpack");
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

    private static void CheckHaulerTakesLeftoverWaterOutInEmptyBottles(GameData data) {
        var world = new World(data, false);
        var chest = world.Add(ThingKind.Chest, new Vector2(20, 20));
        chest.Inventory.Add(Water.Empty, 1);
        var table = world.Add(ThingKind.Cooktable, new Vector2(26, 22));
        world.SetRecipe(table, "berry_jam"); table.Inventory.Add(Water.Liquid, 1);
        var hauler = AddWorker(world, new Vector2(24, 21));
        world.SetDuty(hauler, table, "haul", true);
        SafeTick(world, 40);
        Require(table.Inventory[Water.Liquid] == 0, "a hauler must clear water the table's recipe no longer takes");
        Require(chest.Inventory[Water.Full] == 1 && chest.Inventory[Water.Empty] == 0 && hauler.Bag.Count == 0,
            "the leftover water must go back to storage in the empty bottle the hauler fetched");
    }

    private static void CheckDismantledStationFillsEmptyBottles(GameData data) {
        var world = new World(data, false);
        var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
        world.SetRecipe(table, "dough"); table.Inventory.Add(Water.Liquid, 2);
        EmptyBackpack(world); Give(world, Water.Empty, 1);
        Require(world.Dismantle(table), "the table should come down");
        Require(world.Backpack[Water.Full] == 1 && world.Backpack[Water.Liquid] == 0 && world.Things.All(t => t.Inventory[Water.Liquid] == 0),
            "a dismantled station's water must fill the empty bottles carried, and the rest spill");
    }
}
