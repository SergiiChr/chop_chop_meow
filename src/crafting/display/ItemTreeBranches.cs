using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The lines from the root item to the stations whose recipes use it.</summary>
internal partial class ItemTreeBranches : HBoxContainer {
    private Control root = null!;
    private readonly List<Control> branches = new();

    public void SetRoot(Control item) { root = item; root.ItemRectChanged += QueueRedraw; }

    public void BranchTo(Control group, Control heading) {
        branches.Add(heading);
        group.ItemRectChanged += QueueRedraw;
        heading.ItemRectChanged += QueueRedraw;
    }

    public override void _Draw() {
        if (branches.Count == 0) return;
        Vector2 origin = GlobalPosition;
        var rect = root.GetGlobalRect();
        Vector2 start = new(rect.End.X - origin.X, rect.GetCenter().Y - origin.Y);
        float spine = start.X + 16;
        DrawLine(start, new Vector2(spine, start.Y), Style.Held, 2, true);
        foreach (var branch in branches) {
            var bounds = branch.GetGlobalRect();
            float y = bounds.GetCenter().Y - origin.Y;
            float end = bounds.Position.X - origin.X - 9;
            DrawLine(new Vector2(spine, start.Y), new Vector2(spine, y), Style.Held, 2, true);
            DrawLine(new Vector2(spine, y), new Vector2(end - 5, y), Style.Held, 2, true);
            DrawColoredPolygon([new(end, y), new(end - 6, y - 3), new(end - 6, y + 3)], Style.Held);
        }
    }
}
