using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>What the crew overview and a worker's own panel share.</summary>
public abstract partial class WorkerPage(PageTools tools) : PagePanel(tools) {
    protected static readonly Texture2D? Hardhat = Icons.Of("hardhat");

    /// <summary>A number that changes with a worker's jobs, their order and their haul groups.</summary>
    protected static int DutyCode(Worker w) {
        int code = 0;
        foreach (var entry in w.Jobs) {
            if (entry is Duty duty) { code = code * 31 + duty.Thing * 16 + (int)duty.Job; continue; }
            var group = (HaulGroup)entry;
            code = code * 31 + group.Id * 7919;
            foreach (var haul in group.Hauls) code = code * 31 + haul.Thing;
        }
        return code;
    }

    /// <summary>Keeps a worker's two icon cells on what they carry and the tool they hold.</summary>
    protected void WatchKit(Worker w, ItemSlot carry, Badge carried, ItemSlot tool) {
        live.Watch(() => { int count = w.Bag.Count; return (count, count > 0 ? w.Bag.First() : null, w.Tool); }, now => {
            var (count, item, held) = now;
            carry.Icon = item == null ? null : Icons.Of(item); carried.Text = count > 1 ? count.ToString() : ""; carry.Visible = item != null;
            carry.TooltipText = item == null ? "Carrying nothing" : $"Carrying {count} {world.Data.Name(item)}";
            tool.Icon = held == "" ? null : Icons.Of(held); tool.Visible = held != "";
            tool.TooltipText = held == "" ? "No tool in hand" : world.Data.Name(held);
        });
    }
}

/// <summary>
/// The crew overview, one card per worker.
/// A card shows the worker's hat, name, wage, what they carry and the tool they hold, what they are doing, and their jobs as chips with each place's icon.
/// </summary>
public partial class CrewPanel(PageTools tools) : WorkerPage(tools) {
    protected override void Build() {
        RedrawOnChange(() => world.Workers.Count);
        var count = Style.Pill("", Style.BlueTag, Style.Rim, 13); count.MouseFilter = Control.MouseFilterEnum.Pass; count.TooltipText = "Villagers hired, of the most who will come";
        var wages = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 13); wages.MouseFilter = Control.MouseFilterEnum.Pass; wages.TooltipText = "Wages paid each dawn";
        foreach (var pill in new[] { count, wages }) { pill.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; panel.AddToHeader(pill); }
        live.Watch(() => (world.Workers.Count, world.DailyWages), now => {
            count.Text = $"{now.Item1} / {World.MaxWorkers}";
            wages.Text = $"−{now.Item2} /day";
        });
        if (world.Workers.Count == 0) {
            var empty = Well(new HBoxContainer()); empty.AddThemeConstantOverride("separation", 14);
            empty.AddChild(Style.Icon(Hardhat, 40));
            var hint = Style.Text("No crew yet. Hire villagers at a trading post: more coins up front means a lower daily wage.", 15, Style.Ink, wrap: true);
            hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; empty.AddChild(hint);
            return;
        }
        foreach (var w in world.Workers) WorkerCard(w);
    }

    /// <summary>A worker's card in the crew overview; clicking it opens the worker.</summary>
    private void WorkerCard(Worker w) {
        var card = new PanelContainer(); card.AddThemeStyleboxOverride("panel", Style.Well); AddChild(card);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; column.AddThemeConstantOverride("separation", 10); card.AddChild(column);
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; top.AddThemeConstantOverride("separation", 12); column.AddChild(top);
        top.AddChild(Style.Icon(Hardhat, 40));
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore }; text.AddThemeConstantOverride("separation", 0); top.AddChild(text);
        text.AddChild(Style.Text(w.Name, 18, Style.Ink, Style.Head));
        var status = Style.Text("", 13, Style.Tan); status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; status.ClipText = true; text.AddChild(status);
        var (carry, carried) = ItemSlot.Create($"carry_{w.Id}", 44); carry.MouseFilter = Control.MouseFilterEnum.Ignore; top.AddChild(carry);
        var (tool, _) = ItemSlot.Create($"tool_{w.Id}", 44); tool.MouseFilter = Control.MouseFilterEnum.Ignore; top.AddChild(tool);
        var wage = Style.Pill($"{w.Wage} /day", Style.BeigePill, Style.Ink, 13); wage.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; top.AddChild(wage);
        var chips = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; chips.AddThemeConstantOverride("h_separation", 6); chips.AddThemeConstantOverride("v_separation", 6); column.AddChild(chips);
        int number = 0;
        foreach (var entry in w.Jobs) {
            if (entry is HaulGroup group) chips.AddChild(GroupChip(++number, w, group));
            else if (world.DutyPlace(w, ((Duty)entry).Thing) is Thing at) chips.AddChild(DutyChip(++number, at, ((Duty)entry).Job));
        }
        if (number == 0) chips.AddChild(Style.Pill("No jobs", Style.Tag(Style.Tan), Style.Rim, 12));
        // A flat button over the whole card takes the click and rings it on hover.
        var open = new Button { Flat = false, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, Name = $"crew_{w.Id}" };
        Style.Skin(open, Style.None, Style.CardHover); open.Pressed += () => panel.Select(w); card.AddChild(open);
        RedrawOnChange(() => DutyCode(w));
        live.Watch(() => w.Status, shown => status.Text = shown);
        WatchKit(w, carry, carried, tool);
    }

    /// <summary>A job as a small chip: its number in the worker's list, the place's icon, the job's name and what the place is set to make.</summary>
    private Control DutyChip(int number, Thing at, JobKind job) {
        var row = Chip(number, Style.Chip);
        row.AddChild(Style.Icon(JobList.PlaceIcon(at), 20));
        row.AddChild(Style.Text(World.Def(job).Title, 13, Style.Ink, Style.Bold));
        var made = Style.Icon(null, 18); row.AddChild(made);
        live.Watch(() => JobList.Made(world, at), item => { made.Texture = item == null ? null : Icons.Of(item); made.Visible = item != null; });
        return row.GetParent<Control>();
    }

    /// <summary>A haul group as one chip: its number in the worker's list and the icons of its places.</summary>
    private Control GroupChip(int number, Worker w, HaulGroup group) {
        var row = Chip(number, Style.GroupChip);
        row.AddChild(Style.Text(World.Def(JobKind.Haul).Title, 13, Style.Ink, Style.Bold));
        foreach (var haul in group.Hauls) if (world.DutyPlace(w, haul.Thing) is Thing at) row.AddChild(Style.Icon(JobList.PlaceIcon(at), 20));
        return row.GetParent<Control>();
    }

    /// <summary>An empty chip that starts with a job's number; returns the row to fill.</summary>
    private static HBoxContainer Chip(int number, StyleBox look) {
        var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; chip.AddThemeStyleboxOverride("panel", look);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 6); chip.AddChild(row);
        row.AddChild(Style.Text(number.ToString(), 12, Style.Tan, Style.Bold));
        return row;
    }
}

