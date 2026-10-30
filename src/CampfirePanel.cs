using Godot;

namespace ChopChopMeow;

/// <summary>A campfire: how long its light lasts, the wood in it, and feeding it.</summary>
public partial class CampfirePanel(SidePanel side, Thing fire) : PagePanel(side) {
    public override void _Ready() {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Glyph("line_flame", 30, Style.Ember));
        var light = Style.Text("", 16, Style.Ink); light.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(light);
        var text = new LiveText(shown => light.Text = shown);
        int drawn = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(light)) return;
            int minutes = (int)(fire.Burn / world.Data.DaySeconds * 1440);
            if (minutes == drawn) return;
            drawn = minutes;
            AppendGameHours(text.Clear(), fire.Burn).Append(" of light · wolves keep 5 tiles off"); text.Show();
        });
        var (wood, count) = ItemSlot.Cup("fire_wood"); wood.Icon = Icons.Of("wood"); row.AddChild(wood);
        int held = -1;
        live.Add(() => { if (GodotObject.IsInstanceValid(wood) && fire.Inventory["wood"] != held) { held = fire.Inventory["wood"]; count.Text = held > 0 ? held.ToString() : ""; } });
        AddButton($"Feed · 1 wood · +{world.Data.CampfireHours:0} h", () => world.FuelFire(fire));
        JobButtons(fire);
    }
}
