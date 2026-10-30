using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ChopChopMeow;

public partial class WorldView {
    /// <summary>ProtonScatter generates the candidates offline; gameplay geometry masks them once during loading.</summary>
    private void BuildPlacedEnvironment(Node3D parent) {
        using var document = JsonDocument.Parse(FileAccess.GetFileAsString("res://assets/environment/proton_scatter/placements.json"));
        var grass = new List<(Transform3D At, Color Color)>();
        var flowers = new List<(Transform3D At, Color Color)>();
        var ferns = new List<(Transform3D At, Color Color)>();
        var bushes = new List<(Transform3D At, Color Color)>();
        var stones = new List<(Transform3D At, Color Color)>();
        var branches = new List<(Transform3D At, Color Color)>();
        var patch = new FastNoiseLite { Seed = 671, Frequency = .18f, FractalOctaves = 2 };
        var blocked = new bool[Terrain.Size, Terrain.Size];
        // Decorative plants have no collision or inventory. Keep actual resources and initial buildings readable.
        foreach (var thing in World.S.Things) {
            float radius = thing.Kind == ThingKind.Tree ? .45f : thing.IsResource ? .8f : 2.2f;
            for (int y = Math.Max(0, (int)(thing.Pos.Y - radius)); y <= Math.Min(Terrain.Size - 1, (int)(thing.Pos.Y + radius)); y++)
                for (int x = Math.Max(0, (int)(thing.Pos.X - radius)); x <= Math.Min(Terrain.Size - 1, (int)(thing.Pos.X + radius)); x++)
                    if (thing.Pos.DistanceTo(new Vector2(x + .5f, y + .5f)) < radius + .5f) blocked[x, y] = true;
        }
        int index = 0;
        foreach (var point in document.RootElement.GetProperty("points").EnumerateArray()) {
            int id = index++;
            var p = new Vector2(point[0].GetSingle(), point[1].GetSingle());
            if (!Terrain.Open(p) || Terrain.Beach(p) || Terrain.InLake(p, 1.1f) || Terrain.FromMountains(p) < 2 || blocked[(int)p.X, (int)p.Y]) continue;
            float along = Terrain.Along(p), across = Terrain.Across(p), path = Terrain.Across(Terrain.Spawn) + MathF.Sin(along * .6f) * .13f + MathF.Sin(along * .08f) * 2.5f;
            if (p.DistanceTo(Terrain.Spawn) < 4.5f || along > Terrain.Along(Terrain.Spawn) - 1 && Math.Abs(across - path) < 1.1f) continue;
            bool mineApproach = false;
            foreach (var cliff in World.Cliffs) {
                var offset = p - World.MineSpot(cliff); float depth = offset.Dot(cliff.Face);
                if (Math.Abs(offset.Dot(cliff.Along)) < 3 && depth > -6 && depth < 2) { mineApproach = true; break; }
            }
            if (mineApproach) continue;
            float density = patch.GetNoise2D(p.X, p.Y);
            if (density < -.32f) continue;
            float yaw = point[2].GetSingle(), size = point[3].GetSingle();
            var basis = new Basis(Vector3.Up, yaw).Scaled(Vector3.One * size);
            var at = new Transform3D(basis, To3(p));
            // Restore the ordinary shrubs alongside the new ground cover. Use the same
            // dry-ground and access masks, and keep their sampling independent of other plants.
            if (id % 31 == 0 && Terrain.FromMountains(p) >= 3 && p.DistanceTo(Terrain.Spawn) >= 6) {
                for (int lobe = 0; lobe < 3; lobe++) {
                    float angle = yaw + lobe * Mathf.Tau / 3;
                    float radius = (.35f + ((id + lobe * 3) % 7) * .05f) * size;
                    var offset = new Vector3(MathF.Cos(angle) * .23f, radius * .35f, MathF.Sin(angle) * .23f);
                    var shrubBasis = new Basis(Vector3.Up, angle).Scaled(new Vector3(radius, radius * .75f, radius));
                    bushes.Add((new Transform3D(shrubBasis, To3(p) + offset), new Color("527238").Lerp(new Color("6a8a45"), ((id + lobe) % 5) / 4f)));
                    ShadeScenery(p + new Vector2(offset.X, offset.Z), radius * 1.1f, .45f);
                }
            }
            if (id % 127 == 0) {
                stones.Add((new Transform3D(basis.Scaled(new Vector3(.24f, .12f, .19f)), To3(p, .06f)), new Color("939184")));
                ShadeScenery(p, .3f * size, .6f);
            } else if (id % 293 == 0) {
                branches.Add((new Transform3D(basis, To3(p, .06f)), new Color("78644a")));
                ShadeScenery(p, .3f * size, .3f);
            } else if (id % 13 == 0 && density > .02f) {
                ferns.Add((at, new Color("56783e").Lerp(new Color("829552"), (id % 7) / 7f)));
                ShadeScenery(p, .45f * size, .35f);
            } else {
                grass.Add((at, new Color("678049").Lerp(new Color("9aa46a"), (id % 11) / 11f)));
                ShadeScenery(p, .3f * size, .45f);
                if (id % 19 == 0 && density > .12f)
                    flowers.Add((new Transform3D(basis, To3(p, .26f)), id % 3 == 0 ? new Color("e7ca73") : new Color("d6c5df")));
            }
        }
        Scatter(parent, MeadowMesh(), grass, dressingMaterial);
        Scatter(parent, FernMesh(), ferns, dressingMaterial);
        // All scenery darkens the baked ground shade; shrubs, pebbles and twigs are also few and large enough to cast real shadows.
        Scatter(parent, NatureShape.Scrub, bushes, solidDressingMaterial, true);
        Scatter(parent, new SphereMesh { Radius = .055f, Height = .06f, RadialSegments = 5, Rings = 3 }, flowers, dressingMaterial);
        Scatter(parent, new SphereMesh { Radius = 1, Height = 2, RadialSegments = 7, Rings = 4 }, stones, stillDressingMaterial, true);
        Scatter(parent, new CylinderMesh { TopRadius = .045f, BottomRadius = .07f, Height = .8f, RadialSegments = 5 },
            RotateBranches(branches), stillDressingMaterial, true);
        GD.Print($"PROTON-SCATTER: {grass.Count} meadow patches, {ferns.Count} ferns, {bushes.Count / 3} bushes, {flowers.Count} flowers, {stones.Count} pebbles, {branches.Count} twigs (baked candidates: {index})");
    }

