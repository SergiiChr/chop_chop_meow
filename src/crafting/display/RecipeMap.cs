using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The Recipe map shows item chains by category, with arrows from materials to what they make.
/// A click on an item shows how it is made and what it is used for;
/// a click on a chip for a required tool or an additional resource goes to that item and lights it up for a moment.
/// The map is drawn afresh each time it opens. It keeps the game or Goal Log underneath.
/// </summary>
public partial class RecipeMap : Control {
    private readonly UiKit kit;
    private readonly World world;
    private readonly SidePanel panel;
    private readonly PanelContainer board;
    private readonly Dictionary<string, MapNode> nodes = new();
    private static readonly StyleBoxFlat LaneBack = Style.Flat(new Color("283c50"), 14, 12, horizontal: 14);
    private Action? leaveGoalLog;
    private VBoxContainer? mainView;
    private ItemTree? itemTree;
    private ScrollContainer rows = null!;
    private ScrollContainer detailsScroll = null!;
    private VBoxContainer details = null!;
    private const int DetailsWidth = 340;
    /// <summary>How long a jumped-to item stays lit, in seconds.</summary>
    public const float LitSeconds = 1.5f;
    public const string AltClickHint = "Alt-click: open the Recipe map";
    public const string ItemTreeHint = "Alt-click: open the item tree";