/// <summary>One worker: what they carry and hold, what they are doing, their jobs with a button to drop each, and what can be done with them.</summary>
public partial class WorkerPanel(PageTools tools, Worker? w) : WorkerPage(tools) {
    public override bool Current => w == null || world.Workers.Contains(w);

    protected override void Build() {
        if (w == null) { AddLabel("This worker has left."); return; }
        headingTitle.Text = w.Name; headingIcon.Texture = Hardhat; headingIcon.Visible = true;
        var wage = Style.Pill($"{w.Wage} /day", Style.Tag(Style.Brick), Style.Rim, 13); wage.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; wage.MouseFilter = Control.MouseFilterEnum.Pass; wage.TooltipText = "Daily wage";
        panel.AddToHeader(wage);
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 10);
        var slots = new HBoxContainer(); slots.AddThemeConstantOverride("separation", 14); well.AddChild(slots);
        var toolColumn = new VBoxContainer { Name = "tool" }; toolColumn.AddThemeConstantOverride("separation", 6); slots.AddChild(toolColumn);
        toolColumn.AddChild(Style.WellLabel("Tool"));
        toolColumn.AddChild(NewInventoryGrid(() => w.ToolSlot, null, id => moves.Stack(w.ToolSlot, world.S.Backpack, id), $"{StationSlots.SlotHint}\nPut a tool here to give it to them",
            held => held.Stack is HeldStack stack && world.Data.Items.TryGetValue(stack.Item, out var def) && def.Tool != "", columns: 1, size: 46));
        slots.AddChild(new VSeparator());
        var hands = new VBoxContainer { Name = "hands", SizeFlagsHorizontal = SizeFlags.ExpandFill }; hands.AddThemeConstantOverride("separation", 6); slots.AddChild(hands);
        var carrying = Style.WellLabel(""); hands.AddChild(carrying);
        live.Bind(carrying, text => text.Append("Carrying ").Append(w.Bag.Count).Append(" of ").Append(Worker.BagStack));
        hands.AddChild(NewInventoryGrid(() => w.Bag, null, id => moves.Stack(w.Bag, world.S.Backpack, id), $"{StationSlots.SlotHint}\nPut a stack here and they carry it to where it goes", columns: 6, size: 46));
        var status = Style.Text("", 15, Style.Ink, wrap: true); well.AddChild(status);
        var owed = Style.Text("", 13, Style.Brick, Style.Bold); well.AddChild(owed);
        live.Watch(() => w.Status, shown => status.Text = shown);
        live.Watch(() => (int)w.WageDebt, debt => { owed.Text = debt > 0 ? $"Owed {debt} coins · on strike" : ""; owed.Visible = debt > 0; });
        var jobs = Well(new VBoxContainer()); jobs.AddThemeConstantOverride("separation", 6);
        jobs.AddChild(Style.WellLabel("Jobs · first one first"));
        jobs.AddChild(new JobList(kit, live, w) { Name = "jobs" });
        RedrawOnChange(() => DutyCode(w));
        var buttons = Row(); buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(kit.BigButton("Gather…", Style.Sage, () => { panel.Player.Mode = new ClickMode.Assign(w.Id); panel.Close(); kit.Refresh(); }));
        buttons.AddChild(kit.BigButton("Clear jobs", Style.Enamel, () => world.Assign(w, null)));
        buttons.AddChild(kit.BigButton("Dismiss", Style.Brick, () => { world.Fire(w); panel.Open(Page.Crew); }));
        if (w.WageDebt > 0) AddChild(kit.BigButton("Pay outstanding wages", Style.Mustard, () => world.SettleDebt(w)));
        BagGrid(id => moves.Stack(world.S.Backpack, w.Bag, id), $"{BagHint}\nShift: give a stack · Ctrl: all of it");
    }
}
