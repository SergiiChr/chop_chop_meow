using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A list of recipe cards under a heading for each group of what they make, which closes and opens on a click.
/// Each card shows the product, its name and its inputs, each count in brick while the backpack is short, or the tool it lacks.
/// By hand a click queues a batch. At a station a click marks the recipe workers keep making, and a double-click queues a batch and loads it from the backpack.
/// Shift queues 5 and Ctrl 10, at a station loading them too; a right-click takes as many waiting batches off.
/// </summary>
/// <param name="station">Null when crafting by hand.</param>
/// <param name="openOn">A recipe whose group the board opens, for a page opening rather than drawing again.</param>
public partial class RecipeBoard(UiKit kit, LiveUpdates live, PageMemory memory, SidePanel panel, Action redraw, Thing? station, string? openOn) : VBoxContainer {
    private readonly World world = kit.World;

    /// <summary>How far the worker mark's hard hat stands out over the top of a card.</summary>
    private const int HatRise = 18;

    /// <summary>How many batches a click queues or takes off: 10 with Ctrl, 5 with Shift, else 1.</summary>
    public static int Step => Input.IsKeyPressed(Key.Ctrl) ? 10 : Input.IsKeyPressed(Key.Shift) ? 5 : 1;

    private string Hint => station != null
        ? "Click: marks as a recurring recipe for the worker.\nDouble-click: queue 1.\nShift/Ctrl-click: queue 5/10.\nRight-click: undo 1/5/10 respectively."
        : "Click: queue 1.\nShift/Ctrl-click: queue 5/10.\nRight-click: undo 1/5/10 respectively.";

    /// <summary>Seconds a batch takes here with the tool at hand.</summary>
    private float Seconds(RecipeDef r) => r.Seconds / world.WorkRate(station?.Kind, world.ToolInUse(station, r.Tool, r.Tier));

