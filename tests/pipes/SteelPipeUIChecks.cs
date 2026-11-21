using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static SteelPipeSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task RunSteelPipeUIChecks() {
        await TestClean(CheckHeldPipePlanTurnsAndCyclesShapes);
        await TestClean(CheckRemoveSelectorKeepsTheOtherLayer);
        await TestClean(CheckInspectReadsWaterAndPressureWhileUnderground);
        await TestClean(CheckPuttingPipesInThePlumbingSlotInstallsAndRefundsThem);
    }

    private async Task CheckHeldPipePlanTurnsAndCyclesShapes() {
        Give(Hosted, "steel_pipe", 2);
        await KeyStroke(Key.B);
        await PickUp(Named("build_steel_piping")!);
        Require(cursor.Plan == "steel_piping", "Clicking a pipe plan should put it on the cursor.");
        var tiles = SpotsNearby().Select(Terrain.TileAt).Distinct()
            .Where(t => world.Construction.PlacementBlock(ThingKind.SteelPiping, t) == null && Uncovered(view.Screen(t)))
            .Take(2).ToArray();
        Require(tiles.Length == 2, "The clearing should have two visible tiles for pipes.");
        await Hover(view.Screen(tiles[0]));
        var ghost = view.FindChild("PipePreview", true, false) as MeshInstance3D;
        Require(view.Underground && ghost is { Visible: true } && ghost.Basis.X.X > .999f, "The straight pipe should start with an east-west preview.");
        await KeyStroke(Key.V);
        Require(ghost!.Visible && Mathf.Abs(ghost.Basis.X.Z) > .999f, "V should turn the straight pipe preview a quarter turn.");

        await Hover(pipeShapeButton);
        await Click(pipeShapeButton);
        Require(cursor.Plan == "steel_piping" && pipeShapeButton.Text == "T-junction", "The shape button should turn the held plan into a T-junction.");
        await Click(pipeShapeButton);
        Require(pipeShapeButton.Text == "Cross junction", "The T-junction should give way to a cross junction.");
        await Click(pipeShapeButton);
        Require(pipeShapeButton.Text == "Straight pipe", "The cross junction should give way to a straight pipe.");
        await Click(pipeShapeButton);

        await Click(view.Screen(tiles[0]));
        await Until(() => world.SteelPipes.ShapeAt(tiles[0]) == PipeShape.Tee, "the selected T-junction going underground", 2);
        Require(world.SteelPipes.RotationAt(tiles[0]) == Mathf.Pi / 2, "The placed T-junction should keep the quarter turn.");
        await Click(pipeShapeButton);
        await Click(view.Screen(tiles[1]));
        await Until(() => world.SteelPipes.ShapeAt(tiles[1]) == PipeShape.Cross, "the next pipe going underground as a cross junction", 2);
        Require(world.SteelPipes.RotationAt(tiles[1]) == Mathf.Pi / 2, "The next pipe should keep the quarter turn.");

        foreach (var tile in tiles) Hosted.Construction.TakeUpPipe(tile);
        Drop(Hosted, "steel_pipe", 2);
        await CloseWithEsc(); await Frames();
    }

    private async Task CheckRemoveSelectorKeepsTheOtherLayer() {
        Give(Hosted, "gravel", 4);
        Give(Hosted, world.Data.Buildings[ThingKind.Chest].Cost);
        await Frames();
        var tile = SpotsNearby().Select(Terrain.TileAt)
            .First(t => world.GravelPaths.Block(t) == null && world.Construction.PlacementBlock(ThingKind.Chest, t) == null
                && Uncovered(view.Screen(t)) && Uncovered(view.Screen(t, .4f)));
        Hosted.Construction.Build(ThingKind.GravelPath, tile);
        var chest = Hosted.Construction.Build(ThingKind.Chest, tile);
        Require(chest != null, "The chest should be built over the gravel path.");
        LayPipe(Hosted, tile);
        await Frames();

        await KeyStroke(Key.B);
        await Click(Named("remove")!);
        await RightClick(tile);
        await Hover(view.Screen(tile));
        Require(view.Underground && view.RemovalShown.Pipe == tile, "Underground removal should mark the pipe beneath the chest.");
        await Click(view.Screen(tile));
        await Until(() => !world.SteelPipes.Laid(tile), "the pipe coming up from beneath the chest", 2);
        Require(world.Find(chest!.Id) != null && world.GravelPaths.Laid(tile), "Taking up the pipe should leave the chest and gravel on the surface.");
        Require(world.Backpack["steel_pipe"] == 1, "Taking up the pipe should return it to the backpack.");

        Hosted.Construction.Build(ThingKind.SteelPiping, tile);
        await RightClick(tile);
        await ClickOn(chest);
        await Until(() => world.Find(chest.Id) == null, "the chest coming down after returning to the surface", 2);
        Require(world.SteelPipes.Laid(tile) && world.GravelPaths.Laid(tile), "Taking down the chest should leave both tiles underneath it.");
        await Hover(view.Screen(tile));
        Require(!view.Underground && view.RemovalShown.Tile == tile, "Surface removal should mark the gravel above the pipe.");
        await Click(view.Screen(tile));
        await Until(() => !world.GravelPaths.Laid(tile), "the gravel coming up from above the pipe", 2);
        Require(world.SteelPipes.Laid(tile), "Taking up the gravel should leave the pipe in the ground.");

        Hosted.Construction.TakeUpPipe(tile);
        Drop(Hosted, "steel_pipe"); Drop(Hosted, "gravel", 4); Drop(Hosted, "wood", 8);
        await KeyStroke(Key.Escape); await CloseWithEsc(); await Frames();
    }

    private async Task CheckInspectReadsWaterAndPressureWhileUnderground() {
        var home = world.Player;
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var tile = Terrain.TileAt(bank + Vector2.Down * 1.1f);
        var pump = Hosted.Add(ThingKind.Pump, tile);
        Warp(Hosted, pump.ToWorld(World.BellowsStand)); await Frames(4);
        LayPipe(Hosted, tile); await Frames();
        await KeyStroke(Key.B);
        await Click(Named("remove")!);
        await RightClick(tile);
        await Hover(view.Screen(tile));
        await KeyStroke(Key.E);
        Require(view.Underground && ShownPage() == "Steel pipe", "E over a pipe should inspect it and keep the underground layer visible.");
        Require(HasLabel(panel, "Empty · 0/1 L") && HasLabel(panel, "Pressure 0%"), "A dry pipe should show no water and no pressure.");

        for (int i = 0; i < 10; i++) Hosted.Pump(pump);
        Hosted.Tick(.3f);
        await Until(() => HasLabel(panel, "Full · 1/1 L") && HasLabel(panel, "Pressure 100%"),
            "the inspected pipe filling and displaying pump pressure", 2);
        Hosted.Tick(11);
        await Until(() => HasLabel(panel, "Full · 1/1 L") && HasLabel(panel, "Pressure 0%"),
            "the inspected pipe retaining its water as pressure falls", 2);

        await KeyStroke(Key.Escape); await CloseWithEsc();
        Hosted.Construction.TakeUpPipe(tile);
        Drop(Hosted, "steel_pipe");
        RemoveThings(Hosted, t => t == pump);
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckPuttingPipesInThePlumbingSlotInstallsAndRefundsThem() {
        var spot = SpotsNearby().First(p => world.CanStand(p) && Uncovered(view.Screen(p, .4f)) && view.Pick(view.Screen(p, .4f)) == null);
        var table = Hosted.Add(ThingKind.Cooktable, spot);
        Give(Hosted, "steel_pipe", 12);
        await ClickOn(table);
        var slot = panel.Aside.FindChild("plumbing_slot", true, false) as ItemSlot;
        Require(slot is { Visible: true }, "The cooking table should show a plumbing slot beside its page.");

        await Carry(BagCellOf("steel_pipe"), slot!);
        await Until(() => world.Find(table.Id)?.PlumbingInstalled == true && world.Backpack["steel_pipe"] == 2,
            "the slot taking ten pipes from the held stack and installing the plumbing", 2);
        if (!cursor.Empty) await KeyStroke(Key.Escape);
        await Holding(Key.Shift, () => Click(slot!));
        await Until(() => world.Find(table.Id)?.PlumbingInstalled == false && world.Backpack["steel_pipe"] == 12,
            "taking the pipes back from the slot and removing the plumbing", 2);
        await Holding(Key.Shift, () => Click(BagCellOf("steel_pipe")));
        await Until(() => world.Find(table.Id)?.PlumbingInstalled == true && world.Backpack["steel_pipe"] == 2,
            "Shift on the backpack stack installing ten pipes", 2);

        await Holding(Key.Shift, () => Click(slot!));
        await Until(() => world.Backpack["steel_pipe"] == 12, "refunding all the installed pipes", 2);
        RemoveThings(Hosted, t => t == table);
        Drop(Hosted, "steel_pipe", 12);
        await CloseWithEsc();
    }
}
