using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The recipe map: every item of the game on one screen, a row for each category, each item hanging off the one it is made out of.
/// Stations stand in a strip on top with what they cost. A click on an item shows how it is made and what it is used for;
/// a click on a chip for a required tool or an additional resource goes to that item and lights it up for a moment.
/// The map is drawn afresh each time it opens and holds the game while it shows.
/// </summary>
public partial class RecipeMap : Control {
    private readonly UiKit kit;
    private readonly World world;
    private readonly SidePanel panel;
    private readonly PanelContainer board;
    private readonly Dictionary<string, MapNode> nodes = new();
    private ScrollContainer rows = null!;
    private PanelContainer details = null!;
    private string chosen = "";
    /// <summary>How long a jumped-to item stays lit, in seconds.</summary>
    public const float LitSeconds = 1.5f;

    public RecipeMap(UiKit kit, SidePanel panel) {
        this.kit = kit; this.panel = panel; world = kit.World;
        Name = "recipe_map"; Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The dimmed backdrop takes clicks, so none falls through to the world behind the map.
        var backdrop = new ColorRect { Color = new Color(Style.Line, .55f) }; backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);
        board = new PanelContainer(); board.AddThemeStyleboxOverride("panel", Style.Board);
        board.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 20); AddChild(board);
        world.Replaced += Close;
        kit.HoldWhile(() => Visible);
    }

    /// <summary>The item whose details show, or empty.</summary>
    public string Chosen => chosen;

    /// <summary>An item's place on the map, or null while the map is closed.</summary>
    public MapNode? NodeOf(string item) => nodes.GetValueOrDefault(item);

    public void Open() {
        Clear();
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12); board.AddChild(column);
        column.AddChild(Header());
        column.AddChild(Stations());
        rows = new ScrollContainer { Name = "map_rows", SizeFlagsVertical = SizeFlags.ExpandFill };
        var lanes = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; lanes.AddThemeConstantOverride("separation", 8); rows.AddChild(lanes);
        foreach (var row in world.Data.MapRows) lanes.AddChild(Lane(row));
        column.AddChild(rows);
        details = new PanelContainer { Name = "map_details", CustomMinimumSize = new Vector2(0, 88) }; details.AddThemeStyleboxOverride("panel", Style.Well); column.AddChild(details);
        Choose("");
        Visible = true;
    }

    public void Close() { Visible = false; Clear(); }

    private void Clear() {
        foreach (var child in board.GetChildren()) { board.RemoveChild(child); child.QueueFree(); }
        nodes.Clear(); chosen = "";
    }

    /// <summary>The title, what the marks on the map mean, and the close button.</summary>
    private HBoxContainer Header() {
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 18);
        header.AddChild(Style.Glyph("line_map", 28, Style.Rim));
        var title = Style.Heading("Recipe map"); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
        header.AddChild(Legend(new MapCup { Known = true }, "Made before"));
        header.AddChild(Legend(new MapCup(), "Not made or built yet"));
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
        entry.AddChild(Style.Text(words, 12, Style.Beige));
        return entry;
    }

    /// <summary>The stations that make something, in the order the building plans list them, each with its cost; one not built yet is outlined.</summary>
    private HBoxContainer Stations() {
        var strip = new HBoxContainer { Name = "map_stations" }; strip.AddThemeConstantOverride("separation", 8);
        foreach (var (kind, def) in world.Data.Buildings) {
            if (!world.Data.Recipes.Values.Any(r => r.Station == kind)) continue;
            bool built = world.Reached(kind.Id());
            var chip = new PanelContainer { Name = $"station_{kind.Id()}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            chip.AddThemeStyleboxOverride("panel", built ? MapNode.StationBuilt : MapNode.StationPlanned);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); chip.AddChild(row);
            var icon = Style.Icon(Icons.Of(kind.Id()), 38); icon.Modulate = built ? Colors.White : Style.Unready; row.AddChild(icon);
            var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center }; words.AddThemeConstantOverride("separation", 0); row.AddChild(words);
            words.AddChild(Style.Text(def.Name, 13, built ? Style.Ink : Style.Rim));
            words.AddChild(Style.Text(PageText.Cost(world, def.Cost), 11, built ? Style.Tan : Style.Beige, wrap: true));
            strip.AddChild(chip);
        }
        return strip;
    }

    /// <summary>One category's row: its name, then its items on a grid of steps, each on the line of the item it is made out of or on a line of its own below.</summary>
    private PanelContainer Lane(MapRowDef row) {
        var lane = new PanelContainer { Name = $"lane_{row.Title}" }; lane.AddThemeStyleboxOverride("panel", MapNode.LaneBack);
        var across = new HBoxContainer(); lane.AddChild(across);
        var title = Style.Text(row.Title, 15, Style.Rim, Style.Head); title.CustomMinimumSize = new Vector2(120, MapNode.Height); title.SizeFlagsVertical = SizeFlags.ShrinkBegin; across.AddChild(title);
        var tree = new MapTree(); across.AddChild(tree);
        int line = 0;
        foreach (string root in row.Roots) { Place(tree, root, 0, ref line); line++; }
        // An item that comes out of several others hangs under the first; the others reach it with a joining line.
        foreach (var (item, node) in nodes)
            if (node.GetParent() == tree && world.Data.Sources.TryGetValue(item, out var source))
                foreach (string other in source.From.Skip(1)) tree.Join(nodes[other], node);
        tree.CustomMinimumSize = new Vector2(tree.Columns * MapTree.Pitch - MapTree.Gap, line * MapNode.Height);
        return lane;
    }

    /// <summary>Puts an item at a step and line, then what is made out of it one step on: the first on the same line, the rest on new lines below.</summary>
    private void Place(MapTree tree, string item, int step, ref int line) {
        var node = new MapNode(world, item, Choose, Jump) { Position = new Vector2(step * MapTree.Pitch, line * MapNode.Height) };
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

    /// <summary>Shows an item's details in the strip under the map and rings it; an empty item shows what a click does.</summary>
    private void Choose(string item) {
        if (nodes.TryGetValue(chosen, out var before)) before.Chosen = false;
        chosen = item;
        foreach (var child in details.GetChildren()) { details.RemoveChild(child); child.QueueFree(); }
        if (!nodes.TryGetValue(item, out var node)) { details.AddChild(Style.Text("Click an item to see how it is made and what it is used for. Click a chip on it to go to that tool or resource.", 14, Style.Ink)); return; }
        node.Chosen = true;
        var data = world.Data;
        var strip = new HBoxContainer(); strip.AddThemeConstantOverride("separation", 24); details.AddChild(strip);
        var cup = new PanelContainer { CustomMinimumSize = new Vector2(64, 64), SizeFlagsVertical = SizeFlags.ShrinkCenter }; cup.AddThemeStyleboxOverride("panel", MapNode.ChosenCup); cup.AddChild(Style.Icon(Icons.Of(item), 46)); strip.AddChild(cup);
        var naming = Column(strip, null);
        var name = Style.Text(data.Name(item), 19, Style.Ink, Style.Head); name.Name = "detail_name"; naming.AddChild(name);
        naming.AddChild(Style.Text(node.Known ? "Made before" : "Not made yet", 12, Style.Tan));
        var recipe = data.RecipeOf(item);
        var madeAt = new HBoxContainer(); madeAt.AddThemeConstantOverride("separation", 6); Column(strip, "Made at").AddChild(madeAt);
        if (recipe?.Station is ThingKind station) madeAt.AddChild(Style.Icon(Icons.Of(station.Id()), 28));
        madeAt.AddChild(Style.Text(MadeAt(item), 14, Style.Ink));
        var requires = new HBoxContainer(); requires.AddThemeConstantOverride("separation", 6);
        foreach (var (input, amount) in recipe?.Inputs ?? new()) { requires.AddChild(Style.Icon(Icons.Of(input), 28)); requires.AddChild(Style.Text($"{amount} {data.Name(input)}", 14, Style.Ink)); }
        if (recipe == null) foreach (string input in data.Sources[item].From.Concat(data.Sources[item].Also)) { requires.AddChild(Style.Icon(Icons.Of(input), 28)); requires.AddChild(Style.Text(data.Name(input), 14, Style.Ink)); }
        if (requires.GetChildCount() > 0) Column(strip, "Requires").AddChild(requires); else requires.QueueFree();
        var uses = data.UsedFor(item).ToList();
        var used = new HBoxContainer { Name = "detail_uses" }; used.AddThemeConstantOverride("separation", 4);
        foreach (string use in uses) { var icon = Style.Icon(Icons.Of(use), 28); icon.Name = $"use_{use}"; icon.TooltipText = data.Name(use); icon.MouseFilter = MouseFilterEnum.Stop; used.AddChild(icon); }
        if (uses.Count == 1) used.AddChild(Style.Text(data.Name(uses[0]), 14, Style.Ink));
        var usedColumn = Column(strip, uses.Count > 0 ? "Used for" : null); usedColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (uses.Count > 0) usedColumn.AddChild(used); else used.QueueFree();
        if (recipe != null) strip.AddChild(ShowButton(item, recipe));
    }

    /// <summary>A column of the details strip under a small heading.</summary>
    private static VBoxContainer Column(HBoxContainer strip, string? heading) {
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; column.AddThemeConstantOverride("separation", 4); strip.AddChild(column);
        if (heading != null) column.AddChild(Style.Text(heading, 12, Style.Tan));
        return column;
    }

    /// <summary>Where an item is made or come by, with the tool and the time a batch takes with the plainest one, by hand and at its station where the two differ.</summary>
    private string MadeAt(string item) {
        var data = world.Data;
        if (data.RecipeOf(item) is not RecipeDef r) return data.Sources[item].How;
        string where = MapNode.Where(data, r), tool = r.Tool == "" ? "" : $", {data.ToolName(r.Tool, r.Tier)}";
        float byHand = r.Seconds / world.WorkRate(null, null), there = r.Seconds / world.WorkRate(r.Station, null);
        string time = r.Hand && r.Station is ThingKind station && byHand != there ? $"{byHand:0.#} s by hand, {there:0.#} s at the {data.Name(station).ToLowerInvariant()}" : $"{there:0.#} s";
        return $"{where}{tool} · {time}";
    }

    /// <summary>The button that leaves the map for the place a recipe is made: the nearest station of its kind, else the crafting menu for one made by hand.</summary>
    private Button ShowButton(string item, RecipeDef recipe) {
        Thing? station = null;
        if (recipe.Station is ThingKind kind)
            foreach (var t in world.ThingsOf(kind)) if (station == null || t.Pos.DistanceTo(world.Player) < station.Pos.DistanceTo(world.Player)) station = t;
        string id = world.Data.Recipes.First(p => p.Value == recipe).Key;
        bool byHand = station == null && recipe.Hand;
        string place = byHand ? "in the crafting menu" : $"at the {world.Data.Name(recipe.Station!.Value).ToLowerInvariant()}";
        var show = kit.Button($"Show {place}", () => { Close(); panel.ShowRecipe(station, id); }, disabled: station == null && !byHand);
        show.Name = "map_show"; show.SizeFlagsVertical = SizeFlags.ShrinkCenter; show.CustomMinimumSize = new Vector2(0, 44);
        if (show.Disabled) show.TooltipText = $"Build a {world.Data.Name(recipe.Station!.Value).ToLowerInvariant()} first"; else Style.SkinPill(show, Style.Sage);
        return show;
    }
}

