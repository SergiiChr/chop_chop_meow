using Godot;

namespace ChopChopMeow;

/// <summary>The box a fallen player's belongings wait in.</summary>
public partial class RecoveryBoxPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        headingTitle.Text = "Recovery box";
        Well(new VBoxContainer()).AddChild(StorageGrid(thing, 5));
        var row = Row(); row.AddChild(kit.BigButton("Take all", Style.Brick, () => TakeAll(thing)));
        BagGrid(id => moves.Stack(world.S.Backpack, thing.Inventory, id, thing), "Click: store one · Drag to move");
    }
}
