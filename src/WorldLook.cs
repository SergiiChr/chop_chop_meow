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
        Wind.Update(dt, World.Player);
        terrainView.Follow(EnvironmentReview ? EnvironmentCenter ?? World.Player : World.Player);
        SyncListener();
        if (dt > 0) Footpaths.Update(World, dt);
        gravelPaths.Update(World.GravelPaths);
    }

    /// <summary>The listener hovers over the player, turned like the camera, so sounds pan with their place on screen.</summary>
    private void SyncListener() {
        // A camera looks down its -Z axis, so +Z points back toward the viewer.
        listener.GlobalTransform = new Transform3D(camera.Basis, To3(World.Player) + camera.Basis.Z * camera.Size * .5f);
    }
}
