using Godot;

namespace ChopChopMeow;

/// <summary>A tree, rock, bush or the lake: what is left, how to gather it, and the gatherers.</summary>
public partial class ResourcePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Icon(Icons.Of(World.ResourceItem(thing)), 44));
        var left = Style.Text("", 16, Style.Ink, wrap: true); left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(left);
        live.Bind(left, line => {
            if (thing.Kind != ThingKind.Water) line.Append(thing.Remaining).Append(" left · ");
            line.Append(world.GatherBlock(thing) ?? (thing.IsLoose ? "right-click to pick it up" : "hold right mouse to gather"));
        });
        if (thing.Kind == ThingKind.Water) AddLabel("Collect water · hold right mouse on the pond shore to fill a bottle, or drink if you have none.", 14, Style.Beige);
        JobButtons();
    }
}
