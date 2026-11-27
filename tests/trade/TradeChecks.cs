using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;
using System.Collections.Generic;
using static EconomySteps;

/// <summary>Checks of trade at the trading post: what the village takes and pays for, and the prices a runner fetches.</summary>
public static class TradeChecks {
    public static void Run(GameData data) {
        Test(CheckDonationIsNoSale, data);
        Test(CheckASaveWithAWaterBottleOnTheWayLoads, data);
        Test(CheckAssignedRunnerFetchesCurrentPrices, data);
        Test(CheckCartDeliversAfterFullTrip, data);
        Test(CheckLooseGoodsNeverSellOrSendACart, data);
        Test(CheckSaleLowersDemandAndNewsTravels, data);
        Test(CheckBoughtToolsArriveOneByOneAndSellBackInCrate, data);
        Test(CheckVillageSellsSeedsAndFertilizerByTheCrate, data);
        Test(CheckMarketTakesCratesAndDonations, data);
        Test(CheckCartTakesACrateWhoseFirebugsDiedAsADonation, data);
        Test(CheckRunnerCarriesDeliveriesIntoStorage, data);
        Test(CheckPostsTakeOnlyAskedForCratesAndSendTheCartWhenFull, data);
        Test(CheckBuyingJinglesTheCoinsPaid, data);
        Test(CheckReassigningARunnerCallsOffTheErrand, data);
    }

    /// <summary>A crate of goods the village does not buy goes on the cart as a donation, which is no sale.</summary>
    private static void CheckASaveWithAWaterBottleOnTheWayLoads(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        SetCoins(world, 100);
        Require(world.Buy(sign, Water.Full, 1), "the village should sell a water bottle");
        world.Deserialize(world.Serialize());
        var post = world.Find(sign.Id)!;
        SafeTick(world, data.TradeSeconds + .1f);
        Require(Vessels.Count(post.Output!, Water.Empty, Water.Liquid, Water.Capacity) == 1,
            "a save made while a bought water bottle is on its way should load, and the bottle arrive full");
    }

