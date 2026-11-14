using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Wind, worn footpaths and the sound listener.</summary>
public partial class WorldView {
    private readonly AudioListener3D listener = new();
    private static readonly StringName TerrainTraffic = "traffic";

    // Paths form where people walk: footfall wears a map at two texels a tile, which slowly grows back.
    private const int TrafficScale = 2, TrafficSize = Terrain.Size * TrafficScale;
    private readonly byte[] traffic = new byte[TrafficSize * TrafficSize];
    private Image trafficImage = null!;
    private ImageTexture trafficTexture = null!;
    private float trafficClock, regrowClock;
    private bool trafficChanged;
    /// <summary>Where the player, under id -1, and each worker last set foot; they wear the ground only once they move on from there.</summary>
    private readonly Dictionary<int, Vector2> footfalls = new();
    /// <summary>How far a walker moves between two footfalls before the second wears the ground.</summary>
    private const float FootfallSpacing = .25f;

    private void BuildLook() {
        trafficImage = Image.CreateFromData(TrafficSize, TrafficSize, false, Image.Format.R8, traffic);
        trafficTexture = ImageTexture.CreateFromImage(trafficImage);
        dressingMaterial.SetShaderParameter("traffic", trafficTexture);
        // Terrain3D's material is reached only by its RID; the terrain keeps a reference to the texture through the field.
        RenderingServer.MaterialSetParam(terrainMaterialRid, TerrainTraffic, trafficTexture.GetRid());
        AddChild(listener);
        listener.MakeCurrent();
    }

    /// <summary>Another world starts on unworn ground.</summary>
    private void ClearPaths() {
        Array.Clear(traffic);
        footfalls.Clear();
        ShowPaths();
        trafficClock = 0;
    }

    /// <summary>Hands the wear map to the ground's materials.</summary>
    private void ShowPaths() {
        trafficChanged = false;
        trafficImage.SetData(TrafficSize, TrafficSize, false, Image.Format.R8, traffic);
        trafficTexture.Update(trafficImage);
    }

    /// <summary>How many spots of ground footfall has worn, two to a tile each way.</summary>
    public int WornGround {
        get {
            int worn = 0;
            foreach (byte wear in traffic) if (wear > 0) worn++;
            return worn;
        }
    }

    /// <summary>How worn the ground is at a point, from none at zero to bare at 255.</summary>
    public byte WornAt(Vector2 at) => traffic[(int)(at.Y * TrafficScale) * TrafficSize + (int)(at.X * TrafficScale)];

    /// <summary>Per-frame look updates; none of them allocates.</summary>
    private void UpdateLook(float dt) {
        Wind.Update(dt, World.Player);
        UpdateTerrainTarget();
        SyncListener();
        if (dt > 0) UpdateTraffic(dt);
    }

    /// <summary>The listener hovers over the player, turned like the camera, so sounds pan with their place on screen.</summary>
    private void SyncListener() {
        // A camera looks down its -Z axis, so +Z points back toward the viewer.
        listener.GlobalTransform = new Transform3D(camera.Basis, To3(World.Player) + camera.Basis.Z * camera.Size * .5f);
    }

    private void UpdateTraffic(float dt) {
        trafficClock += dt; regrowClock += dt;
        if (trafficClock < .5f) return;
        trafficClock = 0;
        Tread(-1, World.Player);
        foreach (var worker in World.Workers) Tread(worker.Id, worker.Pos);
        // Grass takes back even a fully bare path within two game days.
        if (regrowClock > World.Data.DaySeconds * 2 / 255) {
            regrowClock = 0;
            for (int i = 0; i < traffic.Length; i++) if (traffic[i] > 0) traffic[i]--;
            trafficChanged = true;
        }
        if (trafficChanged) ShowPaths();
    }

    /// <summary>
    /// One step's wear, strongest under the foot and fading over about a tile, once the walker has moved on from its last
    /// footfall; standing still, or first seen, it wears nothing.
    /// </summary>
    private void Tread(int walker, Vector2 at) {
        bool seen = footfalls.TryGetValue(walker, out var last);
        if (seen && last.DistanceTo(at) < FootfallSpacing) return;
        footfalls[walker] = at;
        if (!seen) return;
        int cx = (int)(at.X * TrafficScale), cy = (int)(at.Y * TrafficScale);
        for (int y = cy - 2; y <= cy + 2; y++)
            for (int x = cx - 2; x <= cx + 2; x++) {
                if (x < 0 || y < 0 || x >= TrafficSize || y >= TrafficSize) continue;
                int distance = Math.Abs(x - cx) + Math.Abs(y - cy), add = distance == 0 ? 6 : distance == 1 ? 4 : distance == 2 ? 2 : 0;
                int index = y * TrafficSize + x;
                traffic[index] = (byte)Math.Min(255, traffic[index] + add);
            }
        trafficChanged = true;
    }
}
