using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The wolf's skeleton and its skinned fur, shared by every wolf and carcass.
/// Every bone rests unrotated, so a pose is only rotations: the spine and tail run along the body and each leg rests as a straight column under its shoulder or hip.
/// </summary>
internal static class WolfShape {
    public const int Loin = 0, Chest = 1, Pelvis = 2, Neck = 3, Head = 4, Jaw = 5, EarL = 6, EarR = 7, Tail = 8;
    /// <summary>The tail has three bones, from the root out.</summary>
    public const int TailBones = 3;
    private const int LegBones = Tail + TailBones;

    /// <summary>A leg's first bone; it is followed by the middle, lower and paw bones. Legs run front left, front right, hind left, hind right.</summary>
    public static int Leg(int leg) => LegBones + leg * 4;

    /// <summary>Upper, middle and lower segment lengths: shoulder to elbow, forearm and pastern in front; thigh, shin and hock to fetlock behind.</summary>
    public static readonly Vector3 FrontLengths = new(.3f, .27f, .1f), HindLengths = new(.25f, .28f, .16f);
    public static Vector3 Lengths(int leg) => leg < 2 ? FrontLengths : HindLengths;

    /// <summary>Where each leg hangs from the chest or pelvis.</summary>
    public static Vector3 Pivot(int leg) => leg switch { 0 => new(.085f, .64f, .31f), 1 => new(-.085f, .64f, .31f), 2 => new(.085f, .62f, -.36f), _ => new(-.085f, .62f, -.36f) };

    /// <summary>How far the sole sits below the paw bone when the paw is flat on the ground.</summary>
    public const float Sole = .035f;

    /// <summary>Each bone's parent and resting position in the model, built facing +Z with the ground at zero.</summary>
    public static readonly (int Parent, Vector3 At)[] Bones = BuildBones();

    private static (int, Vector3)[] BuildBones() {
        var bones = new (int, Vector3)[LegBones + 16];
        bones[Loin] = (-1, new Vector3(0, .66f, -.04f));
        bones[Chest] = (Loin, new Vector3(0, .69f, .22f));
        bones[Pelvis] = (Loin, new Vector3(0, .66f, -.3f));
        bones[Neck] = (Chest, new Vector3(0, .74f, .4f));
        bones[Head] = (Neck, new Vector3(0, .86f, .56f));
        bones[Jaw] = (Head, new Vector3(0, .818f, .6f));
        bones[EarL] = (Head, new Vector3(.052f, .925f, .55f));
        bones[EarR] = (Head, new Vector3(-.052f, .925f, .55f));
        for (int i = 0; i < TailBones; i++) bones[Tail + i] = (i == 0 ? Pelvis : Tail + i - 1, new Vector3(0, .7f, -.48f - .17f * i));
        for (int leg = 0; leg < 4; leg++) {
            var at = Pivot(leg); var lengths = Lengths(leg);
            bones[Leg(leg)] = (leg < 2 ? Chest : Pelvis, at);
            bones[Leg(leg) + 1] = (Leg(leg), at -= new Vector3(0, lengths.X, 0));
            bones[Leg(leg) + 2] = (Leg(leg) + 1, at -= new Vector3(0, lengths.Y, 0));
            bones[Leg(leg) + 3] = (Leg(leg) + 2, at - new Vector3(0, lengths.Z, 0));
        }
        return bones;
    }

