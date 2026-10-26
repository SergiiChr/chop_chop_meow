using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Signpost panel: outgoing crates, the cart's road and the mailbox, hiring, and a chalkboard of prices that opens to the left of the panel.</summary>
public partial class Game {
    private const int SheetWidth = 372;
    private string signpostTab = "market";
    private bool sheetOpen;
    /// <summary>The price rows' scroll area, sized like the panel's to the room there is.</summary>
    private ScrollContainer? sheetScroll;
    private VBoxContainer? sheetRows;

    private void Signpost(Thing sign) {
        var tabs = Row(); tabs.AddThemeConstantOverride("separation", 6);
        tabs.AddChild(TabIcon("coins", "Market", () => signpostTab = "market", signpostTab == "market"));
        tabs.AddChild(TabIcon("hardhat", "Hire", () => signpostTab = "workers", signpostTab == "workers"));
        tabs.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        tabs.AddChild(TabIcon("prices", "Prices", () => sheetOpen = !sheetOpen, sheetOpen));
        if (sheetOpen) PriceSheet(sign);
        if (signpostTab == "workers") HireOffers(sign);
        else Market(sign);
    }

    private Button TabIcon(string icon, string tooltip, Action action, bool active) {
        var button = MenuIcon(icon, tooltip, () => { action(); OpenPage("inspect"); });
        button.CustomMinimumSize = new Vector2(50, 50); button.AddThemeConstantOverride("icon_max_width", 30);
        Style.Skin(button, active ? Style.ButtonPressed : Style.ButtonNormal, Style.ButtonHover, Style.ButtonPressed);
        return button;
    }

