using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Shared pile ground and cached stock shapes derived from existing resource meshes, with a block fallback.</summary>
internal static class PileShape {
    private static ArrayMesh? yard, stack;
    public static Mesh Yard => yard ??= BuildYard();
    public static Mesh Stack => stack ??= BuildStack();
    public static readonly StandardMaterial3D Ground = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1 };

    private static readonly Dictionary<string, Mesh> contents = new();

    private static readonly string[] families = { "logs", "stone", "ore", "coal", "planks", "parts", "bars", "fallback" };

    private static string Family(string item) => item switch {
        "wood" => "logs",
        "stone" => "stone",
        "iron_ore" or "tin_ore" => "ore",
        "coal" => "coal",
        "planks" => "planks",
        "wood_parts" => "parts",
        "iron_bar" or "steel_bar" or "tin_bar" => "bars",
        _ => "fallback"
    };

    private static StandardMaterial3D Material(string family, string color) =>
        ModelKit.Mat(family == "coal" ? "292c30" : color, family == "bars" ? .45f : .95f, family == "bars" ? .5f : 0);

    /// <summary>Builds every stock mesh and item material while loading, so a pile showing a new item mid-game swaps cached resources only.</summary>
    public static void Warm(GameData data) {
        foreach (var family in families) if (!contents.ContainsKey(family)) contents[family] = BuildStock(family, true);
        foreach (var (id, item) in data.Items) Material(Family(id), item.Color);
        Material("fallback", Fallback);
    }

    /// <summary>The color of an item the data does not know.</summary>
    public const string Fallback = "b0a58a";

    /// <summary>Reuse small stock/resource meshes without duplicating nodes or changing inventory slot order.</summary>
    public static void SetStock(MeshInstance3D slot, string item, string color) {
        string family = Family(item);
        slot.Mesh = contents[family];
        slot.MaterialOverride = Material(family, color);
    }

    /// <summary>The stock an item's pile slot shows, in its own proportions rather than fitted to the slot; icons show it this way.</summary>
    public static MeshInstance3D Stock(string item, string color) {
        string family = Family(item);
        return new MeshInstance3D { Mesh = BuildStock(family, false), MaterialOverride = Material(family, color) };
    }

    private static Mesh BuildStock(string family, bool fit) {
        if (family == "fallback") return Stack;
        var faces = new List<Vector3>();
        void Copy(Mesh mesh, Transform3D transform) {
            foreach (var point in mesh.GetFaces()) faces.Add(transform * point);
        }
        void CopyFirstPart(Mesh mesh) {
            // Keep a complete connected lump, so changing the source's triangle count cannot cut it open.
            var vertices = mesh.GetFaces(); var touching = new Dictionary<Vector3, List<int>>();
            for (int i = 0; i < vertices.Length; i++) {
                if (!touching.TryGetValue(vertices[i], out var triangles)) touching[vertices[i]] = triangles = new();
                triangles.Add(i / 3);
            }
            var seen = new HashSet<int> { 0 }; var pending = new Queue<int>(); pending.Enqueue(0);
            while (pending.TryDequeue(out int triangle))
                for (int k = 0; k < 3; k++) {
                    var point = vertices[triangle * 3 + k]; faces.Add(point);
                    foreach (int neighbor in touching[point]) if (seen.Add(neighbor)) pending.Enqueue(neighbor);
                }
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
        if (family == "stone") CopyFirstPart(NatureShape.Rocks("stone", new Color("8d918e"), 0));
        else if (family == "ore") {
            // A complete 20-face nugget from the existing loose-iron mesh, rather than a whole deposit per slot.
            CopyFirstPart(NatureShape.Loose("loose_iron", new Color("a35a2e"), new Color("8b4f2b"), 0));
        } else if (family == "coal") CopyFirstPart(FurnaceShape.CoalPile);
        else {
            // ModelKit's parameter cache supplies the same primitive resources used by the furnace, bench and mine supplies.
            var source = new Node3D();
            if (family == "logs") {
                foreach (float x in new[] { -.075f, .075f }) {
                    var log = ModelKit.Cylinder(source, .11f, .11f, .9f, new(x * 1.5f, 0, 0), ModelKit.Mat("8a5f3c"), new Vector3(90, 0, 0), 7);
                    CopyLog(log.Mesh, log.Transform);
                }
            } else if (family == "parts") {
                // A long tool handle with a knob, a thick grip and two pointed pegs, lying side by side.
                var wood = ModelKit.Mat("b98552");
                var turned = new Vector3(0, 0, 90);
                var shapes = new[] {
                    ModelKit.Cylinder(source, .03f, .038f, .6f, new(0, .038f, -.2f), wood, turned, 8),
                    ModelKit.Cylinder(source, .05f, .05f, .05f, new(-.32f, .05f, -.2f), wood, turned, 8),
                    ModelKit.Cylinder(source, .055f, .055f, .22f, new(-.1f, .055f, .02f), wood, turned, 8),
                    ModelKit.Cylinder(source, .045f, .045f, .03f, new(.025f, .045f, .02f), wood, turned, 8),
                    ModelKit.Cylinder(source, .045f, .045f, .03f, new(-.225f, .045f, .02f), wood, turned, 8),
                };
                foreach (var shape in shapes) Copy(shape.Mesh, shape.Transform);
                foreach (float z in new[] { .19f, .29f }) {
                    var peg = ModelKit.Cylinder(source, .028f, .028f, .2f, new(.05f, .028f, z), wood, turned, 8);
                    var point = ModelKit.Cylinder(source, .028f, .004f, .06f, new(.18f, .028f, z), wood, new Vector3(0, 0, -90), 8);
                    Copy(peg.Mesh, peg.Transform); Copy(point.Mesh, point.Transform);
                }
            } else if (family == "bars") {
                Copy(IngotShape.Mesh, Transform3D.Identity);
            } else
                for (int i = 0; i < 3; i++) {
                    var part = family == "bars"
                        ? ModelKit.Box(source, new(.12f, .06f, .5f), new((i % 2) * .04f, i * .075f, 0), ModelKit.Mat("c2c9c9"))
                        : ModelKit.Box(source, new(1, .15f, .06f), new((i % 2) * .04f, i * .075f, 0), ModelKit.Mat("d2a26a"), new Vector3(90, 0, 0));
                    Copy(part.Mesh, part.Transform);
                }
            source.Free();
        }
        // Fit the stock to its existing slot. All families sit on the same support plane in the 9–4–1 stack.
        var min = faces[0]; var max = min;
        foreach (var point in faces) { min = min.Min(point); max = max.Max(point); }
        var center = (min + max) / 2; var scale = fit ? new Vector3(.72f, .4f, .72f) / (max - min) : Vector3.One;
        var s = Begin();
        for (int i = 0; i < faces.Count; i += 3) {
            var a = (faces[i] - center) * scale; var b = (faces[i + 1] - center) * scale; var c = (faces[i + 2] - center) * scale;
            var normal = (c - a).Cross(b - a).Normalized();
            s.SetNormal(normal);
            s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        }
        return s.Commit();
    }

    private static SurfaceTool Begin() { var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles); return s; }

    private static ArrayMesh BuildYard() {
        var s = Begin();
        Block(s, 1.45f, 1.45f, .02f, .06f, "7a6548");
        // The cross logs butt against the long side logs rather than crossing through their ends.
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
/// Piled resources on a model, shown the way storage piles show them: fixed slots that each hold one item's cached pile stock.
/// Every model that shows piled resources uses one, so a good looks the same in every pile.
/// </summary>
public sealed class StockPile {
    private readonly MeshInstance3D[] slots;
    private readonly string[] shown;

    /// <summary>Adds a hidden slot under <paramref name="parent"/> at each position, its stock scaled by <paramref name="scale"/>.</summary>
    public StockPile(Node3D parent, ReadOnlySpan<Vector3> positions, float scale = 1) {
        slots = new MeshInstance3D[positions.Length]; shown = new string[positions.Length];
        for (int i = 0; i < slots.Length; i++) {
            slots[i] = new MeshInstance3D { Position = positions[i], Scale = Vector3.One * scale, Visible = false };
            parent.AddChild(slots[i]); shown[i] = "";
        }
    }

    public int Count => slots.Length;
    /// <summary>The slot meshes, for checks that compare what piles show.</summary>
    public IReadOnlyList<MeshInstance3D> Slots => slots;

    /// <summary>Shows an item's stock in a slot, or hides the slot for "". Swaps only cached meshes and materials.</summary>
    public void Show(int slot, string item, GameData data) {
        if (shown[slot] == item) return;
        shown[slot] = item;
        var mesh = slots[slot];
        mesh.Visible = item != "";
        if (item == "") return;
        PileShape.SetStock(mesh, item, data.Items.TryGetValue(item, out var def) ? def.Color : PileShape.Fallback);
        // Turn the shared broken coal chunk to avoid a row of identical fracture faces.
        mesh.Rotation = new Vector3(0, item == "coal" ? slot * 2.399963f : 0, 0);
    }

    /// <summary>Shows one item in a slot for every <paramref name="perSlot"/> of <paramref name="count"/>, rounding up, and hides the rest.</summary>
    public void Fill(string item, int count, int perSlot, GameData data) {
        int filled = count <= 0 ? 0 : (count + perSlot - 1) / perSlot;
        for (int i = 0; i < slots.Length; i++) Show(i, i < filled ? item : "", data);
    }
}
