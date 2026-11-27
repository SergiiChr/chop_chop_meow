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
    /// <summary>Chest stacks move by deposit, carrying and halving, filter rows take and release items, and the backpack drawer closes on its own.</summary>
    private async Task CheckChestMovesSplitsAndFiltersStacks(Thing chest) {
        await CloseWithEsc(); await ClickOn(chest!);
        var deposit = Named("deposit"); Require(deposit != null, "Chest context must expose deposit action.");
        await Click(deposit!);
        Require(chest!.Inventory["wood"] > 0 && world.Backpack["steel_axe"] == 1, "Manual deposit must move materials and retain equipped tools.");
        int storedWood = chest.Inventory["wood"];
        await Carry(Cell(0, c => c.TooltipText.StartsWith("Wood")), Cell(1, c => c.TooltipText == ""));
        await Until(() => chest.Inventory["wood"] == 0 && world.Backpack["wood"] == storedWood && cursor.Empty, "a held chest stack put on the backpack moving there", 2);
        await Carry(await Showing(1, c => c.TooltipText.StartsWith("Wood")), hotbar.Buttons[3]);
        await Until(() => world.HotbarItem(3) == "wood" && cursor.Empty, "a held stack put on the hotbar assigning it", 2);
        Require(world.Backpack["wood"] == storedWood, "A held stack put on the hotbar must assign a reference without moving items.");
        await Carry(Cell(1, c => c.TooltipText.StartsWith("Wood")), Cell(0, c => c.TooltipText == ""));
        await Until(() => chest.Inventory["wood"] == storedWood, "a held backpack stack put on the chest storing it", 2);
        await SplitTo(await Showing(0, c => c.TooltipText.StartsWith("Wood")), Cell(1, c => c.TooltipText == ""));
        int half = (Math.Min(storedWood, world.Backpack.StackOf("wood")) + 1) / 2;
        await Until(() => world.Backpack["wood"] == half && chest.Inventory["wood"] == storedWood - half, $"a right click picking up half a stack and a left click putting it down ({world.Backpack["wood"]} of {half}, chest {chest.Inventory["wood"]} of {storedWood})", 2);
        await Carry(await Showing(1, c => c.TooltipText.StartsWith("Wood")), Cell(0, c => c.TooltipText == ""));
        await Until(() => chest.Inventory["wood"] == storedWood, "the half going back into the chest", 2);
        float plainHeight = panel.Size.Y;
        await Click(Named("filters")!);
        Require(Named("filters_close") != null, "Chest filters must open the filter window.");
        Require(ShowsChild(panel, "side_board"), "Chest filters must show the sheet beside the panel.");
        Require(panel.FindChild("filter_in", true, false) != null, "The filter window must show an input filter.");
        Require(panel.FindChild("filter_out", true, false) != null, "The filter window must show an output filter.");
        await Carry(Cell(0, c => c.TooltipText.StartsWith("Wood")), (Control)panel.FindChild("filter_add_in", true, false));
        await Until(() => chest.Inventory.InputFilter.Contains("wood") && cursor.Empty, "a held stack put on a filter row adding it to that filter", 2);
        Require(chest.Inventory["wood"] == storedWood, "A held stack put on a filter row must not move.");
        await Click(Named("filter_in_wood")!);
        Require(!chest.Inventory.InputFilter.Contains("wood"), "Clicking an item in a filter row must take it off.");
        await Click((Control)bag.FindChild("bag_close", true, false));
        await Click(Named("filters")!);
        Require(!bag.Visible && panel.Visible, "The backpack drawer's close button must hide it while the building's page stays open and refreshes.");
        await Click(Named("filters")!);
        await Click(Named("filters_close")!);
        Require(Named("filters_close") == null && panel.Visible, "The filter window's close button must close it and leave the panel open.");
        await Until(() => panel.Size.Y == plainHeight, "the panel shrinking back once the taller filter window closed", 2);
        await KeyStroke(Key.Tab);
        Require(panel.Visible && bag.Visible, "Tab must show the backpack again under the chest's page.");
        await CloseWithEsc();
        await KeyStroke(Key.Tab);
        Require(!panel.Visible, "Tab must not open the side panel.");
        Require(bag.Visible, "Tab must open the backpack drawer.");
        Require(FindButton(panel, "Assign hotbar slots") == null,
            "The side panel must have no separate hotbar assignment.");
        await Click((Control)bag.FindChild("bag_close", true, false));
        Require(!bag.Visible && !panel.Visible, "On the backpack page the drawer's close button must close the page.");
        await KeyStroke(Key.Tab);
        Require(!panel.Visible && bag.Visible, "Tab must open the backpack again after it was closed.");
        await KeyStroke(Key.B);
        int carriedBeforeAssignment = world.Backpack.Count;
        await Carry(Named("build_hut")!, hotbar.Buttons[0]);
        await Until(() => world.HotbarItem(0) == "hut" && cursor.Empty, "a plan put on the hotbar assigning it", 2);
        Require(world.Backpack.Count == carriedBeforeAssignment, "A plan put on the hotbar must store a plan reference without creating items.");
    }
}
