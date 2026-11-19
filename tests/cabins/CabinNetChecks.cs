using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Where the host's cabin stands for the guest to walk into.</summary>
    private static readonly Vector2 NetHut = Terrain.Spawn + new Vector2(-3.5f, -3);

    /// <summary>The network scene of cabins: one click by the guest walks it to the host's cabin and in.</summary>
    private NetScene CabinNetScene => new("cabins", () => Hosted.Add(ThingKind.Hut, NetHut), HostSeesTheGuestWalkIntoTheCabin, GuestWalksIntoTheHostsCabin);

    private async Task HostSeesTheGuestWalkIntoTheCabin(PlayerState guest) {
        var hut = Hosted.Things.Single(t => t.Kind == ThingKind.Hut);
        await Until(() => guest.Sheltered == hut.Id, "the guest walking into the cabin with one click");
    }

    private async Task GuestWalksIntoTheHostsCabin() {
        // The host checks reach from where the guest stood when it clicked, not from where its glide has got to.
        await CloseWithEsc(); await Click(view.Screen(NetHut, .8f));
        await Until(() => world.Sheltered == world.Things.Single(t => t.Kind == ThingKind.Hut).Id, "the host letting the guest into the cabin it walked to");
    }
}
