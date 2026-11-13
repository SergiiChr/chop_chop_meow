using System;
using System.Collections.Generic;
using Godot;
using Ring = (float Radius, float Y, string Tint);

namespace ChopChopMeow;

/// <summary>
/// Goods that no world model shows: one small model an item, carrying its own colours.
/// An item's icon shows the model as built and a pile slot shows it fitted to the slot, so a good looks the same in both.
/// Bread, meat and the bowls are baked Kenney Food Kit meshes from <c>assets/models/food</c> in the game's colours; the rest is built here.
/// </summary>
internal static class ItemShape {
    /// <summary>One flat triangle, wound so that it faces outward, in one colour.</summary>
    private readonly record struct Face(Vector3 A, Vector3 B, Vector3 C, Color Tint);
    /// <summary>How an item is built, what it is made of and, where one alone would not fill a pile slot, how several are put together to fill it.</summary>
    private sealed record Look(Action<List<Face>> Build, StandardMaterial3D Material, Func<List<Face>, List<Face>>? Pile = null);

    private static readonly StandardMaterial3D Plain = ModelKit.Vertex(Surface.Flat, .8f);
    private static readonly StandardMaterial3D Glossy = ModelKit.Vertex(Surface.Flat, .35f);
    private static readonly StandardMaterial3D Sacking = ModelKit.Vertex(Surface.Cloth, .9f);
    private static readonly StandardMaterial3D Steel = ModelKit.Vertex(Surface.Metal, .45f, .35f);
    private static readonly StandardMaterial3D Tin = ModelKit.Vertex(Surface.Metal, .4f, .15f);

    private static readonly Dictionary<string, Look> looks = new() {
        ["steel_parts"] = new(SteelParts, Steel),
        ["tin_can"] = new(f => Can(f, null), Tin, f => Huddle(f, 3)),
        ["canned_jam"] = new(f => Can(f, "b83a5c"), Tin, f => Huddle(f, 3)),
        ["canned_meat_stew"] = new(f => Can(f, "c07a48"), Tin, f => Huddle(f, 3)),
        ["berry_jam"] = new(Jar, Glossy, f => Huddle(f, 3)),
        ["grass"] = new(f => Sheaf(f, 20, .5f, .24f, false, "8fae55", "7c9c46", "a3bf63"), Plain),
        ["wheat"] = new(f => Sheaf(f, 7, .62f, .19f, true, "f0d060", "e2c25b", "f5dc7a"), Plain),
        ["wheat_seeds"] = new(Seeds, Sacking),
        ["fertilizer"] = new(f => Sack(f, "b89b66", "9a8052", null, ("4a3524", "6b5233")), Sacking),
        ["flour"] = new(f => Sack(f, "f2ece0", "d6cdbb", "8fa3b8", null), Sacking),
        ["dough"] = new(Dough, Plain),
        ["bread"] = new(f => Baked(f, "loaf_round", Swap(("995941", "c98a45"), ("e68d64", "ecc892"))), Plain),
        ["raw_meat"] = new(f => Baked(f, "wholer_ham", Swap(("995941", "c4524a"), ("dc9e76", "efe3d0"))), Plain),
        ["burnt_steak"] = new(f => Baked(f, "meat_cooked", Swap(("e2a781", "7a5536"), ("c9835b", "5e3b25"), ("e08860", "5e3b25"), ("ab5e41", "2b1c14"), ("61cb8b", "2b1c14"))), Plain,
            f => Stack(f, 2)),
        ["meat_stew"] = new(f => Baked(f, "bowl_soup", Swap(("f1f1f6", "e9e6dc"), ("e68d64", "8f5a33"), ("ff8e44", "e0873a"), ("61cb8b", "7fa650"), ("f2bf99", "d9b98a"))), Plain),
        ["crushed_berries"] = new(f => Baked(f, "bowl_soup", Swap(("f1f1f6", "b08050"), ("e68d64", "b8434f"), ("ff8e44", "7d1f2e"), ("61cb8b", "d86a70"), ("f2bf99", "8f2436"))), Plain),
        ["coins"] = new(Coins, Glossy),
    };

    public static IEnumerable<string> Items => looks.Keys;
    public static bool Has(string item) => looks.ContainsKey(item);
    public static StandardMaterial3D Material(string item) => looks[item].Material;

