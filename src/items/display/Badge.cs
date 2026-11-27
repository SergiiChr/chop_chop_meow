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
