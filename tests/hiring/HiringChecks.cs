using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of hiring at the trading post: what an offer costs and pays.</summary>
public static class HiringChecks {
    public static void Run(GameData data) {
        Test(CheckHiringOfferPaysUpFrontFromTheBankFirst, data);
    }

    /// <summary>
    /// A hiring offer brings a worker on its wage for its payment up front, paid from the bank first and the backpack for
    /// the rest; a smaller payment up front comes with a higher wage.
    /// </summary>
    private static void CheckHiringOfferPaysUpFrontFromTheBankFirst(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        var cheap = data.HireOffers.OrderBy(o => o.Upfront).First(); var dear = data.HireOffers.OrderBy(o => o.Upfront).Last();
        Require(cheap.Wage > dear.Wage, "a smaller payment up front must come with a higher wage");
        SetCoins(world, (int)dear.Upfront);
        var worker = world.Hire(sign, dear);
        Require(worker != null, "a hiring offer should bring a worker");
        Require(worker!.Wage == dear.Wage, "a hiring offer should fix the worker's wage");
        Require(world.Coins == 0, "a hiring offer should charge its payment up front");
        SetCoins(world, (int)dear.Upfront + 5);
        world.Transfer(world.Backpack, sign.Wages!, Inventory.Coins, (int)dear.Upfront - 2);
        Require(world.Hire(sign, dear) != null && sign.Wages![Inventory.Coins] == 0 && world.Coins == 5,
            "hiring should be paid from the bank first and the backpack for the rest");
    }
}
