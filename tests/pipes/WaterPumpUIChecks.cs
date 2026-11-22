using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;
using static SteelPipeSteps;

namespace ChopChopMeow;

public partial class Game {
    private async Task CheckPlumbingPagesRefreshWithoutAllocating() {
        var tile = Terrain.TileAt(world.Player + Vector2.Right * 2);
        var pump = Hosted.Add(ThingKind.Pump, tile);
        LayPipe(Hosted, tile);
        await MeasurePanelRefreshAllocations([
            ("pump", () => panel.Select(Shown(pump))), ("steel pipe", () => panel.SelectPipe(tile))
        ]);
        RemoveThings(Hosted, t => t == pump);
        Hosted.Construction.TakeUpPipe(tile);
        Drop(Hosted, "steel_pipe");
        await Frames();
    }

    private async Task CheckVisiblePumpGripAndPistonCanBePicked() {
        var home = world.Player;
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var intake = Terrain.TileAt(bank + Vector2.Down * 1.1f);
        Warp(Hosted, intake + Vector2.Up * 2);
        for (int turn = 0; turn < 4; turn++) {
            var pump = Hosted.Add(ThingKind.Pump, intake, rotation: turn * Mathf.Pi / 2);
            await Until(() => view.PropOf(pump.Id) is WaterPumpProp, "the pump model appearing", 3);
            var prop = (WaterPumpProp)view.PropOf(pump.Id)!;
            var grip = prop.Pump.Lever.GetNode<MeshInstance3D>("PumpHandle");
            var piston = (Node3D)prop.Body.FindChild("Piston", true, false);
            // Sample the drawn parts in each pose before the next world frame updates the stroke.
            foreach (float pressed in new[] { 0, .5f, 1 }) {
                prop.Pump.SetPress(pressed);
                CheckPart(grip.GlobalPosition, "grip");
                CheckPart(piston.GlobalPosition + Vector3.Down * .24f, "upper piston");

                void CheckPart(Vector3 point, string part) {
                    var screen = view.Screen(new Vector2(point.X, point.Z), point.Y);
                    Require(view.Pick(screen)?.Id == pump.Id, $"The pump's visible {part} should select it at turn {turn}, stroke {pressed}.");
                }
            }
            RemoveThings(Hosted, t => t == pump);
            await Frames();
        }
        Warp(Hosted, home);
        await Frames(4);
    }

    private async Task CheckWaterPumpControlsShowPressureAndFlow() {
        var home = world.Player;
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var intake = Terrain.TileAt(bank + Vector2.Down * 1.1f);
        var pump = Hosted.Add(ThingKind.Pump, intake);
        Warp(Hosted, pump.ToWorld(World.BellowsStand));
        await ClickOn(pump);
        Require(Named("job_pump") != null, "A pump should offer the Pump job.");
        await Click(Named("pump")!);
        await Until(() => pump.Pressure == 10 && view.PlayerAnimation == ActorAction.Pump,
            "clicking the handle raising pressure and moving the player's arms", 2);
        await KeyStroke(Key.Space);
        await Until(() => (panel.FindChild("pump_reading", true, false) as Label)?.Text == "Pressure 20% · 1.0 L/s",
            "the second stroke raising the gauge and flow readout on the open pump", 2);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == pump);
        Warp(Hosted, home); await Frames(4);
    }
}
