using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Contact checks for the actual rig and props used to carry and fill a barrel.</summary>
internal static class BarrelAnimationChecks {
    public static void Run() {
        Test(CheckPlayerWalksUnderTheWeightOfWater);
        Test(CheckActorsHugTheBarrelThroughAFullStride);
        Test(CheckBarrelCarryKeepsTheTorchVisible);
        Test(CheckWorkerKeepsTheirHandOnTheLadderUntilTheNextReach);
        Test(CheckBothVesselsPourIntoTheRoofOpening);
        Test(CheckBucketPoursOntoTheFarmSoil);
        Test(CheckBottleGaugeKeepsItsFillAfterFading);
        Test(CheckHandsShowTheSelectedFlaskLiquid);
    }

    private static void CheckPlayerWalksUnderTheWeightOfWater() {
        foreach (int liters in new[] { 0, 1, Barrel.Capacity }) {
            var backpack = new Inventory();
            backpack.Add(Barrel.Item, 4);
            if (liters > 0) backpack.Add(Barrel.AtFill(liters), 1);
            var expected = liters == 0 ? ActorAction.Jog : ActorAction.Walk;
            Require(ActorAnimation.PlayerAction(null, false, true, backpack) == expected,
                $"the player should use {expected} while carrying a barrel with {liters} L");
        }
    }

    private static void CheckActorsHugTheBarrelThroughAFullStride() {
        foreach (var (skin, action) in new[] { (ActorSkin.Player, ActorAction.Walk), (ActorSkin.Player, ActorAction.Jog), (ActorSkin.Worker, ActorAction.Carry) }) {
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

    private static void CheckBucketPoursOntoTheFarmSoil() {
        var rig = new ActorRig(ActorSkin.Worker);
        var stream = rig.Root.GetNode<Node3D>("WaterStream");
        var soil = new Vector3(.4f, .03f, .4f);
        int flowing = 0;
        const int frames = 120;
        for (int frame = 0; frame <= frames; frame++) {
            WorkerWaterView.Show(rig, Vector2.Zero, soil, WorkerWaterAction.Pour, false,
                frame / (float)frames, Bucket.AtFill(10), ground: true);
            if (!stream.Visible) continue;
            flowing++;
            var end = rig.Root.Transform * (stream.Position - stream.Basis.Y * .5f);
            var lip = rig.Root.Transform * (stream.Position + stream.Basis.Y * .5f);
            Require(end.DistanceTo(soil) < .004f && lip.Y - end.Y > .35f,
                $"the bucket should pour down onto the soil at frame {frame}");
        }
        Require(flowing is >= 20 and <= 35, "the bucket should pour visibly for about half a second");
        rig.Root.Free();
    }

    private static void CheckBottleGaugeKeepsItsFillAfterFading() {
        var rig = new ActorRig(ActorSkin.Worker);
        var bottle = rig.Root.GetNode<Node3D>("CarriedBottle");
        var parts = ModelKit.FadeParts(rig.Root);
        var blue = new Color("43b9e6");
        WorkerWaterView.Carry(rig, new Pose(), Water.Full);
        ModelKit.SetFade(parts, .5f);
        WorkerWaterView.Carry(rig, new Pose(), Water.TwoLiters);
        ModelKit.SetFade(parts, 0);
        WorkerWaterView.Carry(rig, new Pose(), Water.TwoLiters);
        int lit = bottle.GetChildren().OfType<MeshInstance3D>().Count(mesh =>
            mesh.Visible && mesh.MaterialOverride is StandardMaterial3D gauge && gauge.AlbedoColor == blue);
        Require(lit == 2, $"the bottle should still show two filled marks after the worker returns, found {lit}");
        rig.Root.Free();
    }

    private static void CheckHandsShowTheSelectedFlaskLiquid() {
        foreach (var skin in new[] { ActorSkin.Player, ActorSkin.Worker }) {
            var rig = new ActorRig(skin);
            try {
                var bottle = rig.Root.GetNode<Node3D>("CarriedBottle");
                var jam = new Color("9b2f4f");
                var blue = new Color("43b9e6");
                var poses = new[] { new Pose(), ActorAnimation.PoseAt(ActorAction.Walk, .2f, true) };
                foreach (var pose in poses) {
                    WorkerWaterView.Carry(rig, pose, Water.TwoLiters);
                    Require(bottle.Visible && Bars(blue) == 2 && Bars(jam) == 0, "a held water bottle must show blue");
                    WorkerWaterView.Carry(rig, pose, BerryJam.Bottles[2]);
                    Require(bottle.Visible && Bars(jam) == 2 && Bars(blue) == 0, "changing liquids at the same fill must change the color");
                    Require((bottle.Transform * ActorRig.BottleGrip).DistanceTo(rig.Fist(false)) < .001f,
                        "the flask must stay in the fist while the actor walks");
                    WorkerWaterView.Carry(rig, pose, Water.Empty);
                    Require(bottle.Visible && Bars(blue) == 0 && Bars(jam) == 0, "an empty flask must not retain the previous liquid's color");
                }

                int Bars(Color color) => bottle.GetChildren().OfType<MeshInstance3D>().Count(part =>
                    part.Visible && part.MaterialOverride is StandardMaterial3D material && material.AlbedoColor.IsEqualApprox(color));
            } finally { rig.Root.Free(); }
        }
    }
}
