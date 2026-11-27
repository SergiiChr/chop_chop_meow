using Godot;

namespace ChopChopMeow;

/// <summary>Active food effects under the needs plate, with one badge per effect.</summary>
public partial class FoodEffectRow : HBoxContainer {
    private readonly World world;

    public FoodEffectRow(World world) {
        this.world = world;
        Name = "food_effects";
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 5);
        foreach (var effect in FoodEffects.All) AddChild(new FoodEffectBadge(world, effect));
        Refresh();
    }

    public void Refresh() {
        bool any = false;
        for (int i = 0; i < GetChildCount(); i++) {
            var effect = GetChild<FoodEffectBadge>(i);
            effect.Visible = world.FoodEffects.SecondsLeft(effect.Effect) > 0;
            any |= effect.Visible;
        }
        Visible = any;
    }
}
