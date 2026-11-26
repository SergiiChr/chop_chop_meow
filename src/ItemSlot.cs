using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The pill on a slot's corner: it shows only while it has something to count.</summary>
public partial class Badge : Label {
    public new string Text {
        get => base.Text;
        set { base.Text = value; Visible = value != ""; }
    }

    /// <summary>A stack's quantity, or the number of live firebugs when the slot holds a lantern.</summary>
    internal void SetStack(int quantity, int bugs) =>
        SetCount(bugs >= 0 ? bugs.ToString() : quantity > 1 ? quantity.ToString() : "", bugs >= 0);

    internal void SetCount(string value, bool firebugs = false) {
        float corner = firebugs ? 0 : 1;
        if (AnchorTop != corner) Style.Corner(this, 1, corner);
        AddThemeStyleboxOverride("normal", firebugs ? Style.FirebugCountPill : Style.CountPill);
        AddThemeColorOverride("font_color", firebugs ? Style.FirebugGreen : Style.Rim);
        Text = value;
    }
}

/// <summary>An icon button for an item: an item slot when <see cref="Controls"/> is set, else a plain button.</summary>
public partial class ItemSlot : Button {
    public static readonly StringName IconNormal = "icon_normal_color";

    /// <summary>A cream slot for one stack, with its count on a pill at the corner.</summary>
    public static (ItemSlot Cell, Badge Count) Create(string name, float size = 52) {
        var cell = new ItemSlot {
            Name = name,
            CustomMinimumSize = new Vector2(size, size),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Center,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        Style.Skin(cell, Style.SlotNormal, Style.SlotHover, Style.SlotPressed);
        cell.AddThemeConstantOverride("icon_max_width", (int)(size * .74f));
        var count = CountBadge();
        cell.AddChild(count);
        // A packed crate shows what it holds on a round badge at the other corner.
        var badge = Style.Corner(Style.Icon(null, 24), 0, 0); badge.Name = "badge"; badge.Visible = false;
        cell.AddChild(badge);
        return (cell, count);
    }

    /// <summary>A count pill for a corner of a slot: 0 or 1 for left or right, top or bottom.</summary>
    public static Badge CountBadge(float x = 1, float y = 1) {
        var count = new Badge { Name = "count", MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
        count.AddThemeStyleboxOverride("normal", Style.CountPill);
        count.AddThemeFontOverride("font", Style.Bold); count.AddThemeFontSizeOverride("font_size", 12); count.AddThemeColorOverride("font_color", Style.Rim);
        return Style.Corner(count, x, y);
    }

    /// <summary>What the slot does with the mouse as an item slot, or null for a plain button such as a recipe card.</summary>
    public SlotInput? Controls { get; set; }
    /// <summary>Runs on the second press of a double click, besides the press itself.</summary>
    public Action? DoubleClicked { get; set; }
    /// <summary>Runs on a right click on a plain button.</summary>
    public Action? RightClicked { get; set; }
    /// <summary>Builds a richer tooltip than plain text; the tooltip text must still be non-empty for Godot to show it.</summary>
    public Func<Control>? Popup { get; set; }
    // When, where and on which slot by name the left button last went down, to tell a double click by <see cref="SlotInput.DoubleClickMs"/>:
    // a page that draws itself again after the first press puts a new slot of the same name under the second.
    private static ulong lastPress;
    private static Vector2 lastPressAt;
    private static StringName? lastPressOn;
    /// <summary>How far the mouse may move between the presses of a double click.</summary>
    private const float DoubleClickReach = 5;
    private Tween? shake;

    public override GodotObject? _MakeCustomTooltip(string forText) => Popup?.Invoke();

    // The button pressed on this item slot, which acts once it is let go: an action on the press could redraw the page and take the slot away under the mouse.
    private MouseButton down = MouseButton.None;

    public override void _GuiInput(InputEvent ev) {
        if (ev is not InputEventMouseButton click) return;
        if (!click.Pressed) {
            if (Controls == null || click.ButtonIndex != down) return;
            down = MouseButton.None;
            // A press let go of anywhere but this slot, such as over another slot, does nothing.
            if (new Rect2(Vector2.Zero, Size).HasPoint(click.Position)) Controls.Release(this, click.ButtonIndex);
            AcceptEvent();
            return;
        }
        bool second = false;
        if (click.ButtonIndex == MouseButton.Left) {
            ulong now = Time.GetTicksMsec();
            second = lastPress > 0 && now - lastPress <= SlotInput.DoubleClickMs && lastPressOn == Name && click.GlobalPosition.DistanceTo(lastPressAt) <= DoubleClickReach;
            // A third press starts over, so a triple click is a double click and a single one.
            lastPress = second ? 0 : now; lastPressAt = click.GlobalPosition; lastPressOn = Name;
            if (second) DoubleClicked?.Invoke();
        } else lastPress = 0;
        if (Controls != null) {
            if (click.ButtonIndex is MouseButton.Left or MouseButton.Right) { down = click.ButtonIndex; Controls.Press(second); }
            AcceptEvent();
            return;
        }
        if (click.ButtonIndex == MouseButton.Right && RightClicked != null) { RightClicked(); AcceptEvent(); }
    }

    /// <summary>Whether the slot is shaking now.</summary>
    public bool Shaking => shake != null && shake.IsValid();

    /// <summary>Shakes the slot side to side, as a refusal.</summary>
    public void Shake() {
        if (!Shaking) shake = Shaker.Shake(this, this);
    }
}
