using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunFirebugUIChecks() {
        await TestClean(CheckARightClickWithTheBugNetSelectedSwingsIt);
        await TestClean(CheckARightClickOnAFirebugSwingsTheNetAtIt);
        await TestClean(CheckAShovelDigsTheGroundAFirebugHoversOver);
        await TestClean(CheckFirebugStackShowsAverageRemainingLife);
    }

    private async Task CheckFirebugStackShowsAverageRemainingLife() {
        float caught = Hosted.Time;
        GiveFirebugs(Hosted, 2);
        RunUntil(Hosted, caught + 840);
        GiveFirebugs(Hosted, 2);
        await KeyStroke(Key.Tab);
        await Until(() => world.Backpack[Firebugs.Item] == 4 && bag.Visible, "the backpack showing four caught firebugs", 3);
        string hint = BagCellOf(Firebugs.Item).TooltipText;
        Require(hint.Contains("Average life left: 60 h 0 min") || hint.Contains("Average life left: 59 h 59 min"),
            $"two firebugs with 48 hours left and two with 72 should average 60 hours, got {hint}");
        await CloseWithEsc();
        Drop(Hosted, Firebugs.Item); await Frames();
    }

    private async Task CheckARightClickWithTheBugNetSelectedSwingsIt() {
        await CloseWithEsc();
        string before = world.HotbarItem(8);
        Give(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, "bug_net");
        await KeyStroke(Key.Key9);
        await ClickGround(MouseButton.Right);
        await Until(() => view.PlayerAnimation == ActorAction.NetSwing, "the net's swing playing after a right click on the ground", 2);
        await Until(() => view.PlayerAnimation != ActorAction.NetSwing, "the end of the net's swing", 2);
        Drop(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, before);
        await KeyStroke(Key.Key1);
        await Frames(4);
    }

    /// <summary>A firebug 4.5 m off is past where it darts from the player by itself and within where a missed swing scares it.</summary>
    private async Task CheckARightClickOnAFirebugSwingsTheNetAtIt() {
        await CloseWithEsc();
        string before = world.HotbarItem(8);
        RunUntilNight(Hosted); ShutDens(Hosted); NourishEveryone(Hosted);
        var bug = Hosted.Add(ThingKind.Firebug, world.Player + PlayerControl.KeyDirection(1, 0) * 4.5f, 1);
        var placed = bug.Pos;
        await Until(() => view.PropOf(bug.Id) is FirebugProp { Shown: true }, "the firebug showing in the window", 5);
        await Click(view.Screen(Hosted.Firebugs.At(bug), FirebugShape.Height));
        Require(panel.Visible && panel.Selection.Thing == bug.Id, "A left click on a firebug must open its page.");
        await CloseWithEsc();
        Give(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, "bug_net");
        await KeyStroke(Key.Key9);
        var stood = world.Player;
        await RightClick(Hosted.Firebugs.At(bug), FirebugShape.Height);
        await Until(() => view.PlayerAnimation == ActorAction.NetSwing, "the net's swing playing after a right click on the firebug", 2);
        await Until(() => bug.Pos.DistanceTo(placed) > 3, "the swing that fell short sending the firebug darting", 5);
        Require(world.Player.DistanceTo(stood) < .01f && !panel.Visible, "A right click on a firebug with the net selected must swing where the player stands, not walk to the firebug or open its page.");
        RemoveThings(Hosted, t => t == bug); Drop(Hosted, "bug_net"); SetHotbarSlot(Hosted, 8, before);
        RunUntilMorning(Hosted); NourishEveryone(Hosted);
        await KeyStroke(Key.Key1);
        await CloseWithEsc(); await Frames(4);
    }

    /// <summary>A firebug 6 m off is past where it darts from the player, and hovers between the pointer and the spot to dig.</summary>
    private async Task CheckAShovelDigsTheGroundAFirebugHoversOver() {
        await CloseWithEsc();
        string before = world.HotbarItem(8);
        var home = Hosted.Player;
        RunUntilNight(Hosted); ShutDens(Hosted); NourishEveryone(Hosted);
        Give(Hosted, "steel_shovel"); SetHotbarSlot(Hosted, 8, "steel_shovel");
        await KeyStroke(Key.Key9);
        var at = Enumerable.Range(0, 8).Select(i => (world.Player + Vector2.Right.Rotated(i * Mathf.Tau / 8) * 6).Round())
            .First(p => world.Construction.PlacementBlock(ThingKind.Hole, p) == null && Uncovered(view.Screen(p)));
        var bug = Hosted.Add(ThingKind.Firebug, view.ToWorld(view.Screen(at), FirebugShape.Height), 1);
        await Until(() => view.PropOf(bug.Id) is FirebugProp { Shown: true } && view.Pick(view.Screen(at))?.Id == bug.Id, "the firebug hovering under the pointer", 5);
        var point = RightPress(at, 0);
        try {
            await Until(() => view.PlayerAnimation == ActorAction.Dig, "the shovel digging the ground a firebug hovers over", 8);
        } finally { RightRelease(point); }
        await Until(() => view.PlayerAnimation != ActorAction.Dig, "the shovel stopping when released", 3);
        Warp(Hosted, home);
        RemoveThings(Hosted, t => t == bug); Drop(Hosted, "steel_shovel"); SetHotbarSlot(Hosted, 8, before);
        RunUntilMorning(Hosted); NourishEveryone(Hosted);
        await KeyStroke(Key.Key1);
        await CloseWithEsc(); await Frames(4);
    }
}
