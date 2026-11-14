using System;
using System.Collections.Generic;
using Godot;
using Ring = (float Radius, float Y, string Tint);

namespace ChopChopMeow;

/// <summary>
/// One small model an item, carrying its own colours, for icons and for piles.
/// A good that no world model shows is built here, or baked from Kenney's Food Kit in <c>assets/models/food</c> and
/// given the game's colours: its icon shows the model as built and a pile slot shows it scaled to the slot.
/// A good the world does show keeps the icon of that model, and has a lighter copy of it here for piles.
/// </summary>
internal static class ItemShape {
    /// <summary>One flat triangle, wound so that it faces outward, in one colour.</summary>
    private readonly record struct Face(Vector3 A, Vector3 B, Vector3 C, Color Tint, Drape? Draped = null);
    /// <summary>A face shaded smooth with a scan draped over it: each corner's normal and place on the scan, in world units.</summary>
    private readonly record struct Drape(Vector3 NormalA, Vector3 NormalB, Vector3 NormalC, Vector2 A, Vector2 B, Vector2 C) {
        public Drape Turned(Basis basis) => this with { NormalA = (basis * NormalA).Normalized(), NormalB = (basis * NormalB).Normalized(), NormalC = (basis * NormalC).Normalized() };
    }
    /// <summary>
    /// How an item is built and what it is made of, and how a pile heaps it.
    /// A heap's piece is the item as built, or what <paramref name="Piece"/> builds where the item as built is too
    /// detailed or too small to heap; it is scaled so its longer side on the ground is <paramref name="Size"/>, or
    /// left as built at zero, and each level of the heap adds <paramref name="Rise"/> of its height.
    /// </summary>
    private sealed record Look(Action<List<Face>> Build, StandardMaterial3D Material, float Size = 0, Lay Lay = Lay.Neat, float Rise = 1,
        Action<List<Face>>? Piece = null, StandardMaterial3D? Scan = null);

    /// <summary>A compost pile's heap: not an item, but heaped like one.</summary>
    public const string Compost = "compost";

    private static readonly StandardMaterial3D Plain = ModelKit.Vertex(Surface.Flat, .8f);
    private static readonly StandardMaterial3D Glossy = ModelKit.Vertex(Surface.Flat, .35f);
    private static readonly StandardMaterial3D Wooden = ModelKit.Vertex(Surface.Wood, .95f);
    private static readonly StandardMaterial3D Sacking = ModelKit.Vertex(Surface.Cloth, .9f);
    private static readonly StandardMaterial3D Steel = ModelKit.Vertex(Surface.Metal, .45f, .35f);
    private static readonly StandardMaterial3D Tin = ModelKit.Vertex(Surface.Metal, .4f, .15f);

