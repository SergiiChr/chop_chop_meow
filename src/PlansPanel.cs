using Godot;

namespace ChopChopMeow;

/// <summary>
/// Every building plan as a cell: hovering shows what it needs, a click picks it up for placing or for a hotbar slot, and a double click starts placing it.
/// After them a red cross, which hands the player the remove selector.
/// </summary>
public partial class PlansPanel(PageTools tools) : PagePanel(tools) {
    protected override void Build() {
        var grid = NewGrid(); AddChild(grid);
        var details = Style.Wrapped("Hover a plan to see what it needs. Pick it up and click open ground nearby, or put it on a hotbar slot.", 14, Style.Beige);
        foreach (var (id, def) in world.Data.Buildings) {
            var (cell, _) = ItemSlot.Create($"build_{id.Id()}"); cell.Icon = Icons.Of(id.Id()); cell.TooltipText = $"{def.Name}\nLMB: pick up · Double LMB: place";
            cell.Controls = new SlotInput(cursor) {
                Click = () => cursor.PickUp(id.Id()),
                Use = () => { panel.Player.Mode = new ClickMode.Build(id); panel.Close(); kit.Refresh(); },
                Takes = held => held.Plan != null,
                Put = (held, _) => held.Release(),
            };
            // A mine costs nothing to mark, so it lists what each of its stages takes instead.
            cell.MouseEntered += () => details.Text = id == ThingKind.Mine ? $"{def.Name}\n{PageText.MinePlan(world)}" : $"{def.Name}\n{def.Description}\n\nNeeds: {PageText.Needs(world, def.Cost)}";
            live.Watch(() => world.Backpack.Has(def.Cost), ready => cell.Modulate = ready ? Colors.White : Style.Unready);
            grid.AddChild(cell);
        }
        var (remove, _) = ItemSlot.Create("remove"); remove.Icon = Icons.Of("line_close"); remove.TooltipText = "Remove\nLMB: pick buildings to take down";
        remove.AddThemeColorOverride(ItemSlot.IconNormal, Style.Brick); remove.AddThemeColorOverride("icon_hover_color", Style.Brick); remove.AddThemeColorOverride("icon_pressed_color", Style.Brick);
        void StartRemoving() { panel.Player.Mode = new ClickMode.Remove(); panel.Close(); kit.Refresh(); }
        // A click with a plan held on the cursor lets go of the plan first.
        remove.Controls = new SlotInput(cursor) { Click = StartRemoving, Takes = held => held.Plan != null, Put = (held, _) => { held.Release(); StartRemoving(); } };
        remove.MouseEntered += () => details.Text = "Remove\nClick a building or a gravel path tile within reach to take it down at once. Its cost and everything it holds come back to your backpack.";
        grid.AddChild(remove);
        AddChild(details);
    }
}
