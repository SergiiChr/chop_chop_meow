using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static CampfireMusicSteps;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private NetScene CampfireMusicNetScene => new("campfire-music", null, HostStartsAndBreaksUpACampfireTrio, GuestSeesTheCampfireTrioStartAndStop);

    private async Task HostStartsAndBreaksUpACampfireTrio(PlayerState guest) {
        var fire = Hosted.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(-4, 2));
        var (guitar, drum, harmonica, listener) = AddCampfireCrew(Hosted, fire);
        await Until(() => Hosted.CampfireMusic.SongAt(fire.Id).Track > 0, "the idle workers starting a song at their campfire");
        Signal("campfire-playing", fire.Id.ToString(CultureInfo.InvariantCulture));
        await Signaled("campfire-guest-menu");
        await KeyStroke(Key.Escape);
        Require(menu.Visible, "the host should open the pause menu after the guest");
        await Signaled("campfire-guest-paused");
        await KeyStroke(Key.Escape);
        await Signaled("campfire-guest-heard");
        Hosted.Fire(drum);
        await Signaled("campfire-guest-stopped");
        Hosted.Fire(guitar); Hosted.Fire(harmonica); Hosted.Fire(listener);
        RemoveThings(Hosted, t => t == fire);
    }

    private async Task GuestSeesTheCampfireTrioStartAndStop() {
        int fire = int.Parse(await Signaled("campfire-playing"), CultureInfo.InvariantCulture);
        await Until(() => world.CampfireMusic.SongAt(fire).Track > 0, "the guest's copy hearing which campfire song the host started");
        var song = world.CampfireMusic.SongAt(fire);
        var music = world.CampfireMusic;
        var harmonica = music.SeatOf(world.Workers.Single(w => w.Id == song.Harmonica));
        var guitar = music.SeatOf(world.Workers.Single(w => w.Id == song.Guitar));
        var drum = music.SeatOf(world.Workers.Single(w => w.Id == song.Drum));
        Require(harmonica.Fire == fire && harmonica.Instrument == CampfireInstrument.Harmonica, "the guest must see the trio's harmonica player");
        Require(guitar.Fire == fire && guitar.Instrument == CampfireInstrument.Guitar, "the guest must see the trio's guitarist");
        Require(drum.Fire == fire && drum.Instrument == CampfireInstrument.Drum, "the guest must see the trio's drummer");
        await Until(() => !sounds.DayMusicPlaying, "the campfire song taking over the guest's soundtrack", 6);
        await KeyStroke(Key.Escape);
        Require(menu.Visible && !sounds.DayMusicPlaying, "the guest's menu alone should not pause the song");
        Signal("campfire-guest-menu");
        await Until(() => sounds.DayMusicPlaying, "the soundtrack returning when both players open their menus", 6);
        Signal("campfire-guest-paused");
        await Until(() => !sounds.DayMusicPlaying, "the song resuming even though the guest's menu is still open", 6);
        await KeyStroke(Key.Escape);
        Signal("campfire-guest-heard");
        await Until(() => world.CampfireMusic.SongAt(fire).Track == 0, "the guest seeing the trio break up when its drummer leaves");
        Signal("campfire-guest-stopped");
    }
}
