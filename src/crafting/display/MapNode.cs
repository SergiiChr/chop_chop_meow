using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>An item card and its links to required tools and additional resources.</summary>
public partial class MapNode(World world, string item, Action<string> choose, Action<string> jump, Action<string> openItemTree) : Control {
    public const float Width = 205, Height = 59, RowPitch = 81;
    private const float SlotSide = 40, Chip = 23;
    public static readonly StyleBoxFlat ToolChip = Style.Flat(Style.SlotFill, 11, 0, Style.Mustard, 2);
    public static readonly StyleBoxFlat ResourceChip = Style.Flat(Style.SlotFill, 11, 0, Style.Sky, 2);
    private static readonly StyleBoxFlat KnownCard = Style.Flat(new Color("344f55"), 10, 0, new Color("658982"), 1);
    private static readonly StyleBoxFlat UnknownCard = Style.Flat(new Color("34495e"), 10, 0, new Color("50677b"), 1);
    private static readonly StyleBoxFlat ChosenCard = Style.Flat(new Color("4d5551"), 10, 0, Style.Held, 3);
    private static readonly StyleBoxFlat Hover = Style.Flat(new Color(Style.Rim, .07f), 10, 0, new Color(Style.Rim, .5f), 1);
    private static readonly StyleBoxFlat LitRing = Style.Flat(new Color(Style.Rim, .12f), 12, 0, Style.Rim, 2);
    private static readonly Color SourceText = new("d5dfdf");
    private static readonly Rect2 ItemIcon = new(8, (Height - SlotSide) / 2, SlotSide, SlotSide);

    private TextureRect icon = null!;
    private bool chosen;
    private SceneTreeTimer? light;

    /// <summary>Whether the settlement has had this item before.</summary>
    public bool Known { get; private set; } = world.Had(item);
    public bool Lit => light != null;

    public bool Chosen {
        get => chosen;
        set {
            if (chosen == value) return;
            chosen = value;
            QueueRedraw();
            ((MapTree)GetParent()).QueueRedraw();
        }
    }

    public override void _Ready() {
        var data = world.Data;
        Name = $"map_{item}";
        CustomMinimumSize = Size = new Vector2(Width, RowPitch - 3);
        MouseFilter = MouseFilterEnum.Ignore;

        var card = new ItemSlot {
            Name = "slot",
            Size = new Vector2(Width, Height),
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = $"{data.Name(item)}\n{RecipeMap.ItemTreeHint}",
            RecipeLink = () => openItemTree(item)
        };
        Style.Skin(card, Style.None, Hover);
        // The card's centre must be clickable even when requirement chips lap its lower edge.
        card.Pressed += () => choose(item);
        AddChild(card);
        icon = Style.Icon(Icons.Of(item), 34);
        icon.Position = ItemIcon.GetCenter() - new Vector2(17, 17); icon.Size = new Vector2(34, 34);
        card.AddChild(icon);

        var recipe = data.RecipeOf(item);
        var name = Style.Text(data.Name(item), 16, Style.Rim, Style.Head);
        name.Position = new Vector2(56, 4); name.Size = new Vector2(Width - 64, 22);
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        card.AddChild(name);
        var source = Style.Text(recipe != null ? Where(data, recipe) : data.Sources[item].How, 13, SourceText, wrap: true);
        source.Name = "source"; source.Position = new Vector2(56, 23); source.Size = new Vector2(Width - 64, 33);
        source.VerticalAlignment = VerticalAlignment.Top;
        source.MaxLinesVisible = 2;
        source.AddThemeConstantOverride("line_spacing", 0);
        card.AddChild(source);

        var also = recipe != null ? recipe.Inputs.Keys.Skip(1) : data.Sources[item].Also;
        var chips = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(10, Height - 4) };
        chips.AddThemeConstantOverride("separation", 3);
        if (recipe is { Tool.Length: > 0 }) {
            string tool = data.PlainestTool(recipe.Tool, recipe.Tier, recipe.Station);
            chips.AddChild(ChipFor(tool, "tool", ToolChip, "Required tool"));
        }
        foreach (string other in also) chips.AddChild(ChipFor(other, "also", ResourceChip, "Additional resource"));
        if (chips.GetChildCount() > 0) AddChild(chips); else chips.Free();
        ShowKnown();
    }

    /// <summary>Turns the item to made before as soon as the settlement has had it, in its details too when they show.</summary>
    public override void _Process(double delta) {
        if (!world.Had(item)) return;
        Known = true; ShowKnown();
        if (chosen) choose(item);
    }

    private void ShowKnown() {
        SetProcess(!Known);
        icon.Modulate = Known ? Colors.White : new Color(1, 1, 1, .76f);
        QueueRedraw();
    }

    /// <summary>Where a recipe is made, in a few words.</summary>
    public static string Where(GameData data, RecipeDef r) => r.Station is not ThingKind station ? "By hand"
        : r.Hand ? $"Hand or {data.Name(station).ToLowerInvariant()}" : data.Name(station);

    private Button ChipFor(string other, string kind, StyleBoxFlat look, string role) {
        var chip = new ItemSlot {
            Name = $"{kind}_{other}",
            CustomMinimumSize = new Vector2(Chip, Chip),
            Icon = Icons.Of(other),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = $"{role}: {world.Data.Name(other)}\nClick to find it on the map · {RecipeMap.ItemTreeHint}",
            RecipeLink = () => openItemTree(other)
        };
        Style.Skin(chip, look, Style.SlotHover); chip.AddThemeConstantOverride("icon_max_width", 15);
        chip.Pressed += () => jump(other);
        return chip;
    }

    /// <summary>Lights the card briefly, starting the time over if it is already lit.</summary>
    public void Light(float seconds) {
        var timer = light = GetTree().CreateTimer(seconds);
        QueueRedraw();
        timer.Timeout += () => {
            if (!IsInstanceValid(this) || light != timer) return;
            light = null; QueueRedraw();
        };
    }

    public override void _Draw() {
        var card = new Rect2(0, 0, Width, Height);
        if (Lit) DrawStyleBox(LitRing, card.Grow(3));
        DrawStyleBox(Chosen ? ChosenCard : Known ? KnownCard : UnknownCard, card);
        MapSlot.Paint(this, ItemIcon, Known);
    }
}
