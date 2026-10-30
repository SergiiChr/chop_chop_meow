using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Pieces every panel is built from, so each screen looks and works alike.</summary>
public partial class Game {
    /// <summary>Which job's worker list is open under its button, so a rebuilt page keeps it open.</summary>
    private string openJob = "";
    /// <summary>Whether the filter window is open beside the panel, and whether its item list is shown.</summary>
    private bool filterOpen, filterPicking;
    private static readonly Dictionary<string, string> JobNames = new() {
        ["gather"] = "Gather",
        ["bellows"] = "Bellows",
        ["haul"] = "Haul",
        ["craft"] = "Craft",
        ["deliveries"] = "Manage deliveries",
        ["fuel"] = "Tend fire",
        ["farm"] = "Farm",
        ["build"] = "Build",
        ["mine"] = "Mine",
        ["stock"] = "Stock",
        ["tend"] = "Tend",
        ["work"] = "Work"
    };
    private static string JobName(string job) => JobNames.GetValueOrDefault(job, job);

    /// <summary>Puts a round button in the header, before the close button.</summary>
    private Button HeaderAction(string glyph, string tooltip, Action action, (StyleBoxFlat Normal, StyleBoxFlat Hover) look, string name = "") {
        var button = kit.HeaderButton(glyph, tooltip, action, look, name);
        heading.AddChild(button); heading.MoveChild(button, heading.GetChildCount() - 2);
        return button;
    }

    /// <summary>The funnel that opens the filter window beside the panel, ringed while it is open.</summary>
    private void FilterButton(Thing t) {
        var button = HeaderAction("filter", "Filters: what workers bring and take", () => { filterOpen = !filterOpen; filterPicking = false; OpenPage("inspect"); }, Style.FilterRound, "filters");
        if (filterOpen) { Style.Skin(button, Style.Round(Style.Mustard, 18).Item1, Style.FilterRound.Hover); FilterWindow(t); }
    }

