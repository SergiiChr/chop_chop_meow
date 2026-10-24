using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Factorio-style station view: one typed slot per input and output, each holding a single stack.</summary>
public partial class Game
{
    private void Machine(Thing t)
    {
        var recipe = world.Data.Recipes[t.Recipe];
        var row = Row(); row.Alignment = BoxContainer.AlignmentMode.Center;
        foreach (var item in world.BatchCost(t).Keys) row.AddChild(MachineSlot(t, item, true));
        var progress = new ProgressBar { MaxValue = recipe.Seconds, ShowPercentage = false, CustomMinimumSize = new Vector2(48, 10), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        live.Add(() => progress.Value = t.Processing ? t.Progress : 0);
        // Lift the bar to the middle of the boxes, below the icons above them.
        var middle = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; middle.AddThemeConstantOverride("margin_bottom", 20);
        middle.AddChild(progress); row.AddChild(middle);
        // Leftovers from an earlier recipe or fuel choice get their own output slots so they stay reachable.
        foreach (var item in world.Leftovers(t).Append(recipe.Output).Distinct()) row.AddChild(MachineSlot(t, item, false));
        Dynamic(() => $"Batch: {Cost(world.BatchCost(t))}\n{(t.Processing ? $"Working · {t.Progress:0.0}/{recipe.Seconds:0}s" : "Waiting for ingredients / temperature")}", 14, Muted);
    }

    /// <summary>The item's icon above a box showing what the station holds.</summary>
    private VBoxContainer MachineSlot(Thing t, string item, bool input)
    {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 3);
        column.AddChild(new TextureRect { Texture = HotbarIcon(item), CustomMinimumSize = new Vector2(36, 36), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TooltipText = world.Data.Name(item) });
        var (cell, count) = IconCell($"{(input ? "input" : "output")}_{item}");
        cell.Pressed += () => { MoveStack(t.Inventory, world.S.Backpack, item, t); RefreshLive(); };
        cell.Drag = () => t.Inventory[item] > 0 ? new ItemDrag(item, t.Inventory[item], t.Inventory, t) : null;
        if (input)
        {
            cell.CanTake = drag => drag.From == world.S.Backpack && drag.Item == item;
            cell.Take = drag => { world.LoadInput(t, item, drag.Count); RefreshLive(); };
        }
        string hint = input ? "Click: take back · Drag a backpack stack here to load" : "Click or drag: collect";
        live.Add(() =>
        {
            int held = t.Inventory[item];
            cell.Icon = held > 0 ? HotbarIcon(item) : null;
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}/{t.Inventory.StackSize}\n{hint}";
        });
        column.AddChild(cell);
        return column;
    }
}
