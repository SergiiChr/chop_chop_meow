using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// A loose diary page: one sheet of paper written over in rows of ink, cupped a little, after the sheets of NVIDIA's SimReady "Paper" (paper_stack_a05).
/// On the lake it floats and bobs; off a tree it flutters down, swinging from side to side and tipping into each swing.
/// </summary>
public static class PageShape {
    /// <summary>The name of the page's icon, rendered from this model, that the Diary's cells show.</summary>
    public const string Icon = "diary_page";
    /// <summary>The sheet's width and length in metres, the size of the reference's sheets.</summary>
    private const float Width = .25f, Length = .34f;
    /// <summary>How far its sides curl up above its middle.</summary>
    private const float Cup = .018f;
    private const int Across = 5, Along = 6;
    /// <summary>How far over the ground the sheet's middle lies, clear of the bumps the ground is drawn with.</summary>
    private const float Lift = .03f;
    /// <summary>Rows of writing, and how far in from the edges they stay.</summary>
    private const int Rows = 9;
    private const float Margin = .028f;
    /// <summary>Seconds a page takes to come down off a tree, and the height it starts from, in the tree's crown.</summary>
    public const float FallSeconds = 4;
    private const float Drop = 3.2f;
    /// <summary>Swings a falling page makes from side to side, and how far out each takes it.</summary>
    private const float Swings = 2.5f, Drift = .5f;
    /// <summary>Degrees a falling page tips into each swing.</summary>
    private const float Tilt = 35;

    private static readonly StandardMaterial3D Paper = TwoSided(Cloth("ece3c8", .9f));
    private static readonly StandardMaterial3D Ink = TwoSided(Mat("3f4a5e", .7f));
    private static ArrayMesh? sheet, writing;

    /// <summary>A copy of a shared material seen from both sides, leaving the shared one as it was.</summary>
    private static StandardMaterial3D TwoSided(StandardMaterial3D material) {
        var copy = (StandardMaterial3D)material.Duplicate();
        copy.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return copy;
    }

    /// <summary>The sheet's height over its flat middle at a point across and along it.</summary>
    private static float Curl(float x, float z) => Cup * (x / (Width / 2)) * (x / (Width / 2)) + .006f * MathF.Sin(z / Length * MathF.PI * 1.3f);

    public static PageProp Build() {
        var prop = new PageProp { Sheet = new Node3D() };
        prop.Body.AddChild(prop.Sheet);
        Add(prop.Sheet, sheet ??= Sheet(), Paper, Vector3.Zero);
        Add(prop.Sheet, writing ??= Writing(), Ink, Vector3.Zero);
        return prop;
    }

    /// <summary>The paper: a grid bent to <see cref="Curl"/>, lying <see cref="Lift"/> over the ground.</summary>
    private static ArrayMesh Sheet() {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < Across; i++)
            for (int j = 0; j < Along; j++) {
                var a = Point(i, j); var b = Point(i + 1, j); var c = Point(i + 1, j + 1); var d = Point(i, j + 1);
                tool.AddVertex(a); tool.AddVertex(b); tool.AddVertex(c);
                tool.AddVertex(a); tool.AddVertex(c); tool.AddVertex(d);
            }
        tool.GenerateNormals();
        return tool.Commit();

        static Vector3 Point(int i, int j) {
            float x = (i / (float)Across - .5f) * Width, z = (j / (float)Along - .5f) * Length;
            return new Vector3(x, Lift + Curl(x, z), z);
        }
    }

    /// <summary>Rows of ink across the sheet, each a strip just over the paper and of a length of its own, as handwriting leaves them.</summary>
    private static ArrayMesh Writing() {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        const float thick = .006f;
        for (int row = 0; row < Rows; row++) {
            float z = -Length / 2 + Margin + row * (Length - 2 * Margin) / (Rows - 1);
            // A paragraph ends short; the rest run nearly to the margin.
            float end = Width / 2 - Margin - (row is 3 or 8 ? .09f : row % 3 * .012f);
            float start = -Width / 2 + Margin, step = (end - start) / Across;
            for (int k = 0; k < Across; k++) {
                float x0 = start + k * step, x1 = x0 + step;
                var a = Ink(x0, z - thick / 2); var b = Ink(x1, z - thick / 2); var c = Ink(x1, z + thick / 2); var d = Ink(x0, z + thick / 2);
                tool.AddVertex(a); tool.AddVertex(b); tool.AddVertex(c);
                tool.AddVertex(a); tool.AddVertex(c); tool.AddVertex(d);
            }
        }
        tool.GenerateNormals();
        return tool.Commit();

        // A point of ink, a hair over the paper.
        static Vector3 Ink(float x, float z) => new(x, Lift + .0015f + Curl(x, z), z);
    }

    /// <summary>Bobs and slowly turns the sheet of a page floating on the lake, at <paramref name="time"/> on the view's clock.</summary>
    public static void Float(PageProp page, int id, float time) {
        float phase = Thing.Variation(id, 31) * MathF.Tau;
        page.Sheet.Position = new Vector3(0, LakeAppearance.Level - Lift + .006f * MathF.Sin(time * 1.7f + phase), 0);
        page.Sheet.Rotation = new Vector3(.04f * MathF.Sin(time * 1.3f + phase), time * .05f + phase, .04f * MathF.Sin(time * 1.1f));
    }

    /// <summary>
    /// Moves a page falling off a tree on by <paramref name="dt"/>: down from the crown at a steady pace, swinging from side to side less and less, and tipping
    /// up into each swing, so it lands flat where its thing lies.
    /// </summary>
    public static void Fly(PageProp page, int id, float dt) {
        page.Fall = Math.Min(FallSeconds, page.Fall + dt);
        float way = page.Fall / FallSeconds, swing = way * Swings * MathF.Tau, fade = 1 - way * way;
        var side = Vector3.Right.Rotated(Vector3.Up, Thing.Variation(id, 32) * MathF.Tau);
        page.Sheet.Position = side * (Drift * MathF.Sin(swing) * fade) + Vector3.Up * (Drop * (1 - way));
        page.Sheet.Basis = new Basis(side.Cross(Vector3.Up).Normalized(), Mathf.DegToRad(Tilt) * MathF.Cos(swing) * fade) * new Basis(Vector3.Up, way * 2.4f);
    }
}
