using Godot;

namespace ChopChopMeow;

/// <summary>
/// The backpack as a drawer above the hotbar, under a page that works with it while the player has it open.
/// Each new page opens it there, wherever the player moved it before.
/// </summary>
public partial class BackpackDrawer : MovableWindow {
    /// <summary>Bottom of the drawer, measured up from the bottom of the screen, just above the hotbar.</summary>
    public const int Bottom = 112;
    private readonly World world;
    private readonly Label fill;
    private (int Used, int Slots) shownFill = (-1, -1);

    /// <summary>Where a page puts its grid of the backpack.</summary>
    public VBoxContainer Content { get; }
    /// <summary>
    /// Whether the player has the backpack open: a page that works with it shows it only then.
    /// Opening a building's page leaves it as it is, unless the building is storage, which opens it.
    /// </summary>
    public bool Opened { get; set { field = value; ShowIfOpened(); } }
    /// <summary>Whether the open page's list takes the drawer's room, which hides the drawer while the list shows.</summary>
    public bool Crowded { get; set { if (field == value) return; field = value; ShowIfOpened(); } }
    /// <summary>Whether the open page fills the drawer with its grid of the backpack.</summary>
    public bool Filled => Content.GetChildCount() > 0;
    /// <summary>What a click on a backpack item does for the open building's page, which a click on the hotbar does too; null on other pages.</summary>
    public System.Action<string>? Store { get; set; }

    public BackpackDrawer(UiKit kit, SidePanel panel) {
        world = kit.World;
        Name = "bag"; Visible = false;
        AnchorTop = 1; AnchorBottom = 1; OffsetLeft = 20; OffsetTop = -Bottom; OffsetBottom = -Bottom; GrowVertical = GrowDirection.Begin;
        var drawer = new HBoxContainer(); drawer.AddThemeConstantOverride("separation", 14); AddChild(drawer);
        var label = new VBoxContainer { CustomMinimumSize = new Vector2(40, 0) }; label.AddThemeConstantOverride("separation", 4); drawer.AddChild(label);
        label.AddChild(Style.Glyph("line_bag", 30, Style.Rim));
        fill = Style.Text("", 13, Style.Beige); fill.HorizontalAlignment = HorizontalAlignment.Center; label.AddChild(fill);
        var sort = kit.Button("Sort", () => {
            if (panel.Page == Page.Barrel) panel.Open(Page.Backpack);
            world.SortBackpack();
        });
        sort.Name = "sort_bag"; sort.TooltipText = "Stack loose items and sort by type"; label.AddChild(sort);
        Content = new VBoxContainer(); drawer.AddChild(Content);
        // Every closable window keeps its close button in its top right corner.
        var hide = kit.HeaderButton("close", "Hide the backpack", () => { if (panel.Page == Page.Backpack) panel.Close(); else Opened = false; }, Style.CloseRound, "bag_close");
        hide.SizeFlagsVertical = SizeFlags.ShrinkBegin; drawer.AddChild(hide);
    }

    /// <summary>Empties the drawer for the next page.</summary>
    public void Clear() {
        foreach (var child in Content.GetChildren()) { Content.RemoveChild(child); child.QueueFree(); }
        Crowded = false; shownFill = (-1, -1); Store = null;
        ShowIfOpened();
    }

    /// <summary>Shows the drawer while the player has the backpack open and the page fills it, unless a list takes its room.</summary>
    public void ShowIfOpened() {
        bool shows = Opened && Filled && !Crowded;
        if (Visible != shows) Visible = shows;
        Refresh();
    }

    /// <summary>Shows how full the backpack is.</summary>
    public void Refresh() {
        if (!Visible) return;
        var now = (world.Backpack.UsedSlots, world.Backpack.Slots);
        if (now == shownFill) return;
        shownFill = now;
        fill.Text = $"{now.UsedSlots}/{now.Slots}";
    }
}
