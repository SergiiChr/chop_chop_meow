using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>The lines from the root item to the products of the recipes that directly use it.</summary>
internal partial class ItemTreeBranches : HBoxContainer {
    private Control root = null!;
    private readonly List<Control> branches = new();

    public void SetRoot(Control item) { root = item; root.ItemRectChanged += QueueRedraw; }

    public void BranchTo(Control product) {
        branches.Add(product);
        product.ItemRectChanged += QueueRedraw;
    }

    public override void _Draw() {
        if (branches.Count == 0) return;
        Vector2 origin = GlobalPosition;
        var rect = root.GetGlobalRect();
        Vector2 start = new(rect.End.X - origin.X, rect.GetCenter().Y - origin.Y);
        float spine = start.X + 16;
        DrawLine(start, new Vector2(spine, start.Y), Style.Held, 2, true);
        float top = Mathf.Min(start.Y, branches[0].GetGlobalRect().GetCenter().Y - origin.Y);
        float bottom = Mathf.Max(start.Y, branches[^1].GetGlobalRect().GetCenter().Y - origin.Y);
        if (top < bottom) DrawLine(new Vector2(spine, top), new Vector2(spine, bottom), Style.Held, 2, true);
        foreach (var branch in branches) {
            var bounds = branch.GetGlobalRect();
            float y = bounds.GetCenter().Y - origin.Y;
            float end = bounds.Position.X - origin.X - 3;
            DrawLine(new Vector2(spine, y), new Vector2(end - 5, y), Style.Held, 2, true);
            DrawColoredPolygon([new(end, y), new(end - 6, y - 3), new(end - 6, y + 3)], Style.Held);
        }
    }
}
