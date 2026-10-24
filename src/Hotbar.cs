using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

public partial class Game
{
    private int hotbarAssignSlot;
    private readonly System.Collections.Generic.List<Button> hotbarButtons = new();
    private readonly System.Collections.Generic.List<Label> hotbarCounts = new();
    private readonly System.Collections.Generic.Dictionary<string, Texture2D> hotbarIcons = new();
    private Texture2D? itemAtlas;
    private Texture2D? worldAtlas;

    private void LoadHotbarAtlases()
    {
        if (ResourceLoader.Exists("res://assets/item-atlas.png")) itemAtlas = GD.Load<Texture2D>("res://assets/item-atlas.png");
        if (ResourceLoader.Exists("res://assets/world-atlas.png")) worldAtlas = GD.Load<Texture2D>("res://assets/world-atlas.png");
    }

    private Texture2D? HotbarIcon(string id)
    {
        if (hotbarIcons.TryGetValue(id, out var cached)) return cached;
        Texture2D? atlas = null; int cell = -1, columns = 4, rows = 3;
        // Meat icons are hand-written SVGs; the illustrated atlas has no free cells left for them.
        if (id is "raw_meat" or "cooked_meat") return hotbarIcons[id] = GD.Load<Texture2D>($"res://assets/ui/{id}.svg");
        if (id is "iron_axe" or "iron_pick" or "steel_tools" or "iron_bar" or "steel_bar" or "wood" or "stone" or "iron_ore" or "coal" or "berries" or "water")
        {
            atlas = itemAtlas;
            cell = id switch { "iron_axe" => 0, "iron_pick" => 1, "steel_tools" => 2, "iron_bar" => 3, "steel_bar" => 4, "wood" => 5, "stone" => 6, "iron_ore" => 7, "coal" => 8, "berries" => 9, "water" => 10, _ => 11 };
        }
        else
        {
            atlas = worldAtlas;
            cell = id switch { "furnace" => 5, "reinforced" => 6, "hut" => 7, "chest" => 8, "anvil" => 9, "signpost" => 10, "campfire" => 11, _ => -1 };
        }
        if (atlas == null || cell < 0) return null;
        var icon = new AtlasTexture
        {
            Atlas = atlas,
            Region = new Rect2(cell % columns * atlas.GetWidth() / (float)columns, cell / columns * atlas.GetHeight() / (float)rows, atlas.GetWidth() / (float)columns, atlas.GetHeight() / (float)rows)
        };
        hotbarIcons[id] = icon;
        return icon;
    }

    private Button ConfigureHotbarButton(Button button, int slot)
    {
        button.CustomMinimumSize = new Vector2(66, 62);
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        button.Text = "";
        button.ExpandIcon = true;
        button.AddThemeConstantOverride("icon_max_width", 44);
        button.IconAlignment = HorizontalAlignment.Center;
        button.VerticalIconAlignment = VerticalAlignment.Center;
        button.TooltipText = $"Hotbar slot {slot + 1}";
        var number = new Label
        {
            Text = (slot + 1).ToString(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 4,
            OffsetTop = 2,
            OffsetRight = -42,
            OffsetBottom = -42
        };
        number.AddThemeFontSizeOverride("font_size", 12);
        number.AddThemeColorOverride("font_color", new Color("f2e8cf"));
        button.AddChild(number);
        var count = new Label
        {
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
        count.AddThemeFontSizeOverride("font_size", 11);
        count.AddThemeColorOverride("font_color", new Color("f2e8cf"));
        button.AddChild(count);
        hotbarCounts.Add(count);
        hotbarButtons.Add(button);
        return button;
    }

    private void RefreshHotbar()
    {
        for (int slot = 0; slot < hotbarButtons.Count; slot++)
        {
            var button = hotbarButtons[slot];
            if (!GodotObject.IsInstanceValid(button)) continue;
            if (button.GetParent() is HBoxContainer row) row.Alignment = BoxContainer.AlignmentMode.Center;
            var id = HotbarId(slot);
            int count = world.S.Backpack[id];
            bool building = world.Data.Buildings.ContainsKey(id);
            hotbarCounts[slot].Text = string.IsNullOrEmpty(id) || building || count == 0 ? "" : count.ToString();
            button.Icon = string.IsNullOrEmpty(id) ? null : HotbarIcon(id);
            button.Disabled = !building && (string.IsNullOrEmpty(id) || count == 0);
            button.TooltipText = string.IsNullOrEmpty(id) ? $"Hotbar slot {slot + 1} · empty · assign from Inventory" : $"Hotbar slot {slot + 1} · {world.Data.Name(id)} · {(building ? "building plan" : $"{count} carried")}";
            button.Modulate = world.S.ActiveHotbar == slot ? new Color("f2d98b") : Colors.White;
        }
    }

    // World.Deserialize keeps nine known entries, so reads need no repair here.
    private string HotbarId(int slot) => world.S.HotbarSlots[slot];

    private string? ActiveHotbarItem() => world.Data.Items.ContainsKey(HotbarId(world.S.ActiveHotbar)) ? HotbarId(world.S.ActiveHotbar) : null;

    private bool IsTransferableHotbarItem(string id) => id is not "iron_axe" and not "iron_pick" and not "steel_tools" and not "berries" and not "water";

    private void UseHotbar(int slot)
    {
        if (world == null) return;
        slot = Math.Clamp(slot, 0, 8);
        world.S.ActiveHotbar = slot;
        string id = HotbarId(slot);
        view.BuildMode = "";
        assigningWorker = 0;
        if (string.IsNullOrEmpty(id)) { Notify($"Hotbar slot {slot + 1} is empty. Assign it from Inventory."); RefreshLive(); return; }
        if (world.Data.Buildings.ContainsKey(id))
        {
            view.BuildMode = id; ClosePanel(); Notify($"Place {world.Data.Name(id)}.");
        }
        else if (id is "berries" or "water" or "cooked_meat" or "raw_meat") world.Consume(id);
        else if (world.Data.Items.ContainsKey(id)) Notify($"Selected {world.Data.Name(id)}. Carried tools work with Space and gathering.");
        else Notify($"Unknown hotbar item: {id}.");
        RefreshLive();
    }

    private void HotbarAssignment()
    {
        AddLabel("Choose a slot, then assign an item or building plan. A slot stores a reference; it does not duplicate anything.", 14, Muted);
        var row = Row();
        for (int i = 0; i < 9; i++)
        {
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
        foreach (var id in world.Data.Items.Keys.Concat(world.Data.Buildings.Keys))
        {
            var (cell, _) = IconCell($"assign_{id}"); cell.Icon = HotbarIcon(id);
            cell.TooltipText = world.Data.Buildings.ContainsKey(id) ? $"Plan: {world.Data.Name(id)}" : world.Data.Name(id);
            cell.Pressed += () => SetHotbar(hotbarAssignSlot, id);
            grid.AddChild(cell);
        }
        AddButton("Clear selected slot", () => SetHotbar(hotbarAssignSlot, ""));
        AddButton("Back to inventory", () => OpenPage("inventory"));
    }

    private void SetHotbar(int slot, string id)
    {
        world.S.HotbarSlots[slot] = id;
        world.S.ActiveHotbar = slot;
        Notify(id == "" ? $"Cleared hotbar slot {slot + 1}." : $"Slot {slot + 1}: {world.Data.Name(id)}.");
        OpenPage("hotbar");
    }
}
