using System;
using System.Collections.Generic;
using Godot;
using Ring = (float Radius, float Y, string Tint);

namespace ChopChopMeow;

/// <summary>
/// Goods that no world model shows: one small model an item, carrying its own colours.
/// An item's icon shows the model as built and a pile slot shows it fitted to the slot, so a good looks the same in
/// both.
/// Bread, meat and the bowls are baked Kenney Food Kit meshes from <c>assets/models/food</c> in the game's colours;
/// the rest is built here.
/// </summary>
internal static class ItemShape {
    /// <summary>One flat triangle, wound so that it faces outward, in one colour.</summary>
    private readonly record struct Face(Vector3 A, Vector3 B, Vector3 C, Color Tint);
    /// <summary>
    /// How an item is built and what it is made of, and how a pile slot's worth of it is built where one alone would
    /// not fill the slot.
    /// </summary>
    private sealed record Look(Action<List<Face>> Build, StandardMaterial3D Material, Action<List<Face>>? Pile = null);

    /// <summary>How much of a pile slot's width and depth its stock takes, which leaves room between neighbours.</summary>
    private const float Room = .9f;

    private static readonly StandardMaterial3D Plain = ModelKit.Vertex(Surface.Flat, .8f);
    private static readonly StandardMaterial3D Glossy = ModelKit.Vertex(Surface.Flat, .35f);
    private static readonly StandardMaterial3D Sacking = ModelKit.Vertex(Surface.Cloth, .9f);
    private static readonly StandardMaterial3D Steel = ModelKit.Vertex(Surface.Metal, .45f, .35f);
    private static readonly StandardMaterial3D Tin = ModelKit.Vertex(Surface.Metal, .4f, .15f);

    private static readonly Dictionary<string, Look> looks = new() {
        ["steel_parts"] = new(SteelParts, Steel, f => Cross(f, SteelParts)),
        ["tin_can"] = new(f => OpenCan(f, true), Tin, f => SixPack(f, one => OpenCan(one, false))),
        ["canned_jam"] = new(f => Can(f, "b83a5c", "8f2436"), Tin, f => SixPack(f, one => Can(one, "b83a5c", null, false))),
        ["canned_meat_stew"] = new(f => Can(f, "c07a48", "7a4a26"), Tin, f => SixPack(f, one => Can(one, "c07a48", null, false))),
        ["berry_jam"] = new(f => Jar(f, true), Glossy, f => Huddle(f, one => Jar(one, false))),
        ["grass"] = new(f => Sheaf(f, 20, .5f, .24f, false, "8fae55", "7c9c46", "a3bf63"), Plain),
        ["wheat"] = new(f => Sheaf(f, 7, .62f, .19f, true, "f0d060", "e2c25b", "f5dc7a"), Plain),
        ["wheat_seeds"] = new(Seeds, Sacking),
        ["fertilizer"] = new(f => Sack(f, "b89b66", "9a8052", null, ("4a3524", "6b5233")), Sacking),
        ["flour"] = new(f => Sack(f, "f2ece0", "d6cdbb", "8fa3b8", null), Sacking),
        ["dough"] = new(Dough, Plain),
        // Kenney's loaf is a long oval; widened and raised it is a round one.
        ["bread"] = new(f => Baked(f, "loaf_round", Swap(("995941", "c98a45"), ("e68d64", "ecc892")), new(1, 1.3f, 1.45f)), Plain),
        ["raw_meat"] = new(f => Baked(f, "wholer_ham", Swap(("995941", "c4524a"), ("dc9e76", "efe3d0"))), Plain),
        ["burnt_steak"] = new(Steak, Plain, f => Stack(f, Steak)),
        ["meat_stew"] = new(f => Baked(f, "bowl_soup", Swap(("f1f1f6", "e9e6dc"), ("e68d64", "8f5a33"), ("ff8e44", "e0873a"), ("61cb8b", "7fa650"), ("f2bf99", "d9b98a"))), Plain),
        ["crushed_berries"] = new(f => Baked(f, "bowl_soup", Swap(("f1f1f6", "b08050"), ("e68d64", "b8434f"), ("ff8e44", "7d1f2e"), ("61cb8b", "d86a70"), ("f2bf99", "8f2436"))), Plain),
        ["coins"] = new(Coins, Glossy),
    };

    public static IEnumerable<string> Items => looks.Keys;
    public static bool Has(string item) => looks.ContainsKey(item);
    public static StandardMaterial3D Material(string item) => looks[item].Material;

