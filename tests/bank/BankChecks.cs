using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of the trading post's bank: its wage slot holds coins with no limit and pays wages first.</summary>
public static class BankChecks {
    public static void Run(GameData data) {
        Test(CheckWageSlotTakesOnlyCoinsAndPaysWagesFirst, data);
        Test(CheckBankKeepsCoinsPastAThousandAcrossASave, data);
    }

    private static void CheckWageSlotTakesOnlyCoinsAndPaysWagesFirst(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        sign.Wages!.Add(Inventory.Coins, 4995);
        PlaceOrder(world, new TradeOrder { Goods = new() { ["wood"] = 1 }, Buying = false, Value = 12, Signpost = sign.Id });
        SafeTick(world, data.TradeSeconds + .1f);
        Require(sign.Wages[Inventory.Coins] == 5007 && sign.Output![Inventory.Coins] == 0, "the wage slot should have no limit: every sale coin lands there, past a thousand too");
        sign.Output.Add(Inventory.Coins, 7);
        Give(world, "wood", 1);
        Require(world.Transfer(world.Backpack, sign.Wages, "wood", 1) == 0 && world.Backpack["wood"] == 1, "the wage slot should refuse anything but coins, even by hand");

        var worker = AddWorker(world, sign.Pos);
        sign.Wages.Remove(Inventory.Coins, 5004);
        SetCoins(world, 10);
        world.PayWages();
        Require(worker.WageDebt == 0, "a worker's wage should be paid in full");
        Require(sign.Wages[Inventory.Coins] == 0, "wages should come out of the wage slot first");
        Require(world.Coins == 10 - (worker.Wage - 3), "wages should come out of the backpack after");
        SetCoins(world, 0);
        world.PayWages();
        Require(worker.WageDebt == worker.Wage && sign.Output![Inventory.Coins] == 7, "wages should not be taken from the market output");
    }

    /// <summary>A bank holding more than a stack of coins keeps them in its one cell through a save and still takes more.</summary>
    private static void CheckBankKeepsCoinsPastAThousandAcrossASave(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        sign.Wages!.Add(Inventory.Coins, 5000);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var bank = loaded.Things.Single(t => t.Kind == ThingKind.Signpost).Wages!;
        Require(bank[Inventory.Coins] == 5000 && bank.UsedSlots == 1, $"a loaded bank should keep its 5000 coins in one cell: {bank.UsedSlots} cells");
        Require(bank.Add(Inventory.Coins, 12) == 12, "a loaded bank should still take sale coins");
    }
}
