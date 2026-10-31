using System;
using Godot;

namespace ChopChopMeow;

/// <summary>What outlasts a page: the choices that stay as they were the next time it opens.</summary>
public sealed class PageMemory {
    /// <summary>Whether a packing station shows its good picker.</summary>
    public bool PackPicking;
    /// <summary>The field guide topic shown.</summary>
    public string GuideTab = "controls";
    /// <summary>The tool group whose recipes are shown.</summary>
    public string CraftTab = "";
    /// <summary>The trading post's page, and whether its price sheet is open.</summary>
    public string SignpostTab = "market";
    public bool SheetOpen = true;
}

/// <summary>What every page is built with.</summary>
public sealed record PageTools(SidePanel Panel, UiKit Kit, ItemMoves Moves, PageMemory Memory);

/// <summary>What the side panel inspects: a thing, a worker or nothing, never both.</summary>
public readonly record struct Selection {
    public int Thing { get; private init; }
    public int Worker { get; private init; }
    public static Selection Of(Thing t) => new() { Thing = t.Id };
    public static Selection Of(Worker w) => new() { Worker = w.Id };
}

/// <summary>
/// The panel on the right: one page at a time under a shared header, with the board beside it for the price sheet or the filter window.
/// A page is built afresh when it opens and keeps its own state and updates; opening the page already showing draws it again.
/// </summary>
public partial class SidePanel : PanelContainer {
    /// <summary>Left edge of the panel and the clock above it, measured from the right of the screen.</summary>
    public const int Left = -476;
    /// <summary>Top of the panel, below the clock.</summary>
    private const int Top = 156;
    private readonly World world;
    private readonly UiKit kit;
    private readonly WorldView view;
    private readonly Control ui;
    private readonly PageTools tools;
    private readonly ScrollContainer scroll;
    /// <summary>The board left of the panel that holds the price sign or the filter window.</summary>
    private readonly PanelContainer board;
    /// <summary>The board's column.</summary>
    private readonly VBoxContainer sheet;
    private readonly VBoxContainer content;
    /// <summary>The price rows' scroll area, sized like the panel's to the room there is.</summary>
    private ScrollContainer? sheetScroll;
    private VBoxContainer? sheetRows;
    private Selection shownInspect;
    /// <summary>The open page, or null while the panel is closed.</summary>
    private PagePanel? page;

    public BackpackDrawer Bag { get; }
    /// <summary>The player's control of the world, which also needs this panel, so the game sets it once both exist.</summary>
    public PlayerControl Player { get; set; } = null!;
    /// <summary>The open page, or nothing while the panel is closed.</summary>
    public string Page { get; private set; } = "welcome";
    public Selection Selection { get; private set; }
    public HBoxContainer Heading { get; }
    public TextureRect HeadingIcon { get; }
    public Label HeadingTitle { get; }
    public float ScreenWidth => ui.Size.X;

    public SidePanel(UiKit kit, WorldView view, ItemMoves moves, Control ui) {
        this.kit = kit; this.view = view; this.ui = ui; world = kit.World;
        tools = new PageTools(this, kit, moves, new PageMemory());
        // The panel hugs what it shows, up to the room above the hotbar, and scrolls beyond that.
        AnchorLeft = 1; AnchorRight = 1; OffsetLeft = Left; OffsetRight = -20; OffsetTop = Top; OffsetBottom = Top; Visible = false;
        Bag = new BackpackDrawer(kit, this);
        var frame = new HBoxContainer(); frame.AddThemeConstantOverride("separation", 20); AddChild(frame);
        board = new PanelContainer { Visible = false, SizeFlagsVertical = SizeFlags.ShrinkBegin }; frame.AddChild(board);
        sheet = new VBoxContainer { Name = "side_board", Visible = false }; sheet.AddThemeConstantOverride("separation", 12); board.AddChild(sheet);
        var outer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; outer.AddThemeConstantOverride("separation", 16); frame.AddChild(outer);
        Heading = new HBoxContainer(); Heading.AddThemeConstantOverride("separation", 12); outer.AddChild(Heading);
        HeadingIcon = Style.Icon(null, 44); HeadingIcon.Visible = false; HeadingIcon.MouseFilter = MouseFilterEnum.Pass; Heading.AddChild(HeadingIcon);
        HeadingTitle = Style.Heading("");
        HeadingTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; Heading.AddChild(HeadingTitle);
        Heading.AddChild(kit.HeaderButton("close", "Close · Esc", Close, Style.CloseRound, "close"));
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; outer.AddChild(scroll);
        // Room for the pills that sit on the cups' corners.
        var inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; inset.AddThemeConstantOverride("margin_right", 8); inset.AddThemeConstantOverride("margin_bottom", 8); scroll.AddChild(inset);
        content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; inset.AddChild(content);
        world.Replaced += () => { Selection = default; Close(); };
    }

