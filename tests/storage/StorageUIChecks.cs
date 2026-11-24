using System.Linq;
using System.Threading.Tasks;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of storage filters: the Everything entry, the cell marked A.</summary>
    private async Task RunStorageUIChecks() {
        await TestClean(CheckEverythingComesFirstInTheItemListAndStandsAloneInItsRow);
    }

    /// <summary>A new chest's window shows A in the output row and nothing in the input row; A comes first in the item list, and an item picked after it takes it off.</summary>
    private async Task CheckEverythingComesFirstInTheItemListAndStandsAloneInItsRow() {
        var chest = await OpenFilters(ThingKind.Chest);
        Require(FilterEntries("filter_out", "filter_out_").SequenceEqual([Inventory.Everything]) && !FilterEntries("filter_in", "filter_in_").Any(),
            "A new chest's filters must show A in the output row and nothing in the input row.");
        await Click(Named("filter_add_in")!);
        Require(FilterEntries("item_list", "pick_").First() == Inventory.Everything, "Everything must come first in the item list.");
        await Click(Named($"pick_{Inventory.Everything}")!);
        await Until(() => chest.Inventory.InputFilter.SequenceEqual([Inventory.Everything]) && FilterEntries("filter_in", "filter_in_").SequenceEqual([Inventory.Everything]),
            "picking Everything putting A in the input row", 2);
        await Click(Named("filter_add_in")!); await Click(Named("pick_stone")!);
        await Until(() => FilterEntries("filter_in", "filter_in_").SequenceEqual(["stone"]), "picking stone taking A off the input row", 2);
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == chest);
        await CloseWithEsc();
    }
}
