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
        await TestClean(CheckReorderingBackpackClosesTheBarrel);
        await TestClean(CheckEmptyBarrelCanBeFilteredForJam);
    }

    private async Task CheckReorderingBackpackClosesTheBarrel() {
        string filled = GiveVessel(Hosted, Barrel.Item, Water.Liquid, 12);
        Give(Hosted, Barrel.Item, 4);
        await Frames();
        await OpenBackpackBarrel(filled);
        await Click((Control)bag.FindChild("sort_bag", true, false));
        await Until(() => panel.Page == Page.Backpack && SlotOf(world.Backpack, Barrel.Item) < SlotOf(world.Backpack, filled),
            "sorting puts the empty barrels first and returns to inventory", 2);

        await OpenBackpackBarrel(filled);
        await Carry(BagCellOf(Barrel.Item), BagCellOf(filled));
        await Until(() => panel.Page == Page.Backpack && SlotOf(world.Backpack, filled) < SlotOf(world.Backpack, Barrel.Item),
            "swapping the opened barrel with the empty stack returns to inventory", 2);
        Require(world.Backpack[Barrel.Item] == 4 && world.Backpack[filled] == 1,
            "sorting and swapping preserve the barrels and their water");
        await CloseWithEsc();
        Drop(Hosted, Barrel.Item, 4);
        Drop(Hosted, filled);
        await Frames();
    }

    /// <summary>Four empty barrels share a cell and open independently of the filled barrel beside them.</summary>
    private async Task CheckBackpackAndHotbarOpenTheirOwnBarrel() {
        const int slot = 8;
        string previous = world.HotbarItem(slot);
        int previousStack = world.HotbarStack(slot);
        Give(Hosted, Barrel.Item, 4);
        string filled = GiveVessel(Hosted, Barrel.Item, Water.Liquid, 12);
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
        int full = DropVessels(Hosted, Water.Empty, Water.Liquid, 3);
        int empty = Drop(Hosted, Water.Empty);
        int oneLiter = DropVessels(Hosted, Water.Empty, Water.Liquid, 1);
        int twoLiters = DropVessels(Hosted, Water.Empty, Water.Liquid, 2);
        Give(Hosted, Barrel.Item);
        string bottle = GiveWater(Hosted);
        await OpenEmptyBackpackBarrel();
        await Carry(BagCellOf(bottle), Named("barrel_water")!);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("3/50 L") == true, "the barrel holding three liters after the bottle is poured", 2);

        await Click(Named("barrel_water")!);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("2/50 L") == true && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 1) == 1, "taking one liter of water from the barrel into the bottle", 2);
        var spot = await PutDownBackpackBarrel(Vessels.First(world.Backpack, Barrel.Item, Water.Liquid, 2)!, turn: Key.C);
        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == spot), "putting the barrel down", 2);
        var barrel = Hosted.Things.Single(t => t.Kind == ThingKind.Barrel && t.Pos == spot);
        Require(barrel.Rotation == 3 * Mathf.Pi / 2, "Putting the filled barrel down should turn its spout as chosen.");
        await ClickOn(barrel);
        await Until(() => Named("barrel_water")?.TooltipText.Contains("2/50 L") == true, "the placed barrel showing its two liters", 2);
        Require(Named("job_haul") != null, "A placed barrel must offer a hauling job.");
        await Click(Named("filters")!);
        string waterFilter = $"filter_in_{Water.Liquid}";
        Require(OnPage("filter_out") == null && Named(waterFilter) != null && Named("filter_add_in") == null,
            "A barrel containing water must show one filter row naming water, with no add button.");

        await Holding(Key.Shift, () => Click(Named("barrel_water")!));
        await Until(() => Named("barrel_water")?.TooltipText.Contains("0/50 L") == true && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1, "drawing the remaining water into the bottle", 2);
        await Click(Named(waterFilter)!);
        await Until(() => Named("filter_add_in") != null, "removing the water filter from the empty barrel", 2);
        await Click(Named("filter_add_in")!);
        Require(OnPage("filter_out") == null && Named($"pick_{Water.Liquid}") != null && Named($"pick_{BerryJam.Liquid}") != null
            && Named("pick_stone") == null, "An empty barrel must offer water and jam, but no solids.");
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == barrel);
        DropVessels(Hosted, Water.Empty, Water.Liquid, 3);
        GiveVessel(Hosted, Water.Empty, Water.Liquid, 3, full);
        Give(Hosted, Water.Empty, empty);
        GiveVessel(Hosted, Water.Empty, Water.Liquid, 1, oneLiter);
        GiveVessel(Hosted, Water.Empty, Water.Liquid, 2, twoLiters);
        await Frames();
    }

    private async Task CheckEmptyBarrelCanBeFilteredForJam() {
        var barrel = Hosted.Add(ThingKind.Barrel, SpotsNearby().First(p =>
            world.CanStand(p) && view.Pick(view.Screen(p, .4f)) == null && Uncovered(view.Screen(p, .4f))));
        string jam = GiveVessel(Hosted, Water.Empty, BerryJam.Liquid, 3);
        await Frames();
        await ClickOn(barrel);
        await Until(() => Named("barrel_water") != null, "the empty barrel opening");
        await Click(Named("filters")!);
        await Click(Named("filter_add_in")!);
        Require(Named($"pick_{BerryJam.Liquid}") != null, "An empty barrel's picker should offer jam.");
        await Click(Named($"pick_{BerryJam.Liquid}")!);
        var jamFilter = $"filter_in_{BerryJam.Liquid}";
        await Until(() => Named(jamFilter) != null, "the selected jam filter appearing", 2);
        await Click(Named(jamFilter)!);
        await Until(() => Named("filter_add_in") != null, "the cleared filter accepting a held item", 2);
        await Carry(BagCellOf(jam), Named("filter_add_in")!);
        await Until(() => Named(jamFilter) != null, "the jam bottle setting the jam filter", 2);
        Require(barrel.Inventory.Count == 0 && Vessels.Count(world.Backpack, Water.Empty, BerryJam.Liquid, 3) == 1,
            "Setting the filter must keep the bottle and jam in the backpack.");
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == barrel);
        Drop(Hosted, jam);
        await Frames();
    }

    private async Task CheckCtrlPoursFromAHotbarBoundBucketIntoAPlacedBarrel() {
        const int slot = 8;
        string previous = world.HotbarItem(slot);
        int previousStack = world.HotbarStack(slot);
        int emptyBuckets = Drop(Hosted, Bucket.Item);
        string filled = GiveVessel(Hosted, Bucket.Item, Water.Liquid, 6);
        PutOnHotbar(Hosted, SlotOf(Hosted.Backpack, filled), slot);
        var barrel = Hosted.Add(ThingKind.Barrel, SpotsNearby().First(p =>
            world.CanStand(p) && view.Pick(view.Screen(p, .4f)) == null && Uncovered(view.Screen(p, .4f))));
        await Frames();
        await ClickOn(barrel);
        await Until(() => Named("barrel_water") != null && world.HotbarItem(slot) == filled, "the barrel page and bound water bucket");

        await Holding(Key.Ctrl, () => Click(BagCellOf(filled)));
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
