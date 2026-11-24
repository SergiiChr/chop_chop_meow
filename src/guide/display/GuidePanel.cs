using Godot;

namespace ChopChopMeow;

/// <summary>
/// The field guide: round tabs for its topics beside short tips, each led by the item or key it is about.
/// Keys show as key caps; everything else as the item's icon in a slot.
/// </summary>
public partial class GuidePanel(PageTools tools) : PagePanel(tools) {
    private static readonly (string Key, string Glyph, string Title)[] GuideTopics = {
        ("controls", "line_mouse", "Controls"), ("steel", "line_flame", "Iron and steel"), ("craft", "line_craft", "Crafting"),
        ("crew", "line_crew", "Crew and storage"), ("survive", "line_moon", "Staying alive"), ("trade", "line_cart", "Trade")
    };

    /// <summary>Each topic's tips: a key cap ("key:…"), a line glyph ("line_…") or an item or building icon, then the tip.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, (string Lead, string Tip)[]> GuideTips = new() {
        ["controls"] = new[] {
            ("key:WASD", "Move."),
            ("key:Shift", "Sprint."),
            ("key:RMB", "Hold to collect resources or pick up items. Click open ground or a hotbar slot to use an item."),
            ("key:LMB", "Select item."),
            ("key:Space", "Use the selected building or item. Swing an axe or pump water."),
            ("key:1–9", "Select a hotbar slot. Pick up a building plan or backpack item and put it on the hotbar."),
            ("key:B", "Building plans."), ("key:Tab", "Backpack."), ("key:C", "Crafting."), ("key:H", "Worker management."),
            ("key:Double LMB", "Use an item in the backpack or on the hotbar."),
            ("key:Shift-click", "Move the entire stack or queue 5 crafting batches."),
            ("key:Ctrl-click", "Move all items of this type or queue 10 crafting batches."),
            ("key:F5 · F9", "Quick save and load."),
            ("key:Esc", "Cancel, close a panel, or pause."),
            ("line_layers", "Switch layers with the Layer button below the bank. Click an underground pipe to see its water and pressure.")
        },
        ["steel"] = new[] {
            ("steel_pick", "Buy your first pickaxe at a trading post with crates of berries."),
            ("iron_ore", "Mine iron and coal at the foot of the cliffs with a pickaxe; loose coal needs no tool."),
            ("iron_bar", "Stone furnace: 2 ore make an iron bar over coal or charcoal; wood only cooks food and burns 2 wood into charcoal."),
            ("steel_bar", "Reinforced furnace: 2 iron bars and 2 coal make 2 steel bars, with coal in its fuel slot."),
            ("line_flame", "Pump the bellows: steel requires 1450+ °C.")
        },
        ["craft"] = new[] {
            ("stone_chisel", "Press C to craft on the go. Knap a stone chisel from 2 stone first."),
            ("planks", "With a chisel, carve planks, wood parts, a mallet and torches."),
            ("bench", "A crafting bench works twice as fast and makes tools and crates. Slot a chisel and a hammer."),
            ("steel_axe", "A small steel tool takes 1 steel part, a big one 2, plus a wood part for the handle."),
            ("steel_shovel", $"A shovel digs up a stump for {World.StumpWood} wood and a plain bush for {World.BushLeaves} leaves. Both are gone for good."),
            ("line_map", "The map button on the crafting menu shows everything there is to make and what it is made out of.")
        },
        ["crew"] = new[] {
            ("line_crew", "Hire at a trading post: more coins up front means a lower daily wage."),
            ("bench", "Every building has job buttons at the bottom. Tick workers for each job; one with several does the first in their list that can be done, and drags on their page change the order."),
            ("chest", "Workers fetch from and store in storage. Filters decide what goes where; empty means anything."),
            ("toolstation", "Workers take the tools their jobs need from a tool station and wait if there is none."),
            ("campfire", "Lunch at noon by the campfire. Home at dusk, back at dawn, loads kept."),
            ("coins", "Unpaid workers strike until paid or dismissed.")
        },
        ["survive"] = new[] {
            ("berries", "A full stomach lasts a day. Gather berries early to keep your strength up."),
            (Water.Full, "Your bottle holds three liters. Refill it at the lake; the sea is salt. Without a bottle, drink at the source."),
            ("hut", "Build a cabin and campfire before dusk. A cabin shelters you; a lit fire keeps wolves away. Sleep inside after 10 pm if your food and water will last until dawn."),
            ("campfire", "Wolves leave their dens at dusk and keep out of fire light. A log burns about 4 hours."),
            ("torch", "Right-click open ground to light a torch. Right-click again to swing it. Four quick swings put it out."),
            ("raw_meat", "Butcher a slain wolf for raw meat and cook it before eating.")
        },
        ["trade"] = new[] {
            ("crate", "A crate holds 12. Hold right mouse to pick it up with its contents. Never put a crate in a crate."),
            ("signpost", "Load packed crates at a trading post and send the cart: it is back in 30 seconds."),
            ("packer", "A packing station packs one good into crates while someone works it. Ask for crates of it in the post's filter and tick Send when full to sell without you."),
            ("coins", "Sales fill the bank first; wages come out of it at dawn."),
            ("line_arrow", "Prices fall as you sell and recover over days. The sheet shows the last prices heard.")
        }
    };

    protected override void Build() {
        var board = new HBoxContainer(); board.AddThemeConstantOverride("separation", 14); AddChild(board);
        var tabs = new VBoxContainer(); tabs.AddThemeConstantOverride("separation", 10); board.AddChild(tabs);
        foreach (var (key, glyph, title) in GuideTopics) {
            var tab = new Button { Name = $"guide_{key}", TooltipText = title, CustomMinimumSize = new Vector2(48, 48), FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            tab.Icon = GD.Load<Texture2D>($"res://assets/ui/{glyph}.svg"); tab.ExpandIcon = true; tab.IconAlignment = HorizontalAlignment.Center; tab.AddThemeConstantOverride("icon_max_width", 24);
            tab.AddThemeColorOverride("icon_normal_color", Style.Rim); tab.AddThemeColorOverride("icon_hover_color", Style.Rim);
            var look = key == memory.GuideTab ? Style.TabActive : Style.TabRound; Style.Skin(tab, look.Normal, look.Hover);
            string topic = key;
            tab.Pressed += () => { memory.GuideTab = topic; Redraw(); };
            tabs.AddChild(tab);
        }
        var page = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; page.AddThemeConstantOverride("separation", 10); board.AddChild(page);
        foreach (var (key, _, title) in GuideTopics) if (key == memory.GuideTab) page.AddChild(Style.Heading(title, 20));
        var well = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Pass }; well.AddThemeStyleboxOverride("panel", Style.Well); page.AddChild(well);
        var rows = new VBoxContainer(); rows.AddThemeConstantOverride("separation", 8); well.AddChild(rows);
        var tips = GuideTips[memory.GuideTab];
        for (int i = 0; i < tips.Length; i++) {
            if (i > 0) rows.AddChild(new HSeparator());
            var (lead, tip) = tips[i];
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); rows.AddChild(row);
            if (lead.StartsWith("key:")) {
                var cap = Style.Pill(lead[4..], Style.KeyPill, Style.Line, 13); cap.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; cap.CustomMinimumSize = new Vector2(0, 26);
                var slot = new VBoxContainer { CustomMinimumSize = new Vector2(88, 0), Alignment = BoxContainer.AlignmentMode.Center }; slot.AddChild(cap); cap.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
                row.AddChild(slot);
            } else {
                var itemSlot = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; itemSlot.AddThemeStyleboxOverride("panel", Style.SlotNormal);
                itemSlot.AddChild(lead.StartsWith("line_") ? Style.Glyph(lead, 28, Style.Ink) : Style.Icon(Icons.Of(lead), 32));
                row.AddChild(itemSlot);
            }
            var text = Style.Text(tip, 14, Style.Ink, wrap: true); text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(text);
        }
    }
}
