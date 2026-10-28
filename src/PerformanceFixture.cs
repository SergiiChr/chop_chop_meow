using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A staffed production camp used only by the local rendered performance harness.</summary>
internal static class PerformanceFixture {
    /// <summary>Creates a valley with eight assigned workers, four working stations, a fire, fuel, supplies and wage reserves.</summary>
    internal static World Create(GameData data) {
        var world = new World(data);
        world.S.Time = data.DaySeconds * .3f;
        var at = world.Player;

        Thing Place(string kind, Vector2 offset) {
            // The preferred locations sit inside the open starting clearing; a small search keeps the fixture usable if terrain changes.
            for (int ring = 0; ring <= 4; ring++)
                for (int side = -ring; side <= ring; side++)
                    for (int forward = -ring; forward <= ring; forward++) {
                        if (ring > 0 && Math.Max(Math.Abs(side), Math.Abs(forward)) != ring) continue;
                        var p = at + offset + new Vector2(side, forward) * 2.2f;
                        if (!Terrain.Open(p) || !world.CanStand(p)) continue;
                        return world.Add(kind, p);
                    }
            throw new InvalidOperationException($"No open location for performance fixture {kind}.");
        }

        var stores = new[] {
            Place("chest", new Vector2(8, 0)),
            Place("chest", new Vector2(8, 3)),
            Place("chest", new Vector2(8, -3))
        };
        var supply = stores[0].Inventory;
        supply.Add("iron_bar", 128); supply.Add("coal", 128); supply.Add("iron_ore", 128); supply.Add("wood", 128);
        var wagePost = Place("signpost", new Vector2(-7, -4));
        wagePost.Wages!.Add(Inventory.Coins, 1000);
        world.S.Backpack.Add(Inventory.Coins, 1000);

        var tools = Place("toolstation", new Vector2(-7, 0));
        tools.Inventory.Add("steel_chisel", 1);

        var forge = Place("reinforced", new Vector2(-4, 3));
        forge.Inventory.Add("iron_bar", 64); forge.Inventory.Add("coal", 64);
        forge.Burner!.Add("coal", 64); forge.Heat = 100;

        var furnaceA = Place("furnace", new Vector2(2, 3));
        var furnaceB = Place("furnace", new Vector2(5, 3));
        foreach (var furnace in new[] { furnaceA, furnaceB }) {
            furnace.Inventory.Add("iron_ore", 64);
            furnace.Burner!.Add("wood", 64);
        }

        var bench = Place("bench", new Vector2(0, -4));
        bench.Recipe = "planks";
        bench.Tools!.Add("steel_chisel", 1);
        bench.Inventory.Add("wood", 64);

        var fire = Place("campfire", new Vector2(0, 7));
        fire.Inventory.Add("wood", 64);
        fire.Burn = data.CampfireSeconds * 5;

        Worker Hire(string name, Thing target) {
            var worker = new Worker {
                Id = world.S.NextId++,
                Name = name,
                Pos = target.Pos + Vector2.Right,
                Wage = 8
            };
            world.S.Workers.Add(worker);
            world.Assign(worker, target);
            return worker;
        }

        // Two workers share each production station so hauling and processing overlap; the forge crew also operates the bellows.
        Hire("Forge keeper", forge); Hire("Forge hauler", forge);
        Hire("Smelter A", furnaceA); Hire("Smelter A hauler", furnaceA);
        Hire("Smelter B", furnaceB); Hire("Smelter B hauler", furnaceB);
        Hire("Bench worker", bench);
        Hire("Fire tender", fire);
        return world;
    }
}
