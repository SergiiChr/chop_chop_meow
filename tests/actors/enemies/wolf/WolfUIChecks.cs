using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static ModelSteps;
using static PlayerSteps;
using static CabinSteps;
using static SetupSteps;
using static WorldSteps;
using static WorkerSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>
    /// The UI checks of wolves at a cabin door: how a tugging wolf is drawn and the window onto a door being pried.
    /// </summary>
    private async Task RunWolfUIChecks() {
        await TestClean(CheckAWolfAtATuggedPlayersArmIsDrawnOnTheSideItHolds);
        await TestClean(CheckTheInsideWindowOpensWhenAWolfPriesAtTheDoor);
    }

    /// <summary>
    /// A cabin's door faces down the map, so a player stepping in faces up it with their left arm to the west.
    /// A wolf holding that arm while another tugs them is drawn to the west of them with its jaws on that forearm, and the
    /// one holding their right arm to the east.
    /// </summary>
    private async Task CheckAWolfAtATuggedPlayersArmIsDrawnOnTheSideItHolds() {
        var home = Hosted.Player;
        await CloseWithEsc();
        float health = world.Health; RunUntilNight(Hosted);
        var at = Terrain.Spawn - new Vector2(30, 30);
        RemoveThings(Hosted, t => t.Pos.DistanceTo(at) < 12);
        var hut = Hosted.Add(ThingKind.Hut, at + new Vector2(0, -4));
        Warp(Hosted, Doorways.DoorPoint(hut));
        await Frames();
        Require(Hosted.Doorways.Enter(hut, false), "A player at a cabin's door must step in.");
        var pack = new[] { new Vector2(.8f, 0), new Vector2(-1, .2f), new Vector2(1, .2f) }.Select(from => PlaceWolf(Hosted, new Wolf { Pos = Hosted.Player + from })).ToList();
        await Until(() => world.Door == Doorway.Tugged && world.Wolves.Count(w => w.Side != 0 && view.ModelOf(w) is { Motion: WolfMotion.Rear, Settled: >= 1 }) == 2,
            "a wolf taking each arm of a player another wolf tugs out of a doorway", 3);
        var rig = view.RigOf(world.Me.Id)!;
        foreach (var wolf in world.Wolves.Where(w => w.Side != 0)) {
            var model = view.ModelOf(wolf)!;
            bool left = wolf.Side == BodySide.Left;
            float elbowOut = rig.Elbow(left).X * wolf.Side;
            Require(elbowOut > .30f, $"A latched wolf must draw the player's elbow out to its side, only {elbowOut:0.00} m from the centre.");
            float east = model.Root.GlobalPosition.X - rig.Root.GlobalPosition.X;
            Require(left ? east < -.3f : east > .3f, $"A wolf holding a player's {(left ? "left" : "right")} arm must be drawn on that side of them, not {east:0.00} m to their east.");
            Vector3 jaws = model.Root.GlobalTransform * model.Bite;
            float held = jaws.DistanceTo(rig.Root.GlobalTransform * rig.Elbow(left).Lerp(rig.Fist(left), .5f)), other = jaws.DistanceTo(rig.Root.GlobalTransform * rig.Elbow(!left).Lerp(rig.Fist(!left), .5f));
            Require(held < .15f && held < other, $"A wolf holding a player's {(left ? "left" : "right")} arm must be drawn with its jaws on that forearm, not {held:0.00} m from it and {other:0.00} m from the other.");
        }
        RemoveWolves(Hosted, pack.Contains);
        await Until(() => world.Sheltered == 0 && world.Door == Doorway.None, "a player nobody tugs any more standing free outside the cabin", 4);
        RemoveThings(Hosted, t => t == hut); RunUntilMorning(Hosted); SetNeeds(Hosted, health: health);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }

    /// <summary>The window onto the door from inside stays shut while a wolf scratches, opens as it pries, and shuts once the wolf is gone.</summary>
    private async Task CheckTheInsideWindowOpensWhenAWolfPriesAtTheDoor() {
        var home = Hosted.Player;
        await CloseWithEsc();
        float health = world.Health; RunUntilNight(Hosted);
        // Only this check's wolf comes to the door.
        RemoveWolves(Hosted, _ => true); ShutDens(Hosted);
        var at = Terrain.Spawn - new Vector2(30, 30);
        RemoveThings(Hosted, t => t.Pos.DistanceTo(at) < 12);
        Warp(Hosted, at); await Frames(4);
        var hut = Hosted.Add(ThingKind.Hut, at + new Vector2(0, -4));
        StepInside(Hosted, hut);
        await Until(() => world.Sheltered == hut.Id, "the player being inside the cabin", 3);
        var wolf = PatrolWolf(Hosted, new Wolf { Pos = Doorways.DoorPoint(hut) + Doorways.OutOfDoor * 3 });
        await Until(() => world.Wolves.Any(w => w.DoorCheck == DoorCheckStage.Scratching), "a wolf passing the cabin coming to scratch at its door", 6);
        await Frames();
        Require(!view.InsideDoorPOV.Showing && !view.InsideDoorPOV.Visible, "The window onto the door from inside must stay shut while a wolf only scratches.");
        await Until(() => world.Wolves.Any(w => w.DoorCheck == DoorCheckStage.Prying), "the wolf prying at the unlocked door", 6);
        await Until(() => view.InsideDoorPOV.Showing && view.InsideDoorPOV.Visible, "the window onto the door from inside opening as the wolf pries", 2);
        RemoveWolves(Hosted, w => w == wolf);
        await Until(() => !view.InsideDoorPOV.Showing && !view.InsideDoorPOV.Visible, "the window onto the door from inside shutting once the wolf is gone", 2);
        Hosted.Doorways.Leave();
        await Until(() => world.Sheltered == 0 && world.Door == Doorway.None, "the player stepping out of the cabin again", 4);
        RemoveThings(Hosted, t => t == hut); RunUntilMorning(Hosted); SetNeeds(Hosted, health: health);
        await CloseWithEsc();
        Warp(Hosted, home); await Frames(4);
    }
}