    private static void CheckDonationIsNoSale(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        // A steel chisel is a steel tool, so only the village not buying it keeps this from counting as selling steel.
        world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("steel_chisel", 1)), 1);
        Require(!world.Had(Inventory.Coins), "the recipe map should show coins as unknown before a payout");
        Require(world.Sell(sign), "a cart should take a crate of goods the village does not buy");
        SafeTick(world, data.TradeSeconds + .1f);
        Require(world.Orders.Count == 0 && !world.Reached("sold"), "a donation should not count as a sale");
        Require(world.Had(Inventory.Coins), "the recipe map should know coins from the payout for the crate itself");
    }

    /// <summary>
    /// A worker assigned to the trading post fetches current prices once asked, walking down the valley and back in a
    /// trade's time; with nobody assigned, prices cannot be fetched.
    /// </summary>
    private static void CheckAssignedRunnerFetchesCurrentPrices(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        SkipClock(world, data.DaySeconds * 2);
        Require(world.PriceAge == 2, "the price sheet should have aged before the errand");
        // Past the skipped dawn, so its wages fall due before the runner is hired.
        SafeTick(world, 90);
        Require(world.FetchBlock(sign) == "No worker assigned." && !world.FetchPrices(sign), "fetching prices needs a worker assigned to the signpost");
        var offer = data.HireOffers.OrderBy(o => o.Upfront).Last();
        SetCoins(world, (int)offer.Upfront);
        var runner = world.Hire(sign, offer);
        Require(runner != null, "a hiring offer should bring a worker");
        world.Assign(runner!, sign);
        Require(world.FetchPrices(sign), "the assigned worker should agree to fetch prices");
        Require(runner!.Away > 0, "the assigned worker should set off for fresh prices");
        Require(world.FetchBlock(sign) != null, "the assigned worker should set off only once");
        SafeTick(world, data.TradeSeconds / 2);
        Require(runner!.Pos.DistanceTo(sign.Pos + Vector2.One + Terrain.Seaward * World.TripDistance) < 1, "the runner should be furthest down the valley halfway through the errand");
        SafeTick(world, data.TradeSeconds / 2 + .1f);
        Require(runner.Away == 0, "the runner should be back from the village");
        Require(world.PriceAge == 0, "the runner should return with current prices");
        Require(Math.Abs(world.KnownPrice("berries") - world.UnitPrice("berries", false)) < .01f, "the runner should return with prices matching the market");
        Require(world.FetchBlock(sign) == null, "the runner's return should let prices be fetched again");
    }

    private static void CheckCartDeliversAfterFullTrip(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        var output = sign.Output!;
        world.Player = sign.Pos;
        SetCoins(world, 100);
        float coinsBeforeBuy = world.Coins;
        Require(!world.Buy(sign, "wood", 1) && world.Orders.Count == 0, "the village should not sell goods missing from its list");
        Require(world.Buy(sign, "water", 1), "market should accept an affordable buy order");
        Require(world.Coins == coinsBeforeBuy - 16, "buy order should reserve coins immediately");
        Require(world.Orders.Count == 1, "buy order should put one cart on the road");
        Require(output.Count == 0, "buy order should leave goods in transit, not in the output");
        SafeTick(world, 29.9f);
        Require(world.Orders.Count == 1 && output.Count == 0,
                "cart must not deliver before the full 30-second trip");
        SafeTick(world, .2f);
        var box = output.Items.Keys.SingleOrDefault(World.IsCrate);
        Require(world.Orders.Count == 0, "cart should finish the order after delivering");
        Require(box == null, "cart should not leave a box for a refillable bottle");
        Require(Vessels.Count(output, Water.Empty, Water.Liquid, Water.Capacity) == 1, "cart should deliver one refillable bottle directly to the market output");
        Require(Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 0, "cart should not put the delivered bottle in the backpack");
    }

    /// <summary>A sale pays when the cart returns and lowers demand, and the price sheet only changes when someone brings news.</summary>
    private static void CheckLooseGoodsNeverSellOrSendACart(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        // The post refuses loose goods by hand, so they are put in directly.
        sign.Inventory.Add("wood", 3);
        Require(!world.Sell(sign), "loose goods must not be sold");
        Require(world.Orders.Count == 0, "loose goods must not send a cart");
        Require(sign.Inventory["wood"] == 3, "loose goods must stay on the post");
    }

    private static void CheckSaleLowersDemandAndNewsTravels(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        var output = sign.Output!;
        world.Player = sign.Pos;
        SetCoins(world, 100);
        // Tools have no steady amount, so even a small sale lowers their price.
        world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("steel_axe", 3)), 1);
        world.Player = sign.Pos;
        float coinsBeforeSale = world.Coins, quotedPrice = world.KnownPrice("steel_axe");
        Require(world.Sell(sign), "market should sell the goods loaded into it");
        Require(sign.Inventory.Count == 0, "sale should remove goods at dispatch");
        Require(world.Coins == coinsBeforeSale, "sale should not pay the backpack before the cart returns");
        Require(sign.Wages![Inventory.Coins] == 0, "sale should not pay the wage slot before the cart returns");
        float loweredDemand = world.Demand("steel_axe");
        Require(loweredDemand < 1 && world.KnownPrice("steel_axe") == quotedPrice, "selling should lower demand while the price sheet still shows old news");
        Require(!world.Sell(sign), "an empty market has nothing to sell");
        SafeTick(world, 30.1f);
        Require(world.Orders.Count == 0, "sale proceeds should arrive after the order is complete");
        Require(sign.Wages![Inventory.Coins] > 0, "sale proceeds should arrive in the wage slot when the cart returns");
        Require(output[Inventory.Coins] == 0, "sale proceeds should not go to the market output");
        Require(world.Coins == coinsBeforeSale, "sale proceeds should not go straight to the player");
        Require(world.KnownPrice("steel_axe") < quotedPrice && world.PriceAge == 0, "the returning carter should bring today's lower price");
        SkipClock(world, data.DaySeconds * 2);
        Require(world.Demand("steel_axe") > loweredDemand && world.PriceAge == 2, "the sheet should age by whole days");
        SafeTick(world, 60);
        Require(world.Demand("steel_axe") > loweredDemand, "market demand should recover");
        Require(world.Demand("steel_axe") < 1, "market demand should still be below normal");
        Require(world.KnownPrice("steel_axe") < world.UnitPrice("steel_axe", false),
            "the price sheet should stay stale while demand recovers");
    }

    private static void CheckBoughtToolsArriveOneByOneAndSellBackInCrate(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        SetCoins(world, 20000);
        int lot = data.CrateCapacity;
        Require(world.Buy(sign, "steel_axe", lot), "market should accept a large affordable purchase of tools");
        SafeTick(world, data.TradeSeconds + .1f);
        // Tools take a slot each, so what the output boxes cannot hold waits in a box beside the post.
        int delivered = sign.Output!["steel_axe"] + world.Things.Where(t => t.Kind == ThingKind.Grave).Sum(t => t.Inventory["steel_axe"]);
        Require(delivered == lot, "every bought tool should arrive");
        Require(sign.Output["steel_axe"] == World.MarketSlots, "tools should fill the post's output first");
        Require(!sign.Output.Items.Keys.Any(World.IsCrate), "tools should not arrive in a crate");
        sign.Output.Remove("steel_axe", World.MarketSlots); RemoveThings(world, t => t.Kind == ThingKind.Grave);
        string crate = PackCrate(world, ("steel_axe", lot));
        world.Player = sign.Pos;
        Require(world.Transfer(world.Backpack, sign.Inventory, crate, 1) == 1 && world.Sell(sign), "market should accept the purchased tools back in a crate");
        SafeTick(world, data.TradeSeconds + .1f);
        int paid = sign.Wages![Inventory.Coins] + sign.Output[Inventory.Coins];
        Require(world.Coins + paid < 20000, "buying tools and selling them straight back must lose coins");
        // 12 axes at 39 coins and the crate at 3.15, rounded down.
        Require(paid == 471, $"a sale should pay every unit of a crate at the pre-sale price: {paid} coins");
    }

    private static void CheckVillageSellsSeedsAndFertilizerByTheCrate(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, Site(6, 6));
        world.Player = sign.Pos;
        SetCoins(world, 100);
        Require(world.Buy(sign, "wheat_seeds", 1) && world.Buy(sign, "fertilizer", 1), "the village should sell wheat seeds and fertilizer");
        SafeTick(world, data.TradeSeconds + .1f);
        var bags = sign.Output!.Items.Keys.Where(World.IsCrate).Select(id => world.Packed(id)!).ToList();
        Require(bags.Any(b => b["wheat_seeds"] == data.Market.Buys["wheat_seeds"]) && bags.Any(b => b["fertilizer"] == data.Market.Buys["fertilizer"]), "seeds and fertilizer should arrive by the crate");
    }

    private static void CheckCartTakesACrateWhoseFirebugsDiedAsADonation(GameData data) {
        var world = EmptyWorld(data);
        var byHand = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        var whenFull = world.Add(ThingKind.Signpost, new Vector2(12, 10));
        world.Player = new Vector2(11, 11);
        float caught = world.Time;
        world.Transfer(world.Backpack, byHand.Inventory, PackCrate(world, (Firebugs.Item, 1)), 1);
        world.Transfer(world.Backpack, whenFull.Inventory, PackCrate(world, (Firebugs.Item, 1)), 1);
        for (int i = 2; i < World.MarketSlots; i++) world.Transfer(world.Backpack, whenFull.Inventory, PackCrate(world, ("berries", 12)), 1);
        world.SetAutoSell(whenFull, true);
        RunUntil(world, caught + 3 * data.DaySeconds + 1);
        world.Player = new Vector2(11, 11);
        Require(byHand.Inventory["crate"] == 1 && whenFull.Inventory["crate"] == 1 && world.Orders.Count == 0,
            "a crate whose firebugs all died should wait in the market as an empty crate");
        Require(world.Sell(byHand) && world.Orders.Single().Value == 0 && byHand.Inventory.Count == 0,
            "a cart should take an empty crate as a donation that pays nothing");
        world.Transfer(world.Backpack, whenFull.Inventory, PackCrate(world, ("berries", 12)), 1);
        SafeTick(world, 1);
        Require(world.Orders.Count == 2 && world.Orders[1].Crates == World.MarketSlots && whenFull.Inventory.Count == 0,
            "a post set to sell on its own should send its empty crate along once every slot holds a crate");
        var said = new List<string>(); world.Notify = said.Add;
        SafeTick(world, data.TradeSeconds + .1f);
        Require(said.Contains("Cart returned with 0 coins. Donated 1 Wood crate."), "the returning cart should say the empty crate was donated");
    }

    private static void CheckMarketTakesCratesAndDonations(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        string chisel = PackCrate(world, ("steel_chisel", 1));
        string empty = PackCrate(world);
        Require(World.CanSell(chisel), "the village should take a crate of goods it does not buy as a donation");
        Require(!World.CanSell(empty), "the village should not take an empty crate");
        Require(!World.CanSell("crate"), "the village should not take a bare crate");
        Require(!World.CanSell("steel_axe"), "the village should not take a loose steel axe");
        string meat = PackCrate(world, ("raw_meat", 6), ("burnt_steak", 6));
        string tools = PackCrate(world, ("steel_hammer", 1), ("steel_pick", 1));
        world.Player = sign.Pos;
        world.Transfer(world.Backpack, sign.Inventory, meat, 1); world.Transfer(world.Backpack, sign.Inventory, tools, 1);
        Require(world.Sell(sign), "a cart should take every packed crate");
        Require(world.Orders.Single().Crates == 2, "a cart should count the crates it takes");
        Require(world.Packed(meat) == null, "a cart should take away the crate of meat it sells");
        Require(world.Orders.Single().Goods["raw_meat"] == 6 && world.Orders.Single().Goods["steel_pick"] == 1, "the sale should pay for what the crates hold");
        // Donated goods ride along for nothing: the crate alone is paid for.
        world.Transfer(world.Backpack, sign.Inventory, chisel, 1);
        Require(world.Sell(sign), "a cart should take a crate of goods the village does not buy");
        Require(world.Orders.Last().Value == 3, "donated goods should pay nothing beyond the crate");
        var said = new List<string>(); world.Notify = said.Add;
        SafeTick(world, data.TradeSeconds + .1f);
        Require(said.Any(m => m.EndsWith("Donated 1 Steel chisel.")), "the returning cart should say what was donated");
        SetCoins(world, 1000);
        Require(world.Buy(sign, "canned_meat_stew", 2), "boxed goods should be bought from the post");
        Require(world.Orders.Last().Crates == 2, "boxed goods should be bought by the crate");
        // A crate of 12 at 12.15 each plus its crate at 3.15 is 148.95; a quarter more, rounded up, is 187.
        Require(world.Coins == 1000 - 2 * 187, "boxed goods should cost the crate price");
        Require(world.LotPrice("canned_meat_stew") > data.Market.Buys["canned_meat_stew"] * world.UnitPrice("canned_meat_stew", false), "a bought crate costs more than its contents would sell for");
        SafeTick(world, data.TradeSeconds + .1f);
        var bought = sign.Output!.Items.Keys.Where(World.IsCrate).ToList();
        Require(bought.Count == 2 && bought.All(id => world.Packed(id)?["canned_meat_stew"] == 12), "each bought crate should arrive packed with one purchase");
        SetNeeds(world, hunger: 30); Give(world, "canned_meat_stew", 1); world.Consume("canned_meat_stew");
        Require(world.Backpack["canned_meat_stew"] == 0 && world.Hunger == 150, "canned meat stew should be eaten from the can");
        Require(world.Had("canned_meat_stew"), "what the cart brings back should count as had");
        Require(world.KnownPrice("raw_meat") > 0 && !world.Priced("wheat_seeds"), "the price sheet only tracks goods the village buys");
        Require(!data.Items.ContainsKey("canned_food") && data.Recipes.Values.All(r => !r.Inputs.ContainsKey("raw_meat") || r.Station != ThingKind.Cannery), "raw meat should sell in crates and never go into a can");
    }

    /// <summary>A trading post's runner carries what the cart delivered, other than coins, to storage.</summary>
    private static void CheckRunnerCarriesDeliveriesIntoStorage(GameData data) {
        var world = EmptyWorld(data);
        var store = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(26, 28)));
        var sign = world.Add(ThingKind.Signpost, new Vector2(24, 26));
        sign.Output!.Add("tin_bar", 3); sign.Output.Add(Inventory.Coins, 20);
        var runner = AddWorker(world, new Vector2(24, 25));
        world.SetDuty(runner, sign, JobKind.Deliveries, true);
        SafeTick(world, 20);
        Require(store.Inventory["tin_bar"] == 3, "a runner should carry deliveries into storage");
        Require(sign.Output["tin_bar"] == 0, "a runner should leave no delivery in the post's output");
        Require(sign.Output[Inventory.Coins] == 20, "a runner should leave the coins in the post's output");
    }

    /// <summary>
    /// A trading post is a delivery target only for the crates its filter asks for, and only when it is closer than other storage asking for them.
    /// Set to sell on its own, it sends the cart once every market slot holds a crate.
    /// </summary>
    private static void CheckPostsTakeOnlyAskedForCratesAndSendTheCartWhenFull(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(20, 20));
        var chest = TakeEverything(world, world.Add(ThingKind.Chest, new Vector2(30, 20)));
        world.Player = sign.Pos;
        string crate = PackCrate(world, ("berries", 12));
        Require(world.DepositFor(crate, sign.Pos) == chest, "a post without a filter should not take deliveries");
        world.SetFilter(sign, World.CrateOf("berries"), false, true);
        Require(world.DepositFor(crate, sign.Pos) == sign, "a post asking for crates of berries should take them");
        Require(world.DepositFor("berries", sign.Pos) == chest, "a post should never take loose goods");
        chest.Inventory.Filter(World.CrateOf("berries"), false, true);
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

    private static void CheckBuyingJinglesTheCoinsPaid(GameData data) {
        var world = EmptyWorld(data);
        var heard = Listen(world);
        var sign = world.Add(ThingKind.Signpost, new Vector2(20, 16));
        world.Player = sign.Pos;
        SetCoins(world, 100);
        heard.Clear(); world.Buy(sign, "water", 1);
        Require(heard.SequenceEqual(new[] { "buy_coins" }), "a buy order should jingle the coins paid");
    }

    /// <summary>A runner reassigned on the way to the village calls the errand off rather than freezing on the road.</summary>
    private static void CheckReassigningARunnerCallsOffTheErrand(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        var runner = AddWorker(world, sign.Pos);
        world.Player = sign.Pos; world.Assign(runner, sign);
        Require(world.FetchPrices(sign), "the assigned worker should set off for prices");
        SafeTick(world, data.TradeSeconds / 4);
        world.Assign(runner, null);
        Require(runner.Away == 0, "reassigning a runner should call off the errand rather than freeze them on the road");
    }
}
