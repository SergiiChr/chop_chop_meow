using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The process row every station shares: inputs with what one batch takes under each, the batch time over the progress bar,
/// the fuel under it with how long it lasts, and the main output with its side outputs in a second row.
/// </summary>
public partial class ProcessRow(LiveUpdates live, IEnumerable<Control> inputs, string time, Func<float> progress, Control? fuel, Control? main, IEnumerable<Control> side) : PanelContainer {
    // The input slots and the output slots, each group in a box of its own.
    private HBoxContainer left = null!;
    private VBoxContainer right = null!;
    private Tween? shake;

    /// <summary>Whether the slots are shaking now.</summary>
    public bool Shaking => shake != null && shake.IsValid();
    /// <summary>How many shakes have begun, so a check can tell one ran even when a slow frame has already ended it.</summary>
    public int Shakes { get; private set; }

    /// <summary>Shakes the input and output slots from side to side and back to rest, to show they are taken; a shake under way goes on as it is.</summary>
    public void Shake() {
        if (Shaking) return;
        shake = Shaker.Shake(this, left, right);
        Shakes++;
    }

    public override void _Ready() {
        AddThemeStyleboxOverride("panel", Style.Well); MouseFilter = MouseFilterEnum.Pass;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); AddChild(row);
        left = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin }; left.AddThemeConstantOverride("separation", 12); row.AddChild(left);
        foreach (var input in inputs) left.AddChild(input);
        var middle = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; middle.AddThemeConstantOverride("separation", 8); row.AddChild(middle);
        var clock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; clock.AddThemeConstantOverride("separation", 4); middle.AddChild(clock);
        var timeLabel = Style.Text(time, 14, Style.Ink); timeLabel.Name = "batch_time";
        clock.AddChild(Style.Glyph("line_clock", 16, Style.Ink)); clock.AddChild(timeLabel);
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

/// <summary>The fuel position under a process bar: an icon for what it is, its slot or picture, how long it lasts, and what else it takes.</summary>
public partial class FuelRow(string glyph, Color tint, Control slot, Label lasts, Control? accepted = null) : HBoxContainer {
    public override void _Ready() {
        AddThemeConstantOverride("separation", 8);
        AddChild(Style.Glyph(glyph, 24, tint)); AddChild(slot); AddChild(lasts);
        if (accepted != null) AddChild(accepted);
    }
}

/// <summary>Swings controls from side to side and back to rest, to show a refusal.</summary>
public static class Shaker {
    /// <summary>How long a shake lasts, in seconds, how far it swings to each side, in pixels, and how many times it swings.</summary>
    private const float Seconds = .4f, Reach = 8, Swings = 4;

    /// <summary>Shakes the parts together on a tween of the owner's.</summary>
    public static Tween Shake(Node owner, params Control[] parts) {
        var homes = new Vector2[parts.Length];
        bool placed = false;
        var tween = owner.CreateTween();
        tween.TweenMethod(Callable.From<float>(done => {
            // The parts' places are read on the first step: a row drawn this frame is laid out only by then.
            if (!placed) { placed = true; for (int i = 0; i < parts.Length; i++) homes[i] = parts[i].Position; }
            var swing = new Vector2(MathF.Sin(done * MathF.Tau * Swings) * (1 - done) * Reach, 0);
            for (int i = 0; i < parts.Length; i++) parts[i].Position = homes[i] + swing;
        }), 0f, 1f, Seconds);
        return tween;
    }
}
