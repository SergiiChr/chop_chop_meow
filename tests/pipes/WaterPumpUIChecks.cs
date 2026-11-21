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
