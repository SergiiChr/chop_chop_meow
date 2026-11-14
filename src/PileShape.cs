using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>How a heap lays its pieces: side by side in a grid, in rows of long pieces lying the same way, loose and turned any way, or as one mound that swells.</summary>
public enum Lay { Neat, Rows, Loose, Mound }

/// <summary>
/// The pile's ground and log frame, and how every good heaps up: the piece a heap is made of, taken from
/// <see cref="ItemShape"/>, and where the pieces go on a pad of a given size.
/// </summary>
internal static class PileShape {
    private static ArrayMesh? yard, frame, stack;
    public static Mesh Yard => yard ??= BuildYard();
    public static Mesh Frame => frame ??= BuildFrame();
    public static Mesh Stack => stack ??= BuildStack();
    public static readonly StandardMaterial3D Ground = ModelKit.Vertex(Surface.Earth, 1);
    public static readonly StandardMaterial3D Timber = ModelKit.Vertex(Surface.Wood, .9f);

    /// <summary>The color of an item the data does not know.</summary>
    public const string Fallback = "b0a58a";
    /// <summary>The most pieces a heap shows, whatever its pad and pieces would take.</summary>
    private const int MostPieces = 120;

    /// <summary>A good as a heap shows it: one piece, what it is made of, how pieces are laid and how much of a piece's height each level adds.</summary>
    public sealed record Heap(Mesh Mesh, StandardMaterial3D? Material, Lay Lay, float Rise) {
        public readonly int Triangles = Mesh.GetFaces().Length / 3;
        public readonly Vector3 Size = Mesh.GetAabb().Size;
    }

    /// <summary>Where a heap's pieces go, in the order they are added, and how much darker each loose one is drawn.</summary>
    public sealed record Places(Transform3D[] At, float[] Shade);

    private static readonly Dictionary<string, Heap> heaps = new();
    private static readonly Dictionary<(Heap, Vector2, int), Places> layouts = new();

    /// <summary>The heap an item makes; an item without a model heaps as plain blocks of its colour.</summary>
    public static Heap HeapOf(string item, string color) {
        if (World.IsCrate(item)) item = "crate";
        if (heaps.TryGetValue(item, out var heap)) return heap;
        return heaps[item] = ItemShape.Has(item)
            ? new Heap(ItemShape.Piece(item), ItemShape.Material(item, color), ItemShape.LayOf(item), ItemShape.RiseOf(item))
            : new Heap(Stack, ModelKit.Mat(color, .95f), Lay.Neat, 1);
    }

    /// <summary>Builds every heap's piece and material while loading, so a pile showing a new good mid-game swaps cached resources only.</summary>
    public static void Warm(GameData data) {
        // Asking for a material's RID builds its shader now rather than on the first pile that shows it.
        foreach (var (id, item) in data.Items) Warm(HeapOf(id, item.Color));
        Warm(HeapOf(ItemShape.Compost, Fallback));
        // A heap without a material of its own carries one in each surface of its mesh.
        static void Warm(Heap heap) {
            _ = heap.Material?.GetRid();
            for (int i = 0; i < heap.Mesh.GetSurfaceCount(); i++) _ = heap.Mesh.SurfaceGetMaterial(i)?.GetRid();
        }
    }

    /// <summary>Adds one piece of every heap to the stage that loading draws, so the first pile to show a good builds nothing mid-game.</summary>
    public static void Stage(Node3D stage, GameData data) {
        foreach (string item in data.Items.Keys) new StockPile(stage, Vector3.Zero, Vector2.One, 1).Show(item, 1, data);
        new StockPile(stage, Vector3.Zero, Vector2.One, 1).Show(ItemShape.Compost, 1, data);
    }

