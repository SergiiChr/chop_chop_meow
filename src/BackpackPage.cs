using Godot;

namespace ChopChopMeow;

/// <summary>The backpack is only the drawer; the side panel stays hidden while it shows.</summary>
public partial class BackpackPage(SidePanel side) : PagePanel(side) {
    public override void _Ready() {
        BagGrid(UseFromBag, UseHint);
        // Only the drawer shows, so the camera keeps the player centred.
        panel.Visible = false; view.PanelOpen = false;
    }
}
