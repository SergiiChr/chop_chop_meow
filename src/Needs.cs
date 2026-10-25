using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>An outlined icon whose fill drains from the top as its level falls.</summary>
public partial class NeedGauge : Control
{
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
    public Func<float> Level { get; set; } = () => 100;

    public override void _Draw()
    {
        float scale = Size.Y, left = Shape.Min(p => p.X), width = (Shape.Max(p => p.X) - left) * scale;
        var outline = Shape.Select(p => (p - new Vector2(left, 0)) * scale + new Vector2((Size.X - width) / 2, 0)).ToArray();
        float level = Math.Clamp(Level(), 0, 100) / 100;
        // Clip the outline to the part below the current level, so the icon empties like a vessel.
        var below = new[] { new Vector2(-1, Size.Y * (1 - level)), new Vector2(Size.X + 1, Size.Y * (1 - level)), new Vector2(Size.X + 1, Size.Y + 1), new Vector2(-1, Size.Y + 1) };
        foreach (var part in Geometry2D.IntersectPolygons(outline, below)) DrawColoredPolygon(part, Fill);
        DrawPolyline(outline.Append(outline[0]).ToArray(), level <= .25f ? new Color("e0705a") : new Color("f2e8cf"), 2, true);
    }
}

public partial class Game
{
    private NeedGauge health = null!;
    private NeedGauge hunger = null!;
    private NeedGauge thirst = null!;
    private DamageEdges damageEdges = null!;

    private void BuildNeeds()
    {
        var box = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = PanelLeft - 118, OffsetRight = PanelLeft - 10, OffsetTop = 14, OffsetBottom = 52 };
        box.AddThemeStyleboxOverride("panel", Box(new Color(.14f, .23f, .18f, .89f), 8, 4)); ui.AddChild(box);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; box.AddChild(row);
        hunger = new NeedGauge { Name = "hunger", Shape = NeedGauge.Stomach, Fill = new Color("d99a5b"), Level = () => world.S.Hunger, CustomMinimumSize = new Vector2(30, 30) };
        thirst = new NeedGauge { Name = "thirst", Shape = NeedGauge.Droplet, Fill = new Color("6db6df"), Level = () => world.S.Thirst, CustomMinimumSize = new Vector2(26, 30) };
        health = new NeedGauge { Name = "health", Shape = NeedGauge.Heart, Fill = new Color("d9534f"), Level = () => world.S.Health, CustomMinimumSize = new Vector2(30, 30) };
        row.AddChild(health); row.AddChild(hunger); row.AddChild(thirst);
        damageEdges = new DamageEdges(); damageEdges.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); ui.AddChild(damageEdges);
    }

    /// <summary>Flashes the screen edges on a hit; a pinned player keeps a steady glow while the wolves feed.</summary>
    private void DamageFlash(float lost)
    {
        if (lost >= 3) damageEdges.Strength = 1;
        if (world.S.Mauled && !world.Dead) damageEdges.Strength = Math.Max(damageEdges.Strength, .55f);
    }

    private void RefreshNeeds()
    {
        health.TooltipText = $"Health {world.S.Health:0}%"; hunger.TooltipText = $"Hunger {world.S.Hunger:0}%"; thirst.TooltipText = $"Thirst {world.S.Thirst:0}%";
        health.QueueRedraw(); hunger.QueueRedraw(); thirst.QueueRedraw();
    }
}

/// <summary>A red glow along the screen edges, at most 5% of the screen deep, that fades after a hit.</summary>
public partial class DamageEdges : Control
{
    public float Strength { get; set; }

    public DamageEdges() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Process(double delta)
    {
        if (Strength <= 0) return;
        Strength = Math.Max(0, Strength - (float)delta * 1.6f); QueueRedraw();
    }

    public override void _Draw()
    {
        if (Strength <= 0) return;
        var edge = new Color(.85f, .05f, .05f, .7f * Strength); var clear = new Color(.85f, .05f, .05f, 0);
        float x = Size.X * .05f, y = Size.Y * .05f;
        // Each band runs from the opaque screen edge to transparent at its inner side.
        Band(new(0, 0), new(Size.X, 0), new(Size.X - x, y), new(x, y), edge, clear);
        Band(new(0, Size.Y), new(Size.X, Size.Y), new(Size.X - x, Size.Y - y), new(x, Size.Y - y), edge, clear);
        Band(new(0, 0), new(0, Size.Y), new(x, Size.Y - y), new(x, y), edge, clear);
        Band(new(Size.X, 0), new(Size.X, Size.Y), new(Size.X - x, Size.Y - y), new(Size.X - x, y), edge, clear);
    }

    private void Band(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color edge, Color clear) =>
        DrawPolygon(new[] { a, b, c, d }, new[] { edge, edge, clear, clear });
}
