using ChopChopMeow;
using Godot;
using static Checks;
using static GlassJarSteps;
using static SetupSteps;
using static WorldSteps;

internal static class GlassJarChecks {
    public static void Run(GameData data) {
        Test(CheckGlassFurnaceNeedsAnInstalledPipe, data);
        Test(CheckPlayerBlowsGlassWithHiredBellows, data);
        Test(CheckHiredBlowerNeedsAPlayerAtTheBellows, data);
        Test(CheckSleepingByTheBellowsDoesNotWorkTheGlass, data);
        Test(CheckTwoHiredWorkersCanBlowGlass, data);
        Test(CheckTwoPlayersCanBlowGlass, data);
        Test(CheckOneWorkerCannotDoBothGlassJobs, data);
        Test(CheckGlassPausesOutsideTheHeatWindowAndNeedsTenSecondsInIt, data);
        Test(CheckGlassMeltsOnlyAfterMoreThanHalfASecondOfContinuousOverheat, data);
        Test(CheckGlassWorkSurvivesSaveAndLoad, data);
        Test(CheckFullJarOutputDoesNotHoldTheBlower, data);
        Test(CheckAPlayerWithTwoPipesInReachBlowsAtTheCloserFurnace, data);
        Test(CheckSteelStillTakesEightHeatFromASideStroke, data);
        Test(CheckGlassWorkersSayWhyWorkStopped, data);
        Test(GlassJarUpdateChecks.CheckGlassBlowerAndMeltReachAGuest, data);
    }

