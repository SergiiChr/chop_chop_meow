using System;
using System.Collections.Generic;
using System.Linq;
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
    private ItemSlot Cell(int grid, Func<ItemSlot, bool> match) => panel.FindChildren("*", "GridContainer", true, false).Concat(bag.FindChildren("*", "GridContainer", true, false))
        .ElementAt(grid).GetChildren().OfType<ItemSlot>().First(match);

    private IEnumerable<Button> Slots() => panel.FindChildren("slot_*", "Button", true, false).Concat(bag.FindChildren("slot_*", "Button", true, false)).OfType<Button>();
}
