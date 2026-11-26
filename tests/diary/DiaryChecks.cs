using System;
using System.Linq;
using System.Text.Json.Nodes;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static SandSteps;
using static DiarySteps;

/// <summary>
/// Checks of the diary: where its pages turn up, who sees a page lying in the world, the pop-up a find waits behind, reading, saves and its goal,
/// and the lake's shallows, where people wade but nothing is built.
/// </summary>
public static class DiaryChecks {
    public static void Run(GameData data) {
        Test(CheckChoppingDropsAPageBesideTheTreeForTheChopperAlone, data);
        Test(CheckPickingUpAPageStopsThePlayerUntilTheyTakeIt, data);
        Test(CheckBerriesLeavesAndSandEachTurnUpTheirPageAtOnce, data);
        Test(CheckWorkersTurnUpNoPage, data);
        Test(CheckTheLakePageFloatsWithinWadingReachAndStaysForOthers, data);
        Test(CheckEveryoneWadesTwoMetresIntoTheLake, data);
        Test(CheckOpeningADayMarksItRead, data);
        Test(CheckAFallenPageWaitsForItsPlayerToComeBack, data);
        Test(CheckAGuestHeldByAFindIsNotToldTheyAreBusy, data);
        Test(CheckBuildingOverAFallenPageClearsItForAnotherChop, data);
        Test(CheckBuildingsAreWorkedFromDryGroundOnly, data);
        Test(CheckFindsAndReadDaysComeBackFromASave, data);
        Test(CheckASaveWithABrokenDiaryIsRefused, data);
        Test(CheckTheDiaryGoalCountsEachPlayersOwnPages, data);
    }

    private static void CheckChoppingDropsAPageBesideTheTreeForTheChopperAlone(GameData data) {
        var world = DiaryWorld(data);
        var tree = world.Add(ThingKind.Tree, Terrain.Spawn + new Vector2(10, -10), 1000);
        var guest = world.Arrive("guest", 2);
        var page = ChopUntilAPageFalls(world, tree);
        float off = page.Pos.DistanceTo(tree.Pos);
        Require(off >= Diary.FallNear - .01f && off <= Diary.FallFar + .01f && world.CanStand(page.Pos), $"a page should land on open ground beside the trunk: {off:0.00} m off");
        Require(Diary.ShowsTo(world.Me, page) && !Diary.ShowsTo(guest, page), "a page off a tree should show to the player who chopped and to nobody else");
        Require(world.Me.DiaryFinding == DiaryFind.None && !world.Occupied, "a page falling should leave the player free until they pick it up");
        for (int i = 0; i < 100; i++) world.Gather(tree, data.GatherSeconds);
        Require(world.ThingsOf(ThingKind.FallenPage).Count == 1, "no second page should fall while the first one lies there");
    }

    private static void CheckPickingUpAPageStopsThePlayerUntilTheyTakeIt(GameData data) {
        var world = DiaryWorld(data);
        var tree = world.Add(ThingKind.Tree, Terrain.Spawn + new Vector2(10, -10), 1000);
        var page = ChopUntilAPageFalls(world, tree);
        PickUpPage(world, page);
        Require(world.Me.DiaryFinding == DiaryFind.Tree && world.Find(page.Id) == null, "picking up a fallen page should bring up its pop-up and take it off the ground");
        var stood = world.Player;
        world.MovePlayer(Vector2.Right);
        Require(world.Player == stood && world.Occupied, "a player with a pop-up up should be busy and stay where they stand");
        int wood = world.Backpack["wood"];
        world.Gather(tree, data.GatherSeconds);
        Require(world.Backpack["wood"] == wood, "a busy player should gather nothing");
        world.Diary.Take();
        Require(world.Me.DiaryFinding == DiaryFind.None && !world.Occupied, "taking the page should end the pop-up");
        Require(Diary.Has(world.Me, 36) && Diary.Has(world.Me, 37) && !Diary.Has(world.Me, 2), "the page off a tree should hold days 36 and 37 and no other");
        Require(Diary.Unread(world.Me, 36) && Diary.Unread(world.Me, 37), "a day just found should be unread");
    }

