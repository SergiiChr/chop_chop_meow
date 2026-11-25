using Godot;

namespace ChopChopMeow;

/// <summary>A bed laid on dry ground. Its age decides how long it lasts and whether taking it apart returns anything.</summary>
internal static class StrawBed {
    internal const float HalfWidth = .42f, HalfLength = .94f;
    internal const float LieDownSeconds = 2.05f;
    private const int LifeDays = 3;
    private const float Reach = 2;

    internal static float Left(World world, Thing bed) => (bed.BedBuiltAt + LifeDays * world.Data.DaySeconds) - world.Time;
    internal static bool Salvage(World world, Thing bed) => world.Time < bed.BedBuiltAt + world.Data.HourSeconds;
    internal static bool Valid(Thing thing, float time) =>
        thing.Kind != ThingKind.StrawBed ? thing.BedBuiltAt == 0 : thing.BedBuiltAt >= 0 && thing.BedBuiltAt <= time;

    /// <summary>One can lie down from beside the mat, with no obstacle between.</summary>
    internal static bool Reachable(World world, Thing bed) {
        var from = world.Player;
        if (from.DistanceSquaredTo(bed.Pos) > Reach * Reach) return false;
        for (int i = 1; i <= 16; i++)
            if (!world.CanStand(from.Lerp(bed.Pos, i / 16f))) return false;
        return true;
    }

    internal static bool Fits(World world, Vector2 at, float rotation) {
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2) {
                var corner = at + new Vector2(x * HalfWidth, y * HalfLength).Rotated(rotation);
                if (!world.CanStand(corner) || Terrain.InLake(corner, .8f)) return false;
            }
        return true;
    }

    internal static void Expire(World world) {
        var beds = world.ThingsOf(ThingKind.StrawBed);
        for (int i = beds.Count - 1; i >= 0; i--) {
            var bed = beds[i];
            if (Left(world, bed) > 0) continue;
            world.Sleep.WakeIn(bed, "Your straw bed fell apart.");
            world.S.Things.Remove(bed);
        }
    }
}
