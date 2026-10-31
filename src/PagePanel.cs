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
    protected readonly ItemMoves moves;
    protected readonly PageMemory memory;
    protected readonly LiveUpdates live = new();
    protected HBoxContainer heading => panel.Heading;
    protected TextureRect headingIcon => panel.HeadingIcon;
    protected Label headingTitle => panel.HeadingTitle;

    protected PagePanel(PageTools tools) {
        panel = tools.Panel; kit = tools.Kit; world = kit.World; moves = tools.Moves; memory = tools.Memory;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public override void _Ready() => Build();

    /// <summary>Fills the page from the world and what it keeps; runs when it opens and whenever it is drawn again.</summary>
    protected abstract void Build();

    /// <summary>Whether what this page shows still exists, so that drawing it again makes sense.</summary>
    public virtual bool Current => true;

    /// <summary>Brings what the page shows up to date.</summary>
    public void Refresh() => live.Run();

    /// <summary>Builds the page's body again, keeping its state; a page whose subject has gone gives way to a new one.</summary>
    public void Redraw() {
        if (!Current) { panel.Open(panel.Page); return; }
        live.Clear();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        panel.ClearFrame();
        Build();
        Refresh(); panel.Bag.Refresh();
    }

    /// <summary>Draws the page again once the running update is over, unless it has closed by then.</summary>
    protected void RedrawLater() => Callable.From(() => { if (panel.Shows(this)) Redraw(); }).CallDeferred();

    /// <summary>Puts a round button in the header, before the close button.</summary>
    protected Button HeaderAction(string glyph, string tooltip, Action action, (StyleBoxFlat Normal, StyleBoxFlat Hover) look, string name = "") {
        var button = kit.HeaderButton(glyph, tooltip, action, look, name);
        heading.AddChild(button); heading.MoveChild(button, heading.GetChildCount() - 2);
        return button;
    }

    /// <summary>A beige well inside the panel, holding a row or column of things.</summary>
    protected T Well<T>(T inner) where T : Control {
        AddChild(Style.InWell(inner));
        return inner;
    }

    /// <summary>A label under a cup: what one batch takes or makes.</summary>
    protected static Label PerBatch(string text) {
        var label = Style.Text(text, 13, Style.Tan); label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    /// <summary>Shows the backpack drawer with a grid that works with the open page: <paramref name="wanted"/> lights up what it takes.</summary>
    protected void BagGrid(Action<string> click, string hint, Func<ItemDrag, bool>? accepts = null, Func<string, bool>? wanted = null) {
        panel.Bag.Content.AddChild(NewInventoryGrid(() => world.S.Backpack, null, click, hint, accepts, wanted, BagColumns));
        panel.Bag.Visible = !panel.Bag.Dismissed;
    }
    protected HBoxContainer Row() { var r = new HBoxContainer(); AddChild(r); return r; }
    protected void AddLabel(string text, int size = 15, Color? color = null) => AddChild(Style.Wrapped(text, size, color));
    protected void AddButton(string text, Action action, bool disabled = false) => AddChild(kit.Button(text, action, disabled));
    /// <summary>A label whose text <paramref name="write"/> rebuilds on every refresh.</summary>
    protected void Dynamic(Action<StringBuilder> write, int size = 15, Color? color = null) {
        var l = Style.Wrapped("", size, color); AddChild(l);
        live.Bind(l, write);
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
}
