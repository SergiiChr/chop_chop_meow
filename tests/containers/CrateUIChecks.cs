using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private Task RunCrateUIChecks() => TestClean(CheckStackLargerThanCrateFillsItAndRestStaysHeld);

    /// <summary>Wood left on the cursor after filling one crate can be put into another after walking to it.</summary>
    private async Task CheckStackLargerThanCrateFillsItAndRestStaysHeld() {
        var home = Hosted.Player;
        Drop(Hosted, "wood", world.Backpack["wood"]);
        Require(Give(Hosted, "wood", 64) == 64, "The backpack must have room for a stack of wood.");
        var crates = new[] { Hosted.Add(ThingKind.Crate, world.Player + new Vector2(-1, 0)), Hosted.Add(ThingKind.Crate, world.Player + new Vector2(5, -5)) };
        int cap = world.Data.CrateCapacity;
        await ClickOn(crates[0]);
        await Carry(Cell(1, c => c.TooltipText.Contains("Wood · 64 total")), Cell(0, c => c.TooltipText == ""));
        await Until(() => crates[0].Inventory["wood"] == cap, "the held stack filling the crate", 2);
        Require(world.Held is { Item: "wood", Count: var rest } && rest == 64 - cap, "What did not fit into the crate must stay held.");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = true });
        await Until(() => !world.Near(crates[0]) && world.Near(crates[1]), "walking out of reach of the first crate and up to the second", 10);
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.D, Keycode = Key.D, Pressed = false });
        Require(!cursor.Empty, "Walking to the next crate must keep the rest held.");
        await ClickOn(crates[1]);
        await Until(() => crates[1].Inventory["wood"] == cap, "the held rest filling the next crate", 2);
        Require(world.Held is { Count: var left } && left == 64 - 2 * cap, "What still does not fit must stay held.");
        await KeyStroke(Key.Escape);
        await Until(() => cursor.Empty, "letting go of the leftover wood", 2);
        Require(world.Backpack["wood"] == 64 - 2 * cap, "Letting go must leave the rest in the backpack.");
        foreach (var crate in crates) RemoveThings(Hosted, t => t == crate);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }
}
