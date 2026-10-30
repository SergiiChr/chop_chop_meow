using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>What the side panel's pages keep while they are rebuilt, which they are on every change.</summary>
public sealed class PageMemory {
    /// <summary>Which job's worker list is open under its button.</summary>
    public string OpenJob = "";
    /// <summary>Whether the filter window is open beside the panel, whether its item list is shown, and which row that list adds to.</summary>
    public bool FilterOpen, FilterPicking, PickingOutput;
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

/// <summary>
/// The panel on the right: one page at a time under a shared header, with the board beside it for the price sheet or the filter window.
/// A page is built afresh whenever it opens, and <see cref="Live"/> holds what brings it up to date on each refresh.
/// </summary>
public partial class SidePanel : PanelContainer {
    /// <summary>Left edge of the panel and the clock above it, measured from the right of the screen.</summary>
    public const int Left = -476;
    /// <summary>Top of the panel, below the clock.</summary>
    private const int Top = 156;
    private readonly World world;
    private readonly UiState state;
    private readonly Control ui;
    private HBoxContainer frameHeading = null!;
    private TextureRect icon = null!;
    private Label title = null!;
    private ScrollContainer scroll = null!;
    /// <summary>The board left of the panel that holds the price sign or the filter window.</summary>
    private PanelContainer board = null!;
    /// <summary>The board's column.</summary>
    private VBoxContainer sheet = null!;
    private VBoxContainer content = null!;
    /// <summary>The price rows' scroll area, sized like the panel's to the room there is.</summary>
    private ScrollContainer? sheetScroll;
    private VBoxContainer? sheetRows;
    private (int, int) shownInspect;
    // Counts pages opened and closed, so a refresh notices when an update replaced the page under it.
    private int pagesOpened;

    public UiKit Kit { get; }
    public WorldView View { get; }
    public ItemMoves Moves { get; }
    public BackpackDrawer Bag { get; }
    public PageMemory Memory { get; } = new();
    public List<Action> Live { get; } = new();
    public HBoxContainer Heading => frameHeading;
    public TextureRect HeadingIcon => icon;
    public Label HeadingTitle => title;
    /// <summary>Whether the board beside the panel is showing.</summary>
    public bool SheetShown => sheet.Visible;
    public float ScreenWidth => ui.Size.X;

    public SidePanel(UiKit kit, WorldView view, ItemMoves moves, Control ui) {
        Kit = kit; View = view; Moves = moves; this.ui = ui; world = kit.World; state = kit.State;
        // The panel hugs what it shows, up to the room above the hotbar, and scrolls beyond that.
        AnchorLeft = 1; AnchorRight = 1; OffsetLeft = Left; OffsetRight = -20; OffsetTop = Top; OffsetBottom = Top;
        Bag = new BackpackDrawer(kit, Close);
    }

    public void Open(string name) {
        // A different page or building closes the job list and the filter window that belonged to the last one.
        if (name != state.Page || name == "inspect" && (state.Selected, state.WorkerId) != shownInspect) { Memory.OpenJob = ""; Memory.FilterOpen = false; Memory.FilterPicking = false; Bag.Dismissed = false; }
        shownInspect = (state.Selected, state.WorkerId);
        state.Page = name; Live.Clear(); pagesOpened++; Visible = true; View.PanelOpen = true;
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        Bag.Clear(); sheetScroll = null;
        OffsetLeft = Left;
        var frame = new HBoxContainer(); frame.AddThemeConstantOverride("separation", 20); AddChild(frame);
        board = new PanelContainer { Visible = false, SizeFlagsVertical = SizeFlags.ShrinkBegin }; frame.AddChild(board);
        sheet = new VBoxContainer { Visible = false }; sheet.AddThemeConstantOverride("separation", 12); board.AddChild(sheet);
        var outer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; outer.AddThemeConstantOverride("separation", 16); frame.AddChild(outer);
        frameHeading = new HBoxContainer(); frameHeading.AddThemeConstantOverride("separation", 12); outer.AddChild(frameHeading);
        icon = Style.Icon(null, 44); icon.Visible = false; icon.MouseFilter = MouseFilterEnum.Pass; frameHeading.AddChild(icon);
        title = Style.Heading(name switch { "welcome" => "A new beginning", "build" => "Building plans", "inventory" => "Backpack", "help" => "Field guide", "workers" => "Your crew", "craft" => "Crafting", _ => "Inspect" });
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill; frameHeading.AddChild(title);
        frameHeading.AddChild(Kit.HeaderButton("close", "Close · Esc", Close, Style.CloseRound, "close"));
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; outer.AddChild(scroll);
        // Room for the pills that sit on the cups' corners.
        var inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; inset.AddThemeConstantOverride("margin_right", 8); inset.AddThemeConstantOverride("margin_bottom", 8); scroll.AddChild(inset);
        content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; inset.AddChild(content);
        content.AddChild(name switch {
            "welcome" => new WelcomePage(this),
            "help" => new GuidePanel(this),
            "build" => new PlansPanel(this),
            "inventory" => new BackpackPage(this),
            "workers" => new CrewPanel(this),
            "craft" => new CraftPanel(this),
            _ => new InspectPage(this)
        });
        Kit.Refresh();
    }

    public void Close() { Visible = false; Bag.Visible = false; View.PanelOpen = false; state.Page = ""; Live.Clear(); pagesOpened++; }

    /// <summary>Runs the open page's updates.</summary>
    public void Refresh() {
        // An update can open another page, which replaces the list; the old page's remaining updates are then dropped.
        int opened = pagesOpened;
        for (int i = 0; i < Live.Count && opened == pagesOpened; i++) Live[i]();
    }

    /// <summary>Selects a building or resource and shows it.</summary>
    public void Select(Thing t) {
        if (t.Kind == ThingKind.Reinforced && !(state.Page == "inspect" && state.Selected == t.Id)) Kit.Sounds.Play("open_steel_smelter", t.Pos);
        state.Selected = t.Id; state.WorkerId = 0; View.Selected = t.Id; View.SelectedWorker = 0; View.BuildMode = null; Open("inspect");
    }

    /// <summary>Selects a worker and shows them.</summary>
    public void Select(Worker w) { state.WorkerId = w.Id; state.Selected = 0; View.Selected = 0; View.SelectedWorker = w.Id; Open("inspect"); }

    /// <summary>Enters placement for a crate from the backpack.</summary>
    public void PlaceCrate(string item) {
        if (world.S.Backpack[item] == 0) { Kit.Notify("No empty crate in your backpack. Craft one at a bench."); return; }
        state.PlacingCrate = item; View.BuildMode = ThingKind.Crate; state.AssigningWorker = 0; Close();
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
        if (!Visible || !IsInstanceValid(scroll)) return;
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
