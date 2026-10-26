using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

public partial class Game {
    private int hotbarAssignSlot;
    private readonly System.Collections.Generic.List<ItemSlot> hotbarButtons = new();
    private readonly System.Collections.Generic.List<Label> hotbarCounts = new();
    private readonly System.Collections.Generic.Dictionary<string, Texture2D> hotbarIcons = new();
    private Texture2D? itemAtlas;

    private void LoadHotbarAtlases() {
        if (ResourceLoader.Exists("res://assets/item-atlas.png")) itemAtlas = GD.Load<Texture2D>("res://assets/item-atlas.png");
    }

    private Texture2D? HotbarIcon(string id) {
        if (World.IsCrate(id)) id = "crate";
        if (hotbarIcons.TryGetValue(id, out var cached)) return cached;
        // Icons rendered from the game's own models come first; items without a model have drawn icons named by their id.
        foreach (var path in new[] { $"res://assets/icons/{id}.png", $"res://assets/ui/{id}.svg" })
            if (ResourceLoader.Exists(path)) return hotbarIcons[id] = GD.Load<Texture2D>(path);
        int cell = id == "water" ? 10 : -1;
        if (itemAtlas == null || cell < 0) return null;
        const int Columns = 4, Rows = 3;
        float width = itemAtlas.GetWidth() / (float)Columns, height = itemAtlas.GetHeight() / (float)Rows;
        return hotbarIcons[id] = new AtlasTexture { Atlas = itemAtlas, Region = new Rect2(cell % Columns * width, cell / Columns * height, width, height) };
    }

