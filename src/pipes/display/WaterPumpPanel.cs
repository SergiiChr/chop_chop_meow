using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The pump's pressure and flow, its handle and the workers assigned to it.</summary>
public partial class WaterPumpPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var reading = Style.Heading("", 20);
        reading.Name = "pump_reading";
        AddChild(reading);
        live.Watch(() => (Mathf.RoundToInt(thing.Pressure), Mathf.RoundToInt(thing.FlowRate * 10)), value => {
            reading.Text = FormattableString.Invariant($"Pressure {value.Item1}% · {value.Item2 / 10f:0.0} L/s");
        });
        var row = Row();
        var handle = kit.Button("Pump", () => { if (kit.Paused) kit.Notify("Resume before pumping."); else panel.Player.UseWaterPump(thing); });
        handle.Name = "pump"; handle.TooltipText = "Pump the handle · Space";
        Style.SkinPill(handle, Style.Sage);
        row.AddChild(handle);
        row.AddChild(Style.Pill("Space", Style.KeyPill, Style.Line, 12));
        JobButtons();
    }
}
