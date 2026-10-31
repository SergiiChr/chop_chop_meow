using Godot;

namespace ChopChopMeow;

/// <summary>Stages on a line: done in sage, the current one in mustard, the rest dark.</summary>
public partial class StageTrack(PanelContainer[] nodes) : HBoxContainer {
    private readonly ColorRect[] links = new ColorRect[nodes.Length - 1];

    public override void _Ready() {
        AddThemeConstantOverride("separation", 0);
        for (int i = 0; i < nodes.Length; i++) {
            if (i > 0) { links[i - 1] = new ColorRect { CustomMinimumSize = new Vector2(0, 6), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter }; AddChild(links[i - 1]); }
            AddChild(nodes[i]);
        }
    }

    /// <summary>Marks the stage reached.</summary>
    public void At(int stage) {
        for (int i = 0; i < nodes.Length; i++) nodes[i].AddThemeStyleboxOverride("panel", i < stage ? Style.StageDone : i == stage ? Style.StageNow : Style.StageTodo);
        for (int i = 0; i < links.Length; i++) links[i].Color = i < stage ? Style.Sage : Style.Line;
    }
}
