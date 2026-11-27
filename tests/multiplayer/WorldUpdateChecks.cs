using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of the world updates the host sends its guests: every entry reads back as it was.</summary>
public static class WorldUpdateChecks {
    public static void Run(GameData data) {
        Test(CheckUpdateEntriesReadBackAsTheyWere, data);
    }

    private static void CheckUpdateEntriesReadBackAsTheyWere(GameData data) {
        var world = Settlement(data);
        for (int i = 0; i < 600; i++) world.Tick(1 / 60f);
        var wolf = PlaceWolf(world, new Wolf { Health = 0, Target = 2 });
        StoreCrates(world, new Vector2(60, 110), 5);
        var entries = new WorldUpdates().Capture(world);
        foreach (var t in world.Things) RequireReadsBackAs(entries, new EntryKey(EntryKey.Thing, t.Id), t);
        foreach (var w in world.Workers) RequireReadsBackAs(entries, new EntryKey(EntryKey.Worker, w.Id), w);
        foreach (var o in world.Orders) RequireReadsBackAs(entries, new EntryKey(EntryKey.Order, o.Id), o);
        foreach (var p in world.Players) RequireReadsBackAs(entries, new EntryKey(EntryKey.Player, p.Id), p);
        RequireReadsBackAs(entries, new EntryKey(EntryKey.Wolf, wolf.Id), wolf);
        int packed = world.Things.SelectMany(t => t.Inventory.Items.Keys).Count(World.IsPacked);
        Require(packed == 5, $"the five stored crates should be packed in the chests, each reading back with its chest, not {packed}");
    }
}
