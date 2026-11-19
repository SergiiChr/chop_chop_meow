using System;
using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>
/// Turns what the remove selector points at red while the acting player can reach it: a building through an overlay
/// on its model, a gravel path tile through a red pad lying on it.
/// <paramref name="scene"/> holds the pad, and <paramref name="modelOf"/> gives a drawn thing's model, or null for one
/// not drawn.
/// </summary>
internal sealed class RemovalMark(Node3D scene, Func<int, Node3D?> modelOf) {
    private static readonly StandardMaterial3D Tint = Flat(new Color(1, .12f, .08f, .55f));
    /// <summary>How far above the ground the pad lies, over the gravel.</summary>
    private const float Lift = .01f;
    private readonly MeshInstance3D pad = HiddenPad(scene);

    /// <summary>What is red now.</summary>
    public Removal Shown { get; private set; }

    private static MeshInstance3D HiddenPad(Node3D scene) {
        var pad = Add(scene, GravelPathShape.Tile, Tint, Vector3.Zero);
        pad.Visible = false;
        return pad;
    }

    /// <summary>
    /// Draws the overlay once over every model on the warm stage, so the first building to turn red builds nothing.
    /// </summary>
    public static void Warm(Node stage) => SetOverride(stage, Tint);

    /// <summary>Turns <paramref name="pointed"/> red while it is in reach, and what was red before back.</summary>
    public void Update(World world, Removal pointed) {
        var mark = world.Construction.InReach(pointed) ? pointed : Removal.None;
        if (mark == Shown) return;
        if (modelOf(Shown.Building) is Node3D was) SetOverride(was, null);
        if (modelOf(mark.Building) is Node3D now) SetOverride(now, Tint);
        pad.Visible = mark.Tile != null;
        if (mark.Tile is Vector2I tile) pad.Position = WorldView.To3(tile, Lift);
        Shown = mark;
    }
}
