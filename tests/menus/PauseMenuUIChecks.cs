using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private Task RunPauseMenuUIChecks() => TestClean(CheckPauseMenuAsksBeforeNewGameAndQuit);

    /// <summary>
    /// Esc opens the pause menu with the goal log.
    /// New game and Quit ask for confirmation, and confirming New game starts a fresh settlement.
    /// </summary>
    private async Task CheckPauseMenuAsksBeforeNewGameAndQuit() {
        await CloseWithEsc();
        await KeyStroke(Key.Escape);
        Require(menu.Visible, "Esc must open the pause menu.");
        Require(view.Paused, "Esc must pause the game.");
        Require(HasLabel(menu, "Goal Log"), "The pause menu must show the goal log.");
        // New game and Quit ask first, and Esc or Back returns to the pause menu.
        Warp(Hosted, Terrain.Spawn + new Vector2(6, 0));
        await Click(FindButton(menu, "New game")!);
        Require(startMenu.Visible, "New game must open the start menu screen for its question.");
        Require(FindButton(startMenu.Screen, "Start a new game") != null,
            "New game must ask before leaving the settlement.");
        Require(world.Player != Terrain.Spawn,
            "The question must come up with the player still away from the clearing.");
        await KeyStroke(Key.Escape);
        Require(!startMenu.Visible, "Esc on the new game question must close it.");
        Require(menu.Visible, "Esc on the new game question must return to the pause menu.");
        Require(view.Paused, "The game must stay paused after Esc on the new game question.");
        await Click(FindButton(menu, "Quit")!);
        Require(startMenu.Visible && FindButton(startMenu.Screen, "Quit to desktop") != null, "Quit must ask before closing the game.");
        await Click(FindButton(startMenu.Screen, "Back · Esc")!);
        Require(menu.Visible && view.Paused, "Back on the quit question must return to the pause menu.");
        await Click(FindButton(menu, "New game")!);
        await Click(FindButton(startMenu.Screen, "Start a new game")!);
        Require(!startMenu.Visible, "Confirming a new game must close the start menu.");
        Require(!menu.Visible, "Confirming a new game must close the pause menu.");
        Require(!view.Paused, "Confirming a new game must resume the world.");
        Require(ShownPage() == "A new beginning", "Confirming a new game must show the welcome page.");
        Require(world.Player == Terrain.Spawn, "Confirming a new game must put the player back at the spawn.");
        await CloseWithEsc();
        await KeyStroke(Key.Escape);
        await KeyStroke(Key.Escape);
        Require(!menu.Visible && !view.Paused, "Esc must close the pause menu and resume.");
    }
}
