using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>A page of the side panel, and the pieces every page is built from, so each screen looks and works alike.</summary>
public abstract partial class PagePanel : VBoxContainer {
    protected readonly SidePanel panel;
    protected readonly UiKit kit;
    protected readonly World world;
    protected readonly WorldView view;
    protected readonly Sounds sounds;
    protected readonly ItemMoves moves;
    protected readonly PageMemory memory;
    protected readonly BackpackDrawer bag;
    protected readonly LiveUpdates live;
    protected HBoxContainer heading => panel.Heading;
    protected TextureRect headingIcon => panel.HeadingIcon;
    protected Label headingTitle => panel.HeadingTitle;

    protected PagePanel(SidePanel panel) {
        this.panel = panel;
        kit = panel.Kit; world = kit.World; sounds = kit.Sounds; view = panel.View; moves = panel.Moves; memory = panel.Memory; bag = panel.Bag; live = panel.Live;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    /// <summary>Puts a round button in the header, before the close button.</summary>
    protected Button HeaderAction(string glyph, string tooltip, Action action, (StyleBoxFlat Normal, StyleBoxFlat Hover) look, string name = "") {
        var button = kit.HeaderButton(glyph, tooltip, action, look, name);
        heading.AddChild(button); heading.MoveChild(button, heading.GetChildCount() - 2);
        return button;
    }

    /// <summary>The funnel that opens the filter window beside the panel, ringed while it is open.</summary>
    protected void FilterButton(Thing t) {
        var button = HeaderAction("filter", "Filters: what workers bring and take", () => { memory.FilterOpen = !memory.FilterOpen; memory.FilterPicking = false; panel.Open("inspect"); }, Style.FilterRound, "filters");
        if (memory.FilterOpen) {
            Style.Skin(button, Style.Round(Style.Mustard, 18).Item1, Style.FilterRound.Hover);
            panel.SideBoard(Style.Board, 340).AddChild(new FilterWindow(kit, t, memory, () => panel.Open("inspect")));
        }
    }

    /// <summary>A beige well inside the panel, holding a row or column of things.</summary>
    protected T Well<T>(T inner) where T : Control {
        AddChild(Style.InWell(inner));
        return inner;
    }

    /// <summary>The row of job buttons for a place that offers jobs.</summary>
    protected void JobButtons(Thing t) { if (world.Jobs(t).Length > 0) AddChild(new JobRow(kit, live, t, memory, () => panel.Open("inspect"))); }

    /// <summary>A label under a cup: what one batch takes or makes.</summary>
    protected static Label PerBatch(string text) {
        var label = Style.Text(text, 13, Style.Tan); label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    /// <summary>Seconds as a short duration: seconds up to a minute and a half, then minutes, then hours.</summary>
    protected static System.Text.StringBuilder AppendDuration(System.Text.StringBuilder text, float seconds) {
        if (seconds < 90) return text.Append((int)MathF.Ceiling(seconds)).Append(" s");
        if (seconds < 5400) return text.Append((int)MathF.Round(seconds / 60)).Append(" min");
        return text.Append((int)MathF.Round(seconds / 3600)).Append(" h");
    }

    /// <summary>Shows the backpack drawer with a grid that works with the open page: <paramref name="wanted"/> lights up what it takes.</summary>
    protected void BagGrid(Action<string> click, string hint, Func<ItemDrag, bool>? accepts = null, Func<string, bool>? wanted = null) {
        bag.Content.AddChild(NewInventoryGrid(() => world.S.Backpack, null, click, hint, accepts, wanted, BagColumns));
        bag.Visible = !bag.Dismissed;
    }
    protected HBoxContainer Row() { var r = new HBoxContainer(); AddChild(r); return r; }
    protected void AddLabel(string text, int size = 15, Color? color = null) => AddChild(Style.Wrapped(text, size, color));
    protected void AddButton(string text, Action action, bool disabled = false) => AddChild(kit.Button(text, action, disabled));
    /// <summary>A label whose text <paramref name="write"/> rebuilds on every refresh.</summary>
    protected void Dynamic(Action<StringBuilder> write, int size = 15, Color? color = null) {
        var l = Style.Wrapped("", size, color); AddChild(l);
        live.Bind(l, write);
    }
    protected string Needs(Dictionary<string, int> cost) => LiveText.Of(text => AppendNeeds(text, cost));
    protected string Cost(Dictionary<string, int> cost) => LiveText.Of(text => AppendCost(text, cost));
    /// <summary>Each input with how many the backpack holds of how many are needed.</summary>
    protected void AppendNeeds(StringBuilder text, Dictionary<string, int> cost) {
        bool first = true;
        foreach (var (item, amount) in cost) {
            if (!first) text.Append("  ·  ");
            first = false;
            text.Append($"{world.Data.Name(item)} {world.Backpack[item]}/{amount}");
        }
    }
    protected void AppendCost(StringBuilder text, Dictionary<string, int> cost) {
        bool first = true;
        foreach (var (item, amount) in cost) {
            if (!first) text.Append("  ·  ");
            first = false;
            text.Append($"{amount} {world.Data.Name(item)}");
        }
    }
    protected const string UseHint = "Food and water: click to consume. Crates: click to place. Torch: click to light. Drag onto the hotbar to assign.";
    protected void UseFromBag(string id) {
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) world.Consume(id); else if (World.IsCrate(id)) panel.Player.PlaceCrate(id); else if (id == "torch" && !world.TorchLit) world.UseTorch(Vector2.Zero);
    }
    /// <summary>A storage's slots in a grid of cups; click takes one item, Ctrl all of it.</summary>
    protected GridContainer StorageGrid(Thing t, int columns = GridColumns, float size = 52) =>
        NewInventoryGrid(() => t.Inventory, t, id => moves.Stack(t.Inventory, world.S.Backpack, id, t), "Click: take one · Ctrl: take all · Drag to move", columns: columns, size: size);
    /// <summary>Moves every material the storage takes from the backpack; tools stay, and so do crates, so one is never packed inside another.</summary>
    protected void StoreAll(Thing t) {
        if (!world.Near(t)) { kit.Notify("Walk closer first."); return; }
        foreach (var id in world.Backpack.Items.Keys.Where(i => world.Data.Items.TryGetValue(i, out var def) && def.Tool == "" && i != "crate").ToArray())
            if (world.Holds(t, id) && t.Inventory.Accepts(id)) world.Transfer(world.Backpack, t.Inventory, id, 999);
    }
    protected void TakeAll(Thing t) {
        if (!world.Near(t)) { kit.Notify("Walk closer first."); return; }
        foreach (var id in t.Inventory.Items.Keys.ToArray()) world.Transfer(t.Inventory, world.Backpack, id, 999);
    }
    /// <summary>Burn time as in-game hours and minutes.</summary>
    protected StringBuilder AppendGameHours(StringBuilder text, float seconds) {
        int minutes = (int)(seconds / world.Data.DaySeconds * 1440);
        return text.Append(minutes / 60).Append(" h ").Append(minutes % 60).Append(" min");
    }
}
