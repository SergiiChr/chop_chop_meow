using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

public partial class WorldView {
    /// <summary>ProtonScatter generates the candidates offline; gameplay geometry masks them once during loading. The loose stones, sticks and bushes among them are world things.</summary>
    private void BuildPlacedEnvironment(Node3D parent) {
        var grass = new List<(Transform3D At, Color Color)>();
        var flowers = new List<(Transform3D At, Color Color)>();
        var ferns = new List<(Transform3D At, Color Color)>();
        var blocked = new bool[Terrain.Size, Terrain.Size];
        // Decorative plants have no collision or inventory. Keep actual resources and initial buildings readable.
        foreach (var thing in World.Things) {
            // Grass grows right up to a bush, as it does to the stump a tree leaves.
            if (thing.Kind == ThingKind.Bush) continue;
            float radius = thing.Kind is ThingKind.Tree or ThingKind.Stump ? .45f : thing.IsResource ? .8f : 2.2f;
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
        Scatter(parent, new SphereMesh { Radius = .055f, Height = .06f, RadialSegments = 5, Rings = 3 }, flowers, dressingMaterial);
        GD.Print($"PROTON-SCATTER: {grass.Count} meadow patches, {ferns.Count} ferns, {flowers.Count} flowers (baked candidates: {points.Count})");
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
