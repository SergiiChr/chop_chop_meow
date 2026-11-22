using Godot;

namespace ChopChopMeow;

/// <summary>The pump's pressure gauge, its handle and the workers assigned to it.</summary>
public partial class WaterPumpPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var controls = Row();
        controls.AddThemeConstantOverride("separation", 16);
        var dial = new PressureGauge { Name = "pump_pressure" };
        controls.AddChild(dial);
        live.Watch(() => thing.FlowRate, flow => { dial.Value = flow; dial.QueueRedraw(); });
        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        column.AddThemeConstantOverride("separation", 6);
        controls.AddChild(column);
        column.AddChild(Style.Heading("Pressure", 20));
        var row = new HBoxContainer();
        column.AddChild(row);
        var handle = kit.Button("Pump", () => { if (kit.Paused) kit.Notify("Resume before pumping."); else panel.Player.UseWaterPump(thing); });
        handle.Name = "pump"; handle.TooltipText = "Pump the handle · Space";
        Style.SkinPill(handle, Style.Sage);
        row.AddChild(handle);
        row.AddChild(Style.Pill("Space", Style.KeyPill, Style.Line, 12));
        JobButtons();
    }
}
