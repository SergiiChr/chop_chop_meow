using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>What outlasts a page: the choices that stay as they were the next time it opens.</summary>
public sealed class PageMemory {
    /// <summary>Whether a packing station shows its good picker.</summary>
    public bool PackPicking;
    /// <summary>The field guide topic shown.</summary>
    public string GuideTab = "controls";
    /// <summary>The recipe groups the player has closed, on every recipe board.</summary>
    public readonly HashSet<RecipeGroup> ClosedGroups = new();
    /// <summary>Whether the trading post shows hiring rather than shipping, and whether its price sheet is open.</summary>
    public bool Hiring, SheetOpen = true;
    /// <summary>A station's worker mark before the last press on its recipe board; the page may redraw between the two presses of a double-click.</summary>
    public string KeptBefore = "";
    /// <summary>The number of the packed crate opened from the backpack.</summary>
    public int CarriedCrate;
}

/// <summary>What every page is built with.</summary>
public sealed record PageTools(SidePanel Panel, UiKit Kit, ItemMoves Moves, PageMemory Memory);

/// <summary>The pages of the side panel.</summary>
public enum Page { None, Welcome, Build, Backpack, Guide, Crew, Craft, Inspect, Crate }

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
public partial class SidePanel : MovableWindow {
    /// <summary>Left edge of the panel and the clock above it, measured from the right of the screen.</summary>
    public const int Left = -476;
    /// <summary>Top of the panel, below the clock.</summary>
    private const int Top = 156;
    /// <summary>Each page's title and how it is made, in the order of <see cref="ChopChopMeow.Page"/>.</summary>
    private static readonly (string Title, Func<PageTools, Selection, PagePanel> Make)[] Pages = {
        ("Inspect", InspectPage.For),
        ("A new beginning", (tools, _) => new WelcomePage(tools)),
        ("Building plans", (tools, _) => new PlansPanel(tools)),
        ("Backpack", (tools, _) => new BackpackPage(tools)),
        ("Field guide", (tools, _) => new GuidePanel(tools)),
        ("Your crew", (tools, _) => new CrewPanel(tools)),
        ("Crafting", (tools, _) => new CraftPanel(tools)),
        ("Inspect", InspectPage.For),
        ("Wood crate", (tools, _) => new CarriedCratePanel(tools, tools.Memory.CarriedCrate)),
    };
    private readonly World world;
    private readonly UiKit kit;
    private readonly WorldView view;
    private readonly Control ui;
    private readonly PageTools tools;
    private readonly HBoxContainer heading;
    private readonly ScrollContainer scroll;
    /// <summary>The board left of the panel that holds the price sign or the filter window.</summary>
    private readonly PanelContainer board;
    /// <summary>The board's column.</summary>
    private readonly VBoxContainer sheet;
    private readonly VBoxContainer content;
    /// <summary>What the scroll area holds, whose size the panel takes.</summary>
    private readonly MarginContainer inset;
    /// <summary>The price rows' scroll area, sized like the panel's to the room there is.</summary>
    private ScrollContainer? sheetScroll;
    private Control? sheetRows;
    // The board's height apart from its scroll area, or null to measure it as it is.
    private float? sheetRest;
    // Whether the board's list hides the backpack drawer and reaches down where it was.
    private bool sheetHidesDrawer;
    /// <summary>The open page's own long list and what it holds, which <see cref="Fit"/> sizes to the room the rest of the page leaves.</summary>
    private ScrollContainer? pageScroll;
    private Control? pageRows;
    /// <summary>The least a page's own list keeps, about two rows of recipe cards.</summary>
    private const float MinPageRows = 150;
    private Selection shownInspect;
    private int awaited;
    /// <summary>The open page, or null while the panel is closed.</summary>
    private PagePanel? page;

