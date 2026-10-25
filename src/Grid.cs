using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Icon cells shared by inventory, storage, station and build pages.</summary>
public partial class Game
{
    private const int GridColumns = 6;

    private (ItemSlot Cell, Label Count) IconCell(string name)
    {
        var cell = new ItemSlot
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
    /// Cells drag their stack out and accept stacks dragged in from another inventory.
    /// The inventory is re-read on each refresh because death replaces the backpack object.
    /// </summary>
    private void AddInventoryGrid(Func<Inventory> inventory, Thing? container, Action<string> click, string hint)
    {
        var grid = AddGrid();
        var stacks = new (string Item, int Count)?[inventory().Slots];
        var cells = Enumerable.Range(0, stacks.Length).Select(i => IconCell($"slot_{i}")).ToArray();
        for (int i = 0; i < cells.Length; i++)
        {
            int slot = i;
            var cell = cells[i].Cell;
            cell.Pressed += () => { if (stacks[slot] is (string id, _)) { Act(() => click(id)); RefreshLive(); } };
            cell.Drag = () => stacks[slot] is (string id, int count) ? new ItemDrag(id, count, inventory(), container) : null;
            cell.CanTake = drag => drag.From != null && drag.From != inventory();
            cell.Take = drag => { Act(() => MoveDrag(drag, inventory(), container)); RefreshLive(); };
            grid.AddChild(cell);
        }
        live.Add(() =>
        {
            var current = inventory().Stacks().ToList();
            for (int i = 0; i < cells.Length; i++)
            {
                var (cell, count) = cells[i];
                if (!GodotObject.IsInstanceValid(cell)) return;
                stacks[i] = i < current.Count ? current[i] : null;
                cell.Icon = stacks[i] is (string id, _) ? HotbarIcon(id) : null;
                count.Text = stacks[i] is (_, int n) ? n.ToString() : "";
                cell.TooltipText = stacks[i] is (string named, _) ? $"{world.Data.Name(named)} · {inventory()[named]} total\n{hint}" : "";
            }
        });
    }

    /// <summary>Moves one stack, or every unit of the item while Shift is held.</summary>
    private void MoveStack(Inventory from, Inventory to, string item, Thing near)
    {
        if (!world.Near(near)) { Notify("Walk closer first."); return; }
        from.TransferTo(to, item, Input.IsKeyPressed(Key.Shift) ? from[item] : from.StackSize);
    }

    /// <summary>Drops a dragged stack into an inventory; the building on either end must be within reach.</summary>
    private void MoveDrag(ItemDrag drag, Inventory to, Thing? container)
    {
        if ((drag.Container ?? container) is Thing near && !world.Near(near)) { Notify("Walk closer first."); return; }
        drag.From?.TransferTo(to, drag.Item, drag.Count);
    }

    /// <summary>Building plans and palette entries drag as hotbar references.</summary>
    private static void DragReference(ItemSlot cell, string id) => cell.Drag = () => new ItemDrag(id, 0, null, null);
}