    private static readonly Dictionary<string, Look> looks = new() {
        ["wood_parts"] = new(f => WoodParts(f, 8), Wooden, .55f, Lay.Loose, Piece: f => WoodParts(f, 5)),
        ["steel_parts"] = new(SteelParts, Steel, .5f, Lay.Loose),
        // Cans heap as six-packs of plain cans.
        ["tin_can"] = new(f => OpenCan(f, true), Tin, .42f, Piece: f => SixPack(f, one => OpenCan(one, false))),
        ["canned_jam"] = new(f => Can(f, "b83a5c", "8f2436"), Tin, .42f, Piece: f => SixPack(f, one => Can(one, "b83a5c", null, false))),
        ["canned_meat_stew"] = new(f => Can(f, "c07a48", "7a4a26"), Tin, .42f, Piece: f => SixPack(f, one => Can(one, "c07a48", null, false))),
        ["berry_jam"] = new(f => Jar(f, true), Glossy, .2f, Piece: f => Jar(f, false)),
        ["grass"] = new(f => Sheaf(f, 20, .5f, .24f, false, "8fae55", "7c9c46", "a3bf63"), Plain, .32f, Rise: .7f),
        ["wheat"] = new(f => Sheaf(f, 7, .62f, .19f, true, "f0d060", "e2c25b", "f5dc7a"), Plain, .6f, Lay.Rows, .7f),
        ["wheat_seeds"] = new(Seeds, Sacking, .36f, Rise: .8f),
        ["fertilizer"] = new(f => Sack(f, "b89b66", "9a8052", null, ("4a3524", "6b5233")), Sacking, .36f, Rise: .8f),
        ["flour"] = new(f => Sack(f, "f2ece0", "d6cdbb", "8fa3b8", null), Sacking, .36f, Rise: .8f),
        ["dough"] = new(Dough, Plain, .36f, Lay.Loose, .8f),
        // Kenney's loaf is a long oval; widened and raised it is a round one.
        ["bread"] = new(f => Baked(f, "loaf_round", Swap(("995941", "c98a45"), ("e68d64", "ecc892")), new(1, 1.3f, 1.45f)), Plain, .36f, Lay.Loose, .7f),
        ["raw_meat"] = new(f => Baked(f, "wholer_ham", Swap(("995941", "c4524a"), ("dc9e76", "efe3d0"))), Plain, .42f, Lay.Loose, .7f),
        ["burnt_steak"] = new(f => Baked(f, "meat_cooked", Swap(("e2a781", "7a5536"), ("c9835b", "5e3b25"), ("e08860", "5e3b25"), ("ab5e41", "2b1c14"), ("61cb8b", "2b1c14"))), Plain,
            .32f, Lay.Loose),
        ["meat_stew"] = new(f => Baked(f, "bowl_soup", Swap(("f1f1f6", "e9e6dc"), ("e68d64", "8f5a33"), ("ff8e44", "e0873a"), ("61cb8b", "7fa650"), ("f2bf99", "d9b98a"))), Plain, .3f, Rise: .7f),
        ["crushed_berries"] = new(f => Baked(f, "bowl_soup", Swap(("f1f1f6", "b08050"), ("e68d64", "b8434f"), ("ff8e44", "7d1f2e"), ("61cb8b", "d86a70"), ("f2bf99", "8f2436"))), Plain, .3f,
            Rise: .7f),
        ["coins"] = new(Coins, Glossy, .26f),
        // What follows has a model in the world, which its icon shows; a pile heaps lighter copies of it or of a piece of it.
        ["wood"] = new(Log, Wooden, Lay: Lay.Rows, Rise: .85f),
        ["planks"] = new(Plank, Wooden, Lay: Lay.Rows),
        ["stone"] = new(f => First(f, NatureShape.Rocks(0), 80), NatureShape.PlainRock, .42f, Lay.Loose, .7f),
        ["iron_ore"] = Nugget("pile_iron", "a35a2e"),
        ["tin_ore"] = Nugget("pile_tin", "b9bcb4"),
        ["coal"] = new(f => First(f, NatureShape.Lumps("pile_coal", 0, 0), 20), NatureShape.CoalRock, .24f, Lay.Loose, .7f),
        ["charcoal"] = new(f => Copy(f, CharcoalShape.Briquette, Transform3D.Identity, Colors.White), CharcoalShape.Material, .32f, Lay.Loose, .8f),
        // Bars heap as six-packs: three lying side by side and three across them.
        ["iron_bar"] = new(Ingots, Plain, .5f),
        ["steel_bar"] = new(Ingots, Plain, .5f),
        ["tin_bar"] = new(Ingots, Plain, .5f),
        ["steel_axe"] = Held("steel_axe"),
        ["steel_pick"] = Held("steel_pick"),
        // The shovel's blade spans the other way from an axe's, so it lies on its back.
        ["steel_shovel"] = Held("steel_shovel", new Basis(Vector3.Right, MathF.PI / 2)),
        ["wood_hammer"] = Held("wood_hammer"),
        ["steel_hammer"] = Held("steel_hammer"),
        ["stone_chisel"] = Held("stone_chisel"),
        ["steel_chisel"] = Held("steel_chisel"),
        ["torch"] = new(f => Lighter(f, ActorRig.Torch().Torch, Basis.Identity), Plain, Lay: Lay.Rows),
        ["rake"] = new(f => Copy(f, RackShape.Rake, new Transform3D(new Basis(Vector3.Right, MathF.PI / 2), Vector3.Zero), Colors.White), Plain, Lay: Lay.Rows),
        ["mortar"] = new(f => Copy(f, CookShape.BuildMortar(6, 5), Transform3D.Identity, Colors.White), Plain, .3f),
        ["crate"] = new(f => Copy(f, CrateShape.Cargo, Transform3D.Identity, Colors.White), Plain, .5f),
        ["water"] = Bottle("water"),
        ["water_2"] = Bottle("water_2"),
        ["water_1"] = Bottle("water_1"),
        ["empty_bottle"] = Bottle("empty_bottle"),
        // Berries heap one by one.
        ["berries"] = new(Berry, Glossy, .16f, Lay.Loose, .7f),
        // Leaves and compost are each one mound, which swells with what is in it.
        ["leaves"] = new(Leaves, Plain, Lay: Lay.Mound),
        [Compost] = new(Mulch, Plain, Lay: Lay.Mound, Scan: ModelKit.Scanned(Scan.Soil, 1)),
    };