    public BackpackDrawer Bag { get; }
    /// <summary>A small window of its own left of the panel and level with its top, which a page fills through <see cref="ShowAside"/>; it follows the panel.</summary>
    public PanelContainer Aside { get; }
    private readonly VBoxContainer asideContent;
    private bool asideShown;
    /// <summary>The gap between the panel and the window beside it.</summary>
    private const float AsideGap = 12;
    /// <summary>The player's control of the world, which also needs this panel, so the game sets it once both exist.</summary>
    public PlayerControl Player { get; set; } = null!;
    /// <summary>The open page, or nothing while the panel is closed.</summary>
    public Page Page { get; private set; }
    public Selection Selection { get; private set; }
    public TextureRect HeadingIcon { get; }
    public Label HeadingTitle { get; }
    public float ScreenWidth => ui.Size.X;
    /// <summary>Left edge of the panel beside any board, measured from the right of the screen, where the player moved it.</summary>
    private float LeftEdge => Left + Moved.X;

    public SidePanel(UiKit kit, WorldView view, ItemMoves moves, Control ui) {
        this.kit = kit; this.view = view; this.ui = ui; world = kit.World;
        tools = new PageTools(this, kit, moves, new PageMemory());
        // The panel hugs what it shows, up to the room above the hotbar, and scrolls beyond that.
        AnchorLeft = 1; AnchorRight = 1; OffsetLeft = Left; OffsetRight = -20; OffsetTop = Top; OffsetBottom = Top; Visible = false;
        Bag = new BackpackDrawer(kit, this);
        Aside = new PanelContainer { Name = "aside", Visible = false }; Aside.AddThemeStyleboxOverride("panel", Style.Board);
        asideContent = new VBoxContainer(); asideContent.AddThemeConstantOverride("separation", 12); Aside.AddChild(asideContent);
        var frame = new HBoxContainer(); frame.AddThemeConstantOverride("separation", 20); AddChild(frame);
        // Boxes on the panel pass a press on their own surface on, so it moves the panel.
        board = new PanelContainer { Visible = false, SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Pass }; frame.AddChild(board);
        sheet = new VBoxContainer { Name = "side_board", Visible = false }; sheet.AddThemeConstantOverride("separation", 12); board.AddChild(sheet);
        var outer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; outer.AddThemeConstantOverride("separation", 16); frame.AddChild(outer);
        heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 12); outer.AddChild(heading);
        HeadingIcon = Style.Icon(null, 44); HeadingIcon.Visible = false; HeadingIcon.MouseFilter = MouseFilterEnum.Pass; heading.AddChild(HeadingIcon);
        HeadingTitle = Style.Heading("");
        HeadingTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(HeadingTitle);
        heading.AddChild(kit.HeaderButton("close", "Close · Esc", Close, Style.CloseRound, "close"));
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; outer.AddChild(scroll);
        // Room for the pills that sit on the cups' corners.
        inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; inset.AddThemeConstantOverride("margin_right", 8); inset.AddThemeConstantOverride("margin_bottom", 8); scroll.AddChild(inset);
        content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; inset.AddChild(content);
        world.Replaced += () => { Selection = default; awaited = 0; Close(); };
    }

    /// <summary>Shows a page: the one already showing draws again and keeps its state, any other starts afresh.</summary>
    public void Open(Page name) {
        bool same = name == Page && (name != Page.Inspect || Selection == shownInspect);
        shownInspect = Selection;
        Visible = true; view.PanelOpen = true;
        if (same && page is { Current: true }) { page.Redraw(); return; }
        // A different page or building leaves the backpack drawer's close button behind, and opens both windows where they open first.
        if (!same) { Bag.Dismissed = false; ResetPlace(); Bag.ResetPlace(); }
        Page = name;
        foreach (var child in content.GetChildren()) { content.RemoveChild(child); child.QueueFree(); }
        ClearFrame();
        page = Pages[(int)name].Make(tools, Selection);
        content.AddChild(page);
        page.Refresh(); Bag.Refresh();
    }

    /// <summary>Empties what a page fills around its body: the header, the side board and the backpack drawer.</summary>
    public void ClearFrame() {
        while (heading.GetChildCount() > 3) { var extra = heading.GetChild(2); heading.RemoveChild(extra); extra.QueueFree(); }
        HeadingIcon.Texture = null; HeadingIcon.Visible = false; HeadingIcon.TooltipText = ""; HeadingIcon.SelfModulate = Colors.White;
        HeadingTitle.Text = Pages[(int)Page].Title;
        foreach (var child in sheet.GetChildren()) { sheet.RemoveChild(child); child.QueueFree(); }
        board.Visible = false; sheet.Visible = false; sheetScroll = null; pageScroll = null;
        foreach (var child in asideContent.GetChildren()) { asideContent.RemoveChild(child); child.QueueFree(); }
        asideShown = false; Aside.Visible = false;
        OffsetLeft = LeftEdge; scroll.ScrollVertical = 0;
        Bag.Clear();
    }

    /// <summary>Puts a control in the header, before the close button.</summary>
    public void AddToHeader(Control control) { heading.AddChild(control); heading.MoveChild(control, heading.GetChildCount() - 2); }

    /// <summary>Whether this page is the one showing.</summary>
    public bool Shows(PagePanel shown) => page == shown;

    /// <summary>Leaves only the backpack drawer on screen, so the camera keeps the player centred.</summary>
    public void DrawerOnly() { Visible = false; Aside.Visible = false; view.PanelOpen = false; }

    /// <summary>Opens a page, or closes it if it is the one showing.</summary>
    public void Toggle(Page name) { if (Page == name) Close(); else Open(name); }

    public void Close() { Visible = false; Bag.Visible = false; Aside.Visible = false; view.PanelOpen = false; Page = Page.None; page = null; }

    /// <summary>Whether the open page shows something that has gone, and so closes or gives way on the next refresh.</summary>
    public bool SubjectGone => page is { Current: false };

    /// <summary>
    /// Runs the open page's updates.
    /// A page whose thing has gone, such as an emptied recovery box or a building someone else took down, closes or gives way to the page it names.
    /// </summary>
    public void Refresh() {
        if (SubjectGone) {
            if (page!.GivesWayTo == Page.None) { Selection = default; Close(); } else Open(page.GivesWayTo);
            return;
        }
        page?.Refresh();
    }

    /// <summary>Selects a building or resource and shows it.</summary>
    public void Select(Thing t) {
        if (t.Kind == ThingKind.Reinforced && !(Page == Page.Inspect && Selection.Thing == t.Id)) kit.Sounds.Play("open_steel_smelter", t.Pos);
        Selection = Selection.Of(t);
        // A work order carries on: it waits for a click on a resource, whatever the panel shows meanwhile.
        if (Player.Mode.Placing != null) Player.Mode = new ClickMode.Normal();
        Open(Page.Inspect);
    }

    /// <summary>Opens a packed crate from the backpack.</summary>
    public void OpenCrate(int crate) {
        if (tools.Memory.CarriedCrate != crate) Page = Page.None;
        tools.Memory.CarriedCrate = crate;
        Open(Page.Crate);
    }

    /// <summary>Selects a worker and shows them.</summary>
    public void Select(Worker w) { Selection = Selection.Of(w); Open(Page.Inspect); }

    /// <summary>Opens a thing or worker the server made for this player, as soon as an update brings it.</summary>
    public void SelectWhenHere(int id) => awaited = id;

    /// <summary>Called every frame: opens what <see cref="SelectWhenHere"/> is waiting for once it is here.</summary>
    public void ShowAwaited() {
        if (awaited == 0) return;
        if (world.Find(awaited) is Thing t) { awaited = 0; Select(t); return; }
        foreach (var w in world.Workers) if (w.Id == awaited) { awaited = 0; Select(w); return; }
    }

    /// <summary>Marks a thing as selected without opening its page, as a right click on it does; a selected worker stays selected.</summary>
    public void Mark(Thing t) { if (Selection.Worker == 0) Selection = Selection.Of(t); }

    /// <summary>Clears the selection if it is this thing, which has gone; true when it was.</summary>
    public bool Deselect(int id) {
        if (Selection.Thing != id) return false;
        Selection = default; return true;
    }

    /// <summary>The board beside the panel, for the price sign or the filter window; it hangs left of the panel.</summary>
    public VBoxContainer SideBoard(StyleBox look, float width) {
        board.AddThemeStyleboxOverride("panel", look);
        board.Visible = true; sheet.Visible = true; sheet.CustomMinimumSize = new Vector2(width, 0);
        // The board opens to the left of a panel moved near the left edge of the screen, so the panel makes room for it.
        if (ui.Size.X + LeftEdge - width - 56 is < 0 and float past) Shift(new Vector2(-past, 0));
        OffsetLeft = LeftEdge - width - 56;
        return sheet;
    }

    /// <summary>Hands over the board's long list, so <see cref="Fit"/> sizes its scroll area to the room left; without <paramref name="rest"/> the rest of the board is measured.</summary>
    /// <param name="hidesDrawer">Hides the backpack drawer while the list shows, so the board reaches down to the hotbar, for a list picked from by click.</param>
    public void ShowSheetRows(ScrollContainer scrollArea, Control rows, float? rest = null, bool hidesDrawer = false) { sheetScroll = scrollArea; sheetRows = rows; sheetRest = rest; sheetHidesDrawer = hidesDrawer; }

    /// <summary>Shows the window beside the panel and gives the column a page fills it with.</summary>
    public VBoxContainer ShowAside(float width) {
        asideShown = true; asideContent.CustomMinimumSize = new Vector2(width, 0);
        return asideContent;
    }

    /// <summary>Hands over the page's own long list, such as a recipe board's, so <see cref="Fit"/> lets it scroll in the room the rest of the page leaves rather than push that out of view.</summary>
    public void ShowPageRows(ScrollContainer scrollArea, Control rows) { pageScroll = scrollArea; pageRows = rows; }

    /// <summary>Sizes the panel's scroll area to what it shows, up to the room between the clock and the hotbar.</summary>
    public void Fit() {
        if (!Visible) return;
        Aside.Visible = asideShown;
        if (asideShown) { Aside.ResetSize(); Aside.Position = new Vector2(Math.Max(0, Position.X - Aside.Size.X - AsideGap), Position.Y); }
        float room = Math.Max(160, ui.Size.Y - Position.Y - 120);
        if (pageScroll != null) {
            float rest = inset.GetCombinedMinimumSize().Y - pageScroll.CustomMinimumSize.Y;
            float rows = Math.Max(Math.Min(MinPageRows, pageRows!.GetCombinedMinimumSize().Y), Math.Min(pageRows.GetCombinedMinimumSize().Y, room - rest));
            if (Math.Abs(pageScroll.CustomMinimumSize.Y - rows) > .5f) pageScroll.CustomMinimumSize = new Vector2(0, rows);
        }
        float want = Math.Min(inset.GetCombinedMinimumSize().Y, room);
        if (sheet.Visible && sheetScroll != null) {
            // The board reaches further left than the panel, over the backpack drawer, so it stops above the drawer.
            // The page shows the drawer after the list is handed over, so the list hides it here.
            if (sheetHidesDrawer && Bag.Visible) Bag.Visible = false;
            float clear = Bag.Visible && Bag.Position.Y > Position.Y ? Bag.Position.Y - Position.Y - 40 : room;
            float rest = sheetRest ?? sheet.GetCombinedMinimumSize().Y - sheetScroll.CustomMinimumSize.Y + 60;
            float rows = Math.Max(60, Math.Min(sheetRows!.GetCombinedMinimumSize().Y, Math.Min(room, clear) - rest));
            if (Math.Abs(sheetScroll.CustomMinimumSize.Y - rows) > .5f) sheetScroll.CustomMinimumSize = new Vector2(0, rows);
        }
        // The panel's bottom offset stays at its top, so it hugs this minimum size as it grows and shrinks.
        if (Math.Abs(scroll.CustomMinimumSize.Y - want) >= .5f) scroll.CustomMinimumSize = new Vector2(0, want);
    }
}
