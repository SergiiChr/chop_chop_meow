using Godot;

namespace ChopChopMeow;

/// <summary>The backpack as a drawer above the hotbar, always in the same place, whenever a page works with it.</summary>
public partial class BackpackDrawer : PanelContainer {
    /// <summary>Bottom of the drawer, measured up from the bottom of the screen, just above the hotbar.</summary>
    public const int Bottom = 112;
    private readonly World world;
    private readonly Label fill;
    private (int Used, int Slots) shownFill = (-1, -1);

    /// <summary>Where a page puts its grid of the backpack.</summary>
    public VBoxContainer Content { get; }
    /// <summary>Set by the close button; the drawer stays hidden until another page or building opens.</summary>
    public bool Dismissed { get; set; }

    public BackpackDrawer(UiKit kit, SidePanel panel) {
        world = kit.World;
        Name = "bag"; Visible = false;
        AnchorTop = 1; AnchorBottom = 1; OffsetLeft = 20; OffsetTop = -Bottom; OffsetBottom = -Bottom; GrowVertical = GrowDirection.Begin;
        var drawer = new HBoxContainer(); drawer.AddThemeConstantOverride("separation", 14); AddChild(drawer);
        var label = new VBoxContainer { CustomMinimumSize = new Vector2(40, 0) }; label.AddThemeConstantOverride("separation", 4); drawer.AddChild(label);
        label.AddChild(Style.Glyph("line_bag", 30, Style.Rim));
        fill = Style.Text("", 13, Style.Beige); fill.HorizontalAlignment = HorizontalAlignment.Center; label.AddChild(fill);
        var sort = kit.Button("Sort", () => { world.SortBackpack(); kit.Refresh(); }); sort.Name = "sort_bag"; sort.TooltipText = "Stack loose items and sort by type"; label.AddChild(sort);
        Content = new VBoxContainer(); drawer.AddChild(Content);
        // Every closable window keeps its close button in its top right corner.
        var hide = kit.HeaderButton("close", "Hide the backpack", () => { if (panel.Page == "inventory") panel.Close(); else { Dismissed = true; Visible = false; } }, Style.CloseRound, "bag_close");
        hide.SizeFlagsVertical = SizeFlags.ShrinkBegin; drawer.AddChild(hide);
    }

    /// <summary>Empties the drawer for the next page.</summary>
    public void Clear() {
        foreach (var child in Content.GetChildren()) { Content.RemoveChild(child); child.QueueFree(); }
        Visible = false; shownFill = (-1, -1);
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
