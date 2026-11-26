using Godot;

namespace ChopChopMeow;

/// <summary>The pill on a slot's corner: it shows only while it has something to count.</summary>
public partial class Badge : Label {
    public new string Text {
        get => base.Text;
        set { base.Text = value; Visible = value != ""; }
    }
}