    /// <summary>
    /// Builds the item centred on the origin: one in its own proportions, or a pile slot's worth scaled down to fit a
    /// slot of the given size.
    /// A slot's worth keeps its shape and leaves room round it; only one too tall for the slot is pressed down to it.
    /// </summary>
    public static ArrayMesh Build(string item, Vector3? fit = null) {
        var look = looks[item];
        var faces = new List<Face>();
        if (fit != null && look.Pile != null) look.Pile(faces); else look.Build(faces);
        var (min, max) = Bounds(faces);
        var centre = (min + max) / 2; var scale = Vector3.One;
        if (fit is Vector3 slot) {
            var size = max - min;
            float across = Room * MathF.Min(slot.X / size.X, slot.Z / size.Z);
            scale = new Vector3(across, MathF.Min(across, slot.Y / size.Y), across);
        }
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var (a, b, c, tint) in faces) {
            Vector3 p = (a - centre) * scale, q = (b - centre) * scale, r = (c - centre) * scale;
            s.SetColor(tint); s.SetNormal((r - p).Cross(q - p).Normalized());
            s.AddVertex(p); s.AddVertex(q); s.AddVertex(r);
        }
        return s.Commit();
    }

    private static (Vector3 Min, Vector3 Max) Bounds(List<Face> faces) {
        var min = faces[0].A; var max = min;
        foreach (var (a, b, c, _) in faces) { min = min.Min(a).Min(b).Min(c); max = max.Max(a).Max(b).Max(c); }
        return (min, max);
    }

    /// <summary>What <paramref name="build"/> makes, with its middle on the ground at the origin, and its size.</summary>
    private static (List<Face> Faces, Vector3 Size) One(Action<List<Face>> build) {
        var one = new List<Face>(); build(one);
        var (min, max) = Bounds(one);
        var middle = ((min + max) / 2) with { Y = min.Y };
        for (int i = 0; i < one.Count; i++) one[i] = new(one[i].A - middle, one[i].B - middle, one[i].C - middle, one[i].Tint);
        return (one, max - min);
    }

    private static void Place(List<Face> faces, List<Face> one, Transform3D at) {
        foreach (var (a, b, c, tint) in one) faces.Add(new(at * a, at * b, at * c, tint));
    }

    /// <summary>Three of one standing in a ring, each touching the other two and turned its own way.</summary>
    private static void Huddle(List<Face> faces, Action<List<Face>> build) {
        var (one, size) = One(build);
        float reach = MathF.Max(size.X, size.Z) / 2 / MathF.Sin(MathF.PI / 3);
        for (int i = 0; i < 3; i++) {
            float angle = i * Mathf.Tau / 3;
            Place(faces, one, new Transform3D(new Basis(Vector3.Up, i * 2.4f), new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * reach));
        }
    }

    /// <summary>Six of one standing side by side in two rows of three.</summary>
    private static void SixPack(List<Face> faces, Action<List<Face>> build) {
        var (one, size) = One(build);
        for (int i = 0; i < 6; i++) Place(faces, one, new Transform3D(Basis.Identity, new Vector3((i % 3 - 1) * size.X, 0, (i / 3 - .5f) * size.Z)));
    }

    /// <summary>Two layers of parts spread on the ground, the upper laid across the lower a quarter turn round.</summary>
    private static void Cross(List<Face> faces, Action<List<Face>> build) {
        var (one, size) = One(build);
        Place(faces, one, Transform3D.Identity);
        Place(faces, one, new Transform3D(new Basis(Vector3.Up, MathF.PI / 2), Vector3.Up * size.Y));
    }

    /// <summary>
    /// Two of a flat one lying on each other, the upper sunk into the lower as far as its bevelled edge.
    /// The upper keeps all but its underside, and the lower keeps only its upright sides, which are all that show of it.
    /// </summary>
    private static void Stack(List<Face> faces, Action<List<Face>> build) {
        var (one, size) = One(build);
        var lift = Vector3.Up * size.Y * .8f;
        foreach (var (a, b, c, tint) in one) {
            float up = (c - a).Cross(b - a).Normalized().Y;
            if (MathF.Abs(up) < .3f) faces.Add(new(a, b, c, tint));
            if (up > -.5f) faces.Add(new(a + lift, b + lift, c + lift, tint));
        }
    }

    /// <summary>Kenney's cooked steak, charred.</summary>
    private static void Steak(List<Face> faces) =>
        Baked(faces, "meat_cooked", Swap(("e2a781", "7a5536"), ("c9835b", "5e3b25"), ("e08860", "5e3b25"), ("ab5e41", "2b1c14"), ("61cb8b", "2b1c14")));

    /// <summary>
    /// A baked mesh's triangles, each in the colour <paramref name="recolour"/> gives for its own, the mesh stretched
    /// by <paramref name="stretch"/> where one is given.
    /// </summary>
    private static void Baked(List<Face> faces, string name, Func<Color, Color> recolour, Vector3? stretch = null) {
        var by = stretch ?? Vector3.One;
        var arrays = GD.Load<Mesh>($"res://assets/models/food/{name}.res").SurfaceGetArrays(0);
        var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(); var colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        for (int i = 0; i < points.Length; i += 3) faces.Add(new(points[i] * by, points[i + 1] * by, points[i + 2] * by, recolour(colours[i])));
    }

    /// <summary>
    /// Turns each of a baked model's colours into the game colour paired with the nearest listed one, as much lighter
    /// or darker as it was than that one.
    /// </summary>
    private static Func<Color, Color> Swap(params (string From, string To)[] pairs) {
        var listed = Array.ConvertAll(pairs, pair => (From: new Color(pair.From), To: new Color(pair.To)));
        return colour => {
            var (from, to) = listed[0];
            foreach (var pair in listed) if (Distance(pair.From, colour) < Distance(from, colour)) (from, to) = pair;
            float shade = colour.Luminance / MathF.Max(from.Luminance, .01f);
            return new Color(MathF.Min(to.R * shade, 1), MathF.Min(to.G * shade, 1), MathF.Min(to.B * shade, 1));
        };
    }

    private static float Distance(Color a, Color b) => new Vector3(a.R - b.R, a.G - b.G, a.B - b.B).LengthSquared();

    /// <summary>
    /// Tool heads as they leave the anvil, none with a handle yet: an axe head lying on its cheek, a sledge head behind
    /// it and a collar ring before it.
    /// </summary>
    private static void SteelParts(List<Face> faces) {
        var (bright, steel, dark) = (new Color("a8d2ec"), new Color("7fb4d6"), new Color("2c3e4c"));
        // The axe head from above: the poll, a waist, then the bit flaring to a long beard on the near side; it thins from poll to edge.
        (float X, float Z, float Thick)[] outline = { (-.22f, -.06f, .1f), (-.22f, .06f, .1f), (-.06f, .05f, .085f), (.2f, .27f, .016f), (.24f, -.13f, .016f), (-.06f, -.05f, .085f) };
        Vector3 Top(int i) => new(outline[i].X, outline[i].Thick, outline[i].Z);
        Vector3 Bottom(int i) => new(outline[i].X, 0, outline[i].Z);
        Quad(faces, Top(0), Top(1), Top(2), Top(5), Vector3.Up, bright);
        Quad(faces, Top(5), Top(2), Top(3), Top(4), Vector3.Up, bright);
        for (int i = 0; i < outline.Length; i++) {
            int next = (i + 1) % outline.Length;
            Quad(faces, Bottom(i), Bottom(next), Top(next), Top(i), new Vector3(outline[i].Z - outline[next].Z, 0, outline[next].X - outline[i].X), steel);
        }
        // The eye for the handle runs through the poll, and shows as a dark slot on its near edge.
        Quad(faces, new(-.185f, .025f, .06f), new(-.095f, .025f, .054f), new(-.095f, .07f, .054f), new(-.185f, .07f, .06f), Vector3.Back, dark);
        // The sledge head has its own eye on top.
        var sledge = new Transform3D(new Basis(Vector3.Up, .1f), new Vector3(-.1f, .055f, -.2f));
        Box(faces, sledge, new(.3f, .11f, .11f), steel);
        Quad(faces, sledge * new Vector3(-.04f, .057f, -.022f), sledge * new Vector3(.04f, .057f, -.022f), sledge * new Vector3(.04f, .057f, .022f),
            sledge * new Vector3(-.04f, .057f, .022f), Vector3.Up, dark);
        // The collar is six-sided, like a nut.
        Lathe(faces, new Transform3D(Basis.Identity, new Vector3(-.15f, 0, .24f)),
            new Ring[] { (.085f, 0, "7fb4d6"), (.085f, .07f, "7fb4d6"), (.054f, .07f, "a8d2ec"), (.054f, .004f, "2c3e4c") }, 6);
    }

    private const float CanRadius = .06f, CanHeight = .13f;
    private const string TinShade = "dfe4e1", TinBright = "f4f6f5";

    /// <summary>
    /// An empty tin can, open at the top, its inside drawn as a dark hollow.
    /// It has a rolled rim at each end, three ribs round its body and its cut lid bent up at the back.
    /// A plain can, for cans standing six to a pile slot, is the wall and the hollow alone.
    /// </summary>
    private static void OpenCan(List<Face> faces, bool detail) {
        const float radius = CanRadius, height = CanHeight, rim = radius * 1.07f, rib = radius * 1.05f, mouth = radius * .9f;
        const string dark = "8a9594";
        if (!detail) {
            Lathe(faces, Transform3D.Identity, new Ring[] { (radius, 0, TinShade), (radius, height, TinShade), (0, .03f, dark) }, 8);
            return;
        }
        var rings = new List<Ring> { (rim, 0, TinShade), (rim, .01f, TinBright), (radius, .015f, TinShade) };
        foreach (float y in new[] { .04f, .065f, .09f }) rings.AddRange(new Ring[] { (radius, y - .007f, TinShade), (rib, y, TinBright), (radius, y + .007f, TinShade) });
        rings.AddRange(new Ring[] { (radius, height - .015f, TinShade), (rim, height - .01f, TinShade), (rim, height, TinBright), (mouth, height, TinBright), (0, .03f, dark) });
        Lathe(faces, Transform3D.Identity, rings.ToArray(), 8);
        // The lid stays joined at the back of the mouth and leans away, its inside toward the sun and the game camera.
        float turn = Mathf.DegToRad(-80), lean = Mathf.DegToRad(25);
        Vector3 back = new(MathF.Cos(turn), 0, MathF.Sin(turn)), up = Vector3.Up * MathF.Cos(lean) + back * MathF.Sin(lean), across = Vector3.Up.Cross(back);
        var inside = Vector3.Up * MathF.Sin(lean) - back * MathF.Cos(lean);
        var middle = back * mouth + Vector3.Up * height + up * mouth;
        Vector3 Edge(int i) { float angle = i * Mathf.Tau / 8; return middle + (across * MathF.Cos(angle) + up * MathF.Sin(angle)) * mouth; }
        for (int i = 0; i < 8; i++) {
            Tri(faces, middle, Edge(i), Edge(i + 1), inside, new Color(TinBright));
            Tri(faces, middle, Edge(i), Edge(i + 1), -inside, new Color(TinShade));
        }
    }

    /// <summary>
    /// A tin can sealed under a lid and wrapped in a paper label of the given colour.
    /// The label has a pale panel on the side the game camera sees lit, with a mark of what is inside where
    /// <paramref name="mark"/> gives its colour.
    /// A plain can, for cans standing six to a pile slot, is the label and the lid alone: no rim, no bare tin above and
    /// below the label and no mark.
    /// </summary>
    private static void Can(List<Face> faces, string label, string? mark, bool detail = true) {
        const float radius = CanRadius, height = CanHeight;
        const string panel = "f6eeda", lid = "cdd4d1";
        // The label is the band above the bare tin at the foot, or the whole wall of a plain can.
        string? Panel(int row, int side) => row == (detail ? 1 : 0) && side == 2 ? panel : null;
        Lathe(faces, Transform3D.Identity, detail
            ? new Ring[] { (radius, 0, TinShade), (radius, height * .14f, TinShade), (radius, height * .84f, label), (radius, height, TinShade),
                (radius * .82f, height, TinBright), (0, height * .95f, lid) }
            : new Ring[] { (radius, 0, label), (radius, height, label), (0, height, lid) }, 8, Panel);
        if (mark == null) return;
        // The mark is a diamond in the middle of the panel, standing a hair proud of it.
        float turn = 2.5f * Mathf.Tau / 8, reach = radius * MathF.Cos(MathF.PI / 8) + .001f;
        Vector3 middle = new(MathF.Cos(turn) * reach, height * .49f, MathF.Sin(turn) * reach), facing = new(MathF.Cos(turn), 0, MathF.Sin(turn));
        var along = Vector3.Up.Cross(facing) * .014f; var up = Vector3.Up * .024f;
        Quad(faces, middle - along, middle - up, middle + along, middle + up, facing, new Color(mark));
    }

    /// <summary>The side of a standing good that faces the game camera and still catches the sun, where its bow goes.</summary>
    private static readonly Vector3 Front = new(MathF.Cos(Mathf.DegToRad(96)), 0, MathF.Sin(Mathf.DegToRad(96)));
    private const string Ribbon = "4f7fa8";

    /// <summary>
    /// A bow tied at a knot that faces <paramref name="away"/>, <paramref name="size"/> times the jam jar's: two loops,
    /// and two short ends hanging straight down unless the ribbon's ends are draped some other way.
    /// Each part is drawn from both sides.
    /// </summary>
    private static void Bow(List<Face> faces, Vector3 knot, Vector3 away, float size = 1, bool ends = true) {
        var along = Vector3.Up.Cross(away) * size; var up = Vector3.Up * size; var tint = new Color(Ribbon);
        foreach (int side in new[] { -1, 1 }) {
            var loop = knot + along * side * .04f + away * .012f * size; var end = knot + along * side * .022f + away * .014f * size - up * .046f;
            foreach (var facing in new[] { away, -away }) {
                Tri(faces, knot, loop + up * .02f, loop - up * .014f, facing, tint);
                if (ends) Tri(faces, knot, end - along * side * .011f, end + along * side * .011f, facing, tint.Darkened(.15f));
            }
        }
    }

    /// <summary>
    /// A glass jar of berry jam under a cloth cover tied round the neck with a ribbon, its bow toward the game
    /// camera.
    /// A plain jar, for jars standing three to a pile slot, has the cover alone.
    /// </summary>
    private static void Jar(List<Face> faces, bool detail) {
        const string jam = "b3263c", cloth = "e9dcc0";
        if (!detail) {
            Lathe(faces, Transform3D.Identity, new Ring[] { (.055f, 0, jam), (.055f, .096f, jam), (.046f, .132f, cloth), (0, .138f, "f6eeda") }, 8);
            return;
        }
        // Below the ribbon the cover flares into a frill, and above it the cover swells over the lid.
        Lathe(faces, Transform3D.Identity, new Ring[] {
            (.055f, 0, jam), (.055f, .09f, jam), (.07f, .084f, "c9bb9c"), (.055f, .099f, cloth), (.055f, .109f, Ribbon), (.06f, .116f, cloth), (.048f, .138f, cloth),
            (0, .144f, "f6eeda") }, 8);
        Bow(faces, Front * .058f + Vector3.Up * .104f, Front);
    }

    /// <summary>
    /// A cloth sack standing on the ground, squarish and slumped: tied shut at the neck with a ribbon whose ends hang
    /// down its front, with a printed band if one is given, or open with its top rolled down around what it holds.
    /// </summary>
    private static void Sack(List<Face> faces, string cloth, string shade, string? band, (string Dark, string Light)? holds, float size = 1) {
        var rings = new List<Ring> { (.14f, 0, shade), (.172f, .06f, shade), (.178f, .17f, cloth) };
        if (holds is var (dark, light))
            rings.AddRange(new Ring[] { (.168f, .29f, cloth), (.19f, .32f, cloth), (.17f, .355f, cloth), (.145f, .34f, shade), (.09f, .4f, dark), (0, .43f, light) });
        else
            rings.AddRange(new Ring[] { (.168f, .27f, band ?? cloth), (.125f, .345f, cloth), (.055f, .39f, cloth), (.05f, .41f, Ribbon), (.09f, .455f, cloth), (0, .43f, shade) });
        for (int i = 0; i < rings.Count; i++) rings[i] = (rings[i].Radius * size, rings[i].Y * size, rings[i].Tint);
        Lathe(faces, Transform3D.Identity, rings.ToArray(), 8, square: .1f);
        if (holds != null) return;
        // The ribbon's bow sits at the neck, and its two ends lie over the shoulder and hang down the front.
        var knot = (Front * .06f + Vector3.Up * .4f) * size; var along = Vector3.Up.Cross(Front);
        Bow(faces, knot, Front, 1.6f * size, false);
        foreach (int side in new[] { -1, 1 }) {
            var shoulder = (Front * .155f + along * side * .04f + Vector3.Up * .35f) * size; var end = (Front * .205f + along * side * .07f + Vector3.Up * .24f) * size;
            Vector3 narrow = along * .007f * size, wide = along * .017f * size;
            Quad(faces, knot - narrow, knot + narrow, shoulder + wide, shoulder - wide, Front + Vector3.Up, new Color(Ribbon));
            Quad(faces, shoulder - wide, shoulder + wide, end + wide, end - wide, Front, new Color(Ribbon).Darkened(.15f));
        }
    }

    /// <summary>A small open bag of wheat seeds with a few spilt in front of it.</summary>
    private static void Seeds(List<Face> faces) {
        Sack(faces, "cdb98a", "b09c70", null, ("c9a23c", "e2c25b"), .8f);
        foreach (var (x, z, turn) in new[] { (.2f, .12f, .4f), (.12f, .21f, 1.9f), (.24f, .22f, 2.8f), (.05f, .26f, .9f) }) {
            var along = new Vector3(MathF.Cos(turn), 0, MathF.Sin(turn)) * .03f; var at = new Vector3(x, .012f, z);
            Bar(faces, at - along, at, 0, .014f, 3, new Color("e2c25b"));
            Bar(faces, at, at + along, .014f, 0, 3, new Color("c9a23c"));
        }
    }

    /// <summary>
    /// A sheaf tied at the waist: straight straws cross at the tie, so they spread out below it and above it.
    /// Grass stands on its cut ends and each blade ends in a point.
    /// Wheat lies on its side, an ear on each straw, pointing up and to the right in the icon as tools do.
    /// </summary>
    private static void Sheaf(List<Face> faces, int straws, float height, float spread, bool ears, params string[] tints) {
        int first = faces.Count;
        for (int i = 0; i < straws; i++) {
            // Two straws stand near the middle and the rest ring them; each leans across the middle to the far side.
            float angle = i * 2.399963f, far = i < 2 ? .3f : 1, tall = height * (.84f + .16f * MathF.Abs(MathF.Sin(i * 1.7f)));
            var foot = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * spread * far;
            var top = new Vector3(MathF.Cos(angle + 2.6f), 0, MathF.Sin(angle + 2.6f)) * spread * far * (ears ? 1 : 1.3f) + Vector3.Up * tall;
            var tint = new Color(tints[i % tints.Length]);
            if (!ears) { Bar(faces, foot, top, .03f, 0, 3, tint); continue; }
            var along = (top - foot).Normalized();
            Bar(faces, foot, top, .022f, .022f, 3, tint);
            Bar(faces, top - along * .01f, top + along * .08f, .022f, .07f, 4, tint.Darkened(.08f));
            Bar(faces, top + along * .08f, top + along * .3f, .07f, 0, 4, tint);
        }
        // The straws cross the waist a little off the middle, and the tie wraps them there.
        float tie = height * .42f, waist = spread * .34f + .03f;
        Lathe(faces, Transform3D.Identity, new Ring[] { (waist, tie - .025f, "9a7a44"), (waist, tie + .025f, "9a7a44") }, 6);
        if (!ears) return;
        var lying = new Basis(Vector3.Right, Mathf.DegToRad(-78));
        for (int i = first; i < faces.Count; i++) faces[i] = new(lying * faces[i].A, lying * faces[i].B, lying * faces[i].C, faces[i].Tint);
    }

    /// <summary>A lump of dough pressed down on a floured board: low, longer one way and uneven.</summary>
    private static void Dough(List<Face> faces) {
        Lathe(faces, Transform3D.Identity, new Ring[] { (.27f, 0, "a47548"), (.27f, .03f, "a47548"), (0, .03f, "c79a66") }, 8);
        // Flat where it meets the board, paler where the flour lies on top.
        var middle = new Vector3(.01f, .1f, 0);
        var (points, triangles) = NatureShape.Shape(middle, new(.23f, .1f, .18f), 1, 7, 4, .22f, .04f, .03f, 0);
        for (int i = 0; i < triangles.Count; i += 3) {
            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            var outward = (a + b + c) / 3 - middle;
            Tri(faces, a, b, c, outward, new Color("efdcb0").Lerp(new Color("fbf3dc"), Math.Clamp(outward.Normalized().Y, 0, 1) * .8f));
        }
    }

    /// <summary>A stack of coins with one leaning on it.</summary>
    private static void Coins(List<Face> faces) {
        const float radius = .1f, thick = .028f;
        var stack = new List<Ring> { (radius, 0, "e0ae3a") };
        const int coins = 4;
        for (int i = 1; i <= coins; i++) stack.Add((radius, i * thick, i % 2 == 0 ? "e0ae3a" : "f7cf55"));
        stack.Add((0, coins * thick, "fde488"));
        Lathe(faces, Transform3D.Identity, stack.ToArray(), 10);
        // It leans on the near side with its face up toward the sun.
        var leaning = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(60)), new Vector3(0, .087f, .115f));
        Lathe(faces, leaning, new Ring[] { (0, 0, "e0ae3a"), (radius, 0, "e0ae3a"), (radius, thick, "f7cf55"), (0, thick, "fde488") }, 10);
    }

    private static void Tri(List<Face> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color tint) {
        if ((c - a).Cross(b - a).Dot(outward) < 0) (b, c) = (c, b);
        faces.Add(new(a, b, c, tint));
    }

    private static void Quad(List<Face> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color tint) {
        Tri(faces, a, b, c, outward, tint); Tri(faces, a, c, d, outward, tint);
    }

    private static void Box(List<Face> faces, Transform3D at, Vector3 size, Color tint) {
        var half = size / 2;
        Vector3 Corner(int x, int y, int z) => at * new Vector3(x * half.X, y * half.Y, z * half.Z);
        foreach (int side in new[] { -1, 1 }) {
            Quad(faces, Corner(side, -1, -1), Corner(side, 1, -1), Corner(side, 1, 1), Corner(side, -1, 1), at.Basis * new Vector3(side, 0, 0), tint);
            Quad(faces, Corner(-1, side, -1), Corner(1, side, -1), Corner(1, side, 1), Corner(-1, side, 1), at.Basis * new Vector3(0, side, 0), tint);
            Quad(faces, Corner(-1, -1, side), Corner(1, -1, side), Corner(1, 1, side), Corner(-1, 1, side), at.Basis * new Vector3(0, 0, side), tint);
        }
    }

    /// <summary>
    /// A shape turned about the upright axis of <paramref name="at"/> through rings of a radius and a height, each
    /// band in its upper ring's colour.
    /// A ring of no radius closes the shape, <paramref name="mark"/> gives a band's side another colour, and
    /// <paramref name="square"/> pushes every other corner out to square the shape off.
    /// </summary>
    private static void Lathe(List<Face> faces, Transform3D at, Ring[] rings, int sides, Func<int, int, string?>? mark = null, float square = 0) {
        Vector3 Point(Ring ring, int side) {
            float angle = side * Mathf.Tau / sides, radius = ring.Radius * (1 + square * MathF.Cos(4 * angle));
            return at * new Vector3(MathF.Cos(angle) * radius, ring.Y, MathF.Sin(angle) * radius);
        }
        for (int row = 0; row < rings.Length - 1; row++) {
            var (low, high) = (rings[row], rings[row + 1]);
            for (int side = 0; side < sides; side++) {
                var tint = new Color(mark?.Invoke(row, side) ?? high.Tint);
                float middle = (side + .5f) * Mathf.Tau / sides;
                var outward = at.Basis * new Vector3(MathF.Cos(middle) * (high.Y - low.Y), low.Radius - high.Radius, MathF.Sin(middle) * (high.Y - low.Y));
                if (low.Radius > 0) Tri(faces, Point(low, side), Point(low, side + 1), Point(high, side + 1), outward, tint);
                if (high.Radius > 0) Tri(faces, Point(low, side), Point(high, side + 1), Point(high, side), outward, tint);
            }
        }
    }

    /// <summary>
    /// A straight bar with flat sides from one point to another, going from one radius to the other; a radius of zero
    /// ends it in a point.
    /// </summary>
    private static void Bar(List<Face> faces, Vector3 from, Vector3 to, float fromRadius, float toRadius, int sides, Color tint) {
        var axis = (to - from).Normalized();
        var across = (MathF.Abs(axis.Y) > .9f ? Vector3.Right : Vector3.Up).Cross(axis).Normalized();
        var over = axis.Cross(across);
        Vector3 Spoke(int i) { float turn = i * Mathf.Tau / sides; return across * MathF.Cos(turn) + over * MathF.Sin(turn); }
        for (int i = 0; i < sides; i++) {
            Vector3 here = Spoke(i), next = Spoke(i + 1), facing = here + next;
            if (fromRadius > 0) Tri(faces, from + here * fromRadius, from + next * fromRadius, to + next * toRadius, facing, tint);
            if (toRadius > 0) Tri(faces, from + here * fromRadius, to + next * toRadius, to + here * toRadius, facing, tint);
        }
    }
}
