namespace ChopChopMeow;

/// <summary>Steps of the UI and network checks of the diary.</summary>
public partial class Game {
    /// <summary>The window's player in the server's world.</summary>
    private PlayerState HostedMe => Hosted.PlayerById(world.Me.Id)!;
}
