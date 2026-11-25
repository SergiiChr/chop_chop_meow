using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;

internal static class StrawBedSteps {
    internal static Thing Build(World world, float rotation = 0) {
        Give(world, "leaves", 20);
        var bed = world.Construction.Build(ThingKind.StrawBed, world.Player + Vector2.Right * 2, rotation);
        Require(bed != null, "twenty leaves alone should build a straw bed on clear, dry ground");
        return bed!;
    }

    /// <summary>Where the toe and heel of one boot's sole are in the world.</summary>
    internal static (Vector3 Toe, Vector3 Heel) Sole(ActorRig rig, bool left) {
        var (toe, heel) = rig.BootSole(left);
        return (rig.Root.Transform * toe, rig.Root.Transform * heel);
    }

    /// <summary>Where a hand's palm is in the world.</summary>
    internal static Vector3 Palm(ActorRig rig, bool left) => rig.Root.Transform * rig.HandPad(left);

    /// <summary>Where the left elbow, the one that takes weight on the straw, is in the world.</summary>
    internal static Vector3 Elbow(ActorRig rig) => rig.Root.Transform * rig.Elbow(true);

    /// <summary>Where a torch is in the world, through every node it hangs from.</summary>
    internal static Vector3 TorchAt(Node3D torch) {
        var point = Vector3.Zero;
        for (Node3D? node = torch; node != null; node = node.GetParent() as Node3D) point = node.Transform * point;
        return point;
    }

    /// <summary>A boot stays on top of the straw, close to it, and flat and in place while it bears weight.</summary>
    internal static void RequireBootOnStraw((Vector3 Toe, Vector3 Heel) actual, (Vector3 Toe, Vector3 Heel) start, float down, float clearance) {
        const float tolerance = .008f;
        float sole = Math.Min(actual.Toe.Y, actual.Heel.Y) - StrawBedModel.Top;
        Require(actual.Toe.IsFinite() && actual.Heel.IsFinite() && sole >= -tolerance,
            $"boot must not go through the straw at phase {down:F3}, got {actual}");
        Require(sole < clearance, $"the leg should fold close to the bed rather than kicking upward, got {sole:F3} m at {down:F3}");
        if (down > StrawBedView.BothBootsOnUntil) return;
        Require(Math.Abs(actual.Toe.Y - StrawBedModel.Top) < tolerance && Math.Abs(actual.Heel.Y - StrawBedModel.Top) < tolerance,
            $"both boots should stand flat while bearing weight at phase {down:F3}, got {actual}");
        Require(actual.Toe.DistanceTo(start.Toe) < tolerance && actual.Heel.DistanceTo(start.Heel) < tolerance,
            $"both boots should stay in place while bearing weight at phase {down:F3}, got {actual} from {start}");
    }

    /// <summary>A palm that bears weight stays on the straw where it landed.</summary>
    internal static void RequireHandPlanted(Vector3 actual, Vector3 start, string hand, float down) =>
        Require(Math.Abs(actual.Y - StrawBedModel.Top) < .012f && actual.DistanceTo(start) < .012f,
            $"the {hand} hand should stay planted at phase {down:F3}, got {actual} from {start}");
}
