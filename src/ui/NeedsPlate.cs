using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>An outlined icon filled with its need's colour.</summary>
public partial class NeedGauge : Control {
    // Outlines in units of the gauge height; x is centred afterwards.
    public static readonly Vector2[] Stomach =
    {
        new(.5f, 0), new(.68f, 0), new(.68f, .14f), new(.82f, .18f), new(.94f, .3f), new(1, .46f), new(.98f, .63f), new(.9f, .78f), new(.76f, .9f),
        new(.58f, .97f), new(.4f, .98f), new(.25f, .94f), new(.14f, .86f), new(.06f, .8f), new(0, .72f), new(.07f, .64f), new(.16f, .7f),
        new(.26f, .74f), new(.36f, .72f), new(.44f, .62f), new(.5f, .48f), new(.5f, .3f)
    };
    public static readonly Vector2[] Droplet = Enumerable.Range(0, 25)
        .Select(i => Mathf.DegToRad(63 + i * 234 / 24f)).Select(a => new Vector2(.34f + .3f * MathF.Sin(a), .66f - .3f * MathF.Cos(a)))
        .Prepend(new Vector2(.34f, 0)).ToArray();
    // The classic parametric heart, scaled to the unit height with its point at the bottom.
    public static readonly Vector2[] Heart = Enumerable.Range(0, 40).Select(i => i * MathF.Tau / 40)
        .Select(a => new Vector2(16 * MathF.Pow(MathF.Sin(a), 3), 13 * MathF.Cos(a) - 5 * MathF.Cos(2 * a) - 2 * MathF.Cos(3 * a) - MathF.Cos(4 * a)))
        .Select(p => new Vector2((p.X + 17) / 30, (12 - p.Y) / 30)).ToArray();
    public Vector2[] Shape { get; set; } = Droplet;
    public Color Fill { get; set; }
    private Vector2[] outline = Array.Empty<Vector2>(), rim = Array.Empty<Vector2>();
    private Vector2 outlined;

    public override void _Draw() {
        if (Size.Y <= 0) return;
        if (Size != outlined) {
            outlined = Size;
            float scale = Size.Y, left = Shape.Min(p => p.X), width = (Shape.Max(p => p.X) - left) * scale;
            outline = Shape.Select(p => (p - new Vector2(left, 0)) * scale + new Vector2((Size.X - width) / 2, 0)).ToArray();
            rim = outline.Append(outline[0]).ToArray();
        }
        DrawColoredPolygon(outline, Fill);
        DrawPolyline(rim, Style.Rim, 2, true);
    }
}

/// <summary>A cream capsule that fills from the bottom with a need's level.</summary>
public partial class Vial : Control {
    public Color Fill { get; set; }
    public float Level { get; set; } = 100;
    public float Bonus { get; set; }
    /// <summary>Rims the capsule in brick: the level runs out.</summary>
    public bool Warn { get; set; }

    public Vial() { CustomMinimumSize = new Vector2(22, 56); MouseFilter = MouseFilterEnum.Pass; }

    public override void _Draw() {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Warn ? WarnBox : Box, rect);
        float normal = Math.Clamp(Level, 0, 100);
        float bonus = Math.Clamp(Math.Max(Bonus, Level - 100), 0, 100);
        float firstLap = Math.Min(100 - normal, bonus);
        Color brighter = Fill.Lightened(.4f);
        float scale = (Size.Y - 6) / 100;
        if (normal > 0) DrawFill(Fill, 0, normal + firstLap);
        DrawFill(brighter, normal, firstLap, joinsBottom: normal > 0);
        DrawFill(brighter, 0, bonus - firstLap);

        void DrawFill(Color color, float start, float amount, bool joinsBottom = false) {
            float height = amount * scale;
            if (height > 1) DrawStyleBox(FillBox(color, joinsBottom),
                new Rect2(3, Size.Y - 3 - start * scale - height, Size.X - 6, height));
        }
    }

    internal static string LevelText(string name, float total, float bonus = 0) =>
        bonus > 0 ? $"{name}: {total - bonus:0}% + {bonus:0}% bonus." : $"{name} {total:0}%";

    private static readonly StyleBoxFlat Box = Round(Style.SlotFill, Style.Line), WarnBox = Round(Style.SlotFill, Style.Brick);
    private static StyleBoxFlat Round(Color bg, Color edge) {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = edge, AntiAliasing = true };
        box.SetCornerRadiusAll(11); box.SetBorderWidthAll(2);
        return box;
    }
    // The brighter fill can end on a flat edge where it meets the normal fill.
    private static readonly System.Collections.Generic.Dictionary<(Color, bool), StyleBoxFlat> fills = new();
    private static StyleBoxFlat FillBox(Color color, bool joinsBottom) {
        if (fills.TryGetValue((color, joinsBottom), out var box)) return box;
        fills[(color, joinsBottom)] = box = new StyleBoxFlat { BgColor = color, AntiAliasing = true };
        box.SetCornerRadiusAll(8);
        if (joinsBottom) { box.CornerRadiusBottomLeft = 0; box.CornerRadiusBottomRight = 0; }
        return box;
    }
}

