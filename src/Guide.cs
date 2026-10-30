using Godot;

namespace ChopChopMeow;

/// <summary>
/// The field guide: round tabs for its topics beside short tips, each led by the item or key it is about.
/// Keys show as key caps; everything else as the item's icon in a cup.
/// </summary>
public partial class Game {
    /// <summary>The guide topic shown, kept while the page is rebuilt.</summary>
    private string guideTab = "controls";

    private static readonly (string Key, string Glyph, string Title)[] GuideTopics = {
        ("controls", "line_mouse", "Controls"), ("steel", "line_flame", "Iron and steel"), ("craft", "line_craft", "Crafting"),
        ("crew", "line_crew", "Crew and storage"), ("survive", "line_moon", "Staying alive"), ("trade", "line_cart", "Trade")
    };

    /// <summary>Each topic's tips: a key cap ("key:…"), a line glyph ("line_…") or an item or building icon, then the tip.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, (string Lead, string Tip)[]> GuideTips = new() {
        ["controls"] = new[] {
            ("key:WASD", "Move. Hold Shift to sprint; food and drink drain faster."),
            ("key:Hold RMB", "Walk over and gather. On a placed crate, pick it up with what is inside."),
            ("key:LMB", "Select. On open ground, use what is in hand: swing, eat, drink."),
            ("key:E", "Inspect what is nearest."),
            ("key:Space", "Swing the tool in hand, or pump a selected reinforced furnace."),
            ("key:1–9", "Put a hotbar slot in hand. Drag an item or plan onto a slot to assign it."),
            ("key:B", "Building plans."), ("key:Tab", "Backpack."), ("key:C", "Crafting."), ("key:H", "Your crew."), ("key:I", "This guide."),
            ("key:Click / Ctrl-click", "Click moves one item; Ctrl-click moves all of an item. Right-click picks up half."),
            ("key:F5 · F9", "Quick save and load."),
            ("key:Esc", "Cancel, close a panel, or pause.")
        },
        ["steel"] = new[] {
            ("steel_pick", "Buy your first pickaxe at a trading post with crates of berries."),
            ("iron_ore", "Mine iron and coal at the foot of the cliffs with a pickaxe; loose coal needs no tool."),
            ("iron_bar", "Stone furnace: 2 ore and 1 wood or coal make an iron bar."),
            ("steel_bar", "Reinforced furnace: 2 iron bars and 2 mined coal make 2 steel bars, with coal in its fuel slot."),
            ("line_flame", "Pump the bellows: steel requires 1450+ °C."),
            ("steel_parts", "The anvil hammers steel bars into steel parts while someone works it.")
        },
        ["craft"] = new[] {
            ("stone_chisel", "Press C to craft on the go. Knap a stone chisel from 2 stone first."),
            ("planks", "With a chisel, carve planks, wood parts, a mallet and torches."),
            ("bench", "A crafting bench works twice as fast and makes tools and crates. Slot a chisel and a hammer."),
            ("steel_axe", "A small steel tool takes 1 steel part, a big one 2, plus a wood part for the handle.")
        },
        ["crew"] = new[] {
            ("line_crew", "Hire at a trading post: more coins up front means a lower daily wage."),
            ("bench", "Every building has job buttons at the bottom. Tick workers for each job; one with several jobs takes turns."),
            ("chest", "Workers fetch from and store in storage. Filters decide what goes where; empty means anything."),
            ("toolstation", "Workers take the tools their jobs need from a tool station and wait if there is none."),
            ("campfire", "Lunch at noon by the campfire. Home at dusk, back at dawn, loads kept."),
            ("coins", "Unpaid workers strike until paid or dismissed.")
        },
        ["survive"] = new[] {
            ("berries", "A full stomach lasts a day. Gather berries early to keep your strength up."),
            ("water", "Your bottle holds three sips. Refill it at the lake; the sea is salt. Without a bottle, drink at the source."),
            ("hut", "Build a cabin and campfire before night. A cabin shelters you; a lit fire keeps wolves away. Sleep inside after 10 pm if your food and water will last until dawn."),
            ("campfire", "Wolves hunt at night and keep out of fire light. A log burns about 4 hours."),
            ("torch", "Click to light a torch, click to swing it. Four quick swings put it out."),
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

    private void Help() {
        var board = new HBoxContainer(); board.AddThemeConstantOverride("separation", 14); content.AddChild(board);
        var tabs = new VBoxContainer(); tabs.AddThemeConstantOverride("separation", 10); board.AddChild(tabs);
        foreach (var (key, glyph, title) in GuideTopics) {
            var tab = new Button { Name = $"guide_{key}", TooltipText = title, CustomMinimumSize = new Vector2(48, 48), FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            tab.Icon = GD.Load<Texture2D>($"res://assets/ui/{glyph}.svg"); tab.ExpandIcon = true; tab.IconAlignment = HorizontalAlignment.Center; tab.AddThemeConstantOverride("icon_max_width", 24);
            tab.AddThemeColorOverride("icon_normal_color", Style.Rim); tab.AddThemeColorOverride("icon_hover_color", Style.Rim);
            var look = key == guideTab ? Style.TabActive : Style.TabRound; Style.Skin(tab, look.Normal, look.Hover);
            string topic = key;
            tab.Pressed += () => { guideTab = topic; OpenPage("help"); };
            tabs.AddChild(tab);
        }
        var page = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; page.AddThemeConstantOverride("separation", 10); board.AddChild(page);
        foreach (var (key, _, title) in GuideTopics) if (key == guideTab) page.AddChild(Style.Heading(title, 20));
        var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); page.AddChild(well);
        var rows = new VBoxContainer(); rows.AddThemeConstantOverride("separation", 8); well.AddChild(rows);
        var tips = GuideTips[guideTab];
        for (int i = 0; i < tips.Length; i++) {
            if (i > 0) rows.AddChild(new HSeparator());
            var (lead, tip) = tips[i];
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12); rows.AddChild(row);
            if (lead.StartsWith("key:")) {
                var cap = Style.Pill(lead[4..], Style.KeyPill, Style.Line, 13); cap.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; cap.CustomMinimumSize = new Vector2(0, 26);
                var slot = new VBoxContainer { CustomMinimumSize = new Vector2(88, 0), Alignment = BoxContainer.AlignmentMode.Center }; slot.AddChild(cap); cap.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
                row.AddChild(slot);
            } else {
                var cup = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; cup.AddThemeStyleboxOverride("panel", Style.CupNormal);
                cup.AddChild(lead.StartsWith("line_") ? Style.Glyph(lead, 28, Style.Ink) : Style.Icon(HotbarIcon(lead), 32));
                row.AddChild(cup);
            }
            var text = Style.Text(tip, 14, Style.Ink); text.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(text);
        }
    }
}
