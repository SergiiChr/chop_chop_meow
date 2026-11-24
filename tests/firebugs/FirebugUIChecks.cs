using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunFirebugUIChecks() {
        await TestClean(CheckAClickWithTheBugNetSelectedSwingsIt);
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
}
