using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Which job's worker list is open under its button.</summary>
public sealed class JobChoice {
    public JobKind? Job;
}

/// <summary>
/// A place's job buttons, side by side along the bottom: each shows how many workers do it, and opens a list of workers to tick.
/// Each worker shows how many jobs they have elsewhere, listed on hover.
/// </summary>
/// <param name="redraw">Draws the page again after a job's list opens or closes.</param>
public partial class JobRow(UiKit kit, LiveUpdates live, Thing t, JobChoice choice, Action redraw) : VBoxContainer {

    private readonly World world = kit.World;

    public override void _Ready() {
        var jobs = world.Jobs(t);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); AddChild(row);
        var counts = new Label[jobs.Length];
        for (int i = 0; i < jobs.Length; i++) {
            var job = jobs[i];
            var button = new Button { Name = $"job_{job.Id()}", CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, ToggleMode = true, ButtonPressed = choice.Job == job };
            Style.SkinPill(button, Style.JobColors[i % Style.JobColors.Length]);
            button.TooltipText = Hint(job);
            var inner = new HBoxContainer { Alignment = AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore }; inner.AddThemeConstantOverride("separation", 8);
            inner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); button.AddChild(inner);
            inner.AddChild(Style.Text(World.Def(job).Title, 16, Style.Rim, Style.Head));
            var count = Style.Pill("0", Style.BeigePill, Style.Line, 13); count.CustomMinimumSize = new Vector2(24, 24); inner.AddChild(count);
            // The label sits on the button rather than in it, so the button is widened to fit it.
            button.CustomMinimumSize = new Vector2(inner.GetCombinedMinimumSize().X + 32, 48);
            counts[i] = count;
            button.Pressed += () => { choice.Job = choice.Job == job ? null : job; redraw(); };
            row.AddChild(button);
        }
        var shown = new int[jobs.Length];
        var now = new int[jobs.Length];
        Array.Fill(shown, -1);
        live.Add(() => {
            Array.Clear(now);
            foreach (var w in world.Workers)
                for (int i = 0; i < jobs.Length; i++) if (World.HasDuty(w, t.Id, jobs[i])) now[i]++;
            for (int i = 0; i < jobs.Length; i++) {
                if (now[i] == shown[i]) continue;
                shown[i] = now[i]; counts[i].Text = now[i].ToString();
            }
        });
        if (choice.Job is JobKind open && jobs.Contains(open)) WorkerList(open);
    }

    /// <summary>What a job does here: the table's words, with a haul's goods and a mine's crew named for the place.</summary>
    private string Hint(JobKind job) => job switch {
        JobKind.Haul when t.Kind == ThingKind.Farm => "Brings seeds and fertilizer, waters wheat with a steel bucket and carries the harvest to storage.",
        JobKind.Haul when t.Kind == ThingKind.Mine => "Brings the timber, iron and stone the mine needs.",
        JobKind.Haul when t.Kind == ThingKind.Barrel => "Hauls water to fill this barrel.",
        JobKind.Haul when t.Kind == ThingKind.Hut => "Climbs the ladder to fill the roof barrel with water.",
        JobKind.Haul when t.Kind is ThingKind.Chest or ThingKind.Crate => "Carries what its output filter lets out to places asking for it, and brings what its input filter names.",
        JobKind.Mine => $"{World.Def(job).Hint} Up to {world.Data.Mine.Workers} workers.",
        _ => World.Def(job).Hint
    };

    /// <summary>Every worker, ticked when they do this job here; the circle counts their jobs elsewhere.</summary>
    private void WorkerList(JobKind job) {
        var list = new VBoxContainer(); list.AddThemeConstantOverride("separation", 2);
        var box = new PanelContainer(); box.AddThemeStyleboxOverride("panel", Style.Dropdown); box.AddChild(list); AddChild(box);
        if (world.Workers.Count == 0) { list.AddChild(Style.Text("Hire villagers at a trading post.", 14, Style.Ink)); return; }
        foreach (var w in world.Workers) {
            var worker = w;
            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8); list.AddChild(line);
            var tick = new CheckBox { Text = w.Name, ButtonPressed = World.HasDuty(w, t.Id, job), FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, Name = $"duty_{w.Id}" };
            Style.InkCheck(tick);
            tick.AddThemeFontOverride("font", Style.Bold);
            // A full mine refuses the worker, so the box shows what actually happened.
            live.Tick(tick, () => World.HasDuty(worker, t.Id, job), on => world.SetDuty(worker, t, job, on));
            line.AddChild(tick);
            var elsewhere = new List<Duty>();
            foreach (var d in w.Duties) if (d.Thing != t.Id) elsewhere.Add(d);
            if (elsewhere.Count == 0) continue;
            var circle = Style.Pill(elsewhere.Count.ToString(), Style.Round(Style.Enamel, 11).Item1, Style.Rim, 12);
            circle.CustomMinimumSize = new Vector2(22, 22); circle.MouseFilter = MouseFilterEnum.Pass;
            circle.TooltipText = string.Join("\n", elsewhere.Select(d => $"{(world.DutyPlace(w, d.Thing) is Thing at ? world.Data.Name(at.Kind) : "?")} · {World.Def(d.Job).Title.ToLowerInvariant()}"));
            line.AddChild(circle);
        }
    }
}
