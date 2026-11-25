using Godot;

namespace ChopChopMeow;

/// <summary>Key caps and actions shown on the Controls screen.</summary>
public partial class ControlsList : PanelContainer {
    private static readonly (string Key, string Action)[] Bindings = {
        ("WASD / Arrows", "Move."),
        ("Shift", "Sprint."),
        ("Mouse wheel", "Zoom the overhead view."),
        ("LMB", "Select a resource, building, container, or worker. Put a held stack into storage, or place a building, pipe, or container."),
        ("RMB", "Pick up loose resources. On open ground, eat, drink, light a torch, or swing using the hotbar selection. On a hotbar slot, use its item."),
        ("Hold RMB", "Gather from a resource or pick up a placed crate or barrel. With a shovel selected, dig a planting hole or gather sand."),
        ("Space", "Pump a selected water pump or reinforced furnace once, walking to it if needed. Otherwise, swing or punch."),
        ("1–9 / Click slot", "Select a hotbar slot. A plan or placeable container starts placement."),
        ("Assign to hotbar", "Pick up a building plan or backpack item, then click a hotbar slot."),
        ("B", "Building plans."), ("Tab", "Backpack."), ("C", "Crafting outside placement."), ("H", "Crew."),
        ("C / V", "Turn a plan or container while placing it."),
        ("R / T", "While placing steel pipes, turn the pipe or change its shape."),
        ("Layer", "Switch between the surface and underground. Click a pipe underground to see its water and pressure."),
        ("LMB on item slot", "Pick up the entire stack or put down the held stack."),
        ("RMB on item slot", "Pick up half the stack or put down one item."),
        ("Alt-click an item", "Open the Recipe map and highlight the item."),
        ("Double LMB", "Use an item in the backpack or on the hotbar."),
        ("Double LMB on cabin", "Walk to the cabin door and enter."),
        ("Shift-click", "Move the entire stack to the other open storage, or queue 5 crafting batches."),
        ("Ctrl-click", "Move all items of this type to the other open storage, or queue 10 crafting batches."),
        ("Drag a panel", "Drag an empty spot to move the side panel or backpack."),
        ("F5 · F9", "Quick save and load."),
        ("Esc", "Cancel, close a panel, or pause.")
    };

    public ControlsList() {
        AddThemeStyleboxOverride("panel", Style.Well);
        var scroll = new ScrollContainer {
            CustomMinimumSize = new Vector2(760, 840),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(scroll);
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(rows);

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

            var lead = new HBoxContainer { CustomMinimumSize = new Vector2(142, 28) };
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
