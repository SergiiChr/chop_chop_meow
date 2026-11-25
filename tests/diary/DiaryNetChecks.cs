using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The network scene of the diary: a page falls off a tree for the guest beside the host, the guest's window shows it and the host's does not,
    /// and the guest's click on it and Take put it in the guest's diary on the host.
    /// </summary>
    private NetScene DiaryNetScene => new("diary", null, HostSeesNoPageThatFellForTheGuest, GuestPicksUpAndTakesTheirPage);

    private async Task HostSeesNoPageThatFellForTheGuest(PlayerState guest) {
        var host = HostedMe;
        Warp(guest, host.Pos + Vector2.Right * 2);
        var spot = SpotsNearby().First(p => Hosted.CanStand(p) && p.DistanceTo(guest.Pos) > 1.5f);
        var page = DropPage(Hosted, guest, spot);
        Signal("diary-page-down", page.Id.ToString());
        await Signaled("diary-page-seen");
        Require(world.Find(page.Id) != null && view.PropOf(page.Id) is not { Shown: true }, "The host's window must not show a page that fell for the guest, though it lies in sight.");
        await Until(() => guest.DiaryFinds.Contains(DiaryFind.Tree) && guest.DiaryFinding == DiaryFind.None, "the guest picking up their page and taking it", 30);
        Require(!host.DiaryFinds.Contains(DiaryFind.Tree), "The guest's page must go into the guest's diary alone.");
        await Signaled("diary-taken");
        Signal("diary-over");
    }

    private async Task GuestPicksUpAndTakesTheirPage() {
        await CloseWithEsc();
        int id = int.Parse(await Signaled("diary-page-down"));
        await Until(() => world.Find(id) is Thing page && view.PropOf(page.Id) is PageProp { Shown: true }, "the guest's window showing the page that fell for them", 15);
        Signal("diary-page-seen");
        await ClickOn(world.Find(id)!);
        await Until(() => findPrompt.Visible && HasLabel(findPrompt, "A piece of paper fell off a tree."), "the guest walking over to the page and its pop-up coming up", 20);
        await Click((Button)findPrompt.FindChild("diary_take", true, false));
        await Until(() => sheet.Visible && sheet.Day == 36, "taking the page opening day 36 in the guest's diary", 5);
        await Until(() => hotbar.DiaryBadge.Visible && !findPrompt.Visible, "the host's answer showing the guest's diary badge", 15);
        await KeyStroke(Key.Escape); await CloseWithEsc();
        Signal("diary-taken");
        await Signaled("diary-over");
    }
}
