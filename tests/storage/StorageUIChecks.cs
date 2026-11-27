using System.Linq;
using System.Threading.Tasks;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of storage filters: the Everything entry, the cell marked A.</summary>
    private async Task RunStorageUIChecks() {
        await TestClean(CheckEverythingComesFirstInTheItemListAndStandsAloneInItsRow);
        await TestClean(CheckPileFilterNamesOneResource);
        await TestClean(CheckItemListRunsByGroupSimplestFirst);
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

    /// <summary>An empty pile's filter window has one row naming one resource: a bottle there is one entry for every fill, shows in the heading, and comes off with a click.</summary>
    private async Task CheckPileFilterNamesOneResource() {
        var pile = await OpenFilters(ThingKind.Pile);
        Require(OnPage("filter_in") != null && OnPage("filter_out") == null, "A pile's filter must show only the row of what workers bring.");
        await Click(Named("filter_add_in")!);
        Require(Named($"pick_{Water.Liquid}") == null && Named($"pick_{Water.Full}") == null && Named($"pick_{Water.Empty}") != null,
            "A pile's filter must offer bottles at any fill as one entry, with no entry for water or full bottles.");
        Require(Named($"pick_{World.MixedCrate}") != null, "A pile's filter must offer crates of mixed goods.");
        await Click(Named($"pick_{Water.Empty}")!);
        await Until(() => pile.Inventory.InputFilter.SequenceEqual(new[] { Water.Empty }) && ShownPage() == "Pile · empty bottle",
            "picking bottles making the empty pile a bottle pile, named in its heading", 2);
        Require(Named("filter_add_in") == null, "A pile whose filter names a resource must offer no second one.");
        await Click(Named($"filter_in_{Water.Empty}")!);
        await Until(() => pile.Inventory.InputFilter.Count == 0 && ShownPage() == "Pile" && Named("filter_add_in") != null, "clicking bottles taking them off the empty pile", 2);
        Require(FindButton(panel, "Clear") != null, "The filter window's clear button must read Clear.");
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == pile);
        await CloseWithEsc();
    }

    /// <summary>A chest's item list runs by group, each simplest first: what is gathered, grown or bought, materials, tools, containers, food, then crates of goods.</summary>
    private async Task CheckItemListRunsByGroupSimplestFirst() {
        var chest = await OpenFilters(ThingKind.Chest);
        await Click(Named("filter_add_in")!);
        var listed = FilterEntries("item_list", "pick_").ToList();
        string[] expected = ["wood", "planks", "iron_bar", "steel_bar", "steel_parts", "stone_chisel", "steel_chisel", Water.Empty, Bucket.Item, "flour", "meat_stew", "dough", "canned_meat_stew", "bread", World.CrateOf("berries"), World.CrateOf("bread")];
        Require(listed.Where(expected.Contains).SequenceEqual(expected), $"The item list must run by group, simplest first, not {string.Join(" ", listed)}.");
        Require(listed.SkipWhile(id => !id.StartsWith(World.PackedPrefix)).All(id => id.StartsWith(World.PackedPrefix)), "The item list must show crates of goods after every item.");
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == chest);
        await CloseWithEsc();
    }
}
