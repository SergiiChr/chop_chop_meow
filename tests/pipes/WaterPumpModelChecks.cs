using Godot;
using static Checks;

namespace ChopChopMeow;

public static class WaterPumpModelChecks {
    public static void Run() {
        Test(CheckPistonSlidesAndRockerStaysPinned);
        Test(CheckBothOperatorsKeepTheirFistsOnThePumpHandle);
    }

    private static void CheckPistonSlidesAndRockerStaysPinned() {
        var parent = new Node3D();
        try {
            var pump = new WaterPumpShape(parent);
            var piston = parent.FindChild("Piston", true, false) as Node3D;
            var rocker = parent.FindChild("Rocker", true, false) as Node3D;
            var pin = rocker?.FindChild("RockerPin", true, false) as Node3D;
            Require(piston != null && rocker != null && pin != null, "The pump must show the piston rod and its hinged link.");
            var baseAt = piston.Position;
            var foot = rocker.Position;
            for (int frame = 0; frame <= 60; frame++) {
                pump.SetPress(frame / 60f);
                Require(Mathf.IsEqualApprox(piston.Position.X, baseAt.X) && Mathf.IsEqualApprox(piston.Position.Z, baseAt.Z),
                    "The piston must slide on one vertical axis through its seal.");
                Require(piston.Position.IsEqualApprox(pump.Lever.Position) && rocker.Position.IsEqualApprox(foot),
                    "The lever must stay on the piston pin and the link must stay on its fixed pin.");
                var through = pump.Lever.Transform.AffineInverse() * (rocker.Transform * pin.Position);
                Require(through is { X: > .4f and < .6f } && Mathf.IsZeroApprox(through.Y) && Mathf.IsZeroApprox(through.Z),
                    "The link's top pin must stay seated through the lever.");
            }
            Require(piston.Position.Y > baseAt.Y + .08f && ModelSteps.Triangles(parent) < 2500,
                "The piston must travel visibly while all pump parts stay under the building triangle ceiling.");
        } finally { parent.Free(); }
    }

    private static void CheckBothOperatorsKeepTheirFistsOnThePumpHandle() {
        var parent = new Node3D();
        try {
            var pump = new WaterPumpShape(parent);
            var handle = pump.Lever.GetNode<MeshInstance3D>("PumpHandle");
            var leverPlane = (Node3D)pump.Lever.GetParent();
            foreach (var skin in new[] { ActorSkin.Player, ActorSkin.Worker }) {
                var rig = new ActorRig(skin);
                parent.AddChild(rig.Root);
                rig.Root.Position = new Vector3(0, 0, -2.1f);
                for (int frame = 0; frame <= 60; frame++) {
                    var pose = ActorAnimation.PoseAt(ActorAction.Pump, frame / 60f, false);
                    float pressed = Mathf.Clamp((pose.Lean - 16) / 16, 0, 1);
                    pump.SetPress(pressed);
                    var grip = leverPlane.Transform * pump.Lever.Transform * handle.Transform;
                    var left = grip * (Vector3.Up * .104f);
                    var right = grip * (Vector3.Down * .104f);
                    Require(left.DistanceTo(WaterPumpShape.GripAt(pressed, true)) < .001f
                        && right.DistanceTo(WaterPumpShape.GripAt(pressed, false)) < .001f,
                        "The contact points must match the ends of the drawn grip.");
                    rig.Show(pose, null, false);
                    rig.HoldPumpHandle(pose, left, right, 1);
                    Require((rig.Root.Transform * rig.Fist(true)).DistanceTo(left) < .012f
                        && (rig.Root.Transform * rig.Fist(false)).DistanceTo(right) < .012f,
                        $"{skin}'s hands must stay on the drawn grip at frame {frame} of the stroke.");
                }
                rig.Root.Free();
            }
        } finally { parent.Free(); }
    }
}
