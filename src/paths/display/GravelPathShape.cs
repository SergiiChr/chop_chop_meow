using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// One gravel path tile as a flat model, for the plan's icon, its ghost while placing and the remove selector's red
/// pad.
/// Laid tiles are drawn by the ground itself (<see cref="GravelPathView"/>), joined to their neighbours.
/// </summary>
internal static class GravelPathShape {
    /// <summary>How far above the ground the model lies, clear of it at every zoom.</summary>
    private const float Lift = .012f;
    /// <summary>Sides the pad's rim is drawn with.</summary>
    private const int Sides = 24;
    private static ArrayMesh? tile;

    /// <summary>A tile on its own: a round pad as wide as a path.</summary>
    public static Mesh Tile => tile ??= BuildTile();

    private static ArrayMesh BuildTile() {
        var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        void Point(Vector2 p) { s.SetNormal(Vector3.Up); s.SetUV(p); s.AddVertex(new Vector3(p.X, Lift, p.Y)); }
        Vector2 Rim(int i) => Vector2.Right.Rotated(i * MathF.Tau / Sides) * GravelOutline.HalfWidth;
        // A fan from the middle round the rim.
        for (int i = 0; i < Sides; i++) { Point(Vector2.Zero); Point(Rim(i)); Point(Rim(i + 1)); }
        s.GenerateTangents();
        return s.Commit();
    }
}
