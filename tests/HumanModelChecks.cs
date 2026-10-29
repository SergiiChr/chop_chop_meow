using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;

/// <summary>Shared human geometry stays cached while outfit materials and rig state remain separate.</summary>
public static class HumanModelChecks {
    public static void Run() {
        var rigs = new[] {
            new ActorRig(ActorSkin.Player), new ActorRig(ActorSkin.Worker),
            new ActorRig(ActorSkin.Courier), new ActorRig(ActorSkin.Player, "a45cc8")
        };
        try {
            var core = rigs.Select(Core).ToArray();
            string[] names = { "HumanHips", "HumanTorso", "HumanLeg", "HumanArmLeft", "HumanArmRight" };
            foreach (var meshes in core) {
                Require(meshes.Count == 6, "each rig should contain six core mesh instances, including both legs");
                Require(names.All(name => meshes.Count(m => m.Name == name) == (name == "HumanLeg" ? 2 : 1)), "each role should expose all five named core shapes");
            }
            foreach (string name in names)
                Require(core.SelectMany(meshes => meshes.Where(m => m.Name == name)).Select(m => m.Mesh!.GetInstanceId()).Distinct().Count() == 1,
                    $"{name} geometry should be shared across roles and shirt variants");
            var skeletonIds = core.SelectMany(meshes => meshes.Where(m => m.GetParent() is Skeleton3D))
                .Select(m => m.GetParent().GetInstanceId()).ToArray();
            Require(skeletonIds.Length == 16 && skeletonIds.Distinct().Count() == 16,
                "all four limb skeletons per rig must be independent across actors");

            foreach (string name in names) {
                int outfits = name is "HumanHips" or "HumanLeg" ? 3 : 4;
                Require(core.Select(meshes => meshes.First(m => m.Name == name)).Select(m => m.MaterialOverride!.GetInstanceId()).Distinct().Count() == outfits,
                    $"{name} should share a material only when its outfit colors match");
            }

            var playerArm = core[0].Single(m => m.Name == "HumanArmRight");
            var workerArm = core[1].Single(m => m.Name == "HumanArmRight");
            var playerTexture = (ImageTexture)((StandardMaterial3D)playerArm.MaterialOverride!).AlbedoTexture!;
            var workerTexture = (ImageTexture)((StandardMaterial3D)workerArm.MaterialOverride!).AlbedoTexture!;
            Require(playerTexture.GetImage().GetPixel(1, 0).IsEqualApprox(new Color("d9a27c")), "player short sleeves should expose the player's skin tone");
            Require(workerTexture.GetImage().GetPixel(1, 0).IsEqualApprox(new Color("e9dfc6")), "worker long sleeves should keep the shirt color through the forearm");
            var original = (StandardMaterial3D)playerArm.MaterialOverride!;
            ModelKit.SetFade(playerArm, original, .5f);
            Require(playerArm.MaterialOverride is StandardMaterial3D faded && faded.AlbedoTexture!.GetInstanceId() == playerTexture.GetInstanceId() && Math.Abs(faded.AlbedoColor.A - .5f) < .001f, "fading should preserve the outfit palette texture");
            ModelKit.SetFade(playerArm, original, 0);

            var right = (Skeleton3D)core[0].Single(m => m.Name == "HumanArmRight").GetParent();
            var other = (Skeleton3D)core[1].Single(m => m.Name == "HumanArmRight").GetParent();
            rigs[0].Show(new Pose(ElbowR: 15), null, false);
            rigs[1].Show(new Pose(ElbowR: 75), null, false);
            var playerPose = right.GetBonePoseRotation(1);
            var workerPose = other.GetBonePoseRotation(1);
            Require(!playerPose.IsEqualApprox(workerPose), "different elbow poses should reach each actor's lower bone");
            rigs[1].Show(new Pose(ElbowR: 110), null, false);
            Require(right.GetBonePoseRotation(1).IsEqualApprox(playerPose), "posing one rig must not mutate another rig's skeleton");

            CheckCarrySupport(rigs);
        } finally {
            foreach (var rig in rigs) rig.Root.Free();
        }
    }

    private static void CheckCarrySupport(ActorRig[] rigs) {
        foreach (var rig in rigs) {
            var cargo = FindCargo(rig.Root) ?? throw new InvalidOperationException("Human model check failed: carried cargo mesh was not found");
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
                    Require(palmCenter.X >= crateBounds.Min.X && palmCenter.X <= crateBounds.Max.X
                        && palmCenter.Z >= crateBounds.Min.Z && palmCenter.Z <= crateBounds.Max.Z,
                        $"{name} palm should remain inside the crate footprint while {(moving ? "walking" : "standing")}");
                }
            }
            rig.Show(new Pose(), null, false);
            Require(!((Node3D)cargo.GetParent()).Visible, "the shipping crate should hide when the actor is not carrying");
        }
    }

    private static MeshInstance3D? FindCargo(Node node) {
        if (node is MeshInstance3D mesh && mesh.Mesh?.GetInstanceId() == CrateShape.Cargo.GetInstanceId()) return mesh;
        for (int i = 0; i < node.GetChildCount(); i++)
            if (FindCargo(node.GetChild(i)) is { } found) return found;
        return null;
    }

    private static (Vector3 Min, Vector3 Max) BoundsInTorso(MeshInstance3D mesh) {
        var bounds = mesh.Mesh!.GetAabb();
        var toTorso = ((Node3D)mesh.GetParent()).Transform * mesh.Transform;
        var first = toTorso * bounds.Position;
        var min = first; var max = first;
        for (int corner = 1; corner < 8; corner++) {
            var point = toTorso * (bounds.Position + new Vector3(
                (corner & 1) != 0 ? bounds.Size.X : 0,
                (corner & 2) != 0 ? bounds.Size.Y : 0,
                (corner & 4) != 0 ? bounds.Size.Z : 0));
            min = new Vector3(Math.Min(min.X, point.X), Math.Min(min.Y, point.Y), Math.Min(min.Z, point.Z));
            max = new Vector3(Math.Max(max.X, point.X), Math.Max(max.Y, point.Y), Math.Max(max.Z, point.Z));
        }
        return (min, max);
    }

    private static List<MeshInstance3D> Core(ActorRig rig) {
        var result = new List<MeshInstance3D>();
        Visit(rig.Root, result);
        return result;
    }
    private static void Visit(Node node, List<MeshInstance3D> result) {
        if (node is MeshInstance3D mesh && mesh.Name.ToString().StartsWith("Human", StringComparison.Ordinal)) result.Add(mesh);
        for (int i = 0; i < node.GetChildCount(); i++) Visit(node.GetChild(i), result);
    }
}