    /// <summary>
    /// Builds the item centred on the origin: one in its own proportions, or a slot's worth stretched to fill a box of the given size, as a pile slot shows it.
    /// </summary>
    public static ArrayMesh Build(string item, Vector3? fit = null) {
        var look = looks[item];
        var faces = new List<Face>(); look.Build(faces);
        if (fit != null && look.Pile != null) faces = look.Pile(faces);
        var (min, max) = Bounds(faces);
        var centre = (min + max) / 2; var scale = fit is Vector3 size ? size / (max - min) : Vector3.One;
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

    /// <summary>Copies of one standing in a ring, each touching its neighbours and turned its own way.</summary>
    private static List<Face> Huddle(List<Face> one, int count) {
        var (min, max) = Bounds(one);
        var middle = ((min + max) / 2) with { Y = 0 };
        float reach = MathF.Max(max.X - min.X, max.Z - min.Z) / 2 / MathF.Sin(MathF.PI / count);
        var all = new List<Face>();
        for (int i = 0; i < count; i++) {
            float angle = i * Mathf.Tau / count;
            var at = new Transform3D(new Basis(Vector3.Up, i * 2.4f), new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * reach);
            foreach (var (a, b, c, tint) in one) all.Add(new(at * (a - middle), at * (b - middle), at * (c - middle), tint));
        }
        return all;
    }

    /// <summary>
    /// Copies of a flat one lying on each other, each sunk into the one below as far as its bevelled edge.
    /// The top one keeps all but its underside, and the rest keep only their upright sides, which are all that show of them.
    /// </summary>
    private static List<Face> Stack(List<Face> one, int count) {
        var (min, max) = Bounds(one);
        var all = new List<Face>();
        for (int i = 0; i < count; i++) {
            var lift = Vector3.Up * (max.Y - min.Y) * .8f * i;
            foreach (var (a, b, c, tint) in one) {
                float up = (c - a).Cross(b - a).Normalized().Y;
                if (i == count - 1 ? up > -.5f : MathF.Abs(up) < .3f) all.Add(new(a + lift, b + lift, c + lift, tint));
            }
        }
        return all;
    }

    /// <summary>A baked mesh's triangles, each in the colour <paramref name="recolour"/> gives for its own.</summary>
    private static void Baked(List<Face> faces, string name, Func<Color, Color> recolour) {
        var arrays = GD.Load<Mesh>($"res://assets/models/food/{name}.res").SurfaceGetArrays(0);
        var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(); var colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        for (int i = 0; i < points.Length; i += 3) faces.Add(new(points[i], points[i + 1], points[i + 2], recolour(colours[i])));
    }

    /// <summary>
    /// Turns each of a baked model's colours into the game colour paired with the nearest listed one, as much lighter or darker as it was than that one.
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

    /// <summary>Tool heads as they leave the anvil, none with a handle yet: an axe head lying on its cheek, a sledge head behind it and a collar ring before it.</summary>
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
        // The eye for the handle runs through the poll, and shows as a slot in its near edge.
        Box(faces, new Transform3D(new Basis(Vector3.Up, .062f), new Vector3(-.14f, .05f, .057f)), new(.09f, .05f, .008f), dark);
        // The sledge head has its own eye on top.
        var sledge = new Transform3D(new Basis(Vector3.Up, .1f), new Vector3(-.1f, .055f, -.2f));
        Box(faces, sledge, new(.3f, .11f, .11f), steel);
        Box(faces, sledge.TranslatedLocal(Vector3.Up * .056f), new(.08f, .004f, .05f), dark);
        Lathe(faces, new Transform3D(Basis.Identity, new Vector3(-.15f, 0, .24f)),
            new Ring[] { (.08f, 0, "7fb4d6"), (.08f, .07f, "7fb4d6"), (.056f, .07f, "a8d2ec"), (.056f, .004f, "2c3e4c") }, 8);
    }

