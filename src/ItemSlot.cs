using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// What a drag carries.
/// From is null for references, such as a building plan or a hotbar palette entry, which move no items.
/// Container is the building the items come from; the backpack itself has none.
/// </summary>
public sealed record ItemDrag(string Item, int Count, Inventory? From, Thing? Container, int SourceSlot = -1);

/// <summary>The pill on a cup's corner: it shows only while it has something to count.</summary>
public partial class Badge : Label {
    public new string Text {
        get => base.Text;
        set { base.Text = value; Visible = value != ""; }
    }
}

/// <summary>An icon button that can start an item drag and accept one.</summary>
public partial class ItemSlot : Button {
    public static readonly StringName IconNormal = "icon_normal_color";

    /// <summary>A cream cup for one stack, with its count on a pill at the corner.</summary>
    public static (ItemSlot Cell, Badge Count) Cup(string name, float size = 52) {
        var cell = new ItemSlot {
            Name = name,
            CustomMinimumSize = new Vector2(size, size),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Center,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        Style.Skin(cell, Style.CupNormal, Style.CupHover, Style.CupPressed);
        cell.AddThemeConstantOverride("icon_max_width", (int)(size * .74f));
        var count = CountBadge();
        cell.AddChild(count);
        // A packed crate shows what it holds on a round badge at the other corner.
        var badge = Style.Corner(Style.Icon(null, 24), 0, 0); badge.Name = "badge"; badge.Visible = false;
        cell.AddChild(badge);
        return (cell, count);
    }

    private static Badge CountBadge() {
        var count = new Badge { MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
        count.AddThemeStyleboxOverride("normal", Style.CountPill);
        count.AddThemeFontOverride("font", Style.Bold); count.AddThemeFontSizeOverride("font_size", 12); count.AddThemeColorOverride("font_color", Style.Rim);
        return Style.Corner(count, 1, 1);
    }

    // Godot drag data must be a Variant, so the C# payload rides alongside it for the drag's lifetime.
    private static ItemDrag? dragging;
    public Func<ItemDrag?>? Drag { get; set; }
    public Func<ItemDrag, bool>? CanTake { get; set; }
    /// <summary>Drops the drag here and returns the units that did not fit, which stay on the cursor.</summary>
    public Func<ItemDrag, int>? Take { get; set; }
    /// <summary>Runs on the second press of a double click, besides the press itself.</summary>
    public Action? DoubleClicked { get; set; }
    /// <summary>Builds a richer tooltip than plain text; the tooltip text must still be non-empty for Godot to show it.</summary>
    public Func<Control>? Popup { get; set; }

    public override GodotObject? _MakeCustomTooltip(string forText) => Popup?.Invoke();

    public override Variant _GetDragData(Vector2 atPosition) {
        dragging = Drag?.Invoke();
        if (dragging == null) return default;
        SetDragPreview(Preview());
        return dragging.Item;
    }

    public override void _GuiInput(InputEvent ev) {
        if (ev is not InputEventMouseButton { Pressed: true } click) return;
        if (click.ButtonIndex == MouseButton.Left && click.DoubleClick) DoubleClicked?.Invoke();
        // The click that drops a split half would also press this slot, whose own action could move the items straight back.
        if (click.ButtonIndex == MouseButton.Left && GetViewport().GuiIsDragging()) AcceptEvent();
        // A right click picks up half of what a drag would take, carried until a left click drops it.
        else if (click.ButtonIndex == MouseButton.Right && Drag?.Invoke() is { Count: > 0 } whole) {
            var half = whole with { Count = (whole.Count + 1) / 2 };
            dragging = half;
            ForceDrag(half.Item, Preview());
            AcceptEvent();
        }
    }

    private Control Preview() {
        var preview = new Control();
        preview.AddChild(new TextureRect { Texture = Icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(40, 40), Position = new Vector2(-20, -20), Modulate = new Color(1, 1, 1, .85f) });
        return preview;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => dragging != null && CanTake?.Invoke(dragging) == true;

    public override void _DropData(Vector2 atPosition, Variant data) {
        if (dragging == null || Take?.Invoke(dragging) is not int left || left <= 0) return;
        Hold(dragging with { Count = left });
    }

    /// <summary>
    /// Puts what did not fit back on the cursor.
    /// This drag ends right after the drop, and a held stack drops on a press, so the rest is picked up again only once the button is up: its release would otherwise drop it at once.
    /// </summary>
    private async void Hold(ItemDrag rest) {
        do await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        while (Input.IsMouseButtonPressed(MouseButton.Left));
        if (!IsInstanceValid(this) || !IsInsideTree()) return;
        dragging = rest; ForceDrag(rest.Item, Preview());
    }

    public override void _Notification(int what) {
        if (what == NotificationDragEnd) dragging = null;
    }
}
