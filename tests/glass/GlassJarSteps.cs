using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorkerSteps;
using static WorldSteps;

internal static class GlassJarSteps {
    public static Thing GlassFurnace(World world, Vector2 at, int batches = 1, bool installPipe = true) {
        var furnace = world.Add(ThingKind.Reinforced, at);
        world.Player = at + Vector2.Right;
        world.SetRecipe(furnace, GlassBlowing.Item);
        Require(world.Selected(furnace) == GlassBlowing.Item, "The reinforced furnace should switch to glass jars.");
        if (batches > 0) {
            Give(world, Beach.Sand, 5 * batches);
            Give(world, "coal", batches);
            world.LoadInput(furnace, Beach.Sand, 5 * batches);
            world.LoadFuel(furnace, "coal", batches);
            Require(furnace.Inventory[Beach.Sand] == 5 * batches && furnace.Burner?["coal"] == batches,
                "The glass furnace should take five sand and a separate unit of fuel per batch.");
        }
        if (installPipe) {
            Give(world, "steel_pipe");
            Require(world.SlotTool(furnace, "steel_pipe"), "A steel pipe should install directly in the furnace.");
        }
        return furnace;
    }

    public static Worker HireAtGlassFurnace(World world, Thing furnace, JobKind job) {
        var worker = Staff(world, furnace, job);
        worker.Pos = furnace.ToWorld(job == JobKind.BlowGlass ? GlassBlowing.BlowStand : World.BellowsStand);
        Require(World.HasDuty(worker, furnace.Id, job), $"The reinforced furnace should offer the {job} job.");
        return worker;
    }

    public static void TakeGlassBellows(World world, Thing furnace) {
        world.Player = furnace.ToWorld(World.BellowsStand);
        SafeTick(world, .5f);
        furnace.Heat = 72;
        world.Pump(furnace);
        Require(furnace.Heat == 80, "One manual glass stroke should add eight heat, or 145 °C.");
        SafeTick(world, .5f);
    }

    /// <summary>Holds a test temperature rather than relying on strokes and cooling during a measured span.</summary>
    public static void TickGlassAt(World world, Thing furnace, float heat, int ticks, float dt = .25f) {
        for (int i = 0; i < ticks; i++) {
            furnace.Heat = heat;
            world.Tick(dt);
        }
    }
}
