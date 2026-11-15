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
    /// <summary>What one batch of a recipe takes, with one of the first fuel it burns, which workers look for first.</summary>
    public static Dictionary<string, int> BatchCost(World world, string recipe) {
        var r = world.Data.Recipes[recipe];
        var cost = new Dictionary<string, int>(r.Inputs);
        if (r.Burns) cost[r.Fuels[0]] = cost.GetValueOrDefault(r.Fuels[0]) + 1;
        return cost;
    }

    /// <summary>What a full crate of a good sells for at the full price, the crate included.</summary>
    public static float CrateSells(GameData data, string item) => data.CrateCapacity * data.Items[item].Price + data.Items["crate"].Price;

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
        // Room for more than a day's gathering, so the chest never caps the measure.
        chest.Inventory.Slots = 60;
        // Workers take plain iron tools from a station beside the chest, so the measure stays the baseline output.
        var station = world.Add(ThingKind.Toolstation, new Vector2(22.5f, 22));
        station.Inventory.Add("steel_axe", 1); station.Inventory.Add("steel_pick", 1);
        Hire(world, source);
        Work(world, days * data.DaySeconds);
        return chest.Inventory[World.ResourceItem(source)] / (float)days;
    }

    /// <summary>Liters of water one worker brings in a day, refilling the empty bottles in a chest beside the lake.</summary>
    public static float WaterPerDay(GameData data) {
        var world = WorkDay(data);
        var shore = Terrain.WaterPoint(Terrain.Spawn);
        var pond = world.Add(ThingKind.Water, shore, 9999);
        var chest = world.Add(ThingKind.Chest, shore + (shore - Terrain.Lake).Normalized() * 2.5f);
        chest.Inventory.Slots = 600; chest.Inventory.Add(Water.Empty, 500); chest.Inventory.OutputFilter.Add(Water.Empty);
        Hire(world, pond);
        Work(world, data.DaySeconds);
        return chest.Inventory[Water.Full] * data.Vessels[0].Capacity;
    }

    /// <summary>Crates of a good one worker packs in a day at a packing station between a supply chest of the good and empty crates, and a chest for full crates.</summary>
    public static float Pack(GameData data, string item) {
        var world = WorkDay(data);
        var packer = world.Add(ThingKind.Packer, new Vector2(20, 20));
        world.SetPacking(packer, item);
        var supply = world.Add(ThingKind.Chest, new Vector2(23, 20));
        // Room for more than a day's packing, since tools take a slot each.
        supply.Inventory.Slots = 900;
        supply.Inventory.Add(item, 600); supply.Inventory.Add("crate", 100);
        supply.Inventory.OutputFilter.Add(item); supply.Inventory.OutputFilter.Add("crate");
        // Full crates go to the store, not back into the supply.
        supply.Inventory.InputFilter.Add(item);
        var store = world.Add(ThingKind.Chest, new Vector2(20, 23));
        store.Inventory.Slots = 200;
        Hire(world, packer);
        Work(world, data.DaySeconds);
        // Units packed, in the store's crates and the one still open on the station.
        int packed = store.Inventory.Items.Keys.Append(packer.Output!.First() ?? "").Sum(crate => world.Packed(crate)?[item] ?? 0);
        return packed / (float)data.CrateCapacity;
    }

    /// <summary>Units one worker makes in a day at a station between a well-stocked supply chest and an output chest; a liquid comes in full vessels.</summary>
    public static float Station(GameData data, string recipe) {
        var world = WorkDay(data);
        var r = data.Recipes[recipe];
        var station = world.Add(r.Station!.Value, new Vector2(20, 20));
        world.SetRecipe(station, recipe);
        foreach (string tool in World.SlotKinds(station.Kind)) station.Tools!.Add(tool switch { "chisel" => "stone_chisel", "hammer" => "steel_hammer", _ => tool }, 1);
        var supply = world.Add(ThingKind.Chest, new Vector2(23, 20));
        supply.Inventory.Slots = 300;
        foreach (var item in BatchCost(world, recipe).Keys) {
            // A liquid comes in full vessels.
            string stocked = data.VesselFor(item)?.Full ?? item;
            supply.Inventory.Add(stocked, stocked == item ? 600 : 200); supply.Inventory.OutputFilter.Add(stocked);
        }
        var store = world.Add(ThingKind.Chest, new Vector2(20, 23));
        // Room for a whole day's tools, which take a slot each.
        store.Inventory.Slots = 60; store.Inventory.InputFilter.Add(r.Output);
        Hire(world, station);
        Work(world, data.DaySeconds);
        return store.Inventory[r.Output] + station.Inventory[r.Output];
    }
}

