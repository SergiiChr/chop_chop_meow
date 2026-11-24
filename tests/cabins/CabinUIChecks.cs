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
        await TestClean(CheckCabinHasABarrelWindowAndOffersHaulWhileABarrelIsMounted);
        await TestClean(CheckEnterWalksAroundAStationAndCanBeCancelled);
        await TestClean(CheckUnreachableCabinDoorLeavesThePlayerFreeToWalk);
    }

    private async Task CheckEnterWalksAroundAStationAndCanBeCancelled() {
        var home = Hosted.Player;
        var cabin = Hosted.Add(ThingKind.Hut, home + new Vector2(0, -4));
        var bench = Hosted.Add(ThingKind.Bench, Doorways.DoorPoint(cabin) + Vector2.Down * 2);
        Warp(Hosted, Doorways.DoorPoint(cabin) + Vector2.Down * 6);
        await Frames(4);
        await ClickOn(cabin);
        var enter = FindButton(panel, "Enter")!;
        await Until(() => enter.GetThemeStylebox("normal") == Style.Pill(Style.Mustard).Item1, "highlighted Enter while walking to the cabin", 2);
        await Click(enter);
        Require(enter.GetThemeStylebox("normal") == Style.Pill(Style.Sage).Item1, "cancelling should clear the highlight");
        var stopped = world.Player;
        await Seconds(.3);
        Require(world.Player == stopped && world.Sheltered == 0, "clicking Enter again should stop the automatic walk");
        await Click(enter);
        await Until(() => world.Sheltered == cabin.Id && world.Door == Doorway.None, "walking around the bench and stepping inside", 7);
        await Until(() => FindButton(panel, "Step outside") != null, "the cabin panel showing Step outside", 2);
        await Click(FindButton(panel, "Step outside")!);
        await Until(() => world.Sheltered == 0 && world.Door == Doorway.None, "stepping out of the cabin", 4);
        RemoveThings(Hosted, thing => thing == cabin || thing == bench);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckUnreachableCabinDoorLeavesThePlayerFreeToWalk() {
        var home = Hosted.Player;
        var cabin = Hosted.Add(ThingKind.Hut, home + new Vector2(0, -4));
        // An old layout can have a building on the door spot that new construction keeps clear.
        var bench = Hosted.Add(ThingKind.Bench, Doorways.DoorPoint(cabin));
        await ClickOn(cabin);
        await Until(() => messages.Last == "No clear path to the cabin door.", "the blocked-door message", 2);
        await Until(() => FindButton(panel, "Enter")?.GetThemeStylebox("normal") == Style.Pill(Style.Sage).Item1,
            "the cabin panel clearing Enter's highlight after an unreachable door", 2);
        var stopped = world.Player;
        await Holding(Key.D, () => Seconds(.2));
        Require(world.Player.DistanceTo(stopped) > .2f && world.Sheltered == 0, "the player can walk away from an unreachable door");
        RemoveThings(Hosted, thing => thing == cabin || thing == bench);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckCabinHasABarrelWindowAndOffersHaulWhileABarrelIsMounted() {
        var home = Hosted.Player;
        var cabin = Hosted.Add(ThingKind.Hut, home + new Vector2(0, 4));
        string item = Barrel.AtFill(20);
        Require(Give(Hosted, item) == 1, "the player starts with a barrel to mount");
        await Until(() => world.Backpack[item] == 1, "the player carrying a barrel with twenty liters", 2);
        await ClickOn(cabin);
        await Until(() => Shows(cabin) && world.Sheltered == cabin.Id && world.Door == Doorway.None, "the cabin page after entering", 5);
        var slot = panel.Aside.FindChild("cabin_barrel", true, false) as ItemSlot;
        Require(panel.Aside.Visible && slot != null && HasLabel(panel.Aside, "Roof barrel") && HasLabel(panel.Aside, "Empty"),
            "The cabin must show an empty barrel slot in its own window.");
        Require(panel.Aside.GetGlobalRect().End.X <= panel.GetGlobalRect().Position.X, "The barrel window must stand left of the cabin page.");
        Require(Named("job_haul") == null, "A cabin without a barrel must not offer Haul.");

        await Carry(BagCell(cell => cell.TooltipText.StartsWith(world.Data.Name(item))), slot!);
        await Until(() => cursor.Empty && world.Find(cabin.Id)?.Inventory[item] == 1 && Named("job_haul") is { Visible: true },
            "the cabin offering Haul as soon as the barrel goes in", 3);
        Require(HasLabel(panel.Aside, "Steel barrel") && HasLabel(panel.Aside, "20/50 L"),
            "The barrel window must name the mounted barrel and show its fill.");
        Require(HasLabel(panel, "Water is refilled from the roof barrel."), "The sleep forecast should name its mounted roof barrel.");
        Require(OnPage("thirst")?.TooltipText == "Thirst at dawn 100%",
            "A cabin barrel with plenty of water must show a full thirst gauge at dawn.");

        await Holding(Key.Shift, () => Click((ItemSlot)panel.Aside.FindChild("cabin_barrel", true, false)!));
        await Until(() => world.Backpack[item] == 1 && Named("job_haul") == null && HasLabel(panel.Aside, "Empty"),
            "taking back the barrel, hiding Haul and leaving the slot empty", 3);
        Require(!HasLabel(panel, "Water is refilled"), "Removing the roof barrel should also remove the sleep refill note.");
        Drop(Hosted, item);
        await Click(FindButton(panel, "Step outside")!);
        await Until(() => world.Sheltered == 0 && world.Door == Doorway.None, "stepping out of the cabin", 4);
        RemoveThings(Hosted, thing => thing == cabin);
        await CloseWithEsc();
        Require(!panel.Aside.Visible, "The barrel window must close with the cabin page.");
        Warp(Hosted, home); await Frames(4);
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
        await Until(() => world.Door == Doorway.Entering && world.Me.DoorTime > .12f, "stepping up to the cabin door", 4);
        CheckBodyAtDoor(hut, "entering");
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
        Require(!HasLabel(panel, "Water is refilled"), "A cabin without a roof barrel should omit the sleep refill note.");
        SetNeeds(Hosted, hunger: 5);
        RunUntilMorning(Hosted);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = true });
        await Seconds(.2);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.S, Keycode = Key.S, Pressed = false });
        await Frames();
        Require(world.Sheltered == hut.Id, "Moving must not step the player out of the cabin.");
        await Click(FindButton(panel, "Step outside")!);
        Require(world.Sheltered == 0, "Step outside must leave the cabin.");
        await Until(() => world.Door == Doorway.Leaving && world.Me.DoorTime > .12f, "stepping out of the cabin door", 2);
        CheckBodyAtDoor(hut, "leaving");
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

    private void CheckBodyAtDoor(Thing cabin, string action) {
        var root = view.RigOf(world.Me.Id)!.Root;
        var feet = new Vector2(root.Position.X, root.Position.Z);
        Require(root.Visible && feet.DistanceTo(Doorways.DoorPoint(cabin)) < 1.4f,
            $"the player model should stay visible at the doorway while {action}, feet at {feet}");
    }
}
