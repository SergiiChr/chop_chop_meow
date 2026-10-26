using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Cached anatomical profiles for the wolf; all poses keep the original rig pivots.</summary>
internal static class WolfShape {
    private readonly record struct Section(float Z, float Y, float Width, float Height);
    private static ArrayMesh? body, head;
    public static readonly StandardMaterial3D Fur = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .95f };
    public static Mesh Body => body ??= Loft(new[] {
        new Section(-.51f, .64f, .025f, .055f), new(-.46f, .64f, .13f, .16f),
        new(-.33f, .64f, .185f, .195f), new(-.15f, .66f, .16f, .165f),
        new(.02f, .65f, .18f, .20f), new(.22f, .65f, .205f, .235f),
        new(.34f, .68f, .19f, .23f), new(.44f, .75f, .145f, .195f),
        new(.51f, .79f, .105f, .13f) }, false);
    public static Mesh Head => head ??= BuildHead();

    private static ArrayMesh Loft(Section[] sections, bool muzzle) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        AppendLoft(s, sections, muzzle);
        return s.Commit();
    }

    private static void AppendLoft(SurfaceTool s, Section[] sections, bool muzzle) {
        const int sides = 12;
        const int start = 0;
        // Vertex colors follow the same surface: dark back, grey flanks, pale jaw/chest.
        for (int row = 0; row < sections.Length; row++) {
            var r = sections[row]; var before = sections[Math.Max(0, row - 1)]; var after = sections[Math.Min(sections.Length - 1, row + 1)];
            for (int i = 0; i < sides; i++) {
                float a = i * Mathf.Tau / sides, x = MathF.Cos(a), y = MathF.Sin(a);
                var around = new Vector3(-r.Width * y, r.Height * x, 0);
                var along = new Vector3((after.Width - before.Width) * x, after.Y - before.Y + (after.Height - before.Height) * y, after.Z - before.Z);
                s.SetNormal(around.Cross(along).Normalized());
                Color dark = new("525750"), grey = new("7c8077"), pale = new("b6b6a8");
                var tint = y > .15f ? grey.Lerp(dark, (y - .15f) / .85f) : grey.Lerp(pale, Math.Clamp((-y + .15f) / 1.15f, 0, 1));
                if (muzzle && r.Z > .18f) tint = pale.Lerp(grey, Math.Max(0, y) * .45f);
                s.SetColor(tint); s.AddVertex(new Vector3(r.Width * x, r.Y + r.Height * y, r.Z));
            }
        }
        for (int row = 0; row < sections.Length - 1; row++)
            for (int i = 0; i < sides; i++) {
                int a = start + row * sides + i, b = start + (row + 1) * sides + i;
                int c = start + row * sides + (i + 1) % sides, d = start + (row + 1) * sides + (i + 1) % sides;
                s.AddIndex(a); s.AddIndex(b); s.AddIndex(c); s.AddIndex(c); s.AddIndex(b); s.AddIndex(d);
            }
        for (int end = 0; end < 2; end++) {
            int row = end == 0 ? 0 : sections.Length - 1;
            s.SetNormal(end == 0 ? Vector3.Forward : Vector3.Back); s.SetColor(new Color("7c8077"));
            int center = start + sections.Length * sides + end;
            s.AddVertex(new Vector3(0, sections[row].Y, sections[row].Z));
            for (int i = 0; i < sides; i++) {
                int a = start + row * sides + i, b = start + row * sides + (i + 1) % sides;
                s.AddIndex(center); s.AddIndex(end == 0 ? a : b); s.AddIndex(end == 0 ? b : a);
            }
        }
    }

    private static ArrayMesh BuildHead() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        AppendLoft(s, new[] { new Section(-.12f, -.015f, .08f, .095f), new(-.045f, .025f, .145f, .145f),
            new(.07f, .025f, .14f, .14f), new(.15f, -.005f, .10f, .105f),
            new(.22f, -.035f, .075f, .065f), new(.34f, -.04f, .05f, .042f), new(.365f, -.035f, .038f, .032f) }, true);
        // Broad triangular ears, with a small thickness and a recessed dark inner face.
        int index = 7 * 12 + 2;
        foreach (float side in new[] { -1f, 1f }) {
            var a = new Vector3(side * .045f, .115f, .025f); var b = new Vector3(side * .135f, .105f, .015f);
            var tip = new Vector3(side * .115f, .275f, -.025f); var back = new Vector3(side * .09f, .13f, -.055f);
            Triangle(s, ref index, a, b, tip, new Color("73796e"), Vector3.Back);
            Triangle(s, ref index, b, back, tip, new Color("525750"), new Vector3(side, 0, -1));
            Triangle(s, ref index, back, a, tip, new Color("646b60"), new Vector3(-side, 0, -1));
            Triangle(s, ref index, a, back, b, new Color("646b60"), Vector3.Down);
            Triangle(s, ref index, a.Lerp(tip, .2f) + Vector3.Back * .002f, b.Lerp(tip, .2f) + Vector3.Back * .002f,
                tip.Lerp((a + b) / 2, .22f) + Vector3.Back * .002f, new Color("444a43"), Vector3.Back);
        }
        return s.Commit();
    }

    private static void Triangle(SurfaceTool s, ref int index, Vector3 a, Vector3 b, Vector3 c, Color tint, Vector3 outward) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        // Match the existing indexed loft when appending the ears.
        s.SetColor(tint); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        s.AddIndex(index); s.AddIndex(index + 1); s.AddIndex(index + 2); index += 3;
    }

    /// <summary>How far down the leg its one bend sits: the front wrist, or the hind hock.</summary>
    public static float Joint(bool hind) => hind ? .25f : .3f;

    /// <summary>A leg hanging from its shoulder or hip, skinned to bend at <see cref="Joint"/>.</summary>
    public static Skeleton3D Leg(Node3D parent, bool hind) {
        Color fur = new("777d72"), pale = new("a8ad9d"), dark = new("5d655c");
        return ActorShape.Limb(parent, new[] {
            new ActorShape.Ring(.13f, hind ? .10f : .075f, .11f, fur),
            new ActorShape.Ring(.035f, hind ? .105f : .075f, .105f, fur),
            new ActorShape.Ring(-.10f, .065f, .075f, fur, hind ? .065f : -.015f),
            new ActorShape.Ring(-.22f, .042f, .047f, pale, hind ? -.035f : -.025f),
            new ActorShape.Ring(-.32f, .032f, .035f, pale, hind ? -.08f : 0),
            new ActorShape.Ring(-.405f, .034f, .04f, pale, hind ? -.035f : .015f),
            new ActorShape.Ring(-.44f, .055f, .085f, dark, .04f),
            new ActorShape.Ring(-.485f, .06f, .09f, dark, .045f),
            new ActorShape.Ring(-.49f, .048f, .072f, dark, .045f) }, Joint(hind));
    }

    /// <summary>The tail hanging down from its root, skinned so the outer half can curl.</summary>
    public static Skeleton3D Tail(Node3D parent) {
        Color grey = new("777d72"), dark = new("4e574e");
        return ActorShape.Limb(parent, new[] {
            new ActorShape.Ring(0, .045f, .045f, grey),
            new ActorShape.Ring(-.08f, .065f, .06f, grey),
            new ActorShape.Ring(-.22f, .085f, .075f, grey),
            new ActorShape.Ring(-.35f, .075f, .065f, grey, -.01f),
            new ActorShape.Ring(-.46f, .04f, .04f, dark, -.025f),
            new ActorShape.Ring(-.51f, .008f, .008f, dark, -.035f) }, .22f);
    }
}
