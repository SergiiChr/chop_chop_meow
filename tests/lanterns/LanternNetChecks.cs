using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private NetScene LanternNetScene => new("lantern", null, HostSeesTheGuestsLantern, GuestPlacesAndFillsALantern);

    private async Task HostSeesTheGuestsLantern(PlayerState guest) {
        string empty = Hosted.Lanterns.NewEmpty();
        Give(guest, empty);
        GiveFirebugs(Hosted, guest, 3);
        await Until(() => Hosted.ThingsOf(ThingKind.Lantern).Any(t => Hosted.Lanterns.BugCount(t.LanternItem) == 3), "the guest filling the placed lantern");
        var placed = Hosted.ThingsOf(ThingKind.Lantern).Single(t => Hosted.Lanterns.BugCount(t.LanternItem) == 3);
        Require(guest.Backpack[Firebugs.Item] == 0 && Lanterns.Id(placed.LanternItem) == Lanterns.Id(empty),
            "the guest should use their own three firebugs to fill the same lantern");
        await Until(() => world.Find(placed.Id) is Thing seen && world.Lanterns.BugCount(seen.LanternItem) == 3,
            "the host's window showing the guest's three firebugs in the placed lantern");
        Signal("lantern-filled");
        await Until(() => Hosted.Find(placed.Id) == null && guest.Backpack.Items.Any(p => Hosted.Lanterns.BugCount(p.Key) == 3),
            "the guest lifting the lantern with its three firebugs");
        await Until(() => Lanterns.InHand(guest) is string held && Lanterns.Id(held) == Lanterns.Id(empty)
            && Hosted.Me.LanternInHand == 0, "only the guest taking their lantern in hand");
        Signal("lantern-held");
        await Until(() => guest.LanternInHand == 0, "the guest stowing their lantern");
    }

    private async Task GuestPlacesAndFillsALantern() {
        await CloseWithEsc();
        await Until(() => world.Backpack.Items.Any(p => Lanterns.IsItem(p.Key)) && world.Backpack[Firebugs.Item] >= 3,
            "the guest receiving an empty lantern and three firebugs");
        string item = world.Backpack.Items.Keys.First(Lanterns.IsItem);
        await KeyStroke(Key.Tab);
        await RightClick(BagCell(c => c.TooltipText.StartsWith(Lanterns.EmptyName, System.StringComparison.Ordinal)));
        await Click(FindButton(panel, "Put it down")!);
        var at = SpotsNearby().First(p => world.Player.DistanceTo(p) <= 3.2f && Uncovered(view.Screen(p)) &&
            world.Construction.PlacementBlock(ThingKind.Lantern, p, item) == null);
        await Click(view.Screen(at));
        await Until(() => world.ThingsOf(ThingKind.Lantern).Any(t => t.Pos == at && view.Pick(view.Screen(at, .25f))?.Id == t.Id),
            "the placed lantern becoming clickable in the guest's window");
        await RightClick(at, .25f); await ShowBackpack();
        await Carry(BagCellOf(Firebugs.Item), Named("lantern_firebugs")!);
        await Signaled("lantern-filled");
        await Until(() => world.Find(panel.Selection.Thing) is Thing t && world.Lanterns.BugCount(t.LanternItem) == 3,
            "the guest's window showing all three firebugs in the lantern");
        await Click(FindButton(panel, "Pick up")!);
        await Until(() => world.Lanterns.Carried(Lanterns.Id(item)) is string packed && world.Lanterns.BugCount(packed) == 3 && !panel.Visible,
            "the guest taking the filled lantern back into the backpack");
        string carried = world.Lanterns.Carried(Lanterns.Id(item))!;
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCellOf(carried));
        await Until(() => Lanterns.InHand(world.Me) == carried, "the guest seeing their lantern in hand");
        await Signaled("lantern-held");
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCellOf(carried));
        await Until(() => world.Me.LanternInHand == 0, "the guest seeing their lantern stowed");
    }
}