    /// <summary>The groups' headings and cards in a list that scrolls once it outgrows the room the page leaves it.</summary>
    public override void _Ready() {
        var recipes = world.RecipesAt(station).ToList();
        if (openOn != null) memory.ClosedGroups.Remove(world.Data.Recipes[openOn].Group);
        var list = new ScrollContainer { Name = "recipe_list", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        // Room at the right for the scroll bar and the pills on the cards' corners.
        var room = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; room.AddThemeConstantOverride("margin_right", 12);
        var groups = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; groups.AddThemeConstantOverride("separation", 6);
        AddChild(list); list.AddChild(room); room.AddChild(groups);
        panel.ShowPageRows(list, room);
        foreach (var group in Enum.GetValues<RecipeGroup>()) {
            var ids = recipes.Where(id => world.Data.Recipes[id].Group == group).ToList();
            if (ids.Count == 0) continue;
            bool open = !memory.ClosedGroups.Contains(group);
            groups.AddChild(Heading(group, ids, open));
            if (!open) continue;
            // Two cards a row; an odd last card keeps its half beside an empty one.
            var cards = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; cards.AddThemeConstantOverride("separation", station != null ? 12 : 8);
            groups.AddChild(cards);
            for (int i = 0; i < ids.Count; i += 2) {
                var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation", 8); cards.AddChild(row);
                row.AddChild(Card(ids[i]));
                row.AddChild(i + 1 < ids.Count ? Card(ids[i + 1]) : new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            }
        }
    }

    /// <summary>A group's heading: an arrow, its name and how many recipes it holds; a closed one that hides the recipe workers keep making wears their hard hat.</summary>
    private Button Heading(RecipeGroup group, List<string> ids, bool open) {
        var heading = kit.Button("", () => { if (!memory.ClosedGroups.Remove(group)) memory.ClosedGroups.Add(group); redraw(); });
        heading.Name = $"group_{group}"; heading.TooltipText = open ? "Click to close this group" : "Click to open this group";
        Style.Skin(heading, Style.None, Style.None);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); row.AddThemeConstantOverride("separation", 8);
        heading.AddChild(row);
        row.AddChild(new Chevron { Open = open, CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(Style.Text(group.ToString(), 15, Style.Beige, Style.Head));
        var count = Style.Pill(ids.Count.ToString(), Style.CountPill, Style.Beige, 11); count.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(count);
        if (!open && station != null && ids.Contains(station.Keep)) {
            var hat = Style.Icon(Icons.Of("hardhat"), 22); hat.Name = "kept"; hat.SizeFlagsVertical = SizeFlags.ShrinkCenter; row.AddChild(hat);
        }
        row.AddChild(new ColorRect { Color = new Color(Style.Beige, .3f), CustomMinimumSize = new Vector2(0, 2), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        return heading;
    }

    /// <summary>A recipe card: the product with what a batch makes on a pill, its name, and under it its inputs or the tool it lacks; the queued count sits at the top right.</summary>
    private ItemSlot Card(string id) {
        var r = world.Data.Recipes[id];
        var (cell, _) = ItemSlot.Cup($"craft_{id}");
        cell.CustomMinimumSize = new Vector2(0, 64); cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var face = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 8); face.AddThemeConstantOverride("separation", 8); cell.AddChild(face);
        var product = Style.Icon(Icons.Of(r.Output), 42); product.Name = "product"; product.SizeFlagsVertical = SizeFlags.ShrinkCenter; face.AddChild(product);
        if (r.Amount > 1) product.AddChild(Style.Corner(Style.Pill($"×{r.Amount}", Style.BeigePill, Style.Ink, 11), 1, 1, 5));
        var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        text.AddThemeConstantOverride("separation", 1); face.AddChild(text);
        var name = Style.Text(world.Data.Name(r.Output), 13, Style.Ink, wrap: true); name.MaxLinesVisible = 2; text.AddChild(name);
        var inputs = new HBoxContainer { Name = "inputs", MouseFilter = MouseFilterEnum.Ignore }; inputs.AddThemeConstantOverride("separation", 3); text.AddChild(inputs);
        foreach (var (item, amount) in r.Inputs) {
            inputs.AddChild(Style.Icon(Icons.Of(item), 18));
            var need = Style.Text(amount.ToString(), 13, Style.Ink); need.CustomMinimumSize = new Vector2(14, 0);
            inputs.AddChild(need);
            // Counts turn brick while the backpack is short.
            live.Watch(() => world.Backpack[item] < amount, lacking => need.AddThemeColorOverride("font_color", lacking ? Style.Brick : Style.Ink));
        }
        var lacks = Style.Text(r.Tool == "" ? "" : $"No {world.Data.ToolName(r.Tool, r.Tier)}", 12, Style.Brick); lacks.Name = "lacks"; lacks.Visible = false; text.AddChild(lacks);
        bool NoTool() => r.Tool != "" && world.ToolInUse(station, r.Tool, r.Tier) == null;
        // One look for the tool a refresh: the card names a missing tool in place of its inputs, and its picture fades while the recipe cannot be made from the backpack now.
        live.Watch(() => { bool missing = NoTool(); return (Missing: missing, Ready: !missing && (station != null ? world.Backpack.Has(r.Inputs) : world.CanCraft(id))); }, state => {
            lacks.Visible = state.Missing; inputs.Visible = !state.Missing;
            product.Modulate = state.Ready ? Colors.White : Style.Faded;
        });
        var queued = ItemSlot.CountBadge(1, 0); queued.Name = "queued"; cell.AddChild(queued);
        live.Watch(() => world.Waiting(station, id), waiting => queued.Text = waiting > 0 ? waiting.ToString() : "");
        if (station != null) {
            // The worker mark: a thick outline with a hard hat on top.
            var hat = Style.Corner(Style.Icon(Icons.Of("hardhat"), 34), .5f, 0, HatRise); hat.Name = "kept"; cell.AddChild(hat);
            live.Watch(() => station.Keep == id, kept => {
                Style.Skin(cell, kept ? Style.PlateKept : Style.PlateNormal, kept ? Style.PlateKept : Style.PlateHover);
                hat.Visible = kept;
            });
        } else Style.Skin(cell, Style.PlateNormal, Style.PlateHover);
        // The second press of a double-click undoes what its first press did, then queues one batch with its inputs.
        bool doubled = false;
        cell.Pressed += () => {
            if (doubled) { doubled = false; return; }
            string kept = memory.KeptBefore = station?.Keep ?? "";
            int step = Step;
            if (station == null) kit.Run(() => world.Enqueue(null, id, step));
            else if (step > 1) kit.Run(() => world.QueueBatch(station, id, step));
            else kit.Run(() => world.SetRecipe(station, kept == id ? "" : id));
        };
        cell.DoubleClicked = () => {
            doubled = true;
            if (station == null) return;
            string kept = memory.KeptBefore;
            kit.Run(() => { world.SetRecipe(station, kept); world.QueueBatch(station, id); });
        };
        cell.RightClicked = () => kit.Run(() => world.Enqueue(station, id, -Step));
        void Describe(StringBuilder text) {
            text.Append(r.Name);
            if (r.Amount > 1) text.Append($" ×{r.Amount}");
            text.Append("\nRequires: "); PageText.AppendNeeds(world, text, r.Inputs);
            text.Append($"\n{Seconds(r):0.#} s\n").Append(Hint);
        }
        cell.Popup = () => new RecipePopup(world, station, r, Seconds(r), Hint);
        live.Bind(shown => cell.TooltipText = shown, Describe);
        return cell;
    }
}

/// <summary>A small arrow that points down beside an open group and right beside a closed one.</summary>
public partial class Chevron : Control {
    public bool Open { get; set; }

    public override void _Draw() {
        var mid = Size / 2;
        float reach = Math.Min(Size.X, Size.Y) * .32f;
        var points = Open
            ? new[] { mid + new Vector2(-reach, -reach / 2), mid + new Vector2(0, reach / 2), mid + new Vector2(reach, -reach / 2) }
            : new[] { mid + new Vector2(-reach / 2, -reach), mid + new Vector2(reach / 2, 0), mid + new Vector2(-reach / 2, reach) };
        DrawPolyline(points, Style.Beige, 2.5f, true);
    }
}

/// <summary>
/// A recipe's hover popup: its name, what it requires with the amount the backpack is short of, where its tool is, how long it takes,
/// what its product is used for and what clicks do.
/// </summary>
public partial class RecipePopup(World world, Thing? station, RecipeDef r, float seconds, string hint) : VBoxContainer {
    /// <summary>Most uses the popup pictures before it counts the rest.</summary>
    private const int MaxUses = 5;

    public override void _Ready() {
        AddThemeConstantOverride("separation", 6);
        AddChild(Style.Text($"{r.Name}{(r.Amount > 1 ? $" ×{r.Amount}" : "")}", 17, Style.Rim, Style.Head));
        var requires = Line("Requires");
        foreach (var (item, amount) in r.Inputs) {
            requires.AddChild(Style.Icon(Icons.Of(item), 26));
            requires.AddChild(Style.Text($"{amount} {world.Data.Name(item)}", 14, Style.Rim));
            if (world.Backpack[item] < amount) requires.AddChild(Style.Text($"you have {world.Backpack[item]}", 14, Style.Warn));
        }
        if (r.Tool != "") ToolLine();
        Line("Time").AddChild(Style.Text($"{seconds:0.#} s", 14, Style.Rim));
        var uses = world.Data.UsedFor(r.Output).ToList();
        if (uses.Count > 0) {
            var used = Line("Used for"); used.Name = "used_for";
            foreach (string use in uses.Take(MaxUses)) { var icon = Style.Icon(Icons.Of(use), 26); icon.Name = $"use_{use}"; used.AddChild(icon); }
            used.AddChild(Style.Text(uses.Count == 1 ? world.Data.Name(uses[0]) : uses.Count > MaxUses ? $"and {uses.Count - MaxUses} more" : "", 14, Style.Rim));
        }
        AddChild(new HSeparator());
        AddChild(Style.Text(hint, 12, Style.Beige));
    }

    /// <summary>A row with its name on the left and what it holds flowing beside it.</summary>
    private HFlowContainer Line(string name) {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); AddChild(row);
        var key = Style.Text(name, 12, Style.Beige); key.CustomMinimumSize = new Vector2(62, 30); row.AddChild(key);
        var value = new HFlowContainer { CustomMinimumSize = new Vector2(250, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        value.AddThemeConstantOverride("h_separation", 6); value.AddThemeConstantOverride("v_separation", 2); row.AddChild(value);
        return value;
    }

    /// <summary>
    /// Where the recipe's tool is: green when it is ready to work with, slotted at a station or carried by hand;
    /// yellow when a station's slot is empty and the player carries it, which works for them but not for a worker; red when there is none.
    /// </summary>
    private void ToolLine() {
        string? slotted = station != null ? world.BenchTool(station, r.Tool, r.Tier) : null, carried = world.BestTool(r.Tool, null, r.Tier);
        string? ready = station != null ? slotted : carried;
        string shown = ready ?? carried ?? world.Data.PlainestTool(r.Tool, r.Tier);
        var line = Line("Tool"); line.Name = "tool_line";
        line.AddChild(Style.Icon(Icons.Of(shown), 26));
        line.AddChild(Style.Text(world.Data.Name(shown), 14, Style.Rim));
        var state = ready != null ? Style.Text("available", 14, Style.Ready) : carried != null ? Style.Text("in your backpack", 14, Style.Held) : Style.Text("not available", 14, Style.Warn);
        state.Name = "tool_state"; line.AddChild(state);
        // A note under the line takes its whole width: who cannot use a carried tool, or how to come by a missing one that needs no tool itself.
        string hint = ready != null ? "" : carried != null ? "Workers can't use it until it is slotted."
            : world.Data.RecipeOf(shown) is { Hand: true, Tool: "" } making ? $"Make one by hand: {PageText.Cost(world, making.Inputs)}" : "";
        if (hint != "") { var note = Style.Text(hint, 12, Style.Beige); note.Name = "tool_hint"; note.CustomMinimumSize = new Vector2(250, 0); line.AddChild(note); }
    }
}

/// <summary>What waits in a queue, one cup per recipe in the order first queued; a right-click takes 1, 5 or 10 batches off.</summary>
/// <param name="station">The station whose queue it shows, or null for the hand's; the page draws it again when the queue's recipes change.</param>
public partial class QueueStrip(UiKit kit, LiveUpdates live, Thing? station) : HBoxContainer {
    public override void _Ready() {
        var world = kit.World;
        var queue = world.QueueOf(station);
        Name = "queue";
        AddThemeConstantOverride("separation", 10);
        if (queue.Count == 0) return;
        AddChild(Style.Text("Next", 14, Style.Beige));
        foreach (var q in queue) {
            var r = kit.World.Data.Recipes[q.Recipe];
            var (cell, count) = ItemSlot.Cup($"queued_{q.Recipe}", 44); cell.Icon = Icons.Of(r.Output);
            cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
            string id = q.Recipe;
            live.Watch(() => world.Waiting(station, id), n => { count.Text = n.ToString(); cell.TooltipText = $"{r.Name} · {n} waiting\nRight-click: take 1, 5 or 10 off"; });
            cell.RightClicked = () => kit.Run(() => world.Enqueue(station, id, -RecipeBoard.Step));
            AddChild(cell);
        }
    }
}
