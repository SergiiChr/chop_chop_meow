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
        await TestClean(CheckHotbarPipePlanRemovesAndReusesALaidPipe);
        await TestClean(CheckRemoveSelectorKeepsTheOtherLayer);
        await TestClean(CheckInspectReadsWaterAndPressureWhileUnderground);
        await TestClean(CheckPuttingPipesInThePlumbingSlotInstallsAndRefundsThem);
        await TestClean(CheckGrowingCropsShowWaterFromTheSprinkler);
    }

    private async Task CheckHeldPipePlanTurnsAndCyclesShapes() {
        Give(Hosted, "steel_pipe", 2);
        await KeyStroke(Key.B);
        await PickUp(Named("build_steel_piping")!);
        Require(cursor.Plan == "steel_piping", "Clicking a pipe plan should put it on the cursor.");
        var tiles = SpotsNearby().Select(Terrain.TileAt).Distinct()
            .Where(t => world.Construction.PlacementBlock(ThingKind.SteelPiping, t) == null && Uncovered(view.Screen(t, PipeGround.FloorY)))
            .Take(2).ToArray();
        Require(tiles.Length == 2, "The clearing should have two visible tiles for pipes.");
        await Hover(view.Screen(tiles[0], PipeGround.FloorY));
        var ghost = view.FindChild("PipePreview", true, false) as MeshInstance3D;
        Require(view.Underground && ghost is { Visible: true } && ghost.Basis.X.X > .999f, "The straight pipe should start with an east-west preview.");
        var soil = view.FindChild("ExcavatedSoil", true, false) as MeshInstance3D;
        Require(soil?.Mesh != null && soil.GetAabb().Position.Y < -.5f && soil.GetAabb().End.Y >= 0,
            "The exposed soil should extend from below the pipes to the original ground level.");
        Require(ghost!.GlobalPosition.DistanceTo(WorldView.To3(tiles[0], PipeGround.FloorY)) < .05f,
            "The preview should sit on the buried tile under the pointer.");
        Require(!cursor.Visible, "Only the buried preview should show while aiming the held pipe plan.");
        await Until(() => HasLabel(modeLabel, "Place steel piping"), "the pipe placement prompts appearing", 2);
        var prompts = modeLabel.FindChildren("*", "Label", true, false).Cast<Label>().Where(l => l.IsVisibleInTree()).Select(l => l.Text);
        Require(prompts.SequenceEqual(new[] { "Place steel piping", "LMB", "- build", "RMB", "- remove", "R", "- rotate", "T", "- change type" }),
            "Pipe placement should show its four inputs in order under the title.");
        await KeyStroke(Key.R);
        Require(ghost!.Visible && Mathf.Abs(ghost.Basis.X.Z) > .999f, "R should turn the straight pipe preview a quarter turn.");
        await Hover(hotbar.Buttons[0]);
        Require(cursor.Visible && !ghost.IsVisibleInTree(), "Over a hotbar slot the held pipe plan should appear on the cursor.");
        await Hover(view.Screen(tiles[0], PipeGround.FloorY));
        await KeyStroke(Key.T);
        await Click(view.Screen(tiles[0], PipeGround.FloorY));
        await Until(() => world.SteelPipes.ShapeAt(tiles[0]) == PipeShape.Tee, "the selected T-junction going underground", 2);
        Require(world.SteelPipes.RotationAt(tiles[0]) == Mathf.Pi / 2, "The placed T-junction should keep the quarter turn.");
        await KeyStroke(Key.T);
        await Click(view.Screen(tiles[1], PipeGround.FloorY));
        await Until(() => world.SteelPipes.ShapeAt(tiles[1]) == PipeShape.Cross, "the next pipe going underground as a cross junction", 2);
        Require(world.SteelPipes.RotationAt(tiles[1]) == Mathf.Pi / 2, "The next pipe should keep the quarter turn.");
        await RightClick(tiles[1], PipeGround.FloorY);
        await Until(() => !world.SteelPipes.Laid(tiles[1]) && world.Backpack["steel_pipe"] == 1,
            "right-click returning the last pipe built to the backpack", 2);
        await KeyStroke(Key.T);
        await Click(view.Screen(tiles[1], PipeGround.FloorY));
        await Until(() => world.SteelPipes.ShapeAt(tiles[1]) == PipeShape.Straight, "cycling back to straight and laying the refunded pipe", 2);
        foreach (var tile in tiles) await RightClick(tile, PipeGround.FloorY);
        await Until(() => world.Backpack["steel_pipe"] == 2, "both pipes returning while still in placement", 2);
        Drop(Hosted, "steel_pipe", 2);
        await CloseWithEsc(); await Frames();
    }

    private async Task CheckHotbarPipePlanRemovesAndReusesALaidPipe() {
        int active = world.ActiveHotbar;
        string saved = world.HotbarItem(8);
        var tile = SpotsNearby().Select(Terrain.TileAt).First(t => world.Reaches(t) && Uncovered(view.Screen(t, PipeGround.FloorY)));
        LayPipe(Hosted, tile); world.SetHotbar(8, "steel_piping"); await Frames();
        await KeyStroke(Key.Key9);
        await RightClick(tile, PipeGround.FloorY);
        await Until(() => !world.SteelPipes.Laid(tile) && world.Backpack["steel_pipe"] == 1,
            "taking up a pipe with the hotbar plan", 2);
        await KeyStroke(Key.R); await KeyStroke(Key.T);
        await Click(view.Screen(tile, PipeGround.FloorY));
        await Until(() => world.SteelPipes.ShapeAt(tile) == PipeShape.Tee && world.SteelPipes.RotationAt(tile) == Mathf.Pi / 2,
            "building the turned tee with the refunded pipe from the hotbar", 2);
        await RightClick(tile, PipeGround.FloorY); await CloseWithEsc();
        Drop(Hosted, "steel_pipe"); world.SetHotbar(8, saved); world.SelectHotbar(active); await Frames();
    }

    private async Task CheckRemoveSelectorKeepsTheOtherLayer() {
        Give(Hosted, "gravel", 4);
        Give(Hosted, world.Data.Buildings[ThingKind.Chest].Cost);
        await Frames();
        var tile = SpotsNearby().Select(Terrain.TileAt)
            .First(t => world.GravelPaths.Block(t) == null && world.Construction.PlacementBlock(ThingKind.Chest, t) == null
                && Uncovered(view.Screen(t)) && Uncovered(view.Screen(t, .4f)) && Uncovered(view.Screen(t, PipeGround.FloorY)));
        Hosted.Construction.Build(ThingKind.GravelPath, tile);
        var chest = Hosted.Construction.Build(ThingKind.Chest, tile);
        Require(chest != null, "The chest should be built over the gravel path.");
        LayPipe(Hosted, tile);
        await Frames();

        await KeyStroke(Key.B);
        await Click(Named("remove")!);
        await RightClick(tile);
        await Hover(view.Screen(tile, PipeGround.FloorY));
        Require(view.Underground && view.RemovalShown.Pipe == tile, "Underground removal should mark the pipe beneath the chest.");
        await Click(view.Screen(tile, PipeGround.FloorY));
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
        var tankTile = tile + Vector2I.Up * 2;
        var tank = Hosted.Add(ThingKind.Barrel, tankTile);
        LayPipe(Hosted, tile, Mathf.Pi / 2);
        await Frames();
        await KeyStroke(Key.B);
        await Click(Named("remove")!);
        await RightClick(tile);
        await Hover(view.Screen(tile, PipeGround.FloorY));
        await KeyStroke(Key.E);
        Require(view.Underground && ShownPage() == "Steel pipe", "E over a pipe should inspect it and keep the underground layer visible.");
        Require(HasLabel(panel, "Empty · 0/1 L") && HasLabel(panel, "Pressure 0%"), "A dry pipe should show no water and no pressure.");

        var pipe = ModelSteps.AllMeshes(view.FindChild("Underground", true, false)!)
            .Single(mesh => mesh.Position == WorldView.To3(tile));
        var empty = (StandardMaterial3D)pipe.MaterialOverride;
        Hosted.Pump(pump);
        Hosted.Tick(.1f);
        await Until(() => world.PipeReading(tile).Liters is > 0 and < 1 && pipe.MaterialOverride != empty,
            "the one pipe taking its first water and changing color", 2);
        var wet = (StandardMaterial3D)pipe.MaterialOverride;
        Require(wet.AlbedoColor.B > wet.AlbedoColor.G && wet.AlbedoColor.Luminance < empty.AlbedoColor.Luminance * .8f,
            $"A partly filled pipe should turn visibly darker blue, went from {empty.AlbedoColor} to {wet.AlbedoColor}.");
        LayPipe(Hosted, tile + Vector2I.Up, Mathf.Pi / 2);
        LayPipe(Hosted, tankTile, Mathf.Pi / 2);
        for (int i = 0; i < 10; i++) Hosted.Pump(pump);
        Hosted.Tick(.65f);
        await Until(() => HasLabel(panel, "Full · 1/1 L") && HasLabel(panel, "Pressure 100%"),
            "the inspected pipe filling and displaying pump pressure", 2);
        Hosted.Tick(11);
        await Until(() => HasLabel(panel, "Full · 1/1 L") && HasLabel(panel, "Pressure 0%"),
            "the inspected pipe retaining its water as pressure falls", 2);

        Require(pipe.MaterialOverride == wet, "Standing water should keep its blue color after pressure falls to zero.");
        PlumbingSteps.InstallPlumbing(Hosted, tank);
        Hosted.Tick(.1f);
        await Until(() => HasLabel(panel, "Empty · 0/1 L") && pipe.MaterialOverride == empty,
            "a drained pipe returning to its empty finish", 2);

        await KeyStroke(Key.Escape); await CloseWithEsc();
        foreach (var cell in new[] { tile, tile + Vector2I.Up, tankTile }) Hosted.Construction.TakeUpPipe(cell);
        Drop(Hosted, "steel_pipe", 3);
        RemoveThings(Hosted, t => t == pump || t == tank);
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckGrowingCropsShowWaterFromTheSprinkler() {
        var home = world.Player;
        var tile = Terrain.TileAt(home);
        var farm = Hosted.Add(ThingKind.Farm, tile);
        farm.FarmStage = FarmStage.Growing;
        PlumbingSteps.InstallPlumbing(Hosted, farm);
        Warp(Hosted, home); await Frames(4);
        var spray = (MeshInstance3D)view.PropOf(farm.Id)!.Body.FindChild("FarmSpray", true, false)!;
        Require(!spray.IsVisibleInTree(), "A dry sprinkler must be off.");

        farm.Inventory.Add(Water.Liquid, Farms.LitersPerDay);
        Hosted.Tick(.1f);
        await Until(() => spray.IsVisibleInTree(), "a growing crop using water from its full reserve through the sprinkler", 2);
        var shown = world.Find(farm.Id)!;
        Require(!shown.ReceivingWater && world.Farms.WaterLevel(shown) > 9,
            "The spray should remain visible between top-ups while the reserve waters the crop.");
        farm.FarmStage = FarmStage.Ripe;
        await Until(() => !spray.IsVisibleInTree(), "the spray stopping when the crop ripens with water left", 2);
        RemoveThings(Hosted, t => t == farm);
        Warp(Hosted, home); await Frames();
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
