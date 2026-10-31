using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Trading post panel: outgoing crates, the cart's road and the mailbox, or hiring; and the village's prices on a sign beside the panel.</summary>
public partial class MarketPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    /// <summary>How far the price sign reaches left of the panel.</summary>
    public const int SheetWidth = 420;

    protected override void Body() {
        headingTitle.Text = "Trading post";
        Tab("cart", "coins", "Ship", () => memory.SignpostTab = "market", memory.SignpostTab == "market");
        Tab("crew", "hardhat", "Hire", () => memory.SignpostTab = "workers", memory.SignpostTab == "workers");
        Tab("guide", "prices", "Prices", () => memory.SheetOpen = !memory.SheetOpen, memory.SheetOpen);
        FilterButton();
        if (memory.SheetOpen) PriceSheet();
        if (memory.SignpostTab == "workers") HireOffers();
        else Shipping();
        JobButtons();
    }

    /// <summary>A round tab in the header, lit while its page is shown.</summary>
    private void Tab(string glyph, string name, string tooltip, Action action, bool active) {
        var tab = HeaderAction(glyph, tooltip, () => { action(); Redraw(); }, active ? Style.TabActive : Style.TabRound, name);
        tab.CustomMinimumSize = new Vector2(44, 44);
    }

    private void Shipping() {
        var output = thing.Output!;
        var outbound = Well(new VBoxContainer()); outbound.AddThemeConstantOverride("separation", 8);
        outbound.AddChild(Style.WellLabel("Outbound"));
        var ship = new HBoxContainer(); ship.AddThemeConstantOverride("separation", 14); outbound.AddChild(ship);
        ship.AddChild(NewInventoryGrid(() => thing.Inventory, thing, id => moves.Stack(thing.Inventory, world.S.Backpack, id, thing), "Click: take back · Drag a packed crate here to sell", drag => drag.From == world.S.Backpack && world.CanSell(drag.Item), columns: 3));
        // Sold goods go on the cart rather than into the backpack, so the sale sounds like its largest stack being loaded.
        var sell = kit.Button("", () => { var load = thing.Inventory.Items.Keys.OrderByDescending(id => thing.Inventory[id]).FirstOrDefault(); if (world.Sell(thing) && load != null) kit.Sounds.Play(Sounds.ForItem(load)); });
        sell.Name = "sell"; sell.TooltipText = "Send the cart to the village with these crates"; sell.Icon = GD.Load<Texture2D>("res://assets/ui/line_cart.svg"); sell.ExpandIcon = true; sell.IconAlignment = HorizontalAlignment.Center;
        sell.AddThemeConstantOverride("icon_max_width", 32); sell.CustomMinimumSize = new Vector2(0, 56); sell.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; sell.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var look = Style.Pill(Style.Brick); Style.Skin(sell, look.Item1, look.Item2);
        sell.AddThemeColorOverride("icon_normal_color", Style.Rim); sell.AddThemeColorOverride("icon_hover_color", Style.Rim);
        ship.AddChild(sell);
        var auto = new CheckBox { Text = "Send when full", ButtonPressed = thing.AutoSell, FocusMode = Control.FocusModeEnum.None, Name = "auto_sell", TooltipText = "Send the cart by itself once every slot holds a crate" };
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color" }) auto.AddThemeColorOverride(color, Style.Ink);
        auto.Toggled += on => { world.SetAutoSell(thing, on); auto.SetPressedNoSignal(thing.AutoSell); };
        outbound.AddChild(auto);
        var revenue = Style.Text("", 14, Style.Ink); outbound.AddChild(revenue);
        live.Watch(() => thing.Inventory.Count, _ => {
            int value = SaleWorth();
            revenue.Text = value > 0 ? $"Approximate revenue: {value}" : "";
            revenue.Visible = value > 0;
        });
        Road();
        var inbound = Well(new VBoxContainer()); inbound.AddThemeConstantOverride("separation", 8);
        inbound.AddChild(Style.WellLabel("Inbound"));
        var mail = new HBoxContainer(); mail.AddThemeConstantOverride("separation", 14); inbound.AddChild(mail);
        var glyph = Style.Glyph("line_mail", 30, Style.Ink); glyph.MouseFilter = Control.MouseFilterEnum.Pass; glyph.TooltipText = "Mailbox: what the cart brought back · click to take"; mail.AddChild(glyph);
        mail.AddChild(NewInventoryGrid(() => output, thing, id => moves.Stack(output, world.S.Backpack, id, thing), "Click or drag: take", _ => false, columns: 5));
        BagGrid(id => { if (world.CanSell(id)) moves.Stack(world.S.Backpack, thing.Inventory, id, thing); else kit.Notify("Pack goods the village buys in a crate to sell them."); }, "Click: put a packed crate on the cart · Drag onto a box", wanted: world.CanSell);
    }

    /// <summary>What the crates waiting at the post would fetch at the last known prices.</summary>
    private int SaleWorth() {
        float value = 0;
        foreach (var pair in thing.Inventory.Items) {
            if (world.Packed(pair.Key) is not Inventory packed) continue;
            value += world.Data.Items["crate"].Price;
            foreach (var good in packed.Items) if (world.Data.Market.Sells.ContainsKey(good.Key)) value += world.KnownPrice(good.Key) * good.Value;
        }
        return (int)value;
    }

    /// <summary>The road to the village with every cart from this post drawn where it is.</summary>
    private void Road() {
        var strip = new PanelContainer(); strip.AddThemeStyleboxOverride("panel", Style.Round(Style.Line, 22).Item1); AddChild(strip);
        var pad = new MarginContainer(); pad.AddThemeConstantOverride("margin_left", 12); pad.AddThemeConstantOverride("margin_right", 12); pad.AddThemeConstantOverride("margin_top", 6); pad.AddThemeConstantOverride("margin_bottom", 6);
        strip.AddChild(pad);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); pad.AddChild(row);
        row.AddChild(Style.Icon(Icons.Of("signpost"), 26));
        var road = new CartRoad { Name = "cart", Cart = GD.Load<Texture2D>("res://assets/ui/line_cart.svg"), CustomMinimumSize = new Vector2(0, 30), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddChild(road);
        row.AddChild(Style.Glyph("line_village", 26, Style.Rim));
        var left = Style.Heading("", 15); left.CustomMinimumSize = new Vector2(40, 0); left.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(left);
        // Redrawn only when a cart moves a whole pixel's worth, and the time when the seconds left change.
        int seconds = -1;
        live.Watch(() => {
            road.Trips.Clear();
            TradeOrder? next = null; int moved = 0;
            foreach (var o in world.Orders) {
                if (o.Signpost != thing.Id) continue;
                float trip = o.Elapsed / world.Data.TradeSeconds; road.Trips.Add(trip); moved += (int)(trip * 400);
                if (next == null || o.Elapsed > next.Elapsed) next = o;
            }
            seconds = next == null ? -1 : (int)MathF.Ceiling(Math.Max(0, world.Data.TradeSeconds - next.Elapsed));
            return moved;
        }, _ => road.QueueRedraw());
        live.Watch(() => seconds, away => { left.Text = away < 0 ? "" : $"{away} s"; road.TooltipText = away < 0 ? "No carts on the road." : $"Cart back in {away} s"; });
    }

    private void HireOffers() {
        foreach (var offer in world.Data.HireOffers) {
            var hire = kit.Button($"{offer.Upfront:0} now  ·  {offer.Wage:0} a day", () => { if (world.Hire(thing, offer) is Worker w) panel.Select(w); });
            hire.Name = $"hire_{offer.Upfront:0}"; hire.Icon = GD.Load<Texture2D>("res://assets/ui/hardhat.svg"); hire.ExpandIcon = true; hire.AddThemeConstantOverride("icon_max_width", 28);
            hire.CustomMinimumSize = new Vector2(0, 50); hire.Alignment = HorizontalAlignment.Left;
            hire.TooltipText = $"Hire for {offer.Upfront:0} coins up front and a daily wage of {offer.Wage:0}";
            AddChild(hire);
        }
        AddLabel("Wages come out of the bank at dawn, then your backpack. Unpaid workers strike.", 13, Style.Beige);
    }

    /// <summary>Prices as last heard from the village, on an enamel shop sign beside the panel; buying is always at the current price.</summary>
    private void PriceSheet() {
        var board = panel.SideBoard(Style.Sign, SheetWidth - 56);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); board.AddChild(top);
        var title = Style.Text("Bramblewick prices", 19, Style.Ink, Style.Head); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(title);
        var age = Style.Pill("", Style.Tag(Style.Mustard), Style.Line, 12); top.AddChild(age);
        live.Watch(() => world.PriceAge, days => age.Text = days == 0 ? "today" : days == 1 ? "yesterday" : $"{days} days old");
        var fetch = new Button { Name = "fetch", Icon = GD.Load<Texture2D>("res://assets/ui/line_crew.svg"), Text = "→", ExpandIcon = true, CustomMinimumSize = new Vector2(64, 32), FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        fetch.AddThemeConstantOverride("icon_max_width", 18); var fetchLook = Style.Pill(Style.Enamel); Style.Skin(fetch, fetchLook.Item1, fetchLook.Item2);
        fetch.Pressed += () => kit.Run(() => world.FetchPrices(thing));
        top.AddChild(fetch);
        top.AddChild(kit.HeaderButton("close", "Close the price sheet", () => { memory.SheetOpen = false; Redraw(); }, Style.CloseRound, "sheet_close"));
        live.Watch(() => world.FetchBlock(thing), blocked => {
            fetch.Disabled = blocked != null; fetch.TooltipText = blocked ?? "The worker managing deliveries walks to Bramblewick and back with fresh prices.";
        });
        board.AddChild(new ColorRect { Color = Style.Enamel, CustomMinimumSize = new Vector2(0, 3) });
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 6); board.AddChild(header);
        header.AddChild(new Control { CustomMinimumSize = new Vector2(GoodsColumn, 0) });
        foreach (string column in new[] { "Sell price", "Buy price" }) { var cell = Style.Text(column, 13, Style.Tan); cell.CustomMinimumSize = new Vector2(PriceColumn, 0); cell.HorizontalAlignment = HorizontalAlignment.Center; header.AddChild(cell); }
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", 0);
        var sheetScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; sheetScroll.AddChild(rows); board.AddChild(sheetScroll); panel.ShowPriceRows(sheetScroll, rows);
        var coins = Icons.Of(Inventory.Coins);
        foreach (string id in world.Data.Market.Sells.Keys.Union(world.Data.Market.Buys.Keys)) {
            bool sells = world.Data.Market.Sells.ContainsKey(id), buys = world.Data.Market.Buys.ContainsKey(id);
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 40), TooltipText = world.Lot(id) > 1 ? $"{world.Lot(id)} {world.Data.Name(id)}" : world.Data.Name(id), MouseFilter = Control.MouseFilterEnum.Pass };
            row.AddThemeConstantOverride("separation", 6); rows.AddChild(row);
            // Goods sold by the crate show its size beside the icon, so every row keeps the same height and columns.
            var goods = new HBoxContainer { CustomMinimumSize = new Vector2(GoodsColumn, 0) }; goods.AddThemeConstantOverride("separation", 2); row.AddChild(goods);
            goods.AddChild(Style.Icon(Icons.Of(id), 32));
            if (world.Lot(id) > 1) { var lot = Style.Text($"×{world.Lot(id)}", 13, Style.Tan, Style.Head); lot.VerticalAlignment = VerticalAlignment.Center; goods.AddChild(lot); }
            var sellColumn = new HBoxContainer { CustomMinimumSize = new Vector2(PriceColumn, 0), Alignment = BoxContainer.AlignmentMode.Center }; sellColumn.AddThemeConstantOverride("separation", 4); row.AddChild(sellColumn);
            var sellPrice = Style.Text("", 17, Style.Ink, Style.Head); sellPrice.CustomMinimumSize = new Vector2(40, 0); sellPrice.HorizontalAlignment = HorizontalAlignment.Right; sellColumn.AddChild(sellPrice);
            var trend = Style.Text("", 18, Style.Brick, Style.Head); trend.CustomMinimumSize = new Vector2(20, 0); trend.HorizontalAlignment = HorizontalAlignment.Center; sellColumn.AddChild(trend);
            if (sells) {
                live.Watch(() => (int)MathF.Round(world.KnownPrice(id) * 10), tenths => {
                    float known = world.KnownPrice(id), full = world.Data.Items[id].Price;
                    sellPrice.Text = $"{tenths / 10}.{tenths % 10}";
                    // Below its full price the village has had its fill; the arrow shows which way the price stands.
                    trend.Text = known < full * .99f ? "↘" : known > full * 1.01f ? "↗" : "";
                    trend.AddThemeColorOverride("font_color", known < full ? Style.Brick : Style.Rise);
                });
            }
            var buySlot = new CenterContainer { CustomMinimumSize = new Vector2(PriceColumn, 36) }; row.AddChild(buySlot);
            // Goods the village does not take show no sell price, and goods it does not sell no buy button.
            if (!buys) continue;
            var buy = new Button { Name = $"buy_{id}", Text = $"{world.LotPrice(id)}", Icon = coins, ExpandIcon = true, IconAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(80, 30), FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            buy.AddThemeConstantOverride("icon_max_width", 16); var buyLook = Style.Pill(Style.Brick); Style.Skin(buy, buyLook.Item1, buyLook.Item2);
            buy.AddThemeFontOverride("font", Style.Bold); buy.AddThemeFontSizeOverride("font_size", 14);
            buy.Pressed += () => kit.Run(() => world.Buy(thing, id, ItemMoves.All ? 5 : 1));
            buy.TooltipText = world.Lot(id) > 1 ? $"Buy a crate of {world.Lot(id)} {world.Data.Name(id)} · Ctrl: 5 crates\nCrates arrive in the mailbox." : $"Buy a {world.Data.Name(id).ToLowerInvariant()} · Ctrl: 5\nGoods arrive in the mailbox.";
            buySlot.AddChild(buy);
        }
    }

    private const float PriceColumn = 104;
    /// <summary>Room for a good's icon and the size of the crate it comes in.</summary>
    private const float GoodsColumn = 64;
}
