using System.Threading.Tasks;
using Godot;
using static Checks;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunHudUIChecks() {
        await TestClean(CheckTheCornerShowsTheDayAndTime);
        await TestClean(CheckTheMoonButNotTheSunHasATideTooltip);
    }

    /// <summary>The right corner shows the clock, leaving the needs, bank and pack elsewhere.</summary>
    private async Task CheckTheCornerShowsTheDayAndTime() {
        await Frames();
        Require(corner.Clock.IsVisibleInTree() && HasLabel(corner.Clock, $"Day {world.Day} · {(int)world.Hour}:"),
            "The corner clock should show the current day and time.");
        foreach (string label in new[] { "Health", "Food", "Water", "coins", "Pack" })
            Require(!HasLabel(corner, label), $"The right corner should leave {label} off the clock display.");
    }

    private async Task CheckTheMoonButNotTheSunHasATideTooltip() {
        var dial = (DayDial)corner.Clock.FindChild("sky_dial", true, false);
        RunUntilMorning(Hosted);
        await Frames();
        Require(dial.GetTooltip(dial.Marker) == "", "Hovering the sun should not show a tide forecast.");
        RunUntilNight(Hosted);
        await Frames();
        var at = WindowPoint(dial, dial.Marker);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await Until(() => GetViewport().GuiGetHoveredControl() == dial, "the moon receiving the pointer", 2);
        string tip = dial.GetTooltip(dial.Marker);
        Require(tip.EndsWith(world.Beach.NextTide.High ? "till high tide" : "till low tide"),
            $"Hovering the moon should name the next tide, got '{tip}'.");
        Require(dial.GetTooltip(Vector2.One) == "", "The forecast should only be on the moon, not the rest of the clock.");
        at = GetViewport().GetVisibleRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        RunUntilMorning(Hosted);
        await Frames();
    }
}
