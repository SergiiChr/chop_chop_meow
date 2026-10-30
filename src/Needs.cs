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

/// <summary>A cream capsule that fills from the bottom with a need's level; faded, it shows a level to come.</summary>
public partial class Vial : Control {
    public Color Fill { get; set; }
    public float Level { get; set; } = 100;
    public bool Faded { get; set; }
    /// <summary>Rims the capsule in brick: the level runs out.</summary>
    public bool Warn { get; set; }

    public Vial() { CustomMinimumSize = new Vector2(22, 56); MouseFilter = MouseFilterEnum.Pass; }

    public override void _Draw() {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Warn ? WarnBox : Box, rect);
        float inner = Size.Y - 6, height = inner * Math.Clamp(Level, 0, 100) / 100;
        if (height > 1) DrawStyleBox(FillBox(Faded ? Fill with { A = .55f } : Fill), new Rect2(3, 3 + inner - height, Size.X - 6, height));
    }

    private static readonly StyleBoxFlat Box = Round(Style.CupFill, Style.Line), WarnBox = Round(Style.CupFill, Style.Brick);
    private static StyleBoxFlat Round(Color bg, Color edge) {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = edge, AntiAliasing = true };
        box.SetCornerRadiusAll(11); box.SetBorderWidthAll(2);
        return box;
    }
    // One box per colour, made the first time it is drawn.
    private static readonly System.Collections.Generic.Dictionary<Color, StyleBoxFlat> fills = new();
    private static StyleBoxFlat FillBox(Color color) {
        if (!fills.TryGetValue(color, out var box)) { fills[color] = box = new StyleBoxFlat { BgColor = color, AntiAliasing = true }; box.SetCornerRadiusAll(8); }
        return box;
    }
}

public partial class Game {
    private Vial health = null!;
    private Vial hunger = null!;
    private Vial thirst = null!;
    private DamageEdges damageEdges = null!;
    private Label bankDue = null!;
    private Badge bankCount = null!;
    private ItemSlot bankCup = null!;

