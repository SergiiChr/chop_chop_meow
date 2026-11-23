using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

public partial class Game {
    private async Task ToggleFirstPersonInPauseMenu() {
        await KeyStroke(Key.Escape);
        await Click(FindButton(menu, "Options")!);
        await Click((CheckButton)startMenu.Screen.FindChild("first_person", recursive: true, owned: false));
        await Click(FindButton(startMenu.Screen, "Back · Esc")!);
        await Click(FindButton(menu, "Resume · Esc")!);
    }
}
