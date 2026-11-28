using System.Linq;
using Godot;
using static Checks;

namespace ChopChopMeow;

public static class HardHatModelChecks {
    public static void Run(GameData data) => Test(TheIconUsesTheWorkersHardHat, data);

    private static void TheIconUsesTheWorkersHardHat(GameData data) {
        var icon = WorldModels.IconModel("hardhat", false, data);
        Require(icon != null, "The icon renderer should have the worker's hard hat.");
        var mount = new Node3D();
        try {
            ActorRig.HardHat(mount);
            var worn = ModelSteps.AllMeshes(mount).Select(part => part.Mesh).ToArray();
            var rendered = ModelSteps.AllMeshes(icon).Select(part => part.Mesh);
            Require(worn.Length > 0 && rendered.SequenceEqual(worn), "The icon and the worn hard hat should share their meshes.");
            Require(ModelSteps.Triangles(icon) <= 1000, "The hard hat should fit the icon model triangle ceiling.");
        } finally { mount.Free(); icon.Free(); }
    }
}
