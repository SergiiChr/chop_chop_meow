using System;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Contact checks for the actual rig and props used to carry and fill a barrel.</summary>
internal static class BarrelAnimationChecks {
    public static void Run() {
        CheckActorsHugTheBarrelThroughAFullStride();
        CheckBarrelCarryKeepsTheTorchVisible();
        CheckWorkerKeepsTheirHandOnTheLadderUntilTheNextReach();
        CheckBothVesselsPourIntoTheRoofOpening();
    }

    private static void CheckActorsHugTheBarrelThroughAFullStride() {
        foreach (var (skin, action) in new[] { (ActorSkin.Player, ActorAction.Jog), (ActorSkin.Worker, ActorAction.Carry) }) {
            var rig = new ActorRig(skin);
            var barrel = rig.Torso.GetNode<Node3D>("CarriedBarrel");
            for (int frame = 0; frame < 60; frame++) {
                var pose = ActorAnimation.PoseAt(action, ActorAnimation.Duration(action) * frame / 60, true);
                rig.HugBarrel(pose);
                var barrelFromRoot = rig.Torso.Transform * barrel.Transform;
                var fromHips = barrelFromRoot.AffineInverse();
                foreach (bool left in new[] { true, false }) {
                    var fist = fromHips * (rig.Fist(left) - Vector3.Up * pose.HipHeight);
                    float radius = new Vector2(fist.X, fist.Z).Length();
                    Require(barrel.Visible && Math.Abs(radius - VesselShape.BarrelRadius) < .018f &&
                        fist.Z > 0 && (left ? fist.X > 0 : fist.X < 0),
                        $"{skin} should hug the front sides of the barrel as it strides at frame {frame}, fist is {fist}");
                }
            }
            rig.Root.Free();
        }
    }

    private static void CheckBarrelCarryKeepsTheTorchVisible() {
        var rig = new ActorRig(ActorSkin.Player);
        var torch = rig.Flame.GetParent<Node3D>();
        var held = torch.Transform;
        rig.HugBarrel(new Pose(), lit: true);
        Require(torch.Visible && torch.Transform != held, "both hands hold the barrel while the torch stays visible beside the left grip");
        rig.Show(new Pose(), null, false, lit: true);
        Require(torch.Visible && torch.Transform.IsEqualApprox(held), "putting down the barrel restores the normal torch grip");
        rig.Root.Free();
    }

    private static void CheckWorkerKeepsTheirHandOnTheLadderUntilTheNextReach() {
        var rig = new ActorRig(ActorSkin.Worker);
        var rung = new Vector3(CabinWaterModel.LadderX, CabinWaterModel.FirstRung + 5 * CabinWaterModel.RungSpacing,
            CabinWaterModel.LadderZ + CabinWaterModel.LadderWidth * .28f);
        foreach (float phase in new[] { .23f, .24f }) {
            WorkerWaterView.Show(rig, Vector2.Zero, Vector3.Zero, WorkerWaterAction.ClimbUp, true,
                phase, Bucket.Item);
            var fist = rig.Root.Transform * rig.Fist(true);
            Require(fist.DistanceTo(rung) < .035f, $"the left hand should hold a rung as the worker climbs, fist is {fist}");
        }
        rig.Root.Free();
    }

    private static void CheckBothVesselsPourIntoTheRoofOpening() {
        var rig = new ActorRig(ActorSkin.Worker);
        var stream = rig.Root.GetNode<Node3D>("WaterStream");
        foreach (string vessel in new[] { Bucket.Item, Water.Full }) {
            WorkerWaterView.Show(rig, Vector2.Zero, Vector3.Zero, WorkerWaterAction.Pour, true, .5f, vessel);
            var inlet = rig.Root.Transform * (stream.Position - stream.Basis.Y * .5f);
            var outlet = rig.Root.Transform * (stream.Position + stream.Basis.Y * .5f);
            Require(stream.Visible && inlet.DistanceTo(CabinWaterModel.FillOpening) < .004f && outlet.Y > inlet.Y,
                $"the water from {vessel} should run down into the roof barrel, ends at {inlet} and {outlet}");
        }
        rig.Root.Free();
    }
}
