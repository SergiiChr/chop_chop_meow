using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The process row every station shares: inputs with what one batch takes under each, the batch time over the progress bar,
/// the fuel under it with how long it lasts, and the main output with its side outputs in a second row.
/// </summary>
public partial class ProcessRow(LiveUpdates live, IEnumerable<Control> inputs, string time, Func<float> progress, Control? fuel, Control? main, IEnumerable<Control> side) : PanelContainer {
    public override void _Ready() {
        AddThemeStyleboxOverride("panel", Style.Well);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); AddChild(row);
        var left = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin }; left.AddThemeConstantOverride("separation", 12); row.AddChild(left);
        foreach (var input in inputs) left.AddChild(input);
        var middle = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; middle.AddThemeConstantOverride("separation", 8); row.AddChild(middle);
        var clock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; clock.AddThemeConstantOverride("separation", 4); middle.AddChild(clock);
        clock.AddChild(Style.Glyph("line_clock", 16, Style.Ink)); clock.AddChild(Style.Text(time, 14, Style.Ink));
        var bar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(60, 14), Name = "progress" };
        middle.AddChild(bar);
        // Redrawn only when the bar moves a pixel's worth.
        live.Watch(() => (int)(progress() * 200), at => bar.Value = at / 200f);
        if (fuel != null) { fuel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; middle.AddChild(fuel); }
        var right = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin }; right.AddThemeConstantOverride("separation", 10); row.AddChild(right);
        if (main != null) right.AddChild(main);
        var extras = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; extras.AddThemeConstantOverride("separation", 10); right.AddChild(extras);
        foreach (var output in side) extras.AddChild(output);
        extras.Visible = extras.GetChildCount() > 0;
    }
}

/// <summary>The fuel position under a process bar: an icon for what it is, its cup or picture, and how long it lasts.</summary>
public partial class FuelRow(string glyph, Color tint, Control cup, Label lasts) : HBoxContainer {
    public override void _Ready() {
        AddThemeConstantOverride("separation", 8);
        AddChild(Style.Glyph(glyph, 24, tint)); AddChild(cup); AddChild(lasts);
    }
}
