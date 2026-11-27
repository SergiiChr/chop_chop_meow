using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task CheckTradingPostSendsOnlyCratesOffOnACart() {
        await CloseWithEsc();
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        Give(Hosted, "wood", 5); Give(Hosted, Inventory.Coins, 100);
        var berries = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(0, 1)); berries.Inventory.Add("berries", 12); Hosted.PickUpCrate(berries);
        string packed = Hosted.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        await Click(view.Screen(sign.Pos, .8f));
        Require(Shows(sign), "Clicking a trading post must select it.");
        Require(Named("sell") != null, "The market must offer a sell button.");
        Require(Named("coins") != null, "The market must offer a coins button.");
        Require(Named("hardhat") != null, "The market must offer a hire button.");
        Require(Named("prices") != null, "The market must offer a prices button.");
        await ShowBackpack();
        await Holding(Key.Shift, () => Click(Cell(2, c => c.TooltipText.StartsWith("Wood ·"))));
        Require(sign.Inventory.Count == 0 && world.Backpack["wood"] == 5, "Loose goods must stay in the backpack: the market only takes crates.");
        await Holding(Key.Shift, () => Click(Cell(2, c => c.TooltipText.StartsWith("Wood crate of 12 Berries"))));
        Require(sign.Inventory[packed] == 1 && world.Backpack[packed] == 0, "Shift-clicking a packed crate must load it into the market input.");
        int played = sounds.Played;
        await Click(Named("sell")!);
        Require(sounds.Played > played, "Sell must play the sound of the goods going.");
        Require(world.Orders.Count == 1 && sign.Inventory.Count == 0, "Sell must send the market input off on a cart.");
        await Click(Named("auto_sell")!);
        Require(sign.AutoSell, "Ticking Send when full must set the post to send the cart on its own.");
        await Click(Named("filters")!); await Click(Named("filter_add_in")!);
        await Click(Named($"pick_{World.CrateOf("berries")}")!);
        Require(sign.Inventory.InputFilter.SequenceEqual(new[] { World.CrateOf("berries") }),
            "The post's filter must offer the crate of berries.");
        Require(Named("filter_add_out") == null,
            "The post's filter must not offer a row of goods that workers do not bring.");
        Require(Named("pick_berries") == null, "The post's filter must not offer loose berries.");
        await Click(Named("filters_close")!);
        int before = world.DayRevenue;
        Require(world.DailyWages == 0 && BankRevenue() == ($"+{before}", Style.Warn), "With no sale today the bank must show today's coins in red, under the wages.");
        Hosted.Tick(world.Data.TradeSeconds);
        // A crate of 12 berries at 0.82 and the crate at 3.15 pay 12 coins.
        await Until(() => BankRevenue() == ($"+{before + 12}", Style.Ready), "the bank showing the sale's 12 coins in green, above no wages", 2);
        RemoveThings(Hosted, t => t == sign); Drop(Hosted, Inventory.Coins, 999);
        await CloseWithEsc();
    }
}
