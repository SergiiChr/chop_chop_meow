using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Shows what a resource yields, any gathering requirements, and its worker jobs.</summary>
public partial class ResourcePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        if (thing.Kind == ThingKind.Tree) TreeStatus();
        var stock = new HBoxContainer();
        stock.AddThemeConstantOverride("separation", 8);
        AddChild(stock);
        if (thing.Kind == ThingKind.Water) StockSlot(stock, Water.Liquid, null);
        else StockSlot(stock, World.ResourceItem(thing), () => thing.Remaining);
        if (thing.Kind == ThingKind.Leaves) StockSlot(stock, TreePlanting.Acorn, () => LeafLitter.Acorns(thing));

        if (TreePlanting.NeedsWater(thing)) TreeWater();
        if (thing is { Kind: ThingKind.Tree, TreeStage: 1 }) {
            RedrawOnChange(() => (thing.TreeStage == 1, TreePlanting.NeedsWater(thing)));
        } else {
            GatherNote();
            JobButtons();
        }
    }

    private void StockSlot(HBoxContainer stock, string item, Func<int>? amount) {
        var (slot, count) = ItemSlot.Create($"resource_{item}");
        slot.Icon = Icons.Of(item);
        slot.MouseFilter = MouseFilterEnum.Ignore;
        stock.AddChild(slot);
        if (amount != null) live.Watch(amount, units => count.Text = units.ToString());
    }

    private void GatherNote() {
        string? hint = thing.Kind == ThingKind.Water ? "Hold RMB to drink or fill a container." : null;
        var note = Style.Wrapped("", 14, Style.Beige);
        AddChild(note);
        live.Watch(() => world.GatherBlock(thing) ?? hint, message => {
            note.Text = message ?? "";
            note.Visible = message != null;
        });
    }

    private void TreeStatus() {
        var status = Style.Text("", 15, Style.Rim);
        AddChild(status);
        live.Watch(() => TreePlanting.IsAdult(thing) ? "Adult tree" : TreePlanting.NeedsWater(thing) ? "Planted" : "Growing",
            shown => status.Text = shown);
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