    /// <summary>A hotbar slot; dropping an item or building plan on it assigns that reference.</summary>
    private ItemSlot HotbarButton(int slot) {
        var button = new ItemSlot { FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += () => SelectHotbar(slot);
        button.CanTake = drag => world.Data.Items.ContainsKey(drag.Item) || world.Data.Buildings.ContainsKey(drag.Item);
        button.Take = drag => { SetHotbar(slot, drag.Item); return 0; };
        button.CustomMinimumSize = new Vector2(62, 62);
        Style.Skin(button, Style.SocketNormal, Style.SocketHover, Style.SocketPressed);
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        button.Text = "";
        button.ExpandIcon = true;
        button.AddThemeConstantOverride("icon_max_width", 44);
        button.IconAlignment = HorizontalAlignment.Center;
        button.VerticalIconAlignment = VerticalAlignment.Center;
        button.TooltipText = $"Hotbar slot {slot + 1}";
        var number = new Label {
            Text = (slot + 1).ToString(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 4,
            OffsetTop = 2,
            OffsetRight = -42,
            OffsetBottom = -42
        };
        number.AddThemeFontOverride("font", Style.Bold); number.AddThemeFontSizeOverride("font_size", 12);
        number.AddThemeColorOverride("font_color", Style.Muted);
        button.AddChild(number);
        var count = new Label {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 28,
            OffsetTop = 41,
            OffsetRight = -4,
            OffsetBottom = -2
        };
        count.AddThemeFontOverride("font", Style.Bold); count.AddThemeFontSizeOverride("font_size", 14);
        count.AddThemeColorOverride("font_color", Style.Cream);
        count.AddThemeColorOverride("font_outline_color", Style.Socket); count.AddThemeConstantOverride("outline_size", 4);
        button.AddChild(count);
        hotbarCounts.Add(count);
        hotbarButtons.Add(button);
        return button;
    }

    // What each slot shows, so a slot is redrawn only when its item, count or selection changes: redrawing builds text and styles.
    private readonly System.Collections.Generic.Dictionary<int, (ItemSlot Button, string Id, int Count, bool Active)> shownSlots = new();
    private void RefreshHotbar() {
        for (int slot = 0; slot < hotbarButtons.Count; slot++) {
            var button = hotbarButtons[slot];
            if (!GodotObject.IsInstanceValid(button)) continue;
            var shown = (button, HotbarId(slot), world.S.Backpack[HotbarId(slot)], world.S.ActiveHotbar == slot);
            if (shownSlots.TryGetValue(slot, out var before) && before == shown) continue;
            shownSlots[slot] = shown;
            if (button.GetParent() is HBoxContainer row) row.Alignment = BoxContainer.AlignmentMode.Center;
            var id = HotbarId(slot);
            int count = world.S.Backpack[id];
            bool building = world.Data.Buildings.ContainsKey(id);
            hotbarCounts[slot].Text = string.IsNullOrEmpty(id) || building || count == 0 ? "" : count.ToString();
            button.Icon = string.IsNullOrEmpty(id) ? null : HotbarIcon(id);
            button.Disabled = !building && (string.IsNullOrEmpty(id) || count == 0);
            button.TooltipText = string.IsNullOrEmpty(id) ? $"Hotbar slot {slot + 1} · empty · assign from Inventory" : $"Hotbar slot {slot + 1} · {world.Data.Name(id)} · {(building ? "building plan" : $"{count} carried")}";
            bool active = world.S.ActiveHotbar == slot;
            // The item in hand gets an orange rim.
            button.AddThemeStyleboxOverride("normal", active ? Style.SocketWanted : Style.SocketNormal);
            button.AddThemeStyleboxOverride("disabled", active ? Style.SocketWanted : Style.SocketNormal);
        }
    }

    // World.Deserialize keeps nine known entries, so reads need no repair here.
    private string HotbarId(int slot) => world.S.HotbarSlots[slot];

    private string? ActiveHotbarItem() => world.Data.Items.ContainsKey(HotbarId(world.S.ActiveHotbar)) ? HotbarId(world.S.ActiveHotbar) : null;

    private bool IsTransferableHotbarItem(string id) => world.Data.Items[id].Tool == "" && !world.Data.IsConsumable(id);

    /// <summary>Puts a hotbar slot in hand; building plans and crates go straight to placement, everything else waits for a left click in the world.</summary>
    private void SelectHotbar(int slot) {
        if (world == null) return;
        slot = Math.Clamp(slot, 0, 8);
        world.S.ActiveHotbar = slot;
        string id = HotbarId(slot);
        view.BuildMode = "";
        assigningWorker = 0;
        if (string.IsNullOrEmpty(id)) Notify($"Hotbar slot {slot + 1} is empty. Assign it from Inventory.");
        else if (world.Data.Buildings.ContainsKey(id)) { view.BuildMode = id; ClosePanel(); Notify($"Place {world.Data.Name(id)}."); } else if (id == "crate") PlaceCrate(id);
        else if (UseOf(id) is string use) Notify($"{world.Data.Name(id)} in hand. Left click to {use}.");
        else Notify($"{world.Data.Name(id)} in hand.");
        RefreshLive();
    }

    /// <summary>What a left click does with an item in hand, or null when it has no use.</summary>
    private string? UseOf(string id) => world.Data.IsConsumable(id) ? id == "water" ? "drink" : "eat"
        : id == "torch" ? world.S.TorchLit ? "swing it" : "light it"
        : (world.Data.Items.TryGetValue(id, out var item) ? item.Tool : null) is "axe" or "pickaxe" or "hammer" ? "swing" : null;

    /// <summary>Uses the item in hand toward a point on the ground; items without a use do nothing.</summary>
    private void UseHeld(Vector2 at) {
        if (ActiveHotbarItem() is not string id || UseOf(id) == null) return;
        if (paused) { Notify("Resume before using items."); return; }
        if (world.Data.IsConsumable(id)) Act(() => world.Consume(id));
        else if (id == "torch") world.UseTorch(at - world.Player);
        else world.Attack();
    }

    private void HotbarAssignment() {
        AddLabel("Choose a slot, then assign an item or building plan, or drag one onto the hotbar. A slot stores a reference; it does not duplicate anything.", 14, Muted);
        var row = Row();
        for (int i = 0; i < 9; i++) {
            int slot = i;
            string assigned = HotbarId(i);
            var slotButton = Button($"{i + 1}", () => { hotbarAssignSlot = slot; OpenPage("hotbar"); });
            slotButton.TooltipText = string.IsNullOrEmpty(assigned) ? $"Slot {i + 1} · empty" : $"Slot {i + 1} · {world.Data.Name(assigned)}";
            slotButton.CustomMinimumSize = new Vector2(0, 36);
            slotButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            if (slot == hotbarAssignSlot) slotButton.Modulate = new Color("f2d98b");
            row.AddChild(slotButton);
            if (i == 2 || i == 5) row = Row();
        }
        string current = HotbarId(hotbarAssignSlot);
        AddLabel($"Assigning slot {hotbarAssignSlot + 1} · {(string.IsNullOrEmpty(current) ? "Empty" : world.Data.Name(current))}", 16);
        var grid = AddGrid();
        foreach (var id in world.Data.Items.Keys.Concat(world.Data.Buildings.Keys)) {
            var (cell, _) = IconCell($"assign_{id}"); cell.Icon = HotbarIcon(id); DragReference(cell, id);
            cell.TooltipText = world.Data.Buildings.ContainsKey(id) ? $"Plan: {world.Data.Name(id)}" : world.Data.Name(id);
            cell.Pressed += () => SetHotbar(hotbarAssignSlot, id);
            grid.AddChild(cell);
        }
        AddButton("Clear selected slot", () => SetHotbar(hotbarAssignSlot, ""));
        AddButton("Back to inventory", () => OpenPage("inventory"));
    }

    private void SetHotbar(int slot, string id) {
        world.S.HotbarSlots[slot] = id;
        world.S.ActiveHotbar = slot;
        if (id != "") sounds.Play("putting_items_on_hotbar");
        Notify(id == "" ? $"Cleared hotbar slot {slot + 1}." : $"Slot {slot + 1}: {world.Data.Name(id)}.");
        if (page == "hotbar") OpenPage("hotbar"); else RefreshLive();
    }
}
