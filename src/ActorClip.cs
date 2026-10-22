using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A tightly cropped actor frame and its bottom-feet anchor in texture pixels.</summary>
public sealed record ClipFrame(Texture2D Texture, Vector2 Foot);

/// <summary>Runtime importer for 4-direction, 8-frame actor atlases with transparent gutters.</summary>
public sealed class ActorClip
{
    public const int Directions = 4;
    public const int FrameCount = 8;
    private const int Padding = 2;
    private const byte AlphaThreshold = 51;
    private readonly ClipFrame[,] frames = new ClipFrame[Directions, FrameCount];

    public float BodyHeight { get; }

    private ActorClip(float bodyHeight) => BodyHeight = bodyHeight;

    public ClipFrame Get(int direction, int frame)
    {
        if ((uint)direction >= Directions) throw new ArgumentOutOfRangeException(nameof(direction));
        if ((uint)frame >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        return frames[direction, frame];
    }

    /// <summary>Count distinct pixel images in one direction, for asset validation.</summary>
    public int DistinctFrameCount(int direction)
    {
        if ((uint)direction >= Directions) throw new ArgumentOutOfRangeException(nameof(direction));
        var hashes = new HashSet<ulong>();
        for (int frame = 0; frame < FrameCount; frame++) hashes.Add(Hash(frames[direction, frame].Texture.GetImage()));
        return hashes.Count;
    }

    public static ActorClip Load(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) throw new ArgumentException("Actor atlas resource path is empty.", nameof(resourcePath));
        var source = GD.Load<Texture2D>(resourcePath);
        if (source == null) throw new InvalidOperationException($"Could not load actor atlas '{resourcePath}'.");
        Image image = source.GetImage();
        if (image == null || image.IsEmpty()) throw new InvalidOperationException($"Actor atlas '{resourcePath}' has no readable image data.");
        image.Convert(Image.Format.Rgba8);

        int width = image.GetWidth();
        int height = image.GetHeight();
        if (width < Directions || height < FrameCount)
            throw new InvalidOperationException($"Actor atlas '{resourcePath}' is too small for a {Directions}x{FrameCount} grid ({width}x{height}).");

        byte[] pixels = image.GetData();
        var labels = new int[width * height];
        var components = new List<Component>();
        var queue = new Queue<int>();
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int[] rowCuts = FindRowCuts(pixels, width, height);

        // Label opaque components independently inside each row. A low-alpha scanline near each
        // nominal boundary keeps adjacent frames separate even when their feet or tools touch.
        for (int row = 0; row < FrameCount; row++)
            for (int y = rowCuts[row]; y < rowCuts[row + 1]; y++)
                for (int x = 0; x < width; x++)
                {
                    int start = y * width + x;
                    int alphaOffset = start * 4 + 3;
                    if (labels[start] != 0 || pixels[alphaOffset] <= AlphaThreshold) continue;
                    int id = components.Count + 1;
                    var component = new Component(id, row, x, y);
                    labels[start] = id;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int at = queue.Dequeue();
                        int sx = at % width;
                        int sy = at / width;
                        byte alpha = pixels[at * 4 + 3];
                        component.Add(sx, sy, alpha);
                        for (int n = 0; n < dx.Length; n++)
                        {
                            int nx = sx + dx[n], ny = sy + dy[n];
                            if ((uint)nx >= (uint)width || ny < rowCuts[row] || ny >= rowCuts[row + 1]) continue;
                            int next = ny * width + nx;
                            if (labels[next] != 0 || pixels[next * 4 + 3] <= AlphaThreshold) continue;
                            labels[next] = id;
                            queue.Enqueue(next);
                        }
                    }
                    components.Add(component);
                }

        int[] cellWidths = Enumerable.Range(0, Directions).Select(c => (c + 1) * width / Directions - c * width / Directions).ToArray();
        int[] cellHeights = Enumerable.Range(0, FrameCount).Select(r => (r + 1) * height / FrameCount - r * height / FrameCount).ToArray();
        var selected = new Component?[Directions, FrameCount];
        foreach (Component component in components)
        {
            int col = Math.Clamp((int)(component.CentroidX / width * Directions), 0, Directions - 1);
            int row = component.Row;
            int cellWidth = cellWidths[col], cellHeight = cellHeights[row];
            if (component.Width < cellWidth * .25f || component.Height < cellHeight * .25f ||
                component.Width > cellWidth * 1.6f || component.Height > cellHeight * 1.6f ||
                component.Area < cellWidth * cellHeight * .025f) continue;
            if (selected[col, row] == null || component.Area > selected[col, row]!.Area)
                selected[col, row] = component;
        }

        for (int direction = 0; direction < Directions; direction++)
            for (int frame = 0; frame < FrameCount; frame++)
            {
                if (selected[direction, frame] == null)
                    throw new InvalidOperationException($"Actor atlas '{resourcePath}' is missing a valid body component at direction {direction}, frame {frame}.");
            }

