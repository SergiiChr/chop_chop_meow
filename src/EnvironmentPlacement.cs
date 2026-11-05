using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

public partial class WorldView {
    /// <summary>ProtonScatter generates the candidates offline; gameplay geometry masks them once during loading. The loose stones and sticks among them are world things.</summary>
    private void BuildPlacedEnvironment(Node3D parent) {
        var grass = new List<(Transform3D At, Color Color)>();
        var flowers = new List<(Transform3D At, Color Color)>();
        var ferns = new List<(Transform3D At, Color Color)>();
        var bushes = new List<(Transform3D At, Color Color)>();
        var blocked = new bool[Terrain.Size, Terrain.Size];
        // Decorative plants have no collision or inventory. Keep actual resources and initial buildings readable.
        foreach (var thing in World.Things) {
            float radius = thing.Kind == ThingKind.Tree ? .45f : thing.IsResource ? .8f : 2.2f;
            for (int y = Math.Max(0, (int)(thing.Pos.Y - radius)); y <= Math.Min(Terrain.Size - 1, (int)(thing.Pos.Y + radius)); y++)
                for (int x = Math.Max(0, (int)(thing.Pos.X - radius)); x <= Math.Min(Terrain.Size - 1, (int)(thing.Pos.X + radius)); x++)
                    if (thing.Pos.DistanceTo(new Vector2(x + .5f, y + .5f)) < radius + .5f) blocked[x, y] = true;
        }
        var points = Scenery.Points;
        for (int id = 0; id < points.Count; id++) {
            var (p, yaw, size) = points[id];
            if (Scenery.Loose(id) != null || !Scenery.Dressable(p) || blocked[(int)p.X, (int)p.Y]) continue;
            float density = Scenery.Density(p);
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
            if (id % 13 == 0 && density > .02f) {
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
        // All scenery darkens the baked ground shade; shrubs are also few and large enough to cast real shadows.
        Scatter(parent, NatureShape.Scrub, bushes, solidDressingMaterial, true);
        Scatter(parent, new SphereMesh { Radius = .055f, Height = .06f, RadialSegments = 5, Rings = 3 }, flowers, dressingMaterial);
        GD.Print($"PROTON-SCATTER: {grass.Count} meadow patches, {ferns.Count} ferns, {bushes.Count / 3} bushes, {flowers.Count} flowers (baked candidates: {points.Count})");
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
