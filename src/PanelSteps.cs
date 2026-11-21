using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>Finds controls on screen and opens pages for the UI and network checks.</summary>
public partial class Game {
    /// <summary>Measures repeated refreshes of already open pages while the world they show stands still.</summary>
    private async Task MeasurePanelRefreshAllocations((string Name, Action Open)[] pages) {
        // Opening the pause menu would intercept these pages, so hold the world and open each page directly.
        bool held = true; kit.HoldWhile(() => held);
        await Frames();
        foreach (var (name, open) in pages) {
            open(); await Frames();
            // .NET first runs new code unoptimized, which boxes numbers written into text, and recompiles it optimized once it has been
            // called often, as it does a few seconds after a panel opens. So the page is refreshed in rounds until one allocates nothing;
            // garbage the code itself makes never gets there.
            long bytes = 0; int rounds = 0;
            while (rounds++ < 10) {
                long before = GC.GetAllocatedBytesForCurrentThread();
                // Direct on purpose: this measures the refresh itself, which waiting for the game's own refresh cannot time.
                for (int i = 0; i < 50; i++) RefreshLive();
                bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                if (bytes == 0) break;
                await Seconds(.2);
            }
            string report = $"panel: {name}: {bytes} B over 50 refreshes after {rounds} rounds (budget 0 B)";
            GD.Print(report);
            Require(bytes == 0, $"Allocation budget exceeded: {report}");
        }
        await CloseWithEsc();
        held = false;
    }

    private Button? FindButton(Node parent, string text) {
        foreach (var child in parent.GetChildren()) {
            if (child is Button b && b.Text == text) return b;
            var found = FindButton(child, text); if (found != null) return found;
        }
        return null;
    }

    private Button? Named(string name) => panel.FindChild(name, true, false) as Button;

    /// <summary>A control of the open page by name, such as a job's row or grip, or null.</summary>
    private Control? OnPage(string name) => panel.FindChild(name, true, false) as Control;

    private bool HasLabel(Node parent, string text) {
        foreach (var child in parent.GetChildren()) {
            if (child is Label label && label.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
            if (HasLabel(child, text)) return true;
        }
        return false;
    }

    /// <summary>The panel's grids in order, then the backpack drawer's; none while a page that just opened has not built that grid yet.</summary>
    private IEnumerable<ItemSlot> Cells(int grid) => panel.FindChildren("*", "GridContainer", true, false).Concat(bag.FindChildren("*", "GridContainer", true, false))
        .ElementAtOrDefault(grid)?.GetChildren().OfType<ItemSlot>() ?? [];

    private ItemSlot Cell(int grid, Func<ItemSlot, bool> match) => Cells(grid).First(match);

    /// <summary>Places a building of a kind beside the player, shows its page and opens its filter window.</summary>
    private async Task<Thing> OpenFilters(ThingKind kind) {
        await CloseWithEsc();
        var thing = Hosted.Add(kind, world.Player + new Vector2(4, 0)); await Frames();
        panel.Select(Shown(thing)); await Frames();
        await Click(Named("filters")!);
        return thing;
    }

    /// <summary>
    /// The entries a list of the open filter window shows, in its order, read off the names of its cells, which start with <paramref name="cell"/>.
    /// The rows are <c>filter_in</c> and <c>filter_out</c>, with cells <c>filter_in_</c> and <c>filter_out_</c>; the item list is <c>item_list</c>, with cells <c>pick_</c>.
    /// </summary>
    private IEnumerable<string> FilterEntries(string list, string cell) => OnPage(list)!.FindChildren($"{cell}*", "", true, false).Select(node => node.Name.ToString()[cell.Length..]);

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

    /// <summary>The open station page's work: its input slots, progress bar and output slots.</summary>
    private ProcessRow WorkRow() => panel.FindChildren("*", "", true, false).OfType<ProcessRow>().Single();

    /// <summary>A cell of the backpack drawer, whatever grids the open page has.</summary>
    private ItemSlot BagCell(Func<ItemSlot, bool> match) => bag.FindChildren("*", "GridContainer", true, false)[0].GetChildren().OfType<ItemSlot>().First(match);

    /// <summary>Waits until a grid shows a matching cell, for what changes on the game's own refresh, and returns the cell.</summary>
    private async Task<ItemSlot> Showing(int grid, Func<ItemSlot, bool> match) {
        await Until(() => Cells(grid).Any(match), "a matching cell showing", 2);
        return Cell(grid, match);
    }

    /// <summary>The count pill on the stack carried on the cursor, or empty while it shows none.</summary>
    /// <summary>What the bank shows under its wages: today's sale coins and their colour.</summary>
    private (string Text, Color Color) BankRevenue() => GetTree().Root.FindChild("bank_revenue", true, false) is Label line ? (line.Text, line.GetThemeColor("font_color")) : ("", default);
    private string CarriedCount() => cursor.Visible && cursor.FindChild("held_count", true, false) is Badge { Visible: true } pill ? pill.Text : "";

    /// <summary>Whether the control of this name under <paramref name="parent"/> is on screen.</summary>
    private static bool ShowsChild(Node parent, string name) => parent.FindChild(name, true, false) is Control { Visible: true };

    /// <summary>The side panel's title, or nothing while it is closed.</summary>
    private string ShownPage() => panel.Visible ? panel.HeadingTitle.Text : "";

    /// <summary>Whether the side panel shows this thing and the world marks it as selected.</summary>
    private bool Shows(Thing thing) => ShownPage() == world.Data.Name(thing.Kind) && view.Selection.Thing == thing.Id;

    private IEnumerable<Button> Slots() => panel.FindChildren("slot_*", "Button", true, false).Concat(bag.FindChildren("slot_*", "Button", true, false)).OfType<Button>();

    /// <summary>A bench with a worker on Craft and Haul there, whose page is open; the worker owes wages, so they stand still for the check.</summary>
    private async Task<(Thing Bench, Worker Worker)> OpenBenchWorkerPage() {
        await CloseWithEsc();
        var bench = Hosted.Add(ThingKind.Bench, world.Player + new Vector2(-2, 0));
        Hosted.SetRecipe(bench, "planks");
        var worker = SetupSteps.AddWorker(Hosted, world.Player + Vector2.One); Hosted.Assign(worker, bench);
        worker.WageDebt = 5;
        await KeyStroke(Key.H); await Click(Named($"crew_{worker.Id}")!);
        await Until(() => OnPage("job_1") != null, "the worker's page listing both jobs", 2);
        return (bench, worker);
    }

    /// <summary>Closes the page <see cref="OpenBenchWorkerPage"/> opened and takes its bench and worker out of the world.</summary>
    private async Task CloseWorkerPage(Thing bench, Worker worker) {
        await CloseWithEsc();
        Hosted.Fire(worker); SetupSteps.RemoveThings(Hosted, t => t == bench || t.Kind == ThingKind.Grave);
        await Frames();
    }
}
