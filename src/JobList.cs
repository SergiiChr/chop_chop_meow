using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The handle a job's row is dragged by: six dots.</summary>
public partial class JobGrip(JobList list, JobSpot spot) : Control {
    public override void _Ready() {
        CustomMinimumSize = new Vector2(20, 36); MouseDefaultCursorShape = CursorShape.Drag; TooltipText = "Drag to move this job";
    }

    public override void _Draw() => Dots(this, Size / 2, Style.Tan);

    /// <summary>Draws a grip's six dots around a point.</summary>
    public static void Dots(Control on, Vector2 centre, Color color) {
        for (int i = 0; i < 6; i++) on.DrawCircle(centre + new Vector2(i % 2 * 6 - 3, i / 2 * 6 - 6), 1.8f, color);
    }

    public override void _GuiInput(InputEvent ev) {
        if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press) return;
        // The press is in the grip's own units; the list works in the canvas's.
        list.StartDrag(spot, GetGlobalTransform() * press.Position);
        AcceptEvent();
    }
}

/// <summary>
/// A worker's jobs in the order they come first, a row each, with the hauls of a haul group inside the group's box.
/// A row's grip drags it to another place in the list, and a haul into or out of a group.
/// Controls are named by where the entry lies: <c>job_2</c> and <c>grip_2</c> for the third entry, <c>job_2_0</c> for the first haul of a group there, <c>group_1</c> for the box of group 1.
/// </summary>
public partial class JobList(UiKit kit, LiveUpdates live, Worker w) : VBoxContainer {
    private readonly World world = kit.World;

    /// <summary>A row: where its entry lies, the row itself, and what stands in the list for it, the group's box for a group and its hauls.</summary>
    private readonly record struct Row(JobSpot Spot, Control Line, Control Top);
    private readonly List<Row> rows = new();
    /// <summary>Each haul group's box and the hint inside it, by the group's place in the list.</summary>
    private readonly Dictionary<int, (PanelContainer Box, Control Hint, bool Empty)> groups = new();

    private JobSpot? dragged;
    private Control? ghost;
    private ColorRect? mark;
    private JobSpot target;
    /// <summary>The group the lifted row was last over, by its place in the list, or -1.</summary>
    private int over = -1;

    /// <summary>The icon of a job's place: a resource shows what it yields.</summary>
    public static Texture2D? PlaceIcon(Thing at) => Icons.Of(at.IsResource ? World.ResourceItem(at) : at.Kind.Id());

