using ChopChopMeow;
using Godot;
using static Checks;
using static SteelPipeSteps;
using static SetupSteps;
using static WorldSteps;

public static class SteelPipeChecks {
    public static void Run(GameData data) {
        Test(CheckPipesAndSurfaceObjectsShareATile, data);
        Test(CheckRemovingAPipeReturnsItsMaterialsAndNeedsReach, data);
        Test(CheckStraightPipesJoinOnlyAtMatchingEnds, data);
        Test(CheckTurningATeeRotatesItsOpenings, data);
        Test(CheckPipeTilesAndTheirRotationComeBackFromASave, data);
        Test(FarmSprayModelChecks.Run);
    }

    private static void CheckPipesAndSurfaceObjectsShareATile(GameData data) {
        var world = EmptyWorld(data);
        var tile = new Vector2I(80, 80);
        GravelPathSteps.LayPath(world, tile);
        var chest = world.Add(ThingKind.Chest, tile);
        LayPipe(world, tile + new Vector2(.25f, -.2f));

        Require(world.SteelPipes.Laid(tile) && world.GravelPaths.Laid(tile) && world.Find(chest.Id) == chest,
            "The steel pipe must be in its own layer beneath the gravel path and the chest.");

        var removal = world.Construction.RemovalAt(chest, tile, underground: true);
        Require(removal.Pipe == tile && world.Construction.InReach(removal), "The underground selector must pick the pipe.");
        Require(world.Construction.Remove(removal), "The underground selector should take up the pipe.");
        Require(!world.SteelPipes.Laid(tile) && world.GravelPaths.Laid(tile) && world.Find(chest.Id) == chest,
            "Taking out a steel pipe must leave the path and the chest above it.");
        Require(world.Construction.RemovalAt(chest, tile, underground: true) == Removal.None,
            "The underground selector must leave a surface building alone once the pipe is gone.");
    }

    private static void CheckRemovingAPipeReturnsItsMaterialsAndNeedsReach(GameData data) {
        var world = EmptyWorld(data);
        var tile = new Vector2I(80, 80);
        world.Player = tile + Vector2.Down;
        Require(world.Construction.PlacementBlock(ThingKind.SteelPiping, tile) != null,
            "A player without the pipe's materials must be unable to lay it.");
        LayPipe(world, tile);
        Require(world.Backpack["steel_pipe"] == 0, "Laying a pipe tile must spend one steel pipe.");

        Give(world, "steel_pipe");
        world.Construction.Build(ThingKind.SteelPiping, tile);
        Require(world.Backpack["steel_pipe"] == 1, "A second pipe on the tile must be refused without spending it.");

        var removal = world.Construction.RemovalAt(null, tile, underground: true);
        world.Player = tile + Vector2.Down * 16;
        Require(!world.Construction.InReach(removal) && !world.Construction.Remove(removal) && world.SteelPipes.Laid(tile),
            "A player too far away must be unable to take up the pipe.");
        world.Player = tile + Vector2.Down;
        Require(world.Construction.Remove(removal), "Reaching the pipe must let the player take it up.");
        Require(world.Backpack["steel_pipe"] == 2, "Taking up the tile must return one steel pipe.");
    }

    private static void CheckStraightPipesJoinOnlyAtMatchingEnds(GameData data) {
        var world = EmptyWorld(data);
        var tile = new Vector2I(80, 80);
        var east = new Vector2I(81, 80);
        var southeast = new Vector2I(81, 81);
        LayPipe(world, tile);
        LayPipe(world, east);
        LayPipe(world, southeast);
        LayPipe(world, new Vector2I(78, 80));

        Require(world.SteelPipes.ConnectionMask(tile) == 0b0000_0001
            && world.SteelPipes.ConnectionMask(east) == 0b0001_0000,
            "The two east–west pipes should connect at their matching ends.");
        Require(world.SteelPipes.ConnectionMask(southeast) == 0,
            "The southeast pipe should stay separate from pipes at its corner and side.");

        Require(world.Construction.Remove(Removal.OfPipe(east)), "The east pipe should come out.");
        Require(world.SteelPipes.ConnectionMask(tile) == 0,
            "Removing the east pipe should leave no connection to the southeast pipe.");
        LayPipe(world, east, Mathf.Pi / 2);
        Require(world.SteelPipes.ConnectionMask(tile) == 0 && world.SteelPipes.ConnectionMask(east) == 0,
            "An east-facing end should not join the side of a north–south pipe.");
    }

    private static void CheckTurningATeeRotatesItsOpenings(GameData data) {
        var world = EmptyWorld(data);
        var tile = new Vector2I(80, 80);
        LayPipe(world, tile + Vector2I.Up, Mathf.Pi / 2);
        LayPipe(world, tile + Vector2I.Down, Mathf.Pi / 2);
        LayPipe(world, tile + Vector2I.Right);
        LayPipe(world, tile + Vector2I.Left);
        LayPipe(world, tile + new Vector2I(1, -1));
        LayPipe(world, tile, shape: PipeShape.Tee);
        Require(world.SteelPipes.ConnectionMask(tile) == 0b0001_0101,
            "The tee should join east, south and west.");

        Require(world.Construction.Remove(Removal.OfPipe(tile)), "The tee should come out before replacing it.");
        LayPipe(world, tile, Mathf.Pi, PipeShape.Tee);
        Require(world.SteelPipes.ConnectionMask(tile) == 0b0101_0001,
            "A half-turn should open the north side in place of the south side.");

        Require(world.Construction.Remove(Removal.OfPipe(tile)), "The tee should come out before replacing it with a cross.");
        LayPipe(world, tile, shape: PipeShape.Cross);
        Require(world.SteelPipes.ConnectionMask(tile) == 0b0101_0101,
            "The cross should keep all four connected directions open.");
    }

    private static void CheckPipeTilesAndTheirRotationComeBackFromASave(GameData data) {
        var world = EmptyWorld(data);
        var tile = new Vector2I(80, 80);
        LayPipe(world, tile, Mathf.Pi / 2, PipeShape.Tee);
        string saved = world.Serialize();
        Require(world.Construction.Remove(Removal.OfPipe(tile)), "The laid pipe should come out.");
        Require(!world.SteelPipes.Laid(tile), "The test must take out the pipe after saving.");

        world.Deserialize(saved);

        Require(world.SteelPipes.RotationAt(tile) == Mathf.Pi / 2 && world.SteelPipes.ShapeAt(tile) == PipeShape.Tee,
            "Loading must bring back the pipe with its shape and rotation.");
    }
}
