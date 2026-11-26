using System;
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

        var root = Card(item);
        root.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        diagram.AddChild(root); diagram.SetRoot(root);

        var uses = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        uses.AddThemeConstantOverride("separation", 7); diagram.AddChild(uses);
        uses.ItemRectChanged += diagram.QueueRedraw;
        foreach (var use in RecipeItemUse.Of(kit.World.Data, item)) {
            var product = Card(use.Recipe.Output, use.Label());
            uses.AddChild(product); diagram.BranchTo(product);
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

    private ItemSlot Card(string id, string? role = null) {
        bool root = role == null;
        string nameText = kit.World.Data.Name(id);
        string tooltip = role == null ? nameText : $"{nameText}\n{role}";
        var card = new ItemSlot {
            Name = root ? "item_tree_root" : $"tree_item_{id}",
            CustomMinimumSize = new Vector2(root ? 252 : 226, 66),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = $"{tooltip}\nClick to find it on the map · {RecipeMap.ItemTreeHint}",
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
        var name = Style.Text(nameText, 18, Style.Rim, Style.Head);
        if (root) name.Name = "item_tree_name";
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; words.AddChild(name);
        if (role != null) words.AddChild(Style.Text(role, 13, Style.Beige));
        return card;
    }
}