    /// <summary>A darker well with a row of icons naming what it holds.</summary>
    private VBoxContainer Well(params string[] icons) {
        var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); content.AddChild(well);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); well.AddChild(column);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 6); column.AddChild(heading);
        foreach (string icon in icons) heading.AddChild(Style.Icon(HotbarIcon(icon) ?? GD.Load<Texture2D>($"res://assets/ui/{icon}.svg"), 28));
        return column;
    }

    /// <summary>Builds part of a page into another container than the panel's column.</summary>
    private void Into(Container host, Action build) {
        var column = content; content = new VBoxContainer(); host.AddChild(content);
        try { build(); } finally { content = column; }
    }

    private void Market(Thing sign) {
        var output = sign.Output!;
        var outgoing = Well("crate", "cart", "village");
        Into(outgoing, () => AddInventoryGrid(() => sign.Inventory, sign, id => MoveStack(sign.Inventory, world.S.Backpack, id, sign), "Click: take back · Drag a packed crate here to sell", drag => drag.From == world.S.Backpack && world.CanSell(drag.Item)));
        // Sold goods go on the cart rather than into the backpack, so the sale sounds like its largest stack being loaded.
        var sell = Button("SEND", () => { var load = sign.Inventory.Items.Keys.OrderByDescending(id => sign.Inventory[id]).FirstOrDefault(); if (world.Sell(sign) && load != null) sounds.Play(Sounds.ForItem(load)); });
        sell.Name = "sell"; sell.TooltipText = "Send the cart to the village with these crates"; sell.Icon = GD.Load<Texture2D>("res://assets/ui/cart.svg"); sell.ExpandIcon = true;
        sell.AddThemeConstantOverride("icon_max_width", 40); sell.CustomMinimumSize = new Vector2(0, 50);
        Style.Skin(sell, Style.Primary, Style.PrimaryHover, Style.ButtonPressed);
        sell.AddThemeColorOverride("font_color", Style.Ply); sell.AddThemeColorOverride("font_hover_color", Style.Ply); sell.AddThemeFontOverride("font", Style.Stencil); sell.AddThemeFontSizeOverride("font_size", 20);
        outgoing.AddChild(sell);
        Road(sign);
        var mailbox = Well("mailbox");
        var take = Chip("Take all", "Collect everything delivered", () => { if (world.Near(sign)) foreach (var id in output.Items.Keys.ToArray()) world.Transfer(output, world.S.Backpack, id, output[id]); else Notify("Walk closer first."); }, "collect");
        var heading = (HBoxContainer)mailbox.GetChild(0); heading.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); heading.AddChild(take);
        var boxes = new HBoxContainer(); boxes.AddThemeConstantOverride("separation", 10); mailbox.AddChild(boxes);
        var wages = sign.Wages!;
        // The wage jar: sale coins land here first and pay wages at dawn.
        GridContainer? jar = null;
        Into(boxes, () => jar = AddInventoryGrid(() => wages, sign, id => MoveStack(wages, world.S.Backpack, id, sign), "Wage jar: sale coins land here first and pay wages at dawn\nClick or drag: take · Drop coins here to top it up", drag => drag.Item == Inventory.Coins && drag.From != null && drag.From != wages, columns: 1));
        foreach (var cell in jar!.GetChildren()) ((ItemSlot)cell).AddThemeStyleboxOverride("normal", Style.SocketWage);
        Into(boxes, () => AddInventoryGrid(() => output, sign, id => MoveStack(output, world.S.Backpack, id, sign), "Click or drag: collect", _ => false, columns: 6));
        BagGrid(id => { if (world.CanSell(id)) MoveStack(world.S.Backpack, sign.Inventory, id, sign); else Notify("Pack meat, berries or steel tools in a crate to sell them."); }, "Click: put a packed crate in the market · Drag onto a box", wanted: world.CanSell);
    }

    /// <summary>The road to the village with every cart from this post drawn where it is.</summary>
    private void Road(Thing sign) {
        var row = Row(); row.AddThemeConstantOverride("separation", 6);
        row.AddChild(Style.Icon(HotbarIcon("signpost"), 30));
        var road = new CartRoad { Name = "cart", Cart = GD.Load<Texture2D>("res://assets/ui/cart.svg"), CustomMinimumSize = new Vector2(0, 32), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddChild(road);
        row.AddChild(Style.Icon(GD.Load<Texture2D>("res://assets/ui/village.svg"), 30));
        var left = Label("", 15, Style.Cream); left.CustomMinimumSize = new Vector2(34, 0); left.AutowrapMode = TextServer.AutowrapMode.Off; row.AddChild(left);
        var leftText = new LiveText(shown => { left.Text = shown; road.TooltipText = shown == "" ? "No carts on the road." : $"Cart back in {shown}"; });
        // Redrawn only when a cart moves a whole pixel's worth or the seconds left change.
        int drawn = int.MinValue;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(road)) return;
            road.Trips.Clear();
            TradeOrder? next = null; int moved = 0;
            foreach (var o in world.S.Orders) {
                if (o.Signpost != sign.Id) continue;
                float trip = o.Elapsed / world.Data.TradeSeconds; road.Trips.Add(trip); moved += (int)(trip * 400);
                if (next == null || o.Elapsed > next.Elapsed) next = o;
            }
            int seconds = next == null ? -1 : (int)MathF.Ceiling(Math.Max(0, world.Data.TradeSeconds - next.Elapsed));
            if (moved * 1000 + seconds == drawn) return;
            drawn = moved * 1000 + seconds;
            road.QueueRedraw();
            if (seconds < 0) leftText.Clear(); else leftText.Clear().Append(seconds).Append('s');
            leftText.Show();
        });
    }

    private void HireOffers(Thing sign) {
        foreach (var offer in world.Data.HireOffers) {
            var hire = Button($"{offer.Upfront:0} now  ·  {offer.Wage:0} a day", () => { if (world.Hire(sign, offer) is Worker w) Select(w); });
            hire.Name = $"hire_{offer.Upfront:0}"; hire.Icon = HotbarIcon("hardhat"); hire.ExpandIcon = true; hire.AddThemeConstantOverride("icon_max_width", 30);
            hire.CustomMinimumSize = new Vector2(0, 46); hire.Alignment = HorizontalAlignment.Left;
            hire.TooltipText = $"Hire for {offer.Upfront:0} coins up front and a daily wage of {offer.Wage:0}";
            content.AddChild(hire);
        }
        AddLabel("Wages are paid at each dawn from the wage jar, then from your backpack. Unpaid workers strike.", 13, Muted);
        Assignment(sign);
    }

    /// <summary>Prices as last heard from the village, chalked on a board beside the panel; buying is always at the current price.</summary>
    private void PriceSheet(Thing sign) {
        panel.OffsetLeft = PanelLeft - SheetWidth; sheet.Visible = true; sheet.GetParent<Control>().Visible = true;
        var top = new HBoxContainer(); sheet.AddChild(top);
        var age = Chalk("", 17); age.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(age);
        var news = new LiveText(shown => age.Text = shown);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(age)) return;
            if (world.PriceAge == 0) news.Clear().Append("Today's prices");
            else if (world.PriceAge == 1) news.Clear().Append("Yesterday's prices");
            else news.Clear().Append(world.PriceAge).Append(" days old");
            news.Show();
        });
        var fetch = new Button { Name = "fetch", Icon = HotbarIcon("hardhat"), Text = "→", ExpandIcon = true, CustomMinimumSize = new Vector2(64, 36), FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        fetch.AddThemeConstantOverride("icon_max_width", 24); Style.Skin(fetch, Style.ChalkButton, Style.ChalkButtonHover, Style.ChalkButtonHover);
        fetch.AddThemeFontOverride("font", Style.ChalkHand); fetch.AddThemeColorOverride("font_color", Style.Chalk);
        fetch.Pressed += () => { Act(() => world.FetchPrices(sign)); RefreshLive(); };
        top.AddChild(fetch);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(fetch)) return;
            string? blocked = world.FetchBlock(sign);
            fetch.Disabled = blocked != null; fetch.TooltipText = blocked ?? "The worker assigned here walks to Bramblewick and back with fresh prices.";
        });
        var header = new HBoxContainer(); sheet.AddChild(header);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(40, 0) });
        var demand = Chalk("demand", 13); demand.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; demand.Modulate = new Color(1, 1, 1, .6f); header.AddChild(demand);
        foreach (string column in new[] { "pays", "buy" }) { var cell = Chalk(column, 13); cell.CustomMinimumSize = new Vector2(column == "buy" ? 80 : 50, 0); cell.HorizontalAlignment = HorizontalAlignment.Center; cell.Modulate = new Color(1, 1, 1, .6f); header.AddChild(cell); }
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", 0);
        sheetScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; sheetScroll.AddChild(rows); sheet.AddChild(sheetScroll); sheetRows = rows;
        var coins = HotbarIcon(Inventory.Coins);
        foreach (string id in world.Data.Market.Sells.Keys.Union(world.Data.Market.Buys.Keys)) {
            bool sells = world.Data.Market.Sells.ContainsKey(id), buys = world.Data.Market.Buys.ContainsKey(id);
            var row = new HBoxContainer { TooltipText = world.Lot(id) > 1 ? $"{world.Lot(id)} {world.Data.Name(id)}" : world.Data.Name(id), MouseFilter = Control.MouseFilterEnum.Pass }; rows.AddChild(row);
            var icon = Style.Icon(HotbarIcon(id), 34); row.AddChild(icon);
            if (world.Lot(id) > 1) { var lot = Chalk($"×{world.Lot(id)}", 12); lot.Position = new Vector2(20, 18); icon.AddChild(lot); }
            var pips = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Begin, MouseFilter = Control.MouseFilterEnum.Ignore };
            pips.AddThemeConstantOverride("separation", 3); row.AddChild(pips);
            var sellPrice = Chalk("", 19); sellPrice.CustomMinimumSize = new Vector2(50, 0); sellPrice.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(sellPrice);
            if (sells) {
                // Five chalk strokes, as many bright as the price stands to the full price: fewer means the village has had its fill.
                var strokes = Enumerable.Range(0, 5).Select(_ => new ColorRect { Color = Style.Chalk, CustomMinimumSize = new Vector2(5, 16), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }).ToArray();
                foreach (var stroke in strokes) pips.AddChild(stroke);
                var price = new LiveText(shown => sellPrice.Text = shown);
                int shownTenths = -1;
                live.Add(() => {
                    if (!GodotObject.IsInstanceValid(sellPrice)) return;
                    float known = world.KnownPrice(id);
                    int tenths = (int)MathF.Round(known * 10);
                    if (tenths == shownTenths) return;
                    shownTenths = tenths;
                    price.Clear().Append(tenths / 10).Append('.').Append(tenths % 10); price.Show();
                    int bright = (int)MathF.Round(5 * known / Math.Max(.01f, world.Data.Items[id].Price));
                    for (int i = 0; i < strokes.Length; i++) strokes[i].Modulate = new Color(1, 1, 1, i < bright ? .9f : .15f);
                });
            }
            var buySlot = new CenterContainer { CustomMinimumSize = new Vector2(80, 36) }; row.AddChild(buySlot);
            // Goods the village does not take show no sell price, and goods it does not sell no buy button.
            if (!buys) continue;
            var buy = new Button { Name = $"buy_{id}", Text = $"{world.LotPrice(id)}", Icon = coins, ExpandIcon = true, IconAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(76, 32), FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            buy.AddThemeConstantOverride("icon_max_width", 18); Style.Skin(buy, Style.ChalkButton, Style.ChalkButtonHover, Style.ChalkButtonHover);
            buy.AddThemeFontOverride("font", Style.ChalkHand); buy.AddThemeFontSizeOverride("font_size", 17); buy.AddThemeColorOverride("font_color", Style.Chalk); buy.AddThemeColorOverride("font_hover_color", Style.Chalk);
            buy.Pressed += () => { Act(() => world.Buy(sign, id, Input.IsKeyPressed(Key.Shift) ? 5 : 1)); RefreshLive(); };
            buy.TooltipText = world.Lot(id) > 1 ? $"Buy a crate of {world.Lot(id)} {world.Data.Name(id)} · Shift: 5 crates\nCrates arrive in the mailbox." : $"Buy a {world.Data.Name(id).ToLowerInvariant()} · Shift: 5\nGoods arrive in the mailbox.";
            buySlot.AddChild(buy);
        }
    }

    private static Label Chalk(string text, int size) {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Off, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontOverride("font", Style.ChalkHand); label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", Style.Chalk);
        return label;
    }
}
