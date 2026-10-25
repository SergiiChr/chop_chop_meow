using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// What a drag carries.
/// From is null for references, such as a building plan or a hotbar palette entry, which move no items.
/// Container is the building the items come from; the backpack itself has none.
/// </summary>
public sealed record ItemDrag(string Item, int Count, Inventory? From, Thing? Container);

/// <summary>An icon button that can start an item drag and accept one.</summary>
public partial class ItemSlot : Button {
    // Godot drag data must be a Variant, so the C# payload rides alongside it for the drag's lifetime.
    private static ItemDrag? dragging;
    public Func<ItemDrag?>? Drag { get; set; }
    /// <summary>What a right click picks up: half the stack, carried until a left click drops it.</summary>
    public Func<ItemDrag?>? Half { get; set; }
    public Func<ItemDrag, bool>? CanTake { get; set; }
    /// <summary>Drops the drag here and returns the units that did not fit, which stay on the cursor.</summary>
    public Func<ItemDrag, int>? Take { get; set; }
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
        // The click that drops a split half would also press this slot, whose own action could move the items straight back.
        if (click.ButtonIndex == MouseButton.Left && GetViewport().GuiIsDragging()) AcceptEvent();
        else if (click.ButtonIndex == MouseButton.Right && Half?.Invoke() is ItemDrag half) {
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
