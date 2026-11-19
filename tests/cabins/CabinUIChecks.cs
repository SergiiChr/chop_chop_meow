using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>The UI checks of cabins: going in by a click, the page inside and stepping out.</summary>
    private async Task RunCabinUIChecks() {
        await TestClean(CheckAClickTakesAPlayerIntoACabinWhereTheyLockUpAndStepOutAgain);
    }

    /// <summary>
    /// A click takes a player into a cabin, walking or standing; inside they lock the door, see what a night's sleep would
    /// leave them and cannot walk out, and Step outside lets them out and unlocks the door.
    /// It is one visit to a cabin from the first click to the last step out, which loses its point when split.
    /// </summary>
    private async Task CheckAClickTakesAPlayerIntoACabinWhereTheyLockUpAndStepOutAgain() {
        var home = Hosted.Player;
        await CloseWithEsc();
        var hut = Hosted.Add(ThingKind.Hut, world.Player + new Vector2(0, 4));
        // The click lands while the player is still walking, as when running from a wolf.
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Seconds(.1);
        await Click(view.Screen(hut.Pos, 1.5f));
        await Until(() => world.Sheltered == hut.Id && Shows(hut), "a cabin clicked while walking taking the player inside and opening", 4);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        await Frames();
        await Until(() => view.PropOf(hut.Id) is HutProp { Open: 0 }, "the cabin's door closing behind the player once they are inside", 3);
        // The door's key shows under the door's button for a player inside; a click turns it, which locks the door.
        await Until(() => Named("key") is { Visible: true }, "the cabin page showing the door's key to a player inside", 2);
        await Click(Named("key")!);
        await Until(() => world.Find(hut.Id) is { Locked: true }, "a click on the key locking the cabin's door", 2);
        RunUntilNight(Hosted); SetNeeds(Hosted, hunger: 5);
        await Until(() => Named("sleep")?.TooltipText.Contains("too hungry") == true, "the sleep button explaining that the player is too hungry to sleep", 2);
        // Under "Stats after sleep" the page shows only what a night would leave, each capsule over its icon as in the corner; a fed night heals.
        SetNeeds(Hosted, 100, 100, 40);
        await Until(() => (panel.FindChild("health", true, false) as Control)?.TooltipText == $"Health at dawn {Hosted.HealthAtDawn():0}%" && Hosted.HealthAtDawn() > 40,
            "the cabin page showing the health a fed night's sleep would leave", 2);
        Require(HasLabel(panel, "Stats after sleep") && panel.FindChildren("*", "", true, false).OfType<NeedGauge>().Count() == 3,
            "The cabin page must title its needs \"Stats after sleep\" and show a heart, a stomach and a droplet under them.");
        SetNeeds(Hosted, hunger: 5);
        RunUntilMorning(Hosted);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = true });
        await Seconds(.2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = false });
        await Frames();
        Require(world.Sheltered == hut.Id, "Moving must not step the player out of the cabin.");
        await Click(FindButton(panel, "Step outside")!);
        Require(world.Sheltered == 0, "Step outside must leave the cabin.");
        await Until(() => world.Find(hut.Id) is { Locked: false } && Named("key") is { Visible: false }, "stepping out unlocking the door and hiding its key", 2);
        // With no key held the player stops where they stepped in, right by the door.
        await Click(view.Screen(hut.Pos, 1.5f));
        await Until(() => world.Sheltered == hut.Id, "a click on the cabin taking a standing player inside", 4);
        await Until(() => view.PropOf(hut.Id) is HutProp { Open: 0 }, "the cabin's door closing behind a player who stepped in from beside it", 3);
        // A click on the cabin from inside opens its page again, and stepping out then keeps the player out.
        await CloseWithEsc();
        await Click(view.Screen(hut.Pos, 1.5f));
        await Click(FindButton(panel, "Step outside")!);
        await Seconds(Doorways.LeaveSeconds + 1);
        Require(world.Sheltered == 0 && world.Door == Doorway.None, "A player who clicked the cabin they were in and stepped out must stay outside.");
        RemoveThings(Hosted, t => t == hut); await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }
}
