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
    public static void Run(GameData data) {
        var chain = new ChainCost(data);
        CheckEveryCrateSellsForMoreThanItsWholeChainCosts(data, chain);
        CheckHiredWorkersEarnMoreThanTheirPay(data, chain);
        CheckBasketIsSustainable(data);
    }

    /// <summary>A worker-day's cost: the lowest daily wage among the hire offers, leaving out what is paid up front.</summary>
    private static float DayCost(GameData data) => data.HireOffers.Min(o => o.Wage);

    /// <summary>
    /// A crate of every good the village takes sells for more than its whole chain costs: the worker-days of every step from the ground up at a worker-day's cost,
    /// the coins spent buying what a harvest uses up, and the crate itself and its packing.
    /// Intermediate goods have no price of their own; only the finished good is judged.
    /// Days for one worker add the days the chain waits on a crop to the crate's worker-days.
    /// </summary>
    private static void CheckEveryCrateSellsForMoreThanItsWholeChainCosts(GameData data, ChainCost chain) {
        float day = DayCost(data);
        int units = data.CrateCapacity;
        GD.Print($"Economy: a crate of {units}, by the whole chain at {day:0.0} coins a worker-day; an empty crate takes {chain.WorkerDays("crate"):0.00} worker-days");
        GD.Print($"Economy: {"good",-16} {"worker-days",11} {"labor",7} {"bought",7} {"cost",7} {"sells",7} {"profit",7} {"waits",6} {"one worker",10}");
        foreach (string item in data.Market.Sells.Keys.Where(chain.Measurable)) {
            float days = chain.CrateWorkerDays(item), bought = units * chain.Coins(item), cost = days * day + bought, sells = CrateSells(data, item), wait = chain.WaitDays(item);
            GD.Print($"Economy: {item,-16} {days,11:0.00} {days * day,7:0} {bought,7:0.0} {cost,7:0} {sells,7:0} {sells - cost,7:0} {wait,5:0}d {wait + days,9:0.0}d");
            Require(sells > cost, $"a crate of {item} should sell for more than its whole chain costs: {sells:0} coins against {cost:0}");
        }
        GD.Print($"Economy: not costed, made from meat the player hunts at night: {string.Join(", ", data.Market.Sells.Keys.Where(id => !chain.Measurable(id)))}");
    }

    /// <summary>
    /// A berry picker barely covers their pay, steel tool crews earn several times theirs and cooks a good part more, and longer chains earn more per worker-day.
    /// Each good is judged by a full crate of it, making and packing the crate included.
    /// Food chains are compared among themselves, apart from the tools.
    /// </summary>
    private static void CheckHiredWorkersEarnMoreThanTheirPay(GameData data, ChainCost chain) {
        float cost = DayCost(data);
        var earnings = data.Market.Sells.Keys.Where(chain.Measurable)
            .Select(id => (Item: id, Tool: data.Items[id].Tool != "", Depth: Depth(data, id), PerDay: CrateSells(data, id) / chain.CrateWorkerDays(id))).OrderBy(e => e.Depth).ToList();
        static string PerDay(IEnumerable<KeyValuePair<string, float>> rates) => string.Join(", ", rates.Select(p => $"{p.Key} {p.Value:0}"));
        GD.Print($"Economy: a worker-day costs {cost:0.0} coins; gathered per day {PerDay(chain.Gathered)}; made per day {PerDay(chain.MadePerDay)}; crates packed per day {PerDay(chain.PackedPerDay)}");
        foreach (var e in earnings) GD.Print($"Economy: {e.Item,-16} {chain.CrateWorkerDays(e.Item):0.00} worker-days a crate, {e.PerDay:0} coins per worker-day ({e.PerDay / cost:0.00}x pay)");
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
