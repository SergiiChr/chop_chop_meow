using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChopChopMeow;
using Godot;
using static Checks;
using static WolfSteps;
using static SetupSteps;
using static WorldSteps;
using static EconomySteps;
using static PlayerSteps;
using static WorkerSteps;
using static CabinSteps;

/// <summary>Checks of wolves at a player keeping their distance from them and their bodies apart.</summary>
public static class WolfBodiesChecks {
    public static void Run(GameData data) {
        Test(CheckAWolfClosingInStopsAtBitingDistanceRatherThanOnThePlayer, data);
        Test(CheckAWolfClosingInKeepsOffWhereAnotherTugsThePlayer, data);
        Test(CheckTwoWolvesAtOnePlayerKeepTheirBodiesApart, data);
        Test(CheckAWolfsHeightInTheWorldFollowsItsModel, data);
        Test(CheckAFeedingSpotLeavesRoomForTheWholePull);
        Test(CheckAFeedingWolfKeepsClearOfAnotherLoweringItsHead);
    }

    private static void CheckAFeedingWolfKeepsClearOfAnotherLoweringItsHead() {
        var feeder = new Wolf();
        var neighbor = new Wolf();
        var model = new WolfModel();
        var lowering = new WolfModel();
        var spots = new FeedingSpots(new Dictionary<Wolf, WolfModel> { [feeder] = model, [neighbor] = lowering });
        var fallen = new ActorState();
        var rig = new ActorRig(ActorSkin.Player);
        FallBackView.FallBack(rig, fallen, fallen.Pose, 1, false, null);
        var line = FeedingSpots.BodyLine(fallen);
        model.Root.Position = FeedingSpots.OnTheBody(line, .3f, BodySide.Left).At;
        var free = spots.Place(feeder, model, fallen, rig, 1);
        // The neighbor stands nose to tail behind this spot. Its resting feeding pose clears the feeder,
        // but its muzzle still reaches into the spot as it lowers its head from standing.
        lowering.Root.Transform = new Transform3D(model.Root.Basis, free - model.Root.Basis.Z * 1.4f);
        spots.Remember(new ListView<Wolf>([feeder, neighbor]));
        lowering.Feed(0, 1, false);
        var besideSettled = spots.Place(feeder, model, fallen, rig, 1);
        lowering.Stand(0, 1, false);
        lowering.Feed(0, 1f / 60, false);
        var besideLowering = spots.Place(feeder, model, fallen, rig, 1);
        model.Root.Free(); lowering.Root.Free(); rig.Root.Free();
        Require(besideSettled.DistanceTo(free) < .001f, "A feeding wolf should keep a spot that clears its neighbor.");
        Require(besideLowering.DistanceTo(free) > .01f, "A feeding wolf should leave a spot its neighbor still occupies while lowering its head.");
    }

    private static void CheckAFeedingSpotLeavesRoomForTheWholePull() {
        var wolf = new WolfModel();
        var reserved = WolfModel.FeedingVolumes;
        Span<(Vector3 At, float Radius)> drawn = stackalloc (Vector3, float)[WolfModel.VolumeCount];
        wolf.Feed(0, 1, false);
        float outside = 0;
        for (int frame = 0; frame < 180; frame++) {
            wolf.Feed(frame / 60f, 1f / 60, false);
            wolf.Volumes(drawn);
            for (int i = 0; i < drawn.Length; i++)
                outside = Math.Max(outside, drawn[i].At.DistanceTo(reserved[i].At) + drawn[i].Radius - reserved[i].Radius);
        }
        wolf.Root.Free();
        Require(outside < .004f, $"A feeding wolf must fit in its spot through both pulls; it reached {outside:0.000} m outside.");
    }