/// <summary>A row's items and the lines between them, laid out on a grid of steps across and lines down.</summary>
public partial class MapTree : Control {
    /// <summary>The room between an item and the next step, and from one step to the next.</summary>
    public const float Gap = 40, Pitch = MapNode.Width + Gap;
    /// <summary>How many steps across the row's longest chain runs.</summary>
    public int Columns { get; set; }
    private readonly List<(MapNode From, MapNode To)> links = new(), joins = new();

    /// <summary>Draws a line from an item to one made out of it.</summary>
    public void Link(MapNode from, MapNode to) => links.Add((from, to));

    /// <summary>Draws a line from another item something also comes out of into the line that reaches it.</summary>
    public void Join(MapNode from, MapNode to) => joins.Add((from, to));

    public override void _Draw() {
        foreach (var (from, to) in links) {
            float y = to.Position.Y + MapNode.Height / 2, end = to.Position.X - 2, trunk = to.Position.X - Gap / 2;
            // The first item made sits on its source's line and takes a straight line from where the source's words end; the others branch off that line's middle.
            if (to.Position.Y == from.Position.Y) DrawLine(new Vector2(from.Position.X + from.WordsEnd + 8, y), new Vector2(end - 5, y), Style.Beige, 2, true);
            else {
                DrawLine(new Vector2(trunk, from.Position.Y + MapNode.Height / 2), new Vector2(trunk, y), Style.Beige, 2, true);
                DrawLine(new Vector2(trunk, y), new Vector2(end - 5, y), Style.Beige, 2, true);
            }
            DrawColoredPolygon(new[] { new Vector2(end, y), new Vector2(end - 6, y - 4), new Vector2(end - 6, y + 4) }, Style.Beige);
        }
        foreach (var (from, to) in joins) {
            float y = from.Position.Y + MapNode.Height / 2, trunk = to.Position.X - Gap / 2;
            DrawLine(new Vector2(from.Position.X + from.WordsEnd + 8, y), new Vector2(trunk, y), Style.Beige, 2, true);
            DrawLine(new Vector2(trunk, y), new Vector2(trunk, to.Position.Y + MapNode.Height / 2), Style.Beige, 2, true);
        }
    }
}

