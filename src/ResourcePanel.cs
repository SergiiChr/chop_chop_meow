using Godot;

namespace ChopChopMeow;

/// <summary>A tree, rock, bush or the lake: what is left, how to gather it, and the gatherers.</summary>
public partial class ResourcePanel(SidePanel side, Thing t) : PagePanel(side) {
    public override void _Ready() {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Icon(Icons.Of(World.ResourceItem(t)), 44));
        var left = Style.Text("", 16, Style.Ink); left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; left.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(left);
        var text = new LiveText(shown => left.Text = shown);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(left)) return;
            var line = text.Clear();
            if (t.Kind != ThingKind.Water) line.Append(t.Remaining).Append(" left · ");
            line.Append(world.GatherBlock(t) ?? (t.IsLoose ? "right-click to pick it up" : "hold right mouse to gather"));
            text.Show();
        });
        if (t.Kind == ThingKind.Water) AddLabel("Collect water · hold right mouse on the pond shore to fill a bottle, or drink if you have none.", 14, Style.Beige);
        JobButtons(t);
    }
}
