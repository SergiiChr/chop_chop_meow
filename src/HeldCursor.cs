using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// What the cursor holds: a held stack, which the server keeps (<see cref="World.Held"/>), or a building plan, which only this window knows of.
/// It draws what it holds beside the mouse and lets go of it on Esc or when the side panel closes.
/// </summary>
public partial class HeldCursor : Control {
    private readonly World world;
    private readonly TextureRect icon;
    private readonly Badge count;
    // What is drawn now, so the cursor is redrawn only when it changes.
    private (string? Item, int Count) shown;

    /// <summary>The building plan on the cursor, or null.</summary>
    public string? Plan { get; private set; }
    /// <summary>The held stack, or null.</summary>
    public HeldStack? Stack => world.Held;
    public bool Empty => Plan == null && Stack == null;
    /// <summary>The item or plan on the cursor, or null.</summary>
    public string? Item => Plan ?? Stack?.Item;

    public HeldCursor(World world) {
        this.world = world;
        Name = "held"; MouseFilter = MouseFilterEnum.Ignore; ZIndex = 100; TopLevel = true;
        icon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Size = new Vector2(40, 40), Position = new Vector2(-20, -20), MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, .9f) };
        AddChild(icon);
        var frame = new Control { Size = new Vector2(52, 52), Position = new Vector2(-26, -26), MouseFilter = MouseFilterEnum.Ignore }; AddChild(frame);
        count = ItemSlot.CountBadge(); count.Name = "held_count"; frame.AddChild(count);
        Visible = false;
    }

    /// <summary>Picks up <paramref name="units"/> of a slot, letting go of a plan or stack held before.</summary>
    public void PickUp(Inventory box, int slot, int units) {
        Plan = null;
        world.Hold(box, slot, units);
    }

    /// <summary>Picks up a building plan, which goes into placement over open ground and onto a hotbar slot.</summary>
    public void PickUp(string plan) {
        Release();
        Plan = plan;
    }

    /// <summary>Lets go of what the cursor holds: a stack goes back to its slot.</summary>
    public void Release() {
        Plan = null;
        if (Stack != null) world.Release();
    }

    /// <summary>Puts <paramref name="units"/> of the held stack back in its own slot, which lies in <paramref name="box"/>: all of them lets go of it, fewer keep the rest held.</summary>
    public void PutBack(Inventory box, int slot, int units) {
        if (Stack is not HeldStack held) return;
        if (units >= held.Count) world.Release(); else world.Hold(box, slot, held.Count - units);
    }

    /// <summary>Whether the held stack lies in this slot.</summary>
    public bool From(Inventory box, int slot) => Stack is HeldStack held && held.Slot == slot && world.Locate(box) == held.Box;

    /// <summary>A click on the interface anywhere but an item slot lets go of what the cursor holds; the click still does what it does there.</summary>
    public override void _Input(InputEvent ev) {
        if (Empty || ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right }) return;
        if (GetViewport().GuiGetHoveredControl() is not Control over || over is ItemSlot { Controls: not null }) return;
        Release();
    }

    public override void _Process(double delta) {
        // Over the ground the buried pipe preview already shows the held plan at the placement tile.
        string? item = Plan == "steel_piping" && GetViewport().GuiGetHoveredControl() == null ? null : Item;
        (string? Item, int Count) now = (item, Plan != null ? 0 : Stack?.Count ?? 0);
        if (now != shown) {
            shown = now;
            Visible = now.Item != null;
            if (now.Item != null) icon.Texture = Icons.Of(now.Item);
            count.Text = now.Item != null && now.Count > 1 ? now.Count.ToString() : "";
        }
        if (Visible) GlobalPosition = GetGlobalMousePosition();
    }
}

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
