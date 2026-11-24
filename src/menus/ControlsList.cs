using Godot;

namespace ChopChopMeow;

/// <summary>Key caps and actions shown on the Controls screen.</summary>
public partial class ControlsList : PanelContainer {
    private static readonly (string Key, string Action)[] Bindings = {
        ("WASD", "Move."),
        ("Shift", "Sprint."),
        ("RMB", "Hold to collect resources or pick up items. Click open ground or a hotbar slot to use an item."),
        ("LMB", "Select item."),
        ("Space", "Use the selected building or item. Swing an axe or pump water."),
        ("1–9", "Select a hotbar slot. Pick up a building plan or backpack item and put it on the hotbar."),
        ("B", "Building plans."), ("Tab", "Backpack."), ("C", "Crafting."), ("H", "Worker management."),
        ("Double LMB", "Use an item in the backpack or on the hotbar."),
        ("Shift-click", "Move the entire stack or queue 5 crafting batches."),
        ("Ctrl-click", "Move all items of this type or queue 10 crafting batches."),
        ("F5 · F9", "Quick save and load."),
        ("Esc", "Cancel, close a panel, or pause.")
    };

    public ControlsList() {
        CustomMinimumSize = new Vector2(720, 0);
        AddThemeStyleboxOverride("panel", Style.Well);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 5);
        AddChild(rows);

        for (int i = 0; i < Bindings.Length; i++) {
            if (i > 0) rows.AddChild(new ColorRect {
                Color = new Color(Style.Line, .45f),
                CustomMinimumSize = new Vector2(0, 2),
                MouseFilter = MouseFilterEnum.Ignore
            });
            var (key, action) = Bindings[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            rows.AddChild(row);

            var lead = new HBoxContainer { CustomMinimumSize = new Vector2(104, 28) };
            var cap = Style.Pill(key, Style.KeyPill, Style.Line, 13);
            cap.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            lead.AddChild(cap);
            row.AddChild(lead);

            var text = Style.Text(action, 15, Style.Ink, wrap: true);
            text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(text);
        }
    }
}