/// <summary>A need's capsule with its icon under it, as the corner plate and the cabin show it.</summary>
public partial class NeedColumn : VBoxContainer {
    public Vial Vial { get; }

    private NeedColumn(string name, Vector2[] shape, Color color) {
        AddThemeConstantOverride("separation", 6);
        Vial = new Vial { Name = name, Fill = color, SizeFlagsHorizontal = SizeFlags.ShrinkCenter }; AddChild(Vial);
        AddChild(new NeedGauge { Shape = shape, Fill = color, CustomMinimumSize = new Vector2(22, 20), MouseFilter = MouseFilterEnum.Ignore });
    }

    /// <summary>Health, hunger and thirst, in that order.</summary>
    public static NeedColumn[] All() => [new("health", NeedGauge.Heart, Style.Brick), new("hunger", NeedGauge.Stomach, Style.Mustard), new("thirst", NeedGauge.Droplet, Style.Sky)];
}

/// <summary>
/// The top left plate: health, hunger and thirst as capsules that drain, each over its icon, and the bank:
/// coins kept for wages at the trading posts, with the day's wages, in red while they come to more than the bank holds.
/// </summary>
public partial class NeedsPlate : PanelContainer {
    private readonly UiKit kit;
    private readonly World world;
    private readonly DamageEdges edges;
    private readonly Label bankDue, bankRevenue;
    private readonly Badge bankCount;
    private readonly ItemSlot bankSlot;
    // The capsules, whose tooltips name each need's level.
    public Vial Health { get; }
    public Vial Hunger { get; }
    public Vial Thirst { get; }

