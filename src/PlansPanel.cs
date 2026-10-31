using Godot;

namespace ChopChopMeow;

/// <summary>Every building plan as a cell: hovering shows what it needs, a click starts placing it, and it drags onto the hotbar.</summary>
public partial class PlansPanel(SidePanel side) : PagePanel(side) {
    public override void _Ready() {
        var grid = AddGrid();
        var details = Style.Wrapped("Hover a plan to see what it needs. Click it, then click open ground nearby.", 14, Style.Beige);
        foreach (var (id, def) in world.Data.Buildings) {
            var (cell, _) = ItemSlot.Cup($"build_{id.Id()}"); cell.Icon = Icons.Of(id.Id()); cell.TooltipText = $"{def.Name}\nClick to place · Drag onto the hotbar"; DragReference(cell, id.Id());
            cell.Pressed += () => { panel.Player.Mode = new ClickMode.Build(id); panel.Close(); kit.Refresh(); };
            cell.MouseEntered += () => details.Text = $"{def.Name}\n{def.Description}\n\nNeeds: {Needs(def.Cost)}";
            live.Add(() => { if (GodotObject.IsInstanceValid(cell)) cell.Modulate = world.Backpack.Has(def.Cost) ? Colors.White : Style.Unready; });
            grid.AddChild(cell);
        }
        content.AddChild(details);
    }
}
