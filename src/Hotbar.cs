using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The tool belt along the bottom: round badges for the menus, then nine slots of saved item and plan references.
/// A slot puts its item in hand; plans and crates go straight to placement.
/// </summary>
public partial class Hotbar : PanelContainer {
    private readonly UiKit kit;
    private readonly World world;
    private readonly UiState state;
    private readonly WorldView view;
    private readonly SidePanel panel;

    public System.Collections.Generic.List<ItemSlot> Buttons { get; } = new();
    public System.Collections.Generic.List<Label> Counts { get; } = new();

    public Hotbar(UiKit kit, WorldView view, SidePanel panel) {
        this.kit = kit; world = kit.World; state = kit.State; this.view = view; this.panel = panel;
        AnchorLeft = .5f; AnchorRight = .5f; AnchorTop = 1; AnchorBottom = 1; OffsetTop = -14; OffsetBottom = -14; GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Begin;
        AddThemeStyleboxOverride("panel", Style.Plate);
        var slots = new HBoxContainer(); slots.AddThemeConstantOverride("separation", 10); AddChild(slots);
        slots.AddChild(MenuIcon("build", "Build", () => panel.Open("build"), "B", Style.Brick)); slots.AddChild(MenuIcon("bag", "Backpack", () => panel.Open("inventory"), "Tab", Style.Sage));
        slots.AddChild(MenuIcon("craft", "Crafting", () => panel.Open("craft"), "C", new Color("b08a45"))); slots.AddChild(MenuIcon("crew", "Crew", () => panel.Open("workers"), "H", new Color("5f8aa6"))); slots.AddChild(MenuIcon("guide", "Field guide", () => panel.Open("help"), "I", new Color("7d6488")));
        var divider = new ColorRect { Color = new Color(Style.Beige, .6f), CustomMinimumSize = new Vector2(3, 46), SizeFlagsVertical = SizeFlags.ShrinkCenter }; slots.AddChild(divider);
        for (int i = 0; i < 9; i++) slots.AddChild(HotbarButton(i));
    }

    /// <summary>A round enamel badge with a line icon, and a key pill when a key opens the same page.</summary>
    private Button MenuIcon(string glyph, string tooltip, Action action, string key, Color color) {
        var button = kit.Button("", action);
        button.Name = glyph; button.TooltipText = key == "" ? tooltip : $"{tooltip} · {key}";
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/line_{glyph}.svg");
        button.ExpandIcon = true; button.IconAlignment = HorizontalAlignment.Center; button.VerticalIconAlignment = VerticalAlignment.Center;
        button.CustomMinimumSize = new Vector2(56, 56); button.AddThemeConstantOverride("icon_max_width", 28);
        button.AddThemeColorOverride("icon_normal_color", Style.Rim); button.AddThemeColorOverride("icon_hover_color", Style.Rim); button.AddThemeColorOverride("icon_pressed_color", Style.Rim);
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var look = Style.Round(color, 28); Style.Skin(button, look.Item1, look.Item2);
        if (key != "") button.AddChild(Style.Corner(Style.Pill(key, Style.KeyPill, Style.Line, 10), 1, 1, 8));
        return button;
    }

