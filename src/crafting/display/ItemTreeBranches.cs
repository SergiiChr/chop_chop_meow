using System.Collections.Generic;
using Godot;

namespace ChopChopMeow;

/// <summary>Connects the root to each role, then that role to each recipe product.</summary>
internal partial class ItemTreeBranches : HBoxContainer {
    private Control root = null!;
    private readonly List<(Control Caption, VBoxContainer Products)> branches = new();

    public void SetRoot(Control item) { root = item; root.ItemRectChanged += QueueRedraw; }

    public void BranchTo(Control caption, VBoxContainer products) {
        branches.Add((caption, products));
        caption.ItemRectChanged += QueueRedraw;
        products.ItemRectChanged += QueueRedraw;
        foreach (Control product in products.GetChildren()) product.ItemRectChanged += QueueRedraw;
    }

    public override void _Draw() {
        if (branches.Count == 0) return;
        Vector2 origin = GlobalPosition;
        float rootX = root.GetGlobalRect().End.X - origin.X;
        float rootY = CenterY(root);
        float roleSpine = rootX + 16;
        Line(new(rootX, rootY), new(roleSpine, rootY));
        Spine(roleSpine, rootY, CenterY(branches[0].Caption), CenterY(branches[^1].Caption));
        foreach (var (caption, products) in branches) {
            var labelBounds = caption.GetGlobalRect();
            float y = CenterY(caption);
            float end = products.GlobalPosition.X - origin.X - 3;
            float productSpine = end - 13;
            Line(new(roleSpine, y), new(labelBounds.Position.X - origin.X, y));
            Line(new(labelBounds.End.X - origin.X, y), new(productSpine, y));
            var cards = products.GetChildren();
            Spine(productSpine, y, CenterY((Control)cards[0]), CenterY((Control)cards[^1]));
            foreach (Control card in cards) {
                float cardY = CenterY(card);
                Line(new(productSpine, cardY), new(end - 5, cardY));
                DrawColoredPolygon([new(end, cardY), new(end - 6, cardY - 3), new(end - 6, cardY + 3)], Style.Held);
            }
        }
    }

    private float CenterY(Control control) => control.GetGlobalRect().GetCenter().Y - GlobalPosition.Y;

    private void Spine(float x, float from, float first, float last) {
        float top = Mathf.Min(from, first);
        float bottom = Mathf.Max(from, last);
        if (top < bottom) Line(new(x, top), new(x, bottom));
    }

    private void Line(Vector2 from, Vector2 to) => DrawLine(from, to, Style.Held, 2, true);
}
