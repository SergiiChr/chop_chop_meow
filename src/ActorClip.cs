using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace ChopChopMeow;

public sealed record ClipFrame(Texture2D Texture, Vector2 Foot);

public sealed class ClipData
{
    [JsonPropertyName("texture")] public string Texture { get; set; } = "";
    [JsonPropertyName("body_height")] public float BodyHeight { get; set; }
    [JsonPropertyName("frames")] public List<ClipFrameData> Frames { get; set; } = new();
}
public sealed class ClipFrameData
{
    [JsonPropertyName("region")] public int[] Region { get; set; } = Array.Empty<int>();
    [JsonPropertyName("foot")] public float[] Foot { get; set; } = Array.Empty<float>();
}

/// <summary>References offline-baked atlas regions and explicit pivots; never analyzes pixels.</summary>
public sealed class ActorClip
{
    public const int Directions = 4;
    public int FrameCount { get; }
    private readonly ClipFrame[,] frames;
    public float BodyHeight { get; }
    private ActorClip(float bodyHeight, int frameCount)
    {
        BodyHeight = bodyHeight; FrameCount = frameCount; frames = new ClipFrame[Directions, frameCount];
    }

    public ClipFrame Get(int direction, int frame)
    {
        if ((uint)direction >= Directions) throw new ArgumentOutOfRangeException(nameof(direction));
        if ((uint)frame >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        return frames[direction, frame];
    }

    public static ActorClip Load(string name, ClipData data)
    {
        if (!float.IsFinite(data.BodyHeight) || data.BodyHeight <= 0 || data.Frames.Count < Directions * 2 || data.Frames.Count > Directions * 64 || data.Frames.Count % Directions != 0)
            throw new InvalidOperationException($"Invalid frame count or body height for animation '{name}'.");
        var texture = GD.Load<Texture2D>(data.Texture) ?? throw new InvalidOperationException($"Missing texture for animation '{name}'.");
        var clip = new ActorClip(data.BodyHeight, data.Frames.Count / Directions);
        for (int index = 0; index < data.Frames.Count; index++)
        {
            var frame = data.Frames[index]; var r = frame.Region; var foot = frame.Foot;
            if (r.Length != 4 || foot.Length != 2 || r[0] < 0 || r[1] < 0 || r[2] <= 0 || r[3] <= 0 ||
                r[0] + r[2] > texture.GetWidth() || r[1] + r[3] > texture.GetHeight() ||
                !float.IsFinite(foot[0]) || !float.IsFinite(foot[1]) || foot[0] < 0 || foot[0] > r[2] || foot[1] < 0 || foot[1] > r[3])
                throw new InvalidOperationException($"Invalid region or foot pivot for animation '{name}', frame {index}.");
            var region = new AtlasTexture { Atlas = texture, Region = new Rect2(r[0], r[1], r[2], r[3]), FilterClip = true };
            clip.frames[index % Directions, index / Directions] = new ClipFrame(region, new Vector2(foot[0], foot[1]));
        }
        return clip;
    }
}