    /// <summary>A hotbar slot; dropping an item or building plan on it assigns that reference.</summary>
    private ItemSlot HotbarButton(int slot) {
        var button = new ItemSlot { FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.Pressed += () => Select(slot);
        button.CanTake = drag => world.Data.Items.ContainsKey(drag.Item) || world.Data.IsBuilding(drag.Item);
        button.Take = drag => { SetHotbar(slot, drag.Item); return 0; };
        button.CustomMinimumSize = new Vector2(56, 56);
        Style.Skin(button, Style.CupNormal, Style.CupHover, Style.CupPressed);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        button.Text = "";
        button.ExpandIcon = true;
        button.AddThemeConstantOverride("icon_max_width", 40);
        button.IconAlignment = HorizontalAlignment.Center;
        button.VerticalIconAlignment = VerticalAlignment.Center;
        button.TooltipText = $"Hotbar slot {slot + 1}";
        var number = new Label {
            Text = (slot + 1).ToString(),
            MouseFilter = MouseFilterEnum.Ignore,
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
        Counts.Add(count);
        Buttons.Add(button);
        return button;
    }

    // What each slot shows, so a slot is redrawn only when its item, count or selection changes: redrawing builds text and styles.
    private readonly System.Collections.Generic.Dictionary<int, (ItemSlot Button, string Id, int Count, bool Active)> shownSlots = new();
    public void Refresh() {
        for (int slot = 0; slot < Buttons.Count; slot++) {
            var button = Buttons[slot];
            if (!GodotObject.IsInstanceValid(button)) continue;
            var shown = (button, HotbarId(slot), world.S.Backpack[HotbarId(slot)], world.S.ActiveHotbar == slot);
            if (shownSlots.TryGetValue(slot, out var before) && before == shown) continue;
            shownSlots[slot] = shown;
            if (button.GetParent() is HBoxContainer row) row.Alignment = BoxContainer.AlignmentMode.Center;
            var id = HotbarId(slot);
            int count = world.S.Backpack[id];
            bool building = world.Data.IsBuilding(id);
            Counts[slot].Text = string.IsNullOrEmpty(id) || building || count == 0 ? "" : Water.IsBottle(id) ? $"{Water.BottleSips(id)}/3" : count.ToString();
            button.Icon = string.IsNullOrEmpty(id) ? null : Icons.Of(id);
            button.Disabled = !building && (string.IsNullOrEmpty(id) || count == 0);
            button.TooltipText = string.IsNullOrEmpty(id) ? $"Hotbar slot {slot + 1} · empty · drag an item or plan here" : $"Hotbar slot {slot + 1} · {world.Data.Name(id)} · {(building ? "building plan" : $"{count} carried")}";
            bool active = world.S.ActiveHotbar == slot;
            // The item in hand gets an orange rim.
            button.AddThemeStyleboxOverride("normal", active ? Style.CupWanted : Style.CupNormal);
            button.AddThemeStyleboxOverride("disabled", active ? Style.CupWanted : Style.CupNormal);
            Counts[slot].Visible = Counts[slot].Text != "";
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

    public string? ActiveItem() => world.Data.Items.ContainsKey(HotbarId(world.S.ActiveHotbar)) ? HotbarId(world.S.ActiveHotbar) : null;

    public bool IsTransferable(string id) => world.Data.Items[id].Tool == "" && !world.Data.IsConsumable(id);

    /// <summary>Puts a hotbar slot in hand; building plans and crates go straight to placement, everything else waits for a left click in the world.</summary>
    public void Select(int slot) {
        slot = Math.Clamp(slot, 0, 8);
        world.S.ActiveHotbar = slot;
        string id = HotbarId(slot);
        view.BuildMode = null;
        state.AssigningWorker = 0;
        if (string.IsNullOrEmpty(id)) kit.Notify($"Hotbar slot {slot + 1} is empty. Assign it from Inventory.");
        else if (world.Data.TryBuilding(id, out var kind)) { view.BuildMode = kind; panel.Close(); } else if (id == "crate") panel.PlaceCrate(id);
        kit.Refresh();
    }

    /// <summary>Whether a left click on the ground does something with this item in hand: drink, eat, light or swing.</summary>
    public bool HasUse(string id) => Water.IsBottle(id) || world.Data.IsConsumable(id) || id == "torch"
        || (world.Data.Items.TryGetValue(id, out var item) ? item.Tool : null) is "axe" or "pickaxe" or "hammer";

    /// <summary>Uses the item in hand toward a point on the ground; items without a use do nothing.</summary>
    public void UseHeld(Vector2 at) {
        if (ActiveItem() is not string id || !HasUse(id)) return;
        if (state.Paused) { kit.Notify("Resume before using items."); return; }
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) kit.Act(() => world.Consume(id));
        else if (id == "torch") world.UseTorch(at - world.Player);
        else world.Attack();
    }

    private void SetHotbar(int slot, string id) {
        world.S.HotbarSlots[slot] = id;
        world.S.ActiveHotbar = slot;
        if (id != "") kit.Sounds.Play("putting_items_on_hotbar");
        kit.Refresh();
    }
}