    public NeedsPlate(UiKit kit, ItemMoves moves, DamageEdges edges, HeldCursor cursor) {
        this.kit = kit; world = kit.World; this.edges = edges;
        OffsetLeft = 20; OffsetTop = 18;
        AddThemeStyleboxOverride("panel", Style.Plate);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); AddChild(row);
        var needs = NeedColumn.All();
        foreach (var need in needs) row.AddChild(need);
        (Health, Hunger, Thirst) = (needs[0].Vial, needs[1].Vial, needs[2].Vial);
        row.AddChild(new ColorRect { Color = new Color(Style.Beige, .7f), CustomMinimumSize = new Vector2(3, 0) });
        var bank = new VBoxContainer(); bank.AddThemeConstantOverride("separation", 8); row.AddChild(bank);
        bank.AddChild(Style.Heading("Bank", 13));
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 12); bank.AddChild(line);
        var (slot, count) = ItemSlot.Create("bank", 56);
        bankSlot = slot; bankCount = count;
        Style.Skin(slot, Style.SlotBank, Style.Round(Style.SlotFill, 28).Item2);
        slot.Icon = Icons.Of(Inventory.Coins);
        kit.LinkRecipeMap(slot, () => Inventory.Coins);
        slot.TooltipText = $"Coins kept at the trading posts for wages; sales land here.\nAt a trading post · LMB: pick up all · RMB: half · put coins here to bank them\n{RecipeMap.AltClickHint}";
        slot.Controls = new SlotInput(cursor) {
            Pick = () => NearPost() is Thing post && post.Wages!.Layout[0].Item != "" ? (post.Wages, 0) : null,
            Takes = held => held.Stack is { Box: 0, Item: Inventory.Coins },
            Put = (_, units) => {
                if (NearPost() is not Thing post) { kit.Notify("Bank coins at a trading post."); return; }
                kit.Run(() => moves.PutDown(post.Wages!, -1, units, post));
            },
            Batch = () => {
                if (NearPost() is not Thing post) { kit.Notify("Take coins out at a trading post."); return; }
                kit.Run(() => moves.Stack(post.Wages!, world.S.Backpack, Inventory.Coins, post));
            },
            Click = () => kit.Notify("Take coins out at a trading post."),
        };
        line.AddChild(slot);
        // Wages due a day, with what sales brought in today under them.
        var money = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; money.AddThemeConstantOverride("separation", 2); line.AddChild(money);
        bankDue = Style.Text("", 14, Style.Beige); money.AddChild(bankDue);
        bankRevenue = Style.Text("", 14, Style.Warn); bankRevenue.Name = "bank_revenue"; money.AddChild(bankRevenue);
    }

    /// <summary>The trading post within reach that has coins or room for them, or null.</summary>
    private Thing? NearPost() {
        foreach (var t in world.ThingsOf(ThingKind.Signpost)) if (t.Wages != null && world.Reaches(t)) return t;
        return null;
    }

    /// <summary>Flashes the screen edges on a hit; a pinned player keeps a steady glow while the wolves feed, and a hungry or thirsty one a faint pulse.</summary>
    public void DamageFlash(float lost, float runTime) {
        if (lost >= 3) edges.Strength = 1;
        if (world.Mauled && !world.Dead) edges.Strength = Math.Max(edges.Strength, .55f);
        // Food or drink under 15% keeps the edges pulsing faintly until the player eats or drinks.
        else if (!world.Dead && (world.Hunger < GameData.LowNeed || world.Thirst < GameData.LowNeed)) edges.Strength = Math.Max(edges.Strength, .5f + .15f * MathF.Sin(runTime * 4));
    }

    // The rounded values the tooltips show, so their text is built only when one changes.
    private (int Health, int Hunger, int Thirst, int BonusHealth) shownNeeds = (-1, -1, -1, -1);
    private (int Bank, int Due, int Revenue) shownBank = (-1, -1, -1);
    public void Refresh() {
        var needs = ((int)MathF.Round(world.Health, MidpointRounding.AwayFromZero), (int)MathF.Round(world.Hunger, MidpointRounding.AwayFromZero), (int)MathF.Round(world.Thirst, MidpointRounding.AwayFromZero), (int)MathF.Round(world.BonusHealth, MidpointRounding.AwayFromZero));
        if (needs != shownNeeds) {
            shownNeeds = needs;
            Health.TooltipText = Vial.LevelText("Health", world.Health, world.BonusHealth);
            Hunger.TooltipText = $"Hunger {world.Hunger:0}%"; Thirst.TooltipText = $"Thirst {world.Thirst:0}%";
            Health.Level = world.Health - world.BonusHealth; Health.Bonus = world.BonusHealth;
            Hunger.Level = world.Hunger; Thirst.Level = world.Thirst;
            Health.Warn = world.Health <= 25; Hunger.Warn = world.Hunger <= 25; Thirst.Warn = world.Thirst <= 25;
            Health.QueueRedraw(); Hunger.QueueRedraw(); Thirst.QueueRedraw();
        }
        var bank = (world.WageCoins, world.DailyWages, world.DayRevenue);
        if (bank != shownBank) {
            shownBank = bank;
            bankCount.Text = bank.WageCoins > 0 ? bank.WageCoins.ToString() : "";
            bankSlot.AddThemeColorOverride(ItemSlot.IconNormal, bank.WageCoins > 0 ? Colors.White : Style.Ghost);
            bankDue.Text = bank.DailyWages > 0 ? $"−{bank.DailyWages} /day" : "";
            bankDue.AddThemeColorOverride("font_color", bank.DailyWages > bank.WageCoins ? Style.Warn : Style.Beige);
            bankDue.TooltipText = bank.DailyWages > bank.WageCoins ? "More than the bank holds; the rest comes from your backpack at dawn" : "Paid from the bank at dawn";
            bankRevenue.Text = $"+{bank.DayRevenue}";
            // Green once today's sales are above a day's wages.
            bankRevenue.AddThemeColorOverride("font_color", bank.DayRevenue > bank.DailyWages ? Style.Ready : Style.Warn);
            bankRevenue.TooltipText = "Coins sales brought in since dawn";
        }
    }
}

/// <summary>A red glow along the screen edges, at most 5% of the screen deep, that fades after a hit.</summary>
public partial class DamageEdges : Control {
    public float Strength { get; set; }

    public DamageEdges() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Process(double delta) {
        if (Strength <= 0) return;
        Strength = Math.Max(0, Strength - (float)delta * 1.6f); QueueRedraw();
    }

    public override void _Draw() {
        if (Strength <= 0) return;
        var edge = new Color(.85f, .05f, .05f, .7f * Strength); var clear = new Color(.85f, .05f, .05f, 0);
        float x = Size.X * .05f, y = Size.Y * .05f;
        // Each band runs from the opaque screen edge to transparent at its inner side.
        Band(new(0, 0), new(Size.X, 0), new(Size.X - x, y), new(x, y), edge, clear);
        Band(new(0, Size.Y), new(Size.X, Size.Y), new(Size.X - x, Size.Y - y), new(x, Size.Y - y), edge, clear);
        Band(new(0, 0), new(0, Size.Y), new(x, Size.Y - y), new(x, y), edge, clear);
        Band(new(Size.X, 0), new(Size.X, Size.Y), new(Size.X - x, Size.Y - y), new(Size.X - x, y), edge, clear);
    }

    // Reused, as a low food or drink pulse redraws the edges every frame.
    private readonly Vector2[] corners = new Vector2[4];
    private readonly Color[] shades = new Color[4];

    private void Band(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color edge, Color clear) {
        (corners[0], corners[1], corners[2], corners[3]) = (a, b, c, d);
        (shades[0], shades[1], shades[2], shades[3]) = (edge, edge, clear, clear);
        DrawPolygon(corners, shades);
    }
}
