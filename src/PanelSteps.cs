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

    /// <summary>
    /// The text of a named label in a recipe card's hover popup, or "" when the popup has none.
    /// The popup is built as Godot builds it for a hover, which shows it only after a real delay in a window of its own.
    /// </summary>
    private string PopupLabel(string recipe, string name) {
        var popup = ((ItemSlot)Named($"craft_{recipe}")!).Popup!(); AddChild(popup);
        string text = popup.FindChild(name, true, false) is Label label ? label.Text : "";
        popup.QueueFree();
        return text;
    }

    /// <summary>The open station page's work: its input cups, progress bar and output cups.</summary>
    private ProcessRow WorkRow() => panel.FindChildren("*", "", true, false).OfType<ProcessRow>().Single();

    /// <summary>A cell of the backpack drawer, whatever grids the open page has.</summary>
    private ItemSlot BagCell(Func<ItemSlot, bool> match) => bag.FindChildren("*", "GridContainer", true, false)[0].GetChildren().OfType<ItemSlot>().First(match);

    /// <summary>Waits until a grid shows a matching cell, for what changes on the game's own refresh, and returns the cell.</summary>
    private async Task<ItemSlot> Showing(int grid, Func<ItemSlot, bool> match) {
        await Until(() => Cells(grid).Any(match), "a matching cell showing", 2);
        return Cell(grid, match);
    }

    /// <summary>The count pill on the stack carried on the cursor, or empty while it shows none.</summary>
    private string CarriedCount() => GetTree().Root.FindChild("carried_count", true, false) is Badge { Visible: true } pill ? pill.Text : "";

    /// <summary>Whether the control of this name under <paramref name="parent"/> is on screen.</summary>
    private static bool ShowsChild(Node parent, string name) => parent.FindChild(name, true, false) is Control { Visible: true };

    /// <summary>The side panel's title, or nothing while it is closed.</summary>
    private string ShownPage() => panel.Visible ? panel.HeadingTitle.Text : "";

    /// <summary>Whether the side panel shows this thing and the world marks it as selected.</summary>
    private bool Shows(Thing thing) => ShownPage() == world.Data.Name(thing.Kind) && view.Selection.Thing == thing.Id;

    private IEnumerable<Button> Slots() => panel.FindChildren("slot_*", "Button", true, false).Concat(bag.FindChildren("slot_*", "Button", true, false)).OfType<Button>();
}
