using Godot;
using static Checks;

namespace ChopChopMeow;

public static class WaterPumpModelChecks {
    public static void Run() {
        Test(CheckPistonSlidesAndRockerStaysPinned);
        Test(CheckBothOperatorsKeepTheirHandsAndFeetPlanted);
        Test(CheckInterruptedStrokeReturnsGradually);
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
            var raisedGrip = WaterPumpShape.GripAt(0, true);
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
            Require(piston.Position.Y > baseAt.Y + .28f && raisedGrip.Y - WaterPumpShape.GripAt(1, true).Y > .82f,
                "The piston must travel at least .28 m and the grip must drop at least .82 m.");
            Require(ModelSteps.Triangles(parent) < 2500, "The pump must fit the building triangle ceiling.");
        } finally { parent.Free(); }
    }

    private static void CheckBothOperatorsKeepTheirHandsAndFeetPlanted() {
        var parent = new Node3D();
        try {
            var pump = new WaterPumpShape(parent);
            var handle = pump.Lever.GetNode<MeshInstance3D>("PumpHandle");
            var leverPlane = (Node3D)pump.Lever.GetParent();
            var stand = World.BellowsStand;
            foreach (var skin in new[] { ActorSkin.Player, ActorSkin.Worker }) {
                var rig = new ActorRig(skin);
                parent.AddChild(rig.Root);
                for (int quarter = 0; quarter < 4; quarter++) {
                    float yaw = quarter * Mathf.Pi / 2;
                    var turn = new Basis(Vector3.Up, yaw);
                    rig.Root.Transform = new Transform3D(turn, turn * new Vector3(stand.X, 0, stand.Y));
                    var raised = ActorAnimation.PoseAt(ActorAction.PumpWater, 0, false);
                    for (int frame = 0; frame <= 60; frame++) {
                        float time = frame / 60f;
                        var pose = ActorAnimation.PoseAt(ActorAction.PumpWater, time, false);
                        float pressed = ActorAnimation.WaterPumpPress(time);
                        pump.SetPress(pressed);
                        var grip = leverPlane.Transform * pump.Lever.Transform * handle.Transform;
                        var left = grip * (Vector3.Up * .104f);
                        var right = grip * (Vector3.Down * .104f);
                        Require(left.DistanceTo(WaterPumpShape.GripAt(pressed, true)) < .001f
                            && right.DistanceTo(WaterPumpShape.GripAt(pressed, false)) < .001f,
                            "The contact points must match the ends of the drawn grip.");
                        rig.Show(pose, null, false);
                        rig.HoldPumpHandle(pose, turn * left, turn * right, 1);
                        Require((rig.Root.Transform * rig.Fist(true)).DistanceTo(turn * left) < .012f
                            && (rig.Root.Transform * rig.Fist(false)).DistanceTo(turn * right) < .012f,
                            $"{skin}'s hands must stay on the grip at turn {quarter}, frame {frame}.");
                        CheckBoot(pose, raised, true);
                        CheckBoot(pose, raised, false);
                    }
                }
                rig.Root.Free();
            }
        } finally { parent.Free(); }

        static void CheckBoot(Pose pose, Pose raised, bool left) {
            var leg = pose.Leg(left);
            var (toe, heel) = ActorAnimation.SoleOf(leg.Swing, leg.Knee, leg.Foot);
            var rest = raised.Leg(left);
            var restToe = ActorAnimation.SoleOf(rest.Swing, rest.Knee, rest.Foot).Toe;
            Require(Mathf.Abs(toe.X + pose.HipShift - restToe.X - raised.HipShift) < .003f
                && Mathf.Abs(toe.Y - pose.HipHeight) < .003f && heel.Y < pose.HipHeight + .003f,
                "The boot must keep its toe planted without sinking its heel as the hips move.");
        }
    }

    private static void CheckInterruptedStrokeReturnsGradually() {
        var prop = new WaterPumpProp();
        try {
            prop.ShowStroke(1, 0);
            float bottom = prop.Pump.Lever.Rotation.Z;
            prop.ShowStroke(null, ActorAnimation.BlendSeconds * .5f);
            float halfway = prop.Pump.Lever.Rotation.Z;
            prop.ShowStroke(null, ActorAnimation.BlendSeconds * .5f);
            float raised = prop.Pump.Lever.Rotation.Z;
            Require(bottom < halfway && halfway < raised && Mathf.IsEqualApprox(raised, -Mathf.DegToRad(2)),
                "An interrupted stroke must return through an intermediate pose and settle at the top.");
        } finally { prop.Root.Free(); }
    }
}
