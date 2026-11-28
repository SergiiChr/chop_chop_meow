using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Trail and shore dressing placed only for the film's low cameras.</summary>
internal sealed class IntroLandscape {
    public readonly Node3D Forest, Shore;
    public readonly Node3D BerryBush, BerryCluster;
    private readonly Vector3 berryStem;
    public readonly Vector3 BerryStemAtRest;

    public IntroLandscape(Node3D stage, Func<float, Vector2> trail, float waterline, float berryAlong) {
        Forest = Pivot(stage, Vector3.Zero);
        Shore = Pivot(stage, Vector3.Zero);
        var rng = new Random(23);
        float Chance() => (float)rng.NextDouble();

        var ribbon = new SurfaceTool();
        ribbon.Begin(Mesh.PrimitiveType.Triangles);
        float Width(float along) => .68f + .13f * MathF.Sin(along * 3.2f) + .12f * MathF.Sin(along * 6.9f);
        Vector3 Edge(float along, float across) {
            var center = trail(along);
            var forward = (trail(along + .1f) - center).Normalized();
            return WorldView.StandsAt(center + new Vector2(-forward.Y, forward.X) * (across * Width(along)), .012f);
        }
        void Triangle(Vector3 a, Vector3 b, Vector3 c) {
            if ((b - a).Cross(c - a).Y > 0) (b, c) = (c, b);
            foreach (var v in new[] { a, b, c }) {
                ribbon.SetNormal(Vector3.Up);
                ribbon.SetUV(new Vector2(v.X, v.Z) * .73f);
                ribbon.AddVertex(v);
            }
        }
        for (int panel = 0; panel < 100; panel++) {
            float along = waterline - 33 + panel * .29f;
            var a = Edge(along, -1);
            var b = Edge(along, 1);
            var c = Edge(along + .29f, -1);
            var d = Edge(along + .29f, 1);
            Triangle(a, b, c);
            Triangle(b, d, c);
        }
        Add(Forest, ribbon.Commit(), Mat("77634b", .96f, surface: Surface.Earth), Vector3.Zero);

        var leaf = Mat("496329", .75f);
        var youngLeaf = Mat("789050", .79f);
        var stone = Stone("7a7864");
        for (int i = 0; i < 33; i++) {
            float along = waterline - 30 + i * .62f + Chance() * .49f;
            var center = trail(along);
            var forward = (trail(along + .1f) - center).Normalized();
            var side = new Vector2(-forward.Y, forward.X) * (i % 2 == 0 ? 1 : -1);
            var at = center + side * (.81f + Chance() * 1.45f);
            if (i % 5 == 0) {
                Sphere(Forest, .4f, WorldView.StandsAt(at, .08f), stone, new Vector3(1 + Chance(), .56f, 1.23f), 9);
                continue;
            }
            var fern = Pivot(Forest, WorldView.StandsAt(at), Vector3.Up * (Chance() * 360));
            fern.Scale = Vector3.One * (.62f + .69f * Chance());
            for (int blade = 0; blade < 5; blade++) {
                var frond = Pivot(fern, Vector3.Zero, Vector3.Up * (blade * 72 + Chance() * 18));
                for (int row = 0; row < 4; row++) {
                    float z = .09f + row * .075f;
                    float y = .13f + row * .034f;
                    foreach (int wing in new[] { -1, 1 }) {
                        var tip = Sphere(frond, .055f, new Vector3(wing * (.054f - row * .009f), y, z),
                            row < 3 ? leaf : youngLeaf, new Vector3(1.25f - row * .15f, .11f, .35f), 6);
                        tip.RotationDegrees = new Vector3(-14, wing * -27, wing * 15);
                    }
                }
            }
        }
        // Small stones sit in the worn track. Leaf litter catches the side light at its edges.
        var litter = new[] { Mat("9b7244"), Mat("b79851"), Mat("98784a"), Mat("6b6440") };
        for (int patch = 0; patch < 54; patch++) {
            float along = waterline - 30 + Chance() * 24;
            var center = trail(along);
            var forward = (trail(along + .1f) - center).Normalized();
            var side = forward.Orthogonal();
            var margin = center + side * ((patch % 2 == 0 ? -1 : 1) * (.35f + Chance() * .46f));
            for (int i = 0; i < 4; i++) {
                var at = margin + side * ((Chance() - .5f) * .49f) + forward * ((Chance() - .5f) * .65f);
                var leafAt = WorldView.StandsAt(at, .026f);
                var fallen = Sphere(Forest, .052f, leafAt, litter[i], new Vector3(.68f, .076f, 1.36f), 6);
                fallen.RotationDegrees = new Vector3(Chance() * 9, Chance() * 360, Chance() * 12);
            }
            var gravel = center.Lerp(margin, Chance());
            Sphere(Forest, .038f, WorldView.StandsAt(gravel, .022f), stone, new Vector3(1.4f, .48f, .88f), 7);
        }
        // A few canopies along the route close gaps between the valley's own trees and cast across the trail.
        for (int i = 0; i < 5; i++) {
            float along = waterline - 27.6f + i * 3.85f;
            var center = trail(along);
            var tangent = (trail(along + .2f) - center).Normalized();
            var at = center + tangent.Orthogonal() * (i % 2 == 0 ? -6.3f : 2.85f);
            var tree = Pivot(Forest, WorldView.StandsAt(at), Vector3.Up * (Chance() * 360));
            tree.Scale = Vector3.One * (.75f + Chance() * .15f);
            Tree3DShape.Add(tree, 0, i % 3);
        }
        var timber = Wood("594b38", .96f);
        for (int i = 0; i < 3; i++) {
            var center = trail(waterline - 28 + i * 5.5f);
            var lateral = (trail(waterline - 27 + i * 5.5f) - center).Normalized().Orthogonal();
            var at = center + lateral * (1.25f + i * .59f);
            Cylinder(Forest, .135f, .092f, .8f + Chance(), WorldView.StandsAt(at, .095f), timber,
                new Vector3(90, Chance() * 360, 0), sides: 8);
        }

        // A real stem on the trail side carries the separately posed cluster.
        var pickingMark = trail(berryAlong);
        var pathDirection = (trail(berryAlong + .2f) - pickingMark).Normalized();
        var pathSide = new Vector2(-pathDirection.Y, pathDirection.X);
        var bushAt = pickingMark - pathSide * 1.22f + pathDirection * .24f;
        BerryBush = Pivot(Forest, WorldView.StandsAt(bushAt));
        const int variant = 0;
        var leaves = Tree3DShape.Add(BerryBush, Tree3DShape.Bush, variant);
        var cluster = NatureShape.CardBerries(leaves, variant, 1);
        berryStem = cluster.GetAabb().GetCenter();
        Add(BerryBush, NatureShape.CardBerries(leaves, variant, NatureShape.BerryClusters, first: 1), NatureShape.Glossy, Vector3.Zero);
        var inward = (pickingMark - bushAt).Normalized();
        BerryBush.RotationDegrees = Vector3.Up * Mathf.RadToDeg(ActorAnimation.Yaw(inward, 0) -
            ActorAnimation.Yaw(new Vector2(berryStem.X, berryStem.Z), 0));
        BerryBush.Scale = new Vector3(.87f, .89f / berryStem.Y, .87f);
        BerryStemAtRest = BerryBush.ToGlobal(berryStem);
        BerryCluster = Pivot(Forest, Vector3.Zero);
        Add(BerryCluster, cluster, NatureShape.Glossy, -berryStem * .87f, scale: Vector3.One * .87f);
        PlaceBerryOnStem();

        var pebble = Stone("8b8271", .61f);
        var wet = Stone("626867", .42f);
        var shoreEdge = trail(waterline);
        var down = (trail(waterline + 1) - shoreEdge).Normalized();
        var across = down.Orthogonal();
        for (int i = 0; i < 44; i++) {
            float side = (i % 2 == 0 ? 1 : -1) * (.74f + Chance() * 7.8f);
            var at = shoreEdge + across * side + down * (Chance() * 3.9f - 1.7f);
            float radius = .034f + Chance() * (i % 7 == 0 ? .31f : .12f);
            var nod = Sphere(Shore, radius, WorldView.StandsAt(at, radius * .21f), i % 3 == 0 ? wet : pebble,
                new Vector3(1.13f + Chance() * .38f, .59f + Chance() * .25f, .7f + Chance() * .47f), 9);
            nod.RotationDegrees = new Vector3(0, Chance() * 360, 0);
        }
    }

    /// <summary>Places the cluster back on its own twig as the bush moves.</summary>
    public void PlaceBerryOnStem() {
        BerryCluster.GlobalTransform = new Transform3D(BerryBush.GlobalBasis.Orthonormalized(), BerryBush.ToGlobal(berryStem));
    }
}
