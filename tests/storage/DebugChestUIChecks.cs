using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private Task RunDebugChestUIChecks() => TestClean(CheckDebugChestShowsANewSlotWhileOpen);

    private async Task CheckDebugChestShowsANewSlotWhileOpen() {
        var at = FreeSpotNearby(ThingKind.Chest);
        var chest = Hosted.Add(ThingKind.Chest, at);
        chest.Inventory.UnlimitedSlots = true;
        chest.Inventory.Add("wood", 320);
        int planksBefore = Hosted.Backpack["planks"];
        Require(Give(Hosted, "planks", 1) == 1, "one more plank should fit in the backpack");
        await Until(() => view.Pick(view.Screen(at, .3f))?.Id == chest.Id, "the debug chest being visible", 4);
        await Click(view.Screen(at, .3f));
        Require(panel.HeadingTitle.Text == "Debug chest", "the unlimited chest should have its own heading");
        await Carry(BagCellOf("planks"), OnPage("slot_5")!);
        await Until(() => chest.Inventory["planks"] == planksBefore + 1 && OnPage("slot_6") is ItemSlot { Visible: true },
            "dropping a stack in the last slot leaving another empty slot while the debug chest is open", 4);
        await CloseWithEsc();
        if (planksBefore > 0) Hosted.Transfer(chest.Inventory, Hosted.Backpack, "planks", planksBefore);
        RemoveThings(Hosted, t => t == chest);
        await Frames();
    }
}
