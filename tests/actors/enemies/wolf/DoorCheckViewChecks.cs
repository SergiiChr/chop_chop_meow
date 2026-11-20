using System;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Checks of how a wolf at a cabin's door is drawn: its paws on the ground and the step, and the door it pushes open.</summary>
public static class DoorCheckViewChecks {
    public static void Run() {
        Test(CheckAWolfAtADoorKeepsItsPawsOnTheGroundAndTheStep);
        Test(CheckTheDoorOpensGraduallyRattlesAtEachPryAndNeverStandsInTheWolf);
    }

    /// <summary>The door point the checks' cabin has, and the seconds of a door check before the wolf launches itself in: 3.6 + 1.5 + 2.5 + 2.</summary>
    private static readonly Vector2 Door = new(30, 30);
    private const float LaunchAt = 9.6f, PryAt = 3.6f;
    private const float Frame = 1f / 60;

    /// <summary>Stands a wolf model where a door check has it this many seconds after its first scratch.</summary>
    private static void Show(WolfModel wolf, float clock) {
        var (at, yaw) = DoorCheckView.Placement(Door, clock);
        wolf.Root.Position = at; wolf.Root.Rotation = new Vector3(0, yaw, 0);
        DoorCheckView.Pose(wolf, clock, clock, Frame);
    }

    /// <summary>
    /// Until it launches itself in, no part of a sole is under the ground, a tread or the floor, and a part of a sole that is down
    /// stays where it landed.
    /// </summary>
    private static void CheckAWolfAtADoorKeepsItsPawsOnTheGroundAndTheStep() {
        var wolf = new WolfModel();
        var paws = new PawWatch(spot => StairSteps.HeightAt(spot - Door));
        // The first frames at the door blend into the stand from where the wolf arrived, so the measured ones have it at the door.
        for (float clock = 0; clock < SettleSeconds; clock += Frame) Show(wolf, clock);
        for (float clock = SettleSeconds; clock < LaunchAt; clock += Frame) {
            Show(wolf, clock);
            paws.See(wolf, clock);
        }
        wolf.Root.Free();
        Require(paws.Deepest > -.01f, $"no paw of a wolf at a door should sink into the ground or the step: paw {paws.DeepestLeg} is {-paws.Deepest:0.000} m in, {paws.DeepestAt:0.00} s into the door check");
        Require(paws.Slide < .03f, $"a paw of a wolf at a door that is down should stay where it landed: paw {paws.SlideLeg} has slid {paws.Slide:0.000} m, {paws.SlideAt:0.00} s into the door check");
        Require(paws.PastRange < .6f, $"no joint of a wolf at a door should bend past a dog's range: the {paws.PastJoint} by {paws.PastRange:0.0} degrees");
    }
    /// <summary>Seconds a wolf at a door takes to blend into its stand from where it arrived.</summary>
    private const float SettleSeconds = .15f;

    /// <summary>
    /// The cabin's door is hinged 0.41 m to the left of its door point, 0.925 m in from it, is 0.82 m long and has its
    /// lower edge 0.42 m up. It stays shut until the wolf pries, opens a little at a time, swings back a little in each of the
    /// three pries of the 1.5 s of prying, and no part of the wolf is ever on both sides of it.
    /// </summary>
    private static void CheckTheDoorOpensGraduallyRattlesAtEachPryAndNeverStandsInTheWolf() {
        var wolf = new WolfModel();
        var hinge = Door + new Vector2(-.41f, -.925f);
        Span<(Vector3 At, float Radius)> volumes = stackalloc (Vector3, float)[WolfModel.VolumeCount];
        Span<bool> rattled = stackalloc bool[3];
        float before = 0, widest = 0, deepest = 0, fastest = 0, fastestAt = 0, deepestAt = 0;
        for (float clock = 0; clock < LaunchAt; clock += Frame) {
            Show(wolf, clock);
            float open = DoorCheckView.DoorDegrees(clock);
            Require(clock > PryAt || open == 0, $"the door should stay shut until the wolf pries at it: {open:0.0} degrees open {clock:0.00} s in");
            Require(open > widest - 3 && open <= 100, $"the door should swing back only a little as it rattles, and open no further than its full swing: {open:0.0} degrees after {widest:0.0}, {clock:0.00} s in");
            if (Math.Abs(open - before) > fastest) (fastest, fastestAt) = (Math.Abs(open - before), clock);
            int pry = (int)((clock - PryAt) / .5f);
            if (clock > PryAt && pry < 3 && open < before - .2f) rattled[pry] = true;
            (before, widest) = (open, Math.Max(widest, open));
            var edge = hinge + new Vector2(.82f, 0).Rotated(Mathf.DegToRad(open));
            wolf.Volumes(volumes);
            foreach (var (centre, radius) in volumes) {
                var at = wolf.Root.Transform * centre;
                if (at.Y + radius < .42f) continue;
                var spot = new Vector2(at.X, at.Z);
                var nearest = hinge + (edge - hinge) * Math.Clamp((spot - hinge).Dot(edge - hinge) / (edge - hinge).LengthSquared(), 0, 1);
                if (spot.DistanceTo(nearest) - radius < deepest) (deepest, deepestAt) = (spot.DistanceTo(nearest) - radius, clock);
            }
        }
        wolf.Root.Free();
        Require(fastest < 3, $"the door should open a little at a time: it swings {fastest:0.0} degrees in a sixtieth of a second, {fastestAt:0.00} s in");
        for (int pry = 0; pry < 3; pry++) Require(rattled[pry], $"the door should rattle at each pry of the wolf's prying: it does not swing back in pry {pry + 1}");
        Require(deepest > -.03f, $"the door should never stand in the wolf's body: {-deepest:0.000} m into it, {deepestAt:0.00} s in");
        Require(widest is > 35 and < 70, $"a wolf standing in the doorway should hold the door part of the way open against its flank: {widest:0.0} degrees");
    }
}
