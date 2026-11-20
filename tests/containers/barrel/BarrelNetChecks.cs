using System.Linq;
using System.Threading.Tasks;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private NetScene BarrelNetScene => new("barrels", null, HostSeesGuestPlaceLiftAndFillABarrel, GuestPlacesLiftsAndFillsABarrel);

    private async Task HostSeesGuestPlaceLiftAndFillABarrel(PlayerState guest) {
        Drop(guest, Water.Empty);
        Give(guest, Barrel.Item);
        Give(guest, Water.Full);

        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel), "the guest placing the empty barrel");
        var empty = Hosted.Things.Single(t => t.Kind == ThingKind.Barrel);
        await Until(() => world.Find(empty.Id)?.Inventory[Water.Liquid] == 0 && empty.Inventory[Water.Liquid] == 0 && guest.Backpack[Barrel.Item] == 0,
            "the host's window showing the guest's empty barrel on the ground");
        Signal("barrel-placed");

        await Until(() => Hosted.Find(empty.Id) == null && world.Find(empty.Id) == null && guest.Backpack[Barrel.Item] == 1,
            "the guest lifting the empty barrel and it disappearing for the host");
        Signal("barrel-lifted");

        await Until(() => guest.Backpack[Barrel.AtFill(3)] == 1 && guest.Backpack[Water.Full] == 0 && guest.Backpack[Water.Empty] == 1,
            "the host moving all three liters from the guest's bottle into their portable barrel");
        Signal("barrel-filled");

        await Until(() => Hosted.Things.Any(t => t.Kind == ThingKind.Barrel && t.Inventory[Water.Liquid] == 2),
            "the guest placing the barrel's two remaining liters");
        var filled = Hosted.Things.Single(t => t.Kind == ThingKind.Barrel);
        Require(guest.Backpack[Water.OneLiter] == 1 && Water.BottleItems.Sum(item => guest.Backpack[item]) == 1 && Barrel.In(guest.Backpack) == null,
            "The host must keep one liter in the guest's bottle and the other two only in the placed barrel.");
        await Until(() => world.Find(filled.Id)?.Inventory[Water.Liquid] == 2,
            "the host's window showing two liters in the guest's placed barrel");
    }

    private async Task GuestPlacesLiftsAndFillsABarrel() {
        await Until(() => world.Backpack[Barrel.Item] == 1 && world.Backpack[Water.Full] == 1, "the guest receiving an empty barrel and a full bottle");
        await OpenEmptyBackpackBarrel();
        var spot = await PutDownBackpackBarrel(Barrel.Item, CanApproachBarrel);
        await Until(() => world.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == spot && t.Inventory[Water.Liquid] == 0) && world.Backpack[Barrel.Item] == 0,
            "the guest's empty barrel leaving the backpack and appearing on the ground");
        await Signaled("barrel-placed");

        var barrel = world.Things.Single(t => t.Kind == ThingKind.Barrel);
        await Until(() => view.Pick(view.Screen(spot, .2f))?.Id == barrel.Id && Uncovered(view.Screen(spot, .2f)),
            "the placed barrel being in reach of the pointer", 5);
        var point = RightPress(spot);
        try {
            await Until(() => view.Lifting == barrel.Id || world.Backpack[Barrel.Item] == 1, "the right press starting to lift the barrel", 5);
            await Until(() => world.Backpack[Barrel.Item] == 1 && !world.Things.Any(t => t.Kind == ThingKind.Barrel),
                "a held right click lifting the barrel back into the guest's backpack", 10);
        } finally {
            RightRelease(point);
        }
        await Signaled("barrel-lifted");

        await OpenEmptyBackpackBarrel();
        await Carry(BagCell(c => c.TooltipText.StartsWith(world.Data.Name(Water.Full))), Named("barrel_water")!);
        await Until(() => world.Backpack[Barrel.AtFill(3)] == 1 && world.Backpack[Water.Empty] == 1 && cursor.Empty,
            "the guest's full bottle pouring three liters into the portable barrel");
        await Signaled("barrel-filled");
        await Click(Named("barrel_water")!);
        await Until(() => world.Backpack[Barrel.AtFill(2)] == 1 && world.Backpack[Water.OneLiter] == 1,
            "the guest drawing one liter back into the bottle");

        var filled = await PutDownBackpackBarrel(Barrel.AtFill(2));
        await Until(() => world.Things.Any(t => t.Kind == ThingKind.Barrel && t.Pos == filled && t.Inventory[Water.Liquid] == 2) &&
            world.Backpack[Barrel.AtFill(2)] == 0 && world.Backpack[Water.OneLiter] == 1,
            "the guest seeing the remaining two liters in the placed barrel and one liter in the bottle");
        await CloseWithEsc();
    }
}
