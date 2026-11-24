using Godot;

namespace ChopChopMeow;

/// <summary>A dig marker's instruction or a finished hole's slot.</summary>
public partial class DigSitePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        if (thing.Kind == ThingKind.PlantingSpot) {
            AddLabel("Hold RMB with a shovel to dig.", color: Style.Rim);
            return;
        }
        var slot = stationSlots.Slot(thing.Inventory, TreePlanting.Acorn, true, _ => "Place one acorn here to plant it.",
            takes: held => held.Stack?.Item == TreePlanting.Acorn);
        slot.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        AddChild(slot);
        StoreBagGrid(item => item == TreePlanting.Acorn);
    }
}
