using System;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Checks of how a wolf at a cabin's door is drawn: its paws on the ground and the step, and the door it pushes open.</summary>
public static class VisitViewChecks {
    public static void Run() {
        Test(CheckAWolfAtADoorKeepsItsPawsOnTheGroundAndTheStep);
        Test(CheckTheDoorOpensOnlyAsFarAsTheWolfPushesItAndNeverThroughIt);
    }

    /// <summary>The door point the checks' cabin has, and the seconds of a visit before the wolf launches itself in: 3.6 + 1.5 + 2.5 + 2.</summary>
    private static readonly Vector2 Door = new(30, 30);
    private const float LaunchAt = 9.6f, PryAt = 3.6f;
    private const float Frame = 1f / 60;

    /// <summary>Stands a wolf model where a visit has it this many seconds after its first scratch.</summary>
    private static void Show(WolfModel wolf, float clock) {
        var (at, yaw) = VisitView.Placement(Door, clock);
        wolf.Root.Position = at; wolf.Root.Rotation = new Vector3(0, yaw, 0);
        VisitView.Pose(wolf, clock, clock, Frame);
    }

    /// <summary>Until it launches itself in, no paw is under the ground, a tread or the floor, and a paw that is down stays where it landed.</summary>
    private static void CheckAWolfAtADoorKeepsItsPawsOnTheGroundAndTheStep() {
        var wolf = new WolfModel();
        // The first frame at the door takes the stand, so the measured ones have the wolf where the frame before left it.
        Show(wolf, 0);
        float deepest = 0, slide = 0, deepestAt = 0, slideAt = 0;
        int deepestLeg = 0, slideLeg = 0;
        var landed = new Vector2?[4];
        for (float clock = 0; clock < LaunchAt; clock += Frame) {
            Show(wolf, clock);
            for (int leg = 0; leg < 4; leg++) {
                var paw = wolf.Root.Transform * wolf.Paw(leg);
                var spot = new Vector2(paw.X, paw.Z);
                float over = paw.Y - WolfShape.Sole - StairSteps.HeightAt(spot - Door);
                if (over < deepest) (deepest, deepestAt, deepestLeg) = (over, clock, leg);
                if (Math.Abs(over) < .004f) {
                    landed[leg] ??= spot;
                    if (spot.DistanceTo(landed[leg]!.Value) > slide) (slide, slideAt, slideLeg) = (spot.DistanceTo(landed[leg]!.Value), clock, leg);
                } else landed[leg] = null;
            }
        }
        wolf.Root.Free();
        Require(deepest > -.02f, $"no paw of a wolf at a door should sink into the ground or the step: paw {deepestLeg} is {-deepest:0.000} m in, {deepestAt:0.00} s into the visit");
        Require(slide < .03f, $"a paw of a wolf at a door that is down should stay where it landed: paw {slideLeg} has slid {slide:0.000} m, {slideAt:0.00} s into the visit");
    }

    /// <summary>
    /// The cabin's door is hinged 0.41 m to the left of its door point, 0.925 m in from it, and is 0.82 m long.
    /// It stays shut until the wolf pries, never swings back, and no part of the wolf is ever on both sides of it.
    /// </summary>
    private static void CheckTheDoorOpensOnlyAsFarAsTheWolfPushesItAndNeverThroughIt() {
        var wolf = new WolfModel();
        var hinge = Door + new Vector2(-.41f, -.925f);
        Span<(Vector3 At, float Radius)> volumes = stackalloc (Vector3, float)[WolfModel.VolumeCount];
        float before = 0, deepest = 0;
        for (float clock = 0; clock < LaunchAt; clock += Frame) {
            Show(wolf, clock);
            float open = VisitView.DoorDegrees(clock);
            Require(clock > PryAt || open == 0, $"the door should stay shut until the wolf pries at it: {open:0.0} degrees open {clock:0.00} s in");
            Require(open >= before && open <= 100, $"the door should only open further, up to its full swing: {before:0.0} then {open:0.0} degrees {clock:0.00} s in");
            before = open;
            var edge = hinge + new Vector2(.82f, 0).Rotated(Mathf.DegToRad(open));
            wolf.Volumes(volumes);
            foreach (var (centre, radius) in volumes) {
                var at = wolf.Root.Transform * centre;
                var spot = new Vector2(at.X, at.Z);
                var nearest = hinge + (edge - hinge) * Math.Clamp((spot - hinge).Dot(edge - hinge) / (edge - hinge).LengthSquared(), 0, 1);
                deepest = Math.Min(deepest, spot.DistanceTo(nearest) - radius);
            }
        }
        wolf.Root.Free();
        Require(deepest > -.03f, $"the door should never stand in the wolf's body: {-deepest:0.000} m into it");
        Require(before is > 55 and < 90, $"a wolf standing in the doorway should hold the door most of the way open against its flank: {before:0.0} degrees");
    }
}
