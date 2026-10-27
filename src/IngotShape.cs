using Godot;

namespace ChopChopMeow;

/// <summary>Small cast-metal ingots used by item icons.</summary>
internal static class IngotShape {
    private static readonly string[] metals = { "c2c9c9", "b5d8e4", "dfe2dc" };
    private static ArrayMesh? casting;
    public static Godot.Mesh Mesh => casting ??= Build();

    /// <summary>Builds a low, heavy ingot with a tapered top and a restrained casting mark.</summary>
    public static Node3D Create(string item) {
        int kind = item switch { "steel_bar" => 1, "tin_bar" => 2, _ => 0 };
        var root = new Node3D();
        ModelKit.Add(root, Mesh, ModelKit.Mat(metals[kind], .38f, .72f), Vector3.Zero);
        // A shallow dark runner mark reads as a casting detail without adding letter-like text.
        ModelKit.Box(root, new Vector3(.17f, .008f, .025f), new Vector3(-.08f, .102f, 0),
            ModelKit.Mat(kind == 2 ? "59452f" : "566061", .52f, .55f));
        return root;
    }

    private static ArrayMesh Build() {
        var s = new SurfaceTool();
        s.Begin(Mesh.PrimitiveType.Triangles);
        // End bevels flare into the broad base; the top face is narrower than the foot.
        (float X, float HalfY, float HalfZ)[] rows = {
            (-.36f, .055f, .072f), (-.31f, .10f, .105f),
            (.29f, .10f, .105f), (.36f, .055f, .072f)
        };
        Vector3 Corner(int row, int i) {
            var r = rows[row];
            float y = (i == 0 || i == 3) ? -r.HalfY : r.HalfY;
            float halfZ = y < 0 ? r.HalfZ : r.HalfZ * .68f;
            return new Vector3(r.X, y, (i < 2 ? -1 : 1) * halfZ);
        }
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 outward) {
            var normal = (c - a).Cross(b - a).Normalized();
            if (normal.Dot(outward) < 0) { (b, c) = (c, b); normal = -normal; }
            s.SetNormal(normal);
            s.AddVertex(a); s.AddVertex(b); s.AddVertex(c);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) {
            Face(a, b, c, outward); Face(a, c, d, outward);
        }
        for (int row = 0; row < rows.Length - 1; row++)
            for (int side = 0; side < 4; side++) {
                int next = (side + 1) % 4;
                var a = Corner(row, side); var b = Corner(row, next);
                var c = Corner(row + 1, next); var d = Corner(row + 1, side);
                var outward = new Vector3(0, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
                Quad(a, b, c, d, outward);
            }
        for (int side = 1; side < 3; side++) {
            Face(Corner(0, 0), Corner(0, side), Corner(0, side + 1), Vector3.Left);
            Face(Corner(3, 0), Corner(3, side + 1), Corner(3, side), Vector3.Right);
        }
        return s.Commit();
    }
}
