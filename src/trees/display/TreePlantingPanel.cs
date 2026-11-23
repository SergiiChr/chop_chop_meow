using Godot;

namespace ChopChopMeow;

/// <summary>What a marked planting spot or a sapling still needs.</summary>
public partial class TreePlantingPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        if (thing.Kind == ThingKind.PlantingSpot) {
            AddLabel("Hold right mouse here with a shovel and an acorn in your backpack.", color: Style.Rim);
            return;
        }
        Dynamic(text => {
            if (TreePlanting.NeedsWater(thing))
                text.Append("Needs ").Append(TreePlanting.WaterNeeded - thing.Inventory[Water.Liquid]).Append(" L of water to start growing.");
            else text.Append("Growth · ").Append(TreePlanting.DaysGrown(thing)).Append('/').Append(TreePlanting.GrowDays)
                .Append(" days · ").Append(thing.Remaining).Append(" wood");
        }, color: Style.Rim);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(stationSlots.Slot(thing.Inventory, Water.Liquid, true,
            count => $"Water · {count}/{TreePlanting.WaterNeeded} L",
            takes: held => held.Stack is { Box: 0 } stack && TreePlanting.NeedsWater(thing) && Barrels.CanPour(world.Data, stack.Item)));
        var hint = Style.Text("Pour from a filled bottle or bucket.", 13, Style.Ink, wrap: true);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(hint);
        var water = Style.InWell(row);
        AddChild(water);
        live.Watch(() => TreePlanting.NeedsWater(thing), needed => water.Visible = needed);
        if (thing.TreeStage != 1) JobButtons();
        RedrawOnChange(() => thing.TreeStage == 1);
        StoreBagGrid(item => TreePlanting.NeedsWater(thing) && Barrels.CanPour(world.Data, item));
    }
}
