using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Footpaths worn where the player and workers walk: footfall wears a map at two texels a tile, which slowly grows
/// back and which the ground and its plants are drawn by.
/// </summary>
internal sealed class FootpathWear {
    private const int Scale = 2, Size = Terrain.Size * Scale;
    private readonly byte[] traffic = new byte[Size * Size];
    private readonly Image image;
    private float clock, regrowClock;
    private bool changed;
    /// <summary>Where the player, under id -1, and each worker last set foot; they wear the ground only once they move on from there.</summary>
    private readonly Dictionary<int, Vector2> footfalls = new();
    /// <summary>How far a walker moves between two footfalls before the second wears the ground.</summary>
    private const float FootfallSpacing = .25f;

    /// <summary>The wear map, from none at zero to bare at one.</summary>
    public ImageTexture Texture { get; }

    public FootpathWear() {
        image = Image.CreateFromData(Size, Size, false, Image.Format.R8, traffic);
        Texture = ImageTexture.CreateFromImage(image);
    }

    /// <summary>Another world starts on unworn ground.</summary>
    public void Clear() {
        Array.Clear(traffic);
        footfalls.Clear();
        Show();
        clock = 0;
    }

    /// <summary>Hands the wear map to the ground's materials.</summary>
    private void Show() {
        changed = false;
        image.SetData(Size, Size, false, Image.Format.R8, traffic);
        Texture.Update(image);
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
    public byte WornAt(Vector2 at) => traffic[(int)(at.Y * Scale) * Size + (int)(at.X * Scale)];

    /// <summary>Wears the ground under <paramref name="world"/>'s walkers twice a second and lets it grow back; it allocates nothing.</summary>
    public void Update(World world, float dt) {
        clock += dt; regrowClock += dt;
        if (clock < .5f) return;
        clock = 0;
        Tread(-1, world.Player);
        foreach (var worker in world.Workers) Tread(worker.Id, worker.Pos);
        // Grass takes back even a fully bare path within two game days.
        if (regrowClock > world.Data.DaySeconds * 2 / 255) {
            regrowClock = 0;
            for (int i = 0; i < traffic.Length; i++) if (traffic[i] > 0) traffic[i]--;
            changed = true;
        }
        if (changed) Show();
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
        int cx = (int)(at.X * Scale), cy = (int)(at.Y * Scale);
        for (int y = cy - 2; y <= cy + 2; y++)
            for (int x = cx - 2; x <= cx + 2; x++) {
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                int distance = Math.Abs(x - cx) + Math.Abs(y - cy), add = distance == 0 ? 6 : distance == 1 ? 4 : distance == 2 ? 2 : 0;
                int index = y * Size + x;
                traffic[index] = (byte)Math.Min(255, traffic[index] + add);
            }
        changed = true;
    }
}