    /// <summary>A hand tool lying on its side, or as <paramref name="pose"/> turns it, so its head lies flat.</summary>
    private static Look Held(string tool, Basis? pose = null) =>
        new(f => Lighter(f, ActorRig.Tool(tool), pose ?? new Basis(Vector3.Back, MathF.PI / 2)), Plain, Lay: Lay.Rows);
    /// <summary>A bottle lying on its back, its gauge up.</summary>
    private static Look Bottle(string item) =>
        new(f => Lighter(f, BottleShape.Create(Water.BottleSips(item)), new Basis(Vector3.Right, -MathF.PI / 2)), Plain, .45f, Lay.Rows, .85f);
    /// <summary>One loose lump of ore or coal, the first of those the world scatters, in the given colour.</summary>
    private static Look Nugget(string kind, string colour) =>
        new(f => First(f, NatureShape.Loose(kind, new Color(colour), new Color(colour), 0), 20), NatureShape.Rock, .24f, Lay.Loose, .7f);

    public static IEnumerable<string> Items => looks.Keys;
    public static bool Has(string item) => looks.ContainsKey(item);
    public static Lay LayOf(string item) => looks[item].Lay;
    public static float RiseOf(string item) => looks[item].Rise;
    /// <summary>
    /// What an item's model is made of; a bar is plain metal in its item's colour.
    /// None for a model partly drawn as a scan, whose mesh carries a material for each part.
    /// </summary>
    public static StandardMaterial3D? Material(string item, string color) =>
        item is "iron_bar" or "steel_bar" or "tin_bar" ? ModelKit.Metal(color, .45f, .5f) : looks[item].Scan == null ? looks[item].Material : null;

    /// <summary>Builds one of the item as its icon shows it, centred on the origin.</summary>
    public static ArrayMesh Build(string item) {
        var faces = new List<Face>(); looks[item].Build(faces);
        var (min, max) = Bounds(faces);
        return Commit(faces, new Transform3D(Basis.Identity, -(min + max) / 2), looks[item]);
    }

    /// <summary>Builds the piece a heap of the item is made of, standing on the origin at the size a heap shows it.</summary>
    public static ArrayMesh Piece(string item) {
        var look = looks[item];
        var faces = new List<Face>(); (look.Piece ?? look.Build)(faces);
        // The game's camera looks down on a heap, so it never sees an underside.
        faces.RemoveAll(face => (face.C - face.A).Cross(face.B - face.A).Normalized().Y < -.9f);
        var (min, max) = Bounds(faces);
        float scale = look.Size > 0 ? look.Size / MathF.Max(max.X - min.X, max.Z - min.Z) : 1;
        return Commit(faces, new Transform3D(Basis.FromScale(Vector3.One * scale), -new Vector3((min.X + max.X) / 2, min.Y, (min.Z + max.Z) / 2) * scale), look);
    }

