using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The tool belt along the bottom: round badges for the menus, then nine slots of saved item and plan references.
/// A click selects a slot, which puts its item in the player's hand; plans and crates go straight to placement.
/// </summary>
public partial class Hotbar : PanelContainer {
    private readonly UiKit kit;
    private readonly World world;
    private readonly SidePanel panel;
    private readonly HeldCursor cursor;

    public System.Collections.Generic.List<ItemSlot> Buttons { get; } = new();
    public System.Collections.Generic.List<Label> Counts { get; } = new();

    public Hotbar(UiKit kit, SidePanel panel, HeldCursor cursor) {
        this.kit = kit; world = kit.World; this.panel = panel; this.cursor = cursor;
        AnchorLeft = .5f; AnchorRight = .5f; AnchorTop = 1; AnchorBottom = 1; OffsetTop = -14; OffsetBottom = -14; GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.Begin;
        AddThemeStyleboxOverride("panel", Style.Plate);
        var slots = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; slots.AddThemeConstantOverride("separation", 10); AddChild(slots);
        slots.AddChild(MenuIcon("build", "Build", () => panel.Open(Page.Build), Style.Brick, "B"));
        slots.AddChild(MenuIcon("bag", "Backpack", () => panel.Open(Page.Backpack), Style.Sage, "Tab"));
        slots.AddChild(MenuIcon("craft", "Crafting", () => panel.Open(Page.Craft), new Color("b08a45"), "C"));
        slots.AddChild(MenuIcon("map", "Recipe map", () => panel.Map.Open(), new Color("7d6488")));
        slots.AddChild(MenuIcon("crew", "Crew", () => panel.Open(Page.Crew), new Color("5f8aa6"), "H"));
        var divider = new ColorRect { Color = new Color(Style.Beige, .6f), CustomMinimumSize = new Vector2(3, 46), SizeFlagsVertical = SizeFlags.ShrinkCenter }; slots.AddChild(divider);
        for (int i = 0; i < 9; i++) slots.AddChild(HotbarButton(i));
    }

    /// <summary>A round enamel badge with a line icon and a key cap when it has a shortcut.</summary>
    private Button MenuIcon(string glyph, string tooltip, Action action, Color color, string? key = null) {
        var button = kit.Button("", action);
        button.Name = glyph; button.TooltipText = key == null ? tooltip : $"{tooltip} · {key}";
        button.Icon = GD.Load<Texture2D>($"res://assets/ui/line_{glyph}.svg");
        button.ExpandIcon = true; button.IconAlignment = HorizontalAlignment.Center; button.VerticalIconAlignment = VerticalAlignment.Center;
        button.CustomMinimumSize = new Vector2(56, 56); button.AddThemeConstantOverride("icon_max_width", 28);
        button.AddThemeColorOverride("icon_normal_color", Style.Rim); button.AddThemeColorOverride("icon_hover_color", Style.Rim); button.AddThemeColorOverride("icon_pressed_color", Style.Rim);
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var look = Style.Round(color, 28); Style.Skin(button, look.Item1, look.Item2);
        if (key != null) button.AddChild(Style.Corner(Style.Pill(key, Style.KeyPill, Style.Line, 10), 1, 1, 8));
        return button;
    }

