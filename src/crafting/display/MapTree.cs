using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>A category's item cards and the arrows between materials and products.</summary>
public partial class MapTree : Control {
    public const float Gap = 32, Pitch = MapNode.Width + Gap;
    public int Columns { get; set; }
    private static readonly Color Arrow = new("9cb5c3");
    private readonly List<(MapNode From, MapNode To)> links = new(), joins = new();

    public void Link(MapNode from, MapNode to) => links.Add((from, to));
    public void Join(MapNode from, MapNode to) => joins.Add((from, to));

    public override void _Draw() {
        DrawLinks(highlighted: false);
        DrawLinks(highlighted: true);
    }

    private void DrawLinks(bool highlighted) {
        var color = highlighted ? Style.Held : Arrow;
        float stroke = highlighted ? 2.5f : 1.5f;
        foreach (var (from, to) in joins) {
            if ((from.Chosen || to.Chosen) != highlighted) continue;
            Vector2 start = Exit(from);
            float trunk = to.Position.X - Gap / 2;
            DrawLine(start, new Vector2(trunk, start.Y), color, stroke, true);
            DrawLine(new Vector2(trunk, start.Y), new Vector2(trunk, Entry(to).Y), color, stroke, true);
        }
        foreach (var (from, to) in links) {
            if ((from.Chosen || to.Chosen) != highlighted) continue;
            Vector2 start = Exit(from), end = Entry(to);
            float trunk = to.Position.X - Gap / 2;
            if (start.Y == end.Y) DrawLine(start, end - new Vector2(6, 0), color, stroke, true);
            else {
                DrawLine(start, new Vector2(trunk, start.Y), color, stroke, true);
                DrawLine(new Vector2(trunk, start.Y), new Vector2(trunk, end.Y), color, stroke, true);
                DrawLine(new Vector2(trunk, end.Y), end - new Vector2(6, 0), color, stroke, true);
            }
            DrawColoredPolygon([end, end - new Vector2(7, 4), end - new Vector2(7, -4)], color);
        }
    }

    private static Vector2 Exit(MapNode node) => node.Position + new Vector2(MapNode.Width, MapNode.Height / 2);
    private static Vector2 Entry(MapNode node) => node.Position + new Vector2(0, MapNode.Height / 2);
}
