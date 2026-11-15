using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static WorldSteps;
using static EconomySteps;

/// <summary>
/// Checks the economy rules in docs/ECONOMY.md against the game tables.
/// Worker output is measured by running real workers through a whole day, so the numbers follow walking, hauling, lunch and the commute.
/// </summary>
public static class EconomyChecks {
    /// <summary>Days a hire is expected to last, which spreads the up-front payment into a daily cost.</summary>
    private const float HireDays = 10;

    public static void Run(GameData data) {
        var chain = new ChainCost(data);
        CheckEveryCrateSellsForMoreThanItsWholeChainCosts(data, chain);
        CheckHiredWorkersEarnMoreThanTheirPay(data, chain);
        CheckBasketIsSustainable(data);
    }

    /// <summary>A worker-day's cost: the wage plus the up-front payment spread over the days a hire lasts, averaged over the offers.</summary>
    private static float DayCost(GameData data) => data.HireOffers.Average(o => o.Wage + o.Upfront / HireDays);

    /// <summary>
    /// A crate of every good the village takes sells for more than its whole chain costs: the worker-days of every step from the ground up at a worker-day's cost,
    /// the coins spent buying what a harvest uses up, and the crate itself.
    /// Intermediate goods have no price of their own; only the finished good is judged.
    /// </summary>
    private static void CheckEveryCrateSellsForMoreThanItsWholeChainCosts(GameData data, ChainCost chain) {
        float day = DayCost(data);
        int units = data.CrateCapacity;
        float crateDays = chain.WorkerDays("crate");
        GD.Print($"Economy: a crate of {units}, by the whole chain at {day:0.0} coins a worker-day; an empty crate takes {crateDays:0.00} worker-days");
        GD.Print($"Economy: {"good",-16} {"worker-days",11} {"labor",7} {"bought",7} {"cost",7} {"sells",7} {"profit",7} {"waits",6}");
        foreach (string item in data.Market.Sells.Keys.Where(chain.Measurable)) {
            float days = units * chain.WorkerDays(item) + crateDays, bought = units * chain.Coins(item), cost = days * day + bought;
            float sells = units * data.Items[item].Price + data.Items["crate"].Price;
            GD.Print($"Economy: {item,-16} {days,11:0.00} {days * day,7:0} {bought,7:0.0} {cost,7:0} {sells,7:0} {sells - cost,7:0} {chain.WaitDays(item),5:0}d");
            Require(sells > cost, $"a crate of {item} should sell for more than its whole chain costs: {sells:0} coins against {cost:0}");
        }
        GD.Print($"Economy: not costed, made from meat the player hunts at night: {string.Join(", ", data.Market.Sells.Keys.Where(id => !chain.Measurable(id)))}");
    }

    /// <summary>
    /// A berry picker barely covers their pay, steel tool crews earn several times theirs and cooks a good part more, and longer chains earn more per worker-day.
    /// Food chains are compared among themselves, apart from the tools.
    /// </summary>
    private static void CheckHiredWorkersEarnMoreThanTheirPay(GameData data, ChainCost chain) {
        float cost = DayCost(data);
        var earnings = data.Market.Sells.Keys.Where(chain.Measurable)
            .Select(id => (Item: id, Tool: data.Items[id].Tool != "", Depth: Depth(data, id), PerDay: data.Items[id].Price / chain.WorkerDays(id))).OrderBy(e => e.Depth).ToList();
        GD.Print($"Economy: a worker-day costs {cost:0.0} coins; gathered per day {string.Join(", ", chain.Gathered.Select(p => $"{p.Key} {p.Value:0}"))}; made per day {string.Join(", ", chain.MadePerDay.Select(p => $"{p.Key} {p.Value:0}"))}");
        foreach (var e in earnings) GD.Print($"Economy: {e.Item,-16} {chain.WorkerDays(e.Item):0.000} worker-days each, {e.PerDay:0} coins per worker-day ({e.PerDay / cost:0.0}x pay)");
        float berries = earnings.Single(e => e.Item == "berries").PerDay;
        Require(berries >= .9f * cost && berries <= 1.25f * cost, $"a hired berry picker should barely break even: {berries:0.0} coins a day against {cost:0.0}");
        Require(earnings.Where(e => e.Tool).All(e => e.PerDay >= 4 * cost), "a worker in a steel tool chain should earn at least four times their pay");
        Require(earnings.Where(e => !e.Tool && e.Depth > 0).All(e => e.PerDay >= 1.5f * cost), "a cook should earn at least half as much again as their pay");
        foreach (var group in earnings.GroupBy(e => e.Tool)) {
            var ladder = group.ToList();
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
        var sign = world.Add(ThingKind.Signpost, new Vector2(20, 20));
        world.Player = sign.Pos;
        var takings = new List<int>();
        for (int day = 1; day <= 4; day++) {
            Work(world, (day - 1) * data.DaySeconds + 60 - world.Time);
            takings.Add(Sell(world, sign, sells.ToDictionary(p => p.Key, p => p.Value.Daily)));
        }
        GD.Print($"Economy: a day's basket sells for {string.Join(", ", takings)} coins on four days running");
        // Sales round down to whole coins, so the float recovery can cost one coin.
        Require(takings.All(t => Math.Abs(t - takings[0]) <= 1), "a day's basket should sell for the same every day");

        foreach (var (item, demand) in sells.Where(p => p.Value.Steady > 0)) {
            var fresh = new World(data, false);
            var post = fresh.Add(ThingKind.Signpost, new Vector2(20, 20));
            fresh.Player = post.Pos;
            int crates = (demand.Steady + data.CrateCapacity - 1) / data.CrateCapacity;
            int full = (int)MathF.Floor(demand.Steady * data.Items[item].Price + crates * data.Items["crate"].Price);
            Require(Sell(fresh, post, new() { [item] = demand.Steady }) == full && fresh.UnitPrice(item, false) == data.Items[item].Price,
                    $"the steady amount of {item} should sell at the full price");
            Sell(fresh, post, new() { [item] = data.CrateCapacity });
            Require(fresh.UnitPrice(item, false) < data.Items[item].Price, $"selling {item} past its steady amount should lower the price");
        }
    }

}
