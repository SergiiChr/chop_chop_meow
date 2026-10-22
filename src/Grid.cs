using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Factorio-style icon cells shared by inventory, storage, station and build pages.</summary>
public partial class Game
{
    private const int GridColumns = 6;

    private (Button Cell, Label Count) IconCell(string name)
    {
        var cell = new Button
        {
            Name = name,
            CustomMinimumSize = new Vector2(50, 50),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Center,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        // Tighter padding than regular buttons so the icon fills most of the cell.
        foreach (var (state, color) in new[] { ("normal", "34483c"), ("hover", "597252"), ("pressed", "7c8550"), ("disabled", "34483c") })
            cell.AddThemeStyleboxOverride(state, Box(new Color(color), 4, 3));
        var count = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetRight = -3,
            OffsetBottom = 1
        };
        count.AddThemeFontSizeOverride("font_size", 12);
        count.AddThemeColorOverride("font_outline_color", new Color("1c2b24"));
        count.AddThemeConstantOverride("outline_size", 4);
        cell.AddChild(count);
        return (cell, count);
    }

    private GridContainer AddGrid()
    {
        var grid = new GridContainer { Columns = GridColumns };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        content.AddChild(grid);
        return grid;
    }

    /// <summary>
    /// One cell per slot, refreshed live from the inventory's stacks.
    /// The inventory is re-read on each refresh because death replaces the backpack object.
    /// </summary>
    private void AddInventoryGrid(Func<Inventory> inventory, Action<string> click, string hint)
    {
        var grid = AddGrid();
        var ids = new string?[inventory().Slots];
        var cells = Enumerable.Range(0, ids.Length).Select(i => IconCell($"slot_{i}")).ToArray();
        for (int i = 0; i < cells.Length; i++)
        {
            int slot = i;
            cells[i].Cell.Pressed += () => { if (ids[slot] is string id) { click(id); RefreshLive(); } };
            grid.AddChild(cells[i].Cell);
        }
        live.Add(() =>
        {
            var stacks = inventory().Stacks().ToList();
            for (int i = 0; i < cells.Length; i++)
            {
                var (cell, count) = cells[i];
                if (!GodotObject.IsInstanceValid(cell)) return;
                ids[i] = i < stacks.Count ? stacks[i].Item : null;
                cell.Icon = ids[i] is string id ? HotbarIcon(id) : null;
                count.Text = i < stacks.Count ? stacks[i].Count.ToString() : "";
                cell.TooltipText = ids[i] is string named ? $"{world.Data.Name(named)} · {inventory()[named]} total\n{hint}" : "";
            }
        });
    }

    /// <summary>Moves one stack, or every unit of the item while Shift is held.</summary>
    private void MoveStack(Inventory from, Inventory to, string item, Thing near)
    {
        if (!world.Near(near)) { Notify("Walk closer first."); return; }
        from.TransferTo(to, item, Input.IsKeyPressed(Key.Shift) ? from[item] : from.StackSize);
    }
}
