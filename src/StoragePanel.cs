using Godot;

namespace ChopChopMeow;

/// <summary>A chest or other plain storage: its slots, storing everything, and for a chest its filters and haul job.</summary>
public partial class StoragePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        if (thing.Inventory.UnlimitedSlots) {
            headingTitle.Text = "Debug chest";
            headingIcon.TooltipText = "Adds another slot whenever the last one fills.";
        }
        HeaderAction("stash", "Store all materials · keeps tools", () => moves.StoreAll(thing, moves.Materials), Style.ActionRound, "deposit");
        if (thing.Kind == ThingKind.Chest) FilterButton();
        Well(new VBoxContainer()).AddChild(StorageGrid());
        JobButtons();
        StoreBagGrid();
    }
}
