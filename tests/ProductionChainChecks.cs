using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static SetupSteps;
using static Checks;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>
/// Checks that workers alone run every end product from raw resource to a sold cart: canned meat stew, bread, steel pickaxes and berries.
/// The fixture builds the stations, stocks the tool rack, plants a ripening farm and keeps one chest of raw meat full, standing in for a meat source the game does not have yet.
/// The player stays in a cabin far away, so nothing is worked, carried, packed or sold by hand.
/// </summary>
public static class ProductionChainChecks {
    private static readonly string[] Products = { "canned_meat_stew", "bread", "steel_pick", "berries" };

    public static void Run(GameData data) {
        CheckPileOfPackedCratesTakesOnlyTheSameGood(data);
        CheckPileOfMixedCratesTakesAnyMixedCrate(data);
        CheckPostsTakeOnlyAskedForCratesAndSendTheCartWhenFull(data);
        CheckWorkersRunEveryChain(data);
    }

    /// <summary>A pile of packed crates takes more crates of the same good, but no crate of another good.</summary>
    private static void CheckPileOfPackedCratesTakesOnlyTheSameGood(GameData data) {
        var world = EmptyWorld(data);
        string crate = PackCrate(world, ("bread", data.CrateCapacity));
        var pile = world.Add(ThingKind.Pile, new Vector2(30, 30));
        pile.Inventory.Add(crate, 1); Drop(world, crate, 1);
        world.Tick(.01f);
        string otherBread = PackCrate(world, ("bread", 2)), berryCrate = PackCrate(world, ("berries", 2));
        Require(world.Holds(pile, otherBread), "a pile of bread crates should hold another bread crate");
        Require(pile.Inventory.Accepts(otherBread),
            "a pile of bread crates should accept another bread crate into its inventory");
        Require(!world.Holds(pile, berryCrate), "a pile of bread crates should not hold a crate of berries");
        Require(!world.Holds(pile, PackCrate(world, ("bread", 1), ("berries", 1))), "a pile of bread crates should not hold a crate of mixed goods");
    }

    /// <summary>A pile set by a crate of mixed goods takes any other crate of mixed goods, and stays set after the crate leaves.</summary>
    private static void CheckPileOfMixedCratesTakesAnyMixedCrate(GameData data) {
        var world = EmptyWorld(data);
        string first = PackCrate(world, ("bread", 2), ("berries", 2));
        var pile = world.Add(ThingKind.Pile, new Vector2(30, 30));
        pile.Inventory.Add(first, 1); Drop(world, first, 1);
        world.Tick(.01f);
        Require(world.PileFilter(pile) == World.MixedCrate && world.FilterName(World.MixedCrate) == "Mixed crate", "a crate of mixed goods should set a pile to mixed crates");
        pile.Inventory.Remove(first, 1);
        world.Tick(.01f);
        string other = PackCrate(world, ("wood", 1), ("stone", 1));
        Require(world.Holds(pile, other) && pile.Inventory.Accepts(other) && pile.Inventory.AllowsWithdrawal(other),
            "an emptied pile of mixed crates should still take and give any other crate of mixed goods");
        Require(!world.Holds(pile, PackCrate(world, ("bread", 2))), "a pile of mixed crates should not hold a crate of one good");
    }

    /// <summary>
    /// A trading post is a delivery target only for the crates its filter asks for, and only when it is closer than other storage asking for them.
    /// Set to sell on its own, it sends the cart once every market slot holds a crate.
    /// </summary>
    private static void CheckPostsTakeOnlyAskedForCratesAndSendTheCartWhenFull(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(20, 20));
        var chest = world.Add(ThingKind.Chest, new Vector2(30, 20));
        world.Player = sign.Pos;
        string crate = PackCrate(world, ("berries", 12));
        Require(world.DepositFor(crate, sign.Pos) == chest, "a post without a filter should not take deliveries");
        world.SetFilter(sign, World.CrateOf("berries"), false, true);
        Require(world.DepositFor(crate, sign.Pos) == sign, "a post asking for crates of berries should take them");
        Require(world.DepositFor("berries", sign.Pos) == chest, "a post should never take loose goods");
        chest.Inventory.InputFilter.Add(World.CrateOf("berries"));
        Require(world.DepositFor(crate, chest.Pos) == chest && world.DepositFor(crate, sign.Pos) == sign, "between storage asking for the same crates, the nearer one should win");

        world.SetAutoSell(sign, true);
        world.Transfer(world.Backpack, sign.Inventory, crate, 1);
        SafeTick(world, 1);
        Require(world.Orders.Count == 0, "the cart should wait while market slots are still free");
        for (int i = 1; i < World.MarketSlots; i++) world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("berries", 12)), 1);
        SafeTick(world, 1);
        Require(world.Orders.Count == 1, "a full post set to sell on its own should send one cart");
        Require(world.Orders[0].Crates == World.MarketSlots, "the cart should carry every crate of the full post");
        Require(sign.Inventory.Count == 0, "the post should be empty once the cart leaves");
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
            resown |= farms.Any(f => f.FarmStage == FarmStage.Growing && f.Progress < world.GrowSeconds - 60);
        }
        GD.Print($"Production chains: sold {string.Join(", ", sold)} in {days / data.DaySeconds:0.0} days with {world.Workers.Count} workers; farm sown again: {resown}");
        string statuses = string.Join("\n", world.Workers.Select(w => $"  {w.Name} at {world.Find(w.Assignment)?.Kind}: {w.Status}"));
        Require(Products.All(sold.Contains), $"workers should sell every product by cart; sold {string.Join(", ", sold)}.\n{statuses}");
        Require(resown, $"the farm should be sown again from its own seeds and compost fertilizer.\n{statuses}");
        Require(world.Workers.All(w => w.WageDebt <= 0), "the fixture pays every wage");
    }
}
