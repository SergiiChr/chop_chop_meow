using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The handle a job's row is dragged by: six dots.</summary>
public partial class JobGrip(JobList list, int index) : Control {
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
        list.StartDrag(index, GetGlobalTransform() * press.Position);
        AcceptEvent();
    }
}

/// <summary>
/// A worker's jobs in the order they come first, a row each, with the hauls of a haul group inside the group's box.
/// A row's grip drags it to another place in the list, and a haul into or out of a group.
/// </summary>
public partial class JobList(UiKit kit, LiveUpdates live, Worker w) : VBoxContainer {
    private readonly World world = kit.World;

    /// <summary>A job's row: its place in the worker's list, the group it is in or is, and what stands at the top level of the list for it.</summary>
    private readonly record struct Row(Control Line, Control Top, int Index, int Group, bool Member);
    private readonly List<Row> rows = new();
    /// <summary>Each haul group's box and the hint inside it, by the group's number.</summary>
    private readonly Dictionary<int, (PanelContainer Box, Control Hint, bool Empty)> groups = new();

    private int dragged = -1;
    private Control? ghost;
    private ColorRect? mark;
    private (int Before, int Group) target;

    /// <summary>The icon of a job's place: a resource shows what it yields.</summary>
    public static Texture2D? PlaceIcon(Thing at) => Icons.Of(at.IsResource ? World.ResourceItem(at) : at.Kind.Id());

    /// <summary>
    /// The item a place is set to make, mine, pack or grow, which its jobs show; null for a place set to nothing,
    /// and for a resource, whose icon is already what it yields.
    /// </summary>
    public static string? Made(World world, Thing at) => at.Kind switch {
        ThingKind.Mine => World.MineOre(at),
        ThingKind.Packer => at.Packs == "" ? null : at.Packs,
        ThingKind.Farm => world.Crop,
        _ => world.Data.Recipes.TryGetValue(at.Recipe, out var recipe) ? recipe.Output : null
    };

    public override void _Ready() {
        SetProcessInput(false);
        AddThemeConstantOverride("separation", 6);
        int number = 0;
        for (int i = 0; i < w.Duties.Count; i++) {
            var duty = w.Duties[i];
            if (duty.Job == World.HaulGroup) i = AddGroup(i, ++number);
            else if (world.DutyPlace(w, duty.Thing) is Thing at) {
                if (rows.Count > 0) AddChild(new HSeparator());
                var line = Line(i, ++number, at, false);
                AddChild(line); rows.Add(new Row(line, line, i, 0, false));
            }
        }
        if (rows.Count == 0) AddChild(Style.Text("No jobs. Give one with the job buttons at a building, or send them to gather.", 14, Style.Tan, wrap: true));
        var add = kit.Button("+ New haul group", () => world.AddHaulGroup(w)); add.Name = "new_group"; add.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        add.TooltipText = "Hauls dragged into a group count as one job: the next load is picked across all their places";
        Style.Skin(add, Style.OutlinePill, Style.OutlinePillHover);
        add.AddThemeFontOverride("font", Style.Bold); add.AddThemeFontSizeOverride("font_size", 14);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color" }) add.AddThemeColorOverride(state, Style.Ink);
        AddChild(add);
    }

    /// <summary>Adds a haul group's box with its own row and its hauls' rows; returns the place in the list of its last entry.</summary>
    private int AddGroup(int index, int number) {
        int group = w.Duties[index].Group, last = index;
        while (last + 1 < w.Duties.Count && World.Grouped(w.Duties[last + 1]) && w.Duties[last + 1].Group == group) last++;
        var box = new PanelContainer { Name = $"group_{group}" }; box.AddThemeStyleboxOverride("panel", Style.GroupWell);
        var inside = new VBoxContainer(); inside.AddThemeConstantOverride("separation", 2); box.AddChild(inside);
        AddChild(box);
        var head = Line(index, number, null, false, last - index);
        inside.AddChild(head); rows.Add(new Row(head, box, index, group, false));
        for (int i = index + 1; i <= last; i++) {
            if (world.DutyPlace(w, w.Duties[i].Thing) is not Thing at) continue;
            var line = Line(i, 0, at, true);
            inside.AddChild(line); rows.Add(new Row(line, box, i, group, true));
        }
        var hint = Style.Text("Drag haul jobs here", 13, Style.Tan); hint.HorizontalAlignment = HorizontalAlignment.Center; hint.CustomMinimumSize = new Vector2(0, 30);
        hint.Visible = last == index;
        inside.AddChild(hint);
        groups[group] = (box, hint, last == index);
        return last;
    }

