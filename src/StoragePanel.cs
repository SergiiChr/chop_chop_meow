using Godot;

namespace ChopChopMeow;

/// <summary>A chest or other plain storage: its slots, storing everything, and for a chest its filters.</summary>
public partial class StoragePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        HeaderAction("stash", "Store all materials · keeps tools", () => StoreAll(thing), Style.ActionRound, "deposit");
        if (thing.Kind == ThingKind.Chest) FilterButton();
        Well(new VBoxContainer()).AddChild(StorageGrid(thing));
        BagGrid(id => moves.Stack(world.S.Backpack, thing.Inventory, id, thing), "Click: store one · Ctrl: store all · Drag to move");
    }
}
