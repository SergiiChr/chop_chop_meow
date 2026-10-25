using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Signpost panel: the market, hiring, and a price sheet that opens to the left of the panel.</summary>
public partial class Game {
    private const int SheetWidth = 316;
    private string signpostTab = "market";
    private bool sheetOpen;

    private void Signpost(Thing sign) {
        var tabs = Row();
        tabs.AddChild(TabIcon("coins", "Market", () => signpostTab = "market", signpostTab == "market"));
        tabs.AddChild(TabIcon("hardhat", "Workers", () => signpostTab = "workers", signpostTab == "workers"));
        tabs.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        tabs.AddChild(TabIcon("prices", "Market prices", () => sheetOpen = !sheetOpen, sheetOpen));
        if (sheetOpen) PriceSheet(sign);
        if (signpostTab == "workers") HireOffers(sign);
        else Market(sign);
    }

    private Button TabIcon(string icon, string tooltip, Action action, bool active) {
        var button = MenuIcon(icon, tooltip, () => { action(); OpenPage("inspect"); });
        button.CustomMinimumSize = new Vector2(48, 48); button.AddThemeConstantOverride("icon_max_width", 32);
        if (active) button.AddThemeStyleboxOverride("normal", Box(new Color("7c8550"), 5, 9));
        return button;
    }