/// <summary>
/// What a good costs from the ground up, per unit: worker-days, coins spent buying what goes into it, and days its chain waits on something growing.
/// A crate of it adds the empty crate and the packing.
/// Gathering, station work and packing are measured by running real workers through whole days; a farm plot's work and growing time come from its data.
/// Meat comes from wolves the player kills at night, which no worker's day accounts for, so goods made from it are left out.
/// </summary>
internal sealed class ChainCost {
    private readonly GameData data;
    private readonly World world;
    private readonly Dictionary<string, float> made = new();
    private readonly Dictionary<string, float> packed = new();
    /// <summary>Units of each gathered good, and liters of water, one worker brings in a day.</summary>
    public Dictionary<string, float> Gathered { get; }
    /// <summary>The farm plot's crop, whose work and growing time come from <see cref="FarmDef"/>.</summary>
    private string Crop => data.Farm.Yield.Keys.First();

    public ChainCost(GameData data) {
        this.data = data; world = new World(data, false);
        Gathered = new[] { ("iron_ore", ThingKind.Iron), ("coal", ThingKind.Coal), ("tin_ore", ThingKind.Tin), ("wood", ThingKind.Tree), ("berries", ThingKind.Berries) }
            .ToDictionary(p => p.Item1, p => EconomySteps.Gather(data, p.Item2, p.Item2 == ThingKind.Berries ? 2 : 1));
        Gathered[Water.Liquid] = EconomySteps.WaterPerDay(data);
    }

    /// <summary>Units of a recipe's output one worker makes in a day, measured the first time it is asked for.</summary>
    public float Made(string recipe) => made.TryGetValue(recipe, out float perDay) ? perDay : made[recipe] = EconomySteps.Station(data, recipe);
    public IReadOnlyDictionary<string, float> MadePerDay => made;

    /// <summary>Crates of a good one worker packs in a day, measured the first time it is asked for.</summary>
    public float Packed(string item) => packed.TryGetValue(item, out float perDay) ? perDay : packed[item] = EconomySteps.Pack(data, item);
    public IReadOnlyDictionary<string, float> PackedPerDay => packed;

    /// <summary>Worker-days a full crate of a good takes: its contents, making the empty crate and packing it.</summary>
    public float CrateWorkerDays(string item) => data.CrateCapacity * WorkerDays(item) + WorkerDays("crate") + 1 / Packed(item);

    /// <summary>Whether every step of a good's chain is gathered by a hire, made at a station or grown on a farm plot.</summary>
    public bool Measurable(string item) => Gathered.ContainsKey(item) || item == Crop || data.RecipeIdOf(item) is string key && EconomySteps.BatchCost(world, key).Keys.All(Measurable);

    /// <summary>Worker-days per unit: a hire's day over what it gathers, a harvest's work over its crop, or a batch's materials plus one over the station's daily output.</summary>
    public float WorkerDays(string item) {
        if (Gathered.TryGetValue(item, out float perDay)) return 1 / perDay;
        if (item == Crop) return data.Farm.HarvestWorkdays / data.Farm.Yield[Crop];
        string key = data.RecipeIdOf(item)!;
        return EconomySteps.BatchCost(world, key).Sum(p => WorkerDays(p.Key) * p.Value) / data.Recipes[key].Amount + 1 / Made(key);
    }

    /// <summary>Coins spent per unit buying from the village what a harvest uses up, such as fertilizer; seeds a harvest gives back more of cost nothing.</summary>
    public float Coins(string item) {
        if (Gathered.ContainsKey(item)) return 0;
        if (item == Crop)
            return data.Farm.Sow.Sum(p => Math.Max(0, p.Value - data.Farm.Yield.GetValueOrDefault(p.Key)) * world.LotPrice(p.Key) / (float)world.Lot(p.Key)) / data.Farm.Yield[Crop];
        string key = data.RecipeIdOf(item)!;
        return EconomySteps.BatchCost(world, key).Sum(p => Coins(p.Key) * p.Value) / data.Recipes[key].Amount;
    }

    /// <summary>Days a good's chain waits on a crop growing.</summary>
    public float WaitDays(string item) =>
        Gathered.ContainsKey(item) ? 0 : item == Crop ? data.Farm.GrowDays : EconomySteps.BatchCost(world, data.RecipeIdOf(item)!).Keys.Max(WaitDays);
}
