using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;

/// <summary>
/// Checks the economy rules in docs/ECONOMY.md against data/game.json.
/// Worker output is measured by running real workers through a whole day, so the numbers follow walking, hauling, lunch and the commute.
/// </summary>
public static class EconomyChecks {
    private const float Step = .25f;
    /// <summary>Days a hire is expected to last, which spreads the up-front payment into a daily cost.</summary>
    private const float HireDays = 10;

    public static void Run(GameData data) {
        CheckValueLadder(data);
        CheckHiringPays(data);
        CheckBasketIsSustainable(data);
    }

    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException($"Economy check failed: {message}");
    }

    /// <summary>What one batch of a recipe takes, with the cheapest fuel its station burns.</summary>
    private static Dictionary<string, int> BatchCost(World world, string recipe) =>
        world.BatchCost(new Thing { Kind = world.Data.Recipes[recipe].Station, Recipe = recipe, Fuel = "wood" });

    private static float Value(GameData data, Dictionary<string, int> goods) => goods.Sum(p => data.Items[p.Key].Price * p.Value);

    /// <summary>Recipe steps from gathered materials: raw goods are 0, and a recipe is one more than its deepest input.</summary>
    private static int Depth(GameData data, string item) =>
        data.Recipes.Values.FirstOrDefault(r => r.Output == item) is RecipeDef r ? 1 + r.Inputs.Keys.Max(input => Depth(data, input)) : 0;

    /// <summary>Every recipe adds value at reference prices, and the markup shrinks the deeper the recipe sits in its chain.</summary>
    private static void CheckValueLadder(GameData data) {
        var world = new World(data, false);
        var markups = data.Recipes.Select(p => {
            float input = Value(data, BatchCost(world, p.Key)), output = data.Items[p.Value.Output].Price * p.Value.Amount;
            return (Recipe: p.Key, Depth: Depth(data, p.Value.Output), Markup: output / input - 1);
        }).ToList();
        foreach (var m in markups.OrderBy(m => m.Depth)) GD.Print($"Economy: {m.Recipe,-13} step {m.Depth} markup {m.Markup:P0}");
        Require(markups.All(m => m.Markup > 0), $"every recipe should add value: {string.Join(", ", markups.Where(m => m.Markup <= 0).Select(m => m.Recipe))}");
        foreach (var low in markups)
            foreach (var high in markups.Where(m => m.Depth > low.Depth))
                Require(high.Markup <= low.Markup + .005f, $"{high.Recipe} (step {high.Depth}) should not mark up more than {low.Recipe} (step {low.Depth})");
    }

    /// <summary>
    /// A berry picker barely covers their pay, steel tool crews earn several times theirs and cooks a good part more, and longer chains earn more per worker-day.
    /// Food chains are compared among themselves, apart from the tools.
    /// </summary>
    private static void CheckHiringPays(GameData data) {
        var gathered = new[] { ("iron_ore", "iron"), ("coal", "coal"), ("tin_ore", "tin"), ("wood", "tree"), ("berries", "berries") }.ToDictionary(p => p.Item1, p => Gather(data, p.Item2, p.Item2 == "berries" ? 2 : 1));
        var made = new Dictionary<string, float>();
        float Made(string recipe) => made.TryGetValue(recipe, out float perDay) ? perDay : made[recipe] = Station(data, recipe);
        var world = new World(data, false);
        string RecipeFor(string item) => data.Recipes.First(p => p.Value.Output == item).Key;
        // Meat comes from wolves at night and wheat from farms, so no hired gatherer or station crew alone accounts for them.
        bool Measurable(string item) => gathered.ContainsKey(item) || data.Recipes.Values.Any(r => r.Output == item) && BatchCost(world, RecipeFor(item)).Keys.All(Measurable);
        // Worker-days per unit: the materials' share plus the recipe's own station time, one over the station's daily output.
        float Labor(string item) {
            if (gathered.TryGetValue(item, out float perDay)) return 1 / perDay;
            string key = RecipeFor(item);
            return BatchCost(world, key).Sum(p => Labor(p.Key) * p.Value) / data.Recipes[key].Amount + 1 / Made(key);
        }
        float cost = data.HireOffers.Average(o => o.Wage + o.Upfront / HireDays);
        GD.Print($"Economy: not measured, made from goods no hire gathers: {string.Join(", ", data.Market.Sells.Keys.Where(id => !Measurable(id)))}");
        var earnings = data.Market.Sells.Keys.Where(Measurable)
            .Select(id => (Item: id, Tool: data.Items[id].Tool != "", Depth: Depth(data, id), PerDay: data.Items[id].Price / Labor(id))).OrderBy(e => e.Depth).ToList();
        GD.Print($"Economy: a worker-day costs {cost:0.0} coins; gathered per day {string.Join(", ", gathered.Select(p => $"{p.Key} {p.Value:0}"))}; made per day {string.Join(", ", made.Select(p => $"{p.Key} {p.Value:0}"))}");
        foreach (var e in earnings) GD.Print($"Economy: {e.Item,-16} {Labor(e.Item):0.000} worker-days each, {e.PerDay:0} coins per worker-day ({e.PerDay / cost:0.0}x pay)");
        float berries = earnings.Single(e => e.Item == "berries").PerDay;
        Require(berries >= .9f * cost && berries <= 1.25f * cost, $"a hired berry picker should barely break even: {berries:0.0} coins a day against {cost:0.0}");
        Require(earnings.Where(e => e.Tool).All(e => e.PerDay >= 4 * cost), "a worker in a steel tool chain should earn at least four times their pay");
        Require(earnings.Where(e => !e.Tool && e.Depth > 0).All(e => e.PerDay >= 1.5f * cost), "a cook should earn at least half as much again as their pay");
        foreach (var chain in earnings.GroupBy(e => e.Tool)) {
            var ladder = chain.ToList();
            for (int i = 1; i < ladder.Count; i++)
                Require(ladder[i].Depth == ladder[i - 1].Depth || ladder[i].PerDay > ladder[i - 1].PerDay, $"{ladder[i].Item} has a longer chain and should earn more per worker-day than {ladder[i - 1].Item}");
        }
    }

    /// <summary>Selling each good's daily amount every day keeps prices steady, and staple food holds its price better than tools.</summary>
    private static void CheckBasketIsSustainable(GameData data) {
        var sells = data.Market.Sells;
        Require(sells.Where(p => data.IsFood(p.Key)).All(food => sells.Where(p => data.Items[p.Key].Tool != "").All(tool => food.Value.Drop < tool.Value.Drop)),
                "food demand should be steadier than tool demand");
        var world = new World(data, false);
        var sign = world.Add("signpost", new Vector2(20, 20));
        world.Player = sign.Pos;
        var takings = new List<int>();
        for (int day = 1; day <= 4; day++) {
            Pass(world, (day - 1) * data.DaySeconds + 60 - world.S.Time);
            takings.Add(Sell(world, sign, sells.ToDictionary(p => p.Key, p => p.Value.Daily)));
        }
        GD.Print($"Economy: a day's basket sells for {string.Join(", ", takings)} coins on four days running");
        // Sales round down to whole coins, so the float recovery can cost one coin.
        Require(takings.All(t => Math.Abs(t - takings[0]) <= 1), "a day's basket should sell for the same every day");

        foreach (var (item, demand) in sells.Where(p => p.Value.Steady > 0)) {
            var fresh = new World(data, false);
            var post = fresh.Add("signpost", new Vector2(20, 20));
            fresh.Player = post.Pos;
            int crates = (demand.Steady + data.CrateCapacity - 1) / data.CrateCapacity;
            int full = (int)MathF.Floor(demand.Steady * data.Items[item].Price + crates * data.Items["crate"].Price);
            Require(Sell(fresh, post, new() { [item] = demand.Steady }) == full && fresh.UnitPrice(item, false) == data.Items[item].Price,
                    $"the steady amount of {item} should sell at the full price");
            Sell(fresh, post, new() { [item] = data.CrateCapacity });
            Require(fresh.UnitPrice(item, false) < data.Items[item].Price, $"selling {item} past its steady amount should lower the price");
        }
    }

    /// <summary>Packs the goods into full crates by the signpost, sells them cart by cart and returns the coins.</summary>
    private static int Sell(World world, Thing sign, Dictionary<string, int> goods) {
        int capacity = world.Data.CrateCapacity, coins = 0;
        var crates = goods.SelectMany(p => Enumerable.Range(0, (p.Value + capacity - 1) / capacity).Select(i => (p.Key, Math.Min(capacity, p.Value - i * capacity)))).ToList();
        foreach (var load in crates.Chunk(World.MarketSlots)) {
            foreach (var (item, count) in load) {
                var crate = world.Add("crate", sign.Pos + new Vector2(1, 0));
                crate.Inventory.Add(item, count);
                var before = world.S.Backpack.Items.Keys.ToHashSet();
                Require(world.PickUpCrate(crate), "a crate by the signpost should be picked up");
                world.S.Backpack.TransferTo(sign.Inventory, world.S.Backpack.Items.Keys.Single(id => !before.Contains(id)), 1);
            }
            Require(world.Sell(sign), "the village should take a day's basket");
            Pass(world, world.Data.TradeSeconds + .5f);
            coins += sign.Wages!.Remove(Inventory.Coins, sign.Wages[Inventory.Coins]) + sign.Output!.Remove(Inventory.Coins, sign.Output[Inventory.Coins]);
        }
        return coins;
    }

    /// <summary>Advances time like sleep, without wolves, keeping the player fed and watered.</summary>
    private static void Pass(World world, float seconds) {
        while (seconds > 0) {
            world.S.Hunger = world.S.Thirst = world.S.Health = 100;
            float dt = Math.Min(Step, seconds); world.Tick(dt, true); seconds -= dt;
        }
    }

    private static World WorkDay(GameData data) {
        var world = new World(data, false);
        world.S.Time = 0;
        world.S.Backpack.Add(Inventory.Coins, 1000);
        return world;
    }

    private static void Hire(World world, Thing target) {
        var w = new Worker { Id = world.S.NextId++, Name = "Measured", Pos = target.Pos + new Vector2(1, 1) };
        world.S.Workers.Add(w); world.Assign(w, target);
    }

    /// <summary>Units one worker gathers into a chest beside the resource in a day, averaged over <paramref name="days"/>.</summary>
    private static float Gather(GameData data, string kind, int days = 1) {
        var world = WorkDay(data);
        var source = world.Add(kind, new Vector2(20, 20), kind == "berries" ? 24 : 9999);
        var chest = world.Add("chest", new Vector2(22.5f, 20));
        // Workers take plain iron tools from a station beside the chest, so the measure stays the baseline output.
        var station = world.Add("toolstation", new Vector2(22.5f, 22));
        station.Inventory.Add("steel_axe", 1); station.Inventory.Add("steel_pick", 1);
        Hire(world, source);
        Pass(world, days * data.DaySeconds);
        return chest.Inventory[World.ResourceItem(source)] / (float)days;
    }

    /// <summary>Units one worker makes in a day at a station between a well-stocked supply chest and an output chest.</summary>
    private static float Station(GameData data, string recipe) {
        var world = WorkDay(data);
        var r = data.Recipes[recipe];
        var station = world.Add(r.Station, new Vector2(20, 20));
        world.SetRecipe(station, recipe);
        foreach (string tool in World.SlotKinds(station.Kind)) station.Tools!.Add(tool switch { "chisel" => "stone_chisel", "hammer" => "steel_hammer", _ => tool }, 1);
        var supply = world.Add("chest", new Vector2(23, 20));
        supply.Inventory.Slots = 60;
        foreach (var item in BatchCost(world, recipe).Keys) { supply.Inventory.Add(item, 600); supply.Inventory.OutputFilter.Add(item); }
        var store = world.Add("chest", new Vector2(20, 23));
        store.Inventory.InputFilter.Add(r.Output);
        Hire(world, station);
        Pass(world, data.DaySeconds);
        return store.Inventory[r.Output] + station.Inventory[r.Output];
    }
}