    /// <summary>
    /// Where a heap's pieces go on a pad of the given size, as many as the pad holds and <paramref name="triangles"/> allow.
    /// The ground level fills from the middle out, and each level above is one piece smaller each way and set over
    /// the gaps of the one below; a piece is added only after those it rests on, so any number of pieces makes a heap.
    /// </summary>
    public static Places PlacesOf(Heap heap, Vector2 pad, int triangles) {
        if (layouts.TryGetValue((heap, pad, triangles), out var places)) return places;
        var found = new List<(float Order, Transform3D At, float Shade)>();
        if (heap.Lay == Lay.Mound) found.Add((0, Transform3D.Identity, 1));
        else {
            bool loose = heap.Lay == Lay.Loose;
            // Loose pieces crowd a little; laid ones keep a sliver between them.
            float fit = loose ? .9f : 1.04f; Vector2 step = new(heap.Size.X * fit, heap.Size.Z * fit);
            int across = Math.Max(1, (int)(pad.X / step.X)), deep = Math.Max(1, (int)(pad.Y / step.Y));
            // Rows of long pieces narrow across the pieces only, like stacked logs.
            bool rows = heap.Lay == Lay.Rows; bool alongX = heap.Size.X > heap.Size.Z;
            bool narrowX = across > 1 && !(rows && alongX), narrowZ = deep > 1 && !(rows && !alongX);
            float reach = MathF.Max(step.X, step.Y) * 1.2f;
            var random = new Random(41);
            for (int level = 0; level < 24; level++) {
                int nx = narrowX ? across - level : across, nz = narrowZ ? deep - level : deep;
                if (nx < 1 || nz < 1 || level > 0 && !narrowX && !narrowZ) break;
                for (int ix = 0; ix < nx; ix++)
                    for (int iz = 0; iz < nz; iz++) {
                        var at = new Vector3((ix - (nx - 1) / 2f) * step.X, level * heap.Size.Y * heap.Rise, (iz - (nz - 1) / 2f) * step.Y);
                        // A row fills along its pieces before it widens.
                        float far = rows ? (alongX ? MathF.Abs(at.Z) + MathF.Abs(at.X) * .01f : MathF.Abs(at.X) + MathF.Abs(at.Z) * .01f) : new Vector2(at.X, at.Z).Length();
                        var basis = Basis.Identity; float shade = 1;
                        if (loose) {
                            at += new Vector3((float)random.NextDouble() - .5f, 0, (float)random.NextDouble() - .5f) * step.X * .2f;
                            basis = new Basis(Vector3.Up, (float)random.NextDouble() * Mathf.Tau).Scaled(Vector3.One * (.85f + (float)random.NextDouble() * .25f));
                            shade = .8f + (float)random.NextDouble() * .2f;
                        }
                        found.Add((level * reach + far, new Transform3D(basis, at), shade));
                    }
            }
            found.Sort((a, b) => a.Order.CompareTo(b.Order));
            int most = Math.Min(MostPieces, Math.Max(1, triangles / heap.Triangles));
            if (found.Count > most) found.RemoveRange(most, found.Count - most);
        }
        places = new Places(found.ConvertAll(f => f.At).ToArray(), found.ConvertAll(f => f.Shade).ToArray());
        return layouts[(heap, pad, triangles)] = places;
    }

    /// <summary>What an item's icon shows where no other model does: a pair of logs for wood, three boards for planks, and otherwise one of the good as <see cref="ItemShape"/> builds it.</summary>
    public static MeshInstance3D Stock(string item, string color) => item is "wood" or "planks"
        ? new MeshInstance3D { Mesh = BuildIcon(item == "wood"), MaterialOverride = ModelKit.Wood(color, .95f) }
        : new MeshInstance3D { Mesh = ItemShape.Build(item), MaterialOverride = ItemShape.Material(item, color) };

