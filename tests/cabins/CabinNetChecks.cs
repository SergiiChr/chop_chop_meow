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
    private const float NetHutRotation = 3 * Mathf.Pi / 2;

    /// <summary>The guest sees the host's quarter-turned cabin and walks into its door with a double click.</summary>
    private NetScene CabinNetScene => new("cabins", () => Hosted.Add(ThingKind.Hut, NetHut, rotation: NetHutRotation), HostSeesTheGuestWalkIntoTheCabin, GuestWalksIntoTheHostsCabin);

    private async Task HostSeesTheGuestWalkIntoTheCabin(PlayerState guest) {
        var hut = Hosted.Things.Single(t => t.Kind == ThingKind.Hut);
        await Until(() => guest.Sheltered == hut.Id, "the guest walking into the rotated cabin with a double click");
        Require(guest.Pos.DistanceTo(NetHut + new Vector2(2, .34f)) < .18f, "the guest should enter from the rotated doorstep");
    }

    private async Task GuestWalksIntoTheHostsCabin() {
        // The host checks reach from where the guest stood when it double clicked, not from where its glide has got to.
        var hut = world.Things.Single(t => t.Kind == ThingKind.Hut);
        Require(hut.Rotation == NetHutRotation, "the host's cabin angle should reach the guest");
        await CloseWithEsc(); await DoubleClick(view.Screen(NetHut, .8f));
        await Until(() => world.Sheltered == hut.Id, "the host letting the guest into the cabin it walked to");
    }
}