    /// <summary>
    /// A wolf is as tall in the world, where swings are tried against it, as its model is drawn: on the ground, reared up, and
    /// through a leap for a standing player's throat, its body posed and flown onto the throat.
    /// </summary>
    private static void CheckAWolfsHeightInTheWorldFollowsItsModel(GameData data) {
        var model = new WolfModel();
        model.Stand(0, 1, false);
        float standing = ModelSteps.WolfTop(model);
        Require(Math.Abs(standing - WolfBodies.StandingTop) < .03f, $"a standing wolf's model is {standing:0.00} m tall, not {WolfBodies.StandingTop} m");
        float reared = ModelSteps.WolfTop(WolfModel.Rearing(1.1f, 0));
        Require(Math.Abs(reared - WolfBodies.RearedTop) < .05f, $"a reared wolf's model is {reared:0.00} m tall, not {WolfBodies.RearedTop} m");
        // The flight comes down on the hind paws, reared up with the jaws at the throat.
        float throat = FallBackView.Throat(Vector2.Zero, Vector2.Down, 0).Y;
        for (float progress = 0; progress <= 1; progress += .125f) {
            model.Stand(0, 1, true); model.Leap(progress, 1, jawsUp: throat);
            float flown = Movement.Rise(progress * World.LeapSeconds, World.LeapSeconds);
            float leaping = ModelSteps.WolfTop(model) + flown, world = WolfBodies.Top(new Wolf { Leap = World.LeapSeconds * (1 - progress) + 1e-4f });
            Require(Math.Abs(leaping - world) < .09f,
                $"a wolf {progress:0.000} through its leap is {leaping:0.00} m tall in its model and {world:0.00} m in the world");
        }
        Require(Math.Abs(model.Bite.Z - WolfSnap.ThroatJawsAhead) < .02f,
            $"the throat leap's jaws close {model.Bite.Z:0.00} m ahead, with reach measured at {WolfSnap.ThroatJawsAhead:0.00} m");
        model.Snap(1, 1, 0, 1);
        Require(Math.Abs(model.Bite.Z - WolfSnap.JawsAhead) < .02f && Math.Abs(model.Bite.X) < .01f,
            $"the bite should close on its hitbox when the wolf lands, with the jaw at {model.Bite}");
        model.Root.Free();
    }

    private static void CheckAWolfClosingInStopsAtBitingDistanceRatherThanOnThePlayer(GameData data) {
        var world = NightWorld(data);
        var wolf = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3, 0) });
        float nearest = float.MaxValue;
        for (int i = 0; i < 60; i++) { world.Tick(.05f); nearest = Math.Min(nearest, wolf.Pos.DistanceTo(world.Player)); }
        Require(world.Health < 100, "a wolf closing in should still bite a player standing still");
        Require(nearest > .8f, $"a wolf should stop at biting distance rather than stand on the player, not come {nearest:0.00} m from them");
    }
    private static void CheckAWolfClosingInKeepsOffWhereAnotherTugsThePlayer(GameData data) {
        var world = NightWorld(data);
        var hut = world.Add(ThingKind.Hut, world.Player + new Vector2(0, -4));
        world.Player = Doorways.DoorPoint(hut);
        Require(world.Doorways.Enter(hut, false), "a player at the door should step in");
        PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(.8f, 0) });
        WolfSteps.UntilTugged(world);
        Require(world.Door == Doorway.Tugged, "a wolf should tug the player out of the doorway");
        // With a wolf at each arm, one more waits just past where the tug ends.
        foreach (int side in new[] { -1, 1 }) PlaceWolf(world, new Wolf { Pos = Doorways.DoorPoint(hut) + new Vector2(side, .2f) });
        var other = PlaceWolf(world, new Wolf { Pos = Doorways.DoorPoint(hut) + Doorways.OutOfDoor * 2 });
        float nearest = float.MaxValue;
        for (float time = 0; time < DoorTug.TugSeconds - .1f; time += .05f) {
            world.Tick(.05f);
            nearest = Math.Min(nearest, other.Pos.DistanceTo(Doorways.DoorPoint(hut) + Doorways.OutOfDoor * (DoorTug.TugDistance * DoorTug.TugProgress(world.Me.DoorTime / DoorTug.TugSeconds))));
        }
        Require(nearest > .8f, $"a wolf closing in should keep off where another wolf has tugged the player, not come {nearest:0.00} m from them");
    }
    private static void CheckTwoWolvesAtOnePlayerKeepTheirBodiesApart(GameData data) {
        var world = NightWorld(data);
        // Both close in on the player's right side, nearly along the same line.
        var first = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3, 0), Attack = 5 });
        var second = PlaceWolf(world, new Wolf { Pos = world.Player + new Vector2(3.5f, .3f), Attack = 5 });
        for (int i = 0; i < 60; i++) world.Tick(.05f);
        var a = WolfBodies.Body(first, world.Player); var b = WolfBodies.Body(second, world.Player);
        float gap = float.MaxValue;
        // Sample points along each body, nose to tail.
        for (int i = 0; i <= 20; i++) for (int j = 0; j <= 20; j++) gap = Math.Min(gap, a.Tail.Lerp(a.Nose, i / 20f).DistanceTo(b.Tail.Lerp(b.Nose, j / 20f)));
        Require(gap >= 2 * WolfBodies.WolfHalfWidth - .01f, $"two wolves at one player should keep their bodies apart, not come {gap:0.00} m from each other");
    }
}
