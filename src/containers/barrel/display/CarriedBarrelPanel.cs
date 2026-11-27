using System;
using Godot;

namespace ChopChopMeow;

/// <summary>The water slot of an opened backpack barrel.</summary>
public partial class CarriedBarrelPanel(PageTools tools, int barrelSlot) : PagePanel(tools) {
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
        BarrelPanel.ShowContents(world.Data, live, cell, count, () => Barrel.LiquidOf(Unit), () => Barrel.Liters(Unit),
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

    /// <summary>The barrel itself while it holds something, or null for an empty one.</summary>
    private Item? Unit => Item is string barrel ? world.S.Backpack.Object(barrel) : null;

    private bool CanPour(string item) => Item != null && world.Data.VesselOf(item) != null && world.S.Backpack.Object(item) is { } vessel
        && Vessels.LitersIn(vessel) > 0 && (Barrel.Liters(Unit) == 0 || Barrel.LiquidOf(Unit) == Vessels.LiquidIn(vessel));

    private void Take(int liters) {
        if (Item is string barrel) world.MoveInBarrel(barrel, Barrel.LiquidOf(Unit), liters, false, barrelSlot);
    }
}
