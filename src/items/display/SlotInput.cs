using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// What an item slot does with the mouse, through the cursor:
/// a left click picks up its stack, or puts down what the cursor holds; a right click picks up half, or puts down one;
/// a double click uses the item; Ctrl or Shift moves a batch to the other inventory open.
/// </summary>
public sealed class SlotInput(HeldCursor cursor) {
    /// <summary>How soon a second press must follow the first to make a double click.</summary>
    public const ulong DoubleClickMs = 200;

    /// <summary>The slot whose stack a click picks up, or null when there is nothing to pick up.</summary>
    public Func<(Inventory Box, int Slot)?>? Pick { get; init; }
    /// <summary>Whether what the cursor holds can go here; when it cannot, the slot shakes.</summary>
    public Func<HeldCursor, bool>? Takes { get; init; }
    /// <summary>Puts down this many of what the cursor holds here, once <see cref="Takes"/> allows it.</summary>
    public Action<HeldCursor, int>? Put { get; init; }
    /// <summary>A Ctrl- or Shift-click with nothing held.</summary>
    public Action? Batch { get; init; }
    /// <summary>A double click with nothing held; with one set, a single click waits <see cref="DoubleClickMs"/> to tell the two apart.</summary>
    public Action? Use { get; init; }
    /// <summary>A left click with nothing held on a slot that has nothing to pick up.</summary>
    public Action? Click { get; init; }
    /// <summary>A right click with nothing held on a slot that has nothing to pick up.</summary>
    public Action? RightClick { get; init; }

    private SceneTreeTimer? waiting;
    // Set when a second press comes within the double click's time, for its release to use the item.
    private bool doubled;

    /// <summary>A button goes down on the slot; <paramref name="second"/> tells that it follows another press within <see cref="DoubleClickMs"/>.</summary>
    public void Press(bool second) {
        if (second && waiting != null) { waiting = null; doubled = true; }
    }

    /// <summary>A button pressed on <paramref name="slot"/> is let go of there, which does what the click does.</summary>
    public void Release(ItemSlot cell, MouseButton button) {
        if (button == MouseButton.Left) {
            if (doubled) { doubled = false; if (cursor.Empty) Use?.Invoke(); return; }
            if (!cursor.Empty) { PutDown(cell, int.MaxValue); return; }
            if (Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Shift)) { Batch?.Invoke(); return; }
            if (Use == null) { Single(false); return; }
            // A click still waiting for a second press has had its time, as this one came too late to make a double click.
            if (waiting != null) { waiting = null; Single(false); }
            var timer = waiting = cell.GetTree().CreateTimer(DoubleClickMs / 1000.0);
            timer.Timeout += () => { if (waiting == timer && GodotObject.IsInstanceValid(cell) && cell.IsInsideTree()) { waiting = null; Single(false); } };
        } else if (button == MouseButton.Right) {
            if (!cursor.Empty) PutDown(cell, 1);
            else if (Pick?.Invoke() == null) RightClick?.Invoke();
            else Single(true);
        }
    }

    /// <summary>Picks up the slot's stack, or half of it, or clicks a slot with nothing to pick up.</summary>
    private void Single(bool half) {
        if (Pick?.Invoke() is not (Inventory box, int slot)) { if (!half) Click?.Invoke(); return; }
        int units = box.Layout[slot].Free;
        cursor.PickUp(box, slot, half ? (units + 1) / 2 : units);
    }

    private void PutDown(ItemSlot cell, int units) {
        // Back on its own slot, whatever the slot takes: a left click lets go of the stack and a right click puts one back.
        if (cursor.Stack is HeldStack && Pick?.Invoke() is (Inventory box, int slot) && cursor.From(box, slot)) { cursor.PutBack(box, slot, units); return; }
        if (Takes?.Invoke(cursor) != true || Put == null) { cell.Shake(); return; }
        Put(cursor, units);
    }
}
