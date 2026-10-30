using Godot;

namespace ChopChopMeow;

/// <summary>The box a fallen player's belongings wait in.</summary>
public partial class RecoveryBoxPanel(SidePanel side, Thing t) : PagePanel(side) {
    public override void _Ready() {
        headingTitle.Text = "Recovery box";
        Well(new VBoxContainer()).AddChild(StorageGrid(t, 5));
        var row = Row(); row.AddChild(kit.BigButton("Take all", Style.Brick, () => TakeAll(t)));
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Drag to move");
    }
}
