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
        var diagram = new ItemTreeBranches { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        diagram.AddThemeConstantOverride("separation", 42); canvas.AddChild(diagram);

        var root = Card(item);
        root.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        diagram.AddChild(root); diagram.SetRoot(root);

        var uses = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        uses.AddThemeConstantOverride("separation", 20); diagram.AddChild(uses);
        uses.ItemRectChanged += diagram.QueueRedraw;
        foreach (var group in RecipeItemUse.Of(kit.World.Data, item).GroupBy(use => use.Role).OrderBy(group => group.Key)) {
            var role = group.Key.ToString();
            var branch = new HBoxContainer { Name = $"item_tree_role_{role}" };
            branch.AddThemeConstantOverride("separation", 42); uses.AddChild(branch);
            branch.ItemRectChanged += diagram.QueueRedraw;
            var caption = Style.Text(role, 17, Style.Rim, Style.Head);
            caption.CustomMinimumSize = new Vector2(104, 0);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.SizeFlagsVertical = SizeFlags.ShrinkCenter; branch.AddChild(caption);
            var products = new VBoxContainer();
            products.AddThemeConstantOverride("separation", 6); branch.AddChild(products);
            foreach (var use in group) products.AddChild(Card(use.Recipe.Output, role));
            diagram.BranchTo(caption, products);
        }
        if (uses.GetChildCount() == 0) {
            var empty = Style.Text("No recipe uses this item directly.", 17, Style.Beige);
            uses.AddChild(empty);
            uses.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }
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
        var card = new MapItemLink(kit.World, id, $"Click to find it on the map · {RecipeMap.ItemTreeHint}", role) {
            Name = root ? $"item_tree_root_{id}" : $"tree_item_{id}",
            CustomMinimumSize = new Vector2(root ? 252 : 226, root ? 66 : 46),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            RecipeLink = () => openItemTree(id)
        };
        card.Pressed += () => showOnMap(id);
        Style.Skin(card, root ? RootCard : ItemCard, root ? RootCard : ItemHover);
        var face = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, root ? 9 : 6);
        face.AddThemeConstantOverride("separation", 8); card.AddChild(face);
        var icon = Style.Icon(null, root ? 36 : 30);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter; face.AddChild(icon);
        var name = Style.Text("", 18, Style.Rim, Style.Head);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        face.AddChild(name);
        card.PresentIdentity = (caption, texture) => {
            icon.Texture = texture;
            icon.Visible = card.Known;
            name.Text = caption;
            name.HorizontalAlignment = card.Known ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        };
        return card;
    }
}