    /// <summary>A beige well inside the panel, holding a row or column of things.</summary>
    private T Well<T>(T inner) where T : Control {
        var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); content.AddChild(well);
        well.AddChild(inner);
        return inner;
    }

    /// <summary>A label under a cup: what one batch takes or makes.</summary>
    private static Label PerBatch(string text) {
        var label = Style.Text(text, 13, Style.Tan); label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    /// <summary>Seconds as a short duration: seconds up to a minute and a half, then minutes, then hours.</summary>
    private static System.Text.StringBuilder AppendDuration(System.Text.StringBuilder text, float seconds) {
        if (seconds < 90) return text.Append((int)MathF.Ceiling(seconds)).Append(" s");
        if (seconds < 5400) return text.Append((int)MathF.Round(seconds / 60)).Append(" min");
        return text.Append((int)MathF.Round(seconds / 3600)).Append(" h");
    }

    /// <summary>
    /// The process row every station shares: inputs with what one batch takes under each, the batch time over the progress bar,
    /// the fuel under it with how long it lasts, and the main output with its side outputs in a second row.
    /// </summary>
    private void Process(IEnumerable<Control> inputs, string time, Func<float> progress, Control? fuel, Control? main, IEnumerable<Control> side) {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 14);
        var left = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; left.AddThemeConstantOverride("separation", 12); row.AddChild(left);
        foreach (var input in inputs) left.AddChild(input);
        var middle = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; middle.AddThemeConstantOverride("separation", 8); row.AddChild(middle);
        var clock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; clock.AddThemeConstantOverride("separation", 4); middle.AddChild(clock);
        clock.AddChild(Style.Glyph("line_clock", 16, Style.Ink)); clock.AddChild(Style.Text(time, 14, Style.Ink));
        var bar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(60, 14), Name = "progress" };
        middle.AddChild(bar);
        // Redrawn only when the bar moves a pixel's worth.
        int drawn = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(bar)) return;
            int at = (int)(progress() * 200);
            if (at == drawn) return;
            drawn = at; bar.Value = at / 200f;
        });
        if (fuel != null) { fuel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter; middle.AddChild(fuel); }
        var right = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; right.AddThemeConstantOverride("separation", 10); row.AddChild(right);
        if (main != null) right.AddChild(main);
        var extras = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; extras.AddThemeConstantOverride("separation", 10); right.AddChild(extras);
        foreach (var output in side) extras.AddChild(output);
        extras.Visible = extras.GetChildCount() > 0;
    }

    /// <summary>The fuel position under a process bar: an icon for what it is, its cup or picture, and how long it lasts.</summary>
    private static HBoxContainer FuelRow(string glyph, Color tint, Control cup, Label lasts) {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Style.Glyph(glyph, 24, tint)); row.AddChild(cup); row.AddChild(lasts);
        return row;
    }

    /// <summary>
    /// A place's job buttons, side by side along the bottom: each shows how many workers do it, and opens a list of workers to tick.
    /// Each worker shows how many jobs they have elsewhere, listed on hover.
    /// </summary>
    private void JobButtons(Thing t) {
        var jobs = world.Jobs(t);
        if (jobs.Length == 0) return;
        var row = Row(); row.AddThemeConstantOverride("separation", 12);
        for (int i = 0; i < jobs.Length; i++) {
            string job = jobs[i];
            var look = Style.Pill(Style.JobColors[i % Style.JobColors.Length]);
            var button = new Button { Name = $"job_{job}", CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, ToggleMode = true, ButtonPressed = openJob == job };
            Style.Skin(button, look.Item1, look.Item2, look.Item2);
            button.TooltipText = JobHint(t, job);
            var inner = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore }; inner.AddThemeConstantOverride("separation", 8);
            inner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); button.AddChild(inner);
            inner.AddChild(Style.Text(JobName(job), 16, Style.Rim, Style.Head));
            var count = Style.Pill("0", Style.BeigePill, Style.Line, 13); count.CustomMinimumSize = new Vector2(24, 24); inner.AddChild(count);
            // The label sits on the button rather than in it, so the button is widened to fit it.
            button.CustomMinimumSize = new Vector2(inner.GetCombinedMinimumSize().X + 32, 48);
            int shown = -1;
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(count)) return;
                int n = 0;
                foreach (var w in world.S.Workers) if (World.HasDuty(w, t.Id, job)) n++;
                if (n == shown) return;
                shown = n; count.Text = n.ToString();
            });
            button.Pressed += () => { openJob = openJob == job ? "" : job; OpenPage("inspect"); };
            row.AddChild(button);
        }
        if (openJob != "" && jobs.Contains(openJob)) WorkerList(t, openJob);
    }

    private string JobHint(Thing t, string job) => job switch {
        "bellows" => "Keeps the fire hot. Only one worker pumps at a time.",
        "haul" => t.Kind switch { ThingKind.Farm => "Brings seeds and fertilizer and carries the harvest to storage.", ThingKind.Mine => "Brings the timber, iron and stone the mine needs.", _ => "Brings ingredients and fuel and carries what is made to storage." },
        "craft" => "Works the station; it only makes progress while someone works it.",
        "deliveries" => "Fetches fresh prices on request and carries deliveries to storage.",
        "fuel" => "Keeps the fire fed with wood from storage.",
        "farm" => "Harvests the ripe wheat.",
        "build" => "Digs the entrance and fits the frame. Needs a pickaxe, then a hammer.",
        "mine" => $"Mines ore and stone underground with a pickaxe. Up to {world.Data.Mine.Workers} workers.",
        "stock" => "Brings tools from storage to the rack.",
        "tend" => "Brings what the compost takes from storage and carries fertilizer back.",
        _ => "Gathers and carries to storage."
    };

    /// <summary>Every worker, ticked when they do this job here; the circle counts their jobs elsewhere.</summary>
    private void WorkerList(Thing t, string job) {
        var list = new VBoxContainer(); list.AddThemeConstantOverride("separation", 2);
        var box = new PanelContainer(); box.AddThemeStyleboxOverride("panel", Style.Dropdown); box.AddChild(list); content.AddChild(box);
        if (world.S.Workers.Count == 0) { list.AddChild(Style.Text("Hire villagers at a trading post.", 14, Style.Ink)); return; }
        foreach (var w in world.S.Workers) {
            var worker = w;
            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8); list.AddChild(line);
            var tick = new CheckBox { Text = w.Name, ButtonPressed = World.HasDuty(w, t.Id, job), FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Name = $"duty_{w.Id}" };
            tick.AddThemeColorOverride("font_color", Style.Ink); tick.AddThemeColorOverride("font_hover_color", Style.Ink); tick.AddThemeColorOverride("font_pressed_color", Style.Ink); tick.AddThemeColorOverride("font_hover_pressed_color", Style.Ink);
            tick.AddThemeFontOverride("font", Style.Bold);
            // A full mine refuses the worker, so the box shows what actually happened.
            tick.Toggled += on => { world.SetDuty(worker, t, job, on); tick.SetPressedNoSignal(World.HasDuty(worker, t.Id, job)); RefreshLive(); };
            line.AddChild(tick);
            var elsewhere = w.Duties.Where(d => d.Thing != t.Id).ToList();
            if (elsewhere.Count == 0) continue;
            var circle = Style.Pill(elsewhere.Count.ToString(), Style.Round(Style.Enamel, 11).Item1, Style.Rim, 12);
            circle.CustomMinimumSize = new Vector2(22, 22); circle.MouseFilter = Control.MouseFilterEnum.Pass;
            circle.TooltipText = string.Join("\n", elsewhere.Select(d => $"{(world.DutyPlace(w, d.Thing) is Thing at ? world.Data.Name(at.Kind) : "?")} · {JobName(d.Job).ToLowerInvariant()}"));
            line.AddChild(circle);
        }
    }

    /// <summary>The board beside the panel, for the price sign or the filter window; it hangs left of the panel.</summary>
    private VBoxContainer SideBoard(StyleBox look, float width) {
        side.AddThemeStyleboxOverride("panel", look);
        side.Visible = true; sheet.Visible = true; sheet.CustomMinimumSize = new Vector2(width, 0);
        panel.OffsetLeft = PanelLeft - width - 56;
        return sheet;
    }

    /// <summary>
    /// The one filter window for any storage that has filters: a row of what workers bring and one of what they take.
    /// Drop an item on a row to add it, click one to take it off; an empty row allows anything.
    /// A crate of a good the village buys can be named too; a trading post has only the row of crates workers bring, and an empty one takes no deliveries.
    /// </summary>
    private void FilterWindow(Thing t) {
        var board = SideBoard(Style.Board, 340);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 10); board.AddChild(top);
        top.AddChild(Style.Glyph("line_filter", 28, Style.Rim));
        var title = Style.Heading("Filters", 22); title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(title);
        top.AddChild(Style.Icon(Icons.Of(t.Kind.Id()), 32));
        top.AddChild(kit.HeaderButton("close", "Close the filters", () => { filterOpen = false; filterPicking = false; OpenPage("inspect"); }, Style.CloseRound, "filters_close"));
        bool post = t.Kind == ThingKind.Signpost;
        foreach (bool output in post ? new[] { false } : new[] { false, true }) {
            var filter = output ? t.Inventory.OutputFilter : t.Inventory.InputFilter;
            var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); board.AddChild(well);
            var row = new HFlowContainer { Name = output ? "filter_out" : "filter_in" }; row.AddThemeConstantOverride("h_separation", 10); row.AddThemeConstantOverride("v_separation", 10); well.AddChild(row);
            var glyph = Style.Glyph(output ? "line_out" : "line_in", 30, Style.Ink); glyph.MouseFilter = Control.MouseFilterEnum.Pass;
            glyph.TooltipText = output ? "Workers take only these from here" : post ? "Workers bring these crates here to sell" : "Workers bring only these here";
            row.AddChild(glyph);
            foreach (string item in filter.OrderBy(FilterName)) {
                var cell = FilterCell($"filter_{(output ? "out" : "in")}_{item}", item); cell.TooltipText = $"{FilterName(item)}\nClick to take it off";
                string id = item;
                cell.Pressed += () => { world.SetFilter(t, id, output, false); OpenPage("inspect"); };
                row.AddChild(cell);
            }
            if (filter.Count == 0) { var any = Style.Text(post ? "no deliveries" : "anything", 14, Style.Tan); any.CustomMinimumSize = new Vector2(0, 44); row.AddChild(any); }
            var (add, _) = ItemSlot.Cup($"filter_add_{(output ? "out" : "in")}", 44);
            add.Text = "+"; add.AddThemeFontSizeOverride("font_size", 22); add.AddThemeColorOverride("font_color", Style.Tan);
            add.TooltipText = "Add an item · or drop one from your backpack";
            add.Pressed += () => { filterPicking = !filterPicking; pickingOutput = output; OpenPage("inspect"); };
            add.CanTake = drag => drag.From != null && (!World.PackedKind(drag.Item).IsEmpty || !post && world.Data.Items.ContainsKey(drag.Item));
            // The item stays where it was: a filter only names it, or for a packed crate the kind of crate it is.
            add.Take = drag => {
                var kind = World.PackedKind(drag.Item);
                world.SetFilter(t, kind.IsEmpty ? drag.Item : new string(kind), output, true); Callable.From(() => OpenPage("inspect")).CallDeferred(); return 0;
            };
            row.AddChild(add);
        }
        if (filterPicking) {
            var picker = new GridContainer { Columns = 6 }; picker.AddThemeConstantOverride("h_separation", 6); picker.AddThemeConstantOverride("v_separation", 6);
            var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); well.AddChild(picker); board.AddChild(well);
            var crates = world.Data.Market.Sells.Keys.Select(World.CrateOf).Where(id => post || world.Holds(t, id));
            foreach (var id in (post ? crates : world.Data.Items.Keys.Where(id => world.Holds(t, id)).Concat(crates)).OrderBy(FilterName)) {
                var cell = FilterCell($"pick_{id}", id); cell.TooltipText = FilterName(id);
                cell.Pressed += () => { world.SetFilter(t, id, pickingOutput, true); filterPicking = false; OpenPage("inspect"); };
                picker.AddChild(cell);
            }
        }
        var clear = kit.Button(post ? "Clear" : "Clear both", () => { world.ClearFilters(t); OpenPage("inspect"); }); clear.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; board.AddChild(clear);
    }
    private bool pickingOutput;

    /// <summary>What a filter entry names: an item, or a crate of a good.</summary>
    private string FilterName(string id) => world.CrateGood(id) is string good ? $"Crate of {world.Data.Name(good).ToLowerInvariant()}" : world.Data.Name(id);

    /// <summary>A filter entry's cell: the item, or for a crate of a good, the good with a small crate in the corner.</summary>
    private ItemSlot FilterCell(string name, string id) {
        var (cell, _) = ItemSlot.Cup(name, 44);
        string? good = world.CrateGood(id);
        cell.Icon = Icons.Of(good ?? id);
        if (good != null) {
            var badge = Style.Icon(Icons.Of("crate"), 22); badge.MouseFilter = Control.MouseFilterEnum.Ignore;
            badge.AnchorLeft = badge.AnchorTop = badge.AnchorRight = badge.AnchorBottom = 1;
            badge.OffsetLeft = badge.OffsetTop = -20; badge.OffsetRight = badge.OffsetBottom = 2;
            cell.AddChild(badge);
        }
        return cell;
    }
}