    public RecipeMap(UiKit kit, SidePanel panel) {
        this.kit = kit; this.panel = panel; world = kit.World;
        Name = "recipe_map"; Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The dimmed backdrop takes clicks, so none falls through to the world behind the map.
        var backdrop = new ColorRect { Color = new Color(Style.Line, .75f) }; backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);
        board = new PanelContainer(); board.AddThemeStyleboxOverride("panel", Style.Board);
        board.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 20); AddChild(board);
        world.Replaced += Close;
    }

    /// <summary>The item whose details show, or empty.</summary>
    public string Chosen { get; private set; } = "";

    /// <summary>An item's place on the map, or null while the map is closed.</summary>
    public MapNode? NodeOf(string item) => nodes.GetValueOrDefault(item);

    public void Open() => OpenAt("");

    /// <summary>Opens at the item's map entry, leaving any open panel or Goal Log underneath.</summary>
    public void OpenAt(string item, Action? leaveGoalLog = null) {
        item = EntryFor(item);
        if (item != "" && !world.Data.Items.ContainsKey(item)) return;
        Clear();
        this.leaveGoalLog = leaveGoalLog;
        var column = mainView = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); board.AddChild(column);
        column.AddChild(Header());
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10); column.AddChild(body);
        rows = new ScrollContainer { Name = "map_rows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var lanes = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lanes.AddThemeConstantOverride("separation", 8); rows.AddChild(lanes);
        foreach (var row in world.Data.MapRows) lanes.AddChild(Lane(row));
        body.AddChild(rows);
        var sidebar = new PanelContainer { Name = "map_details", CustomMinimumSize = new Vector2(DetailsWidth, 0) };
        sidebar.AddThemeStyleboxOverride("panel", Style.Well); body.AddChild(sidebar);
        detailsScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        sidebar.AddChild(detailsScroll);
        details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 16); detailsScroll.AddChild(details);
        Choose(item);
        Visible = true;
        if (item != "") JumpWhenReady(rows, item);
    }

    /// <summary>Waits for the map to size its cards and scroll area before putting the chosen item in view.</summary>
    private async void JumpWhenReady(ScrollContainer opened, string item) {
        opened.Modulate = Colors.Transparent;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!Visible || rows != opened) return;
        if (Chosen == item && nodes.TryGetValue(item, out var node)) {
            var offset = node.GetGlobalRect().GetCenter() - opened.GetGlobalRect().GetCenter();
            opened.ScrollHorizontal += Mathf.RoundToInt(offset.X);
            opened.ScrollVertical += Mathf.RoundToInt(offset.Y);
            node.Light(LitSeconds);
        }
        opened.Modulate = Colors.White;
    }

    /// <summary>Uses the map entry for a carried container whose exact contents or fill are not separate entries.</summary>
    private static string EntryFor(string item) {
        if (World.IsPacked(item)) return "crate";
        if (BerryJam.InBottle(item)) return BerryJam.Liquid;
        if (GameData.OffTheMap(item) && Water.IsBottle(item)) return Water.Full;
        return ContainerItem.Base(item);
    }

    public void Close() { Visible = false; Clear(); }

    /// <summary>Replaces the map's contents with the direct recipes of another item, within the same board.</summary>
    private void OpenItemTree(string item) {
        mainView!.Hide();
        RemoveItemTree();
        itemTree = new ItemTree(kit, EntryFor(item), BackToMap, Close, ShowOnMap, OpenItemTree);
        board.AddChild(itemTree);
    }

    /// <summary>Restores the main map exactly where the player left it.</summary>
    private void BackToMap() {
        RemoveItemTree();
        mainView!.Show();
    }

    /// <summary>Returns to the main map and selects the corresponding item.</summary>
    private void ShowOnMap(string item) {
        BackToMap();
        Choose(item);
        JumpWhenReady(rows, item);
    }

    private void RemoveItemTree() {
        if (itemTree == null) return;
        board.RemoveChild(itemTree); itemTree.QueueFree(); itemTree = null;
    }

    private void Clear() {
        foreach (var child in board.GetChildren()) { board.RemoveChild(child); child.QueueFree(); }
        nodes.Clear(); Chosen = ""; leaveGoalLog = null; mainView = null; itemTree = null;
    }

    /// <summary>The title, what the marks on the map mean, and the close button.</summary>
    private HBoxContainer Header() {
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 16);
        var glyph = Style.Glyph("line_map", 34, Style.Rim);
        glyph.SizeFlagsVertical = SizeFlags.ShrinkCenter; header.AddChild(glyph);
        var intro = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        intro.AddThemeConstantOverride("separation", 1); header.AddChild(intro);
        intro.AddChild(Style.Heading("Recipe map", 30));
        intro.AddChild(Style.Text("Follow the arrows to what each item makes. Alt-click to open its item tree.", 15, Style.Beige));
        header.AddChild(Legend(new MapSlot { Known = true }, "Made before"));
        header.AddChild(Legend(new MapSlot(), "Undiscovered"));
        header.AddChild(Legend(Sample(MapNode.ToolChip), "Required tool"));
        header.AddChild(Legend(Sample(MapNode.ResourceChip), "Additional resources"));
        header.AddChild(kit.HeaderButton("close", "Close · Esc", Close, Style.CloseRound, "map_close"));
        return header;
    }

    private static Panel Sample(StyleBox look) {
        var sample = new Panel(); sample.AddThemeStyleboxOverride("panel", look);
        return sample;
    }

    private static HBoxContainer Legend(Control sample, string words) {
        var entry = new HBoxContainer(); entry.AddThemeConstantOverride("separation", 6);
        sample.CustomMinimumSize = new Vector2(18, 18); sample.SizeFlagsVertical = SizeFlags.ShrinkCenter; entry.AddChild(sample);
        entry.AddChild(Style.Text(words, 14, Style.Beige));
        return entry;
    }

    /// <summary>One category's row: its name, then its items on a grid of steps, each on the line of the item it is made out of or on a line of its own below.</summary>
    private PanelContainer Lane(MapRowDef row) {
        var lane = new PanelContainer { Name = $"lane_{row.Title}" }; lane.AddThemeStyleboxOverride("panel", LaneBack);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 7); lane.AddChild(column);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 12); column.AddChild(heading);
        heading.AddChild(Style.Text(row.Title, 19, Style.Rim, Style.Head));
        heading.AddChild(new ColorRect {
            Color = new Color(Style.Beige, .24f),
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        });
        var tree = new MapTree(); column.AddChild(tree);
        int line = 0;
        foreach (string root in row.Roots) { Place(tree, root, 0, ref line); line++; }
        // An item that comes out of several others hangs under the first; the others reach it with a joining line.
        foreach (var (item, node) in nodes)
            if (node.GetParent() == tree && world.Data.Sources.TryGetValue(item, out var source))
                foreach (string other in source.From.Skip(1)) tree.Join(nodes[other], node);
        tree.CustomMinimumSize = new Vector2(tree.Columns * MapTree.Pitch - MapTree.Gap, line * MapNode.RowPitch);
        return lane;
    }

    /// <summary>Puts an item at a step and line, then what is made out of it one step on: the first on the same line, the rest on new lines below.</summary>
    private void Place(MapTree tree, string item, int step, ref int line) {
        var node = new MapNode(world, item, Choose, Jump, OpenItemTree) { Position = new Vector2(step * MapTree.Pitch, line * MapNode.RowPitch) };
        nodes[item] = node; tree.AddChild(node); tree.Columns = Math.Max(tree.Columns, step + 1);
        var made = world.Data.MadeOutOf(item);
        for (int i = 0; i < made.Count; i++) {
            if (i > 0) line++;
            Place(tree, made[i], step + 1, ref line);
            tree.Link(node, nodes[made[i]]);
        }
    }

    /// <summary>Scrolls to an item and lights it up for a moment.</summary>
    private void Jump(string item) {
        if (!nodes.TryGetValue(item, out var node)) return;
        rows.EnsureControlVisible(node);
        node.Light(LitSeconds);
    }

    /// <summary>Shows an item's details beside the map and rings it; an empty item shows what a click does.</summary>
    private void Choose(string item) {
        if (nodes.TryGetValue(Chosen, out var before)) before.Chosen = false;
        if (item != Chosen) detailsScroll.ScrollVertical = 0;
        Chosen = item;
        foreach (var child in details.GetChildren()) { details.RemoveChild(child); child.QueueFree(); }
        if (!nodes.TryGetValue(item, out var node)) {
            details.AddChild(Style.Text("Click an item for its recipe. Click a small ingredient or tool icon to find it on the map.",
                16, Style.Ink, wrap: true));
            return;
        }

        node.Chosen = true;
        if (!node.Known) {
            var undiscovered = DetailItem(item, 46, "detail_item");
            undiscovered.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            details.AddChild(undiscovered);
            details.AddChild(Style.Text("Discover this item to see its details.", 16, Style.Ink, wrap: true));
            return;
        }

        var data = world.Data;
        var recipe = data.RecipeOf(item);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); details.AddChild(header);
        header.AddChild(DetailItem(item, 46, "detail_item"));
        var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        identity.AddThemeConstantOverride("separation", 3); header.AddChild(identity);
        var name = Style.Text(data.Name(item), 23, Style.Ink, Style.Head, wrap: true);
        name.Name = "detail_name"; identity.AddChild(name);
        var known = Style.Pill("Made before", Style.Tag(Style.Sage), Style.SlotFill, 12);
        known.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; identity.AddChild(known);
        if (data.Items[item].Description is { Length: > 0 } said) {
            var text = Style.Text(said, 14, Style.Ink, wrap: true); text.Name = "detail_text"; details.AddChild(text);
        }

        var madeAt = Fact(details, recipe == null ? "Source" : "Made at");
        madeAt.AddChild(Style.Text(MadeAt(item), 16, Style.Ink, wrap: true));
        var needs = recipe != null
            ? recipe.Inputs.Select(p => (p.Key, $"{p.Value} "))
            : data.Sources[item].From.Concat(data.Sources[item].Also).Select(input => (input, ""));
        var requires = Fact(details, "Requires");
        foreach (var (input, amount) in needs) {
            var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 4);
            var icon = DetailItem(input, 28, $"requires_{input}");
            pair.AddChild(icon);
            var label = Style.Text(icon.Known ? amount + data.Name(input) : amount + MapItemLink.Unknown, 15, Style.Ink, wrap: true);
            icon.Discovered += () => label.Text = amount + data.Name(input);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill; pair.AddChild(label);
            requires.AddChild(pair);
        }
        if (requires.GetChildCount() == 1) requires.AddChild(Style.Text("No ingredients", 15, Style.Tan));
        var uses = data.UsedFor(item).ToList();
        var used = Flow(Fact(details, "Used for")); used.Name = "detail_uses";
        foreach (string use in uses) {
            var icon = DetailItem(use, 28, $"use_{use}");
            used.AddChild(icon);
            if (uses.Count != 1) continue;
            void AddName() => used.AddChild(Style.Text(data.Name(use), 15, Style.Ink));
            if (icon.Known) AddName(); else icon.Discovered += AddName;
        }
        if (uses.Count == 0) used.AddChild(Style.Text("—", 15, Style.Tan));
        if (recipe != null) details.AddChild(ShowButton(item, recipe));
    }

    /// <summary>A detail thumbnail keeps its icon size and also lets Alt-click follow that item.</summary>
    private MapItemLink DetailItem(string item, int size, string name) {
        var icon = new MapItemLink(world, item, ItemTreeHint) {
            Name = name,
            ExpandIcon = true,
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Center,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            RecipeLink = () => OpenItemTree(item)
        };
        Style.Skin(icon, Style.None, Style.None);
        icon.AddThemeConstantOverride("icon_max_width", size);
        Style.InkText(icon);
        icon.AddThemeFontOverride("font", Style.Head);
        return icon;
    }

    private static VBoxContainer Fact(VBoxContainer sidebar, string heading) {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2); sidebar.AddChild(column);
        column.AddChild(Style.Text(heading, 14, Style.Tan, Style.Head));
        return column;
    }

    private static HFlowContainer Flow(VBoxContainer column) {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 9); flow.AddThemeConstantOverride("v_separation", 2);
        column.AddChild(flow);
        return flow;
    }

    /// <summary>Where an item is made or come by, with the tool and the time a batch takes with the plainest one, by hand and at its station where the two differ.</summary>
    private string MadeAt(string item) {
        var data = world.Data;
        if (data.RecipeOf(item) is not RecipeDef r) return data.Sources[item].How;
        string where = MapNode.Where(data, r);
        string? plainest = r.Tool == "" ? null : data.PlainestTool(r.Tool, r.Tier, r.Station);
        string tool = plainest == null ? "" : $", {(world.Had(plainest) ? data.ToolName(r) : MapItemLink.Unknown)}";
        float byHand = r.Seconds / world.WorkRate(null, plainest), there = r.Seconds / world.WorkRate(r.Station, plainest);
        string time = r.Hand && r.Station is ThingKind station && byHand != there ? $"{byHand:0.#} s by hand, {there:0.#} s at the {data.Name(station).ToLowerInvariant()}" : $"{there:0.#} s";
        return $"{where}{tool} · {time}";
    }

    /// <summary>The button that leaves the map for the place a recipe is made: the nearest station of its kind, else the crafting menu for one made by hand.</summary>
    private Button ShowButton(string item, RecipeDef recipe) {
        var station = recipe.Station is ThingKind kind ? world.Nearest(kind, world.Player) : null;
        string id = world.Data.RecipeIdOf(item)!;
        bool byHand = station == null && recipe.Hand;
        string place = byHand ? "in the crafting menu" : $"at the {world.Data.Name(recipe.Station!.Value).ToLowerInvariant()}";
        var show = kit.Button($"Show {place}", () => { leaveGoalLog?.Invoke(); Close(); panel.ShowRecipe(station, id); }, disabled: station == null && !byHand);
        show.Name = "map_show"; show.SizeFlagsVertical = SizeFlags.ShrinkCenter; show.CustomMinimumSize = new Vector2(0, 44);
        if (show.Disabled) show.TooltipText = $"Build a {world.Data.Name(recipe.Station!.Value).ToLowerInvariant()} first"; else Style.SkinPill(show, Style.Sage);
        return show;
    }
}
