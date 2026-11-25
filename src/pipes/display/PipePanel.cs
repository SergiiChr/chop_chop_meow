using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The water standing inside one buried pipe and the pressure its connected pumps supply.</summary>
public partial class PipePanel(PageTools tools, Vector2I tile) : PagePanel(tools) {
    public override bool Current => world.SteelPipes.Laid(tile);

    protected override void Build() {
        headingTitle.Text = "Steel pipe";
        headingIcon.Texture = Icons.Of("steel_pipe");
        headingIcon.Visible = true;
        var water = Style.Heading("", 20);
        water.Name = "pipe_water";
        var pressure = Style.Text("", 16, Style.Rim);
        pressure.Name = "pipe_pressure";
        AddChild(water);
        AddChild(pressure);
        live.Watch(() => {
            var reading = world.PipeReading(tile);
            int ml = reading.Liters <= 0 ? 0 : reading.Liters >= 1 ? 1000 : Math.Clamp(Mathf.RoundToInt(reading.Liters * 1000), 1, 999);
            return (ml, jam: world.PipeLiquid(tile) == BerryJam.Liquid, percent: Mathf.RoundToInt(reading.Pressure));
        }, value => {
            string item = value.jam ? "Berry jam · " : "";
            water.Text = item + (value.ml switch {
                0 => "Empty · 0/1 L",
                1000 => "Full · 1/1 L",
                _ => FormattableString.Invariant($"Partly full · {value.ml / 1000f:0.###}/1 L")
            });
            pressure.Text = $"Pressure {value.percent}%";
        });
    }
}
