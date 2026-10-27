using System;
using ChopChopMeow;
using Godot;
using System.Linq;

namespace ChopChopMeow.Tests;

/// <summary>Focused bottle-state checks; called from the deterministic simulation checks.</summary>
public static class WaterChecks {
    public static void Run(GameData data) {
        CheckFullBottleRequirement(data);
        CheckData(data);
        CheckThreeSips(data);
        CheckRefillAndDirectSip(data);
        CheckBottleSave(data);
        CheckCrafting(data);
        CheckRecipeContainersAndWorker(data);
        CheckLegacyOverflow(data);
    }

    private static void CheckFullBottleRequirement(GameData data) {
        foreach (string item in new[] { "empty_bottle", "water_1", "water_2" }) {
            var world = new World(data, false);
            var table = world.Add("cooktable", world.Player + Vector2.Right);
            world.SetRecipe(table, "dough");
            world.S.Backpack.Add(item, 1); world.S.Backpack.Add("flour", 1);
            string message = ""; world.Notify = text => message = text;
            Require(world.LoadInput(table, item, 1) == 0 && message == Water.FullBottleWarning,
                "partial/empty bottle input clicks must give the full-bottle warning");
            message = "";
            Require(world.Transfer(world.S.Backpack, table.Inventory, item, 1) == 0 && message == Water.FullBottleWarning,
                "direct recipe transfers must reject non-full bottles with the warning");
            message = ""; world.LoadBatch(table);
            Require(message == Water.FullBottleWarning && world.S.Backpack[item] == 1 && table.Inventory.Count == 0,
                "batch loading must retain the non-full bottle and explain the requirement");
            var chest = world.Add("chest", world.Player + Vector2.Left);
            Require(world.Transfer(world.S.Backpack, chest.Inventory, item, 1) == 1,
                "non-full bottles must remain transferable to ordinary storage");
            world.S.Backpack.Add("water", 1);
            Require(world.LoadInput(table, "water", 1) == 1, "a full bottle must load normally");
        }
    }

    private static void CheckData(GameData data) {
        Require(data.ThirstHours == 24, "a full thirst gauge should last one game day");
        foreach (string item in Water.BottleItems) {
            Require(data.Items.ContainsKey(item), $"data should define bottle state {item}");
            Require(Inventory.Unstacked.Contains(item), $"bottle state {item} should take an individual inventory slot");
            Require(!data.IsFood(item), $"bottle state {item} should not count as food");
        }
        Require(data.IsConsumable("water") && data.IsConsumable("water_2") && data.IsConsumable("water_1"), "each filled bottle state should be consumable");
        Require(!data.IsConsumable("empty_bottle"), "an empty bottle should not be consumable");
    }

    private static void CheckThreeSips(GameData data) {
        var world = new World(data, false);
        world.S.Thirst = 0;
        Require(world.S.Backpack.Add("water", 1) == 1, "a filled bottle should fit");
        Require(Water.SipBottle(world, "water"), "first sip should be consumed");
        Require(world.S.Backpack["water_2"] == 1 && world.S.Thirst > 33.32f && world.S.Thirst < 33.34f, "first sip should leave two sips and restore one third thirst");
        Require(Water.SipBottle(world, "water_2") && world.S.Backpack["water_1"] == 1, "second sip should leave one sip");
        Require(Water.SipBottle(world, "water_1") && world.S.Backpack["empty_bottle"] == 1, "third sip should leave the same bottle empty");
        Require(world.S.Thirst > 99.99f && world.S.Thirst <= 100, "three sips should restore a full thirst gauge");
        Require(!Water.SipBottle(world, "empty_bottle") && world.S.Backpack["empty_bottle"] == 1, "an empty bottle should not produce a free sip");
        Require(world.S.Backpack.UsedSlots == 1, "each sip should preserve one physical bottle slot");
    }

