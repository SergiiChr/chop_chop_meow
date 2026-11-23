using System.Collections.Generic;
using ChopChopMeow;
using static Checks;

public static class PipeWaterUpdateChecks {
    public static void CheckPipeWaterUpdatesKeepTheHostsFieldsAndOrder() {
        var key = new EntryKey(EntryKey.PipeWater, 0);
        var host = new WorldUpdates();
        var guest = new WorldUpdates();
        var before = new Snapshot { [key] = """{"100":1,"101":1,"102":1}"""u8.ToArray() };
        var copy = guest.Merge(null, host.Delta(before, null));
        foreach (var (state, change) in new[] {
            ("""{"100":1,"101":0.75,"102":1}"""u8.ToArray(), "when the water level in one tile changes"),
            ("""{"101":0.75,"102":1}"""u8.ToArray(), "when a wet tile is removed"),
            ("""{"101":0.75,"102":1,"103":1}"""u8.ToArray(), "when a wet tile is added"),
            ("""{"103":1,"102":1,"101":0.75}"""u8.ToArray(), "when wet tiles are written in a different order")
        }) {
            var current = new Snapshot { [key] = state };
            copy = guest.Merge(copy, host.Delta(current, before));
            Require(Snapshot.Same(copy[key], state), $"The guest's pipe water must match the host's {change}.");
            before = current;
        }

        var unordered = WorldUpdates.Pack(new Dictionary<string, Dictionary<int, double>> {
            ["~v:0"] = new() { [101] = .5, [103] = .25 }
        });
        var patched = guest.Merge(copy, unordered);
        Require(Snapshot.Same(patched[key], """{"103":0.25,"102":1,"101":0.5}"""u8),
            "A patch with fields in a different order should still replace both values within the baseline's order.");
    }
}
