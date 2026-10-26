using System;
using System.Collections.Generic;
using System.Linq;
using ChopChopMeow;
using Godot;

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
        } finally {
            foreach (var rig in rigs) rig.Root.Free();
        }
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
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException($"Human model check failed: {message}");
    }
}
