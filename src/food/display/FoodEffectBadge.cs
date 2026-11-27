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
        AddThemeStyleboxOverride("panel", Style.KeyPill);
        AddChild(effect switch {
            FoodEffect.CarbLoad => Style.Glyph("line_flame", 18, Style.Brick),
            FoodEffect.ProteinRecovery => NeedIcon(NeedGauge.Heart, Style.Brick),
            FoodEffect.WellFed => NeedIcon(NeedGauge.Stomach, Style.Mustard),
            _ => Style.Glyph("line_leaf", 18, Style.Sage)
        });
    }

    private static NeedGauge NeedIcon(Vector2[] shape, Color fill) =>
        new() { Shape = shape, Fill = fill, CustomMinimumSize = new Vector2(18, 18), MouseFilter = MouseFilterEnum.Ignore };

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
