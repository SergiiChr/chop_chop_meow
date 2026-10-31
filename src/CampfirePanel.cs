using Godot;

namespace ChopChopMeow;

/// <summary>A campfire: how long its light lasts, the wood in it, and feeding it.</summary>
public partial class CampfirePanel(SidePanel side, Thing fire) : PagePanel(side) {
    public override void _Ready() {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Glyph("line_flame", 30, Style.Ember));
        var light = Style.Text("", 16, Style.Ink); light.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(light);
        live.Bind(light, text => AppendGameHours(text, fire.Burn).Append(" of light · wolves keep 5 tiles off"));
        var (wood, count) = ItemSlot.Cup("fire_wood"); wood.Icon = Icons.Of("wood"); row.AddChild(wood);
        live.Watch(() => fire.Inventory["wood"], held => count.Text = held > 0 ? held.ToString() : "");
        AddButton($"Feed · 1 wood · +{world.Data.CampfireHours:0} h", () => world.FuelFire(fire));
        JobButtons(fire);
    }
}