    /// <summary>
    /// The faces as a mesh: flat-shaded ones in one surface and draped ones, with tangents, in another.
    /// A mesh with both carries the look's material for each, since one material cannot draw both.
    /// </summary>
    private static ArrayMesh Commit(List<Face> faces, Transform3D at, Look look) {
        var mesh = new ArrayMesh();
        foreach (bool draped in new[] { false, true }) {
            var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
            int count = 0;
            foreach (var face in faces) {
                if (face.Draped.HasValue != draped) continue;
                count++;
                Vector3 p = at * face.A, q = at * face.B, r = at * face.C;
                s.SetColor(face.Tint);
                if (face.Draped is Drape drape) {
                    foreach (var (corner, normal, uv) in new[] { (p, drape.NormalA, drape.A), (q, drape.NormalB, drape.B), (r, drape.NormalC, drape.C) }) {
                        s.SetNormal(normal); s.SetUV(uv); s.AddVertex(corner);
                    }
                    continue;
                }
                s.SetNormal((r - p).Cross(q - p).Normalized());
                s.AddVertex(p); s.AddVertex(q); s.AddVertex(r);
            }
            if (count == 0) continue;
            if (draped) s.GenerateTangents();
            s.Commit(mesh);
            if (look.Scan != null) mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, draped ? look.Scan : look.Material);
        }
        return mesh;
    }

    private static (Vector3 Min, Vector3 Max) Bounds(List<Face> faces) {
        var min = faces[0].A; var max = min;
        foreach (var face in faces) { min = min.Min(face.A).Min(face.B).Min(face.C); max = max.Max(face.A).Max(face.B).Max(face.C); }
        return (min, max);
    }

    /// <summary>What <paramref name="build"/> makes, with its middle on the ground at the origin, and its size.</summary>
    private static (List<Face> Faces, Vector3 Size) One(Action<List<Face>> build) {
        var one = new List<Face>(); build(one);
        var (min, max) = Bounds(one);
        var middle = ((min + max) / 2) with { Y = min.Y };
        for (int i = 0; i < one.Count; i++) one[i] = one[i] with { A = one[i].A - middle, B = one[i].B - middle, C = one[i].C - middle };
        return (one, max - min);
    }

    private static void Place(List<Face> faces, List<Face> one, Transform3D at) {
        foreach (var face in one) faces.Add(new(at * face.A, at * face.B, at * face.C, face.Tint, face.Draped?.Turned(at.Basis)));
    }

    /// <summary>Six of one standing side by side in two rows of three.</summary>
    private static void SixPack(List<Face> faces, Action<List<Face>> build) {
        var (one, size) = One(build);
        for (int i = 0; i < 6; i++) Place(faces, one, new Transform3D(Basis.Identity, new Vector3((i % 3 - 1) * size.X, 0, (i / 3 - .5f) * size.Z)));
    }

    /// <summary>
    /// Six cast bars as they are stacked: three lying side by side and three across them.
    /// The lower three lose their tops, which the upper three cover.
    /// </summary>
    private static void Ingots(List<Face> faces) {
        var one = new List<Face>(); Copy(one, IngotShape.Mesh, Transform3D.Identity, Colors.White);
        for (int i = -1; i <= 1; i++) {
            foreach (var (a, b, c, tint, _) in one) {
                var lower = new Vector3(0, .1f, i * .22f);
                if ((c - a).Cross(b - a).Normalized().Y < .9f) faces.Add(new(a + lower, b + lower, c + lower, tint));
            }
            Place(faces, one, new Transform3D(new Basis(Vector3.Up, MathF.PI / 2), new Vector3(i * .22f, .3f, 0)));
        }
    }

    /// <summary>A log lying on the ground, as long as a pile's logs have always been.</summary>
    private static void Log(List<Face> faces) =>
        Cylinder(faces, new Transform3D(new Basis(Vector3.Right, MathF.PI / 2), new Vector3(0, .11f, 0)), .11f, .11f, .9f, 7, new Color("af8053"));

    /// <summary>A sawn board lying flat.</summary>
    private static void Plank(List<Face> faces) => Box(faces, new Transform3D(Basis.Identity, new Vector3(0, .03f, 0)), new(.15f, .06f, 1), new Color("d2a26a"));

    /// <summary>The first triangles of a mesh that scatters several lumps: its first lump alone.</summary>
    private static void First(List<Face> faces, Mesh mesh, int triangles) {
        var all = new List<Face>(); Copy(all, mesh, Transform3D.Identity, Colors.White);
        faces.AddRange(all.GetRange(0, triangles));
    }

    /// <summary>
    /// A baked mesh's triangles, each in the colour <paramref name="recolour"/> gives for its own, the mesh stretched
    /// by <paramref name="stretch"/> where one is given.
    /// </summary>
    private static void Baked(List<Face> faces, string name, Func<Color, Color> recolour, Vector3? stretch = null) {
        int first = faces.Count;
        Copy(faces, GD.Load<Mesh>($"res://assets/models/food/{name}.res"), new Transform3D(Basis.FromScale(stretch ?? Vector3.One), Vector3.Zero), Colors.White);
        for (int i = first; i < faces.Count; i++) faces[i] = faces[i] with { Tint = recolour(faces[i].Tint) };
    }

    /// <summary>A mesh's triangles placed by <paramref name="at"/>, each in its first corner's colour, or in <paramref name="plain"/> where the mesh carries none.</summary>
    private static void Copy(List<Face> faces, Mesh mesh, Transform3D at, Color plain) {
        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++) {
            var arrays = mesh.SurfaceGetArrays(surface);
            var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var colours = arrays[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            var index = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            // A mesh with a place on a scan for each point is draped, and keeps its smooth normals.
            var uvs = arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for (int i = 0; i < (index?.Length ?? points.Length); i += 3) {
                int a = index?[i] ?? i, b = index?[i + 1] ?? i + 1, c = index?[i + 2] ?? i + 2;
                var drape = uvs == null ? (Drape?)null : new Drape(normals[a], normals[b], normals[c], uvs[a], uvs[b], uvs[c]).Turned(at.Basis);
                faces.Add(new(at * points[a], at * points[b], at * points[c], colours?[a] ?? plain, drape));
            }
        }
    }

    /// <summary>
    /// A model built from the kit's primitives, turned by <paramref name="pose"/>, as triangles in its materials'
    /// colours and light enough to pile: each cylinder is redrawn on at most six sides, and flames are left out.
    /// The model is freed.
    /// </summary>
    private static void Lighter(List<Face> faces, Node3D model, Basis pose) {
        void Add(Node node, Transform3D parent) {
            var at = node is Node3D placed ? parent * placed.Transform : parent;
            if (node is MeshInstance3D { Mesh: Mesh mesh } part && (part.MaterialOverride ?? mesh.SurfaceGetMaterial(0)) is StandardMaterial3D material
                && material.ShadingMode != BaseMaterial3D.ShadingModeEnum.Unshaded) {
                if (mesh is CylinderMesh round) Cylinder(faces, at, round.BottomRadius, round.TopRadius, round.Height, Math.Min(round.RadialSegments, 6), material.AlbedoColor);
                else Copy(faces, mesh, at, material.AlbedoColor);
            }
            foreach (var child in node.GetChildren()) Add(child, at);
        }
        Add(model, new Transform3D(pose, Vector3.Zero) * model.Transform.AffineInverse());
        model.Free();
    }

    /// <summary>An upright cylinder or cone placed by <paramref name="at"/>, its middle at the origin, with its ends closed.</summary>
    private static void Cylinder(List<Face> faces, Transform3D at, float bottom, float top, float height, int sides, Color tint) {
        Vector3 Point(float radius, float y, int side) { float angle = side * Mathf.Tau / sides; return at * new Vector3(MathF.Cos(angle) * radius, y, MathF.Sin(angle) * radius); }
        for (int side = 0; side < sides; side++) {
            float middle = (side + .5f) * Mathf.Tau / sides;
            var outward = at.Basis * new Vector3(MathF.Cos(middle) * height, bottom - top, MathF.Sin(middle) * height);
            if (bottom > 0) Tri(faces, Point(bottom, -height / 2, side), Point(bottom, -height / 2, side + 1), Point(top, height / 2, side + 1), outward, tint);
            if (top > 0) Tri(faces, Point(bottom, -height / 2, side), Point(top, height / 2, side + 1), Point(top, height / 2, side), outward, tint);
        }
        for (int side = 1; side < sides - 1; side++) {
            if (bottom > 0) Tri(faces, Point(bottom, -height / 2, 0), Point(bottom, -height / 2, side), Point(bottom, -height / 2, side + 1), at.Basis * Vector3.Down, tint);
            if (top > 0) Tri(faces, Point(top, height / 2, 0), Point(top, height / 2, side), Point(top, height / 2, side + 1), at.Basis * Vector3.Up, tint);
        }
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
    /// Turned wooden parts lying side by side: a long tool handle with a knob, a thick grip with a collar at each end
    /// and two pointed pegs, each turned on the given number of sides.
    /// </summary>
    private static void WoodParts(List<Face> faces, int sides) {
        var wood = new Color("b98552");
        // A part lies along X, its thicker end toward -X.
        void Turned(float thick, float thin, float length, float x, float z) =>
            Cylinder(faces, new Transform3D(new Basis(Vector3.Back, MathF.PI / 2), new Vector3(x, MathF.Max(thick, thin), z)), thick, thin, length, sides, wood);
        Turned(.03f, .038f, .6f, 0, -.2f); Turned(.05f, .05f, .05f, -.32f, -.2f);
        Turned(.055f, .055f, .22f, -.1f, .02f); Turned(.045f, .045f, .03f, .025f, .02f); Turned(.045f, .045f, .03f, -.225f, .02f);
        foreach (float z in new[] { .19f, .29f }) {
            Turned(.028f, .028f, .2f, .05f, z);
            Cylinder(faces, new Transform3D(new Basis(Vector3.Back, -MathF.PI / 2), new Vector3(.18f, .028f, z)), .028f, 0, .06f, sides, wood);
        }
    }

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

    /// <summary>One berry, a bare twenty-sided lump.</summary>
    private static void Berry(List<Face> faces) {
        var middle = new Vector3(0, .055f, 0);
        var (points, triangles) = NatureShape.Shape(middle, Vector3.One * .055f, 0, 3, 0, 0, 0, -1, 0);
        for (int t = 0; t < triangles.Count; t += 3) {
            Vector3 a = points[triangles[t]], b = points[triangles[t + 1]], c = points[triangles[t + 2]];
            Tri(faces, a, b, c, (a + b + c) / 3 - middle, new Color("c0283a"));
        }
    }

    /// <summary>
    /// A broad low mound, and where the top of it is at a distance from its middle.
    /// Its faces are flat, each in one of the given colours, or without them shaded smooth with the scan draped over it.
    /// </summary>
    private static Func<float, float> Mound(List<Face> faces, float radius, float height, int seed, Color[]? palette) {
        var middle = new Vector3(0, height * .1f, 0); var radii = new Vector3(radius, height, radius);
        var (points, triangles) = NatureShape.Shape(middle, radii, 2, seed, 5, .12f, .04f, 0, 0);
        var normals = NatureShape.SmoothNormals(points, triangles, middle);
        for (int t = 0; t < triangles.Count; t += 3) {
            int i = triangles[t], j = triangles[t + 1], k = triangles[t + 2];
            Vector3 a = points[i], b = points[j], c = points[k];
            if (palette != null) { Tri(faces, a, b, c, (a + b + c) / 3 - middle, palette[t / 3 * 7 % palette.Length]); continue; }
            if ((c - a).Cross(b - a).Dot((a + b + c) / 3 - middle) < 0) { (b, c) = (c, b); (j, k) = (k, j); }
            faces.Add(new(a, b, c, Colors.White, new Drape(normals[i], normals[j], normals[k],
                NatureShape.Draped(a, middle, radii), NatureShape.Draped(b, middle, radii), NatureShape.Draped(c, middle, radii))));
        }
        return reach => middle.Y + height * MathF.Sqrt(MathF.Max(0, 1 - reach * reach / (radius * radius)));
    }

    /// <summary>The leaf patch's own leaves laid flat on a mound, spread from its middle out.</summary>
    private static void Litter(List<Face> faces, Func<float, float> top, float radius, int leaves) {
        // A leaf of the patch is eleven triangles that start at the middle of its ridge.
        var patch = new List<Face>(); Copy(patch, LeafShape.For(0), Transform3D.Identity, Colors.White);
        for (int leaf = 0; leaf < leaves; leaf++) {
            float angle = leaf * 2.399963f, reach = radius * .85f * MathF.Sqrt((leaf + .5f) / leaves);
            var grown = Basis.FromScale(Vector3.One * 1.5f);
            var to = new Vector3(MathF.Cos(angle) * reach, top(reach) + .012f, MathF.Sin(angle) * reach) - grown * patch[leaf * 11].A;
            for (int t = leaf * 11; t < leaf * 11 + 11; t++) faces.Add(new(grown * patch[t].A + to, grown * patch[t].B + to, grown * patch[t].C + to, patch[t].Tint));
        }
    }

    /// <summary>A raked-up mound of fallen leaves in the leaf patch's colours, with some of the patch's own leaves lying on it.</summary>
    private static void Leaves(List<Face> faces) {
        var top = Mound(faces, .95f, .42f, 11, new Color[] { new("c08a3a"), new("a8652c"), new("d0a347"), new("8e5a2a"), new("b8753a") });
        Litter(faces, top, .95f, 16);
    }

    /// <summary>A compost heap: a mound of dark soil with leaves on it and stalks and scraps poking out of it.</summary>
    private static void Mulch(List<Face> faces) {
        const float radius = 1.1f;
        var top = Mound(faces, radius, .5f, 23, null);
        Litter(faces, top, radius, 7);
        Color[] scraps = { new("8fae55"), new("c08a3a"), new("d9c27a"), new("7c9c46"), new("a8652c") };
        for (int i = 0; i < 20; i++) {
            float angle = i * 2.399963f + 1, reach = radius * .9f * MathF.Sqrt((i + .5f) / 20);
            var foot = new Vector3(MathF.Cos(angle) * reach, top(reach) - .03f, MathF.Sin(angle) * reach);
            var lean = new Vector3(MathF.Cos(angle * 3) * .07f, .16f + .06f * MathF.Sin(i * 1.7f), MathF.Sin(angle * 3) * .07f);
            Bar(faces, foot, foot + lean, .035f, 0, 3, scraps[i % scraps.Length]);
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