    private static Mesh BuildIcon(bool logs) {
        var faces = new List<Vector3>();
        void Copy(Mesh mesh, Transform3D transform) {
            foreach (var point in mesh.GetFaces()) faces.Add(transform * point);
        }
        void CopyLog(Mesh mesh, Transform3D transform) {
            // Preserve the mine supply log's seven-sided outline, skipping its redundant middle ring.
            var vertices = mesh.GetFaces(); var bounds = mesh.GetAabb(); var ring = new List<Vector3>();
            foreach (var v in vertices)
                if (Mathf.IsEqualApprox(v.Y, bounds.End.Y) && v.X * v.X + v.Z * v.Z > .001f && !ring.Exists(p => p.DistanceSquaredTo(v) < 1e-10f)) ring.Add(v);
            ring.Sort((a, b) => MathF.Atan2(a.Z, a.X).CompareTo(MathF.Atan2(b.Z, b.X)));
            Vector3 Top(int i) => transform * ring[i];
            Vector3 Bottom(int i) => transform * new Vector3(ring[i].X, bounds.Position.Y, ring[i].Z);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 outward) {
                if ((c - a).Cross(b - a).Dot(outward) < 0) (b, c) = (c, b);
                faces.Add(a); faces.Add(b); faces.Add(c);
            }
            for (int i = 0; i < ring.Count; i++) {
                int next = (i + 1) % ring.Count;
                var outward = transform.Basis * new Vector3(ring[i].X + ring[next].X, 0, ring[i].Z + ring[next].Z);
                Face(Bottom(i), Bottom(next), Top(next), outward);
                Face(Bottom(i), Top(next), Top(i), outward);
            }
            for (int i = 1; i < ring.Count - 1; i++) {
                Face(Top(0), Top(i), Top(i + 1), transform.Basis * Vector3.Up);
                Face(Bottom(0), Bottom(i), Bottom(i + 1), transform.Basis * Vector3.Down);
            }
        }
        // ModelKit's parameter cache supplies the same primitive resources used by the furnace, bench and mine supplies.
        var source = new Node3D();
        if (logs)
            foreach (float x in new[] { -.075f, .075f }) {
                var log = ModelKit.Cylinder(source, .11f, .11f, .9f, new(x * 1.5f, 0, 0), ModelKit.Mat("8a5f3c"), new Vector3(90, 0, 0), 7);
                CopyLog(log.Mesh, log.Transform);
            }
        else
            for (int i = 0; i < 3; i++) {
                var part = ModelKit.Box(source, new(1, .15f, .06f), new((i % 2) * .04f, i * .075f, 0), ModelKit.Mat("d2a26a"), new Vector3(90, 0, 0));
                Copy(part.Mesh, part.Transform);
            }
        source.Free();
        var min = faces[0]; var max = min;
        foreach (var point in faces) { min = min.Min(point); max = max.Max(point); }
        var center = (min + max) / 2;
        var s = Begin();
        for (int i = 0; i < faces.Count; i += 3) {
            var a = faces[i] - center; var b = faces[i + 1] - center; var c = faces[i + 2] - center;
            s.SetNormal((c - a).Cross(b - a).Normalized());
            s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        }
        return s.Commit();
    }

    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildYard() {
        var s = Begin();
        Block(s, 1.45f, 1.45f, .02f, .06f, "7a6548");
        return s.Commit();
    }

    /// <summary>The logs edging the yard; the cross logs butt against the long side logs rather than crossing through their ends.</summary>
    private static ArrayMesh BuildFrame() {
        var s = Begin();
        foreach (float side in new[] { -1.4f, 1.4f }) {
            Log(s, new(side, .08f, -1.49f), new(side, .08f, 1.49f));
            Log(s, new(-1.31f, .08f, side), new(1.31f, .08f, side));
        }
        return s.Commit();
    }

    private static ArrayMesh BuildStack() {
        var s = Begin();
        Block(s, .37f, .35f, .4f, .075f, "ffffff");
        return s.Commit();
    }

    private static void Block(SurfaceTool s, float bottomHalf, float topHalf, float height, float corner, string tint) {
        Vector3[] Ring(float half, float y) => new[] {
            new Vector3(-half + corner, y, -half), new Vector3(half - corner, y, -half),
            new Vector3(half, y, -half + corner), new Vector3(half, y, half - corner),
            new Vector3(half - corner, y, half), new Vector3(-half + corner, y, half),
            new Vector3(-half, y, half - corner), new Vector3(-half, y, -half + corner) };
        var lower = Ring(bottomHalf, -height / 2); var upper = Ring(topHalf, height / 2);
        for (int i = 0; i < 8; i++) {
            int next = (i + 1) % 8;
            Quad(s, lower[i], lower[next], upper[next], upper[i], new(lower[i].X + lower[next].X, 0, lower[i].Z + lower[next].Z), tint);
        }
        for (int i = 1; i < 7; i++) {
            Triangle(s, lower[0], lower[i], lower[i + 1], Vector3.Down, tint);
            Triangle(s, upper[0], upper[i], upper[i + 1], Vector3.Up, tint);
        }
    }

    private static void Log(SurfaceTool s, Vector3 start, Vector3 end) {
        const int sides = 6;
        var axis = (end - start).Normalized();
        var across = axis.Cross(Vector3.Up).Normalized();
        Vector3 Point(Vector3 center, int i) => center + across * (MathF.Cos(i * Mathf.Tau / sides) * .09f) + Vector3.Up * (MathF.Sin(i * Mathf.Tau / sides) * .09f);
        for (int i = 0; i < sides; i++) {
            int next = (i + 1) % sides;
            Quad(s, Point(start, i), Point(start, next), Point(end, next), Point(end, i), Point(start, i) - start, i % 2 == 0 ? "8a6a48" : "806142");
        }
        for (int i = 1; i < sides - 1; i++) {
            Triangle(s, Point(start, 0), Point(start, i), Point(start, i + 1), -axis, "b08a5a");
            Triangle(s, Point(end, 0), Point(end, i), Point(end, i + 1), axis, "b08a5a");
        }
    }

    private static void Quad(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, string tint) {
        Triangle(s, a, b, c, outward, tint); Triangle(s, a, c, d, outward, tint);
    }

    private static void Triangle(SurfaceTool s, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, string tint) {
        var normal = (c - a).Cross(b - a).Normalized();
        if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
        s.SetColor(new Color(tint)); s.SetNormal(normal); s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
    }
}

