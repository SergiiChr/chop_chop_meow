using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The crew overview, one card per worker, and a worker's own panel.
/// A card shows the worker's hat, name, wage, what they carry and the tool they hold, what they are doing, and their jobs as chips with each place's icon.
/// </summary>
public partial class PagePanel {
    private static readonly Texture2D Hardhat = GD.Load<Texture2D>("res://assets/ui/hardhat.svg");

    private void Workers() {
        var count = Style.Pill("", Style.BlueTag, Style.Rim, 13); count.MouseFilter = Control.MouseFilterEnum.Pass; count.TooltipText = "Villagers hired, of the most who will come";
        var wages = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 13); wages.MouseFilter = Control.MouseFilterEnum.Pass; wages.TooltipText = "Wages paid each dawn";
        foreach (var pill in new[] { count, wages }) { pill.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; heading.AddChild(pill); heading.MoveChild(pill, heading.GetChildCount() - 2); }
        var countText = new LiveText(shown => count.Text = shown);
        var wagesText = new LiveText(shown => wages.Text = shown);
        (int Hired, int Daily) shown = (-1, -1);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(count)) return;
            var now = (world.S.Workers.Count, world.DailyWages);
            if (now == shown) return;
            shown = now;
            countText.Clear().Append(now.Item1).Append(" / ").Append(World.MaxWorkers); countText.Show();
            wagesText.Clear().Append('−').Append(now.Item2).Append(" /day"); wagesText.Show();
        });
        if (world.S.Workers.Count == 0) {
            var empty = Well(new HBoxContainer()); empty.AddThemeConstantOverride("separation", 14);
            empty.AddChild(Style.Icon(Hardhat, 40));
            var hint = Style.Text("No crew yet. Hire villagers at a trading post: more coins up front means a lower daily wage.", 15, Style.Ink);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart; hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; empty.AddChild(hint);
            return;
        }
        foreach (var w in world.S.Workers) WorkerCard(w);
    }

    /// <summary>A worker's card in the crew overview; clicking it opens the worker.</summary>
    private void WorkerCard(Worker w) {
        var card = new PanelContainer(); card.AddThemeStyleboxOverride("panel", Style.Well); content.AddChild(card);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; column.AddThemeConstantOverride("separation", 10); card.AddChild(column);
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; top.AddThemeConstantOverride("separation", 12); column.AddChild(top);
        top.AddChild(Style.Icon(Hardhat, 40));
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore }; text.AddThemeConstantOverride("separation", 0); top.AddChild(text);
        text.AddChild(Style.Text(w.Name, 18, Style.Ink, Style.Head));
        var status = Style.Text("", 13, Style.Tan); status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; status.ClipText = true; text.AddChild(status);
        var (carry, carried) = ItemSlot.Cup($"carry_{w.Id}", 44); carry.MouseFilter = Control.MouseFilterEnum.Ignore; top.AddChild(carry);
        var (tool, _) = ItemSlot.Cup($"tool_{w.Id}", 44); tool.MouseFilter = Control.MouseFilterEnum.Ignore; top.AddChild(tool);
        var wage = Style.Pill($"{w.Wage} /day", Style.BeigePill, Style.Ink, 13); wage.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; top.AddChild(wage);
        var chips = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; chips.AddThemeConstantOverride("h_separation", 6); chips.AddThemeConstantOverride("v_separation", 6); column.AddChild(chips);
        foreach (var d in w.Duties) if (world.DutyPlace(w, d.Thing) is Thing at) chips.AddChild(DutyChip(at, d.Job));
        if (w.Duties.Count == 0) chips.AddChild(Style.Pill("No jobs", Style.Tag(Style.Tan), Style.Rim, 12));
        // A flat button over the whole card takes the click and rings it on hover.
        var open = new Button { Flat = false, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, Name = $"crew_{w.Id}" };
        Style.Skin(open, Style.None, Style.CardHover); open.Pressed += () => panel.Select(w); card.AddChild(open);
        (string Status, int Duties) drawn = ("", -1);
        (int Carried, string? Item, string Tool) carrying = (-1, null, "");
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(status)) return;
            // A job given or taken elsewhere changes the chips, so the page is rebuilt.
            if (drawn.Duties >= 0 && w.Duties.Count != drawn.Duties) { drawn.Duties = w.Duties.Count; Callable.From(() => { if (state.Page == "workers") panel.Open("workers"); }).CallDeferred(); return; }
            drawn.Duties = w.Duties.Count;
            if (w.Status != drawn.Status) status.Text = drawn.Status = w.Status;
            ShowKit(w, ref carrying, carry, carried, tool);
        });
    }

    /// <summary>Shows what a worker carries and the tool they hold in their two icon cells, redrawing them only when that differs from <paramref name="shown"/>.</summary>
    private void ShowKit(Worker w, ref (int Carried, string? Item, string Tool) shown, ItemSlot carry, Badge carried, ItemSlot tool) {
        int count = w.Bag.Count;
        string? item = count > 0 ? w.Bag.First() : null;
        if (shown == (count, item, w.Tool)) return;
        shown = (count, item, w.Tool);
        carry.Icon = item == null ? null : Icons.Of(item); carried.Text = count > 1 ? count.ToString() : ""; carry.Visible = item != null;
        carry.TooltipText = item == null ? "Carrying nothing" : $"Carrying {count} {world.Data.Name(item)}";
        tool.Icon = w.Tool == "" ? null : Icons.Of(w.Tool); tool.Visible = w.Tool != "";
        tool.TooltipText = w.Tool == "" ? "No tool in hand" : world.Data.Name(w.Tool);
    }

    /// <summary>A job as a small chip: the place's icon and the job's name.</summary>
    private Control DutyChip(Thing at, string job) {
        var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; chip.AddThemeStyleboxOverride("panel", Style.Chip);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 6); chip.AddChild(row);
        row.AddChild(Style.Icon(PlaceIcon(at), 20));
        row.AddChild(Style.Text(JobName(job), 13, Style.Ink, Style.Bold));
        return chip;
    }

    private Texture2D? PlaceIcon(Thing at) => Icons.Of(at.IsResource ? World.ResourceItem(at) : at.Kind.Id());

    /// <summary>One worker: what they carry and hold, what they are doing, their jobs with a button to drop each, and what can be done with them.</summary>
    private void WorkerPanel(Worker? w) {
        if (w == null) { AddLabel("This worker has left."); return; }
        headingTitle.Text = w.Name; headingIcon.Texture = Hardhat; headingIcon.Visible = true;
        var wage = Style.Pill($"{w.Wage} /day", Style.Tag(Style.Brick), Style.Rim, 13); wage.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; wage.MouseFilter = Control.MouseFilterEnum.Pass; wage.TooltipText = "Daily wage";
        heading.AddChild(wage); heading.MoveChild(wage, heading.GetChildCount() - 2);
        var well = Well(new HBoxContainer()); well.AddThemeConstantOverride("separation", 12);
        var (carry, carried) = ItemSlot.Cup("carry", 56); well.AddChild(carry);
        var (tool, _) = ItemSlot.Cup("tool", 56); well.AddChild(tool);
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; text.AddThemeConstantOverride("separation", 2); well.AddChild(text);
        var status = Style.Text("", 15, Style.Ink); status.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.AddChild(status);
        var owed = Style.Text("", 13, Style.Brick, Style.Bold); text.AddChild(owed);
        var owedText = new LiveText(shown => { owed.Text = shown; owed.Visible = shown != ""; });
        (string Status, int Owed) drawn = ("", -1);
        (int Carried, string? Item, string Tool) carrying = (-1, null, "");
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(status)) return;
            if (w.Status != drawn.Status) status.Text = drawn.Status = w.Status;
            ShowKit(w, ref carrying, carry, carried, tool);
            int debt = (int)w.WageDebt;
            if (debt == drawn.Owed) return;
            drawn.Owed = debt;
            if (debt > 0) owedText.Clear().Append("Owed ").Append(debt).Append(" coins · on strike"); else owedText.Clear();
            owedText.Show();
        });
        var jobs = Well(new VBoxContainer()); jobs.AddThemeConstantOverride("separation", 6);
        jobs.AddChild(Style.WellLabel("Jobs"));
        bool first = true;
        foreach (var d in w.Duties.ToArray()) {
            if (world.DutyPlace(w, d.Thing) is not Thing at) continue;
            var duty = d;
            if (!first) jobs.AddChild(new HSeparator());
            first = false;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); jobs.AddChild(row);
            row.AddChild(Style.Icon(PlaceIcon(at), 32));
            var place = Style.Text(world.Data.Name(at.Kind), 15, Style.Ink); place.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(place);
            int colour = System.Array.IndexOf(world.Jobs(at), d.Job);
            var tag = Style.Pill(JobName(d.Job), Style.Tag(Style.JobColors[System.Math.Max(0, colour) % Style.JobColors.Length]), Style.Rim, 13); tag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; row.AddChild(tag);
            var drop = kit.HeaderButton("close", "Take this job away", () => { world.SetDuty(w, at, duty.Job, false); panel.Open("inspect"); }, Style.CloseRound, $"drop_{d.Job}_{at.Id}");
            drop.CustomMinimumSize = new Vector2(30, 30); drop.AddThemeConstantOverride("icon_max_width", 14); row.AddChild(drop);
        }
        if (first) {
            var none = Style.Text("No jobs. Give one with the job buttons at a building, or send them to gather.", 14, Style.Tan);
            none.AutowrapMode = TextServer.AutowrapMode.WordSmart; jobs.AddChild(none);
        }
        var buttons = Row(); buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(kit.BigButton("Gather…", Style.Sage, () => { state.AssigningWorker = w.Id; view.BuildMode = null; panel.Close(); kit.Refresh(); }));
        buttons.AddChild(kit.BigButton("Clear jobs", Style.Enamel, () => { world.Assign(w, null); panel.Open("inspect"); }));
        buttons.AddChild(kit.BigButton("Dismiss", Style.Brick, () => { world.Fire(w); panel.Open("workers"); }));
        if (w.WageDebt > 0) content.AddChild(kit.BigButton("Pay outstanding wages", Style.Mustard, () => world.SettleDebt(w)));
    }
}
