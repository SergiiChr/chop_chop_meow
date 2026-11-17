using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>The pond's bank, water, stones, reeds and lily pads, along the shore that bounds walking.</summary>
internal static class PondDressing {
    /// <summary>
    /// Adds the pond: its bank, its water, and its stones, reeds and lily pads.
    /// <paramref name="still"/> holds the waves at one moment.
    /// </summary>
    internal static void Build(Node3D parent, bool still) {
        var at = new Vector3(Terrain.Lake.X, 0, Terrain.Lake.Y);
        // The bank and water follow the same shore line that bounds walking.
        ShorelineAppearance.BuildLakeBank(parent);
        LakeAppearance.BuildLakeAppearance(parent, still);
        var rng = new Random(77);
        const int stones = 150;
        for (int i = 0; i < stones; i++) {
            float angle = i * Mathf.Tau / stones + (float)rng.NextDouble() * .03f;
            // Stones collect in irregular patches instead of forming an evenly spaced border.
            if (MathF.Sin(angle * 7 + 1.2f) + .55f * MathF.Sin(angle * 13) < -.05f) continue;
            float r = Terrain.Shore(angle) + .12f + (float)rng.NextDouble() * .95f;
            var stone = Sphere(parent, .1f + (float)rng.NextDouble() * .14f, at + new Vector3(MathF.Cos(angle) * r, .03f, MathF.Sin(angle) * r), Stone(i % 3 == 0 ? "a3a7a3" : "8d918e", .9f), new Vector3(1.2f, .5f, 1), 6);
            stone.RotationDegrees = new Vector3(0, i * 47, 0);
        }
        // Reed clumps with cattails along the banks, lily pads near the shore.
        for (float angle = .2f; angle < Mathf.Tau; angle += .45f + (float)rng.NextDouble() * .5f) {
            var reach = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (Terrain.Shore(angle) - .1f);
            var clump = at + new Vector3(reach.X, -Terrain.BedDepth(Terrain.Lake + reach), reach.Y);
            for (int i = 0; i < 7; i++) {
                var offset = new Vector3((float)rng.NextDouble() - .5f, 0, (float)rng.NextDouble() - .5f) * .6f;
                float height = .7f + (float)rng.NextDouble() * .6f;
                Cylinder(parent, .02f, .01f, height, clump + offset + new Vector3(0, height / 2, 0), Mat("6b8a3e"), new Vector3(((float)rng.NextDouble() - .5f) * 14, 0, ((float)rng.NextDouble() - .5f) * 14), 4);
                if (i % 3 == 0) Cylinder(parent, .035f, .035f, .16f, clump + offset + new Vector3(0, height - .05f, 0), Mat("6e4a2e"), null, 6);
            }
        }
        var lilyPad = LakeAppearance.LilyPadMaterial(LilyPadColor, still);
        for (int i = 0; i < 24; i++) {
            float angle = (float)rng.NextDouble() * Mathf.Tau, r = Terrain.Shore(angle) - 1 - (float)rng.NextDouble() * 3;
            LilyPad(parent, at + new Vector3(MathF.Cos(angle) * r, LakeAppearance.Level + .01f, MathF.Sin(angle) * r), lilyPad);
        }
    }

    /// <summary>
    /// Puts a patch of water and a lily pad on the stage drawn behind the ground while loading, so their shaders and
    /// pipelines are built before the player first sees the pond or the sea.
    /// </summary>
    internal static void Warm(Node3D stage) {
        stage.AddChild(new MeshInstance3D { Mesh = LakeAppearance.Grid([0, 1], [0, 1], (x, z) => new Vector3(x, 0, z)), MaterialOverride = LakeAppearance.CreateMaterial(false) });
        LilyPad(stage, Vector3.Zero, LakeAppearance.LilyPadMaterial(LilyPadColor, false));
    }

    private static readonly Color LilyPadColor = new("5b8a3e");

    private static void LilyPad(Node3D parent, Vector3 at, Material material) => Cylinder(parent, .28f, .28f, .01f, at, material, null, 9);
}
