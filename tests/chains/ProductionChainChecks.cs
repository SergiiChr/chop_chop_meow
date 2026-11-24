using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static WorldSteps;

/// <summary>
/// Checks that workers alone run every end product from raw resource to a sold cart: canned meat stew, bread, steel pickaxes and berries.
/// The fixture builds the stations, stocks the tool rack, plants a ripening farm and keeps one chest of raw meat full, standing in for a meat source the game does not have yet.
/// The player stays in a cabin far away, so nothing is worked, carried, packed or sold by hand.
/// </summary>
public static class ProductionChainChecks {
    private static readonly string[] Products = { "canned_meat_stew", "bread", "steel_pick", "berries" };
    public static void Run(GameData data) {
        Test(CheckWorkersRunEveryChain, data);
    }

    private static void CheckWorkersRunEveryChain(GameData data) {
        var world = EmptyWorld(data);
        var site = new ChainSite(world);
        var meat = site.AddSettlement(Products);
        site.AddGatherers();
        site.AddCrateBenches();
        site.AddCannedStewChain();
        var farms = site.AddBreadChain();
        site.AddSteelPickChain();
        site.AddPackers(Products);

        var sold = new HashSet<string>();
        bool resown = false;
        var seen = new HashSet<TradeOrder>();
        float days = 0, limit = 6 * data.DaySeconds;
        for (; days < limit && !(Products.All(sold.Contains) && resown); days += 5) {
            meat.Inventory.Add("raw_meat", 64 - meat.Inventory["raw_meat"]);
            SafeTick(world, 5);
            foreach (var order in world.Orders) if (!order.Buying && seen.Add(order)) sold.UnionWith(order.Goods.Keys);
            resown |= farms.Any(f => f.FarmStage == FarmStage.Growing && f.Progress < world.Farms.GrowSeconds - 60);
        }
        GD.Print($"Production chains: sold {string.Join(", ", sold)} in {days / data.DaySeconds:0.0} days with {world.Workers.Count} workers; farm sown again: {resown}");
        string statuses = string.Join("\n", world.Workers.Select(w => $"  {w.Name} at {world.Find(w.Assignment)?.Kind}: {w.Status}"));
        Require(Products.All(sold.Contains), $"workers should sell every product by cart; sold {string.Join(", ", sold)}.\n{statuses}");
        Require(resown, $"the farm should be sown again from its own seeds and compost fertilizer.\n{statuses}");
        Require(world.Workers.All(w => w.WageDebt <= 0), "the fixture pays every wage");
    }
}
