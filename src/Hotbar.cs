using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

public partial class Game {
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
        button.CanTake = drag => world.Data.Items.ContainsKey(drag.Item) || world.Data.IsBuilding(drag.Item);
        button.Take = drag => { SetHotbar(slot, drag.Item); return 0; };
        button.CustomMinimumSize = new Vector2(56, 56);
        Style.Skin(button, Style.CupNormal, Style.CupHover, Style.CupPressed);
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        button.Text = "";
        button.ExpandIcon = true;
        button.AddThemeConstantOverride("icon_max_width", 40);
        button.IconAlignment = HorizontalAlignment.Center;
        button.VerticalIconAlignment = VerticalAlignment.Center;
        button.TooltipText = $"Hotbar slot {slot + 1}";
        var number = new Label {
            Text = (slot + 1).ToString(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 6,
            OffsetTop = 3,
            OffsetRight = -38,
            OffsetBottom = -38
        };
        number.AddThemeFontOverride("font", Style.Bold); number.AddThemeFontSizeOverride("font_size", 10);
        number.AddThemeColorOverride("font_color", Style.Tan);
        button.AddChild(number);
        // The count sits on a pill at the cup's corner, as on every other cup.
        var count = Style.Corner(Style.Pill("", Style.CountPill, Style.Rim, 12), 1, 1);
        count.Visible = false;
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
            bool building = world.Data.IsBuilding(id);
            hotbarCounts[slot].Text = string.IsNullOrEmpty(id) || building || count == 0 ? "" : Water.IsBottle(id) ? $"{Water.BottleSips(id)}/3" : count.ToString();
            button.Icon = string.IsNullOrEmpty(id) ? null : HotbarIcon(id);
            button.Disabled = !building && (string.IsNullOrEmpty(id) || count == 0);
            button.TooltipText = string.IsNullOrEmpty(id) ? $"Hotbar slot {slot + 1} · empty · drag an item or plan here" : $"Hotbar slot {slot + 1} · {world.Data.Name(id)} · {(building ? "building plan" : $"{count} carried")}";
            bool active = world.S.ActiveHotbar == slot;
            // The item in hand gets an orange rim.
            button.AddThemeStyleboxOverride("normal", active ? Style.CupWanted : Style.CupNormal);
            button.AddThemeStyleboxOverride("disabled", active ? Style.CupWanted : Style.CupNormal);
            hotbarCounts[slot].Visible = hotbarCounts[slot].Text != "";
        }
    }

    // World.Deserialize keeps nine known entries, so reads need no repair here.
    private string HotbarId(int slot) {
        string id = world.S.HotbarSlots[slot];
        if (!Water.IsBottle(id)) return id;
        foreach (var bottle in DrinkingBottles) if (world.S.Backpack[bottle] > 0) return bottle;
        return "empty_bottle";
    }
    private static readonly string[] DrinkingBottles = { "water_1", "water_2", "water", "empty_bottle" };

    private string? ActiveHotbarItem() => world.Data.Items.ContainsKey(HotbarId(world.S.ActiveHotbar)) ? HotbarId(world.S.ActiveHotbar) : null;

    private bool IsTransferableHotbarItem(string id) => world.Data.Items[id].Tool == "" && !world.Data.IsConsumable(id);

    /// <summary>Puts a hotbar slot in hand; building plans and crates go straight to placement, everything else waits for a left click in the world.</summary>
    private void SelectHotbar(int slot) {
        if (world == null) return;
        slot = Math.Clamp(slot, 0, 8);
        world.S.ActiveHotbar = slot;
        string id = HotbarId(slot);
        view.BuildMode = null;
        assigningWorker = 0;
        if (string.IsNullOrEmpty(id)) Notify($"Hotbar slot {slot + 1} is empty. Assign it from Inventory.");
        else if (Kinds.Parse(id) is ThingKind kind && world.Data.Buildings.ContainsKey(kind)) { view.BuildMode = kind; ClosePanel(); } else if (id == "crate") PlaceCrate(id);
        RefreshLive();
    }

    /// <summary>Whether a left click on the ground does something with this item in hand: drink, eat, light or swing.</summary>
    private bool HasUse(string id) => Water.IsBottle(id) || world.Data.IsConsumable(id) || id == "torch"
        || (world.Data.Items.TryGetValue(id, out var item) ? item.Tool : null) is "axe" or "pickaxe" or "hammer";

    /// <summary>Uses the item in hand toward a point on the ground; items without a use do nothing.</summary>
    private void UseHeld(Vector2 at) {
        if (ActiveHotbarItem() is not string id || !HasUse(id)) return;
        if (paused) { Notify("Resume before using items."); return; }
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) Act(() => world.Consume(id));
        else if (id == "torch") world.UseTorch(at - world.Player);
        else world.Attack();
    }

    private void SetHotbar(int slot, string id) {
        world.S.HotbarSlots[slot] = id;
        world.S.ActiveHotbar = slot;
        if (id != "") sounds.Play("putting_items_on_hotbar");
        RefreshLive();
    }
}
