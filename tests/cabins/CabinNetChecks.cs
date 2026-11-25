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
    private static readonly Vector2 NetStrawBed = Terrain.Spawn + new Vector2(-4.4f, 3.8f);
    private const float NetHutRotation = 3 * Mathf.Pi / 2;

    /// <summary>The guest sees the host's quarter-turned cabin and walks into its door with a double click.</summary>
    private NetScene CabinNetScene => new("cabins",
        () => {
            Hosted.Add(ThingKind.Hut, NetHut, rotation: NetHutRotation);
            Hosted.Add(ThingKind.StrawBed, NetStrawBed);
        },
        HostSleepsOnStrawWhileGuestEntersCabin, GuestEntersCabinAndSleepsToDawn);

    private async Task HostSleepsOnStrawWhileGuestEntersCabin(PlayerState guest) {
        var hut = Hosted.Things.Single(t => t.Kind == ThingKind.Hut);
        await Until(() => guest.Sheltered == hut.Id, "the guest walking into the rotated cabin with a double click");
        Require(guest.Pos.DistanceTo(NetHut + new Vector2(2, .34f)) < .18f, "the guest should enter from the rotated doorstep");
        Hosted.As(guest, Hosted.Doorways.TurnKey);
        RunUntilPhase(Hosted, .65f);
        SetNeeds(Hosted, 100, 100, 50); SetNeeds(guest, 100, 100, 50);
        Hosted.Add(ThingKind.Campfire, NetStrawBed + Vector2.Down * 2).Burn = Hosted.Data.DaySeconds;
        Warp(Hosted, NetStrawBed + Vector2.Right * 1.5f);
        var bed = Hosted.Things.Single(t => t.Kind == ThingKind.StrawBed);
        int day = Hosted.Day;
        await CloseWithEsc();
        await Until(() => view.Pick(view.Screen(NetStrawBed, .06f))?.Id == bed.Id, "the straw bed within reach of the pointer");
        await Click(view.Screen(NetStrawBed, .06f));
        await Click(Named("sleep")!);
        await Until(() => world.Me.SleepingAt == bed.Id && Named("sleep") is Button { Text: "Wake" } && Hosted.Health > 50.05f,
            "the host sleeping and healing before the guest turns in");
        Require(Hosted.Day == day && guest.Health == 50, "the awake guest should neither heal nor skip time");
        Signal("host-on-straw-bed");
        await Until(() => Hosted.Day == day + 1 && world.Day == day + 1 && world.Me.SleepingAt == 0 && guest.SleepingAt == 0,
            "the guest's sleep taking both players to dawn and waking them");
    }

    private async Task GuestEntersCabinAndSleepsToDawn() {
        // The host checks reach from where the guest stood when it double clicked, not from where its glide has got to.
        var hut = world.Things.Single(t => t.Kind == ThingKind.Hut);
        Require(hut.Rotation == NetHutRotation, "the host's cabin angle should reach the guest");
        await CloseWithEsc(); await DoubleClick(view.Screen(NetHut, .8f));
        await Until(() => world.Sheltered == hut.Id, "the host letting the guest into the cabin it walked to");
        await Signaled("host-on-straw-bed");
        var bed = world.Things.Single(t => t.Kind == ThingKind.StrawBed);
        await Until(() => world.Players.Single(p => p != world.Me).SleepingAt == bed.Id,
            "the guest seeing the host asleep on the new straw bed");
        int day = world.Day;
        await Until(() => Named("sleep") is Button { Visible: true, Text: "Sleep" }, "the cabin's Sleep button appearing inside");
        await Click(Named("sleep")!);
        await Until(() => world.Day == day + 1 && world.Me.SleepingAt == 0 && world.Sheltered == hut.Id,
            "the guest waking in the cabin at dawn with the host");
    }
}
