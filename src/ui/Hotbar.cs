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
    /// <summary>The diary's badge, shown once the player has found part of the diary, with a dot while a day waits to be read.</summary>
    public Button DiaryBadge { get; }
    private readonly Panel unread;
    // Set by the first find taken here, so the badge that comes with it waits behind the day it opened and is stamped into the row as that closes.
    private bool stampDiary;
    /// <summary>Seconds the diary's badge takes to be stamped into the row.</summary>
    private const float StampSeconds = .45f;
    public System.Collections.Generic.List<Badge> Counts { get; } = new();

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
        // The diary's badge waits for the player's first find.
        DiaryBadge = MenuIcon("diary", "Diary", () => panel.Open(Page.Diary), new Color("8a6a4a"));
        DiaryBadge.Visible = false;
        unread = Style.Corner(new Panel { Name = "unread", MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(16, 16) }, 1, 0, 2);
        unread.AddThemeStyleboxOverride("panel", Style.Flat(Style.Brick, 8, 0, Style.Rim, 2));
        DiaryBadge.AddChild(unread);
        slots.AddChild(DiaryBadge);
        var divider = new ColorRect { Color = new Color(Style.Beige, .6f), CustomMinimumSize = new Vector2(3, 46), SizeFlagsVertical = SizeFlags.ShrinkCenter }; slots.AddChild(divider);
        for (int i = 0; i < 9; i++) slots.AddChild(HotbarButton(i));
    }

    /// <summary>
    /// Stamps the diary's badge into the row once the find just taken shows in the world and the day it opened is closed again, if it is the player's first,
    /// so the stamp shows them where the diary is kept.
    /// </summary>
    public void StampDiaryOnFirstFind() => stampDiary = !DiaryBadge.Visible;

    /// <summary>Brings the diary's badge down into the row like a stamp: large, tilted and faint, then hard and flat in its place with a small bounce.</summary>
    private void Stamp() {
        DiaryBadge.PivotOffset = DiaryBadge.CustomMinimumSize / 2;
        DiaryBadge.Modulate = new Color(1, 1, 1, 0);
        // The row lays its badges out again as this one shows, which sets its size and tilt back, so each step starts from its own value.
        var tween = DiaryBadge.CreateTween().SetParallel();
        tween.TweenProperty(DiaryBadge, "scale", Vector2.One, StampSeconds).From(Vector2.One * 2.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(DiaryBadge, "rotation_degrees", 0f, StampSeconds).From(-14f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(DiaryBadge, "modulate:a", 1f, StampSeconds * .4f).From(0f);
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
            Takes = held => held.Item is string id && (world.Data.HasItem(id) || world.Data.IsBuilding(id)),
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
            RightClick = () => { if (!panel.OpenLantern(world.HotbarItem(slot))) Use(slot); },
        };
        kit.LinkRecipeMap(button, () => world.HotbarItem(slot));
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
        var count = ItemSlot.CountBadge();
        button.AddChild(count);
        Counts.Add(count);
        Buttons.Add(button);
        return button;
    }

    // What each slot shows, so a slot is redrawn only when its item, count or selection changes: redrawing builds text and styles.
    private readonly (string Id, int Count, bool Active, int Life, int Bugs, int PackedCount)?[] shownSlots = new (string, int, bool, int, int, int)?[9];
    public void Refresh() {
        var me = world.Me;
        bool badgeShown = Diary.Started(me) && !(stampDiary && panel.ReadingSheet.Visible);
        if (badgeShown != DiaryBadge.Visible) {
            DiaryBadge.Visible = badgeShown;
            if (badgeShown && stampDiary) Stamp();
        }
        if (badgeShown) stampDiary = false;
        bool waiting = Diary.AnyUnread(me);
        if (unread.Visible != waiting) unread.Visible = waiting;
        for (int slot = 0; slot < Buttons.Count; slot++) {
            var button = Buttons[slot];
            string id = world.HotbarItem(slot);
            int count = world.HotbarCount(slot);
            bool active = world.InHand == "" && world.ActiveHotbar == slot;
            int life = id == Firebugs.Item && count > 0 ? world.Firebugs.AverageMinutes(world.S.Backpack, world.HotbarStack(slot)) : -1;
            bool lantern = Lanterns.IsItem(id);
            int bugs = count > 0 && lantern ? Lanterns.BugCount(world.S.Backpack, id) : -1;
            var shown = (id, count, active, life, bugs, World.IsPacked(id) ? world.S.Backpack.Object(id)?.Contents?.Count ?? 0 : 0);
            if (shownSlots[slot] == shown) continue;
            shownSlots[slot] = shown;
            bool building = world.Data.IsBuilding(id);
            var badge = Counts[slot];
            if (building || count == 0) badge.SetCount("");
            else if (count == 1 && Barrel.Is(id)) badge.SetCount($"{Barrel.Liters(id)}/{Barrel.Capacity} L");
            else if (count == 1 && world.Data.VesselOf(id) is VesselDef vessel)
                badge.SetCount($"{vessel.LitersIn(id)}/{vessel.Capacity}");
            else badge.SetStack(count, bugs);
            button.Icon = string.IsNullOrEmpty(id) ? null : Icons.Of(id);
            button.Disabled = !building && (string.IsNullOrEmpty(id) || count == 0);
            string clicks = lantern ? "LMB: select · RMB: open for refill · Double LMB: take in hand or stow" : "LMB: select · RMB or Double LMB: use";
            button.TooltipText = string.IsNullOrEmpty(id) ? $"Hotbar slot {slot + 1} · empty · put an item or plan here" : $"Hotbar slot {slot + 1} · {world.Describe(id, world.S.Backpack)} · {(building ? "building plan" : $"{count} carried")}{world.Data.DescriptionLine(id)}{PageText.FirebugLifeLine(life)}\n{clicks}";
            if (!building && id.Length > 0) button.TooltipText += $"\n{RecipeMap.AltClickHint}";
            // The selected slot gets an orange rim.
            button.AddThemeStyleboxOverride("normal", active ? Style.SlotWanted : Style.SlotNormal);
            button.AddThemeStyleboxOverride("disabled", active ? Style.SlotWanted : Style.SlotNormal);
        }
    }

    /// <summary>The item selected: the tool in hand, or else the item on the selected hotbar slot; null for none.</summary>
    public string? ActiveItem() {
        string id = world.InHand != "" ? world.InHand : world.HotbarItem(world.ActiveHotbar);
        return world.Data.HasItem(id) ? id : null;
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
        else if (id == World.TorchItem && !world.TorchLit) kit.Run(() => world.UseTorch(Vector2.Zero));
        else if (Lanterns.IsItem(id)) {
            panel.Player.Mode = new ClickMode.Normal();
            world.Lanterns.ToggleInHand(Lanterns.Id(id)); panel.Close(); kit.Refresh();
        } else if (World.IsCrate(id)) panel.OpenCrate(World.PackedId(id));
        else if (Barrel.Is(id)) panel.OpenBarrel(stack >= 0 ? stack : world.S.Backpack.PreferredSlot);
        else return false;
        return true;
    }

    /// <summary>Selects a carried tool: the hotbar slot that holds it, or else the tool in hand with no slot selected.</summary>
    public void SelectTool(string tool) {
        for (int slot = 0; slot < 9; slot++) if (world.HotbarItem(slot) == tool) { Select(slot); return; }
        panel.Player.Mode = new ClickMode.Normal();
        world.SelectInHand(tool);
        if (world.InHand == tool && world.ToolKind(tool) == GroundHoles.Tool) SelectDigging();
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
        else if (world.ToolKind(id) == GroundHoles.Tool && world.HotbarCount(slot) > 0) SelectDigging();
        kit.Refresh();
    }

    private void SelectDigging() {
        panel.Player.SelectDigging();
        panel.Close();
    }

    /// <summary>Whether this item can be eaten, drunk, lit or swung by right-clicking open ground.</summary>
    private bool HasUse(string id) => Water.IsBottle(id) || world.Data.IsConsumable(id) || world.IsWeapon(id);

    /// <summary>The selected carried weapon, if there is one.</summary>
    public string? ActiveWeapon() =>
        ActiveItem() is string item && world.Backpack[item] > 0 && world.IsWeapon(item) ? item : null;

    /// <summary>Uses the item selected on the hotbar toward a point on the ground; items without a use do nothing.</summary>
    public void UseSelected(Vector2 at) {
        if (ActiveItem() is not string id || !HasUse(id)) return;
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) FromStack(ActiveStack(), () => world.Consume(id));
        else if (id == World.TorchItem) world.UseTorch(at - world.Player);
        else if (world.ToolKind(id) == "net") world.SwingNet(at - world.Player);
        else world.Attack(at - world.Player, id);
    }
}