    private static void CheckBerriesLeavesAndSandEachTurnUpTheirPageAtOnce(GameData data) {
        var world = DiaryWorld(data);
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        GatherUntilFound(world, bush, DiaryFind.Berries);
        Require(world.Occupied, "a page found in a bush should stop the player at once with its pop-up");
        world.Diary.Take();
        var leaves = world.Add(ThingKind.LeafSpot, Terrain.Spawn + new Vector2(16, -10), 10000);
        GatherUntilFound(world, leaves, DiaryFind.Leaves);
        world.Diary.Take();
        Require(Diary.Has(world.Me, 40) && Diary.Has(world.Me, 10), "the bush's page should hold day 40 and the leaves' page day 10");
        for (int i = 0; i < 100; i++) world.Gather(bush, data.GatherSeconds);
        Require(world.Me.DiaryFinding == DiaryFind.None, "a bush should turn up no page for a player who has it");

        EquipBeachShovel(world);
        RunUntil(world, 4 * data.DaySeconds);
        int dug = 0;
        for (int i = 0; i < 40 * 40 && world.Me.DiaryFinding == DiaryFind.None; i++) {
            var square = SandSquare + new Vector2(i % 40 - 20, i / 40 - 20);
            if (!Beach.IsSand(square) || world.Holes.Block(square) != null) continue;
            world.Player = square + Vector2.Left;
            DigSand(world, square, ++dug);
        }
        Require(world.Me.DiaryFinding == DiaryFind.Sand, $"a page should turn up in the sand within {dug} squares dug");
        world.Diary.Take();
        Require(Diary.Has(world.Me, 6), "the sand's page should hold day 6");
    }

    private static void CheckWorkersTurnUpNoPage(GameData data) {
        var world = DiaryWorld(data);
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        TakeEverything(world, world.Add(ThingKind.Chest, Terrain.Spawn + new Vector2(13, -10)));
        var worker = AddWorker(world, bush.Pos);
        world.Assign(worker, bush);
        world.Player = Terrain.Spawn + new Vector2(0, -40);
        SafeTick(world, 150);
        Require(bush.Remaining <= 10000 - 60, $"the worker should pick berries many times over: {10000 - bush.Remaining} picked");
        Require(world.Me.DiaryFinding == DiaryFind.None && world.Me.DiaryFinds.Count == 0, "a worker's picking should turn up no page for anybody");
    }

    private static void CheckTheLakePageFloatsWithinWadingReachAndStaysForOthers(GameData data) {
        var world = new World(data);
        var page = world.ThingsOf(ThingKind.FloatingPage).Single();
        var guest = world.Arrive("guest", 2);
        for (float time = 0; time < 2 * data.DaySeconds; time += 7) {
            var at = Diary.FloatAt(page, time);
            var from = at - Terrain.Lake;
            float inward = Terrain.Shore(MathF.Atan2(from.Y, from.X)) - from.Length();
            Require(inward >= Diary.FloatsFrom - .01f && inward <= Diary.FloatsTo + .01f, $"the lake's page should drift {Diary.FloatsFrom} to {Diary.FloatsTo} m in from the shore: {inward:0.00} m");
            Require(inward - PageReach <= Terrain.Wade, $"a player wading in should reach the lake's page: {inward:0.00} m in");
        }
        Require(Diary.ShowsTo(world.Me, page) && Diary.ShowsTo(guest, page), "the lake's page should show to every player who has not taken it");
        PickUpPage(world, page);
        Require(world.Me.DiaryFinding == DiaryFind.Lake, "picking up the lake's page should bring up its pop-up");
        world.Diary.Take();
        Require(Diary.Has(world.Me, 2) && world.Find(page.Id) == page, "the lake's page should hold day 2 and stay on the lake for the others");
        Require(!Diary.ShowsTo(world.Me, page) && Diary.ShowsTo(guest, page), "the lake's page should be gone for the player who took it alone");
        world.Gather(page, 0);
        Require(world.Me.DiaryFinding == DiaryFind.None, "a player who took the lake's page should not pick it up again");
    }

    private static void CheckEveryoneWadesTwoMetresIntoTheLake(GameData data) {
        var world = EmptyWorld(data);
        foreach (float angle in new[] { 0f, 1.7f, 3.4f, 5.1f }) {
            var way = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float shore = Terrain.Shore(angle);
            Require(world.CanStand(Terrain.Lake + way * (shore - Terrain.Wade + .1f)), $"anyone should stand in the shallows {Terrain.Wade} m in from the shore");
            Require(!world.CanStand(Terrain.Lake + way * (shore - Terrain.Wade - .1f)), $"nobody should wade further than {Terrain.Wade} m in");
        }
    }

