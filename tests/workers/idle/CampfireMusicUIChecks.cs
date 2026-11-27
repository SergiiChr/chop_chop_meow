using System.Threading.Tasks;
using Godot;
using static CampfireMusicSteps;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task CheckPausingACampfireSongBringsBackTheSoundtrack() {
        await CloseWithEsc();
        var home = world.Player;
        var fire = Hosted.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(-4, 2));
        var (guitar, drum, harmonica, listener) = AddCampfireCrew(Hosted, fire);
        Warp(Hosted, fire.Pos + Vector2.Down * 3);
        await Until(() => world.CampfireMusic.SongAt(fire.Id) is { Track: > 0 } song && world.Time >= song.StartsAt,
            "the campfire trio starting its song");
        await Until(() => !sounds.DayMusicPlaying, "the nearby campfire song taking over the soundtrack", 6);

        await KeyStroke(Key.Escape);
        Require(view.Paused, "Esc should open the pause menu while the campfire song plays");
        await Until(() => sounds.DayMusicPlaying, "the normal soundtrack returning while the campfire song is paused", 6);

        await KeyStroke(Key.Escape);
        Hosted.Fire(guitar); Hosted.Fire(drum); Hosted.Fire(harmonica); Hosted.Fire(listener);
        RemoveThings(Hosted, t => t == fire);
        Warp(Hosted, home);
        await Frames(4);
    }
}
