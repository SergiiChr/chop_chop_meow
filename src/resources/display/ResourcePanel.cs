using Godot;

namespace ChopChopMeow;

/// <summary>A tree, rock, bush or the lake: what is left, how to gather it, and the gatherers.</summary>
public partial class ResourcePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        if (thing.Kind == ThingKind.Tree) TreeStock();
        else GatherStock();
        if (TreePlanting.NeedsWater(thing)) TreeWater();
        if (thing.Kind == ThingKind.Leaves) LeafAcorns();
        if (thing.Kind == ThingKind.Water) AddLabel("Collect water · hold right mouse on the lake shore to fill a bottle, or drink if you have none.", 14, Style.Beige);
        if (thing is { Kind: ThingKind.Tree, TreeStage: 1 })
            RedrawOnChange(() => (thing.TreeStage == 1, TreePlanting.NeedsWater(thing)));
        else JobButtons();
    }

    private void GatherStock() {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Icon(Icons.Of(World.ResourceItem(thing)), 44));
        var left = Style.Text("", 16, Style.Ink, wrap: true); left.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(left);
        live.Bind(left, line => {
            if (thing.Kind != ThingKind.Water) line.Append(thing.Remaining).Append(" left · ");
            line.Append(world.GatherBlock(thing) ?? (thing.IsLoose ? "right-click to pick it up" : "hold right mouse to gather"));
        });
    }

    private void TreeStock() {
        var status = Style.Text("", 15, Style.Rim);
        AddChild(status);
        live.Watch(() => TreePlanting.IsAdult(thing) ? "Adult tree" : TreePlanting.NeedsWater(thing) ? "Planted" : "Growing",
            shown => status.Text = shown);
        var (slot, count) = ItemSlot.Create("tree_wood");
        slot.Icon = Icons.Of("wood");
        slot.MouseFilter = MouseFilterEnum.Ignore;
        slot.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        AddChild(slot);
        live.Watch(() => thing.Remaining, amount => count.Text = amount.ToString());
    }

    private void LeafAcorns() {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Icon(Icons.Of(TreePlanting.Acorn), 44));
        var count = Style.Text("", 16, Style.Ink);
        row.AddChild(count);
        live.Bind(count, line => line.Append(LeafLitter.Acorns(thing)).Append(" left"));
    }

    private void TreeWater() {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(stationSlots.Slot(thing.Inventory, Water.Liquid, true,
            count => $"Water · {count}/{TreePlanting.WaterNeeded} L",
            takes: held => held.Stack is { Box: 0 } stack && TreePlanting.NeedsWater(thing) && Barrels.CanPour(world.Data, stack.Item)));
        var need = Style.Text("", 13, Style.Ink, wrap: true);
        need.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        need.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(need);
        var water = Style.InWell(row);
        AddChild(water);
        live.Bind(need, line => line.Append("Needs ").Append(TreePlanting.WaterNeeded - thing.Inventory[Water.Liquid]).Append(" L of water to start growing."));
        StoreBagGrid(item => TreePlanting.NeedsWater(thing) && Barrels.CanPour(world.Data, item));
    }
}