    /// <summary>Shows a page: the one already showing draws again and keeps its state, any other starts afresh.</summary>
    public void Open(string name) {
        bool same = name == Page && (name != "inspect" || Selection == shownInspect);
        shownInspect = Selection;
        Visible = true; view.PanelOpen = true;
        if (same && page is { Current: true }) { page.Redraw(); return; }
        // A different page or building leaves the backpack drawer's close button behind.
        if (!same) Bag.Dismissed = false;
        Page = name;
        foreach (var child in content.GetChildren()) { content.RemoveChild(child); child.QueueFree(); }
        ClearFrame();
        page = name switch {
            "welcome" => new WelcomePage(tools),
            "help" => new GuidePanel(tools),
            "build" => new PlansPanel(tools),
            "inventory" => new BackpackPage(tools),
            "workers" => new CrewPanel(tools),
            "craft" => new CraftPanel(tools),
            _ => InspectPage.For(tools, Selection)
        };
        content.AddChild(page);
        page.Refresh(); Bag.Refresh();
    }

    /// <summary>Empties what a page fills around its body: the header, the side board and the backpack drawer.</summary>
    public void ClearFrame() {
        while (Heading.GetChildCount() > 3) { var extra = Heading.GetChild(2); Heading.RemoveChild(extra); extra.QueueFree(); }
        HeadingIcon.Texture = null; HeadingIcon.Visible = false; HeadingIcon.TooltipText = ""; HeadingIcon.SelfModulate = Colors.White;
        HeadingTitle.Text = Page switch { "welcome" => "A new beginning", "build" => "Building plans", "inventory" => "Backpack", "help" => "Field guide", "workers" => "Your crew", "craft" => "Crafting", _ => "Inspect" };
        foreach (var child in sheet.GetChildren()) { sheet.RemoveChild(child); child.QueueFree(); }
        board.Visible = false; sheet.Visible = false; sheetScroll = null;
        OffsetLeft = Left; scroll.ScrollVertical = 0;
        Bag.Clear();
    }

    /// <summary>Whether this page is the one showing.</summary>
    public bool Shows(PagePanel shown) => page == shown;

    /// <summary>Leaves only the backpack drawer on screen, so the camera keeps the player centred.</summary>
    public void DrawerOnly() { Visible = false; view.PanelOpen = false; }

    /// <summary>Opens a page, or closes it if it is the one showing.</summary>
    public void Toggle(string name) { if (Page == name) Close(); else Open(name); }

    public void Close() { Visible = false; Bag.Visible = false; view.PanelOpen = false; Page = ""; page = null; }

    /// <summary>Runs the open page's updates.</summary>
    public void Refresh() => page?.Refresh();

    /// <summary>Selects a building or resource and shows it.</summary>
    public void Select(Thing t) {
        if (t.Kind == ThingKind.Reinforced && !(Page == "inspect" && Selection.Thing == t.Id)) kit.Sounds.Play("open_steel_smelter", t.Pos);
        Selection = Selection.Of(t);
        // A work order carries on: it waits for a click on a resource, whatever the panel shows meanwhile.
        if (Player.Mode.Placing != null) Player.Mode = new ClickMode.Normal();
        Open("inspect");
    }

    /// <summary>Selects a worker and shows them.</summary>
    public void Select(Worker w) { Selection = Selection.Of(w); Open("inspect"); }

    /// <summary>Marks a thing as selected without opening its page, as a right click on it does.</summary>
    public void Mark(Thing t) => Selection = Selection.Of(t);

    /// <summary>Clears the selection if it is this thing, which has gone; true when it was.</summary>
    public bool Deselect(int id) {
        if (Selection.Thing != id) return false;
        Selection = default; return true;
    }

    /// <summary>The board beside the panel, for the price sign or the filter window; it hangs left of the panel.</summary>
    public VBoxContainer SideBoard(StyleBox look, float width) {
        board.AddThemeStyleboxOverride("panel", look);
        board.Visible = true; sheet.Visible = true; sheet.CustomMinimumSize = new Vector2(width, 0);
        OffsetLeft = Left - width - 56;
        return sheet;
    }

    /// <summary>Hands over the price rows, so <see cref="Fit"/> sizes their scroll area.</summary>
    public void ShowPriceRows(ScrollContainer scrollArea, VBoxContainer rows) { sheetScroll = scrollArea; sheetRows = rows; }

    /// <summary>Sizes the panel's scroll area to what it shows, up to the room between the clock and the hotbar.</summary>
    public void Fit() {
        if (!Visible) return;
        float room = Math.Max(160, ui.Size.Y - Top - 120);
        float want = Math.Min(content.GetParent<Control>().GetCombinedMinimumSize().Y, room);
        if (sheet.Visible && IsInstanceValid(sheetScroll)) {
            // The board reaches further left than the panel, over the backpack drawer, so it stops above the drawer.
            float clear = Bag.Visible ? ui.Size.Y - Top - BackpackDrawer.Bottom - Bag.Size.Y - 40 : room;
            float rows = Math.Min(sheetRows!.GetCombinedMinimumSize().Y, Math.Min(room, clear) - 110);
            if (Math.Abs(sheetScroll!.CustomMinimumSize.Y - rows) > .5f) sheetScroll.CustomMinimumSize = new Vector2(0, rows);
        }
        if (Math.Abs(scroll.CustomMinimumSize.Y - want) < .5f) return;
        // A control grows to its minimum size but never shrinks back on its own, so the panel is reset to it.
        scroll.CustomMinimumSize = new Vector2(0, want);
        OffsetBottom = Top;
    }
}
