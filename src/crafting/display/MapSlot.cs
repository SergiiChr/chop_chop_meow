using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A slot as the map draws it: filled for an item made before, a dashed outline for one not made yet.</summary>
public partial class MapSlot : Control {
    public bool Known { get; set; }
    private static readonly StyleBoxFlat Unknown = Style.Flat(new Color(Style.SlotFill, .14f), 12, 0);

    public override void _Draw() => Paint(this, new Rect2(Vector2.Zero, Size), Known);

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
