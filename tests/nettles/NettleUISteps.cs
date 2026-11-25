using System.Threading.Tasks;
using Godot;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// Puts a nettle beside the player and holds right mouse on it until it is picked, then lets go and takes the nettle
    /// out of the backpack, and gives back the health its sting took.
    /// Returns whether the groan was held at any moment of the pick.
    /// </summary>
    private async Task<bool> PickNettleBesideThePlayer() {
        float health = Hosted.Health;
        var nettle = Hosted.Add(ThingKind.Nettle, world.Player + new Vector2(1, -1), 1);
        await Frames(2);
        var press = RightPress(nettle.Pos, .2f);
        bool groaned = false;
        await Until(() => {
            groaned |= sounds.Holding("sting_groan");
            return Hosted.Find(nettle.Id) == null;
        }, "holding RMB on a nettle picking it", 5);
        RightRelease(press);
        Drop(Hosted, "nettle"); SetNeeds(Hosted, health: health);
        return groaned;
    }
}
