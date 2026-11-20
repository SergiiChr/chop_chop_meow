using System;
using Godot;

namespace ChopChopMeow;

/// <summary>Wind, worn footpaths, gravel paths and the sound listener.</summary>
public partial class WorldView {
    private readonly AudioListener3D listener = new();
    /// <summary>Footpaths worn by walking; they are not saved, so another world starts on unworn ground.</summary>
    internal FootpathWear Footpaths { get; } = new();
    private readonly GravelPathView gravelPaths = new();

    private void BuildLook() {
        dressingMaterial.SetShaderParameter("traffic", Footpaths.Texture);
        terrainView.SetTraffic(Footpaths.Texture);
        dressingMaterial.SetShaderParameter("gravel", gravelPaths.Texture);
        terrainView.SetGravel(gravelPaths.Texture);
        gravelPaths.Update(World.GravelPaths);
        AddChild(listener);
        listener.MakeCurrent();
    }

    /// <summary>Per-frame look updates; none of them allocates.</summary>
    private void UpdateLook(float dt) {
        Wind.Update(dt, GrassParters());
        LakeAppearance.Advance(dt);
        terrainView.Follow(EnvironmentReview ? EnvironmentCenter ?? World.Player : World.Player);
        SyncListener();
        if (dt > 0) Footpaths.Update(World, dt);
        gravelPaths.Update(World.GravelPaths);
    }

    private readonly Vector2[] parters = new Vector2[Wind.Pushers];
    /// <summary>
    /// Where the people the grass parts round stand: this player, the others and the workers in sight, the nearest
    /// <see cref="Wind.Pushers"/> of them to this player when there are more.
    /// </summary>
    private ReadOnlySpan<Vector2> GrassParters() {
        int count = 0;
        Part(World.Player, ref count);
        foreach (var p in World.Players) if (p != World.Me && p.Sheltered == 0 && World.Sees(p.Pos)) Part(p.Pos, ref count);
        foreach (var w in World.Workers) if (World.Sees(w)) Part(w.Pos, ref count);
        return parters.AsSpan(0, count);
    }

    /// <summary>Adds someone the grass parts round, in place of the farthest from this player once the list is full.</summary>
    private void Part(Vector2 at, ref int count) {
        if (count < parters.Length) { parters[count++] = at; return; }
        int farthest = 1;
        for (int i = 2; i < count; i++) if (parters[i].DistanceSquaredTo(World.Player) > parters[farthest].DistanceSquaredTo(World.Player)) farthest = i;
        if (at.DistanceSquaredTo(World.Player) < parters[farthest].DistanceSquaredTo(World.Player)) parters[farthest] = at;
    }

    /// <summary>The listener hovers over the player, turned like the camera, so sounds pan with their place on screen.</summary>
    private void SyncListener() {
        // A camera looks down its -Z axis, so +Z points back toward the viewer.
        listener.GlobalTransform = new Transform3D(camera.Basis, To3(World.Player) + camera.Basis.Z * camera.Size * .5f);
    }
}
