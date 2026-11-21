using System.Threading.Tasks;
using Godot;
using static Checks;
using static SteelPipeSteps;
using static PlumbingSteps;

namespace ChopChopMeow;

public partial class Game {
    private static readonly Vector2I NetPipe = Terrain.TileAt(Terrain.Spawn) + new Vector2I(-2, -2);
    private static readonly Vector2I NetPipeCorner = NetPipe + new Vector2I(1, 1), NetPipeEast = NetPipe + Vector2I.Right;

    private Thing? netPipePump;

    private NetScene SteelPipeNetScene => new("steel-pipes", () => {
        LayPipe(Hosted, NetPipe);
        LayPipe(Hosted, NetPipeCorner, Mathf.Pi / 2);
        netPipePump = Hosted.Add(ThingKind.Pump, NetPipe);
    }, HostSeesTheGuestMoveAPipe, GuestMovesTheHostsPipe);

    private async Task HostSeesTheGuestMoveAPipe(PlayerState guest) {
        var stood = Hosted.Player;
        PumpFully(Hosted, netPipePump!);
        Hosted.Player = stood;
        await Until(() => !Hosted.SteelPipes.Laid(NetPipeCorner) && Hosted.SteelPipes.RotationAt(NetPipeEast) == Mathf.Pi
            && Hosted.SteelPipes.ShapeAt(NetPipeEast) == PipeShape.Cross,
            "the guest taking up the corner pipe and laying it east of the host's pipe", 90);
        Require(world.Backpack["steel_pipe"] == 0 && guest.Backpack["steel_pipe"] == 0,
            "The lifted pipe must go back into the ground without reaching the host's backpack.");
    }

    private async Task GuestMovesTheHostsPipe() {
        Require(world.SteelPipes.Laid(NetPipe) && world.SteelPipes.RotationAt(NetPipeCorner) == Mathf.Pi / 2,
            "Joining must bring over both steel pipes and their rotations.");
        Require(world.SteelPipes.ConnectionMask(NetPipe) == 0b0000_0010,
            "The corner neighbor of the host's pipe must also be a neighbor on the guest.");
        await Until(() => world.PipeReading(NetPipeCorner).Liters == 1,
            "the server's water reaching the corner pipe on the guest");
        var removal = world.Construction.RemovalAt(null, NetPipeCorner, underground: true);
        Require(world.Construction.Remove(removal), "The guest should be able to take up the host's pipe in reach.");
        await Until(() => !world.SteelPipes.Laid(NetPipeCorner) && world.Backpack["steel_pipe"] == 1,
            "the server taking up the corner pipe and giving it to the guest");
        world.Construction.Build(ThingKind.SteelPiping, NetPipeEast, Mathf.Pi, PipeShape.Cross);
        await Until(() => world.SteelPipes.RotationAt(NetPipeEast) == Mathf.Pi
            && world.SteelPipes.ShapeAt(NetPipeEast) == PipeShape.Cross && world.Backpack["steel_pipe"] == 0,
            "the server laying the guest's pipe facing west");
        Require(world.SteelPipes.ConnectionMask(NetPipe) == 0b0000_0001,
            "Moving the pipe must move the host's corner neighbor to the side.");
    }
}