/// <summary>A cup as the map draws it: filled for an item made before, a dashed outline for one not made yet.</summary>
public partial class MapCup : Control {
    public bool Known { get; set; }
    private static readonly StyleBoxFlat Unknown = new() { BgColor = new Color(Style.CupFill, .14f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };

    public override void _Draw() => Paint(this, new Rect2(Vector2.Zero, Size), Known);

    /// <summary>Paints a cup on any control.</summary>
    public static void Paint(Control on, Rect2 rect, bool known) {
        if (known) { on.DrawStyleBox(Style.CupNormal, rect); return; }
        on.DrawStyleBox(Unknown, rect);
        float corner = Math.Min(6, rect.Size.X / 4);
        Vector2 a = rect.Position, b = rect.End;
        on.DrawDashedLine(new Vector2(a.X + corner, a.Y + 1), new Vector2(b.X - corner, a.Y + 1), Style.Beige, 2, 4);
        on.DrawDashedLine(new Vector2(a.X + corner, b.Y - 1), new Vector2(b.X - corner, b.Y - 1), Style.Beige, 2, 4);
        on.DrawDashedLine(new Vector2(a.X + 1, a.Y + corner), new Vector2(a.X + 1, b.Y - corner), Style.Beige, 2, 4);
        on.DrawDashedLine(new Vector2(b.X - 1, a.Y + corner), new Vector2(b.X - 1, b.Y - corner), Style.Beige, 2, 4);
    }
}

/// <summary>
/// An item on the map: its cup, its name and where it is made, with a chip on the cup's corners for the tool its recipe requires
/// and for each additional resource it takes.
/// </summary>
public partial class MapNode : Control {
    public const float Width = 200, Height = 48;
    private const float Cup = 40, Chip = 22;
    /// <summary>Left edge of the chips, which lap over the cup's right edge.</summary>
    private const float ChipsLeft = Cup - 4;
    public static readonly StyleBoxFlat ToolChip = Round(Style.CupFill, Style.Mustard, Chip / 2, 2), ResourceChip = Round(Style.CupFill, Style.Line, Chip / 2, 2);
    public static readonly StyleBoxFlat ChosenCup = Round(Style.CupFill, Style.Mustard, 14, 3), LaneBack = Round(new Color(Style.Line, .5f), Colors.Transparent, 14, 0, 12, 10);
    public static readonly StyleBoxFlat StationBuilt = Round(Style.Beige, Colors.Transparent, 12, 0, 8, 6), StationPlanned = Round(Colors.Transparent, new Color(Style.Beige, .7f), 12, 2, 8, 6);
    private static readonly StyleBoxFlat ChosenRing = Round(Colors.Transparent, Style.Mustard, 14, 3), LitRing = Round(new Color(Style.Rim, .25f), Style.Rim, 16, 4);

