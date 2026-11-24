using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;
using static GravelPathSteps;
using static PlayerSteps;

/// <summary>
/// Checks of gravel and gravel paths: crushing rocks, laying and taking up tiles, where gravel lies and how fast
/// walkers are on it.
/// </summary>
public static class GravelPathChecks {
    public static void Run(GameData data) {
        Test(CheckASteelHammerAtTheBenchCrushesAStoneIntoGravel, data);
        Test(CheckGravelCannotBeCrushedByHand, data);
        Test(CheckAPathTileCostsFourGravel, data);
        Test(CheckAPathTileGoesUnderBuildingsAndBuildingsGoOverIt, data);
        Test(CheckAPathTileKeepsOffWaterAndStandingResources, data);
        Test(CheckAPathTileClearsAStumpAway, data);
        Test(CheckATileAloneIsARoundPad, data);
        Test(CheckTilesSideBySideJoinIntoOneStrip, data);
        Test(CheckTilesCornerToCornerAreJoinedByASlantedStrip, data);
        Test(CheckATurnIsCutOnTheSlantInside, data);
        Test(CheckAStaircaseOfTilesHasStraightSlantedEdges, data);
        Test(CheckFourTilesInASquareAreGravelAllOver, data);
        Test(CheckWorkersWalkAFifthFasterOnGravel, data);
        Test(CheckThePlayerWalksAndSprintsAFifthFasterOnGravel, data);
        Test(CheckTakingUpATileGivesItsGravelBack, data);
        Test(CheckATileOutOfReachStaysDown, data);
        Test(CheckGravelThatDoesNotFitTheBackpackWaitsInABox, data);
        Test(CheckPathsComeBackFromASave, data);
    }

