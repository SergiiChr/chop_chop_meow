using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A place's job buttons, side by side along the bottom: each shows how many workers do it, and opens a list of workers to tick.
/// Each worker shows how many jobs they have elsewhere, listed on hover.
/// </summary>
/// <param name="redraw">Draws the page again after a job's list opens or closes.</param>
public partial class JobRow(UiKit kit, LiveUpdates live, Thing t, PageMemory memory, Action redraw) : VBoxContainer {
    private static readonly Dictionary<string, string> Names = new() {
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
    public static string Title(string job) => Names.GetValueOrDefault(job, job);

    private readonly World world = kit.World;

    public override void _Ready() {
        var jobs = world.Jobs(t);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); AddChild(row);
        var counts = new Label[jobs.Length];
        for (int i = 0; i < jobs.Length; i++) {
            string job = jobs[i];
            var look = Style.Pill(Style.JobColors[i % Style.JobColors.Length]);
            var button = new Button { Name = $"job_{job}", CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, ToggleMode = true, ButtonPressed = memory.OpenJob == job };
            Style.Skin(button, look.Item1, look.Item2, look.Item2);
            button.TooltipText = Hint(job);
            var inner = new HBoxContainer { Alignment = AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore }; inner.AddThemeConstantOverride("separation", 8);
            inner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); button.AddChild(inner);
            inner.AddChild(Style.Text(Title(job), 16, Style.Rim, Style.Head));
            var count = Style.Pill("0", Style.BeigePill, Style.Line, 13); count.CustomMinimumSize = new Vector2(24, 24); inner.AddChild(count);
            // The label sits on the button rather than in it, so the button is widened to fit it.
            button.CustomMinimumSize = new Vector2(inner.GetCombinedMinimumSize().X + 32, 48);
            counts[i] = count;
            button.Pressed += () => { memory.OpenJob = memory.OpenJob == job ? "" : job; redraw(); };
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
        if (memory.OpenJob != "" && jobs.Contains(memory.OpenJob)) WorkerList(memory.OpenJob);
    }

    private string Hint(string job) => job switch {
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
    private void WorkerList(string job) {
        var list = new VBoxContainer(); list.AddThemeConstantOverride("separation", 2);
        var box = new PanelContainer(); box.AddThemeStyleboxOverride("panel", Style.Dropdown); box.AddChild(list); AddChild(box);
        if (world.Workers.Count == 0) { list.AddChild(Style.Text("Hire villagers at a trading post.", 14, Style.Ink)); return; }
        foreach (var w in world.Workers) {
            var worker = w;
            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8); list.AddChild(line);
            var tick = new CheckBox { Text = w.Name, ButtonPressed = World.HasDuty(w, t.Id, job), FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, Name = $"duty_{w.Id}" };
            tick.AddThemeColorOverride("font_color", Style.Ink); tick.AddThemeColorOverride("font_hover_color", Style.Ink); tick.AddThemeColorOverride("font_pressed_color", Style.Ink); tick.AddThemeColorOverride("font_hover_pressed_color", Style.Ink);
            tick.AddThemeFontOverride("font", Style.Bold);
            // A full mine refuses the worker, so the box shows what actually happened.
            tick.Toggled += on => { world.SetDuty(worker, t, job, on); tick.SetPressedNoSignal(World.HasDuty(worker, t.Id, job)); kit.Refresh(); };
            line.AddChild(tick);
            var elsewhere = w.Duties.Where(d => d.Thing != t.Id).ToList();
            if (elsewhere.Count == 0) continue;
            var circle = Style.Pill(elsewhere.Count.ToString(), Style.Round(Style.Enamel, 11).Item1, Style.Rim, 12);
            circle.CustomMinimumSize = new Vector2(22, 22); circle.MouseFilter = MouseFilterEnum.Pass;
            circle.TooltipText = string.Join("\n", elsewhere.Select(d => $"{(world.DutyPlace(w, d.Thing) is Thing at ? world.Data.Name(at.Kind) : "?")} · {Title(d.Job).ToLowerInvariant()}"));
            line.AddChild(circle);
        }
    }
}