    private static void CheckOpeningADayMarksItRead(GameData data) {
        var world = DiaryWorld(data);
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        GatherUntilFound(world, bush, DiaryFind.Berries);
        world.Diary.Read(40);
        world.Diary.Take();
        Require(Diary.Unread(world.Me, 40), "a day opened while it still waited in its pop-up should be unread once taken");
        world.Diary.Read(40);
        Require(!Diary.Unread(world.Me, 40) && !Diary.AnyUnread(world.Me), "opening a day should mark it read");
        world.Diary.Read(2);
        Require(!world.Me.DiaryRead.Contains(2), "a day not found should not be read");
    }

    private static void CheckAFallenPageWaitsForItsPlayerToComeBack(GameData data) {
        var world = DiaryWorld(data);
        var guest = world.Arrive("guest", 2);
        Give(guest, "steel_axe");
        var tree = world.Add(ThingKind.Tree, Terrain.Spawn + new Vector2(10, -10), 1000);
        var page = world.As(guest, () => ChopUntilAPageFalls(world, tree));
        world.Depart(guest.Id);
        Require(world.Find(page.Id) == page, "a page off a tree should stay where it fell when its player leaves");
        var back = world.Arrive("guest", 9);
        Require(Diary.ShowsTo(back, page) && !Diary.ShowsTo(world.Me, page), "a player back in the game should find their page where it fell");
    }

    /// <summary>A guest's window keeps sending the gather they hold until the host's word of their find reaches it, and the host lets that go quietly.</summary>
    private static void CheckAGuestHeldByAFindIsNotToldTheyAreBusy(GameData data) {
        var world = DiaryWorld(data);
        var guest = world.Arrive("guest", 2);
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        guest.Pos = bush.Pos + Vector2.Right;
        TurnUpFind(guest, DiaryFind.Berries);
        guest.GatherTarget = bush.Id;
        int told = 0;
        world.Relay += (to, message) => { if (to == guest && message == "You are busy.") told++; };
        world.Tick(.1f);
        Require(told == 0, $"a guest whose find waits should not be told they are busy by the gather their window still sends: told {told} times");
    }

    private static void CheckBuildingOverAFallenPageClearsItForAnotherChop(GameData data) {
        var world = DiaryWorld(data);
        var guest = world.Arrive("guest", 2);
        Give(guest, "steel_axe");
        var page = DropPage(world, guest, world.Player + new Vector2(3, 0));
        world.Depart(guest.Id);
        Give(world, "wood", 6); Give(world, "stone", 4);
        Require(world.Construction.Build(ThingKind.Campfire, page.Pos) != null, "the host should build a campfire over a page they cannot see");
        Require(world.Find(page.Id) == null, "a building over a fallen page should clear the page away");
        var back = world.Arrive("guest", 9);
        var tree = world.Add(ThingKind.Tree, Terrain.Spawn + new Vector2(10, -10), 1000);
        world.As(back, () => ChopUntilAPageFalls(world, tree));
    }

    private static void CheckBuildingsAreWorkedFromDryGroundOnly(GameData data) {
        var world = EmptyWorld(data);
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var intake = (Vector2)Terrain.TileAt(bank + Vector2.Down * 1.1f);
        world.Player = intake + new Vector2(0, -3);
        Give(world, "steel_bar", 4); Give(world, "stone", 10); Give(world, "wood_parts", 4);
        // Turned along the shore, the pump's handle comes in the shallows, where people wade.
        var handle = intake + World.BellowsStand.Rotated(Mathf.Pi / 2);
        Require(world.CanStand(handle) && Terrain.InLake(handle), "a handle turned along the shore should come in the shallows");
        Require(world.Construction.PlacementBlock(ThingKind.Pump, intake, rotation: Mathf.Pi / 2) == "Turn the handle toward open ground on the bank.",
            "a pump's handle should be worked from dry ground, not the shallows");
        // A furnace on dry ground just up the bank, turned with its bellows toward the lake.
        var furnace = bank + Vector2.Up;
        world.Player = furnace + new Vector2(3, -1);
        var bellows = furnace + World.BellowsStand.Rotated(Mathf.Pi);
        Require(world.CanStand(bellows) && Terrain.InLake(bellows), "a furnace's bellows turned toward the lake should come in the shallows");
        Require(world.Construction.PlacementBlock(ThingKind.Reinforced, furnace, rotation: Mathf.Pi) == "Leave dry, open ground at the furnace's bellows.",
            "a furnace's bellows should be worked from dry ground, not the shallows");
    }

