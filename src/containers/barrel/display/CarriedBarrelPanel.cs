using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The water slot of an opened backpack barrel.</summary>
public partial class CarriedBarrelPanel(PageTools tools, int barrelSlot) : PagePanel(tools) {
    public override bool OpensBackpack => true;
    private string? Item {
        get {
            var layout = world.Backpack.Layout;
            if ((uint)barrelSlot >= (uint)layout.Count) return null;
            var slot = layout[barrelSlot];
            return slot.Count > 0 && slot.Held == 0 && Barrel.Is(slot.Item) ? slot.Item : null;
        }
    }

    public override bool Current => Item != null;
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
                    world.FromHeldStack(() => world.MoveInBarrel(barrel, stack.Item, Math.Min(units, stack.Count), true, barrelSlot));
            }),
        };
        BarrelPanel.ShowContents(live, cell, count, () => Item is string barrel ? Barrel.LiquidOf(barrel) : Water.Liquid, () => Item is string barrel ? Barrel.Liters(barrel) : 0,
            "LMB: take 1 L · Shift or Ctrl: fill backpack vessels");
        Well(new HBoxContainer()).AddChild(cell);
        AddButton("Put it down", () => { if (Item is string barrel) panel.Player.PlaceContainer(barrel, barrelSlot); });
        BagGrid(id => {
            if (Item is string barrel && CanPour(id)) world.MoveInBarrel(barrel, id, ItemMoves.Amount(world.S.Backpack, id), true, barrelSlot);
        }, $"{BagHint}\nShift: pour a stack · Ctrl: all of it", wanted: CanPour, put: PutInBackpack);
    }

    private void PutInBackpack(int slot, string item, int count) {
        bool swapsBarrel = slot == barrelSlot && item != Item && world.Held is { Box: 0 } held &&
            count == world.Backpack.Layout[held.Slot].Count;
        moves.Store(world.S.Backpack, slot)(item, count);
        if (swapsBarrel) panel.Open(Page.Backpack);
    }

    private bool CanPour(string item) => Item is string barrel && world.Data.VesselOf(item) is VesselDef vessel
        && vessel.LitersIn(item) > 0 && (Barrel.Liters(barrel) == 0 || Barrel.LiquidOf(barrel) == vessel.Liquid);

    private void Take(int liters) {
        if (Item is string barrel) world.MoveInBarrel(barrel, Barrel.LiquidOf(barrel), liters, false, barrelSlot);
    }
}
