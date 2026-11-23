using System;
using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Searches walkable tiles with reusable buffers, checking solid footprints in the cells a step crosses.</summary>
internal sealed class WalkSearch(World world) {
    internal enum Result { Searching, Found, Blocked }

    internal const float WaypointReach = .06f;
    private const float CellWidth = 4;
    private static readonly Vector2I[] Steps = {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    private readonly record struct Obstacle(Vector2 Center, float Radius);
    private readonly Queue<Vector2I> queue = new();
    private readonly Dictionary<Vector2I, List<Obstacle>> obstacles = new();
    private readonly Stack<List<Obstacle>> spare = new();
    private readonly List<(int Id, Vector2 Center)> layout = new();
    private int[] seen = [], previous = [];
    private int search;
    private Vector2 from, destination;
    private Vector2I start;
    private bool exact, wholeWalk, near;

    /// <summary>Starts a route search, or takes a clear straight leg when the whole walk is needed.</summary>
    public Result Begin(Vector2 from, Vector2 destination, List<Vector2> route, bool exact, bool wholeWalk = false) {
        this.from = from;
        this.destination = destination;
        this.exact = exact;
        this.wholeWalk = wholeWalk;
        ReadObstacles(rememberLayout: wholeWalk);
        if (exact && !Open(destination)) return Result.Blocked;
        bool canStart = Open(from);
        if (wholeWalk) {
            if (!canStart) return Result.Blocked;
            if (OpenSegment(from, destination)) { route.Add(destination); return Result.Found; }
        }
        start = Terrain.TileAt(from);
        if (!Terrain.OnMap(start)) return Result.Blocked;
        if (seen.Length == 0) { seen = new int[Terrain.Size * Terrain.Size]; previous = new int[seen.Length]; }
        search++;
        queue.Clear();
        near = from.DistanceTo(destination) < World.WalkReach;
        // An exact walk can meet the grid at any adjacent tile it has a clear step to.
        if (exact && canStart) {
            if (OpenSegment(from, start)) Seed(start);
            foreach (var step in Steps) {
                var tile = start + step;
                if (Terrain.OnMap(tile) && OpenSegment(from, tile)) Seed(tile);
            }
        } else Seed(start);
        return queue.Count == 0 ? Result.Blocked : Result.Searching;
    }

    /// <summary>Visits at most <paramref name="tiles"/> more tiles and appends the waypoints when it finds a route.</summary>
    public Result Continue(List<Vector2> route, int tiles) {
        float endRange = exact ? 1.5f : 1.15f;
        while (tiles-- > 0 && queue.TryDequeue(out var current)) {
            if ((near || exact || current != start) && ((Vector2)current).DistanceSquaredTo(destination) <= endRange * endRange
                && (!exact || OpenSegment(current, destination))) {
                Finish(Terrain.TileIndex(current), route);
                return Result.Found;
            }
            bool canLeave = !exact || Open(current);
            foreach (var step in Steps) {
                var next = current + step;
                if (!Terrain.OnMap(next)) continue;
                int index = Terrain.TileIndex(next);
                if (seen[index] == search || !Open(next)) continue;
                if (exact) {
                    if (canLeave && !OpenSegment(current, next)) continue;
                } else if (step.X != 0 && step.Y != 0
                    && (!Open(current + new Vector2I(step.X, 0)) || !Open(current + new Vector2I(0, step.Y)))) continue;
                seen[index] = search;
                previous[index] = Terrain.TileIndex(current);
                queue.Enqueue(next);
            }
        }
        return queue.Count == 0 ? Result.Blocked : Result.Searching;
    }

    private void Seed(Vector2I tile) {
        int index = Terrain.TileIndex(tile);
        seen[index] = search;
        previous[index] = -1;
        queue.Enqueue(tile);
    }

    private void Finish(int node, List<Vector2> route) {
        int first = route.Count;
        for (; previous[node] >= 0; node = previous[node]) route.Add(new Vector2(node % Terrain.Size, node / Terrain.Size));
        var firstTile = new Vector2(node % Terrain.Size, node / Terrain.Size);
        if (exact && Open(firstTile) && from.DistanceSquaredTo(firstTile) > WaypointReach * WaypointReach) route.Add(firstTile);
        route.Reverse(first, route.Count - first);
        if (wholeWalk) route.Add(destination);
    }

    private static Vector2I Cell(Vector2 point) => new(Mathf.FloorToInt(point.X / CellWidth), Mathf.FloorToInt(point.Y / CellWidth));

    /// <summary>Whether the solid things have changed since the cabin began its walk search.</summary>
    public bool LayoutChanged() {
        int i = 0;
        foreach (var kind in World.WalkBlockers)
            foreach (var thing in world.ThingsOf(kind)) {
                if (i >= layout.Count || layout[i++] != (thing.Id, thing.Pos)) return true;
            }
        return i != layout.Count;
    }

    private void ReadObstacles(bool rememberLayout) {
        layout.Clear();
        foreach (var list in obstacles.Values) { list.Clear(); spare.Push(list); }
        obstacles.Clear();
        foreach (var kind in World.WalkBlockers) {
            float radius = World.Footprint(kind);
            foreach (var thing in world.ThingsOf(kind)) {
                var center = thing.Pos;
                if (rememberLayout) layout.Add((thing.Id, center));
                var min = Cell(center - Vector2.One * radius);
                var max = Cell(center + Vector2.One * radius);
                for (int y = min.Y; y <= max.Y; y++)
                    for (int x = min.X; x <= max.X; x++) {
                        var cell = new Vector2I(x, y);
                        if (!obstacles.TryGetValue(cell, out var list)) obstacles.Add(cell, list = spare.Count > 0 ? spare.Pop() : []);
                        list.Add(new Obstacle(center, radius));
                    }
            }
        }
    }

    private bool Open(Vector2 at) {
        if (!World.Open(at)) return false;
        if (obstacles.TryGetValue(Cell(at), out var list))
            foreach (var obstacle in list) if (at.DistanceTo(obstacle.Center) < obstacle.Radius) return false;
        return true;
    }

    private bool OpenSegment(Vector2 from, Vector2 to) {
        if (!World.Open(from) || !World.Open(to)) return false;
        var segment = to - from;
        float length = segment.Length();
        if (length == 0) return Open(to);
        var heading = segment / length;
        var min = Cell(new Vector2(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y)));
        var max = Cell(new Vector2(Math.Max(from.X, to.X), Math.Max(from.Y, to.Y)));
        for (int y = min.Y; y <= max.Y; y++)
            for (int x = min.X; x <= max.X; x++) {
                if (!obstacles.TryGetValue(new Vector2I(x, y), out var list)) continue;
                foreach (var obstacle in list) {
                    float along = Math.Clamp((obstacle.Center - from).Dot(heading), 0, length);
                    if ((from + heading * along).DistanceTo(obstacle.Center) < obstacle.Radius) return false;
                }
            }
        for (float d = .1f; d < length; d += .1f) if (!World.Open(from + heading * d)) return false;
        return true;
    }
}
