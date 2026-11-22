using System.Globalization;
using Godot;

namespace ChopChopMeow;

/// <summary>The pump dial shows liters per second, with a half-liter tick between each numbered mark.</summary>
public partial class PressureGauge : Godot.Range {
    private const int Diameter = 176, FullFlow = 5;
    private const float Radius = Diameter / 2f - 2;

    public PressureGauge() {
        CustomMinimumSize = Vector2.One * Diameter;
        MouseFilter = MouseFilterEnum.Ignore;
        MaxValue = FullFlow;
        Step = 0;
        AccessibilityName = "Pressure";
        AccessibilityDescription = "Pump flow in liters per second";
    }

    public override void _Ready() {
        for (int liters = 0; liters <= FullFlow; liters++)
            Marking(liters.ToString(CultureInfo.InvariantCulture), Needle((float)liters / FullFlow) * (Radius - 17), 20);
        Marking("L/s", Vector2.Down * (Radius * .48f), 17);
    }

    private void Marking(string text, Vector2 offset, int fontSize) {
        var label = Style.Text(text, fontSize, Style.Ink, Style.Head);
        AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        label.Position = (Size - label.Size) / 2 + offset;
    }

    public override void _Draw() {
        var center = Size / 2;
        DrawCircle(center, Radius, Style.Line, true, -1, true);
        DrawCircle(center, Radius - 2, Style.Mustard, true, -1, true);
        DrawCircle(center, Radius - 5, Style.SlotFill, true, -1, true);
        float scale = Radius - 30;
        int divisions = FullFlow * 2;
        for (int mark = 0; mark <= divisions; mark++) {
            var direction = Needle((float)mark / divisions);
            bool wholeLiter = mark % 2 == 0;
            DrawLine(center + direction * (scale - (wholeLiter ? 13 : 7)),
                center + direction * scale, Style.Ink, wholeLiter ? 3 : 1.5f, true);
        }
        var needle = Needle((float)Ratio);
        DrawLine(center - needle * 9, center + needle * (scale - 2), Style.Brick, 4, true);
        DrawCircle(center, 6, Style.Line, true, -1, true);
        DrawCircle(center, 3.5f, Style.Mustard, true, -1, true);
    }

    private static Vector2 Needle(float fraction) =>
        Vector2.Up.Rotated(-Mathf.DegToRad(WorldModels.Gauge(fraction)));
}
