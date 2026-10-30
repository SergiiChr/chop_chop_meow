using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

/// <summary>Finds controls on screen for the UI and network checks.</summary>
public partial class Game {
    private Button? FindButton(Node parent, string text) {
        foreach (var child in parent.GetChildren()) {
            if (child is Button b && b.Text == text) return b;
            var found = FindButton(child, text); if (found != null) return found;
        }
        return null;
    }

    private Button? Named(string name) => panel.FindChild(name, true, false) as Button;

    private bool HasLabel(Node parent, string text) {
        foreach (var child in parent.GetChildren()) {
            if (child is Label label && label.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (HasLabel(child, text)) return true;
        }
        return false;
    }

    /// <summary>The panel's grids in order, then the backpack drawer's.</summary>
    private IEnumerable<ItemSlot> Cells(int grid) => panel.FindChildren("*", "GridContainer", true, false).Concat(bag.FindChildren("*", "GridContainer", true, false))
        .ElementAt(grid).GetChildren().OfType<ItemSlot>();

    private ItemSlot Cell(int grid, Func<ItemSlot, bool> match) => Cells(grid).First(match);

    /// <summary>Waits until a grid shows a matching cell, for what changes on the game's own refresh, and returns the cell.</summary>
    private async Task<ItemSlot> Showing(int grid, Func<ItemSlot, bool> match) {
        await Until(() => Cells(grid).Any(match), "a matching cell showing", 2);
        return Cell(grid, match);
    }

    /// <summary>The side panel's title, or nothing while it is closed.</summary>
    private string ShownPage() => panel.Visible ? headingTitle.Text : "";

    /// <summary>Whether the side panel shows this thing and the world marks it as selected.</summary>
    private bool Shows(Thing thing) => ShownPage() == world.Data.Name(thing.Kind) && view.Selected == thing.Id;

    private IEnumerable<Button> Slots() => panel.FindChildren("slot_*", "Button", true, false).Concat(bag.FindChildren("slot_*", "Button", true, false)).OfType<Button>();
}
