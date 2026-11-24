using Godot;

namespace ChopChopMeow;

/// <summary>A finished hole's slot.</summary>
public partial class HolePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var slot = stationSlots.Slot(thing.Inventory, TreePlanting.Acorn, true, _ => "Place one acorn here to plant it.",
            takes: held => held.Stack?.Item == TreePlanting.Acorn);
        slot.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        AddChild(slot);
        StoreBagGrid(item => item == TreePlanting.Acorn);
    }
}
