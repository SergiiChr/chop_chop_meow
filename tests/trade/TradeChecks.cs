using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Checks of trade at the trading post: what the village takes and pays for, and the prices a runner fetches.</summary>
public static class TradeChecks {
    public static void Run(GameData data) {
        Test(CheckDonationIsNoSale, data);
        Test(CheckCoinsAlreadyHeldAreKnownOnLoad, data);
        Test(CheckAssignedRunnerFetchesCurrentPrices, data);
    }

    /// <summary>A crate of goods the village does not buy goes on the cart as a donation, which is no sale.</summary>
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

    private static void CheckCoinsAlreadyHeldAreKnownOnLoad(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, world.Player);
        SetCoins(world, 3);
        Require(world.Transfer(world.Backpack, sign.Wages!, Inventory.Coins, 3) == 3, "the wage slot should hold the coins");
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        Require(loaded.Had(Inventory.Coins), "the recipe map should know coins already held in the settlement's wage slot");
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
}
