using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A slot as the map draws it: filled for an item made before, a dashed outline for an undiscovered one.</summary>
public partial class MapSlot : Control {
    public bool Known { get; set; }
    public bool OverLight { get; init; }
    public int MarkSize { get; init; } = 12;
    private static readonly StyleBoxFlat OnLight = Style.Flat(Style.Line, 12, 0);
    private static readonly StyleBoxFlat Unknown = Style.Flat(new Color(Style.SlotFill, .14f), 12, 0);

    public override void _Ready() {
        if (Known) return;
        var mark = Style.Text(MapItemLink.QuestionMark, MarkSize, Style.Rim, Style.Head);
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(mark);
    }

    public override void _Draw() {
        var rect = new Rect2(Vector2.Zero, Size);
        if (OverLight && !Known) DrawStyleBox(OnLight, rect);
        Paint(this, rect, Known);
    }

    /// <summary>Paints a slot on any control.</summary>
    public static void Paint(Control on, Rect2 rect, bool known) {
        if (known) { on.DrawStyleBox(Style.SlotNormal, rect); return; }
        on.DrawStyleBox(Unknown, rect);
        float corner = Math.Min(6, rect.Size.X / 4);
        Vector2 a = rect.Position, b = rect.End;
        on.DrawDashedLine(new Vector2(a.X + corner, a.Y + 1), new Vector2(b.X - corner, a.Y + 1), new Color(Style.Beige, .68f), 1.5f, 4);
        on.DrawDashedLine(new Vector2(a.X + corner, b.Y - 1), new Vector2(b.X - corner, b.Y - 1), new Color(Style.Beige, .68f), 1.5f, 4);
        on.DrawDashedLine(new Vector2(a.X + 1, a.Y + corner), new Vector2(a.X + 1, b.Y - corner), new Color(Style.Beige, .68f), 1.5f, 4);
        on.DrawDashedLine(new Vector2(b.X - 1, a.Y + corner), new Vector2(b.X - 1, b.Y - corner), new Color(Style.Beige, .68f), 1.5f, 4);
    }
}
