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

    private async Task CheckRepeatedWaterPumpInputKeepsTheHandleMoving() {
        var home = world.Player;
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var pump = Hosted.Add(ThingKind.Pump, Terrain.TileAt(bank + Vector2.Down * 1.1f));
        Warp(Hosted, pump.ToWorld(World.BellowsStand));
        await ClickOn(pump);
        var prop = (WaterPumpProp)view.PropOf(pump.Id)!;
        await Click(Named("pump")!);
        await Until(() => LeverAngle() < -16, "the first pump stroke moving down", 2);

        await KeyStroke(Key.Space);
        await Until(() => pump.Pressure == 20, "repeated input raising pressure a second time", 2);
        Require(LeverAngle() < -12, "Repeated pump input must keep the handle moving down.");
        await Until(() => LeverAngle() < -37, "the first stroke reaching the bottom", 2);
        await Until(() => LeverAngle() > -3, "the first stroke returning to the top", 2);
        await Until(() => LeverAngle() < -37, "the next stroke reaching the bottom without more input", 2);
        await Until(() => view.PlayerAnimation != ActorAction.PumpWater, "the last stroke finishing on its own", 2);
        Require(LeverAngle() > -3, "The last pump stroke must finish with the handle raised.");

        await CloseWithEsc();
        RemoveThings(Hosted, t => t == pump);
        Warp(Hosted, home);
        await Frames(4);

        float LeverAngle() => prop.Pump.Lever.RotationDegrees.Z;
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

    private async Task CheckPumpAtTheWaterlineCanBeInspected() {
        var home = world.Player;
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var intake = Terrain.TileAt(bank + Vector2.Down);
        var pump = Hosted.Add(ThingKind.Pump, intake);
        // Place the lake's source point directly in front of the chamber, where its old pick shape hid the steel.
        var water = Hosted.Add(ThingKind.Water, (Vector2)intake + new Vector2(.25f, .6f), 9999);
        Warp(Hosted, intake + Vector2.Up * 2);
        await Frames(4);
        var shore = Terrain.WaterPoint(bank + Vector2.Right * 3);
        await Click(view.Screen(shore));
        Require(world.Find(panel.Selection.Thing)?.Kind == ThingKind.Water, "Open water beside the pump should still select the lake.");

        var chamber = (Vector2)intake + Vector2.Down * .38f;
        await Hover(view.Screen(chamber, .25f));
        Require(view.Hover == pump.Id, "The chamber in front of the water should show the pump's hover label.");
        await KeyStroke(Key.E);
        Require(panel.Selection.Thing == pump.Id, "E over the pump should inspect it even when the lake was selected.");
        await Click(view.Screen(shore));
        var shaft = view.Screen(chamber, .89f) + Vector2.Right * 3;
        await Hover(shaft);
        Require(view.Hover == pump.Id, "Aiming at the thin shaft should show the pump's label.");
        await Click(shaft);
        Require(panel.Selection.Thing == pump.Id, "Clicking the shaft should open the pump controls.");

        await CloseWithEsc();
        RemoveThings(Hosted, t => t == pump || t == water);
        Warp(Hosted, home); await Frames(4);
    }

    private async Task CheckWaterPumpControlsMoveThePressureGauge() {
        var home = world.Player;
        var bank = Terrain.WaterPoint(Terrain.Lake + Vector2.Up * 25);
        var intake = Terrain.TileAt(bank + Vector2.Down * 1.1f);
        var pump = Hosted.Add(ThingKind.Pump, intake);
        var stand = pump.ToWorld(World.BellowsStand);
        Warp(Hosted, stand + new Vector2(.5f, -.65f));
        await ClickOn(pump);
        Require(Named("job_pump") != null, "A pump should offer the Pump job.");
        var gauge = OnPage("pump_pressure") as Godot.Range;
        Require(gauge is { Value: 0, MaxValue: 5 } && gauge.IsVisibleInTree() && HasLabel(panel, "Pressure"),
            "The idle pressure gauge should start at zero on a five-liter scale.");
        foreach (string marking in new[] { "0", "1", "2", "3", "4", "5", "L/s" })
            Require(HasLabel(gauge, marking), $"The pump dial should show its {marking} marking.");
        await Click(Named("pump")!);
        await Until(() => pump.Pressure == 10 && gauge.Value == .5 && view.PlayerAnimation == ActorAction.PumpWater,
            "clicking the handle raising pressure and moving the player's arms", 2);
        Require(world.Player.DistanceTo(stand) < .02f && Hosted.Player.DistanceTo(stand) < .02f,
            "Using the pump from beside its handle should step the player into reach before both hands press down.");
        await KeyStroke(Key.Space);
        await Until(() => gauge.Value == 1, "the second stroke reaching the first whole liter per second", 2);
        for (int stroke = 2; stroke < 10; stroke++) await KeyStroke(Key.Space);
        await Until(() => gauge.Value == 5 && pump.FlowRate == 5, "the dial reaching 5 L/s at full pressure", 2);
        Hosted.Tick(WaterPumps.StrokeSeconds + 10);
        await Until(() => gauge.Value == 0, "the needle returning to zero as pressure falls", 2);
        await CloseWithEsc();
        RemoveThings(Hosted, t => t == pump);
        Warp(Hosted, home); await Frames(4);
    }
}
