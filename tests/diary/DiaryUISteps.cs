using System.Threading.Tasks;
using Godot;

namespace ChopChopMeow;

/// <summary>Steps of the UI and network checks of the diary.</summary>
public partial class Game {
    /// <summary>The window's player in the server's world.</summary>
    private PlayerState HostedMe => Hosted.PlayerById(world.Me.Id)!;

    /// <summary>The player clicks Take on the pop-up a find waits behind, which opens <paramref name="day"/> large within <paramref name="seconds"/>.</summary>
    private async Task TakeFind(int day, float seconds = 3) {
        await Click((Button)findPrompt.FindChild("diary_take", true, false));
        await Until(() => sheet.Visible && sheet.Day == day, $"taking the find opening day {day} large", seconds);
    }
}
