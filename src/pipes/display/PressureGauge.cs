using Godot;

namespace ChopChopMeow;

/// <summary>The pump's dial, with one division for each stroke from empty to full pressure.</summary>
public partial class PressureGauge : Godot.Range {
    public override void _Draw() {
        var center = Size / 2;
        float radius = Mathf.Min(Size.X, Size.Y) / 2 - 2;
        DrawCircle(center, radius, Style.Line, true, -1, true);
        DrawCircle(center, radius - 2, Style.Mustard, true, -1, true);
        DrawCircle(center, radius - 5, Style.SlotFill, true, -1, true);
        for (int mark = 0; mark <= 10; mark++) {
            var direction = Needle(mark / 10f);
            bool major = mark % 5 == 0;
            DrawLine(center + direction * (radius - (major ? 17 : 13)),
                center + direction * (radius - 8), Style.Ink, major ? 2.5f : 1.5f, true);
        }
        var needle = Needle((float)Ratio);
        DrawLine(center - needle * 6, center + needle * (radius - 13), Style.Brick, 3, true);
        DrawCircle(center, 4.5f, Style.Line, true, -1, true);
        DrawCircle(center, 2.5f, Style.Mustard, true, -1, true);
    }

    private static Vector2 Needle(float fraction) =>
        Vector2.Up.Rotated(-Mathf.DegToRad(WorldModels.Gauge(fraction)));
}
