using System.Threading.Tasks;
using Godot;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// Puts a nettle beside the player and holds right mouse on it until it is picked, then lets go and takes the nettle
    /// out of the backpack, and gives back the health its sting took.
    /// Returns whether the groan started during the pick, and whether it then broke off before the nettle was gone.
    /// </summary>
    private async Task<(bool Groaned, bool BrokeOff)> PickNettleBesideThePlayer() {
        float health = Hosted.Health;
        var nettle = Hosted.Add(ThingKind.Nettle, world.Player + new Vector2(1, -1), 1);
        await Frames(2);
        var press = RightPress(nettle.Pos, .2f);
        bool groaned = false, brokeOff = false;
        await Until(() => {
            if (Hosted.Find(nettle.Id) == null) return true;
            bool holding = sounds.Holding("sting_groan");
            brokeOff |= groaned && !holding;
            groaned |= holding;
            return false;
        }, "holding RMB on a nettle picking it", 5);
        RightRelease(press);
        Drop(Hosted, "nettle"); SetNeeds(Hosted, health: health);
        return (groaned, brokeOff);
    }
}
