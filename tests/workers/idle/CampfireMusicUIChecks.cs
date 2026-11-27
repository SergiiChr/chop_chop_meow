using System.Threading.Tasks;
using Godot;
using static CampfireMusicSteps;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunCampfireMusicUIChecks() {
        await TestClean(CheckOnlyAPauseBringsBackTheSoundtrackDuringACampfireSong);
        await TestClean(CheckSleepFastForwardLetsTheSoundtrackPlay);
    }

    private async Task CheckOnlyAPauseBringsBackTheSoundtrackDuringACampfireSong() {
        await CloseWithEsc();
        var home = world.Player;
        var fire = Hosted.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(-4, 2));
        var (guitar, drum, harmonica, listener) = AddCampfireCrew(Hosted, fire);
        Warp(Hosted, fire.Pos + Vector2.Down * 3);
        await Until(() => world.CampfireMusic.SongAt(fire.Id) is { Track: > 0 } song && world.Time >= song.StartsAt,
            "the campfire trio starting its song");
        await Until(() => !sounds.DayMusicPlaying, "the nearby campfire song taking over the soundtrack", 6);

        // Run the sound mix without a new world update, as it would during a brief network gap.
        sounds.UpdateMusic(.2f);
        sounds.UpdateMusic(.2f);
        Require(!sounds.DayMusicPlaying, "a brief network gap should not bring back the normal soundtrack");

        await KeyStroke(Key.Escape);
        Require(view.Paused, "Esc should open the pause menu while the campfire song plays");
        await Until(() => sounds.DayMusicPlaying, "the normal soundtrack returning while the campfire song is paused", 6);

        await KeyStroke(Key.Escape);
        Hosted.Fire(guitar); Hosted.Fire(drum); Hosted.Fire(harmonica); Hosted.Fire(listener);
        RemoveThings(Hosted, t => t == fire);
        Warp(Hosted, home);
        await Frames(4);
    }

    private async Task CheckSleepFastForwardLetsTheSoundtrackPlay() {
        var home = world.Player;
        var fire = Hosted.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(-4, 2));
        var bed = Hosted.Add(ThingKind.StrawBed, fire.Pos + Vector2.Down * 2.8f);
        var (guitar, drum, harmonica, listener) = AddCampfireCrew(Hosted, fire);
        Warp(Hosted, bed.Pos + Vector2.Right * 1.4f);
        await Until(() => world.CampfireMusic.SongAt(fire.Id) is { Track: > 0 } song && world.Time >= song.StartsAt,
            "the nearby campfire trio starting its song");
        await Until(() => !sounds.DayMusicPlaying, "the nearby song taking over the soundtrack", 6);

        await Until(() => view.Pick(view.Screen(bed.Pos, .06f))?.Id == bed.Id, "the straw bed within reach of the pointer", 2);
        await Click(view.Screen(bed.Pos, .06f));
        await Until(() => Named("sleep") is Button { Text: "Sleep" }, "the straw bed's Sleep button", 2);
        await Click(Named("sleep")!);
        await Until(() => world.Sleep.SleepingAt == bed.Id, "sleeping beside the campfire", 2);
        await Until(() => sounds.DayMusicPlaying, "the soundtrack returning during accelerated sleep", 4);

        await KeyStroke(Key.W);
        await Until(() => world.Sleep.SleepingAt == 0, "waking beside the campfire", 2);
        await CloseWithEsc();
        Hosted.Fire(guitar); Hosted.Fire(drum); Hosted.Fire(harmonica); Hosted.Fire(listener);
        RemoveThings(Hosted, t => t == fire || t == bed);
        Warp(Hosted, home);
        await Frames(4);
    }
}
