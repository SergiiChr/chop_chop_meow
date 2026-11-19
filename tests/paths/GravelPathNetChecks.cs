using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The path tile the host lays before the guest joins, and the tile corner to corner with it that the guest lays.
    /// </summary>
    private static readonly Vector2I NetPath = Terrain.TileAt(Terrain.Spawn + new Vector2(3, -2)), NetLaid = NetPath + new Vector2I(1, -1);

    /// <summary>
    /// The network scene of gravel paths: joining brings the host's path tile over, the guest takes it up with the
    /// remove
    /// selector and lays it again beside where it was, and the gravel is the guest's.
    /// </summary>
    private NetScene GravelPathNetScene => new("gravel-paths", () => GravelPathSteps.LayPath(Hosted, NetPath), HostSeesTheGuestMoveAPathTile, GuestMovesTheHostsPathTile);

    private async Task HostSeesTheGuestMoveAPathTile(PlayerState guest) {
        await Until(() => !Hosted.GravelPaths.Laid(NetPath) && Hosted.GravelPaths.Laid(NetLaid), "the guest taking up the host's path tile and laying it beside where it was", 90);
        await Until(() => world.GravelPaths.Laid(NetLaid) && !world.GravelPaths.Laid(NetPath), "the host's window showing the tile the guest laid");
        Require(world.Backpack["gravel"] == 0, "Gravel from a tile the guest takes up must not reach the host's backpack.");
    }

    private async Task GuestMovesTheHostsPathTile() {
        // The host's gravel path comes up for the guest's remove selector, and goes down again where the guest lays it.
        Require(world.GravelPaths.Laid(NetPath), "Joining must bring the host's gravel path into the guest's world.");
        await KeyStroke(Key.B); await Click(Named("remove")!);
        await Click(view.Screen(NetPath));
        await Until(() => !world.GravelPaths.Laid(NetPath) && world.Backpack["gravel"] == 4, "the host taking up the path tile the guest clicked with the remove selector");
        await KeyStroke(Key.Escape);
        await KeyStroke(Key.B); await PickUp(Named("build_gravel_path")!);
        await Click(view.Screen(NetLaid));
        await Until(() => world.GravelPaths.Laid(NetLaid) && world.Backpack["gravel"] == 0, "the host laying the path tile the guest placed");
        await CloseWithEsc();
    }
}
