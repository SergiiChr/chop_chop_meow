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
public partial class ItemSlot : Button
{
    // Godot drag data must be a Variant, so the C# payload rides alongside it for the drag's lifetime.
    private static ItemDrag? dragging;
    public Func<ItemDrag?>? Drag { get; set; }
    public Func<ItemDrag, bool>? CanTake { get; set; }
    public Action<ItemDrag>? Take { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        dragging = Drag?.Invoke();
        if (dragging == null) return default;
        var preview = new Control();
        preview.AddChild(new TextureRect { Texture = Icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(40, 40), Position = new Vector2(-20, -20), Modulate = new Color(1, 1, 1, .85f) });
        SetDragPreview(preview);
        return dragging.Item;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => dragging != null && CanTake?.Invoke(dragging) == true;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (dragging != null) Take?.Invoke(dragging);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd) dragging = null;
    }
}
