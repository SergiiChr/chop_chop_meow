using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The network scene of firebugs: the host puts one out near the guest at night, the guest's window shows it there,
    /// and the guest's swing of the net, which falls short, sends it darting on the host.
    /// </summary>
    private NetScene FirebugNetScene => new("firebugs", null, HostSeesTheGuestsSwingScareAFirebug, GuestSwingsTheNetNearAFirebug);

    private async Task HostSeesTheGuestsSwingScareAFirebug(PlayerState guest) {
        Give(guest, "bug_net");
        await Signaled("net-in-hand");
        // By day firebugs are cleared away at once.
        RunUntilNight(Hosted); ShutDens(Hosted); KeepFirebugsIn(Hosted); SetNeeds(guest, 100, 100, 100);
        RemoveThings(Hosted, t => t.Kind == ThingKind.Firebug);
        // Past the 4 m a firebug darts from on its own, and within the 5 m a missed swing scares it from.
        var off = Enumerable.Range(0, 8).Select(i => Vector2.Right.Rotated(i * Mathf.Tau / 8) * 4.5f)
            .First(way => World.Open(guest.Pos + way) && Hosted.Players.All(p => p.Id == guest.Id || p.Pos.DistanceTo(guest.Pos + way) > 4.5f));
        var bug = Hosted.Add(ThingKind.Firebug, guest.Pos + off, 1);
        var placed = bug.Pos;
        await Frames(4);
        Require(bug.Pos == placed, "A firebug 4.5 m from the guest must stay where the host put it.");
        Signal("firebug-out", GD.VarToStr(placed));
        await Until(() => view.OtherAnimation(guest.Id) == ActorAction.NetSwing, "the host seeing the guest swing the net", 15);
        await Until(() => bug.Pos.DistanceTo(placed) > 3, "the guest's missed swing sending the host's firebug darting", 10);
        await Signaled("firebug-fled");
        RemoveThings(Hosted, t => t.Kind == ThingKind.Firebug); Drop(guest, "bug_net");
        RunUntilMorning(Hosted); NourishEveryone(Hosted);
        Signal("firebug-night-over");
    }

    private async Task GuestSwingsTheNetNearAFirebug() {
        await CloseWithEsc();
        await Until(() => world.Backpack["bug_net"] == 1, "the bug net the host gave the guest", 15);
        // A double click on a tool that is on no hotbar slot takes it in hand.
        await KeyStroke(Key.Tab);
        await DoubleClick(BagCellOf("bug_net"));
        await Until(() => world.InHand == "bug_net", "the host putting the bug net in the guest's hand", 10);
        await CloseWithEsc();
        Signal("net-in-hand");
        var placed = GD.StrToVar(await Signaled("firebug-out")).AsVector2();
        await Until(() => world.ThingsOf(ThingKind.Firebug).Count == 1 && world.ThingsOf(ThingKind.Firebug)[0].Pos.DistanceTo(placed) < .01f, "the host's firebug showing in the guest's world where the host put it", 15);
        var bug = world.ThingsOf(ThingKind.Firebug)[0];
        await Until(() => view.PropOf(bug.Id) is FirebugProp { Shown: true }, "the guest's window drawing the host's firebug", 5);
        await ClickGround();
        await Until(() => view.PlayerAnimation == ActorAction.NetSwing, "the guest's net swing playing", 5);
        await Until(() => world.Find(bug.Id) is Thing fled && fled.Pos.DistanceTo(placed) > 3, "the guest seeing the firebug dart off from its swing", 15);
        Signal("firebug-fled");
        await Signaled("firebug-night-over");
        await KeyStroke(Key.Key1);
    }
}