    /// <summary>
    /// The item a place is set to make, mine, pack or grow, which its jobs show; null for a place set to nothing,
    /// and for a resource, whose icon is already what it yields.
    /// </summary>
    public static string? Made(World world, Thing at) => at.Kind switch {
        ThingKind.Mine => Mines.Ore(at),
        ThingKind.Packer => at.Packs == "" ? null : at.Packs,
        ThingKind.Farm => world.Farms.Crop,
        _ => world.Data.Recipes.TryGetValue(at.Recipe, out var recipe) ? recipe.Output : null
    };
    public override void _Ready() {
        SetProcessInput(false);
        AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < w.Jobs.Count; i++) {
            if (w.Jobs[i] is HaulGroup group) { AddGroup(group, i); continue; }
            var duty = (Duty)w.Jobs[i];
            if (world.DutyPlace(w, duty.Thing) is not Thing at) continue;
            if (rows.Count > 0) AddChild(new HSeparator());
            var line = Line(new JobSpot(i), duty, at);
            AddChild(line); rows.Add(new Row(new JobSpot(i), line, line));
        }
        if (rows.Count == 0) AddChild(Style.Text("No jobs. Give one with the job buttons at a building, or send them to gather.", 14, Style.Tan, wrap: true));
        var add = kit.Button("+ New haul group", () => world.AddHaulGroup(w)); add.Name = "new_group"; add.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        add.TooltipText = "Hauls dragged into a group count as one job: the next load is picked across all their places";
        Style.Skin(add, Style.OutlinePill, Style.OutlinePillHover);
        add.AddThemeFontOverride("font", Style.Bold); add.AddThemeFontSizeOverride("font_size", 14);
        Style.InkText(add);
        AddChild(add);
    }

    /// <summary>Adds a haul group's box with its own row and its hauls' rows.</summary>
    private void AddGroup(HaulGroup group, int entry) {
        var box = new PanelContainer { Name = $"group_{group.Id}" }; box.AddThemeStyleboxOverride("panel", Style.GroupWell);
        var inside = new VBoxContainer(); inside.AddThemeConstantOverride("separation", 2); box.AddChild(inside);
        AddChild(box);
        var head = Line(new JobSpot(entry), null, null, group);
        inside.AddChild(head); rows.Add(new Row(new JobSpot(entry), head, box));
        for (int h = 0; h < group.Hauls.Count; h++) {
            if (world.DutyPlace(w, group.Hauls[h].Thing) is not Thing at) continue;
            var line = Line(new JobSpot(entry, h), group.Hauls[h], at);
            inside.AddChild(line); rows.Add(new Row(new JobSpot(entry, h), line, box));
        }
        var hint = Style.Text("Drag haul jobs here", 13, Style.Tan); hint.HorizontalAlignment = HorizontalAlignment.Center; hint.CustomMinimumSize = new Vector2(0, 30);
        bool empty = group.Hauls.Count == 0;
        hint.Visible = empty;
        inside.AddChild(hint);
        groups[entry] = (box, hint, empty);
    }

    /// <summary>What a row's controls are named after: the entry's place in the list, and a haul's place in its group.</summary>
    private static string SpotName(JobSpot spot) => spot.InGroup ? $"{spot.Entry}_{spot.Haul}" : $"{spot.Entry}";

    /// <summary>
    /// One row: grip, the entry's number in the order, its place, what the place is set to make, the job and a button to take it away.
    /// A haul group's own row shows how many places it holds and takes the group apart.
    /// </summary>
    private HBoxContainer Line(JobSpot spot, Duty? duty, Thing? at, HaulGroup? group = null) {
        var row = new HBoxContainer { Name = $"job_{SpotName(spot)}" }; row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new JobGrip(this, spot) { Name = $"grip_{SpotName(spot)}" });
        if (spot.InGroup) row.AddChild(new Control { CustomMinimumSize = new Vector2(12, 0) });
        else {
            var order = Style.Pill((spot.Entry + 1).ToString(), Style.NumberRing, Style.Ink, 12); order.CustomMinimumSize = new Vector2(22, 22); order.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(order);
        }
        Summary(row, duty, at, spot.InGroup, group?.Hauls.Count ?? 0);
        Button drop;
        if (group != null) drop = kit.HeaderButton("close", "Take this group apart · its hauls stay as jobs of their own", () => world.RemoveHaulGroup(w, group.Id), Style.Round(Style.Tan, 18), $"ungroup_{group.Id}");
        else drop = kit.HeaderButton("minus", "Take this job away", () => world.SetDuty(w, at!, duty!.Job, false), Style.CloseRound, $"drop_{duty!.Job.Id()}_{at!.Id}");
        drop.CustomMinimumSize = new Vector2(30, 30); drop.AddThemeConstantOverride("icon_max_width", 14);
        row.AddChild(drop);
        return row;
    }

    /// <summary>Fills a row, or the copy of it that follows the mouse, with what the entry is: a job's place, what the place makes and the job's name, or a haul group.</summary>
    private void Summary(HBoxContainer row, Duty? duty, Thing? at, bool member, int hauls) {
        if (duty == null || at == null) {
            var title = Style.Text("Haul group", 15, Style.Ink); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(title);
            row.AddChild(Style.Text(hauls == 1 ? "1 place" : $"{hauls} places", 13, Style.Tan));
            return;
        }
        row.AddChild(Style.Icon(PlaceIcon(at), member ? 28 : 32));
        var place = Style.Text(world.Data.Name(at.Kind), 15, Style.Ink); place.SizeFlagsHorizontal = SizeFlags.ExpandFill; place.ClipText = true; place.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(place);
        row.AddChild(MadeCup(world, live, at, 22));
        int colour = Array.IndexOf(world.Jobs(at), duty.Job);
        var tag = Style.Pill(World.Def(duty.Job).Title, Style.Tag(Style.JobColors[Math.Max(0, colour) % Style.JobColors.Length]), Style.Rim, 13); tag.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(tag);
    }

    /// <summary>A small cup with the item a place is set to make, kept up to date and hidden while it is set to nothing.</summary>
    public static Control MadeCup(World world, LiveUpdates live, Thing at, float size) {
        var cup = new PanelContainer { Name = $"made_{at.Id}", SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Pass }; cup.AddThemeStyleboxOverride("panel", Style.MadeCup);
        var icon = Style.Icon(null, size); cup.AddChild(icon);
        live.Watch(() => Made(world, at), item => {
            cup.Visible = item != null; icon.Texture = item == null ? null : Icons.Of(item);
            cup.TooltipText = item == null ? "" : $"Set to {world.Lower(world.Data.Name(item))}";
        });
        return cup;
    }

    /// <summary>Lifts an entry's row onto the mouse; it lands where the button is let go.</summary>
    internal void StartDrag(JobSpot spot, Vector2 at) {
        int found = rows.FindIndex(r => r.Spot == spot);
        if (dragged != null || found < 0 || World.JobAt(w, spot) is not JobEntry entry) return;
        dragged = spot; over = -1;
        var row = rows[found];
        var group = entry as HaulGroup;
        (group != null ? row.Top : row.Line).Modulate = Style.Faded;
        var lifted = new PanelContainer { TopLevel = true, MouseFilter = MouseFilterEnum.Ignore, Name = "lifted" }; lifted.AddThemeStyleboxOverride("panel", Style.Lifted);
        var copy = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; copy.AddThemeConstantOverride("separation", 8); lifted.AddChild(copy);
        copy.AddChild(new Control { CustomMinimumSize = new Vector2(12, 32) });
        var duty = entry as Duty;
        Summary(copy, duty, duty == null ? null : world.DutyPlace(w, duty.Thing), spot.InGroup, group?.Hauls.Count ?? 0);
        lifted.Size = new Vector2(Size.X, 0);
        AddChild(lifted); ghost = lifted;
        mark = new ColorRect { TopLevel = true, Color = Style.Brick, MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Size.X, 4), Name = "drop_mark" };
        AddChild(mark);
        SetProcessInput(true);
        MoveDrag(at);
    }

    public override void _Input(InputEvent ev) {
        if (dragged is not JobSpot from) return;
        // The list can lose an entry while one is lifted, such as a job at a building taken down; the rows are then out of date until the page is drawn again.
        if (World.JobAt(w, from) == null) { EndDrag(); return; }
        if (ev is InputEventMouseMotion motion) MoveDrag(OnCanvas(motion));
        else if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release) {
            MoveDrag(OnCanvas(release));
            var to = target;
            EndDrag();
            kit.Run(() => world.MoveJob(w, from, to));
        } else if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape }) EndDrag();
        else return;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Where a mouse event is on the canvas the rows lie on, whatever the window's stretch and the interface's scale.</summary>
    private Vector2 OnCanvas(InputEventMouse ev) => GetGlobalTransform() * ((InputEventMouse)MakeInputLocal(ev)).Position;

    public override void _ExitTree() => dragged = null;

    /// <summary>Follows the mouse with the lifted row, and marks where it would land: between two entries of the list, or among the hauls of the group under it.</summary>
    private void MoveDrag(Vector2 at) {
        var from = dragged!.Value;
        bool haul = World.JobAt(w, from) is Duty { Job: JobKind.Haul };
        int into = -1;
        // The rows can be out of date for a moment, so a box counts only while a group still stands at its place in the list.
        if (haul) foreach (var (entry, parts) in groups) if (parts.Box.GetGlobalRect().HasPoint(at) && World.JobAt(w, new JobSpot(entry)) is HaulGroup) into = entry;
        if (into != over) {
            over = into;
            foreach (var (entry, parts) in groups) {
                parts.Box.AddThemeStyleboxOverride("panel", entry == into ? Style.GroupOver : Style.GroupWell);
                parts.Hint.Visible = parts.Empty || entry == into && from.Entry != entry;
            }
        }
        float y;
        if (into >= 0) {
            // Among the group's hauls it lands where it is dropped; below them all, at their end.
            target = new JobSpot(into, ((HaulGroup)w.Jobs[into]).Hauls.Count); y = groups[into].Box.GetGlobalRect().End.Y - 8;
            foreach (var row in rows) {
                if (!row.Spot.InGroup || row.Spot.Entry != into || at.Y >= row.Line.GetGlobalRect().GetCenter().Y) continue;
                target = row.Spot; y = row.Line.GetGlobalRect().Position.Y - 3;
                break;
            }
        } else {
            // Outside a group it lands between entries of the list, a group counting as one.
            target = new JobSpot(w.Jobs.Count); y = rows.Count > 0 ? rows[^1].Top.GetGlobalRect().End.Y + 1 : GetGlobalRect().Position.Y;
            foreach (var row in rows) {
                if (row.Spot.InGroup) continue;
                var rect = row.Top.GetGlobalRect();
                if (at.Y >= rect.GetCenter().Y) continue;
                target = row.Spot; y = rect.Position.Y - 5;
                break;
            }
        }
        mark!.GlobalPosition = new Vector2(GlobalPosition.X, y);
        ghost!.GlobalPosition = new Vector2(GlobalPosition.X + 14, at.Y - ghost.Size.Y / 2);
    }

    private void EndDrag() {
        dragged = null; over = -1; SetProcessInput(false);
        ghost?.QueueFree(); mark?.QueueFree(); ghost = null; mark = null;
        foreach (var row in rows) { row.Line.Modulate = Colors.White; row.Top.Modulate = Colors.White; }
        foreach (var (_, parts) in groups) { parts.Box.AddThemeStyleboxOverride("panel", Style.GroupWell); parts.Hint.Visible = parts.Empty; }
    }
}
