using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A compact effect marker whose tooltip counts down while the mouse stays on it.</summary>
public partial class FoodEffectBadge : PanelContainer {
    private readonly World world;
    private readonly (string Name, string Description) detail;
    public FoodEffect Effect { get; }

    public FoodEffectBadge(World world, FoodEffect effect) {
        this.world = world;
        Effect = effect;
        Name = effect.ToString();
        detail = FoodEffects.Describe(effect);
        TooltipText = detail.Name;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(48, 48);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        (string Glyph, Color Fill, Color Rim) look = effect switch {
            FoodEffect.CarbLoad => ("line_carb_load", new("502b21"), new("ffb48a")),
            FoodEffect.ProteinRecovery => ("line_protein_recovery", new("52263a"), new("ffafcc")),
            FoodEffect.WellFed => ("line_well_fed", new("4d381d"), new("ffdc83")),
            FoodEffect.Vitamins => ("line_vitamins", new("293e30"), new("b5e2b2")),
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
        AddThemeStyleboxOverride("panel", Style.Flat(look.Fill, 24, 0, look.Rim, 2));
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(center);
        center.AddChild(Style.Glyph(look.Glyph, 24, look.Rim));
    }

    public override GodotObject _MakeCustomTooltip(string forText) => new Countdown(world, Effect, detail);

    private sealed partial class Countdown(World world, FoodEffect effect, (string Name, string Description) detail) : Label {
        private double shown = -1;

        public override void _Ready() {
            AddThemeFontOverride("font", Style.Bold);
            AddThemeFontSizeOverride("font_size", 14);
            AddThemeColorOverride("font_color", Style.Rim);
            _Process(0);
        }

        public override void _Process(double delta) {
            double minutes = Math.Ceiling(world.FoodEffects.SecondsLeft(effect) / world.Data.HourSeconds * 60 - .0001);
            if (minutes == shown) return;
            shown = minutes;
            string left = effect == FoodEffect.WellFed ? "at this pace" : "left";
            Text = $"{detail.Name}\n{detail.Description}\n{Math.Floor(minutes / 60):0} h {minutes % 60:0} min {left}";
            ResetSize();
        }
    }
}
