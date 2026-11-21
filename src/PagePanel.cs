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
    protected readonly HeldCursor cursor;
    protected readonly LiveUpdates live = new();
    protected TextureRect headingIcon => panel.HeadingIcon;
    protected Label headingTitle => panel.HeadingTitle;

    protected PagePanel(PageTools tools) {
        panel = tools.Panel; kit = tools.Kit; world = kit.World; moves = tools.Moves; memory = tools.Memory; cursor = tools.Cursor;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public override void _Ready() => Build();

    /// <summary>Fills the page from the world and what it keeps; runs when it opens and whenever it is drawn again.</summary>
    protected abstract void Build();

    /// <summary>Whether what this page shows still exists, so that drawing it again makes sense.</summary>
    public virtual bool Current => true;

    /// <summary>The page that takes this one's place once what it shows has gone; with none the panel closes.</summary>
    public virtual Page GivesWayTo => Page.None;

    /// <summary>Brings what the page shows up to date.</summary>
    public void Refresh() => live.Run();

    /// <summary>Builds the page's body again, keeping its state; a page whose subject has gone closes or gives way, as on any refresh.</summary>
    public void Redraw() {
        if (!Current) { panel.Refresh(); return; }
        live.Clear();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        panel.ClearFrame();
        Build();
        Refresh(); panel.Bag.Refresh();
    }

    /// <summary>
    /// Draws the page again when <paramref name="read"/> gives something other than it gives now.
    /// What sets a page's pieces changes on the server, so a page drawn right after an action would show the world before the answer.
    /// </summary>
    protected void RedrawOnChange<T>(Func<T> read) {
        var drawn = read();
        live.Add(() => { if (!EqualityComparer<T>.Default.Equals(read(), drawn)) RedrawLater(); });
    }

    /// <summary>Draws the page again once the running update is over, unless it has closed by then.</summary>
    protected void RedrawLater() => Callable.From(() => { if (panel.Shows(this)) Redraw(); }).CallDeferred();

    /// <summary>Puts a round button in the header, before the close button.</summary>
    protected Button HeaderAction(string glyph, string tooltip, Action action, (StyleBoxFlat Normal, StyleBoxFlat Hover) look, string name = "") {
        var button = kit.HeaderButton(glyph, tooltip, action, look, name);
        panel.AddToHeader(button);
        return button;
    }

    /// <summary>A beige well inside the panel, holding a row or column of things.</summary>
    protected T Well<T>(T inner) where T : Control {
        AddChild(Style.InWell(inner));
        return inner;
    }

    /// <summary>
    /// Shows the backpack drawer with a grid that works with the open page: <paramref name="batch"/> is what a Ctrl- or Shift-click on a stack does there, and <paramref name="wanted"/> lights up what the page takes.
    /// A double click uses an item, as on the backpack's own page.
    /// </summary>
    protected void BagGrid(Action<string>? batch, string hint, Func<HeldCursor, bool>? takes = null, Func<string, bool>? wanted = null, Action<int, string, int>? put = null) {
        // A pour changes the contents of a bound vessel while leaving the vessel in its slot.
        Action<string>? spared = batch == null ? null : id => {
            if (Input.IsKeyPressed(Key.Ctrl) && !PoursFromBag(id)) world.FromSpares(() => batch(id));
            else batch(id);
        };
        panel.Bag.Content.AddChild(NewInventoryGrid(() => world.S.Backpack, null, spared, hint, takes, wanted, BagColumns, use: UseFromBag, put: put));
        panel.Bag.Visible = !panel.Bag.Dismissed;
        if (StoresFromHotbar) panel.Bag.Store = batch;
    }
    /// <summary>Whether a Ctrl- or Shift-click on the hotbar moves items here as one in the backpack drawer does: true on pages about a building or an opened crate.</summary>
    protected virtual bool StoresFromHotbar => false;
    /// <summary>Whether the page takes the contents of a vessel while the vessel stays in the backpack.</summary>
    protected virtual bool PoursFromBag(string item) => false;
    protected HBoxContainer Row() { var r = new HBoxContainer(); AddChild(r); return r; }
    protected void AddLabel(string text, int size = 15, Color? color = null) => AddChild(Style.Wrapped(text, size, color));
    protected void AddButton(string text, Action action, bool disabled = false) => AddChild(kit.Button(text, action, disabled));
    /// <summary>A label whose text <paramref name="write"/> rebuilds on every refresh.</summary>
    protected void Dynamic(Action<StringBuilder> write, int size = 15, Color? color = null) {
        var l = Style.Wrapped("", size, color); AddChild(l);
        live.Bind(l, write);
    }
    /// <summary>The hint on backpack cells: what each click does, with the page's own Ctrl- and Shift-click when it has one.</summary>
    protected const string BagHint = "LMB: pick up · RMB: pick up half · Double LMB: use";
    /// <summary>
    /// A double click on a backpack item eats or drinks it, lights a torch, opens a container, or selects a tool on the hotbar.
    /// </summary>
    protected void UseFromBag(string id) {
        if (!panel.Hotbar.UseItem(id) && world.Data.Items.TryGetValue(id, out var def) && def.Tool != "") panel.Hotbar.SelectTool(id);
    }
}
