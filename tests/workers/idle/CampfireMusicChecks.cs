using System;
using ChopChopMeow;
using Godot;
using static CampfireMusicSteps;
using static Checks;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

internal static class CampfireMusicChecks {
    public static void Run(GameData data) {
        Test(CheckArrivingWorkersPickOnlyInstrumentsMissingFromTheirCampfire, data);
        Test(CheckSeparateCampfiresCanEachHaveAGuitar, data);
        Test(CheckTheThreeInstrumentsAtOneCampfirePlayOneSong, data);
        Test(CheckACampfireSongKeepsTimeInAnOldSettlement, data);
        Test(CheckInstrumentsAtDifferentCampfiresNeverStartASongTogether, data);
        Test(CheckOneInstrumentPlaysTwoToThreeSecondsAfterEachSoundAndStopsWhenTheWorkerLeaves, data);
        Test(CheckLunchingWorkersDoNotPlayCampfireMusic, data);
        Test(CheckEveryGuitarStrumLastsTwoSeconds);
        Test(CheckEveryHarmonicaSoundLastsLongerThanASixthOfASecond);
    }

    private static void CheckEveryGuitarStrumLastsTwoSeconds() {
        for (int solo = 1; solo <= CampfireScore.SoloCount(CampfireInstrument.Guitar); solo++) {
            float duration = CampfireScore.Solo(CampfireInstrument.Guitar, solo).Duration;
            Require(Math.Abs(duration - 2) < .01f, $"guitar strum {solo} should last two seconds, got {duration:F2}s");
        }
    }

    private static void CheckEveryHarmonicaSoundLastsLongerThanASixthOfASecond() {
        for (int solo = 1; solo <= CampfireScore.SoloCount(CampfireInstrument.Harmonica); solo++) {
            float duration = CampfireScore.Solo(CampfireInstrument.Harmonica, solo).Duration;
            Require(duration > .16f, $"harmonica sound {solo} should last longer than 0.16s, got {duration:F2}s");
        }
    }

    private static void CheckArrivingWorkersPickOnlyInstrumentsMissingFromTheirCampfire(GameData data) {
        var world = FundedWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var music = world.CampfireMusic;
        var guitar = SeatOnTheSameSuccessfulRoll(world, fire, 0);
        Require(music.SeatOf(guitar).Instrument == CampfireInstrument.Guitar, "the first worker should bring a guitar");
        var harmonica = SeatOnTheSameSuccessfulRoll(world, fire, 1);
        Require(music.SeatOf(harmonica).Instrument == CampfireInstrument.Harmonica,
            "with a guitar already at the fire, a successful roll should only choose between harmonica and drum");
        var drum = SeatOnTheSameSuccessfulRoll(world, fire, 2);
        Require(music.SeatOf(drum).Instrument == CampfireInstrument.Drum, "the next successful roll should get the only missing instrument");
        var listener = SeatOnTheSameSuccessfulRoll(world, fire, 3);
        Require(music.SeatOf(listener).Instrument == CampfireInstrument.None, "an idle visitor cannot bring a fourth instrument");

        world.Fire(drum);
        Work(world, 1);
        Require(music.SeatOf(listener).Instrument == CampfireInstrument.None, "a seated listener does not roll again when an instrument frees up");
        var replacement = SeatOnTheSameSuccessfulRoll(world, fire, 2);
        Require(music.SeatOf(replacement).Instrument == CampfireInstrument.Drum, "a fresh idle visit can bring back the missing drum");
    }

    private static void CheckSeparateCampfiresCanEachHaveAGuitar(GameData data) {
        var world = FundedWorld(data);
        var near = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var far = world.Add(ThingKind.Campfire, new Vector2(30, 20));
        var first = SeatOnTheSameSuccessfulRoll(world, near, 0);
        var second = SeatOnTheSameSuccessfulRoll(world, far, 0);
        Require(world.CampfireMusic.SeatOf(first) == new CampfireSeatMusic(near.Id, CampfireInstrument.Guitar) &&
            world.CampfireMusic.SeatOf(second) == new CampfireSeatMusic(far.Id, CampfireInstrument.Guitar),
            "one fire's guitarist should not take the guitar away from a different fire");
    }

