using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>A shallow, rounded mound of pressed leaves kept together by a rim of folded leaves.</summary>
internal static class StrawBedModel {
    internal const float Top = .15f;
    private const float Rim = .115f;
    private static ArrayMesh? weave;
    private static readonly StandardMaterial3D Straw = Vertex(Surface.Flat, .94f);

    internal static Prop Build() {
        var prop = new Prop();
        Add(prop.Body, weave ??= Weave(), Straw, Vector3.Zero);
        for (int i = -1; i <= 1; i++)
            Add(prop.Body, LeafShape.For(i + 1), Straw, new Vector3(0, Top - .002f, i * .52f)).Scale =
                new Vector3(.63f, .38f, .79f);
        return prop;
    }

    private static ArrayMesh Weave() {
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        ReadOnlySpan<Vector2> outline = [
            new(-.30f, -.91f), new(.26f, -.94f), new(.39f, -.80f), new(.41f, .68f),
            new(.31f, .92f), new(-.28f, .91f), new(-.41f, .75f), new(-.40f, -.70f)
        ];
        var dirt = new Color("79502c");
        var woven = new Color("a68347");
        var leaves = new Color("885334");
        for (int i = 0; i < outline.Length; i++) {
            var a = outline[i]; var b = outline[(i + 1) % outline.Length];
            var baseA = On(a * .87f, .005f); var baseB = On(b * .87f, .005f);
            var bodyA = On(a, .067f); var bodyB = On(b, .067f);
            var lipA = On(a * .97f, Rim); var lipB = On(b * .97f, Rim);
            var topA = On(a * .90f, Top); var topB = On(b * .90f, Top);
            Band(baseA, baseB, bodyA, bodyB, dirt);
            Band(bodyA, bodyB, lipA, lipB, woven.Darkened(.18f));
            Band(lipA, lipB, topA, topB, woven);
            Tri(topA, topB, Vector3.Up * Top, leaves);
        }
        var rng = new Random(93711);
        Color[] straw = [new("c6a45c"), new("b08b4a"), new("d9b975"), new("a3834b"), new("c29a54")];
        for (int edge = 0; edge < outline.Length; edge++) {
            var a = outline[edge]; var b = outline[(edge + 1) % outline.Length];
            var along = (b - a).Normalized();
            var across = new Vector2(along.Y, -along.X);
            int blades = (int)MathF.Ceiling(a.DistanceTo(b) * 28);
            for (int i = 0; i < blades; i++) {
                var middle = a.Lerp(b, (i + Rand()) / blades) + across * (Rand() - .7f) * .087f;
                var axis = along.Rotated((Rand() - .5f) * 1.25f) * (.060f + Rand() * .104f);
                var wing = axis.Orthogonal().Normalized() * (.005f + Rand() * .012f);
                float y = Rim - .006f + Rand() * .037f;
                var endA = On(middle - axis, y - .009f); var endB = On(middle + axis, y - .01f);
                var left = On(middle - wing, y); var right = On(middle + wing, y);
                var raised = On(middle, y + .014f);
                var strawColor = straw[rng.Next(straw.Length)];
                Blade(endA, left, raised, strawColor); Blade(endB, raised, left, strawColor);
                var shade = strawColor.Darkened(.12f);
                Blade(endA, raised, right, shade); Blade(endB, right, raised, shade);
            }
        }
        return mesh.Commit();

        float Rand() => (float)rng.NextDouble();
        static Vector3 On(Vector2 p, float y) => new(p.X, y, p.Y);
        void Band(Vector3 a, Vector3 b, Vector3 upperA, Vector3 upperB, Color color) {
            Tri(a, upperB, upperA, color); Tri(a, b, upperB, color);
        }
        void Blade(Vector3 a, Vector3 b, Vector3 c, Color color) {
            if ((c - a).Cross(b - a).Y < 0) (b, c) = (c, b);
            Tri(a, b, c, color);
        }
        void Tri(Vector3 a, Vector3 b, Vector3 c, Color color) {
            mesh.SetNormal((c - a).Cross(b - a).Normalized());
            mesh.SetColor(color); mesh.AddVertex(a); mesh.AddVertex(b); mesh.AddVertex(c);
        }
    }
}
