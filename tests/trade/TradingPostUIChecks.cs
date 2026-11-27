using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunTradingPostUIChecks() {
        await TestClean(CheckTradingPostSendsOnlyCratesOffOnACart);
        await TestClean(CheckPriceSheetBuysGoodsAndFetchNeedsAHiredWorker);
        await TestClean(CheckPriceSheetKeepsItsPlaceWhenFiltersOpen);
    }

    private async Task CheckTradingPostSendsOnlyCratesOffOnACart() {
        await CloseWithEsc();
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        Give(Hosted, "wood", 5); Give(Hosted, Inventory.Coins, 100);
        var berries = Hosted.Add(ThingKind.Crate, world.Player + new Vector2(0, 1)); berries.Inventory.Add("berries", 12); Hosted.PickUpCrate(berries);
        string packed = Hosted.Backpack.Items.Keys.Single(id => id.StartsWith(World.PackedPrefix));
        await Click(view.Screen(sign.Pos, .8f));
        Require(Shows(sign), "Clicking a trading post must select it.");
        Require(Named("sell") is { Text: "Sell goods", Icon: null }, "The market must offer a text-only Sell goods button.");
        Require(Named("coins") != null, "The market must offer a coins button.");
        Require(Named("hardhat") != null, "The market must offer a hire button.");
        Require(Named("prices") == null && Named("sheet_close") == null && ShowsChild(panel, "buy_water"),
            "The price sheet must open with the trading post and have no separate toggle or close button.");
        int woodBefore = world.Backpack["wood"];
        await Holding(Key.Shift, () => Click(BagCell(c => c.TooltipText.StartsWith("Wood ·"))));
        Require(sign.Inventory.Count == 0 && world.Backpack["wood"] == woodBefore,
            $"Loose goods must stay in the backpack: the market only takes crates. Wood: {woodBefore} → {world.Backpack["wood"]}; post: {sign.Inventory.Count}.");
        await Holding(Key.Shift, () => Click(BagCell(c => c.TooltipText.StartsWith("Wood crate of 12 Berries"))));
        Require(sign.Inventory[packed] == 1 && world.Backpack[packed] == 0, "Shift-clicking a packed crate must load it into the market input.");
        int played = sounds.Played;
        await Click(Named("sell")!);
        Require(sounds.Played > played, "Sell must play the sound of the goods going.");
        Require(world.Orders.Count == 1 && sign.Inventory.Count == 0, "Sell must send the market input off on a cart.");
        await Click(Named("auto_sell")!);
        Require(sign.AutoSell, "Ticking Send when full must set the post to send the cart on its own.");
        await Click(Named("filters")!); await Click(Named("filter_add_in")!);
        Require(ShowsChild(panel, "item_list") && ShowsChild(panel, "buy_water"), "Picking a filter must leave the price sheet visible.");
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

    private async Task CheckPriceSheetBuysGoodsAndFetchNeedsAHiredWorker() {
        await CloseWithEsc();
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        Give(Hosted, Inventory.Coins, 100);
        await Click(view.Screen(sign.Pos, .8f));
        var fetch = Named("fetch");
        Require(ShowsChild(panel, "side_board"), "The price sheet must open.");
        Require(panel.OffsetLeft == SidePanel.Left - MarketPanel.SheetWidth, "The price sheet must open beside the panel.");
        Require(fetch != null, "The price sheet must offer a fetch button.");
        Require(fetch.Disabled, "The fetch button must be disabled while no worker is assigned.");
        Require(fetch.TooltipText == "No worker assigned.", "The fetch button must explain that no worker is assigned.");
        Require(Named("buy_wood") == null, "The price sheet must not offer wood, which the village does not sell.");
        Require(Named("buy_berries") == null,
            "The price sheet must not offer berries, which the village does not sell.");
        Require(Named("buy_water") != null, "The price sheet must offer water, which the village sells.");
        await Click(Named("buy_water")!);
        Require(world.Orders.Count == 1 && world.Orders[0].Buying, "Buy on the price sheet must order goods.");
        await Click(Named("hardhat")!);
        Require(ShowsChild(panel, "buy_water"), "The price sheet must stay visible while hiring.");
        Require(OnPage("hire_heading") is Label { Text: "Hire a worker" }, "The Workers tab must head its offers with Hire a worker.");
        Require(Named("hire_60")?.Text == "18 coins/day.\n60 coins downpayment.",
            "A hiring offer must state its daily wage on one line, then its downpayment on the next.");
        var dear = world.Data.HireOffers.OrderBy(o => o.Upfront).Last();
        await Click(Named($"hire_{dear.Upfront:0}")!);
        var hired = Hosted.Workers.LastOrDefault();
        Require(hired != null && hired.Wage == dear.Wage, "A hiring offer must set the new worker's wage.");
        Hosted.Assign(hired!, sign); await ClickOn(sign);
        Require(!Named("fetch")!.Disabled, "With a worker assigned, fresh prices can be fetched.");
        await Click(Named("fetch")!);
        Require(hired!.Away > 0, "Fetching must send the assigned worker to the village.");
        Hosted.Fire(hired); Hosted.Tick(world.Data.TradeSeconds); RemoveThings(Hosted, t => t == sign || t.Kind == ThingKind.Grave); Drop(Hosted, Inventory.Coins, 999);
        await CloseWithEsc();
    }

    private async Task CheckPriceSheetKeepsItsPlaceWhenFiltersOpen() {
        var resolution = GetWindow().ContentScaleSize;
        // At the smallest supported size, the sheet has more prices than fit at once.
        ApplyResolution(StartMenu.Resolutions[0]);
        var sign = Hosted.Add(ThingKind.Signpost, world.Player + new Vector2(2, 0));
        await ClickOn(sign);
        await Click(Named("coins")!);
        var rows = (ScrollContainer)OnPage("price_rows")!;
        await Until(() => rows.GetVScrollBar().Visible, "the price sheet becoming scrollable", 2);
        await Wheel(rows.GetGlobalTransformWithCanvas() * (rows.Size / 2));
        int offset = rows.ScrollVertical;
        Require(offset > 0, "Scrolling the price sheet must move the prices before opening Filters.");
        await Click(Named("filters")!);
        await Until(() => OnPage("price_rows") is ScrollContainer open && open.ScrollVertical == offset,
            "the price sheet retaining its scroll position with Filters open", 2);
        await Click(Named("filters_close")!);
        await Until(() => OnPage("price_rows") is ScrollContainer closed && closed.ScrollVertical == offset,
            "the price sheet retaining its scroll position after closing Filters", 2);
        RemoveThings(Hosted, t => t == sign);
        await CloseWithEsc();
        ApplyResolution(resolution);
    }
}
