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
        if (Empty || ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right } click) return;
        if (GetViewport().GuiGetHoveredControl() is not Control over || over is ItemSlot { Controls: not null }) return;
        if (click is { AltPressed: true, ButtonIndex: MouseButton.Left } && over is ItemSlot { RecipeLink: not null }) return;
        Release();
    }

    public override void _Process(double delta) {
        // Over the ground the buried pipe preview already shows the held plan at the placement tile.
        string? item = Plan == "steel_piping" && GetViewport().GuiGetHoveredControl() == null ? null : Item;
        (string? Item, int Count) now = (item, Plan != null ? 0 : Stack?.Count ?? 0);
        if (now != shown) {
            shown = now;
            Visible = now.Item != null;
            if (now.Item != null) icon.Texture = Icons.Of(Vessels.LookOf(world.Data, world.HeldBox, now.Item));
            count.Text = now.Item != null && now.Count > 1 ? now.Count.ToString() : "";
        }
        if (Visible) GlobalPosition = GetGlobalMousePosition();
    }
}