    /// <summary>Unlabeled input and output boxes like a station's, with the nearest cart's trip between them.</summary>
    private void Market(Thing sign) {
        var output = sign.Output!;
        AddInventoryGrid(() => sign.Inventory, sign, id => MoveStack(sign.Inventory, world.S.Backpack, id, sign), "Click: take back · Drag a packed crate here to sell", drag => drag.From == world.S.Backpack && world.CanSell(drag.Item));
        var trip = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10), Name = "cart" };
        content.AddChild(trip);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(trip)) return;
            var next = world.S.Orders.Where(o => o.Signpost == sign.Id).OrderByDescending(o => o.Elapsed).FirstOrDefault();
            trip.Value = next == null ? 0 : next.Elapsed / world.Data.TradeSeconds;
            trip.TooltipText = next == null ? "No carts on the road." : $"Cart back in {Math.Max(0, world.Data.TradeSeconds - next.Elapsed):0}s";
        });
        var wages = sign.Wages!;
        AddLabel("WAGES", 14);
        AddInventoryGrid(() => wages, sign, id => MoveStack(wages, world.S.Backpack, id, sign), "Sale coins land here first and pay wages at dawn\nClick or drag: take · Drop coins here to top it up", drag => drag.Item == Inventory.Coins && drag.From != null && drag.From != wages);
        AddInventoryGrid(() => output, sign, id => MoveStack(output, world.S.Backpack, id, sign), "Click or drag: collect", _ => false);
        var row = Row();
        // Sold goods go on the cart rather than into the backpack, so the sale sounds like its largest stack being loaded.
        var sell = Button("Sell", () => { var load = sign.Inventory.Items.Keys.OrderByDescending(id => sign.Inventory[id]).FirstOrDefault(); if (world.Sell(sign) && load != null) sounds.Play(Sounds.ForItem(load)); }); sell.Name = "sell"; sell.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(sell);
        var collect = Button("Collect", () => { if (world.Near(sign)) foreach (var id in output.Items.Keys.ToArray()) world.Transfer(output, world.S.Backpack, id, output[id]); else Notify("Walk closer first."); });
        collect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(collect);
        AddLabel("BACKPACK", 14);
        AddLabel("The village only takes crates packed with meat, berries or steel tools.", 13, Muted);
        AddInventoryGrid(() => world.S.Backpack, null, id => { if (world.CanSell(id)) MoveStack(world.S.Backpack, sign.Inventory, id, sign); else Notify("Pack meat, berries or steel tools in a crate to sell them."); }, "Click: put a packed crate in the market · Drag onto a box");
    }

    private void HireOffers(Thing sign) {
        AddLabel("HIRE A VILLAGER", 16);
        foreach (var offer in world.Data.HireOffers) {
            var hire = Button($"{offer.Upfront:0} coins now · {offer.Wage:0} a day", () => { if (world.Hire(sign, offer) is Worker w) Select(w); });
            hire.Name = $"hire_{offer.Upfront:0}"; content.AddChild(hire);
        }
        AddLabel("Wages are paid at each dawn from the market's wage slot, then from your backpack. Unpaid workers strike.", 13, Muted);
        Assignment(sign);
    }

    /// <summary>Prices as last heard from the village, beside the panel; buying is always at the current price.</summary>
    private void PriceSheet(Thing sign) {
        panel.OffsetLeft = PanelLeft - SheetWidth; sheet.Visible = true;
        sheet.AddChild(Label("MARKET PRICES", 20));
        var age = Label("", 13, Muted); sheet.AddChild(age);
        live.Add(() => { if (GodotObject.IsInstanceValid(age)) age.Text = world.PriceAge switch { 0 => "News from today", 1 => "News from yesterday", int n => $"News from {n} days ago" }; });
        AddSheetNote("Sell prices are per item, sold in crates. Tools are bought one by one; other goods come in crates.");
        var header = new HBoxContainer(); sheet.AddChild(header);
        header.AddChild(new Label { Text = "", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        foreach (string column in new[] { "sell", "buy" }) header.AddChild(PriceCell(column, Muted));
        header.AddChild(new Control { CustomMinimumSize = new Vector2(52, 0) });
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", 3);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(rows); sheet.AddChild(scroll);
        foreach (string id in world.Data.Market.Sells.Keys.Union(world.Data.Market.Buys.Keys)) {
            bool sells = world.Data.Market.Sells.ContainsKey(id), buys = world.Data.Market.Buys.ContainsKey(id);
            var row = new HBoxContainer(); rows.AddChild(row);
            row.AddChild(new TextureRect { Texture = HotbarIcon(id), CustomMinimumSize = new Vector2(26, 26), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var name = Label(world.Lot(id) > 1 ? $"{world.Lot(id)} {world.Data.Name(id)}" : world.Data.Name(id), 14); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.AutowrapMode = TextServer.AutowrapMode.Off; name.ClipText = true; row.AddChild(name);
            // Goods the village does not take show no sell price, and goods it does not sell no buy price.
            var sellPrice = PriceCell("", Cream); row.AddChild(sellPrice); row.AddChild(PriceCell(buys ? $"{world.LotPrice(id)}" : "", Cream));
            if (sells) live.Add(() => { if (GodotObject.IsInstanceValid(sellPrice)) sellPrice.Text = $"{world.KnownPrice(id):0.0}"; });
            if (!buys) { row.AddChild(new Control { CustomMinimumSize = new Vector2(52, 28) }); continue; }
            var buy = Button("Buy", () => world.Buy(sign, id, Input.IsKeyPressed(Key.Shift) ? 5 : 1));
            buy.Name = $"buy_{id}"; buy.CustomMinimumSize = new Vector2(52, 28); row.AddChild(buy);
            buy.TooltipText = world.Lot(id) > 1 ? $"Buy a crate of {world.Lot(id)} · Shift: 5 crates\nCrates arrive in the market output." : "Buy 1 · Shift: 5\nGoods arrive in the market output.";
        }
        var fetch = Button("Send for fresh prices", () => world.FetchPrices(sign)); fetch.Name = "fetch"; sheet.AddChild(fetch);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(fetch)) return;
            string? blocked = world.FetchBlock(sign);
            fetch.Disabled = blocked != null; fetch.TooltipText = blocked ?? "The worker assigned here walks to Bramblewick and back with fresh prices.";
        });
    }

    private void AddSheetNote(string text) => sheet.AddChild(Label(text, 13, Muted));

    private Label PriceCell(string text, Color color) {
        var cell = Label(text, 14, color); cell.CustomMinimumSize = new Vector2(44, 0); cell.HorizontalAlignment = HorizontalAlignment.Right; cell.AutowrapMode = TextServer.AutowrapMode.Off;
        return cell;
    }
}