/// <summary>
/// A good heaped on or beside a model, as storage piles show it: pieces of the good on a pad, more of them the more there is.
/// Every model that shows piled goods uses one, so a good looks the same in every pile.
/// The pieces are drawn in one batch, and a change in how many shows costs nothing but a count.
/// </summary>
public sealed class StockPile {
    private readonly MultiMeshInstance3D node;
    private readonly MultiMesh pieces = new() { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true };
    private readonly Vector2 pad;
    private readonly int triangles;
    private string shown = "";
    private PileShape.Places places = null!;
    /// <summary>The share or count last shown, so a refresh that changes nothing costs nothing.</summary>
    private float last = -1;
    private Transform3D mound = Transform3D.Identity;

    /// <summary>
    /// Adds an empty heap under <paramref name="parent"/> with its middle on the ground at <paramref name="at"/>.
    /// Its pieces are drawn <paramref name="scale"/> times their size over a pad of the given width and depth, and
    /// a full heap stays within the given number of triangles.
    /// </summary>
    public StockPile(Node3D parent, Vector3 at, Vector2 pad, int triangles, float scale = 1) {
        this.pad = pad / scale; this.triangles = triangles;
        node = new MultiMeshInstance3D { Multimesh = pieces, Position = at, Scale = Vector3.One * scale, Visible = false };
        parent.AddChild(node);
    }

    /// <summary>How many pieces show.</summary>
    public int Count => node.Visible ? pieces.VisibleInstanceCount : 0;
    /// <summary>The piece the heap is made of, and what it is made of, for checks that compare what piles show.</summary>
    public Mesh? Piece => pieces.Mesh;
    public Material? Material => node.MaterialOverride;
    /// <summary>Where a piece stands on the pad; a mound's one piece is as large as its share made it.</summary>
    public Transform3D Place(int piece) => places.At.Length == 1 ? mound : places.At[piece];

    /// <summary>Shows a share of a full heap of an item, from nothing at 0 to every piece at 1; any at all shows at least one piece.</summary>
    public void Fill(string item, float share, GameData data) {
        if (item == "" || share <= 0) { node.Visible = false; last = -1; return; }
        if (shown == item && last == share) return;
        Change(item, data); last = share;
        share = MathF.Min(share, 1);
        if (places.At.Length == 1) {
            // A mound is one piece that swells with what is in it.
            mound = new Transform3D(Basis.FromScale(Vector3.One * (.45f + .55f * MathF.Cbrt(share))), Vector3.Zero);
            pieces.SetInstanceTransform(0, mound);
            pieces.VisibleInstanceCount = 1;
        } else pieces.VisibleInstanceCount = Math.Clamp((int)MathF.Ceiling(share * places.At.Length), 1, places.At.Length);
        node.Visible = true;
    }

    /// <summary>Shows a number of pieces of an item, as many as the heap holds at most.</summary>
    public void Show(string item, int count, GameData data) {
        if (item == "" || count <= 0) { node.Visible = false; last = -1; return; }
        if (shown == item && last == count) return;
        Change(item, data); last = count;
        pieces.VisibleInstanceCount = Math.Min(count, places.At.Length);
        node.Visible = true;
    }

    /// <summary>Lays out the heap for another item. Swaps only cached meshes, materials and places.</summary>
    private void Change(string item, GameData data) {
        if (shown == item) return;
        shown = item;
        var heap = PileShape.HeapOf(item, data.Items.TryGetValue(item, out var def) ? def.Color : PileShape.Fallback);
        places = PileShape.PlacesOf(heap, pad, triangles);
        pieces.Mesh = heap.Mesh; node.MaterialOverride = heap.Material;
        pieces.InstanceCount = places.At.Length;
        for (int i = 0; i < places.At.Length; i++) {
            pieces.SetInstanceTransform(i, places.At[i]);
            pieces.SetInstanceColor(i, new Color(places.Shade[i], places.Shade[i], places.Shade[i]));
        }
    }
}
