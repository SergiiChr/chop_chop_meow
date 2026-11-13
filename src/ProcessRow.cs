using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The process row every station shares: inputs with what one batch takes under each, the batch time over the progress bar,
/// the fuel under it with how long it lasts, and the main output with its side outputs in a second row.
/// </summary>
public partial class ProcessRow(LiveUpdates live, IEnumerable<Control> inputs, string time, Func<float> progress, Control? fuel, Control? main, IEnumerable<Control> side) : PanelContainer {
    /// <summary>How long a shake lasts, in seconds, how far it swings to each side, in pixels, and how many times it swings.</summary>
    private const float ShakeSeconds = .4f, ShakeReach = 8, ShakeSwings = 4;

    // The input cups and the output cups, each group in a box of its own.
    private HBoxContainer left = null!;
    private VBoxContainer right = null!;
    private Tween? shake;
    private Vector2 leftHome, rightHome;

    /// <summary>Whether the cups are shaking now.</summary>
    public bool Shaking => shake != null && shake.IsValid();

    /// <summary>Shakes the input and output cups from side to side and back to rest, to show they are taken.</summary>
    public void Shake() {
        if (Shaking) { shake!.Kill(); left.Position = leftHome; right.Position = rightHome; }
        leftHome = left.Position; rightHome = right.Position;
        shake = CreateTween();
        shake.TweenMethod(Callable.From<float>(done => {
            var swing = new Vector2(MathF.Sin(done * MathF.Tau * ShakeSwings) * (1 - done) * ShakeReach, 0);
            left.Position = leftHome + swing; right.Position = rightHome + swing;
        }), 0f, 1f, ShakeSeconds);
    }

    public override void _Ready() {
        AddThemeStyleboxOverride("panel", Style.Well); MouseFilter = MouseFilterEnum.Pass;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); AddChild(row);
        left = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin }; left.AddThemeConstantOverride("separation", 12); row.AddChild(left);
        foreach (var input in inputs) left.AddChild(input);
        var middle = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; middle.AddThemeConstantOverride("separation", 8); row.AddChild(middle);
        var clock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; clock.AddThemeConstantOverride("separation", 4); middle.AddChild(clock);
        clock.AddChild(Style.Glyph("line_clock", 16, Style.Ink)); clock.AddChild(Style.Text(time, 14, Style.Ink));
        var bar = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(60, 14), Name = "progress" };
        middle.AddChild(bar);
        // Redrawn only when the bar moves a pixel's worth.
        live.Watch(() => (int)(progress() * 200), at => bar.Value = at / 200f);
        if (fuel != null) { fuel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; middle.AddChild(fuel); }
        right = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin }; right.AddThemeConstantOverride("separation", 10); row.AddChild(right);
        if (main != null) right.AddChild(main);
        var extras = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; extras.AddThemeConstantOverride("separation", 10); right.AddChild(extras);
        foreach (var output in side) extras.AddChild(output);
        extras.Visible = extras.GetChildCount() > 0;
    }
}

/// <summary>The fuel position under a process bar: an icon for what it is, its cup or picture, how long it lasts, and what else it takes.</summary>
public partial class FuelRow(string glyph, Color tint, Control cup, Label lasts, Control? accepted = null) : HBoxContainer {
    public override void _Ready() {
        AddThemeConstantOverride("separation", 8);
        AddChild(Style.Glyph(glyph, 24, tint)); AddChild(cup); AddChild(lasts);
        if (accepted != null) AddChild(accepted);
    }
}