    private static void CheckFindsAndReadDaysComeBackFromASave(GameData data) {
        var world = DiaryWorld(data);
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        GatherUntilFound(world, bush, DiaryFind.Berries);
        world.Diary.Take();
        world.Diary.Read(40);
        var tree = world.Add(ThingKind.Tree, Terrain.Spawn + new Vector2(16, -10), 1000);
        var page = ChopUntilAPageFalls(world, tree);
        var leaves = world.Add(ThingKind.LeafSpot, Terrain.Spawn + new Vector2(22, -10), 10000);
        GatherUntilFound(world, leaves, DiaryFind.Leaves);
        string save = world.Serialize();
        world.Diary.Take();
        world.Diary.Read(10);
        var loaded = DiaryWorld(data);
        loaded.Deserialize(save);
        var me = loaded.Me;
        Require(me.DiaryFinds.SequenceEqual([DiaryFind.Berries]) && me.DiaryRead.SequenceEqual([40]), "a load should bring back the finds taken and the days read");
        Require(me.DiaryFinding == DiaryFind.Leaves && loaded.Occupied, "a load should bring back a pop-up still waiting");
        Require(loaded.Find(page.Id) is { Kind: ThingKind.FallenPage } && Diary.ShowsTo(me, loaded.Find(page.Id)!), "a load should bring back the page lying where it fell, for its player");
    }

    /// <summary>A save whose diary holds an unknown find, a day read but not found or a fallen page that is not there is refused with a clear reason instead of failing mid-game.</summary>
    private static void CheckASaveWithABrokenDiaryIsRefused(GameData data) {
        var world = DiaryWorld(data);
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        GatherUntilFound(world, bush, DiaryFind.Berries);
        world.Diary.Take();
        world.Diary.Read(40);
        string save = world.Serialize();
        DiaryWorld(data).Deserialize(save);
        foreach (var (field, broken) in new (string, JsonNode?)[] {
            ("DiaryFinds", null), ("DiaryFinds", new JsonArray(99)), ("DiaryFinds", new JsonArray((int)DiaryFind.Berries, (int)DiaryFind.Berries)),
            ("DiaryRead", new JsonArray(2)), ("DiaryFinding", (int)DiaryFind.Berries), ("FallenPage", 99999),
        }) {
            var edited = JsonNode.Parse(save)!;
            var player = edited["Party"]!.AsObject().First().Value!;
            Require(player[field] != null || field is "DiaryFinding" or "FallenPage", $"save fixture field {field} is missing");
            player[field] = broken;
            bool refused = false;
            try { DiaryWorld(data).Deserialize(edited.ToJsonString()); } catch (InvalidOperationException error) { refused = error.Message.Contains("out of range"); }
            Require(refused, $"a save whose diary has {field} set to {broken?.ToJsonString() ?? "null"} should be refused with a clear reason");
        }
    }

    private static void CheckTheDiaryGoalCountsEachPlayersOwnPages(GameData data) {
        var world = DiaryWorld(data);
        Require(world.Goals.Status(GoalId.Diary) == GoalStatus.Hidden, "the diary's goal should stay hidden until a page is found");
        var bush = world.Add(ThingKind.Berries, Terrain.Spawn + new Vector2(10, -10), 10000);
        GatherUntilFound(world, bush, DiaryFind.Berries);
        Require(world.Goals.Status(GoalId.Diary) == GoalStatus.Hidden, "a page still in its pop-up should not show the goal yet");
        world.Diary.Take();
        Require(world.Goals.Status(GoalId.Diary) == GoalStatus.Active && Diary.FoundDays(world.Me) == 1, "taking the first page should show the diary's goal with one of its pages found");
        TakeEveryFind(world.Arrive("guest", 2));
        Require(world.Goals.Status(GoalId.Diary) == GoalStatus.Active, "another player's pages should not count toward this player's goal");
        TakeEveryFind(world.Me);
        Require(world.Goals.Status(GoalId.Diary) == GoalStatus.Completed && Diary.FoundDays(world.Me) == 11, "every part of the diary taken should hold all 11 days and complete the goal");
    }
}