    private static void CheckASteelHammerAtTheBenchCrushesAStoneIntoGravel(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "stone", 1);
        var bench = BenchLeftWithAMallet(world, "gravel");
        Require(bench.Inventory["gravel"] == 0, "a wooden mallet should not crush rocks");
        SwapMalletForSteelHammer(world, bench);
        // Ten base seconds, at twice the speed at a bench and 1.8 times with a steel hammer.
        SafeTick(world, 10 / 2f / 1.8f + 1);
        Require(bench.Inventory["gravel"] == 1 && bench.Inventory["stone"] == 0, "a steel hammer at the bench should crush one stone into one gravel");
    }

    private static void CheckGravelCannotBeCrushedByHand(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "stone", 1); Give(world, "steel_hammer", 1);
        world.Craft("gravel", 1);
        SafeTick(world, 30);
        Require(world.Backpack["gravel"] == 0 && world.Backpack["stone"] == 1, "rocks should be crushed only at a bench");
    }

    private static void CheckAPathTileCostsFourGravel(GameData data) {
        var world = EmptyWorld(data);
        var said = new List<string>(); world.Notify += said.Add;
        var tile = new Vector2I(80, 80);
        world.Player = tile + new Vector2(0, 1);
        Give(world, "gravel", 7);
        world.Construction.Build(ThingKind.GravelPath, tile);
        Require(world.GravelPaths.Laid(tile) && world.Backpack["gravel"] == 3, "a path tile should take four gravel from the backpack");
        world.Construction.Build(ThingKind.GravelPath, tile + Vector2I.Right);
        Require(!world.GravelPaths.Laid(tile + Vector2I.Right) && world.Backpack["gravel"] == 3 && said.Contains("Missing materials in your backpack."),
            "a path tile should not be laid with three gravel");
    }

    private static void CheckAPathTileGoesUnderBuildingsAndBuildingsGoOverIt(GameData data) {
        var world = EmptyWorld(data);
        var under = new Vector2I(80, 80);
        var chest = world.Add(ThingKind.Chest, under);
        var yard = world.Add(ThingKind.Pile, new Vector2(90, 80));
        LayPath(world, under, new Vector2I(90, 80), new Vector2I(91, 81));
        Require(world.Find(chest.Id) != null && world.Find(yard.Id) != null, "laying a path under a building should leave the building");

        var over = new Vector2I(80, 90);
        LayPath(world, over);
        Give(world, "wood", 16);
        world.Player = over + new Vector2(-1.5f, 0);
        Require(world.Construction.Build(ThingKind.Chest, over) != null && world.GravelPaths.Laid(over), "a chest should go up on a path tile and leave the tile under it");
        LayPath(world, new Vector2I(80, 100));
        world.Player = new Vector2(80, 102.5f);
        Require(world.Construction.Build(ThingKind.Pile, new Vector2(80, 100)) != null && world.GravelPaths.Laid(new Vector2I(80, 100)), "a pile should go over a path tile and leave the tile under it");
    }

    private static void CheckAPathTileKeepsOffWaterAndStandingResources(GameData data) {
        var world = EmptyWorld(data);
        var said = new List<string>(); world.Notify += said.Add;
        Give(world, "gravel", 40);
        var tree = PlantTree(world, new Vector2(80, 80), false);
        var boulder = world.Add(ThingKind.Stone, new Vector2(82, 80), 5);
        world.Player = new Vector2(81, 81);
        foreach (var resource in new[] { tree, boulder }) {
            world.Construction.Build(ThingKind.GravelPath, resource.Pos);
            Require(!world.GravelPaths.Laid(Terrain.TileAt(resource.Pos)), $"a path tile should not be laid on a {resource.Kind}");
        }
        Require(said.Contains("A resource is in the way."), "the player should be told a resource is in the way");
        var bush = world.Add(ThingKind.Berries, new Vector2(80, 82), 0);
        var patch = world.Add(ThingKind.LeafSpot, new Vector2(82, 82), 0);
        foreach (var empty in new[] { bush, patch }) {
            world.Construction.Build(ThingKind.GravelPath, empty.Pos);
            Require(!world.GravelPaths.Laid(Terrain.TileAt(empty.Pos)), $"a path tile should not be laid on {empty.Kind} with nothing left to gather");
        }
        world.Construction.Build(ThingKind.GravelPath, new Vector2(81, 80));
        Require(world.GravelPaths.Laid(81, 80), "a path tile should go between two resources");
        world.Construction.Build(ThingKind.GravelPath, new Vector2(81, 80));
        Require(world.Backpack["gravel"] == 36 && said.Contains("Gravel is already laid here."), "a tile already laid should not be laid again");

        // Each is refused within reach of the player, so by its ground and not by its distance: water four tiles out
        // from the lake's shore, and a mountain side.
        var water = Terrain.Lake + new Vector2(Terrain.LakeRadius - 2, 0);
        var cliff = new Vector2(200, 20);
        foreach (var (ground, name) in new[] { (water, "the lake"), (cliff, "a cliff") }) {
            said.Clear();
            world.Player = ground + new Vector2(4, 0);
            world.Construction.Build(ThingKind.GravelPath, ground);
            Require(!world.GravelPaths.Laid(Terrain.TileAt(ground)) && world.Backpack["gravel"] == 36, $"a path tile should not be laid on {name}");
            Require(said.Contains("Choose dry, open ground."), $"the player should be told {name} is no ground for a path, not {string.Join(" ", said)}");
        }
    }

    private static void CheckAPathTileClearsAStumpAway(GameData data) {
        var world = EmptyWorld(data);
        var stump = Fell(world, PlantTree(world, new Vector2(80, 80), false));
        LayPath(world, new Vector2I(80, 80));
        Require(world.Find(stump.Id) == null, "a path tile laid on a stump should clear the stump away");
    }

    private static void CheckATileAloneIsARoundPad(GameData data) {
        var world = EmptyWorld(data);
        LayPath(world, new Vector2I(80, 80));
        var paths = world.GravelPaths;
        // A path is three quarters of a tile wide: gravel reaches 0.375 from the tile's middle.
        Require(paths.Covers(new Vector2(80, 80)) && paths.Covers(new Vector2(80.3f, 80)) && paths.Covers(new Vector2(80, 79.7f)), "a tile should be gravel round its middle");
        Require(!paths.Covers(new Vector2(80.45f, 80)) && !paths.Covers(new Vector2(80, 80.45f)), "gravel should end short of the tile's sides");
        // 0.42 from the middle.
        Require(!paths.Covers(new Vector2(80.3f, 80.3f)), "a tile alone should be round, not square");
    }

    private static void CheckTilesSideBySideJoinIntoOneStrip(GameData data) {
        var world = EmptyWorld(data);
        LayRow(world, new Vector2I(80, 80), 3);
        var paths = world.GravelPaths;
        Require(paths.Covers(new Vector2(80.5f, 80.3f)) && paths.Covers(new Vector2(81.5f, 79.7f)), "tiles side by side should join into one strip, with no notch where they meet");
        Require(!paths.Covers(new Vector2(80.5f, 80.45f)), "the strip should be three quarters of a tile wide");
        Require(paths.Covers(new Vector2(79.7f, 80)) && !paths.Covers(new Vector2(79.55f, 80)) && !paths.Covers(new Vector2(79.7f, 80.3f)), "the strip should have round ends");
    }

    private static void CheckTilesCornerToCornerAreJoinedByASlantedStrip(GameData data) {
        var world = EmptyWorld(data);
        LayPath(world, new Vector2I(80, 80));
        var paths = world.GravelPaths;
        // In the bare tile beside the first, 0.28 from the diagonal through both tiles' middles.
        var onStrip = new Vector2(80.7f, 80.3f);
        Require(!paths.Covers(onStrip), "the tile beside a tile alone should be bare");
        LayPath(world, new Vector2I(81, 81));
        Require(paths.Covers(new Vector2(80.5f, 80.5f)) && paths.Covers(onStrip) && paths.Covers(new Vector2(80.3f, 80.7f)), "two tiles corner to corner should be joined by a strip through the corner they share");
        // 0.57 from the diagonal.
        Require(!paths.Covers(new Vector2(80.9f, 80.1f)), "the slanted strip should be as wide as any other, not fill the tiles beside it");
    }

    private static void CheckATurnIsCutOnTheSlantInside(GameData data) {
        var world = EmptyWorld(data);
        LayPath(world, new Vector2I(80, 80), new Vector2I(81, 80), new Vector2I(81, 81));
        var paths = world.GravelPaths;
        // Half way between the two end tiles' middles, and 0.14 and 0.3 from it toward the bare tile inside the turn.
        Require(paths.Covers(new Vector2(80.5f, 80.5f)) && paths.Covers(new Vector2(80.4f, 80.6f)) && paths.Covers(new Vector2(80.29f, 80.71f)), "a turn's inside corner should be filled up to a slanted edge, as between two tiles corner to corner");
        // 0.45 from that slanted line, and the middle of the bare tile.
        Require(!paths.Covers(new Vector2(80.18f, 80.82f)) && !paths.Covers(new Vector2(80, 81)), "the slanted edge inside a turn should be as far from the line between its end tiles as any path's edge");
        // The turn's outside corner is round: 0.42 and 0.35 from the corner tile's middle.
        Require(!paths.Covers(new Vector2(81.3f, 79.7f)) && paths.Covers(new Vector2(81.25f, 79.75f)), "a turn's outside corner should be round");
    }

    private static void CheckAStaircaseOfTilesHasStraightSlantedEdges(GameData data) {
        var world = EmptyWorld(data);
        LayPath(world, new Vector2I(80, 80), new Vector2I(81, 80), new Vector2I(81, 81), new Vector2I(82, 81), new Vector2I(82, 82));
        var paths = world.GravelPaths;
        // Each side's edge runs 0.375 outside the line through that side's corner tiles: points 0.3 and 0.45 out from it, at two steps.
        Require(paths.Covers(new Vector2(80.29f, 80.71f)) && paths.Covers(new Vector2(81.29f, 81.71f)), "a staircase's notches on one side should be filled up to one slanted edge");
        Require(!paths.Covers(new Vector2(80.18f, 80.82f)) && !paths.Covers(new Vector2(81.18f, 81.82f)), "that slanted edge should run straight, three eighths of a tile out");
        Require(paths.Covers(new Vector2(81.71f, 80.29f)) && !paths.Covers(new Vector2(81.82f, 80.18f)), "a staircase's notch on the other side should be filled up to a slanted edge too");
    }

    private static void CheckFourTilesInASquareAreGravelAllOver(GameData data) {
        var world = EmptyWorld(data);
        LayPath(world, new Vector2I(80, 80), new Vector2I(81, 80), new Vector2I(81, 81));
        // In the fourth tile, 0.42 from the line between the tiles at the turn's ends and 0.28 from its own middle.
        var inFourth = new Vector2(80.2f, 80.8f);
        Require(!world.GravelPaths.Covers(inFourth), "three tiles of a square should leave the fourth's place bare past their slanted edge");
        LayPath(world, new Vector2I(80, 81));
        Require(world.GravelPaths.Covers(inFourth) && world.GravelPaths.Covers(new Vector2(80.5f, 80.5f)), "four tiles in a square should be gravel all over");
        // 0.42 from the corner tile's middle.
        Require(!world.GravelPaths.Covers(new Vector2(79.7f, 79.7f)), "a square of four tiles should have round corners");
    }

    private static void CheckWorkersWalkAFifthFasterOnGravel(GameData data) {
        var from = new Vector2I(80, 80);
        var (onGrass, grassWalker) = WorkerSentAlongARow(data, from, 0, 30);
        var (paved, gravelWalker) = WorkerSentAlongARow(data, from, 16, 30);
        SafeTick(onGrass, 4); SafeTick(paved, 4);
        float grass = grassWalker.Pos.X - from.X, onGravel = gravelWalker.Pos.X - from.X;
        // A worker walks 2.25 tiles a second: 9 tiles in four seconds on grass and 10.8 on gravel.
        Require(grass > 8.4f && grass <= 9.01f, $"a worker should walk about 9 tiles of grass in four seconds, not {grass}");
        Require(onGravel / grass > 1.19f && onGravel / grass < 1.21f, $"a worker should walk 1.2 times as far on gravel, not {onGravel / grass}");
    }

    private static void CheckThePlayerWalksAndSprintsAFifthFasterOnGravel(GameData data) {
        var world = EmptyWorld(data);
        world.Player = new Vector2(80, 80);
        Require(world.PlayerSpeed is > 4.19f and < 4.21f, $"a player should walk 4.2 tiles a second on grass, not {world.PlayerSpeed}");
        LayPath(world, new Vector2I(80, 80));
        world.Player = new Vector2(80, 80);
        Require(world.PlayerSpeed is > 5.03f and < 5.05f, $"a player should walk 5.04 tiles a second on gravel, not {world.PlayerSpeed}");
        world.Me.Sprinting = true;
        Require(world.PlayerSpeed is > 8.05f and < 8.08f, $"a player should sprint 8.064 tiles a second on gravel, not {world.PlayerSpeed}");
    }

    private static void CheckTakingUpATileGivesItsGravelBack(GameData data) {
        var world = EmptyWorld(data);
        var tile = new Vector2I(80, 80);
        LayPath(world, tile, tile + Vector2I.Right);
        Require(world.Backpack["gravel"] == 0, "laying two tiles should use up their gravel");
        Require(world.GravelPaths.TakeUp(tile) && !world.GravelPaths.Laid(tile) && world.GravelPaths.Laid(tile + Vector2I.Right), "taking up a tile should remove that tile only");
        Require(world.Backpack["gravel"] == 4, "taking up a tile should give its four gravel back");
        Require(!world.GravelPaths.TakeUp(tile) && world.Backpack["gravel"] == 4, "a tile already taken up should give nothing");
    }

    private static void CheckATileOutOfReachStaysDown(GameData data) {
        var world = EmptyWorld(data);
        var said = new List<string>(); world.Notify += said.Add;
        var tile = new Vector2I(80, 80);
        LayPath(world, tile);
        // Reach is 15 metres.
        world.Player = tile + new Vector2(15.5f, 0);
        Require(!world.GravelPaths.TakeUp(tile) && world.GravelPaths.Laid(tile) && said.Contains("Walk closer first."), "a tile out of reach should stay down");
        world.Player = tile + new Vector2(14.5f, 0);
        Require(world.GravelPaths.TakeUp(tile), "a tile within reach should come up");
    }

    private static void CheckGravelThatDoesNotFitTheBackpackWaitsInABox(GameData data) {
        var world = EmptyWorld(data);
        var tile = new Vector2I(80, 80);
        LayPath(world, tile);
        SetBackpackSlots(world, 1); Give(world, "wood", 1);
        Require(world.GravelPaths.TakeUp(tile), "a tile should come up with a full backpack");
        var box = world.Things.SingleOrDefault(t => t.Kind == ThingKind.Grave);
        Require(box != null && box.Pos == (Vector2)tile && box.Inventory["gravel"] == 4 && world.Backpack["gravel"] == 0, "gravel that does not fit the backpack should wait in a box on the tile");
    }

    private static void CheckPathsComeBackFromASave(GameData data) {
        var world = EmptyWorld(data);
        var kept = new Vector2I(80, 80); var taken = new Vector2I(81, 80);
        LayPath(world, kept, taken);
        string save = world.Serialize();
        world.GravelPaths.TakeUp(kept);
        var loaded = EmptyWorld(data);
        loaded.Deserialize(save);
        Require(loaded.GravelPaths.Laid(kept) && loaded.GravelPaths.Laid(taken) && loaded.GravelPaths.Covers(new Vector2(80.5f, 80.3f)), "a loaded game should have the paths the save had");
        world.Deserialize(save);
        Require(world.GravelPaths.Laid(kept), "loading should bring back a tile taken up since the save");
    }
}
