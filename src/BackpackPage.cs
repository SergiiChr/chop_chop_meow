using Godot;

namespace ChopChopMeow;

/// <summary>The backpack is only the drawer; the side panel stays hidden while it shows.</summary>
public partial class BackpackPage(PageTools tools) : PagePanel(tools) {
    public override bool OpensBackpack => true;
    protected override void Build() {
        BagGrid(null, BagHint);
        panel.DrawerOnly();
    }
}
