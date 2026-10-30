using Godot;

namespace ChopChopMeow;

/// <summary>A chest or other plain storage: its slots, storing everything, and for a chest its filters.</summary>
public partial class StoragePanel(SidePanel side, Thing t) : PagePanel(side) {
    public override void _Ready() {
        HeaderAction("stash", "Store all materials · keeps tools", () => StoreAll(t), Style.ActionRound, "deposit");
        if (t.Kind == ThingKind.Chest) FilterButton(t);
        Well(new VBoxContainer()).AddChild(StorageGrid(t));
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move");
    }
}
