using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static ModelSteps;

/// <summary>Shared human geometry stays cached while outfit materials and rig state remain separate.</summary>
public static class HumanModelChecks {
    private static readonly string[] CoreParts = { "HumanHips", "HumanTorso", "HumanLeg", "HumanArmLeft", "HumanArmRight" };

    public static void Run() {
        var rigs = new[] {
            new ActorRig(ActorSkin.Player), new ActorRig(ActorSkin.Worker),
            new ActorRig(ActorSkin.Courier), new ActorRig(ActorSkin.Player, "a45cc8")
        };
        try {
            var core = rigs.Select(Core).ToArray();
            CheckRigsShareGeometryButKeepTheirOwnSkeletons(core);
            CheckOutfitsShareMaterialsOnlyWhenTheirColorsMatch(core);
            CheckSleevesShowSkinOrShirtAndFadingKeepsThePalette(core);
            CheckPosingOneRigLeavesAnotherAlone(rigs, core);
            CheckCarriedCrateFollowsCarryingVisibility(rigs);
        } finally {
            foreach (var rig in rigs) rig.Root.Free();
        }
    }

    private static void CheckRigsShareGeometryButKeepTheirOwnSkeletons(List<MeshInstance3D>[] core) {
        foreach (var meshes in core) {
            Require(meshes.Count == 6, "each rig should contain six core mesh instances, including both legs");
            Require(CoreParts.All(name => meshes.Count(m => m.Name == name) == (name == "HumanLeg" ? 2 : 1)), "each role should expose all five named core shapes");
        }
        foreach (string name in CoreParts)
            Require(core.SelectMany(meshes => meshes.Where(m => m.Name == name)).Select(m => m.Mesh!.GetInstanceId()).Distinct().Count() == 1,
                $"{name} geometry should be shared across roles and shirt variants");
        var skeletonIds = core.SelectMany(meshes => meshes.Where(m => m.GetParent() is Skeleton3D))
            .Select(m => m.GetParent().GetInstanceId()).ToArray();
        Require(skeletonIds.Length == 16 && skeletonIds.Distinct().Count() == 16,
            "all four limb skeletons per rig must be independent across actors");
    }

    private static void CheckOutfitsShareMaterialsOnlyWhenTheirColorsMatch(List<MeshInstance3D>[] core) {
        foreach (string name in CoreParts) {
            int outfits = name is "HumanHips" or "HumanLeg" ? 3 : 4;
            Require(core.Select(meshes => meshes.First(m => m.Name == name)).Select(m => m.MaterialOverride!.GetInstanceId()).Distinct().Count() == outfits,
                $"{name} should share a material only when its outfit colors match");
        }
    }

    private static void CheckSleevesShowSkinOrShirtAndFadingKeepsThePalette(List<MeshInstance3D>[] core) {
        var playerArm = core[0].Single(m => m.Name == "HumanArmRight");
        var workerArm = core[1].Single(m => m.Name == "HumanArmRight");
        var playerTexture = (ImageTexture)((StandardMaterial3D)playerArm.MaterialOverride!).AlbedoTexture!;
        var workerTexture = (ImageTexture)((StandardMaterial3D)workerArm.MaterialOverride!).AlbedoTexture!;
        Require(playerTexture.GetImage().GetPixel(1, 0).IsEqualApprox(new Color("d9a27c")), "player short sleeves should expose the player's skin tone");
        Require(workerTexture.GetImage().GetPixel(1, 0).IsEqualApprox(new Color("e9dfc6")), "worker long sleeves should keep the shirt color through the forearm");
        var original = (StandardMaterial3D)playerArm.MaterialOverride!;
        ModelKit.SetFade(playerArm, original, .5f);
        var faded = playerArm.MaterialOverride as StandardMaterial3D;
        Require(faded != null, "fading should give the arm a standard material");
        Require(faded.AlbedoTexture!.GetInstanceId() == playerTexture.GetInstanceId(),
            "fading should preserve the outfit palette texture");
        Require(Math.Abs(faded.AlbedoColor.A - .5f) < .001f, "fading should make the material half transparent");
        ModelKit.SetFade(playerArm, original, 0);
    }

