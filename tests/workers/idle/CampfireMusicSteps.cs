using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

internal static class CampfireMusicSteps {
    /// <summary>Seats three instrumentalists and a listener from a reproducible run of visit rolls.</summary>
    public static (World World, Thing Fire, Worker Guitar, Worker Drum, Worker Harmonica, Worker Listener) SeatCampfireTrio(
        GameData data, int days = 0) {
        var world = FundedWorld(data);
        if (days > 0) SkipClock(world, days * data.DaySeconds);
        var fire = world.Add(ThingKind.Campfire, new Vector2(20, 20));
        var (guitar, drum, harmonica, listener) = AddCampfireCrew(world, fire);
        Work(world, .5f);
        return (world, fire, guitar, drum, harmonica, listener);
    }

    public static (Worker Guitar, Worker Drum, Worker Harmonica, Worker Listener) AddCampfireCrew(World world, Thing fire) {
        world.CampfireMusic.Seed(374);
        return (AddAtFire(world, fire, 0), AddAtFire(world, fire, 1), AddAtFire(world, fire, 2), AddAtFire(world, fire, 3));
    }

    public static Worker AddAtFire(World world, Thing fire, int seat) =>
        AddWorker(world, fire.Pos + Vector2.FromAngle(seat * Mathf.Tau / BreakSeats.FireSeats) * BreakSeats.FireRadius);

    /// <summary>Whether any of the three musicians is playing a solo clip now.</summary>
    public static bool AnySolo(World world, Worker first, Worker second, Worker third) =>
        world.CampfireMusic.SeatOf(first).Solo > 0 || world.CampfireMusic.SeatOf(second).Solo > 0 ||
        world.CampfireMusic.SeatOf(third).Solo > 0;

    /// <summary>Seats newcomers one at a time, sending away each who brings nothing, until one brings the instrument.</summary>
    public static Worker SeatUntilOneBrings(World world, Thing fire, int seat, CampfireInstrument instrument) {
        Worker? bringing = null;
        for (int visit = 0; visit < 60 && bringing == null; visit++) {
            var worker = AddAtFire(world, fire, seat);
            Work(world, 1, () => world.CampfireMusic.SeatOf(worker).Fire != 0);
            if (world.CampfireMusic.SeatOf(worker).Instrument == instrument) bringing = worker;
            else { world.Fire(worker); Work(world, .1f); }
        }
        Require(bringing != null, $"a newcomer should bring the {instrument} within 60 visits");
        return bringing!;
    }

    /// <summary>Seats a newcomer whose visit roll would choose guitar with all three instruments available.</summary>
    public static Worker SeatOnTheSameSuccessfulRoll(World world, Thing fire, int seat) {
        world.CampfireMusic.Seed(16);
        var worker = AddAtFire(world, fire, seat);
        Work(world, 1, () => world.CampfireMusic.SeatOf(worker).Fire != 0);
        Require(world.CampfireMusic.SeatOf(worker).Fire == fire.Id, "the newcomer should sit at the campfire being checked");
        return worker;
    }
}
