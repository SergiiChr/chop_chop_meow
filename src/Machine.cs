using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Station view: one typed slot per input and output, each holding a single stack.</summary>
public partial class Game {
    private void Machine(Thing t) {
        var recipe = world.Data.Recipes[t.Recipe];
        var row = Row(); row.Alignment = BoxContainer.AlignmentMode.Center;
        foreach (var item in world.BatchCost(t).Keys) row.AddChild(MachineSlot(t, item, true));
        var progress = new ProgressBar { MaxValue = recipe.Seconds, ShowPercentage = false, CustomMinimumSize = new Vector2(48, 10), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        live.Add(() => progress.Value = t.Processing ? t.Progress : 0);
        // Lift the bar to the middle of the boxes, below the icons above them.
        var middle = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; middle.AddThemeConstantOverride("margin_bottom", 20);
        middle.AddChild(progress); row.AddChild(middle);
        // Leftovers from an earlier recipe or fuel choice get their own output slots so they stay reachable.
        foreach (var item in world.Leftovers(t).Append(recipe.Output).Distinct()) row.AddChild(MachineSlot(t, item, false));
        Dynamic(text => {
            text.Append("Batch: "); AppendCost(text, world.BatchCost(t));
            if (t.Processing) text.Append($"\nWorking · {t.Progress:0.0}/{recipe.Seconds:0}s");
            else text.Append("\nWaiting for ingredients / temperature");
        }, 14, Muted);
    }

    /// <summary>Switches a wood-or-coal station's fuel between batches.</summary>
    private void FuelToggle(Thing t) {
        if (t.Kind is not ("furnace" or "cooktable" or "cannery")) return;
        // A guest's switch comes back with the host's next update, so the label follows the building rather than the click.
        var fuel = Button("", () => world.SetFuel(t, t.Fuel == "wood" ? "coal" : "wood")); content.AddChild(fuel);
        var label = new LiveText(shown => fuel.Text = shown);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(fuel)) return;
            label.Clear().Append($"Fuel: {world.Data.Name(t.Fuel)} · click to switch"); label.Show();
        });
    }

    /// <summary>Bellows furnace temperature with the clear bar that steel needs.</summary>
    private void HeatGauge(Thing t) {
        Dynamic(text => {
            text.Append($"HEAT  {t.Heat:0}°  ·  ");
            if (t.Heat >= World.SteelHeat) {
                text.Append("steel progressing");
            } else {
                if (t.Processing) text.Append("burning coal · ");
                text.Append($"pump above {World.SteelHeat:0}°");
            }
        }, 15, new Color("edc17c"));
        var gauge = new HeatBar { Level = () => t.Heat, CustomMinimumSize = new Vector2(0, 22), Name = "heat" };
        // Redrawn only when the heat moves a tenth of a degree.
        int drawn = int.MinValue;
        content.AddChild(gauge);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(gauge) || (int)(t.Heat * 10) == drawn) return;
            drawn = (int)(t.Heat * 10); gauge.QueueRedraw();
        });
    }

    /// <summary>The item's icon above a box showing what the station holds.</summary>
    private VBoxContainer MachineSlot(Thing t, string item, bool input) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 3);
        column.AddChild(new TextureRect { Texture = HotbarIcon(item), CustomMinimumSize = new Vector2(36, 36), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TooltipText = world.Data.Name(item) });
        var (cell, count) = IconCell($"{(input ? "input" : "output")}_{item}");
        cell.Pressed += () => { Act(() => MoveStack(t.Inventory, world.S.Backpack, item, t)); RefreshLive(); };
        cell.Drag = () => t.Inventory[item] > 0 ? new ItemDrag(item, t.Inventory[item], t.Inventory, t) : null;
        if (input) {
            cell.CanTake = drag => drag.From == world.S.Backpack && drag.Item == item;
            cell.Take = drag => {
                int left = world.Near(t) ? Leftover(drag, Math.Min(t.Inventory.StackSize - t.Inventory[item], t.Inventory.Room(item))) : 0;
                Act(() => world.LoadInput(t, item, drag.Count)); RefreshLive(); return left;
            };
        }
        string hint = input ? "Click: take back · Drag a backpack stack here to load" : "Click or drag: collect";
        // Redrawn only when the amount held changes.
        int drawn = -1;
        live.Add(() => {
            int held = t.Inventory[item];
            if (held == drawn) return;
            drawn = held;
            cell.Icon = held > 0 ? HotbarIcon(item) : null;
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}/{t.Inventory.StackSize}\n{hint}";
        });
        column.AddChild(cell);
        return column;
    }
}

/// <summary>A 0–100° bar with a marker at the steel temperature; the fill turns warm once it clears the marker.</summary>
public partial class HeatBar : Control {
    public System.Func<float> Level { get; set; } = () => 0;

    public override void _Draw() {
        float level = Mathf.Clamp(Level() / 100, 0, 1), clear = World.SteelHeat / 100 * Size.X;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("1c2b24"));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * level, Size.Y)), Level() >= World.SteelHeat ? new Color("f0a03c") : new Color("8a5a46"));
        DrawLine(new Vector2(clear, -3), new Vector2(clear, Size.Y + 3), new Color("f2e8cf"), 3);
    }
}
