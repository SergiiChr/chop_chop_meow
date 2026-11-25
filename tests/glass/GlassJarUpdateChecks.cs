using System.Text.Json;
using ChopChopMeow;
using Godot;
using static Checks;
using static GlassJarSteps;
using static WorldSteps;

internal static class GlassJarUpdateChecks {
    public static void CheckGlassBlowerAndMeltReachAGuest(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var blower = HireAtGlassFurnace(world, furnace, JobKind.BlowGlass);
        var key = new EntryKey(EntryKey.Thing, furnace.Id);
        var server = new WorldUpdates();
        var receiver = new WorldUpdates();
        var baseline = server.Capture(world);
        var guest = receiver.Merge(null, server.Delta(baseline, null));
        TakeGlassBellows(world, furnace);

        var blowing = server.Capture(world);
        guest = receiver.Merge(guest, server.Delta(blowing, baseline));
        var copy = JsonSerializer.Deserialize<Thing>(guest[key], GameData.Json);
        Require(copy is { Processing: true } && copy.GlassBlower == -blower.Id && copy.Heat > 70 && copy.Tools?["steel_pipe"] == 1,
            "A guest should see the pipe, heat and worker blowing the batch.");

        TickGlassAt(world, furnace, 100, 3);
        var melted = server.Capture(world);
        guest = receiver.Merge(guest, server.Delta(melted, blowing));
        copy = JsonSerializer.Deserialize<Thing>(guest[key], GameData.Json);
        Require(copy is { Processing: false, GlassResult: GlassResult.Melted, GlassResultLeft: > 0 }
            && copy.Inventory[GlassBlowing.Item] == 0, "A guest should see the splash and no jar from the melted batch.");

        TickGlassAt(world, furnace, 80, 6);
        var settled = server.Capture(world);
        guest = receiver.Merge(guest, server.Delta(settled, melted));
        copy = JsonSerializer.Deserialize<Thing>(guest[key], GameData.Json);
        Require(copy is { GlassResult: GlassResult.None, GlassResultLeft: 0 }, "A guest should clear the splash after it finishes.");
    }
}