    private static void CheckRefillAndDirectSip(GameData data) {
        var world = new World(data, false);
        var lake = new Thing { Kind = "water" };
        world.S.Backpack.Slots = 1;
        world.S.Backpack.Add("empty_bottle", 1);
        Require(Water.CollectWater(world, lake) == WaterCollectResult.RefilledBottle, "gathering with an empty bottle should refill it");
        Require(world.S.Backpack["water"] == 1 && world.S.Backpack.Count == 1 && world.S.Backpack.UsedSlots == 1, "refilling should reuse its slot without creating bottles");
        Require(Water.CollectWater(world, lake) == WaterCollectResult.NoChange, "a full bottle should not refill into an extra bottle");

        var priority = new World(data, false);
        priority.S.Backpack.Add("water_1", 1);
        priority.S.Backpack.Add("empty_bottle", 1);
        Water.CollectWater(priority, lake);
        Require(priority.S.Backpack["water"] == 1 && priority.S.Backpack["water_1"] == 1 && priority.S.Backpack["empty_bottle"] == 0, "an empty bottle should refill before a partial bottle");

        world.S.Backpack.Remove("water", 1);
        world.S.Thirst = 25;
        Require(Water.CollectWater(world, lake) == WaterCollectResult.SourceSip, "gathering without a bottle should drink directly from the source");
        Require(world.S.Thirst > 58.32f && world.S.Thirst < 58.34f && world.S.Backpack.Count == 0, "a direct source sip should restore exactly one third thirst without creating a bottle");
    }

    private static void CheckBottleSave(GameData data) {
        var world = new World(data, false);
        world.S.Backpack.Add("water_2", 1);
        var loaded = new World(data, false);
        loaded.Deserialize(world.Serialize());
        Require(loaded.S.Backpack["water_2"] == 1 && loaded.S.Backpack["water"] == 0, "save and load should preserve the bottle's partial state");
    }

    private static void CheckCrafting(GameData data) {
        var recipe = data.Recipes["water_bottle"];
        Require(recipe.Output == "empty_bottle" && recipe.Amount == 1 && recipe.Tool == "hammer" && recipe.Tier == 2, "the bottle recipe should create one empty bottle with the tin-can hammer tier");
        Require(recipe.Inputs.TryGetValue("tin_bar", out int tin) && tin == 1 && recipe.Inputs.TryGetValue("wood_parts", out int parts) && parts == 1, "the bottle recipe should use one tin bar and one wood part");
        Require(data.Market.Buys.ContainsKey("water"), "full bottles should remain purchasable");
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Water check failed: " + message);
    }

    private static void CheckRecipeContainersAndWorker(GameData data) {
        var world = new World(data, false);
        var bench = world.Add("bench", world.Player + Vector2.Right * 2);
        bench.Tools!.Add("steel_hammer", 1); world.SetRecipe(bench, "water_bottle");
        bench.Inventory.Add("tin_bar", 1); bench.Inventory.Add("wood_parts", 1);
        for (int i = 0; i < 100; i++) world.Tick(.25f, true);
        Require(bench.Inventory["empty_bottle"] == 1 && bench.Inventory["water"] == 0,
            "the actual bench recipe must make exactly one empty bottle without creating water");
        var table = world.Add("cooktable", world.Player + Vector2.Left * 2);
        world.SetRecipe(table, "dough"); table.Inventory.Add("water", 1); table.Inventory.Add("flour", 1);
        for (int i = 0; i < 100; i++) world.Tick(.25f, true);
        Require(table.Inventory["empty_bottle"] == 1 && table.Inventory["water"] == 0 && table.Inventory["dough"] > 0,
            "cooking must use the water and return its reusable container");

        world = new World(data, false);
        var shore = Terrain.WaterPoint(World.Spawn);
        var outward = (shore - Terrain.Lake).Normalized();
        var pond = world.Add("water", shore, 9999);
        var chest = world.Add("chest", shore + outward * 5); chest.Inventory.Add("empty_bottle", 1);
        var worker = new Worker { Id = world.S.NextId++, Pos = chest.Pos };
        world.S.Workers.Add(worker); world.Assign(worker, pond);
        for (int i = 0; i < 240; i++) world.Tick(.25f, true);
        Require(chest.Inventory["water"] == 1 && chest.Inventory["empty_bottle"] == 0 && worker.Bag.Count == 0,
            "an assigned collector must refill and deliver an existing bottle without generating more containers");
    }

    private static void CheckLegacyOverflow(GameData data) {
        var old = new World(data, false);
        Inventory.Unstacked.Remove("water");
        string saved;
        try { old.S.Backpack.Add("water", 64); old.S.Backpack.Add("wood", 1); saved = old.Serialize(); } finally { Inventory.Unstacked.Add("water"); }
        var loaded = new World(data, false); loaded.Deserialize(saved);
        int preserved = loaded.S.Backpack["water"] + loaded.S.Things.Where(t => t.Kind == "grave").Sum(t => t.Inventory["water"]);
        Require(preserved == 64 && loaded.S.Backpack.UsedSlots <= 24 && loaded.S.Backpack["wood"] == 1,
            "legacy stacked bottles must overflow to a recovery box without loss or oversized player inventory");
    }
}
