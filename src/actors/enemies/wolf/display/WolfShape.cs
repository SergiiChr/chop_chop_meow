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
    /// <summary>The upper lips, which lift off the teeth in a snarl, and the brow over each eye, which lowers at its inner end.</summary>
    public const int Lip = LegBones + 16, BrowL = Lip + 1, BrowR = Lip + 2;

    /// <summary>A leg's first bone; it is followed by the middle, lower and paw bones. Legs run front left, front right, hind left, hind right.</summary>
    public static int Leg(int leg) => LegBones + leg * 4;

    /// <summary>Upper, middle and lower segment lengths: shoulder to elbow, forearm and pastern in front; thigh, shin and hock to fetlock behind.</summary>
    public static readonly Vector3 FrontLengths = new(.3f, .27f, .1f), HindLengths = new(.25f, .28f, .16f);
    public static Vector3 Lengths(int leg) => leg < 2 ? FrontLengths : HindLengths;

    /// <summary>Where each leg hangs from the chest or pelvis.</summary>
    public static Vector3 Pivot(int leg) => leg switch { 0 => new(.085f, .64f, .31f), 1 => new(-.085f, .64f, .31f), 2 => new(.085f, .62f, -.36f), _ => new(-.085f, .62f, -.36f) };

    /// <summary>How far the sole sits below the paw bone when the paw is flat on the ground.</summary>
    public const float Sole = .035f;
    /// <summary>Where the jaws close on what the wolf bites, just behind the nose, at rest.</summary>
    public static readonly Vector3 Bite = new(0, .826f, .76f);

    /// <summary>Each bone's parent and resting position in the model, built facing +Z with the ground at zero.</summary>
    public static readonly (int Parent, Vector3 At)[] Bones = BuildBones();

    private static (int, Vector3)[] BuildBones() {
        var bones = new (int, Vector3)[BrowR + 1];
        bones[Loin] = (-1, new Vector3(0, .66f, -.04f));
        bones[Chest] = (Loin, new Vector3(0, .69f, .22f));
        bones[Pelvis] = (Loin, new Vector3(0, .66f, -.3f));
        bones[Neck] = (Chest, new Vector3(0, .74f, .4f));
        bones[Head] = (Neck, new Vector3(0, .86f, .56f));
        bones[Jaw] = (Head, new Vector3(0, .818f, .6f));
        bones[EarL] = (Head, new Vector3(.062f, .925f, .54f));
        bones[EarR] = (Head, new Vector3(-.062f, .925f, .54f));
        bones[Lip] = (Head, new Vector3(0, .83f, .7f));
        bones[BrowL] = (Head, new Vector3(.1f, .92f, .606f));
        bones[BrowR] = (Head, new Vector3(-.1f, .92f, .606f));
        for (int i = 0; i < TailBones; i++) bones[Tail + i] = (i == 0 ? Pelvis : Tail + i - 1, new Vector3(0, .665f, -.46f - .17f * i));
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
        Black = new("1e1f1c"), Mouth = new("6b2e2b"), InnerEar = new("b7a596"), Gum = new("8f4a4c"), Tongue = new("b8585c"), Tooth = new("efe9d9");
    public static readonly StandardMaterial3D Fur = ModelKit.Vertex(Surface.Flat, .95f);
    private static readonly StandardMaterial3D EyeShine = new() { AlbedoColor = new Color("d9a046"), Roughness = .35f, EmissionEnabled = true, Emission = new Color("d9a046"), EmissionEnergyMultiplier = 1.2f };
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
        // Back line, belly line and half width along the body: a deep chest, a full belly with only a slight tuck, and a croup that slopes down into the thighs.
        (float Z, float Top, float Bottom, float Half, float Keel)[] body = {
            (-.53f, .67f, .6f, .03f, 0), (-.5f, .72f, .54f, .1f, .1f), (-.44f, .755f, .5f, .145f, .15f), (-.34f, .775f, .49f, .155f, .2f),
            (-.22f, .785f, .51f, .145f, .2f), (-.08f, .795f, .47f, .15f, .3f), (.07f, .815f, .41f, .17f, .4f), (.19f, .835f, .37f, .178f, .45f),
            (.3f, .835f, .38f, .17f, .45f) };
        // The thick maned neck carries on from the chest as one surface, its sections tipping back as it rises to the head.
        var neck = new[] {
            new Ring(new Vector3(0, .64f, .385f), .16f, .2f, Chest, -1, 0, .4f, 18), new Ring(new Vector3(0, .71f, .45f), .148f, .165f, Chest, Neck, .5f, .3f, 38),
            new Ring(new Vector3(0, .765f, .5f), .13f, .138f, Neck, -1, 0, .25f, 50), new Ring(new Vector3(0, .82f, .54f), .105f, .11f, Neck, Head, .3f, .2f, 55),
            new Ring(new Vector3(0, .865f, .565f), .08f, .085f, Neck, Head, .7f, 0, 58) };
        var rings = new Ring[body.Length + neck.Length];
        for (int i = 0; i < body.Length; i++) {
            var (z, top, bottom, half, keel) = body[i];
            var (bone, next, blend) = z < -.3f ? (Pelvis, -1, 0f) : z < -.04f ? (Pelvis, Loin, (z + .3f) / .26f) : z < .22f ? (Loin, Chest, (z + .04f) / .26f) : (Chest, -1, 0f);
            rings[i] = new Ring(new Vector3(0, (top + bottom) / 2, z), half, (top - bottom) / 2, bone, next, Mathf.SmoothStep(0, 1, blend), keel);
        }
        neck.CopyTo(rings, body.Length);
        Tube(s, ref start, rings, Vector3.Right, Vector3.Up, 16, (r, _, up) => Coat(up, r >= body.Length || rings[r].Center.Z is > .05f or < -.3f ? 1 : .5f));
        // A broad skull with a dark crown, a tan mask round the eyes and white cheeks, narrowing to a deep muzzle with a darker bridge.
        var head = new Ring[Skull.Length];
        for (int i = 0; i < Skull.Length; i++) head[i] = new Ring(new Vector3(0, Skull[i].Y, Skull[i].Z), Skull[i].W, Skull[i].H, Head);
        Tube(s, ref start, head, Vector3.Right, Vector3.Up, 16, (r, across, up) =>
            r == 0 ? Coat(up, 1)
            : r >= Muzzle ? (up > .55f ? Tan.Lerp(Dark, .45f) : up > -.55f ? Pale.Lerp(Tan, .2f * Math.Max(0, up)) : Mouth)
            : up > .6f ? Grey.Lerp(Dark, Math.Min(1, (up - .6f) * 2.2f + .25f)) : up > .05f ? (Math.Abs(across) > .25f ? Tan.Lerp(Pale, .35f) : Grey.Lerp(Tan, .5f)) : Pale);
        // The lower jaw hangs from its own bone: black lips round a dark mouth, with the tongue lying in it.
        var jaw = new[] {
            new Ring(new Vector3(0, .815f, .6f), .058f, .024f, Jaw), new Ring(new Vector3(0, .807f, .68f), .046f, .02f, Jaw),
            new Ring(new Vector3(0, .808f, .76f), .036f, .015f, Jaw), new Ring(new Vector3(0, .811f, .797f), .02f, .01f, Jaw) };
        Tube(s, ref start, jaw, Vector3.Right, Vector3.Up, 12, (_, _, up) => up > .55f ? Mouth : up > .2f ? Black : Pale);
        var tongue = new[] {
            new Ring(new Vector3(0, .836f, .63f), .024f, .006f, Jaw), new Ring(new Vector3(0, .829f, .7f), .023f, .006f, Jaw),
            new Ring(new Vector3(0, .827f, .76f), .019f, .005f, Jaw), new Ring(new Vector3(0, .824f, .787f), .01f, .004f, Jaw) };
        Tube(s, ref start, tongue, Vector3.Right, Vector3.Up, 8, (_, _, _) => Tongue);
        Teeth(s, ref start);
        Lips(s, ref start);
        foreach (int side in new[] { 1, -1 }) {
            int ear = side > 0 ? EarL : EarR;
            var at = Bones[ear].At;
            // Upright triangular ears, cupped forward: pale inside, dark outside and along the rim.
            var ears = new[] {
                new Ring(at + new Vector3(side * .004f, -.02f, 0), .052f, .042f, ear), new Ring(at + new Vector3(side * .014f, .055f, -.005f), .037f, .027f, ear),
                new Ring(at + new Vector3(side * .024f, .108f, -.01f), .015f, .009f, ear), new Ring(at + new Vector3(side * .027f, .13f, -.012f), .002f, .002f, ear) };
            Tube(s, ref start, ears, new Vector3(1, 0, side * .35f).Normalized(), new Vector3(-side * .35f, 0, 1).Normalized(), 8, (_, across, up) => up > .2f && Math.Abs(across) < .8f ? InnerEar : Dark.Lerp(Grey, .25f));
            // A dark rim round each eye and a slit pupil on it, and over it a heavy brow that a snarl lowers at its inner end.
            Vector3 eye = Eye(side), faces = EyeFaces(side), level = Vector3.Up.Cross(faces).Normalized();
            Tube(s, ref start, new[] { new Ring(eye - faces * .016f, .012f, .01f, Head), new Ring(eye - faces * .009f, .021f, .015f, Head), new Ring(eye - faces * .004f, .017f, .012f, Head) },
                level, Vector3.Up, 8, (_, _, _) => Black);
            Tube(s, ref start, new[] { new Ring(eye + faces * .003f, .003f, .006f, Head), new Ring(eye + faces * .0075f, .004f, .0075f, Head), new Ring(eye + faces * .009f, .001f, .002f, Head) },
                level, Vector3.Up, 6, (_, _, _) => Black);
            int brow = side > 0 ? BrowL : BrowR;
            Vector3 outer = Bones[brow].At, inner = new(side * .03f, .914f, .652f), along = (inner - outer).Normalized();
            Tube(s, ref start, new[] { new Ring(outer - along * .012f, .006f, .006f, brow), new Ring(outer.Lerp(inner, .3f), .012f, .013f, brow), new Ring(outer.Lerp(inner, .75f), .013f, .015f, brow), new Ring(inner + along * .006f, .006f, .007f, brow) },
                Vector3.Up, along.Cross(Vector3.Up).Normalized(), 6, (_, _, _) => Dark.Lerp(Grey, .2f));
        }
        // A broad black nose on the tip of the muzzle.
        Tube(s, ref start, new[] { new Ring(new Vector3(0, .853f, .8f), .018f, .014f, Head), new Ring(new Vector3(0, .854f, .822f), .028f, .022f, Head), new Ring(new Vector3(0, .85f, .842f), .012f, .01f, Head) },
            Vector3.Right, Vector3.Up, 8, (_, _, _) => Black);
        FurSpikes(s, ref start);
        for (int leg = 0; leg < 4; leg++) LegTube(s, ref start, leg);
        // A full brush tail, dark on top and at the tip.
        (float Z, float Y, float W, float H)[] brush = { (-.44f, .675f, .04f, .045f), (-.5f, .665f, .052f, .058f), (-.58f, .66f, .066f, .072f), (-.68f, .655f, .074f, .08f), (-.78f, .655f, .072f, .075f), (-.88f, .655f, .055f, .058f), (-.95f, .655f, .03f, .032f), (-.98f, .655f, .006f, .006f) };
        var tail = new Ring[brush.Length];
        for (int i = 0; i < brush.Length; i++) {
            float along = (-.46f - brush[i].Z) / .17f;
            int bone = Math.Clamp((int)along, 0, TailBones - 1);
            tail[i] = new Ring(new Vector3(0, brush[i].Y, brush[i].Z), brush[i].W, brush[i].H, Tail + bone, bone < TailBones - 1 ? Tail + bone + 1 : -1, bone < TailBones - 1 ? Mathf.SmoothStep(.5f, 1, along - bone) : 0);
        }
        Tube(s, ref start, tail, Vector3.Right, Vector3.Up, 12, (r, _, up) => r >= 5 ? Dark.Darkened(.2f) : up > .1f ? Dark.Lerp(Grey, .3f) : Tan.Lerp(Pale, .5f));
        return s.Commit();
    }

    /// <summary>The skull's sections from the back of the head to the tip of the muzzle: how far forward, how high, and half its width and height.</summary>
    private static readonly (float Z, float Y, float W, float H)[] Skull = {
        (.46f, .865f, .085f, .085f), (.5f, .882f, .118f, .102f), (.545f, .886f, .126f, .102f), (.59f, .882f, .112f, .092f), (.635f, .868f, .08f, .07f),
        (.675f, .857f, .062f, .052f), (.72f, .851f, .054f, .044f), (.765f, .848f, .048f, .039f), (.8f, .847f, .04f, .032f), (.818f, .846f, .02f, .017f) };
    /// <summary>The first of the skull's sections that is muzzle.</summary>
    private const int Muzzle = 5;

    /// <summary>A tooth: a white cone from its root in the gum to its point.</summary>
    private static void Fang(SurfaceTool s, ref int start, Vector3 root, Vector3 point, float width, int bone) =>
        Tube(s, ref start, new[] { new Ring(root, width, width, bone), new Ring(root.Lerp(point, .5f), width * .7f, width * .7f, bone), new Ring(point, .0008f, .0008f, bone) }, Vector3.Right, Vector3.Back, 5, (_, _, _) => Tooth);

    /// <summary>
    /// The teeth and the gums they stand in: long fangs, small front teeth between them and a row of cheek teeth behind, in
    /// the upper jaw and the lower. The lips and the closed jaw hide them; a snarl lifts the lips off the upper ones.
    /// </summary>
    private static void Teeth(SurfaceTool s, ref int start) {
        foreach (int side in new[] { 1, -1 }) {
            // Upper jaw: the gum along the edge of the muzzle, the fang, two front teeth and three cheek teeth.
            Tube(s, ref start, new[] { new Ring(new Vector3(side * .05f, .818f, .65f), .006f, .007f, Head), new Ring(new Vector3(side * .042f, .816f, .73f), .006f, .007f, Head), new Ring(new Vector3(side * .027f, .817f, .798f), .006f, .006f, Head), new Ring(new Vector3(0, .818f, .808f), .006f, .006f, Head) },
                Vector3.Right, Vector3.Up, 6, (_, _, _) => Gum);
            Fang(s, ref start, new Vector3(side * .027f, .816f, .77f), new Vector3(side * .028f, .786f, .774f), .0075f, Head);
            Fang(s, ref start, new Vector3(side * .007f, .815f, .806f), new Vector3(side * .007f, .8f, .808f), .0042f, Head);
            Fang(s, ref start, new Vector3(side * .019f, .815f, .802f), new Vector3(side * .019f, .799f, .804f), .0045f, Head);
            foreach (var (z, x, deep) in new[] { (.735f, .041f, .014f), (.705f, .044f, .016f), (.672f, .047f, .014f) })
                Fang(s, ref start, new Vector3(side * x, .816f, z), new Vector3(side * x, .816f - deep, z), .0062f, Head);
            // Lower jaw: the fang stands ahead of the upper one, with two front teeth and three cheek teeth.
            Fang(s, ref start, new Vector3(side * .024f, .819f, .752f), new Vector3(side * .025f, .846f, .755f), .0068f, Jaw);
            Fang(s, ref start, new Vector3(side * .006f, .818f, .786f), new Vector3(side * .006f, .83f, .788f), .0038f, Jaw);
            Fang(s, ref start, new Vector3(side * .016f, .818f, .781f), new Vector3(side * .016f, .831f, .783f), .004f, Jaw);
            foreach (var (z, x, tall) in new[] { (.72f, .032f, .011f), (.692f, .036f, .013f), (.662f, .04f, .011f) })
                Fang(s, ref start, new Vector3(side * x, .824f, z), new Vector3(side * x, .824f + tall, z), .0055f, Jaw);
        }
    }

    /// <summary>
    /// The upper lips: a flap of pale fur with a black edge down each side of the muzzle and across its front, hanging
    /// over the upper teeth. They follow their own bone, which a snarl lifts.
    /// </summary>
    private static void Lips(SurfaceTool s, ref int start) {
        foreach (int side in new[] { 1, -1 }) {
            var flap = new Ring[Skull.Length - Muzzle + 1];
            for (int i = 0; i < flap.Length; i++) {
                // The flap starts where the muzzle leaves the cheek and runs to its tip, lying on the muzzle's lower flank.
                var (z, y, w, h) = Skull[Muzzle - 1 + i];
                bool tip = i == flap.Length - 1;
                flap[i] = new Ring(new Vector3(side * (w - (tip ? .012f : .003f)), y - h * .6f - .008f, z + (i == 0 ? .012f : 0)), tip ? .004f : .008f, tip ? .014f : .025f, Lip);
            }
            Tube(s, ref start, flap, Vector3.Right, Vector3.Up, 8, (_, _, up) => up < -.45f ? Black : Pale.Lerp(Tan, .15f));
        }
        Tube(s, ref start, new[] { new Ring(new Vector3(-.028f, .819f, .806f), .006f, .016f, Lip), new Ring(new Vector3(0, .82f, .814f), .007f, .017f, Lip), new Ring(new Vector3(.028f, .819f, .806f), .006f, .016f, Lip) },
            Vector3.Back, Vector3.Up, 6, (_, _, up) => up < -.45f ? Black : Pale.Lerp(Tan, .15f));
    }

    /// <summary>A fur spike: a cone from its root in the coat to its tip, <paramref name="width"/> across at the root, shading from one colour to another.</summary>
    private static void FurSpike(SurfaceTool s, ref int start, Vector3 root, Vector3 tip, float width, int bone, Color from, Color to) {
        var along = (tip - root).Normalized();
        var across = along.Cross(Vector3.Up).LengthSquared() < .01f ? Vector3.Right : along.Cross(Vector3.Up).Normalized();
        Tube(s, ref start, new[] { new Ring(root - along * width * .5f, width, width * .7f, bone), new Ring(root.Lerp(tip, .45f), width * .62f, width * .42f, bone), new Ring(tip, .002f, .002f, bone) },
            across, across.Cross(along), 5, (r, _, _) => r == 0 ? from : r == 1 ? from.Lerp(to, .6f) : to);
    }

    /// <summary>
    /// The spiky fur that stands out from the coat: a ruff down each cheek, spikes on the crown, a mane round the neck and
    /// over the shoulders, a spike under the throat and hackles along the back.
    /// </summary>
    private static void FurSpikes(SurfaceTool s, ref int start) {
        var white = Pale.Lightened(.15f);
        foreach (int side in new[] { 1, -1 }) {
            // Cheek ruff, fanning out and back from under the ear down to the jaw.
            FurSpike(s, ref start, new Vector3(side * .108f, .905f, .535f), new Vector3(side * .19f, .915f, .46f), .03f, Head, Grey, Dark);
            FurSpike(s, ref start, new Vector3(side * .116f, .875f, .54f), new Vector3(side * .21f, .86f, .47f), .034f, Head, Pale, Grey);
            FurSpike(s, ref start, new Vector3(side * .11f, .845f, .54f), new Vector3(side * .195f, .8f, .485f), .034f, Head, white, Grey.Lerp(Pale, .4f));
            FurSpike(s, ref start, new Vector3(side * .09f, .82f, .54f), new Vector3(side * .15f, .755f, .5f), .03f, Head, white, Pale);
            FurSpike(s, ref start, new Vector3(side * .032f, .972f, .5f), new Vector3(side * .05f, 1f, .42f), .026f, Head, Grey, Dark);
            // The mane round the neck, and a second, shorter row over the shoulders.
            FurSpike(s, ref start, new Vector3(side * .078f, .885f, .47f), new Vector3(side * .125f, .935f, .37f), .04f, Neck, Grey, Dark);
            FurSpike(s, ref start, new Vector3(side * .13f, .8f, .47f), new Vector3(side * .215f, .815f, .37f), .044f, Neck, Grey.Lerp(Tan, .4f), Grey);
            FurSpike(s, ref start, new Vector3(side * .122f, .7f, .47f), new Vector3(side * .195f, .66f, .39f), .042f, Neck, Tan.Lerp(Pale, .5f), Grey.Lerp(Tan, .5f));
            FurSpike(s, ref start, new Vector3(side * .068f, .632f, .46f), new Vector3(side * .105f, .575f, .415f), .036f, Neck, white, Pale);
            FurSpike(s, ref start, new Vector3(side * .1f, .805f, .3f), new Vector3(side * .15f, .84f, .2f), .038f, Chest, Grey, Dark);
            FurSpike(s, ref start, new Vector3(side * .165f, .69f, .3f), new Vector3(side * .225f, .68f, .21f), .038f, Chest, Grey.Lerp(Tan, .5f), Grey);
        }
        FurSpike(s, ref start, new Vector3(0, .978f, .51f), new Vector3(0, 1.012f, .43f), .028f, Head, Grey, Dark);
        FurSpike(s, ref start, new Vector3(0, .912f, .47f), new Vector3(0, .965f, .36f), .042f, Neck, Dark.Lerp(Grey, .4f), Dark);
        FurSpike(s, ref start, new Vector3(0, .605f, .44f), new Vector3(0, .53f, .43f), .04f, Chest, white, Pale);
        // Hackles along the back, from the withers to the loin.
        FurSpike(s, ref start, new Vector3(0, .832f, .3f), new Vector3(0, .88f, .19f), .04f, Chest, Dark.Lerp(Grey, .3f), Dark);
        FurSpike(s, ref start, new Vector3(0, .822f, .17f), new Vector3(0, .862f, .07f), .034f, Chest, Dark.Lerp(Grey, .3f), Dark);
        FurSpike(s, ref start, new Vector3(0, .802f, .02f), new Vector3(0, .836f, -.07f), .03f, Loin, Dark.Lerp(Grey, .3f), Dark);
        FurSpike(s, ref start, new Vector3(0, .79f, -.14f), new Vector3(0, .818f, -.22f), .028f, Loin, Dark.Lerp(Grey, .3f), Dark);
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
            ? new[] { (pivot.Y + .06f, .062f, .1f, -.01f), (pivot.Y - .04f, .068f, .125f, -.005f), (pivot.Y - .14f, .068f, .118f, .005f), (elbow + .03f, .056f, .085f, .005f),
                (elbow - .05f, .05f, .07f, -.01f), (elbow - .15f, .034f, .045f, -.014f), (wrist + .03f, .025f, .032f, -.012f), (wrist, .027f, .036f, -.018f),
                (wrist - .07f, .022f, .026f, 0f), (fetlock + .015f, .024f, .028f, 0f) }
            : new[] { (pivot.Y + .04f, .058f, .092f, 0f), (pivot.Y - .1f, .062f, .096f, .005f), (elbow + .09f, .056f, .08f, 0f), (elbow + .01f, .05f, .064f, -.012f),
                (elbow - .06f, .041f, .05f, 0f), (elbow - .17f, .031f, .036f, .004f), (wrist + .02f, .026f, .03f, .004f), (wrist - .01f, .027f, .032f, .006f),
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

    /// <summary>Where each eye sits on the skull, and the way it faces: forward and a little outward.</summary>
    private static Vector3 Eye(int side) => new(side * .074f, .898f, .634f);
    private static Vector3 EyeFaces(int side) => new Vector3(side * .8f, .15f, 1).Normalized();

    private static ArrayMesh BuildEyes() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        int start = 0;
        foreach (int side in new[] { 1, -1 }) {
            var at = Eye(side); var faces = EyeFaces(side);
            Tube(s, ref start, new[] { new Ring(at - faces * .006f, .006f, .006f, Head), new Ring(at, .015f, .01f, Head), new Ring(at + faces * .006f, .006f, .005f, Head) },
                Vector3.Up.Cross(faces).Normalized(), Vector3.Up, 8, (_, _, _) => Colors.White);
        }
        return s.Commit();
    }
}
