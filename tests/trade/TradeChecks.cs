using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static PlayerSteps;
using static WorkerSteps;

/// <summary>Checks of trade at the trading post: what the village takes and pays for.</summary>
public static class TradeChecks {
    public static void Run(GameData data) {
        Test(CheckDonationIsNoSale, data);
    }

    /// <summary>A crate of goods the village does not buy goes on the cart as a donation, which is no sale.</summary>
    private static void CheckDonationIsNoSale(GameData data) {
        var world = EmptyWorld(data);
        var sign = world.Add(ThingKind.Signpost, new Vector2(10, 10));
        world.Player = sign.Pos;
        // A steel chisel is a steel tool, so only the village not buying it keeps this from counting as selling steel.
        world.Transfer(world.Backpack, sign.Inventory, PackCrate(world, ("steel_chisel", 1)), 1);
        Require(world.Sell(sign), "a cart should take a crate of goods the village does not buy");
        SafeTick(world, data.TradeSeconds + .1f);
        Require(world.Orders.Count == 0 && !world.Reached("sold"), "a donation should not count as a sale");
    }
}