    private static List<(Transform3D At, Color Color)> RotateBranches(List<(Transform3D At, Color Color)> branches) {
        for (int i = 0; i < branches.Count; i++) {
            var item = branches[i];
            item.At.Basis *= new Basis(Vector3.Right, Mathf.Pi / 2);
            branches[i] = item;
        }
        return branches;
    }

    private static ArrayMesh MeadowMesh() {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 7; i++) {
            float angle = i * 2.39996f;
            var lean = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
            var baseAt = lean * (.08f + i * .023f);
            var side = new Vector3(-lean.Z, 0, lean.X) * .025f;
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(baseAt - side); surface.AddVertex(baseAt + side);
            surface.AddVertex(baseAt + lean * .1f + Vector3.Up * (.16f + i * .018f));
        }
        return surface.Commit();
    }

    private static ArrayMesh FernMesh() {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int frond = 0; frond < 7; frond++) {
            float angle = frond * Mathf.Tau / 7;
            var forward = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
            var side = new Vector3(-forward.Z, 0, forward.X);
            for (int leaf = 1; leaf <= 6; leaf++) {
                float t = leaf / 7f;
                var center = forward * (t * .58f) + Vector3.Up * (MathF.Sin(t * Mathf.Pi * .8f) * .38f);
                float width = .13f * (1 - t) + .015f;
                foreach (int sign in new[] { -1, 1 }) {
                    surface.SetNormal(Vector3.Up);
                    surface.AddVertex(center - forward * .045f);
                    surface.AddVertex(center + side * width * sign - forward * .08f);
                    surface.AddVertex(center + forward * .055f);
                }
            }
        }
        return surface.Commit();
    }
}
