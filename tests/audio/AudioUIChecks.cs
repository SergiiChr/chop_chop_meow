using System;
using System.Linq;
using System.Threading.Tasks;
using static Checks;
using static WorldSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunAudioUIChecks() {
        await TestClean(CheckSoundsRotateAndMatchTheWolfHowl);
        await TestClean(CheckMusicRotatesCrossfadesAndResumes);
    }

    /// <summary>Sounds rotate through variants, and a howl plays as long as a wolf holds it.</summary>
    private Task CheckSoundsRotateAndMatchTheWolfHowl() {
        Require(Sounds.Names.All(name => sounds.VariantCount(name) > 0), "Every sound must load.");
        Require(sounds.VariantCount("footsteps_default") == 6, "Numbered footstep files must group as six variants.");
        Require(sounds.VariantCount("chop_tree") == 1, "A single chop file must count as one variant.");
        Require(Math.Abs(sounds.Length("wolf_howl") - world.Data.LoneHowlSeconds) < .1 && Math.Abs(sounds.Length("wolf_howl_group") - world.Data.PackHowlSeconds) < .1,
            "A wolf must hold its howl as long as the howl recording plays.");
        // Random picks can keep to two of the three variants, about once in a thousand runs: a seed makes them the same every run.
        sounds.Reseed(1);
        var picks = Enumerable.Range(0, 12).Select(_ => { sounds.Play("anvil_work"); return sounds.LastVariant("anvil_work"); }).ToArray();
        Require(picks.Zip(picks.Skip(1)).All(p => p.First != p.Second) && picks.Distinct().Count() == 3, "Repeated sounds must rotate through their variants without repeating the previous one.");
        return Task.CompletedTask;
    }

    /// <summary>Both playlists advance and wrap, crossfade at nightfall and dawn, and resume when their time of day returns.</summary>
    private async Task CheckMusicRotatesCrossfadesAndResumes() {
        sounds.UpdateMusic(Sounds.MusicFadeSeconds);
        Require(sounds.DayMusicPlaying && !sounds.NightMusicPlaying && sounds.DayMusicTrack == "path_to_lake_land",
            "Daytime must start with Path to Lake Land and no night music.");
        foreach (string name in new[] { "mystic_theme", "jazz_jazz_music", "jazz_bar", "lofi_soul", "groove_soul", "path_to_lake_land" }) {
            sounds.SkipToDayTrackEnd();
            await Until(() => sounds.DayMusicPlaying && sounds.DayMusicTrack == name, $"day music advancing to {name}", 5);
        }
        sounds.SkipToDayTrackEnd();
        await Until(() => sounds.DayMusicTrack == "mystic_theme" && sounds.DayMusicPosition > .2f,
            "Mystic Theme playing long enough to resume", 5);
        RunUntilNight(Hosted); await Frames(1); sounds.UpdateMusic(Sounds.MusicFadeSeconds / 2);
        Require(sounds.DayMusicPlaying && sounds.NightMusicPlaying, "Nightfall must crossfade the day music into the night music.");
        sounds.UpdateMusic(Sounds.MusicFadeSeconds);
        Require(!sounds.DayMusicPlaying && sounds.NightMusicPlaying, "Day music must stop once faded out.");
        float dayStoppedAt = sounds.DayMusicPosition;
        Require(dayStoppedAt > .2f, "Nightfall must keep the day music's position.");
        Require(sounds.NightMusicTrack == "forest", "Night music must start with Forest.");
        sounds.SkipToNightTrackEnd();
        await Until(() => sounds.NightMusicPlaying && sounds.NightMusicTrack == "soft_jazz", "Soft Jazz following Forest", 5);
        await Until(() => sounds.NightMusicPosition > .2f, "Soft Jazz playing long enough to resume", 5);
        RunUntilMorning(Hosted); await Frames(1); sounds.UpdateMusic(Sounds.MusicFadeSeconds / 2);
        Require(sounds.DayMusicPlaying && sounds.NightMusicPlaying, "Dawn must crossfade the night music into the day music.");
        Require(sounds.DayMusicTrack == "mystic_theme" && sounds.DayMusicPosition >= dayStoppedAt,
            "The next day must resume the same track where it stopped.");
        sounds.UpdateMusic(Sounds.MusicFadeSeconds);
        float nightStoppedAt = sounds.NightMusicPosition;
        Require(sounds.DayMusicPlaying && !sounds.NightMusicPlaying, "Night music must stop once faded out.");
        Require(nightStoppedAt > .2f, "Dawn must keep the night music's position.");
        RunUntilNight(Hosted); await Frames(1); sounds.UpdateMusic(.1f);
        Require(sounds.NightMusicPlaying && sounds.NightMusicTrack == "soft_jazz" && sounds.NightMusicPosition >= nightStoppedAt,
            "The next night must resume the same track where it stopped.");
        sounds.SkipToNightTrackEnd();
        await Until(() => sounds.NightMusicPlaying && sounds.NightMusicTrack == "forest", "night music wrapping from Soft Jazz to Forest", 5);
        RunUntilMorning(Hosted); await Frames(1); sounds.UpdateMusic(Sounds.MusicFadeSeconds * 2);
    }
}
