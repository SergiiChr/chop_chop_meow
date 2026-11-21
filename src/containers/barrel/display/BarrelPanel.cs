using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A placed barrel's water slot, one-resource filter and hauling job.</summary>
public partial class BarrelPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        headingTitle.Text = world.Data.Name(Barrel.Item);
        headingIcon.Texture = Icons.Of(Barrel.Item); headingIcon.Visible = true;
        FilterButton();
        var (cell, count) = WaterCell();
        moves.TakeCell(cell, thing.Inventory, Water.Liquid, thing,
            held => held.Stack is HeldStack stack && Barrels.CanPour(world.Data, stack.Item),
            (item, units) => moves.Store(thing.Inventory, -1)(item, units));
        ShowLiters(live, cell, count, () => thing.Inventory[Water.Liquid], StationSlots.SlotHint);
        Well(new HBoxContainer()).AddChild(cell);
        var hint = Row(); hint.AddThemeConstantOverride("separation", 8);
        hint.AddChild(Style.Text("Hold", 14, Style.Beige)); hint.AddChild(Style.Glyph("line_mouse", 24, Style.Rim)); hint.AddChild(Style.Text("to pick up", 14, Style.Beige));
        StoreBagGrid(id => Barrels.CanPour(world.Data, id));
        JobButtons();
    }

    internal static (ItemSlot Cell, Badge Count) WaterCell() {
        var (cell, count) = ItemSlot.Create("barrel_water");
        cell.Icon = Icons.Of(Water.Liquid);
        return (cell, count);
    }

    internal static void ShowLiters(LiveUpdates live, ItemSlot cell, Badge count, Func<int> liters, string takeHint) {
        live.Watch(liters, held => {
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = $"{held}/{Barrel.Capacity} L";
            cell.TooltipText = $"Water · {held}/{Barrel.Capacity} L\n{takeHint}\nPut a vessel holding water here to pour";
        });
    }
}