    private static void CheckGlassFurnaceNeedsAnInstalledPipe(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6), installPipe: false);
        HireAtGlassFurnace(world, furnace, JobKind.Bellows);
        Give(world, "steel_pipe", 2);
        world.Player = furnace.ToWorld(GlassBlowing.BlowStand);
        TickGlassAt(world, furnace, 80, 8);
        Require(furnace.Inventory[Beach.Sand] == 5 && furnace.Burner?["coal"] == 1 && !furnace.Processing,
            "Neither worker heat nor a carried pipe should let a glass batch start without the slotted pipe.");
        Require(world.SlotBlock(furnace, "stone") == "The glass blowing pipe slot takes a steel pipe.",
            "The refusal should name the slot and the item it accepts.");
        var jar = data.Recipes[GlassBlowing.Item];
        Require(!world.HasToolFor(furnace, jar), "A carried steel pipe should not make the glass recipe ready.");
        Require(world.SlotTool(furnace, "steel_pipe"), "The furnace should accept a steel pipe.");
        Require(world.HasToolFor(furnace, jar) && world.BenchTool(furnace, jar) == "steel_pipe",
            "The recipe should see the fitted steel pipe as its tool.");
        Require(world.Backpack["steel_pipe"] == 1 && world.BenchTool(furnace, GlassBlowing.Tool) == "steel_pipe",
            "Only one pipe should leave the backpack for the glass blowing pipe slot.");
        Require(!world.SlotTool(furnace, "steel_pipe"), "A second pipe should not fit the occupied glass blowing pipe slot.");
        world.UnslotTool(furnace, "steel_pipe");
        Require(world.Backpack["steel_pipe"] == 2 && world.BenchTool(furnace, GlassBlowing.Tool) == null,
            "Removing the pipe should return it to the backpack.");
    }

    private static void CheckPlayerBlowsGlassWithHiredBellows(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var pumper = HireAtGlassFurnace(world, furnace, JobKind.Bellows);
        TickGlassAt(world, furnace, 80, 8);
        Require(!furnace.Processing, "A hired bellows operator alone should not start glass.");
        world.Player = furnace.ToWorld(GlassBlowing.BlowStand);
        Work(world, 40, () => furnace.Inventory[GlassBlowing.Item] == 1);
        Require(furnace.Inventory[GlassBlowing.Item] == 1 && furnace.Burner?["coal"] == 0 && furnace.Inventory[Beach.Sand] == 0,
            $"Standing at the front should blow a jar with hired bellows, but the worker said \"{pumper.Status}\".");
        Require(world.BenchTool(furnace, GlassBlowing.Tool) == "steel_pipe", "A finished jar should leave the pipe in place.");
    }

    private static void CheckHiredBlowerNeedsAPlayerAtTheBellows(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var blower = HireAtGlassFurnace(world, furnace, JobKind.BlowGlass);
        world.Pump(furnace);
        Require(furnace.Heat == 0, "The side of a furnace is not a glass bellows position.");
        TickGlassAt(world, furnace, 80, 4);
        Require(!furnace.Processing && furnace.GlassBlower == 0, "A hired blower alone must not work the glass.");
        TakeGlassBellows(world, furnace);
        TickGlassAt(world, furnace, 80, 4);
        Require(furnace.Processing && furnace.GlassBlower == -blower.Id && blower.WorkState == WorkerWorkState.Blowing,
            "A player operating bellows should let the worker blow.");
        world.Player = furnace.Pos + Vector2.Right;
        float progress = furnace.Progress;
        TickGlassAt(world, furnace, 80, 4);
        Require(furnace.Progress == progress, "When the player leaves the rear, the worker cannot keep blowing with no bellows operator.");
    }

    private static void CheckSleepingByTheBellowsDoesNotWorkTheGlass(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var blower = HireAtGlassFurnace(world, furnace, JobKind.BlowGlass);
        world.Player = furnace.Pos + Vector2.Right * 2;
        Give(world, "leaves", 20);
        var bed = world.Construction.Build(ThingKind.StrawBed, furnace.ToWorld(new Vector2(0, -2.3f)), 0);
        Require(bed != null, "A straw bed should fit near the glass furnace bellows.");
        TickGlassAt(world, furnace, 80, 4);
        world.Player = bed.Pos;
        world.Sleep.Start(bed);
        TickGlassAt(world, furnace, 80, 4);
        Require(world.Sleep.Place == bed.Id && !furnace.Processing && furnace.Burner?["coal"] == 1,
            "A player asleep by the hot furnace must not start a glass batch with a hired blower.");
        world.Sleep.Wake();
        TickGlassAt(world, furnace, 80, 4);
        Require(furnace.Processing && furnace.GlassBlower == -blower.Id,
            "An awake player at the same spot should be able to operate the bellows.");
    }

    private static void CheckTwoHiredWorkersCanBlowGlass(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var blower = HireAtGlassFurnace(world, furnace, JobKind.BlowGlass);
        var pumper = HireAtGlassFurnace(world, furnace, JobKind.Bellows);
        world.Player = furnace.Pos + Vector2.Right * 8;
        Work(world, 45, () => furnace.Inventory[GlassBlowing.Item] == 1);
        Require(furnace.Inventory[GlassBlowing.Item] == 1,
            $"Two workers should make a jar without the player, but the blower said \"{blower.Status}\" and the pumper \"{pumper.Status}\".");
    }

    private static void CheckTwoPlayersCanBlowGlass(GameData data) {
        var (world, guest, _) = Hosted(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        world.As(guest, () => world.MoveTo(furnace.ToWorld(GlassBlowing.BlowStand)));
        TakeGlassBellows(world, furnace);
        Require(furnace.GlassBlower == guest.Id && furnace.Processing,
            "The guest at the front should blow while a different player operates the bellows.");
        TickGlassAt(world, furnace, 80, 40);
        Require(furnace.Inventory[GlassBlowing.Item] == 1, "Two players should finish a jar without a worker.");
    }

    private static void CheckOneWorkerCannotDoBothGlassJobs(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var worker = HireAtGlassFurnace(world, furnace, JobKind.Bellows);
        world.SetDuty(worker, furnace, JobKind.BlowGlass, true);
        TickGlassAt(world, furnace, 80, 40);
        Require(!furnace.Processing && furnace.Inventory[Beach.Sand] == 5 && furnace.Burner?["coal"] == 1,
            "Assigning both jobs to one worker cannot substitute for two people at opposite ends.");
    }

    private static void CheckAPlayerWithTwoPipesInReachBlowsAtTheCloserFurnace(GameData data) {
        var world = EmptyWorld(data);
        var near = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var other = GlassFurnace(world, near.Pos + new Vector2(.25f, 3.4f), rotation: Mathf.Pi);
        HireAtGlassFurnace(world, near, JobKind.Bellows);
        HireAtGlassFurnace(world, other, JobKind.Bellows);
        near.Heat = other.Heat = 80;

        world.Player = near.ToWorld(GlassBlowing.BlowStand);
        SafeTick(world, 2);
        Require(near.Progress > 0 && !other.Processing && other.Inventory[Beach.Sand] == 5,
            "One player in reach of both glass blowing pipes must make progress at only the closer furnace.");

        world.Player = other.ToWorld(GlassBlowing.BlowStand);
        float progress = near.Progress;
        SafeTick(world, 2);
        Require(near.Progress == progress && other.Progress > 0,
            "Moving closer to the other glass blowing pipe should pause the first batch and start the second.");
    }

    private static void CheckGlassPausesOutsideTheHeatWindowAndNeedsTenSecondsInIt(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        HireAtGlassFurnace(world, furnace, JobKind.BlowGlass);
        TakeGlassBellows(world, furnace);
        furnace.Progress = 0;
        TickGlassAt(world, furnace, 1300f * 80 / 1450 - 1, 4);
        Require(furnace.Progress == 0, "A glass batch should not advance below 1300 °C.");
        // The furnace cools by .8 normalized heat before reading a quarter-second tick.
        TickGlassAt(world, furnace, 1300f * 80 / 1450 + .8f, 1);
        Require(furnace.Progress == .25f, "A glass batch should advance at exactly 1300 °C.");
        TickGlassAt(world, furnace, 1600f * 80 / 1450 + .8f, 1);
        Require(furnace.Progress == .5f, "A glass batch should advance at exactly 1600 °C.");
        TickGlassAt(world, furnace, 80, 37);
        Require(furnace.Inventory[GlassBlowing.Item] == 0 && furnace.Progress > 9.7f,
            "Less than ten seconds at temperature should not finish a jar.");
        TickGlassAt(world, furnace, 80, 1);
        Require(furnace.Inventory[GlassBlowing.Item] == 1 && furnace.GlassResult == GlassResult.Success,
            "Ten seconds of working heat should finish exactly one jar and show the successful result.");
    }

    private static void CheckGlassMeltsOnlyAfterMoreThanHalfASecondOfContinuousOverheat(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6), batches: 2);
        HireAtGlassFurnace(world, furnace, JobKind.BlowGlass);
        TakeGlassBellows(world, furnace);
        Require(furnace.Processing && furnace.Inventory[Beach.Sand] == 5 && furnace.Burner?["coal"] == 1,
            "Starting the first batch should consume just its five sand and one coal.");
        float progress = furnace.Progress;
        TickGlassAt(world, furnace, 100, 2);
        Require(furnace.Processing && furnace.Progress == progress && furnace.GlassResult != GlassResult.Melted,
            "Exactly half a second above the heat window should pause, not melt.");
        TickGlassAt(world, furnace, 80, 1);
        TickGlassAt(world, furnace, 100, 2);
        Require(furnace.Processing, "Going back in range should reset the half-second overheat timer.");
        TickGlassAt(world, furnace, 100, 1);
        Require(!furnace.Processing && furnace.GlassResult == GlassResult.Melted && furnace.Inventory[GlassBlowing.Item] == 0,
            "More than half a second of continuous overheat should melt the first batch without producing a jar.");
        Require(furnace.Inventory[Beach.Sand] == 5 && furnace.Burner?["coal"] == 1,
            "Melting the first batch should not consume the backup batch.");
        TickGlassAt(world, furnace, 100, 6);
        Require(!furnace.Processing && furnace.Inventory[Beach.Sand] == 5 && furnace.Burner?["coal"] == 1,
            "Even after the splash, the backup batch must wait until heat returns to range.");
        TickGlassAt(world, furnace, 80, 39);
        Require(furnace.Processing && furnace.Inventory[GlassBlowing.Item] == 0,
            "The backup batch should start from scratch, not pick up the melted batch's progress.");
        TickGlassAt(world, furnace, 80, 2);
        Require(furnace.Inventory[GlassBlowing.Item] == 1 && furnace.Burner?["coal"] == 0 && furnace.Inventory[Beach.Sand] == 0,
            "The next five sand and one coal should make one jar without another recipe order.");
    }

    private static void CheckGlassWorkSurvivesSaveAndLoad(GameData data) {
        var world = EmptyWorld(data);
        var forge = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        HireAtGlassFurnace(world, forge, JobKind.BlowGlass);
        TakeGlassBellows(world, forge);
        TickGlassAt(world, forge, 80, 12);
        TickGlassAt(world, forge, 100, 1);
        Require(forge.Progress > 0 && forge.GlassOverheat == .25f, "The original batch must have work and overheat to save.");

        var loaded = EmptyWorld(data);
        loaded.Deserialize(world.Serialize());
        var copy = loaded.Find(forge.Id)!;
        Require(copy.Processing && copy.Progress == forge.Progress && copy.GlassOverheat == .25f
            && loaded.BenchTool(copy, GlassBlowing.Tool) == "steel_pipe", "Loading should restore the batch, its overheat time and the fitted pipe.");
        TickGlassAt(loaded, copy, 80, 44);
        Require(copy.Inventory[GlassBlowing.Item] == 1,
            "Cooling a restored batch should let it finish without another load of sand or coal.");
    }

    private static void CheckFullJarOutputDoesNotHoldTheBlower(GameData data) {
        var world = EmptyWorld(data);
        var forge = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        HireAtGlassFurnace(world, forge, JobKind.Bellows);
        world.Player = forge.ToWorld(GlassBlowing.BlowStand);
        int full = forge.Inventory.SlotRoom(GlassBlowing.Item);
        forge.Inventory.Add(GlassBlowing.Item, full);
        for (int i = 0; i < 48 && forge.Progress < 10; i++) TickGlassAt(world, forge, 80, 1);
        Require(forge.Processing && forge.Progress == 10 && forge.GlassResult == GlassResult.Success
            && forge.GlassBlower == world.Me.Id && forge.GlassResultLeft > 0,
            "The jar should finish with the blower's success pose even when its output slot is full.");
        TickGlassAt(world, forge, 80, 4);
        Require(forge.Processing && forge.GlassResult == GlassResult.None && forge.GlassBlower == 0,
            "Once the success pose ends, a jar waiting for output room must release the blower.");

        world.Player = forge.Pos + Vector2.Right * 3;
        Require(world.Transfer(forge.Inventory, world.Backpack, GlassBlowing.Item, 1) == 1, "Take one jar from the full stack.");
        TickGlassAt(world, forge, 80, 1);
        Require(!forge.Processing && forge.GlassBlower == 0 && forge.Inventory[GlassBlowing.Item] == full,
            "The completed jar should fill the freed space even after the blower walks away.");
    }

    public static void CheckAWorkingGlassCrewAllocatesNothing(GameData data) {
        var world = EmptyWorld(data);
        var furnace = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        HireAtGlassFurnace(world, furnace, JobKind.Bellows);
        HireAtGlassFurnace(world, furnace, JobKind.BlowGlass);
        world.Player = furnace.Pos + Vector2.Right * 8;
        Work(world, 12, () => furnace.Progress > 0);
        Require(furnace.Progress > 0, "The glass crew should start the allocation fixture.");

        // Keep all the measured ticks within the same batch, before finishing can add the new product.
        AllocationChecks.Check("tick: glass blower and bellows crew", () => world.Tick(1 / 60f), runs: 400);
        Require(furnace.Processing && furnace.Progress > 7, "Both workers should have kept blowing for the measured ticks.");
    }

    private static void CheckGlassWorkersSayWhyWorkStopped(GameData data) {
        var world = EmptyWorld(data);
        var forge = GlassFurnace(world, Terrain.Spawn + new Vector2(6, 6));
        var blower = HireAtGlassFurnace(world, forge, JobKind.BlowGlass);
        var pumper = HireAtGlassFurnace(world, forge, JobKind.Bellows);
        Work(world, 40, () => forge.Inventory[GlassBlowing.Item] == 1);
        SafeTick(world, 2);
        Require(blower.Status == "Needs sand" && pumper.Status == "Needs sand",
            "After finishing the last jar, both workers should say they need sand.");

        Give(world, Beach.Sand, 5);
        world.LoadInput(forge, Beach.Sand, 5);
        SafeTick(world, 1);
        Require(blower.Status == "Needs coal in the fuel slot" && pumper.Status == "Needs coal in the fuel slot",
            "Sand without fuel should make both glass workers say they need coal.");

        world.UnslotTool(forge, "steel_pipe");
        SafeTick(world, 1);
        Require(blower.Status == "Needs a steel pipe in the Glass blowing pipe slot" && blower.Status == pumper.Status,
            "Both workers should tell the player when the steel pipe is no longer fitted.");

        forge.Inventory.Remove(Beach.Sand, 5);
        forge.Inventory.Remove(GlassBlowing.Item, 1);
        world.SetRecipe(forge, "steel_bar");
        Require(world.Selected(forge) == "steel_bar", "The emptied furnace should switch back to steel.");
        SafeTick(world, 1);
        Require(blower.Status == "Furnace is not making glass jars" && pumper.Status == "Nothing to smelt",
            "A furnace switched back to steel should give each worker the current reason for stopping.");
    }

    private static void CheckSteelStillTakesEightHeatFromASideStroke(GameData data) {
        var world = EmptyWorld(data);
        var furnace = world.Add(ThingKind.Reinforced, Terrain.Spawn + new Vector2(6, 6));
        world.Player = furnace.Pos + Vector2.Right;
        world.Pump(furnace);
        Require(furnace.Heat == 8, "The side of the furnace should still allow a steel stroke, adding 145 °C.");
    }
}