    /// <summary>
    /// A hotbar slot: a left click selects it, a right click or double left click uses it, and Ctrl or Shift stores its item in the building open.
    /// An item or building plan put on it from the cursor assigns that reference, and the stack goes back where it was.
    /// </summary>
    private ItemSlot HotbarButton(int slot) {
        var button = new ItemSlot { FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.Controls = new SlotInput(cursor) {
            Takes = held => held.Item is string id && (world.Data.Items.ContainsKey(id) || world.Data.IsBuilding(id)),
            // A held stack binds itself to the slot (World.BindHeld); a plan is only named there.
            Put = (held, _) => {
                if (held.Stack is HeldStack stack) world.FromHeldStack(() => world.SetHotbar(slot, stack.Item));
                else world.SetHotbar(slot, held.Plan!);
                kit.Sounds.Play("putting_items_on_hotbar");
                kit.Refresh();
                held.Release();
            },
            Click = () => Select(slot),
            Use = () => Use(slot),
            Batch = () => Store(slot),
            RightClick = () => Use(slot),
        };
        button.CustomMinimumSize = new Vector2(56, 56);
        Style.Skin(button, Style.SlotNormal, Style.SlotHover, Style.SlotPressed);
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
        // The count sits on a pill at the slot's corner, as on every other slot.
        var count = Style.Corner(Style.Pill("", Style.CountPill, Style.Rim, 12), 1, 1);
        count.Visible = false;
        button.AddChild(count);
        Counts.Add(count);
        Buttons.Add(button);
        return button;
    }

    // What each slot shows, so a slot is redrawn only when its item, count or selection changes: redrawing builds text and styles.
    private readonly (string Id, int Count, bool Active)?[] shownSlots = new (string, int, bool)?[9];
    public void Refresh() {
        for (int slot = 0; slot < Buttons.Count; slot++) {
            var button = Buttons[slot];
            string id = world.HotbarItem(slot);
            int count = world.HotbarCount(slot);
            bool active = world.InHand == "" && world.ActiveHotbar == slot;
            var shown = (id, count, active);
            if (shownSlots[slot] == shown) continue;
            shownSlots[slot] = shown;
            bool building = world.Data.IsBuilding(id);
            Counts[slot].Text = string.IsNullOrEmpty(id) || building || count == 0 ? ""
                : count == 1 && Barrel.Is(id) ? $"{Barrel.Liters(id)}/{Barrel.Capacity} L"
                : count == 1 && world.Data.VesselOf(id) is VesselDef vessel ? $"{vessel.LitersIn(id)}/{vessel.Capacity}" : count.ToString();
            button.Icon = string.IsNullOrEmpty(id) ? null : Icons.Of(id);
            button.Disabled = !building && (string.IsNullOrEmpty(id) || count == 0);
            button.TooltipText = string.IsNullOrEmpty(id) ? $"Hotbar slot {slot + 1} · empty · put an item or plan here" : $"Hotbar slot {slot + 1} · {world.Data.Name(id)} · {(building ? "building plan" : $"{count} carried")}\nLMB: select · RMB or Double LMB: use";
            // The selected slot gets an orange rim.
            button.AddThemeStyleboxOverride("normal", active ? Style.SlotWanted : Style.SlotNormal);
            button.AddThemeStyleboxOverride("disabled", active ? Style.SlotWanted : Style.SlotNormal);
            Counts[slot].Visible = Counts[slot].Text != "";
        }
    }

    /// <summary>The item selected: the tool in hand, or else the item on the selected hotbar slot; null for none.</summary>
    public string? ActiveItem() {
        string id = world.InHand != "" ? world.InHand : world.HotbarItem(world.ActiveHotbar);
        return world.Data.Items.ContainsKey(id) ? id : null;
    }

    /// <summary>The backpack cell the item selected is used from: the stack bound to the selected hotbar slot, or -1 for the tool in hand or a slot bound to none.</summary>
    public int ActiveStack() => world.InHand != "" ? -1 : world.HotbarStack(world.ActiveHotbar);

    /// <summary>Runs an action with the stack bound to a hotbar slot first in line to leave the backpack, so it is that stack the action uses.</summary>
    public void FromStack(int stack, Action action) {
        var bag = world.S.Backpack; int first = bag.PreferredSlot;
        bag.PreferredSlot = stack;
        try { action(); } finally { bag.PreferredSlot = first; }
    }

    /// <summary>
    /// A Ctrl- or Shift-click on a slot: with a building's page open, its item moves there as one in the backpack drawer does.
    /// Shift moves the slot's own stack and Ctrl all of the item, its stack too; otherwise it selects the slot.
    /// </summary>
    private void Store(int slot) {
        string id = world.HotbarItem(slot);
        if (panel.Visible && panel.Bag.Store is Action<string> store && world.HotbarCount(slot) > 0) { kit.Run(() => FromStack(world.HotbarStack(slot), () => store(id))); return; }
        Select(slot);
    }

    /// <summary>Uses the slot's stack or selects the slot when its item has no direct use.</summary>
    private void Use(int slot) {
        string id = world.HotbarItem(slot);
        if (world.HotbarCount(slot) == 0 || !UseItem(id, world.HotbarStack(slot))) Select(slot);
    }

    /// <summary>
    /// Uses a carried item as a double click does, here or in the backpack: eats or drinks it, lights a torch or opens a container.
    /// The item comes from backpack cell <paramref name="stack"/>, or with -1 from the cell already first in line.
    /// </summary>
    public bool UseItem(string id, int stack = -1) {
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) kit.Run(() => { if (stack >= 0) FromStack(stack, () => world.Consume(id)); else world.Consume(id); });
        else if (id == "torch" && !world.TorchLit) kit.Run(() => world.UseTorch(Vector2.Zero));
        else if (World.IsCrate(id)) panel.OpenCrate(World.PackedId(id));
        else if (Barrel.Is(id)) panel.OpenBarrel(stack >= 0 ? stack : world.S.Backpack.PreferredSlot);
        else return false;
        return true;
    }

    /// <summary>Selects a carried tool: the hotbar slot that holds it, or else the tool in hand with no slot selected.</summary>
    public void SelectTool(string tool) {
        for (int slot = 0; slot < 9; slot++) if (world.HotbarItem(slot) == tool) { Select(slot); return; }
        panel.Player.Mode = new ClickMode.Normal();
        world.SelectInHand(tool);
        kit.Refresh();
    }

    /// <summary>Selects a hotbar slot, putting building plans and containers into placement or another item into the player's hand.</summary>
    public void Select(int slot) {
        slot = Math.Clamp(slot, 0, 8);
        world.SelectHotbar(slot);
        string id = world.HotbarItem(slot);
        panel.Player.Mode = new ClickMode.Normal();
        if (string.IsNullOrEmpty(id)) kit.Notify($"Hotbar slot {slot + 1} is empty. Assign it from Inventory.");
        else if (world.Data.TryBuilding(id, out var kind)) { panel.Player.Mode = new ClickMode.Build(kind); panel.Close(); } else if (id == "crate") panel.Player.PlaceContainer(id);
        else if (Barrel.Is(id) && world.HotbarStack(slot) is int barrelSlot and >= 0) panel.Player.PlaceContainer(id, barrelSlot);
        kit.Refresh();
    }

    /// <summary>Whether this item can be eaten, drunk, lit or swung by right-clicking open ground.</summary>
    private bool HasUse(string id) => Water.IsBottle(id) || world.Data.IsConsumable(id) || id == "torch"
        || (world.Data.Items.TryGetValue(id, out var item) ? item.Tool : null) is "axe" or "pickaxe" or "hammer";

    /// <summary>Uses the item selected on the hotbar toward a point on the ground; items without a use do nothing.</summary>
    public void UseSelected(Vector2 at) {
        if (ActiveItem() is not string id || !HasUse(id)) return;
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) FromStack(ActiveStack(), () => world.Consume(id));
        else if (id == "torch") world.UseTorch(at - world.Player);
        else world.Attack(at - world.Player);
    }
}
