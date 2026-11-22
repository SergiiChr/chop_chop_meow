using ChopChopMeow;
using static Checks;
using static PlayerSteps;
using static WorldSteps;

/// <summary>Steps of the playthrough from a fresh start: selling at the trading post and buying from it.</summary>
internal static class ProgressionSteps {
    /// <summary>Puts full crates of an item on a trading post's cart, sells them and takes the coins once the cart is back.</summary>
    public static void SellCrates(World world, Thing sign, string item, int crates) {
        for (int i = 0; i < crates; i++) LoadCrate(world, sign, item, world.Data.CrateCapacity);
        Require(world.Sell(sign), $"the trading post should send {crates} crates of {item}");
        SafeTick(world, world.Data.TradeSeconds + .2f);
        CollectCoins(world, sign);
    }

    /// <summary>Takes the coins in a trading post's wage and output slots into the backpack.</summary>
    public static void CollectCoins(World world, Thing sign) {
        world.Transfer(sign.Wages!, world.Backpack, Inventory.Coins, sign.Wages![Inventory.Coins]);
        world.Transfer(sign.Output!, world.Backpack, Inventory.Coins, sign.Output![Inventory.Coins]);
    }

    /// <summary>Buys one of an item at a trading post and takes it into the backpack once the cart brings it.</summary>
    public static void BuyOne(World world, Thing sign, string item) {
        Require(world.Coins >= world.LotPrice(item), $"the backpack should hold the {world.LotPrice(item):0.00} coins one {item} costs, not {world.Coins:0.00}");
        Require(world.Buy(sign, item, 1), $"the village should sell a {item}");
        SafeTick(world, world.Data.TradeSeconds + .2f);
        Require(world.Transfer(sign.Output!, world.Backpack, item, 1) == 1, $"the bought {item} should arrive at the trading post");
    }
}