    private static void CheckPosingOneRigLeavesAnotherAlone(ActorRig[] rigs, List<MeshInstance3D>[] core) {
        var right = (Skeleton3D)core[0].Single(m => m.Name == "HumanArmRight").GetParent();
        var other = (Skeleton3D)core[1].Single(m => m.Name == "HumanArmRight").GetParent();
        rigs[0].Show(new Pose(ElbowR: 15), null, false);
        rigs[1].Show(new Pose(ElbowR: 75), null, false);
        var playerPose = right.GetBonePoseRotation(1);
        var workerPose = other.GetBonePoseRotation(1);
        Require(!playerPose.IsEqualApprox(workerPose), "different elbow poses should reach each actor's lower bone");
        rigs[1].Show(new Pose(ElbowR: 110), null, false);
        Require(right.GetBonePoseRotation(1).IsEqualApprox(playerPose), "posing one rig must not mutate another rig's skeleton");
    }

    private static void CheckCarriedCrateFollowsCarryingVisibility(ActorRig[] rigs) {
        foreach (var rig in rigs) {
            var cargo = FindCargo(rig.Root);
            Require(cargo != null, "the carried cargo mesh should be found");
            Require(cargo.Mesh!.GetInstanceId() == CrateShape.Cargo.GetInstanceId(), "carried cargo should reuse the cached shipping-crate mesh");
            var crateBounds = BoundsInTorso(cargo);
            foreach (bool moving in new[] { false, true }) {
                var pose = ActorAnimation.PoseAt(ActorAction.Carry, .2f, moving);
                rig.Show(pose, null, true);
                Require(cargo.Visible && ((Node3D)cargo.GetParent()).Visible, "the shipping crate should follow the carrying visibility handle");
                foreach (string name in new[] { "HumanArmLeft", "HumanArmRight" }) {
                    var arm = Core(rig).Single(m => m.Name == name);
                    var skeleton = (Skeleton3D)arm.GetParent();
                    var shoulder = (Node3D)skeleton.GetParent();
                    var lower = skeleton.GetBoneRest(1)
                        * new Transform3D(new Basis(skeleton.GetBonePoseRotation(1)), Vector3.Zero)
                        * arm.Skin!.GetBindPose(1);
                    var toTorso = shoulder.Transform * skeleton.Transform * arm.Transform * lower;
                    var palmCenter = toTorso * new Vector3(0, -.565f, .015f);
                    var palmTop = toTorso * new Vector3(0, -.565f, .056f);
                    float palmMaxY = Math.Max(palmCenter.Y, palmTop.Y);
                    Require(palmMaxY >= crateBounds.Min.Y - .04f && palmMaxY <= crateBounds.Min.Y + .01f,
                        $"{name} should support the crate from below while {(moving ? "walking" : "standing")}");
                    Require(palmCenter.X >= crateBounds.Min.X, $"{name} palm should not pass the crate footprint's near X edge while {(moving ? "walking" : "standing")}");
                    Require(palmCenter.X <= crateBounds.Max.X, $"{name} palm should not pass the crate footprint's far X edge while {(moving ? "walking" : "standing")}");
                    Require(palmCenter.Z >= crateBounds.Min.Z, $"{name} palm should not pass the crate footprint's near Z edge while {(moving ? "walking" : "standing")}");
                    Require(palmCenter.Z <= crateBounds.Max.Z, $"{name} palm should not pass the crate footprint's far Z edge while {(moving ? "walking" : "standing")}");
                }
            }
            rig.Show(new Pose(), null, false);
            Require(!((Node3D)cargo.GetParent()).Visible, "the shipping crate should hide when the actor is not carrying");
        }
    }
}