    /// <summary>
    /// One row: grip, the job's number in the list, its place, what the place is set to make, the job and a button to take it away.
    /// With no place it is a haul group's own row, which counts its <paramref name="hauls"/>.
    /// </summary>
    private HBoxContainer Line(int index, int number, Thing? at, bool member, int hauls = 0) {
        var duty = w.Duties[index];
        var row = new HBoxContainer { Name = $"job_{index}" }; row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new JobGrip(this, index) { Name = $"grip_{index}" });
        if (member) row.AddChild(new Control { CustomMinimumSize = new Vector2(12, 0) });
        else {
            var order = Style.Pill(number.ToString(), Style.NumberRing, Style.Ink, 12); order.CustomMinimumSize = new Vector2(22, 22); order.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(order);
        }
        Summary(row, duty, at, member, hauls);
        Button drop;
        if (at != null) drop = kit.HeaderButton("close", "Take this job away", () => world.SetDuty(w, at, duty.Job, false), Style.CloseRound, $"drop_{duty.Job}_{at.Id}");
        else drop = kit.HeaderButton("close", "Take this group apart · its hauls stay as jobs of their own", () => world.RemoveHaulGroup(w, duty.Group), Style.Round(Style.Tan, 18), $"ungroup_{duty.Group}");
        drop.CustomMinimumSize = new Vector2(30, 30); drop.AddThemeConstantOverride("icon_max_width", 14);
        row.AddChild(drop);
        return row;
    }

    /// <summary>Fills a row, or the copy of it that follows the mouse, with what the job is: its place, what the place makes and the job's name.</summary>
    private void Summary(HBoxContainer row, Duty duty, Thing? at, bool member, int hauls) {
        if (at == null) {
            var title = Style.Text("Haul group", 15, Style.Ink); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(title);
            row.AddChild(Style.Text(hauls == 1 ? "1 place" : $"{hauls} places", 13, Style.Tan));
            return;
        }
        row.AddChild(Style.Icon(PlaceIcon(at), member ? 28 : 32));
        var place = Style.Text(world.Data.Name(at.Kind), 15, Style.Ink); place.SizeFlagsHorizontal = SizeFlags.ExpandFill; place.ClipText = true; place.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(place);
        row.AddChild(MadeCup(world, live, at, 22));
        int colour = Array.IndexOf(world.Jobs(at), duty.Job);
        var tag = Style.Pill(JobRow.Title(duty.Job), Style.Tag(Style.JobColors[Math.Max(0, colour) % Style.JobColors.Length]), Style.Rim, 13); tag.SizeFlagsVertical = SizeFlags.ShrinkCenter;
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

    /// <summary>Lifts a job's row onto the mouse; it lands where the button is let go.</summary>
    internal void StartDrag(int index, Vector2 at) {
        int found = rows.FindIndex(r => r.Index == index);
        if (dragged >= 0 || found < 0) return;
        dragged = index;
        var row = rows[found];
        var duty = w.Duties[index];
        (duty.Job == World.HaulGroup ? row.Top : row.Line).Modulate = Style.Faded;
        var lifted = new PanelContainer { TopLevel = true, MouseFilter = MouseFilterEnum.Ignore, Name = "lifted" }; lifted.AddThemeStyleboxOverride("panel", Style.Lifted);
        var copy = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; copy.AddThemeConstantOverride("separation", 8); lifted.AddChild(copy);
        copy.AddChild(new Control { CustomMinimumSize = new Vector2(12, 32) });
        int hauls = duty.Job == World.HaulGroup ? rows.Count(r => r.Member && r.Group == duty.Group) : 0;
        Summary(copy, duty, duty.Job == World.HaulGroup ? null : world.DutyPlace(w, duty.Thing), row.Member, hauls);
        lifted.Size = new Vector2(Size.X, 0);
        AddChild(lifted); ghost = lifted;
        mark = new ColorRect { TopLevel = true, Color = Style.Brick, MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Size.X, 4), Name = "drop_mark" };
        AddChild(mark);
        SetProcessInput(true);
        MoveDrag(at);
    }

    public override void _Input(InputEvent ev) {
        if (dragged < 0) return;
        if (ev is InputEventMouseMotion motion) MoveDrag(OnCanvas(motion));
        else if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release) {
            MoveDrag(OnCanvas(release));
            int index = dragged; var (before, group) = target;
            EndDrag();
            kit.Run(() => world.MoveDuty(w, index, before, group));
        } else if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape }) EndDrag();
        else return;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Where a mouse event is on the canvas the rows lie on, whatever the window's stretch and the interface's scale.</summary>
    private Vector2 OnCanvas(InputEventMouse ev) => GetGlobalTransform() * ((InputEventMouse)MakeInputLocal(ev)).Position;

    public override void _ExitTree() => dragged = -1;

    /// <summary>Follows the mouse with the lifted row, and marks where it would land: between two jobs, or in the haul group under it.</summary>
    private void MoveDrag(Vector2 at) {
        var duty = w.Duties[dragged];
        int into = 0;
        if (duty.Job == "haul") foreach (var (group, parts) in groups) if (parts.Box.GetGlobalRect().HasPoint(at)) into = group;
        foreach (var (group, parts) in groups) {
            parts.Box.AddThemeStyleboxOverride("panel", group == into ? Style.GroupOver : Style.GroupWell);
            parts.Hint.Visible = parts.Empty || group == into && duty.Group != group;
        }
        int before = w.Duties.Count; float y = GetGlobalRect().End.Y;
        if (into != 0) {
            // Among the group's hauls it lands where it is dropped; with none under it, at their end.
            var box = groups[into].Box.GetGlobalRect();
            before = rows.FindLastIndex(r => r.Group == into) is >= 0 and int tail ? rows[tail].Index + 1 : before; y = box.End.Y - 8;
            foreach (var row in rows) {
                if (!row.Member || row.Group != into || at.Y >= row.Line.GetGlobalRect().GetCenter().Y) continue;
                before = row.Index; y = row.Line.GetGlobalRect().Position.Y - 3;
                break;
            }
        } else {
            // Outside a group it lands between jobs of the list itself, a group counting as one.
            foreach (var row in rows) {
                if (row.Member) continue;
                var rect = row.Top.GetGlobalRect();
                if (at.Y >= (row.Group != 0 ? rect.GetCenter().Y : row.Line.GetGlobalRect().GetCenter().Y)) continue;
                before = row.Index; y = rect.Position.Y - 5;
                break;
            }
            if (before == w.Duties.Count && rows.Count > 0) y = rows[^1].Top.GetGlobalRect().End.Y + 1;
        }
        target = (before, into);
        mark!.GlobalPosition = new Vector2(GlobalPosition.X, y);
        ghost!.GlobalPosition = new Vector2(GlobalPosition.X + 14, at.Y - ghost.Size.Y / 2);
    }

    private void EndDrag() {
        dragged = -1; SetProcessInput(false);
        ghost?.QueueFree(); mark?.QueueFree(); ghost = null; mark = null;
        foreach (var row in rows) { row.Line.Modulate = Colors.White; row.Top.Modulate = Colors.White; }
        foreach (var (_, parts) in groups) { parts.Box.AddThemeStyleboxOverride("panel", Style.GroupWell); parts.Hint.Visible = parts.Empty; }
    }
}
