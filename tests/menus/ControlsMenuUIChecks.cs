using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    private Task RunControlsMenuUIChecks() => TestClean(CheckMainMenuOpensControlsAndReturns);

    private async Task CheckMainMenuOpensControlsAndReturns() {
        var controls = FindButton(startMenu.Screen, "Controls");
        Require(controls != null, "The main menu must offer Controls.");
        float time = world.Time;
        await Click(controls!);
        Require(HasLabel(startMenu.Screen, "CONTROLS") && HasLabel(startMenu.Screen, "Sprint.")
            && HasLabel(startMenu.Screen, "Move the entire stack or queue 5 crafting batches."),
            "Controls must show the movement and crafting shortcuts.");
        Require(view.Paused && world.Time == time, "The world must stay paused on the Controls screen.");
        await KeyStroke(Key.Escape);
        Require(FindButton(startMenu.Screen, "New game") != null, "Esc must return from Controls to the main menu.");
        await Click(FindButton(startMenu.Screen, "Controls")!);
        await Click(FindButton(startMenu.Screen, "Back · Esc")!);
        Require(FindButton(startMenu.Screen, "New game") != null, "Back must return from Controls to the main menu.");
    }
}
