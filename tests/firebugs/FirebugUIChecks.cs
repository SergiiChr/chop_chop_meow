using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunFirebugUIChecks() {
        await TestClean(CheckAClickWithTheBugNetSelectedSwingsIt);
        await TestClean(CheckAClickOnAFirebugSwingsTheNetAtItInsteadOfSelectingIt);
    }

    private async Task CheckAClickWithTheBugNetSelectedSwingsIt() {
        await CloseWithEsc();
        string before = world.HotbarItem(8);
        Give(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, "bug_net");
        await KeyStroke(Key.Key9);
        await ClickGround();
        await Until(() => view.PlayerAnimation == ActorAction.NetSwing, "the net's swing playing after a click on the ground", 2);
        await Until(() => view.PlayerAnimation != ActorAction.NetSwing, "the end of the net's swing", 2);
        Drop(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, before);
        await KeyStroke(Key.Key1);
        await Frames(4);
    }

    /// <summary>A firebug 4.5 m off is past where it darts from the player by itself and within where a missed swing scares it.</summary>
    private async Task CheckAClickOnAFirebugSwingsTheNetAtItInsteadOfSelectingIt() {
        await CloseWithEsc();
        string before = world.HotbarItem(8);
        RunUntilNight(Hosted); ShutDens(Hosted); NourishEveryone(Hosted);
        var bug = Hosted.Add(ThingKind.Firebug, world.Player + PlayerControl.KeyDirection(1, 0) * 4.5f, 1);
        var placed = bug.Pos;
        await Until(() => view.PropOf(bug.Id) is FirebugProp { Shown: true }, "the firebug showing in the window", 5);
        await Click(view.Screen(Hosted.Firebugs.At(bug), FirebugShape.Height));
        Require(panel.Visible && panel.Selection.Thing == bug.Id, "With no bug net selected a click on a firebug must open its page.");
        await CloseWithEsc();
        Require(!panel.Visible, "Escape must close the firebug's page.");
        Give(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, "bug_net");
        await KeyStroke(Key.Key9);
        await Click(view.Screen(Hosted.Firebugs.At(bug), FirebugShape.Height));
        await Until(() => view.PlayerAnimation == ActorAction.NetSwing, "the net's swing playing after a click on the firebug", 2);
        Require(!panel.Visible, "With a bug net selected a click on a firebug must swing the net, not open the firebug's page.");
        await Until(() => bug.Pos.DistanceTo(placed) > 3, "the swing that fell short sending the firebug darting", 5);
        RemoveThings(Hosted, t => t == bug); Drop(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, before);
        RunUntilMorning(Hosted); NourishEveryone(Hosted);
        await KeyStroke(Key.Key1);
        await CloseWithEsc(); await Frames(4);
    }
}
