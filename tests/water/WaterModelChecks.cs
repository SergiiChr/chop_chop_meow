using System.Linq;
using Godot;
using static Checks;

namespace ChopChopMeow;

/// <summary>Checks of how bottles look: their icons and the pieces a pile heaps them as, at every fill.</summary>
public static class WaterModelChecks {
    public static void Run(GameData data) {
        Test(CheckBottleIconsAndPilesKeepTheirShapeAndFill, data);
    }

    private static void CheckBottleIconsAndPilesKeepTheirShapeAndFill(GameData data) {
        var blue = new Color("43b9e6");
        var jamColor = new Color("9b2f4f");
        for (int liters = 1; liters <= Water.Capacity; liters++) {
            string waterItem = Water.Looks[liters], jamItem = BerryJam.Looks[liters];
            var water = WorldModels.IconModel(waterItem, false, data);
            var jam = WorldModels.IconModel(jamItem, false, data);
            try {
                Require(water != null && jam != null, "water and jam at any fill must both have a flask model");
                var waterParts = ModelSteps.AllMeshes(water!).ToArray();
                var jamParts = ModelSteps.AllMeshes(jam!).ToArray();
                Require(waterParts.Length == jamParts.Length && waterParts.Zip(jamParts).All(pair =>
                        pair.First.Mesh == pair.Second.Mesh && pair.First.Transform.IsEqualApprox(pair.Second.Transform)),
                    "water and jam icons must use the same flask geometry");
                Require(Marks(waterParts, blue) == liters && Marks(jamParts, jamColor) == liters,
                    $"each icon should show {liters} marks in its own liquid's color");

                // A pile tints a look the game has no item for, such as a part-full bottle, in the fallback color.
                var waterPiece = PileShape.HeapOf(waterItem, data.Items.TryGetValue(waterItem, out var waterDef) ? waterDef.Color : PileShape.Fallback).Mesh;
                var jamPiece = PileShape.HeapOf(jamItem, data.Items.TryGetValue(jamItem, out var jamDef) ? jamDef.Color : PileShape.Fallback).Mesh;
                Require(waterPiece.GetFaces().SequenceEqual(jamPiece.GetFaces()),
                    "water and jam in a pile must be heaped as the same flask");
                int Colored(Mesh piece, Color color) => piece.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Color].AsColorArray()
                    .Count(tint => tint.IsEqualApprox(color));
                Require(Colored(waterPiece, blue) > 0 && Colored(waterPiece, blue) == Colored(jamPiece, jamColor),
                    "each pile piece must keep its liquid's full marks");
            } finally { water?.Free(); jam?.Free(); }
        }

        static int Marks(MeshInstance3D[] parts, Color color) => parts.Count(part =>
            part.MaterialOverride is StandardMaterial3D gauge && gauge.AlbedoColor.IsEqualApprox(color));
    }
}