    private static void CheckTheThreeInstrumentsAtOneCampfirePlayOneSong(GameData data) {
        var (world, fire, guitar, drum, harmonica, listener) = SeatCampfireTrio(data);
        var music = world.CampfireMusic;
        Require(music.SeatOf(guitar).Instrument == CampfireInstrument.Guitar && music.SeatOf(drum).Instrument == CampfireInstrument.Drum &&
            music.SeatOf(harmonica).Instrument == CampfireInstrument.Harmonica, "idle workers should roll their instruments on sitting at the fire");
        var song = music.SongAt(fire.Id);
        Require(song.Track is 1 or 2 && song.Plays(guitar.Id) && song.Plays(drum.Id) && song.Plays(harmonica.Id),
            "the three workers should start one of the two campfire songs together");
        Require(music.SeatOf(listener).Fire == fire.Id && !song.Plays(listener.Id), "the fourth idle worker should listen without playing");
        float songEnds = song.StartsAt + CampfireScore.Song(song.Track).Duration;
        Work(world, 135, () => AnySolo(world, guitar, drum, harmonica) || world.Time >= songEnds);
        Require(!AnySolo(world, guitar, drum, harmonica) && world.Time >= songEnds, "the trio should play no solos during their song");
        Work(world, 15, () => AnySolo(world, guitar, drum, harmonica));
        Require(AnySolo(world, guitar, drum, harmonica), "the trio should go back to playing solos after their song");
        Require(music.SongAt(fire.Id) == song, "the fire should not start another song while the trio remains seated");
        world.Fire(drum);
        Work(world, .5f);
        Require(music.SongAt(fire.Id).Track == 0, "an incomplete trio should end the gathering");
        var nextDrummer = SeatUntilOneBrings(world, fire, 1, CampfireInstrument.Drum);
        Work(world, 2, () => music.SongAt(fire.Id).Track > 0);
        Require(music.SongAt(fire.Id).Plays(nextDrummer.Id), "the musicians should play another song when a new trio gathers");
    }

    private static void CheckACampfireSongKeepsTimeInAnOldSettlement(GameData data) {
        var (world, fire, _, _, _, _) = SeatCampfireTrio(data, days: 48);
        var song = world.CampfireMusic.SongAt(fire.Id);
        Require(song.Track > 0, "the old settlement's trio should have a song to play");
        Work(world, 1, () => world.Time >= song.StartsAt);
        float elapsed = world.Time - song.StartsAt;
        Require(elapsed >= 0, "the old settlement's trio should have started its song");

        for (int frame = 0; frame < 480; frame++) world.Tick(1f / 60);
        float played = world.Time - song.StartsAt - elapsed;
        Require(Math.Abs(played - 8) < 1f / 60, $"eight seconds of playback should pass on the world's clock, got {played:F3}s");
    }

    private static void CheckInstrumentsAtDifferentCampfiresNeverStartASongTogether(GameData data) {
        var world = FundedWorld(data);
        var near = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var far = world.Add(ThingKind.Campfire, new Vector2(30, 20));
        world.CampfireMusic.Seed(374);
        var guitar = AddAtFire(world, near, 0);
        AddAtFire(world, near, 1);
        var harmonica = AddAtFire(world, far, 0);
        Work(world, .5f);
        Require(world.CampfireMusic.SeatOf(guitar).Instrument == CampfireInstrument.Guitar &&
            world.CampfireMusic.SeatOf(harmonica).Instrument == CampfireInstrument.Harmonica, "the instruments should be present at separate fires");
        Require(world.CampfireMusic.SongAt(near.Id).Track == 0 && world.CampfireMusic.SongAt(far.Id).Track == 0,
            "instruments at separate campfires should not form a trio");
    }

    private static void CheckOneInstrumentPlaysTwoToThreeSecondsAfterEachSoundAndStopsWhenTheWorkerLeaves(GameData data) {
        var world = FundedWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        world.CampfireMusic.Seed(374);
        var guitarist = AddAtFire(world, fire, 0);
        Work(world, 1.9f);
        Require(world.CampfireMusic.SeatOf(guitarist) is { Instrument: CampfireInstrument.Guitar, Solo: 0 },
            "the guitarist should wait at least two seconds before the first strum");
        Work(world, 1.5f, () => world.CampfireMusic.SeatOf(guitarist).Solo > 0);
        var first = world.CampfireMusic.SeatOf(guitarist);
        Require(first.Solo > 0, "the guitarist should strum within about three seconds of sitting down");
        float firstEnds = first.SoloAt + CampfireScore.Solo(CampfireInstrument.Guitar, first.Solo).Duration;
        Work(world, 20, () => world.CampfireMusic.SeatOf(guitarist).SoloAt > first.SoloAt);
        float gap = world.CampfireMusic.SeatOf(guitarist).SoloAt - firstEnds;
        Require(gap is >= 2 and <= 3.3f, $"the next strum should start two to three seconds after the first has finished, got {gap:F2}s");
        world.Fire(guitarist);
        Work(world, .2f);
        Require(world.CampfireMusic.SeatOf(guitarist).Fire == 0, "a worker who has left the campfire should no longer play there");
    }

    private static void CheckLunchingWorkersDoNotPlayCampfireMusic(GameData data) {
        var world = FundedWorld(data);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        RunUntil(world, (data.LunchHour - 6) * data.HourSeconds + 1);
        world.CampfireMusic.Seed(374);
        var worker = AddAtFire(world, fire, 0);
        Work(world, 1);
        Require(world.Lunch && Seated(worker) && world.CampfireMusic.SeatOf(worker).Fire == 0,
            "a worker sitting at lunch should neither play nor listen to campfire music");
    }
}
