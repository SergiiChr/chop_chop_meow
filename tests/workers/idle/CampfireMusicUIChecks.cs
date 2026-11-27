using System.Threading.Tasks;
using Godot;
using static CampfireMusicSteps;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunCampfireMusicUIChecks() {
        await TestClean(CheckALateUpdateDoesNotRewindACampfireSong);
        await TestClean(CheckTheSoundtrackReturnsWhenASongEndsDuringAnUpdateStall);
        await TestClean(CheckOnlyAPauseBringsBackTheSoundtrackDuringACampfireSong);
        await TestClean(CheckSleepFastForwardLetsTheSoundtrackPlay);
    }

    private async Task CheckALateUpdateDoesNotRewindACampfireSong() {
        await CloseWithEsc();
        var home = world.Player;
        var fire = Hosted.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(-4, 2));
        var (guitar, drum, harmonica, listener) = AddCampfireCrew(Hosted, fire);
        Warp(Hosted, fire.Pos + Vector2.Down * 3);
        await Until(() => world.CampfireMusic.SongAt(fire.Id) is { Track: > 0 } cue && world.Time >= cue.StartsAt + .5f,
            "a campfire song already in progress");
        var song = world.CampfireMusic.SongAt(fire.Id);

        // No input stalls server ticks without pausing play. Keep the connection up and deliver one late tick.
        hosted!.SetProcess(false);
        try {
            await Until(() => world.Time == Hosted.Time, "receiving the server's last tick before the stall");
            float stoppedAt = world.Time;
            await Until(() => sounds.CampfireSongTime(fire.Id, song) > stoppedAt - song.StartsAt + .4f,
                "the audible song playing on during the stall");
            float heard = sounds.CampfireSongTime(fire.Id, song);
            Hosted.Tick(.05f);
            await Until(() => world.Time > stoppedAt, "receiving a late world update");
            await Frames(4);
            Require(sounds.CampfireSongTime(fire.Id, song) >= heard, "the audible song and its pose should not rewind on a late update");
        } finally { hosted.SetProcess(true); }

        Hosted.Fire(guitar); Hosted.Fire(drum); Hosted.Fire(harmonica); Hosted.Fire(listener);
        RemoveThings(Hosted, t => t == fire);
        Warp(Hosted, home);
        await Frames(4);
    }

    private async Task CheckTheSoundtrackReturnsWhenASongEndsDuringAnUpdateStall() {
        var home = world.Player;
        var fire = Hosted.Add(ThingKind.Campfire, Terrain.Spawn + new Vector2(-4, 2));
        var (guitar, drum, harmonica, listener) = AddCampfireCrew(Hosted, fire);
        Warp(Hosted, fire.Pos + Vector2.Down * 3);
        await Until(() => world.CampfireMusic.SongAt(fire.Id) is { Track: > 0 } song && world.Time >= song.StartsAt,
            "the nearby campfire trio starting a song");
        await Until(() => !sounds.DayMusicPlaying, "the nearby song taking over the soundtrack", 6);
        var cue = world.CampfireMusic.SongAt(fire.Id);

        // Jump close to the last note without advancing other work, then leave that update on screen.
        hosted!.SetProcess(false);
        float before = Hosted.Time;
        try {
            float end = cue.StartsAt + CampfireScore.Song(cue.Track).Duration;
            SkipClock(Hosted, end - .65f - Hosted.Time);
            await Until(() => world.Time >= end - .7f && world.Time < end, "a last update just before the song ends");
            await Until(() => sounds.DayMusicPlaying, "the soundtrack returning when the audible song ends", 3);
        } finally {
            SkipClock(Hosted, before - Hosted.Time);
            hosted.SetProcess(true);
        }

        Hosted.Fire(guitar); Hosted.Fire(drum); Hosted.Fire(harmonica); Hosted.Fire(listener);
        RemoveThings(Hosted, t => t == fire);
        Warp(Hosted, home);
        await Frames(4);
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
