using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunBarrelUIChecks() {
        await TestClean(CheckBarrelOpensInTheBackpackAndCanBePlacedWithWater);
        await TestClean(CheckBackpackAndHotbarOpenTheirOwnBarrel);
        await TestClean(CheckCtrlPoursFromAHotbarBoundBucketIntoAPlacedBarrel);
        await TestClean(CheckHotbarShowsTheCountOfAnEmptyBucketStack);
    }

    /// <summary>Four empty barrels share a cell and open independently of the filled barrel beside them.</summary>
    private async Task CheckBackpackAndHotbarOpenTheirOwnBarrel() {
        const int slot = 8;
        string previous = world.HotbarItem(slot), filled = Barrel.AtFill(12);
        int previousStack = world.HotbarStack(slot);
        Give(Hosted, Barrel.Item, 4);
        Give(Hosted, filled);
        await Frames();

        await OpenBackpackBarrel(filled);
        await Carry(BagCellOf(Barrel.Item), hotbar.Buttons[slot]);
        await Until(() => world.HotbarCount(slot) == 4 && cursor.Empty, "four empty barrels bound to one hotbar slot", 2);
        Require(panel.Page == Page.Barrel && Named("barrel_water")?.TooltipText.Contains("12/50 L") == true,
            "Holding a stack of empty barrels must leave the filled barrel page open.");
        Require(hotbar.Counts[slot].Visible && hotbar.Counts[slot].Text == "4",
            $"A hotbar stack of four empty barrels must show 4, which showed {hotbar.Counts[slot].Text}.");

        await DoubleClick(hotbar.Buttons[slot]);
        await Until(() => panel.Page == Page.Barrel && Named("barrel_water")?.TooltipText.Contains("0/50 L") == true,
            "the hotbar opening an empty barrel while a filled one is in the backpack", 2);
        await OpenBackpackBarrel(filled);
        var spot = await PutDownBackpackBarrel(filled);
        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == spot && t.Inventory[Water.Liquid] == 12),
            "placing the selected filled barrel with its water", 2);
        Require(Hosted.Backpack[Barrel.Item] == 4, "Placing the filled barrel must leave all four empty barrels in the backpack.");

        await CloseWithEsc();
        RemoveThings(Hosted, t => t.Kind == ThingKind.Barrel && t.Pos == spot);
        Drop(Hosted, Barrel.Item, 4);
        if (previousStack >= 0) PutOnHotbar(Hosted, previousStack, slot);
        else SetHotbarSlot(Hosted, slot, previous);
        await Frames();
    }

    /// <summary>
    /// The barrel can be opened, filled and drawn from in the backpack.
    /// Its water and filter are visible after putting it down.
    /// </summary>
    private async Task CheckBarrelOpensInTheBackpackAndCanBePlacedWithWater() {
        int full = Drop(Hosted, Water.Full);
        int empty = Drop(Hosted, Water.Empty);
        int oneLiter = Drop(Hosted, Water.OneLiter);
        int twoLiters = Drop(Hosted, Water.TwoLiters);
        Give(Hosted, Barrel.Item);
        Give(Hosted, Water.Full);
        await OpenEmptyBackpackBarrel();
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(Water.Full))), Named("barrel_water")!);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("3/50 L") == true, "the barrel holding three liters after the bottle is poured", 2);

        await Click(Named("barrel_water")!);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("2/50 L") == true && world.Backpack[Water.OneLiter] == 1, "taking one liter of water from the barrel into the bottle", 2);
        var spot = await PutDownBackpackBarrel(Barrel.AtFill(2));
        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == spot), "putting the barrel down", 2);
        var barrel = Hosted.Things.Single(t => t.Kind == ThingKind.Barrel && t.Pos == spot);
        await ClickOn(barrel);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("2/50 L") == true, "the placed barrel showing its two liters", 2);
        Require(Named("job_haul") != null, "A placed barrel must offer a hauling job.");
        await Click(Named("filters")!);
        string waterFilter = $"filter_in_{Water.Liquid}";
        Require(OnPage("filter_out") == null && Named(waterFilter) != null && Named("filter_add_in") == null,
            "A barrel containing water must show one filter row naming water, with no add button.");

        await Holding(Key.Shift, () => Click(Named("barrel_water")!));
        await Until(() => Named("barrel_water")?.TooltipText.Contains("0/50 L") == true && world.Backpack[Water.Full] == 1, "drawing the remaining water into the bottle", 2);
        await Click(Named(waterFilter)!);
        await Until(() => Named("filter_add_in") != null, "removing the water filter from the empty barrel", 2);
        await Click(Named("filter_add_in")!);
        Require(OnPage("filter_out") == null && Named($"pick_{Water.Liquid}") != null && Named("pick_stone") == null,
            "An empty barrel must offer only water in its filter picker.");
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == barrel);
        Drop(Hosted, Water.Full, 1);
        Give(Hosted, Water.Full, full);
        Give(Hosted, Water.Empty, empty);
        Give(Hosted, Water.OneLiter, oneLiter);
        Give(Hosted, Water.TwoLiters, twoLiters);
        await Frames();
    }

    private async Task CheckCtrlPoursFromAHotbarBoundBucketIntoAPlacedBarrel() {
        const int slot = 8;
        string previous = world.HotbarItem(slot), filled = Bucket.AtFill(6);
        int previousStack = world.HotbarStack(slot);
        int emptyBuckets = Drop(Hosted, Bucket.Item);
        Give(Hosted, filled);
        PutOnHotbar(Hosted, SlotOf(Hosted.Backpack, filled), slot);
        var barrel = Hosted.Add(ThingKind.Barrel, SpotsNearby().First(p =>
            world.CanStand(p) && view.Pick(view.Screen(p, .4f)) == null && Uncovered(view.Screen(p, .4f))));
        await Frames();
        await ClickOn(barrel);
        await Until(() => Named("barrel_water") != null && world.HotbarItem(slot) == filled, "the barrel page and bound water bucket");

        await Holding(Key.Ctrl, () => Click(BagCell(cell => cell.TooltipText.StartsWith(world.Data.Name(filled)))));
        await Until(() => barrel.Inventory[Water.Liquid] == 6 && world.HotbarItem(slot) == Bucket.Item,
            "Ctrl pouring six liters and leaving the empty bucket bound to the hotbar", 2);

        await CloseWithEsc();
        RemoveThings(Hosted, t => t == barrel);
        Drop(Hosted, Bucket.Item, 1);
        Give(Hosted, Bucket.Item, emptyBuckets);
        if (previousStack >= 0) PutOnHotbar(Hosted, previousStack, slot);
        else SetHotbarSlot(Hosted, slot, previous);
        await Frames();
    }

    private async Task CheckHotbarShowsTheCountOfAnEmptyBucketStack() {
        const int slot = 8;
        string previousItem = world.HotbarItem(slot);
        int previousStack = world.HotbarStack(slot);
        int previousBuckets = Drop(Hosted, Bucket.Item);
        Give(Hosted, Bucket.Item, 5);
        await KeyStroke(Key.Tab);
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(Bucket.Item))), hotbar.Buttons[slot]);
        await Until(() => world.HotbarItem(slot) == Bucket.Item && world.HotbarCount(slot) == 5 && cursor.Empty, "five empty buckets bound to the hotbar", 2);
        await Frames();
        Require(hotbar.Counts[slot].Visible && hotbar.Counts[slot].Text == "5",
            $"Five empty buckets must show a count of 5 in their hotbar slot, which showed {hotbar.Counts[slot].Text}.");
        await CloseWithEsc();
        Drop(Hosted, Bucket.Item, 5);
        Give(Hosted, Bucket.Item, previousBuckets);
        if (previousStack >= 0) PutOnHotbar(Hosted, previousStack, slot);
        else SetHotbarSlot(Hosted, slot, previousItem);
        await Frames();
    }
}
