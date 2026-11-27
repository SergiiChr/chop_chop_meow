using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A placed barrel's liquid slot, one-resource filter and hauling job.</summary>
public partial class BarrelPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        headingTitle.Text = world.Data.Name(Barrel.Item);
        headingIcon.Texture = Icons.Of(Barrel.Item); headingIcon.Visible = true;
        FilterButton();
        var (cell, count) = WaterCell();
        moves.TakeCell(cell, thing.Inventory, () => Barrels.LiquidIn(thing), thing,
            held => held.Stack is HeldStack stack && Barrels.Accepts(world.Data, thing, stack.Item),
            (item, units) => moves.Store(thing.Inventory, -1)(item, units));
        ShowContents(world.Data, live, cell, count, () => Barrels.LiquidIn(thing), () => thing.Inventory.Count, StationSlots.SlotHint);
        Well(new HBoxContainer()).AddChild(cell);
        var hint = Row(); hint.AddThemeConstantOverride("separation", 8);
        hint.AddChild(Style.Text("Hold", 14, Style.Beige)); hint.AddChild(Style.Glyph("line_mouse", 24, Style.Rim)); hint.AddChild(Style.Text("to pick up", 14, Style.Beige));
        StoreBagGrid(id => Barrels.Accepts(world.Data, thing, id));
        JobButtons();
    }

    internal static (ItemSlot Cell, Badge Count) WaterCell() {
        var (cell, count) = ItemSlot.Create("barrel_water");
        cell.Icon = Icons.Of(Water.Liquid);
        return (cell, count);
    }

    internal static void ShowLiters(GameData data, LiveUpdates live, ItemSlot cell, Badge count, Func<int> liters, string takeHint) {
        ShowContents(data, live, cell, count, static () => Water.Liquid, liters, takeHint);
    }

    internal static void ShowContents(GameData data, LiveUpdates live, ItemSlot cell, Badge count, Func<string> liquid, Func<int> liters, string takeHint) {
        live.Watch(() => (item: liquid(), held: liters()), value => {
            var (item, held) = value;
            string what = item == BerryJam.Liquid ? "Berry jam" : "Water";
            cell.Icon = Icons.Of(item);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = $"{held}/{Barrel.Capacity} L";
            string title = $"{what} · {held}/{Barrel.Capacity} L";
            if (held > 0 && FoodDescription.Of(data, item) is ItemDef food) FoodDescription.SetTooltip(cell, title, item, food);
            else {
                cell.Popup = null;
                cell.TooltipText = $"{title}\n{takeHint}\nPut a filled vessel here to pour";
            }
        });
    }
}