    private static readonly Color Dark = new("4a4e46"), Grey = new("7b7d71"), Tan = new("8b7f64"), Pale = new("cbc5b0"),
        Black = new("1e1f1c"), Mouth = new("6b2e2b"), InnerEar = new("b7a596");
    public static readonly StandardMaterial3D Fur = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .95f };
    private static readonly StandardMaterial3D EyeShine = new() { AlbedoColor = new Color("d9a046"), Roughness = .35f, EmissionEnabled = true, Emission = new Color("d9a046"), EmissionEnergyMultiplier = .5f };
    private static ArrayMesh? fur, eyes;
    private static Skin? skin;

    /// <summary>A new skeleton in its resting pose under <paramref name="parent"/>, wearing the shared fur and eyes.</summary>
    public static Skeleton3D Build(Node3D parent) {
        var skeleton = new Skeleton3D();
        parent.AddChild(skeleton);
        for (int i = 0; i < Bones.Length; i++) {
            skeleton.AddBone($"b{i}");
            var (up, at) = Bones[i];
            if (up >= 0) skeleton.SetBoneParent(i, up);
            skeleton.SetBoneRest(i, new Transform3D(Basis.Identity, up >= 0 ? at - Bones[up].At : at));
        }
        skeleton.ResetBonePoses();
        skin ??= BuildSkin();
        fur ??= BuildFur();
        eyes ??= BuildEyes();
        skeleton.AddChild(new MeshInstance3D { Mesh = fur, MaterialOverride = Fur, Skin = skin, Skeleton = new NodePath("..") });
        skeleton.AddChild(new MeshInstance3D { Mesh = eyes, MaterialOverride = EyeShine, Skin = skin, Skeleton = new NodePath(".."), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return skeleton;
    }

    private static Skin BuildSkin() {
        var built = new Skin();
        for (int i = 0; i < Bones.Length; i++) built.AddBind(i, new Transform3D(Basis.Identity, -Bones[i].At));
        return built;
    }

    /// <summary>One cross-section of a tube: an ellipse across <paramref name="U"/> and <paramref name="V"/>, skinned to a bone or blended toward the next.</summary>
    /// <param name="Keel">Narrows the lower half, for a chest that is deeper than it is wide.</param>
    /// <param name="Tilt">Tips the section back by this many degrees, for a tube that turns upward, such as the neck rising out of the chest.</param>
    private readonly record struct Ring(Vector3 Center, float Width, float Height, int Bone, int Next = -1, float Blend = 0, float Keel = 0, float Tilt = 0);

    /// <summary>How a tube is coloured at a ring index, from the side (-1 to 1 across) and the height around its section (-1 underneath to 1 on top).</summary>
    private delegate Color Shade(int ring, float across, float up);

    /// <summary>
    /// Appends a closed tube through <paramref name="rings"/>.
    /// Each ring's section lies across <paramref name="u"/> and <paramref name="v"/>; normals come from the neighbouring vertices so the surface shades smoothly.
    /// </summary>
    private static void Tube(SurfaceTool s, ref int start, Ring[] rings, Vector3 u, Vector3 v, int sides, Shade shade) {
        var points = new Vector3[rings.Length, sides];
        for (int r = 0; r < rings.Length; r++)
            for (int i = 0; i < sides; i++) {
                float a = i * Mathf.Tau / sides, x = MathF.Cos(a), y = MathF.Sin(a);
                var ring = rings[r];
                float width = ring.Width * (y < 0 ? 1 - ring.Keel * y * y : 1);
                points[r, i] = ring.Center + u * (width * x) + v.Rotated(u, -Mathf.DegToRad(ring.Tilt)) * (ring.Height * y);
            }
        for (int r = 0; r < rings.Length; r++)
            for (int i = 0; i < sides; i++) {
                var around = points[r, (i + 1) % sides] - points[r, (i + sides - 1) % sides];
                var along = points[Math.Min(r + 1, rings.Length - 1), i] - points[Math.Max(r - 1, 0), i];
                var normal = around.Cross(along).Normalized();
                if (normal.Dot(points[r, i] - rings[r].Center) < 0) normal = -normal;
                float a = i * Mathf.Tau / sides;
                Vertex(s, points[r, i], normal, shade(r, MathF.Cos(a), MathF.Sin(a)), rings[r]);
            }
        for (int r = 0; r < rings.Length - 1; r++)
            for (int i = 0; i < sides; i++) {
                int a = start + r * sides + i, b = start + (r + 1) * sides + i, c = start + r * sides + (i + 1) % sides, d = start + (r + 1) * sides + (i + 1) % sides;
                Triangle(s, a, b, c, points[r, i], points[r + 1, i], points[r, (i + 1) % sides], rings[r].Center);
                Triangle(s, c, b, d, points[r, (i + 1) % sides], points[r + 1, i], points[r + 1, (i + 1) % sides], rings[r].Center);
            }
        // Both ends close on a centre point.
        int next = start + rings.Length * sides;
        for (int end = 0; end < 2; end++) {
            int r = end == 0 ? 0 : rings.Length - 1;
            var outward = (rings[r].Center - rings[end == 0 ? 1 : rings.Length - 2].Center).Normalized();
            Vertex(s, rings[r].Center + outward * .004f, outward, shade(r, 0, 0), rings[r]);
            for (int i = 0; i < sides; i++) {
                int a = start + r * sides + i, b = start + r * sides + (i + 1) % sides;
                Triangle(s, next, a, b, rings[r].Center + outward, points[r, i], points[r, (i + 1) % sides], rings[r].Center);
            }
            next++;
        }
        start = next;
    }

    private static void Vertex(SurfaceTool s, Vector3 at, Vector3 normal, Color color, Ring ring) {
        s.SetNormal(normal); s.SetColor(color);
        s.SetBones(new[] { ring.Bone, ring.Next < 0 ? ring.Bone : ring.Next, 0, 0 });
        s.SetWeights(new[] { 1 - ring.Blend, ring.Blend, 0, 0 });
        s.AddVertex(at);
    }

    /// <summary>Adds a triangle wound to face away from <paramref name="inside"/>.</summary>
    private static void Triangle(SurfaceTool s, int a, int b, int c, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 inside) {
        bool outward = (pb - pa).Cross(pc - pa).Dot(pa - inside) < 0;
        s.AddIndex(a); s.AddIndex(outward ? b : c); s.AddIndex(outward ? c : b);
    }

    /// <summary>The coat around the body: a dark saddle, grey flanks warming to tan low down, and a pale belly and throat.</summary>
    private static Color Coat(float up, float tan) {
        var color = up > 0 ? Grey.Lerp(Dark, Mathf.SmoothStep(.2f, .95f, up)) : Grey.Lerp(Tan, tan * Mathf.SmoothStep(0, -.4f, up));
        return color.Lerp(Pale, Mathf.SmoothStep(-.45f, -.95f, up));
    }

    private static ArrayMesh BuildFur() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        int start = 0;
        // Back line, belly line and half width along the body: deep narrow chest, tucked waist, then the haunches.
        (float Z, float Top, float Bottom, float Half, float Keel)[] body = {
            (-.57f, .745f, .68f, .03f, 0), (-.53f, .78f, .58f, .1f, .1f), (-.45f, .795f, .52f, .14f, .15f), (-.34f, .79f, .51f, .145f, .2f),
            (-.22f, .785f, .58f, .12f, .2f), (-.08f, .79f, .55f, .13f, .3f), (.07f, .81f, .46f, .155f, .4f), (.19f, .83f, .42f, .165f, .45f),
            (.3f, .83f, .425f, .16f, .45f) };
        // The thick maned neck carries on from the chest as one surface, its sections tipping back as it rises to the head.
        var neck = new[] {
            new Ring(new Vector3(0, .655f, .385f), .145f, .18f, Chest, -1, 0, .4f, 18), new Ring(new Vector3(0, .72f, .445f), .13f, .145f, Chest, Neck, .5f, .3f, 38),
            new Ring(new Vector3(0, .77f, .495f), .115f, .12f, Neck, -1, 0, .25f, 50), new Ring(new Vector3(0, .822f, .535f), .095f, .1f, Neck, Head, .3f, .2f, 55),
            new Ring(new Vector3(0, .865f, .565f), .08f, .085f, Neck, Head, .7f, 0, 58) };
        var rings = new Ring[body.Length + neck.Length];
        for (int i = 0; i < body.Length; i++) {
            var (z, top, bottom, half, keel) = body[i];
            var (bone, next, blend) = z < -.3f ? (Pelvis, -1, 0f) : z < -.04f ? (Pelvis, Loin, (z + .3f) / .26f) : z < .22f ? (Loin, Chest, (z + .04f) / .26f) : (Chest, -1, 0f);
            rings[i] = new Ring(new Vector3(0, (top + bottom) / 2, z), half, (top - bottom) / 2, bone, next, Mathf.SmoothStep(0, 1, blend), keel);
        }
        neck.CopyTo(rings, body.Length);
        Tube(s, ref start, rings, Vector3.Right, Vector3.Up, 16, (r, _, up) => Coat(up, r >= body.Length || rings[r].Center.Z is > .05f or < -.3f ? 1 : .5f));
        // Skull and muzzle, with the pale cheeks and darker bridge of a grey wolf's face.
        (float Z, float Y, float W, float H)[] skull = {
            (.47f, .865f, .08f, .08f), (.52f, .88f, .115f, .1f), (.585f, .88f, .108f, .09f), (.645f, .862f, .078f, .066f),
            (.7f, .848f, .056f, .047f), (.755f, .842f, .043f, .037f), (.795f, .842f, .031f, .028f), (.81f, .842f, .014f, .013f) };
        var head = new Ring[skull.Length];
        for (int i = 0; i < skull.Length; i++) head[i] = new Ring(new Vector3(0, skull[i].Y, skull[i].Z), skull[i].W, skull[i].H, Head);
        Tube(s, ref start, head, Vector3.Right, Vector3.Up, 14, (r, across, up) =>
            r >= 6 ? Black : r >= 4 ? (up > .45f ? Tan.Lerp(Grey, .4f) : Pale) : r == 0 ? Coat(up, 1) : up > .5f ? Grey.Lerp(Dark, (up - .5f) * 1.6f) : up < .3f ? Pale : Grey.Lerp(Pale, .5f));
        // The lower jaw hangs from its own bone and shows a dark mouth on top when it opens.
        var jaw = new[] {
            new Ring(new Vector3(0, .815f, .6f), .055f, .024f, Jaw), new Ring(new Vector3(0, .806f, .68f), .04f, .02f, Jaw),
            new Ring(new Vector3(0, .808f, .76f), .029f, .014f, Jaw), new Ring(new Vector3(0, .81f, .785f), .013f, .008f, Jaw) };
        Tube(s, ref start, jaw, Vector3.Right, Vector3.Up, 10, (_, _, up) => up > .5f ? Mouth : Pale);
        Teeth(s, ref start);
        foreach (int side in new[] { 1, -1 }) {
            int ear = side > 0 ? EarL : EarR;
            var at = Bones[ear].At;
            // Upright triangular ears, cupped forward: pale inside, grey outside.
            var ears = new[] {
                new Ring(at + new Vector3(side * .004f, -.015f, 0), .048f, .04f, ear), new Ring(at + new Vector3(side * .012f, .055f, -.005f), .034f, .026f, ear),
                new Ring(at + new Vector3(side * .02f, .105f, -.01f), .014f, .008f, ear), new Ring(at + new Vector3(side * .022f, .125f, -.012f), .002f, .002f, ear) };
            Tube(s, ref start, ears, new Vector3(1, 0, side * .35f).Normalized(), new Vector3(-side * .35f, 0, 1).Normalized(), 8, (_, _, up) => up > .2f ? InnerEar : Dark.Lerp(Grey, .4f));
        }
        // A black nose on the tip of the muzzle.
        Tube(s, ref start, new[] { new Ring(new Vector3(0, .85f, .79f), .012f, .01f, Head), new Ring(new Vector3(0, .852f, .805f), .021f, .017f, Head), new Ring(new Vector3(0, .848f, .822f), .006f, .006f, Head) },
            Vector3.Right, Vector3.Up, 8, (_, _, _) => Black);
        for (int leg = 0; leg < 4; leg++) LegTube(s, ref start, leg);
        // A full brush tail, dark on top and at the tip.
        (float Z, float Y, float W, float H)[] brush = { (-.46f, .71f, .04f, .045f), (-.52f, .7f, .052f, .058f), (-.6f, .695f, .066f, .072f), (-.7f, .69f, .074f, .08f), (-.8f, .69f, .072f, .075f), (-.9f, .69f, .055f, .058f), (-.97f, .69f, .03f, .032f), (-1, .69f, .006f, .006f) };
        var tail = new Ring[brush.Length];
        for (int i = 0; i < brush.Length; i++) {
            float along = (-.48f - brush[i].Z) / .17f;
            int bone = Math.Clamp((int)along, 0, TailBones - 1);
            tail[i] = new Ring(new Vector3(0, brush[i].Y, brush[i].Z), brush[i].W, brush[i].H, Tail + bone, bone < TailBones - 1 ? Tail + bone + 1 : -1, bone < TailBones - 1 ? Mathf.SmoothStep(.5f, 1, along - bone) : 0);
        }
        Tube(s, ref start, tail, Vector3.Right, Vector3.Up, 12, (r, _, up) => r >= 5 ? Dark.Darkened(.2f) : up > .1f ? Dark.Lerp(Grey, .3f) : Tan.Lerp(Pale, .5f));
        return s.Commit();
    }

    /// <summary>Two small white fangs on each side of the upper jaw, out of sight until the mouth opens.</summary>
    private static void Teeth(SurfaceTool s, ref int start) {
        foreach (var at in new[] { new Vector3(.024f, .812f, .762f), new Vector3(-.024f, .812f, .762f) })
            Tube(s, ref start, new[] { new Ring(at + new Vector3(0, .012f, 0), .006f, .006f, Head), new Ring(at - new Vector3(0, .014f, 0), .001f, .001f, Head) }, Vector3.Right, Vector3.Back, 5, (_, _, _) => new Color("eee8d8"));
    }

    /// <summary>
    /// A leg as one skinned tube down its resting column, then the paw forward along the ground.
    /// Hind legs carry the heavy thigh of the haunch; forelegs are slimmer, with the elbow just below the chest.
    /// </summary>
    private static void LegTube(SurfaceTool s, ref int start, int leg) {
        bool hind = leg >= 2;
        int bone = Leg(leg);
        var pivot = Pivot(leg); var lengths = Lengths(leg);
        float elbow = pivot.Y - lengths.X, wrist = elbow - lengths.Y, fetlock = wrist - lengths.Z;
        // Height, half width, half depth and how far the section sits forward of the column.
        (float Y, float W, float D, float Forward)[] column = hind
            ? new[] { (pivot.Y + .06f, .055f, .09f, 0f), (pivot.Y - .04f, .06f, .105f, .015f), (pivot.Y - .14f, .062f, .1f, .015f), (elbow + .03f, .05f, .074f, .005f),
                (elbow - .05f, .046f, .064f, -.01f), (elbow - .15f, .034f, .045f, -.014f), (wrist + .03f, .025f, .032f, -.012f), (wrist, .027f, .036f, -.018f),
                (wrist - .07f, .022f, .026f, 0f), (fetlock + .015f, .024f, .028f, 0f) }
            : new[] { (pivot.Y + .04f, .05f, .08f, 0f), (pivot.Y - .1f, .055f, .085f, .005f), (elbow + .09f, .05f, .07f, 0f), (elbow + .01f, .045f, .058f, -.012f),
                (elbow - .06f, .037f, .046f, 0f), (elbow - .17f, .028f, .033f, .004f), (wrist + .02f, .026f, .03f, .004f), (wrist - .01f, .027f, .032f, .006f),
                (wrist - .06f, .024f, .028f, .004f), (fetlock + .015f, .025f, .029f, .006f) };
        float[] joints = { elbow, wrist, fetlock };
        var rings = new Ring[column.Length];
        for (int i = 0; i < column.Length; i++) {
            var (y, w, d, forward) = column[i];
            var (b, next, blend) = Weight(y, joints, bone);
            rings[i] = new Ring(new Vector3(pivot.X, y, pivot.Z + forward), w, d, b, next, blend);
        }
        float top = pivot.Y;
        float outside = Math.Sign(pivot.X);
        Tube(s, ref start, rings, Vector3.Right, Vector3.Back, 10, (r, across, front) => {
            float y = column[r].Y;
            var color = y > top - .12f ? Grey : y > elbow - .05f ? Grey.Lerp(Tan, .6f) : Tan.Lerp(Pale, .15f);
            return color.Lerp(Pale, Math.Clamp(-front * .5f - across * outside * .4f, 0, .55f));
        });
        // The paw: a rounded pad from the heel forward to the toes, dark underneath.
        float sole = fetlock - Sole;
        var paw = new[] {
            new Ring(new Vector3(pivot.X, fetlock + .005f, pivot.Z - .03f), .022f, .02f, bone + 3), new Ring(new Vector3(pivot.X, sole + .03f, pivot.Z - .005f), .033f, .03f, bone + 3),
            new Ring(new Vector3(pivot.X, sole + .025f, pivot.Z + .03f), .036f, .025f, bone + 3), new Ring(new Vector3(pivot.X, sole + .016f, pivot.Z + .058f), .03f, .016f, bone + 3),
            new Ring(new Vector3(pivot.X, sole + .012f, pivot.Z + .07f), .012f, .008f, bone + 3) };
        Tube(s, ref start, paw, Vector3.Right, Vector3.Up, 10, (_, _, up) => up < -.6f ? Dark.Darkened(.3f) : Tan.Lerp(Pale, .3f));
    }

    /// <summary>The bone a leg ring at height <paramref name="y"/> follows, handing over to the next across four centimetres either side of each joint.</summary>
    private static (int Bone, int Next, float Blend) Weight(float y, float[] joints, int first) {
        for (int j = 0; j < joints.Length; j++) {
            if (y > joints[j] + .04f) return (first + j, -1, 0);
            if (y >= joints[j] - .04f) return (first + j, first + j + 1, Mathf.SmoothStep(0, 1, (joints[j] + .04f - y) / .08f));
        }
        return (first + joints.Length, -1, 0);
    }

    private static ArrayMesh BuildEyes() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        int start = 0;
        foreach (int side in new[] { 1, -1 }) {
            var at = new Vector3(side * .08f, .886f, .628f);
            var across = new Vector3(side, 0, .55f).Normalized();
            Tube(s, ref start, new[] { new Ring(at - across * .006f, .006f, .006f, Head), new Ring(at, .014f, .01f, Head), new Ring(at + across * .006f, .006f, .005f, Head) },
                Vector3.Up.Cross(across).Normalized(), Vector3.Up, 8, (_, _, _) => Colors.White);
        }
        return s.Commit();
    }
}