    private readonly Label nameLabel, sourceLabel;
    /// <summary>Where the item's words start: after the cup, or after the chips beside it.</summary>
    private readonly float wordsStart;
    private bool chosen;
    private int lit;

    public string Item { get; }
    /// <summary>Whether the settlement has had this item before.</summary>
    public bool Known { get; }
    /// <summary>Whether the item is lit up after a jump to it.</summary>
    public bool Lit => lit > 0;
    public bool Chosen { set { chosen = value; QueueRedraw(); } }
    /// <summary>Where the item's words end, which the line to what is made out of it starts after.</summary>
    public float WordsEnd => wordsStart + Math.Max(nameLabel.GetMinimumSize().X, sourceLabel.GetMinimumSize().X);

    /// <param name="choose">Shows an item's details.</param>
    /// <param name="jump">Goes to another item and lights it up.</param>
    public MapNode(World world, string item, Action<string> choose, Action<string> jump) {
        var data = world.Data;
        Item = item; Known = world.Had(item); Name = $"map_{item}";
        CustomMinimumSize = Size = new Vector2(Width, Height); MouseFilter = MouseFilterEnum.Ignore;
        var cup = new Button { Name = "cup", Position = new Vector2(0, (Height - Cup) / 2), Size = new Vector2(Cup, Cup), Icon = Icons.Of(item), ExpandIcon = true, IconAlignment = HorizontalAlignment.Center, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = data.Name(item) };
        Style.Skin(cup, Style.None, Style.None); cup.AddThemeConstantOverride("icon_max_width", 30);
        if (!Known) { var faded = new Color(1, 1, 1, .55f); cup.AddThemeColorOverride("icon_normal_color", faded); cup.AddThemeColorOverride("icon_hover_color", faded); cup.AddThemeColorOverride("icon_pressed_color", faded); }
        cup.Pressed += () => choose(item);
        AddChild(cup);
        var recipe = data.RecipeOf(item);
        // Chips stand in a column beside the cup, the tool over the resources, so those of two lines never meet.
        bool tooled = recipe is { Tool.Length: > 0 };
        if (tooled) AddChild(ChipFor(data, data.PlainestTool(recipe!.Tool, recipe.Tier), "tool", ToolChip, 1, 0, "Required tool", jump));
        var also = recipe != null ? recipe.Inputs.Keys.Skip(1) : data.Sources[item].Also;
        int across = 0;
        foreach (string other in also) AddChild(ChipFor(data, other, "also", ResourceChip, Height - Chip - 1, across++, "Additional resource", jump));
        wordsStart = tooled || across > 0 ? ChipsLeft + Math.Max(1, across) * (Chip + 2) + 4 : Cup + 8;
        var words = new VBoxContainer { Position = new Vector2(wordsStart, 0), Size = new Vector2(Width - wordsStart, Height), Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        words.AddThemeConstantOverride("separation", -2); AddChild(words);
        nameLabel = Style.Text(data.Name(item), 13, Known ? Style.Rim : Style.Beige); words.AddChild(nameLabel);
        sourceLabel = Style.Text(recipe != null ? Where(data, recipe) : data.Sources[item].How, 11, Style.Beige); sourceLabel.Name = "source"; words.AddChild(sourceLabel);
    }

    /// <summary>Where a recipe is made, in a few words.</summary>
    public static string Where(GameData data, RecipeDef r) => r.Station is not ThingKind station ? "By hand" : r.Hand ? $"Hand or {data.Name(station).ToLowerInvariant()}" : data.Name(station);

    /// <summary>A round chip beside the cup that goes to the item it pictures.</summary>
    private Button ChipFor(GameData data, string other, string kind, StyleBoxFlat look, float top, int across, string role, Action<string> jump) {
        var chip = new Button { Name = $"{kind}_{other}", Position = new Vector2(ChipsLeft + across * (Chip + 2), top), Size = new Vector2(Chip, Chip), Icon = Icons.Of(other), ExpandIcon = true, IconAlignment = HorizontalAlignment.Center, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = $"{role}: {data.Name(other)}\nClick to go to it" };
        Style.Skin(chip, look, look); chip.AddThemeConstantOverride("icon_max_width", 14);
        chip.Pressed += () => jump(other);
        return chip;
    }

    /// <summary>Lights the item up for a while; a second jump starts the time again.</summary>
    public async void Light(float seconds) {
        lit++; QueueRedraw();
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;
        lit--; QueueRedraw();
    }

    public override void _Draw() {
        var cup = new Rect2(0, (Height - Cup) / 2, Cup, Cup);
        if (Lit) DrawStyleBox(LitRing, cup.Grow(5));
        MapCup.Paint(this, cup, Known);
        if (chosen) DrawStyleBox(ChosenRing, cup.Grow(2));
    }

    private static StyleBoxFlat Round(Color fill, Color edge, float radius, int border, int horizontal = 0, int vertical = 0) {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = edge, AntiAliasing = true, ContentMarginLeft = horizontal, ContentMarginRight = horizontal, ContentMarginTop = vertical, ContentMarginBottom = vertical };
        box.SetCornerRadiusAll((int)radius); box.SetBorderWidthAll(border);
        return box;
    }
}
