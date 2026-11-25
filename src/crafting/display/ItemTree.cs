using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>One item at the root of the recipes that directly use it as an ingredient, tool or fuel.</summary>
internal partial class ItemTree(UiKit kit, string item, Action back, Action close,
    Action<string> showOnMap, Action<string> openItemTree) : VBoxContainer {
    private static readonly StyleBoxFlat Canvas = Style.Flat(new Color("283c50"), 14, 20);
    private static readonly StyleBoxFlat ItemCard = Style.Flat(new Color("34495e"), 10, 10, new Color("647d8e"), 1);
    private static readonly StyleBoxFlat ItemHover = Style.Flat(new Color("415b6e"), 10, 10, Style.Rim, 1);
    private static readonly StyleBoxFlat RootCard = Style.Flat(new Color("4d5551"), 10, 10, Style.Held, 3);

    public override void _Ready() {
        Name = "item_tree";
        AddThemeConstantOverride("separation", 12);
        AddChild(Header());

        var scroll = new ScrollContainer {
            Name = "item_tree_rows",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(scroll);
        var canvas = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        canvas.AddThemeStyleboxOverride("panel", Canvas); scroll.AddChild(canvas);
        var diagram = new ItemTreeBranches();
        diagram.AddThemeConstantOverride("separation", 42); canvas.AddChild(diagram);

        var rootColumn = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        rootColumn.AddThemeConstantOverride("separation", 7); diagram.AddChild(rootColumn);
        rootColumn.AddChild(Style.Text("Selected item", 16, Style.Beige, Style.Head));
        var data = kit.World.Data;
        string source = data.RecipeOf(item) is RecipeDef recipe ? MapNode.Where(data, recipe) : data.Sources[item].How;
        var root = Card(item, source, root: true);
        rootColumn.AddChild(root); diagram.SetRoot(root);

        var uses = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        uses.AddThemeConstantOverride("separation", 14); diagram.AddChild(uses);
        foreach (var group in RecipeItemUse.Of(data, item).GroupBy(use => use.Recipe.Station)) {
            var branch = new VBoxContainer(); branch.AddThemeConstantOverride("separation", 7); uses.AddChild(branch);
            var title = Style.Text(group.Key is ThingKind station ? data.Name(station) : "By hand", 19, Style.Rim, Style.Head);
            branch.AddChild(title); diagram.BranchTo(branch, title);
            var cards = new HFlowContainer(); cards.AddThemeConstantOverride("h_separation", 8);
            cards.AddThemeConstantOverride("v_separation", 8); branch.AddChild(cards);
            foreach (var use in group) {
                string role = use.Label();
                if (use.Recipe is { Hand: true, Station: not null }) role += " · Also by hand";
                cards.AddChild(Card(use.Recipe.Output, role));
            }
        }
        if (uses.GetChildCount() == 0) uses.AddChild(Style.Text("No recipe uses this item directly.", 17, Style.Beige));
    }

    private HBoxContainer Header() {
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 16);
        var goBack = kit.Button("←  Recipe map", back);
        goBack.Name = "item_tree_back"; goBack.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Style.SkinPill(goBack, Style.Line); header.AddChild(goBack);
        var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 1); header.AddChild(words);
        words.AddChild(Style.Heading("Item tree", 30));
        words.AddChild(Style.Text("Click to find an item on the map. Alt-click to follow its direct recipes.", 15, Style.Beige));
        header.AddChild(kit.HeaderButton("close", "Close · Esc", close, Style.CloseRound, "map_close"));
        return header;
    }

    private ItemSlot Card(string id, string note, bool root = false) {
        var data = kit.World.Data;
        var card = new ItemSlot {
            Name = root ? "item_tree_root" : $"tree_item_{id}",
            CustomMinimumSize = new Vector2(root ? 252 : 226, 66),
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = $"{data.Name(id)}\n{note}\nClick to find it on the map · {RecipeMap.ItemTreeHint}",
            RecipeLink = () => openItemTree(id)
        };
        card.Pressed += () => showOnMap(id);
        Style.Skin(card, root ? RootCard : ItemCard, root ? RootCard : ItemHover);
        var face = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 9);
        face.AddThemeConstantOverride("separation", 8); card.AddChild(face);
        var icon = Style.Icon(Icons.Of(id), 36);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter; face.AddChild(icon);
        var words = new VBoxContainer {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        words.AddThemeConstantOverride("separation", 1); face.AddChild(words);
        var name = Style.Text(data.Name(id), 18, Style.Rim, Style.Head);
        if (root) name.Name = "item_tree_name";
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; words.AddChild(name);
        var source = Style.Text(note, 13, Style.Beige);
        source.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; words.AddChild(source);
        return card;
    }
}
