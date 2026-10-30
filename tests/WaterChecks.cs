using System;
using ChopChopMeow;
using Godot;
using System.Linq;
using static Checks;
using static SetupSteps;
using static WorkerSteps;

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
        CheckLegacyStackedBottlesOverflowToRecoveryBox(data);
    }

    private static void CheckRecipesTakeOnlyFullBottles(GameData data) {
        foreach (string item in new[] { "empty_bottle", "water_1", "water_2" }) {
            var world = new World(data, false);
            var table = world.Add(ThingKind.Cooktable, world.Player + Vector2.Right);
            world.SetRecipe(table, "dough");
            Give(world, item, 1); Give(world, "flour", 1);
            string message = ""; world.Notify = text => message = text;
            Require(world.LoadInput(table, item, 1) == 0 && message == Water.FullBottleWarning,
                "partial/empty bottle input clicks must give the full-bottle warning");
            message = "";
            Require(world.Transfer(world.Backpack, table.Inventory, item, 1) == 0 && message == Water.FullBottleWarning,
                "direct recipe transfers must reject non-full bottles with the warning");
            message = ""; world.LoadBatch(table);
            Require(message == Water.FullBottleWarning && world.Carried(item) == 1 && table.Inventory.Count == 0,
                "batch loading must retain the non-full bottle and explain the requirement");
            var chest = world.Add(ThingKind.Chest, world.Player + Vector2.Left);
            Require(world.Transfer(world.Backpack, chest.Inventory, item, 1) == 1,
                "non-full bottles must remain transferable to ordinary storage");
            Give(world, "water", 1);
            Require(world.LoadInput(table, "water", 1) == 1, "a full bottle must load normally");
        }
    }

    private static void CheckBottleStatesAreDefinedInData(GameData data) {
        Require(data.ThirstHours == 24, "a full thirst gauge should last one game day");
        foreach (string item in Water.BottleItems) {
            Require(data.Items.ContainsKey(item), $"data should define bottle state {item}");
            Require(Inventory.Unstacked.Contains(item), $"bottle state {item} should take an individual inventory slot");
            Require(!data.IsFood(item), $"bottle state {item} should not count as food");
        }
        Require(data.IsConsumable("water") && data.IsConsumable("water_2") && data.IsConsumable("water_1"), "each filled bottle state should be consumable");
        Require(!data.IsConsumable("empty_bottle"), "an empty bottle should not be consumable");
    }

    private static void CheckBottleGivesThreeSips(GameData data) {
        var world = new World(data, false);
        SetNeeds(world, thirst: 0);
        Require(Give(world, "water", 1) == 1, "a filled bottle should fit");
        Require(Water.SipBottle(world, "water"), "first sip should be consumed");
        Require(world.Carried("water_2") == 1 && world.Thirst > 33.32f && world.Thirst < 33.34f, "first sip should leave two sips and restore one third thirst");
        Require(Water.SipBottle(world, "water_2") && world.Carried("water_1") == 1, "second sip should leave one sip");
        Require(Water.SipBottle(world, "water_1") && world.Carried("empty_bottle") == 1, "third sip should leave the same bottle empty");
        Require(world.Thirst > 99.99f && world.Thirst <= 100, "three sips should restore a full thirst gauge");
        Require(!Water.SipBottle(world, "empty_bottle") && world.Carried("empty_bottle") == 1, "an empty bottle should not produce a free sip");
        Require(world.Backpack.UsedSlots == 1, "each sip should preserve one physical bottle slot");
    }

    private static void CheckBottlesRefillFromWaterAndCanBeSippedDirectly(GameData data) {
        var world = new World(data, false);
        var lake = new Thing { Kind = ThingKind.Water };
        SetBackpackSlots(world, 1);
        Give(world, "empty_bottle", 1);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.RefilledBottle, "gathering with an empty bottle should refill it");
        Require(world.Carried("water") == 1 && world.Backpack.Count == 1 && world.Backpack.UsedSlots == 1, "refilling should reuse its slot without creating bottles");
        Require(Water.CollectWater(world, lake) == WaterCollectResult.NoChange, "a full bottle should not refill into an extra bottle");

        var priority = new World(data, false);
        Give(priority, "water_1", 1);
        Give(priority, "empty_bottle", 1);
        Water.CollectWater(priority, lake);
        Require(priority.Carried("water") == 1 && priority.Carried("water_1") == 1 && priority.Carried("empty_bottle") == 0, "an empty bottle should refill before a partial bottle");

        Drop(world, "water", 1);
        SetNeeds(world, thirst: 25);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceSip, "gathering without a bottle should drink directly from the source");
        Require(world.Thirst > 58.32f && world.Thirst < 58.34f && world.Backpack.Count == 0, "a direct source sip should restore exactly one third thirst without creating a bottle");
    }

    private static void CheckBottleKeepsPartialStateAcrossSave(GameData data) {
        var world = new World(data, false);
        Give(world, "water_2", 1);
        var loaded = new World(data, false);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Carried("water_2") == 1 && loaded.Carried("water") == 0, "save and load should preserve the bottle's partial state");
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
        world.SetRecipe(table, "dough"); table.Inventory.Add("water", 1); table.Inventory.Add("flour", 1);
        for (int i = 0; i < 100; i++) world.Tick(.25f, true);
        Require(table.Inventory["empty_bottle"] == 1 && table.Inventory["water"] == 0 && table.Inventory["dough"] > 0,
            "cooking must use the water and return its reusable container");

        world = new World(data, false);
        var shore = Terrain.WaterPoint(Terrain.Spawn);
        var outward = (shore - Terrain.Lake).Normalized();
        var pond = world.Add(ThingKind.Water, shore, 9999);
        var chest = world.Add(ThingKind.Chest, shore + outward * 5); chest.Inventory.Add("empty_bottle", 1);
        var worker = AddWorker(world, chest.Pos);
        world.Assign(worker, pond);
        for (int i = 0; i < 240; i++) world.Tick(.25f, true);
        Require(chest.Inventory["water"] == 1 && chest.Inventory["empty_bottle"] == 0 && worker.Bag.Count == 0,
            "an assigned collector must refill and deliver an existing bottle without generating more containers");
    }

    private static void CheckLegacyStackedBottlesOverflowToRecoveryBox(GameData data) {
        var old = new World(data, false);
        Inventory.Unstacked.Remove("water");
        string saved;
        try { Give(old, "water", 64); Give(old, "wood", 1); saved = old.Serialize(); } finally { Inventory.Unstacked.Add("water"); }
        var loaded = new World(data, false); loaded.Deserialize(saved);
        int preserved = loaded.Carried("water") + loaded.Things.Where(t => t.Kind == ThingKind.Grave).Sum(t => t.Inventory["water"]);
        Require(preserved == 64 && loaded.Backpack.UsedSlots <= 24 && loaded.Carried("wood") == 1,
            "legacy stacked bottles must overflow to a recovery box without loss or oversized player inventory");
    }
}