    /// <summary>
    /// The top left plate: health, hunger and thirst as capsules that drain, each over its icon, and the bank:
    /// coins kept for wages at the trading posts, with the day's wages, in red while they come to more than the bank holds.
    /// </summary>
    private void BuildNeeds() {
        var box = needsPlate = new PanelContainer { OffsetLeft = 20, OffsetTop = 18 };
        box.AddThemeStyleboxOverride("panel", Style.Plate); ui.AddChild(box);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); box.AddChild(row);
        Vial Need(string name, Vector2[] shape, Color color) {
            var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); row.AddChild(column);
            var vial = new Vial { Name = name, Fill = color }; vial.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter; column.AddChild(vial);
            var icon = new NeedGauge { Shape = shape, Fill = color, CustomMinimumSize = new Vector2(22, 20), MouseFilter = Control.MouseFilterEnum.Ignore };
            column.AddChild(icon);
            return vial;
        }
        health = Need("health", NeedGauge.Heart, Style.Brick);
        hunger = Need("hunger", NeedGauge.Stomach, Style.Mustard);
        thirst = Need("thirst", NeedGauge.Droplet, Style.Sky);
        row.AddChild(new ColorRect { Color = new Color(Style.Beige, .7f), CustomMinimumSize = new Vector2(3, 0) });
        var bank = new VBoxContainer(); bank.AddThemeConstantOverride("separation", 8); row.AddChild(bank);
        bank.AddChild(Style.Heading("Bank", 13));
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 12); bank.AddChild(line);
        var (cup, count) = ItemSlot.Cup("bank", 56);
        bankCup = cup; bankCount = count;
        Style.Skin(cup, Style.CupBank, Style.Round(Style.CupFill, 28).Item2);
        cup.Icon = Icons.Of(Inventory.Coins);
        cup.TooltipText = "Coins kept at the trading posts for wages; sales land here first.\nDrop coins here at a trading post to bank them · click to take some out";
        cup.CanTake = drag => drag.Item == Inventory.Coins && drag.From == world.S.Backpack;
        cup.Take = drag => {
            if (NearPost() is not Thing post) { Notify("Bank coins at a trading post."); return drag.Count; }
            int left = 0; kit.Act(() => left = MoveDrag(drag, post.Wages!, post)); RefreshLive(); return left;
        };
        cup.Pressed += () => {
            if (NearPost() is not Thing post) { Notify("Take coins out at a trading post."); return; }
            kit.Act(() => MoveStack(post.Wages!, world.S.Backpack, Inventory.Coins, post)); RefreshLive();
        };
        line.AddChild(cup);
        bankDue = Style.Text("", 14, Style.Beige); line.AddChild(bankDue);
        damageEdges = new DamageEdges(); damageEdges.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); ui.AddChild(damageEdges);
    }

    /// <summary>The trading post within reach that has coins or room for them, or null.</summary>
    private Thing? NearPost() {
        foreach (var t in world.S.Things) if (t.Kind == ThingKind.Signpost && t.Wages != null && world.Near(t)) return t;
        return null;
    }

    /// <summary>Flashes the screen edges on a hit; a pinned player keeps a steady glow while the wolves feed, and a hungry or thirsty one a faint pulse.</summary>
    private void DamageFlash(float lost) {
        if (lost >= 3) damageEdges.Strength = 1;
        if (world.S.Mauled && !world.Dead) damageEdges.Strength = Math.Max(damageEdges.Strength, .55f);
        // Food or drink under 15% keeps the edges pulsing faintly until the player eats or drinks.
        else if (!world.Dead && (world.S.Hunger < GameData.LowNeed || world.S.Thirst < GameData.LowNeed)) damageEdges.Strength = Math.Max(damageEdges.Strength, .5f + .15f * MathF.Sin(runTime * 4));
    }

    // The rounded values the tooltips show, so their text is built only when one changes.
    private (int Health, int Hunger, int Thirst) shownNeeds = (-1, -1, -1);
    private (int Bank, int Due) shownBank = (-1, -1);
    private void RefreshNeeds() {
        var needs = ((int)MathF.Round(world.S.Health, MidpointRounding.AwayFromZero), (int)MathF.Round(world.S.Hunger, MidpointRounding.AwayFromZero), (int)MathF.Round(world.S.Thirst, MidpointRounding.AwayFromZero));
        if (needs != shownNeeds) {
            shownNeeds = needs;
            health.TooltipText = $"Health {world.S.Health:0}%"; hunger.TooltipText = $"Hunger {world.S.Hunger:0}%"; thirst.TooltipText = $"Thirst {world.S.Thirst:0}%";
            health.Level = world.S.Health; hunger.Level = world.S.Hunger; thirst.Level = world.S.Thirst;
            health.Warn = world.S.Health <= 25; hunger.Warn = world.S.Hunger <= 25; thirst.Warn = world.S.Thirst <= 25;
            health.QueueRedraw(); hunger.QueueRedraw(); thirst.QueueRedraw();
        }
        var bank = (world.WageCoins, DailyWages());
        if (bank != shownBank) {
            shownBank = bank;
            bankCount.Text = bank.WageCoins > 0 ? bank.WageCoins.ToString() : "";
            bankCup.AddThemeColorOverride(ItemSlot.IconNormal, bank.WageCoins > 0 ? Colors.White : Style.Ghost);
            bankDue.Text = bank.Item2 > 0 ? $"−{bank.Item2} /day" : "";
            bankDue.AddThemeColorOverride("font_color", bank.Item2 > bank.WageCoins ? Style.Warn : Style.Beige);
            bankDue.TooltipText = bank.Item2 > bank.WageCoins ? "More than the bank holds; the rest comes from your backpack at dawn" : "Paid from the bank at dawn";
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
