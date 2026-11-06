using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Cached meshes for the wolf, built facing +Z with the root at the ground: a deep-chested forequarter with a rising neck, a hindquarter that flexes at the loin,
/// a head with hinged jaw and ears, three-segment legs and a brush tail. <see cref="WolfAnimation"/> keeps the matching joint positions.
/// </summary>
internal static class WolfShape {
    private readonly record struct Section(float Z, float Y, float Width, float Height);
    private static ArrayMesh? fore, hind, neck, head, jaw, earLeft, earRight;
    public static readonly StandardMaterial3D Fur = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .95f };
    private static readonly Color Dark = new("525750"), Grey = new("7c8077"), Pale = new("b6b6a8");

    /// <summary>The chest, shoulders and withers, from the loin forward to the base of the neck; it overlaps the hindquarter behind the loin pivot.</summary>
    public static Mesh Fore => fore ??= Loft(new[] {
        new Section(-.13f, .64f, .18f, .21f), new(-.02f, .62f, .195f, .235f), new(.12f, .60f, .20f, .245f),
        new(.25f, .62f, .19f, .24f), new(.34f, .65f, .17f, .21f), new(.40f, .68f, .14f, .165f), new(.43f, .69f, .07f, .09f) });
    /// <summary>The loin, hips and croup, from the tail root forward past the loin pivot.</summary>
    public static Mesh Hind => hind ??= Loft(new[] {
        new Section(-.50f, .66f, .03f, .05f), new(-.47f, .65f, .12f, .15f), new(-.40f, .66f, .16f, .185f),
        new(-.30f, .67f, .155f, .165f), new(-.18f, .65f, .165f, .19f), new(-.05f, .63f, .18f, .22f), new(-.01f, .63f, .14f, .17f) });
    /// <summary>The neck, rising from the withers; its root is the neck pivot and the head hangs from its end.</summary>
    public static Mesh Neck => neck ??= Loft(new[] {
        new Section(-.09f, -.02f, .135f, .16f), new(.01f, .02f, .125f, .145f), new(.10f, .07f, .11f, .125f),
        new(.18f, .11f, .095f, .105f), new(.235f, .135f, .08f, .09f), new(.25f, .14f, .045f, .05f) });
    public static Mesh Head => head ??= Loft(new[] {
        new Section(-.12f, -.015f, .08f, .095f), new(-.045f, .025f, .145f, .145f), new(.07f, .025f, .14f, .14f), new(.15f, -.005f, .10f, .105f),
        new(.22f, -.035f, .075f, .065f), new(.34f, -.04f, .05f, .042f), new(.365f, -.035f, .038f, .032f) }, true);
    /// <summary>The lower jaw, hinged at its root under the cheeks; it closes flush under the muzzle.</summary>
    public static Mesh Jaw => jaw ??= Loft(new[] {
        new Section(-.03f, -.02f, .06f, .028f), new(.05f, -.02f, .056f, .03f), new(.13f, -.015f, .046f, .026f), new(.2f, -.012f, .032f, .018f), new(.215f, -.01f, .014f, .008f) }, true);
    public static Mesh Ear(bool left) => left ? earLeft ??= BuildEar(1) : earRight ??= BuildEar(-1);

    private static ArrayMesh Loft(Section[] sections, bool muzzle = false) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        AppendLoft(s, sections, muzzle);
        return s.Commit();
    }

    private static void AppendLoft(SurfaceTool s, Section[] sections, bool muzzle) {
        const int sides = 12;
        // Vertex colors follow the same surface: dark back, grey flanks, pale jaw/chest/belly.
        for (int row = 0; row < sections.Length; row++) {
            var r = sections[row]; var before = sections[Math.Max(0, row - 1)]; var after = sections[Math.Min(sections.Length - 1, row + 1)];
            for (int i = 0; i < sides; i++) {
                float a = i * Mathf.Tau / sides, x = MathF.Cos(a), y = MathF.Sin(a);
                var around = new Vector3(-r.Width * y, r.Height * x, 0);
                var along = new Vector3((after.Width - before.Width) * x, after.Y - before.Y + (after.Height - before.Height) * y, after.Z - before.Z);
                s.SetNormal(around.Cross(along).Normalized());
                var tint = y > .15f ? Grey.Lerp(Dark, (y - .15f) / .85f) : Grey.Lerp(Pale, Math.Clamp((-y + .15f) / 1.15f, 0, 1));
                if (muzzle && r.Z > .18f) tint = Pale.Lerp(Grey, Math.Max(0, y) * .45f);
                s.SetColor(tint); s.AddVertex(new Vector3(r.Width * x, r.Y + r.Height * y, r.Z));
            }
        }
        for (int row = 0; row < sections.Length - 1; row++)
            for (int i = 0; i < sides; i++) {
                int a = row * sides + i, b = (row + 1) * sides + i;
                int c = row * sides + (i + 1) % sides, d = (row + 1) * sides + (i + 1) % sides;
                s.AddIndex(a); s.AddIndex(b); s.AddIndex(c); s.AddIndex(c); s.AddIndex(b); s.AddIndex(d);
            }
        for (int end = 0; end < 2; end++) {
            int row = end == 0 ? 0 : sections.Length - 1;
            s.SetNormal(end == 0 ? Vector3.Forward : Vector3.Back); s.SetColor(Grey);
            int center = sections.Length * sides + end;
            s.AddVertex(new Vector3(0, sections[row].Y, sections[row].Z));
            for (int i = 0; i < sides; i++) {
                int a = row * sides + i, b = row * sides + (i + 1) % sides;
                s.AddIndex(center); s.AddIndex(end == 0 ? a : b); s.AddIndex(end == 0 ? b : a);
            }
        }
    }

    /// <summary>A broad triangular ear standing from its pivot at the base, with a small thickness and a recessed dark inner face.</summary>
    private static ArrayMesh BuildEar(float side) {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        int index = 0;
        var a = new Vector3(side * -.045f, .005f, .005f); var b = new Vector3(side * .045f, -.005f, -.005f);
        var tip = new Vector3(side * .025f, .165f, -.05f); var back = new Vector3(0, .02f, -.075f);
        Triangle(s, ref index, a, b, tip, new Color("73796e"), Vector3.Back);
        Triangle(s, ref index, b, back, tip, Dark, new Vector3(side, 0, -1));
        Triangle(s, ref index, back, a, tip, new Color("646b60"), new Vector3(-side, 0, -1));
        Triangle(s, ref index, a, back, b, new Color("646b60"), Vector3.Down);
        Triangle(s, ref index, a.Lerp(tip, .2f) + Vector3.Back * .002f, b.Lerp(tip, .2f) + Vector3.Back * .002f,
            tip.Lerp((a + b) / 2, .22f) + Vector3.Back * .002f, new Color("444a43"), Vector3.Back);
        return s.Commit();
    }

    private static void Triangle(SurfaceTool s, ref int index, Vector3 a, Vector3 b, Vector3 c, Color tint, Vector3 outward) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        s.SetColor(tint); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        s.AddIndex(index); s.AddIndex(index + 1); s.AddIndex(index + 2); index += 3;
    }

    /// <summary>
    /// A leg hanging from its shoulder or hip, skinned to bend at the two joints <see cref="WolfAnimation"/> gives it: the elbow and wrist, or the stifle and hock.
    /// The profile zigzags the way a canine leg stands: the thigh slopes forward to the stifle, the shin back to the hock and the paw points forward.
    /// </summary>
    public static Skeleton3D Leg(Node3D parent, bool hind) {
        Color fur = new("777d72"), pale = new("a8ad9d"), dark = new("5d655c");
        var rings = hind ? new[] {
            new ActorShape.Ring(.12f, .10f, .12f, fur, -.01f), new ActorShape.Ring(.02f, .105f, .125f, fur, .025f),
            new ActorShape.Ring(-.09f, .085f, .10f, fur, .065f), new ActorShape.Ring(-.18f, .055f, .065f, fur, .09f),
            new ActorShape.Ring(-.28f, .042f, .048f, pale, .02f), new ActorShape.Ring(-.38f, .034f, .038f, pale, -.07f),
            new ActorShape.Ring(-.49f, .032f, .034f, pale, -.03f), new ActorShape.Ring(-.54f, .048f, .07f, dark, .03f),
            new ActorShape.Ring(-.585f, .052f, .085f, dark, .045f), new ActorShape.Ring(-.60f, .042f, .068f, dark, .045f) }
        : new[] {
            new ActorShape.Ring(.12f, .085f, .11f, fur), new ActorShape.Ring(.02f, .08f, .10f, fur, -.01f),
            new ActorShape.Ring(-.12f, .058f, .072f, fur, -.03f), new ActorShape.Ring(-.23f, .046f, .055f, fur, -.04f),
            new ActorShape.Ring(-.35f, .038f, .043f, pale, -.025f), new ActorShape.Ring(-.47f, .033f, .036f, pale, -.01f),
            new ActorShape.Ring(-.52f, .048f, .065f, dark, .03f), new ActorShape.Ring(-.565f, .052f, .08f, dark, .05f),
            new ActorShape.Ring(-.58f, .042f, .065f, dark, .05f) };
        return ActorShape.Limb(parent, rings, WolfAnimation.Joints(hind));
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
