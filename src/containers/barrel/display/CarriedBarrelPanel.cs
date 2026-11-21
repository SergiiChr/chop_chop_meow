using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The backpack's one barrel, with its water reachable while the barrel stays in the bag.</summary>
public partial class CarriedBarrelPanel(PageTools tools) : PagePanel(tools) {
    private string? Item => Barrel.In(world.S.Backpack);

    public override bool Current => Item != null && !(world.Held is { Box: 0 } held && Barrel.Is(held.Item));
    public override Page GivesWayTo => Page.Backpack;
    protected override bool StoresFromHotbar => true;
    protected override bool PoursFromBag(string item) => CanPour(item);

    protected override void Build() {
        headingIcon.Texture = Icons.Of(Barrel.Item); headingIcon.Visible = true;
        var (cell, count) = BarrelPanel.WaterCell();
        cell.Controls = new SlotInput(cursor) {
            Click = () => kit.Run(() => Take(1)),
            Batch = () => kit.Run(() => Take(Barrel.Capacity)),
            Takes = held => held.Stack is { Box: 0 } stack && CanPour(stack.Item),
            Put = (_, units) => kit.Run(() => {
                if (Item is string barrel && cursor.Stack is HeldStack stack)
                    world.FromHeldStack(() => world.MoveInBarrel(barrel, stack.Item, Math.Min(units, stack.Count), true));
            }),
        };
        BarrelPanel.ShowLiters(live, cell, count, () => Item is string barrel ? Barrel.Liters(barrel) : 0,
            "LMB: take 1 L · Shift or Ctrl: fill backpack vessels");
        Well(new HBoxContainer()).AddChild(cell);
        AddButton("Put it down", () => { if (Item is string barrel) panel.Player.PlaceContainer(barrel); });
        BagGrid(id => {
            if (Item is string barrel && CanPour(id)) world.MoveInBarrel(barrel, id, ItemMoves.Amount(world.S.Backpack, id), true);
        }, $"{BagHint}\nShift: pour a stack · Ctrl: all of it", wanted: CanPour);
    }

    private bool CanPour(string item) => item != Water.Liquid && Barrels.CanPour(world.Data, item);

    private void Take(int liters) {
        if (Item is string barrel) world.MoveInBarrel(barrel, Water.Liquid, liters, false);
    }
}
