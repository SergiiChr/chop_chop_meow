using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The network scene of a nettle's sting: the host puts a nettle beside the guest, the guest picks it by hand and
    /// groans, and the host hears the guest groan until the nettle is picked.
    /// </summary>
    private NetScene NettleNetScene => new("nettles", null, HostHearsTheGuestGroan, GuestPicksANettleByHand);

    private async Task HostHearsTheGuestGroan(PlayerState guest) {
        int shovels = Drop(guest, "steel_shovel");
        var off = Enumerable.Range(0, 8).Select(i => Vector2.Right.Rotated(i * Mathf.Tau / 8)).First(way => World.Open(guest.Pos + way));
        var nettle = Hosted.Add(ThingKind.Nettle, guest.Pos + off, 1);
        Signal("nettle-up", GD.VarToStr(nettle.Pos));
        // The host's own player picks nothing, so a groan the host hears is the guest's.
        await Until(() => sounds.Holding("sting_groan"), "the host hearing the guest groan as the nettle stings them", 15);
        await Until(() => Hosted.Find(nettle.Id) == null, "the guest picking the host's nettle", 10);
        await Until(() => !sounds.Holding("sting_groan"), "the host hearing the guest's groan stop once the nettle is picked", 3);
        await Signaled("nettle-picked");
        Drop(guest, "nettle"); Give(guest, "steel_shovel", shovels); SetNeeds(guest, health: 100);
        Signal("nettle-over");
    }

    private async Task GuestPicksANettleByHand() {
        await CloseWithEsc();
        var placed = GD.StrToVar(await Signaled("nettle-up")).AsVector2();
        await Until(() => world.ThingsOf(ThingKind.Nettle).Any(t => t.Pos.DistanceTo(placed) < .01f), "the host's nettle showing in the guest's world", 15);
        var nettle = world.ThingsOf(ThingKind.Nettle).First(t => t.Pos.DistanceTo(placed) < .01f);
        var press = RightPress(nettle.Pos, .2f);
        await Until(() => sounds.Holding("sting_groan"), "the guest groaning as the nettle stings them", 10);
        await Until(() => world.Find(nettle.Id) == null, "the guest picking the nettle by hand", 10);
        RightRelease(press);
        await Until(() => !sounds.Holding("sting_groan"), "the guest's groan stopping once the nettle is picked", 3);
        Signal("nettle-picked");
        await Signaled("nettle-over");
    }
}
