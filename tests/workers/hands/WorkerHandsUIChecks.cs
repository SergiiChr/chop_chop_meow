using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task CheckAWorkersLoadAndToolAreTakenAndGivenOnTheirPage() {
        var (bench, worker) = await OpenBenchWorkerPage(); await ShowBackpack();
        worker.Bag.Add("planks", 5); worker.Tool = "steel_axe";
        int planks = world.Backpack["planks"], axes = world.Backpack["steel_axe"];
        // The page's first grid is the tool slot and its second the worker's hands.
        await PickUp(await Showing(1, c => c.TooltipText.StartsWith("Wood planks ·", StringComparison.Ordinal)));
        await Click(BagCell(c => c.Visible && c.Icon == null));
        await Until(() => cursor.Empty && world.Backpack["planks"] == planks + 5 && Cells(1).All(c => c.Icon == null), "the worker's planks landing in the backpack", 2);
        await PickUp(await Showing(0, c => c.TooltipText.StartsWith("Steel axe ·", StringComparison.Ordinal)));
        await Click(BagCell(c => c.Visible && c.Icon == null));
        await Until(() => cursor.Empty && world.Backpack["steel_axe"] == axes + 1 && Cells(0).All(c => c.Icon == null), "the worker's axe landing in the backpack", 2);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood planks ·", StringComparison.Ordinal)));
        await Click(Cells(0).First());
        Require(!cursor.Empty && Cells(0).All(c => c.Icon == null), "A worker's tool slot must not take planks.");
        await Click(Cells(1).First(c => c.Visible));
        await Until(() => cursor.Empty && Cells(1).Any(c => c.TooltipText.StartsWith("Wood planks ·", StringComparison.Ordinal)), "the planks going into the worker's hands", 2);
        await CloseWorkerPage(bench, worker);
    }

    /// <summary>A packed crate put into a worker's hands fills them alone, and the page says so above them.</summary>
    private async Task CheckAWorkerHoldingAPackedCrateSaysSoAboveTheirHands() {
        string packed = PackCrate(Hosted, ("berries", 1));
        var (bench, worker) = await OpenBenchWorkerPage(); await ShowBackpack();
        var carrying = (Label)OnPage("hands")!.GetChild(0);
        await PickUp(BagCell(c => c.TooltipText.StartsWith("Wood crate of", StringComparison.Ordinal)));
        await Click(Cells(1).First(c => c.Visible));
        await Until(() => cursor.Empty && worker.Bag[packed] == 1 && carrying.Text == "Carrying a packed crate", "the worker's page saying their hands carry a packed crate", 2);
        await PickUp(await Showing(1, c => c.TooltipText.StartsWith("Wood crate of", StringComparison.Ordinal)));
        await Click(BagCell(c => c.Visible && c.Icon == null));
        await Until(() => cursor.Empty && worker.Bag.Count == 0, "the packed crate going back into the backpack", 2);
        Drop(Hosted, packed);
        await CloseWorkerPage(bench, worker);
    }
}
