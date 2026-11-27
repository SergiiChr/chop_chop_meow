using System.Linq;
using System.Threading.Tasks;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private NetScene BarrelNetScene => new("barrels", null, HostSeesGuestPlaceLiftAndFillABarrel, GuestPlacesLiftsAndFillsABarrel);

    private async Task HostSeesGuestPlaceLiftAndFillABarrel(PlayerState guest) {
        Drop(guest, Water.Empty);
        Give(guest, Barrel.Item, 5);
        GiveVessel(Hosted, guest, Water.Empty, Water.Liquid, Water.Capacity);

        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel), "the guest placing the empty barrel");
        var empty = Hosted.Things.Single(t => t.Kind == ThingKind.Barrel);
        await Until(() => world.Find(empty.Id)?.Inventory[Water.Liquid] == 0 && empty.Inventory[Water.Liquid] == 0 && guest.Backpack[Barrel.Item] == 4,
            "the host's window showing the empty barrel the guest placed from their stack");
        Signal("barrel-placed");

        await Until(() => Hosted.Find(empty.Id) == null && world.Find(empty.Id) == null &&
            guest.Backpack.Layout.Any(s => s.Item == Barrel.Item && s.Count == 5), "the guest lifting the empty barrel back into its stack");
        Signal("barrel-lifted");

        await Until(() => Vessels.Count(guest.Backpack, Barrel.Item, Water.Liquid, 3) == 1 && guest.Backpack.Layout.Any(s => s.Item == Barrel.Item && s.Count == 4) &&
            Vessels.Count(guest.Backpack, Water.Empty, Water.Liquid, 3) == 0 && guest.Backpack[Water.Empty] == 1, "the host moving three liters into one barrel from the guest's stack");
        Signal("barrel-filled");

        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel && t.Inventory[Water.Liquid] == 2),
            "the guest placing the barrel's two remaining liters");
        var filled = Hosted.Things.Single(t => t.Kind == ThingKind.Barrel);
        Require(Vessels.Count(guest.Backpack, Water.Empty, Water.Liquid, 1) == 1 && guest.Backpack.CountOfKind(Water.Empty) == 1 &&
            guest.Backpack[Barrel.Item] == 4 && Barrel.FilledIn(guest.Backpack) == null,
            "The host must keep one liter in the guest's bottle, two in the placed barrel, and four empty barrels in the backpack.");
        await Until(() => world.Find(filled.Id)?.Inventory[Water.Liquid] == 2,
            "the host's window showing two liters in the guest's placed barrel");
    }

    private async Task GuestPlacesLiftsAndFillsABarrel() {
        await Until(() => world.Backpack.Layout.Any(s => s.Item == Barrel.Item && s.Count == 5) && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 3) == 1,
            "the guest receiving a stack of five empty barrels and a full bottle");
        await OpenEmptyBackpackBarrel();
        var spot = await PutDownBackpackBarrel(Barrel.Item, CanApproachBarrel);
        await Until(() => world.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == spot && t.Inventory[Water.Liquid] == 0) && world.Backpack[Barrel.Item] == 4,
            "one empty barrel from the guest's stack appearing on the ground");
        await Signaled("barrel-placed");

        var barrel = world.Things.Single(t => t.Kind == ThingKind.Barrel);
        await Until(() => view.Pick(view.Screen(spot, .2f))?.Id == barrel.Id && Uncovered(view.Screen(spot, .2f)),
            "the placed barrel being in reach of the pointer", 5);
        var point = RightPress(spot);
        try {
            await Until(() => view.Lifting == barrel.Id || world.Backpack[Barrel.Item] == 5, "the right press starting to lift the barrel", 5);
            await Until(() => world.Backpack.Layout.Any(s => s.Item == Barrel.Item && s.Count == 5) && !world.Things.Any(t => t.Kind == ThingKind.Barrel),
                "a held right click lifting the barrel back into its stack in the guest's backpack", 10);
        } finally {
            RightRelease(point);
        }
        await Signaled("barrel-lifted");

        await OpenEmptyBackpackBarrel();
        await Carry(BagCellOf(Vessels.First(world.Backpack, Water.Empty, Water.Liquid, Water.Capacity)!), Named("barrel_water")!);
        await Until(() => Vessels.Count(world.Backpack, Barrel.Item, Water.Liquid, 3) == 1 && world.Backpack.Layout.Any(s => s.Item == Barrel.Item && s.Count == 4) &&
            world.Backpack[Water.Empty] == 1 && cursor.Empty && Named("barrel_water")?.TooltipText.Contains("3/50 L") == true,
            "the guest filling the open barrel from their bottle and keeping four empty barrels in one stack");
        await Signaled("barrel-filled");
        await Click(Named("barrel_water")!);
        await Until(() => Vessels.Count(world.Backpack, Barrel.Item, Water.Liquid, 2) == 1 && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 1) == 1,
            "the guest drawing one liter back into the bottle");

        var filled = await PutDownBackpackBarrel(Vessels.First(world.Backpack, Barrel.Item, Water.Liquid, 2)!);
        await Until(() => world.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == filled && t.Inventory[Water.Liquid] == 2) &&
            world.Backpack[Barrel.Item] == 4 && Barrel.FilledIn(world.Backpack.Inventory) == null && Vessels.Count(world.Backpack, Water.Empty, Water.Liquid, 1) == 1,
            "the guest seeing two liters in the placed barrel, one in the bottle, and four empty barrels in the backpack");
        await CloseWithEsc();
    }
}
