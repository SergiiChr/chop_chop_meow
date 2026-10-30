using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Measures value and worker output for the economy checks.</summary>
internal static class EconomySteps {
    /// <summary>What one batch of a recipe takes, with the cheapest fuel its station burns.</summary>
    public static Dictionary<string, int> BatchCost(World world, string recipe) {
        var r = world.Data.Recipes[recipe];
        if (r.Station is not ThingKind kind) return new(r.Inputs);
        var station = new Thing { Kind = kind, Recipe = recipe, Fuel = "wood" };
        var cost = new Dictionary<string, int>(world.BatchCost(station));
        if (world.FuelOf(station) is string fuel) cost[fuel] = cost.GetValueOrDefault(fuel) + 1;
        return cost;
    }

    public static float Value(GameData data, Dictionary<string, int> goods) => goods.Sum(p => data.Items[p.Key].Price * p.Value);

    /// <summary>Recipe steps from gathered materials: raw goods are 0, and a recipe is one more than its deepest input.</summary>
    public static int Depth(GameData data, string item) =>
        data.Recipes.Values.FirstOrDefault(r => r.Output == item) is RecipeDef r ? 1 + r.Inputs.Keys.Max(input => Depth(data, input)) : 0;

    /// <summary>Packs the goods into full crates by the signpost, sells them cart by cart and returns the coins.</summary>
    public static int Sell(World world, Thing sign, Dictionary<string, int> goods) {
        int capacity = world.Data.CrateCapacity, coins = 0;
        var crates = goods.SelectMany(p => Enumerable.Range(0, (p.Value + capacity - 1) / capacity).Select(i => (p.Key, Math.Min(capacity, p.Value - i * capacity)))).ToList();
        foreach (var load in crates.Chunk(World.MarketSlots)) {
            foreach (var (item, count) in load) {
                world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, (item, count)), 1);
            }
            Require(world.Sell(sign), "the village should take a day's basket");
            Work(world, world.Data.TradeSeconds + .5f);
            coins += sign.Wages!.Remove(Inventory.Coins, sign.Wages[Inventory.Coins]) + sign.Output!.Remove(Inventory.Coins, sign.Output[Inventory.Coins]);
        }
        return coins;
    }

    /// <summary>Units one worker gathers into a chest beside the resource in a day, averaged over <paramref name="days"/>.</summary>
    public static float Gather(GameData data, ThingKind kind, int days = 1) {
        var world = WorkDay(data);
        var source = world.Add(kind, new Vector2(20, 20), kind == ThingKind.Berries ? 24 : 9999);
        var chest = world.Add(ThingKind.Chest, new Vector2(22.5f, 20));
        // Workers take plain iron tools from a station beside the chest, so the measure stays the baseline output.
        var station = world.Add(ThingKind.Toolstation, new Vector2(22.5f, 22));
        station.Inventory.Add("steel_axe", 1); station.Inventory.Add("steel_pick", 1);
        Hire(world, source);
        Work(world, days * data.DaySeconds);
        return chest.Inventory[World.ResourceItem(source)] / (float)days;
    }

    /// <summary>Units one worker makes in a day at a station between a well-stocked supply chest and an output chest.</summary>
    public static float Station(GameData data, string recipe) {
        var world = WorkDay(data);
        var r = data.Recipes[recipe];
        var station = world.Add(r.Station!.Value, new Vector2(20, 20));
        world.SetRecipe(station, recipe);
        foreach (string tool in World.SlotKinds(station.Kind)) station.Tools!.Add(tool switch { "chisel" => "stone_chisel", "hammer" => "steel_hammer", _ => tool }, 1);
        var supply = world.Add(ThingKind.Chest, new Vector2(23, 20));
        supply.Inventory.Slots = 60;
        foreach (var item in BatchCost(world, recipe).Keys) { supply.Inventory.Add(item, 600); supply.Inventory.OutputFilter.Add(item); }
        var store = world.Add(ThingKind.Chest, new Vector2(20, 23));
        // Room for a whole day's tools, which take a slot each.
        store.Inventory.Slots = 60; store.Inventory.InputFilter.Add(r.Output);
        Hire(world, station);
        Work(world, data.DaySeconds);
        return store.Inventory[r.Output] + station.Inventory[r.Output];
    }
}