        float[] neutralHeights = Enumerable.Range(0, Directions)
            .Select(d => FootAnchor(width, pixels, labels, selected[d, 0]!).Y - Padding)
            .ToArray();
        float bodyHeight = Median(neutralHeights);
        var clip = new ActorClip(bodyHeight);
        for (int direction = 0; direction < Directions; direction++)
            for (int frame = 0; frame < FrameCount; frame++)
                clip.frames[direction, frame] = MakeFrame(resourcePath, direction, frame, width, height, pixels, labels, selected[direction, frame]!);
        return clip;
    }

    private static int[] FindRowCuts(byte[] pixels, int width, int height)
    {
        var projection = new int[height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (pixels[(y * width + x) * 4 + 3] > AlphaThreshold) projection[y]++;

        var cuts = new int[FrameCount + 1];
        cuts[0] = 0;
        cuts[FrameCount] = height;
        for (int row = 1; row < FrameCount; row++)
        {
            int nominal = row * height / FrameCount;
            int radius = Math.Max(1, (int)Math.Floor(height / (float)FrameCount * .2f));
            int low = Math.Max(cuts[row - 1] + 1, nominal - radius);
            int high = Math.Min(height - (FrameCount - row), nominal + radius);
            int best = nominal;
            for (int candidate = low; candidate <= high; candidate++)
                if (projection[candidate] < projection[best] ||
                    (projection[candidate] == projection[best] && Math.Abs(candidate - nominal) < Math.Abs(best - nominal)))
                    best = candidate;
            cuts[row] = best;
        }
        return cuts;
    }

    private static ClipFrame MakeFrame(string resourcePath, int direction, int frame, int imageWidth, int imageHeight, byte[] pixels, int[] labels, Component component)
    {
        int cropWidth = component.Width + Padding * 2;
        int cropHeight = component.Height + Padding * 2;
        var output = new byte[cropWidth * cropHeight * 4];
        for (int y = component.MinY; y <= component.MaxY; y++)
            for (int x = component.MinX; x <= component.MaxX; x++)
            {
                int sourceIndex = y * imageWidth + x;
                if (labels[sourceIndex] != component.Id) continue;
                int sourceOffset = sourceIndex * 4;
                int destOffset = ((y - component.MinY + Padding) * cropWidth + x - component.MinX + Padding) * 4;
                output[destOffset] = pixels[sourceOffset];
                output[destOffset + 1] = pixels[sourceOffset + 1];
                output[destOffset + 2] = pixels[sourceOffset + 2];
                output[destOffset + 3] = pixels[sourceOffset + 3];
            }

        Vector2 foot = FootAnchor(imageWidth, pixels, labels, component);
        Image cropped = Image.CreateFromData(cropWidth, cropHeight, false, Image.Format.Rgba8, output);
        var texture = ImageTexture.CreateFromImage(cropped);
        if (texture == null) throw new InvalidOperationException($"Could not create actor frame from '{resourcePath}' at direction {direction}, frame {frame}.");
        return new ClipFrame(texture, foot);
    }

    private static Vector2 FootAnchor(int imageWidth, byte[] pixels, int[] labels, Component component)
    {
        int lowerStartY = component.MinY + (int)Math.Floor(component.Height * .65f);
        int bootMaxY = -1;
        for (int y = lowerStartY; y <= component.MaxY; y++)
            for (int x = component.MinX; x <= component.MaxX; x++)
            {
                int index = y * imageWidth + x;
                if (labels[index] != component.Id || !IsBootPixel(pixels, index * 4)) continue;
                bootMaxY = Math.Max(bootMaxY, y);
            }

        if (bootMaxY >= 0)
        {
            int anchorStartY = bootMaxY - Math.Max(1, (int)Math.Ceiling(component.Height * .08f));
            double xSum = 0, weightSum = 0;
            int count = 0;
            for (int y = Math.Max(lowerStartY, anchorStartY); y <= bootMaxY; y++)
                for (int x = component.MinX; x <= component.MaxX; x++)
                {
                    int index = y * imageWidth + x;
                    int offset = index * 4;
                    if (labels[index] != component.Id || !IsBootPixel(pixels, offset)) continue;
                    double weight = pixels[offset + 3];
                    xSum += (x - component.MinX + Padding + .5) * weight;
                    weightSum += weight;
                    count++;
                }
            if (count > 10 && weightSum > 0)
                return new Vector2((float)(xSum / weightSum), bootMaxY - component.MinY + Padding + 1);
        }

        return new Vector2(component.Width / 2f + Padding, component.Height - 1 + Padding + .5f);
    }

    private static bool IsBootPixel(byte[] pixels, int offset)
    {
        int red = pixels[offset], green = pixels[offset + 1], blue = pixels[offset + 2];
        return pixels[offset + 3] > AlphaThreshold && red > 28 && red > green * 1.15f && green > blue * 1.08f;
    }

    private static float Median(float[] values)
    {
        Array.Sort(values);
        int middle = values.Length / 2;
        return (values[middle - 1] + values[middle]) / 2f;
    }

    private static ulong Hash(Image image)
    {
        image.Convert(Image.Format.Rgba8);
        byte[] bytes = image.GetData();
        ulong hash = 14695981039346656037UL;
        foreach (byte value in bytes) { hash ^= value; hash *= 1099511628211UL; }
        hash ^= (uint)image.GetWidth(); hash *= 1099511628211UL;
        hash ^= (uint)image.GetHeight(); hash *= 1099511628211UL;
        return hash;
    }

    private sealed class Component
    {
        public int Id { get; }
        public int Row { get; }
        public int MinX { get; private set; }
        public int MinY { get; private set; }
        public int MaxX { get; private set; }
        public int MaxY { get; private set; }
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
        public long Area { get; private set; }
        private double weightSum;
        private double xSum;
        private double ySum;
        public double CentroidX => xSum / weightSum;
        public double CentroidY => ySum / weightSum;

        public Component(int id, int row, int x, int y) { Id = id; Row = row; MinX = MaxX = x; MinY = MaxY = y; }

        public void Add(int x, int y, byte alpha)
        {
            MinX = Math.Min(MinX, x); MinY = Math.Min(MinY, y);
            MaxX = Math.Max(MaxX, x); MaxY = Math.Max(MaxY, y);
            Area++;
            weightSum += alpha;
            xSum += x * (double)alpha;
            ySum += y * (double)alpha;
        }
    }
}