    /// <summary>
    /// A tin can: open and empty, its inside drawn as a dark hollow, or sealed under a lid and wrapped in a label of the given colour with a pale patch on one side.
    /// </summary>
    private static void Can(List<Face> faces, string? label) {
        const float radius = .06f, height = .13f;
        if (label == null) {
            Lathe(faces, Transform3D.Identity,
                new Ring[] { (radius, 0, "dfe4e1"), (radius, height, "dfe4e1"), (radius * .88f, height, "f4f6f5"), (0, .03f, "8a9594") }, 8);
            return;
        }
        // The patch is on the side the game camera sees lit.
        Lathe(faces, Transform3D.Identity,
            new Ring[] { (radius, 0, label), (radius, height, label), (radius * .82f, height, "f4f6f5"), (0, height * .95f, "cdd4d1") }, 8,
            (row, side) => row == 0 && side == 2 ? "f6eeda" : null);
    }

    /// <summary>A glass jar of berry jam under a cloth cover.</summary>
    private static void Jar(List<Face> faces) => Lathe(faces, Transform3D.Identity,
        new Ring[] { (.055f, 0, "b3263c"), (.055f, .096f, "b3263c"), (.046f, .132f, "e9dcc0"), (0, .138f, "f6eeda") }, 8);

    /// <summary>
    /// A cloth sack standing on the ground, squarish and slumped: tied shut at the neck, with a printed band if one is given,
    /// or open with its top rolled down around what it holds.
    /// </summary>
    private static void Sack(List<Face> faces, string cloth, string shade, string? band, (string Dark, string Light)? holds, float size = 1) {
        var rings = new List<Ring> { (.14f, 0, shade), (.172f, .06f, shade), (.178f, .17f, cloth) };
        if (holds is var (dark, light))
            rings.AddRange(new Ring[] { (.168f, .29f, cloth), (.19f, .32f, cloth), (.17f, .355f, cloth), (.145f, .34f, shade), (.09f, .4f, dark), (0, .43f, light) });
        else
            rings.AddRange(new Ring[] { (.168f, .27f, band ?? cloth), (.125f, .345f, cloth), (.055f, .39f, cloth), (.05f, .41f, "9a7a44"), (.09f, .455f, cloth), (0, .43f, shade) });
        for (int i = 0; i < rings.Count; i++) rings[i] = (rings[i].Radius * size, rings[i].Y * size, rings[i].Tint);
        Lathe(faces, Transform3D.Identity, rings.ToArray(), 8, square: .1f);
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
    /// Grass stands on its cut ends and each blade ends in a point; wheat lies on its side, an ear on each straw, pointing up and to the right in the icon as tools do.
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

    /// <summary>A round of dough resting on a floured board.</summary>
    private static void Dough(List<Face> faces) {
        Lathe(faces, Transform3D.Identity, new Ring[] { (.27f, 0, "a47548"), (.27f, .03f, "a47548"), (0, .03f, "c79a66") }, 8);
        // A lump pressed flat where it meets the board, paler where the flour lies on top.
        var middle = new Vector3(0, .13f, 0);
        var (points, triangles) = NatureShape.Shape(middle, new(.2f, .13f, .2f), 1, 7, 2, .08f, .02f, .03f, 0);
        for (int i = 0; i < triangles.Count; i += 3) {
            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            var outward = (a + b + c) / 3 - middle;
            var normal = (c - a).Cross(b - a).Normalized();
            if (normal.Dot(outward) < 0) normal = -normal;
            Tri(faces, a, b, c, outward, new Color("efdcb0").Lerp(new Color("fbf3dc"), Math.Clamp(normal.Y, 0, 1) * .8f));
        }
    }

    /// <summary>A stack of coins with one leaning on it.</summary>
    private static void Coins(List<Face> faces) {
        const float radius = .1f, thick = .028f;
        var stack = new List<Ring> { (radius, 0, "e0ae3a") };
        for (int i = 1; i <= 5; i++) stack.Add((radius, i * thick, i % 2 == 0 ? "e0ae3a" : "f7cf55"));
        stack.Add((0, 5 * thick, "fde488"));
        Lathe(faces, Transform3D.Identity, stack.ToArray(), 10);
        // It leans on the near side with its face up toward the sun.
        var leaning = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(60)), new Vector3(0, .087f, .127f));
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
    /// A shape turned about the upright axis of <paramref name="at"/> through rings of a radius and a height, each band in its upper ring's colour.
    /// A ring of no radius closes the shape, <paramref name="mark"/> gives a band's side another colour, and <paramref name="square"/> pushes every other corner out to square the shape off.
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

    /// <summary>A straight bar with flat sides from one point to another, going from one radius to the other; a radius of zero ends it in a point.</summary>
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
