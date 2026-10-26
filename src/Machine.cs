using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Station view: a process line of input sockets, the batch's progress and output sockets, every station alike.
/// Each socket holds a single stack and shows a faint picture of what goes in it while empty.
/// </summary>
public partial class Game {
    private void Machine(Thing t) {
        var recipe = world.Data.Recipes[t.Recipe];
        var well = new PanelContainer(); well.AddThemeStyleboxOverride("panel", Style.Well); content.AddChild(well);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 8); well.AddChild(row);
        foreach (var (item, amount) in world.BatchCost(t)) row.AddChild(MachineSlot(t, item, true, $"×{amount}"));
        var middle = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        middle.AddThemeConstantOverride("separation", 4); row.AddChild(middle);
        var progress = new ProgressBar { MaxValue = recipe.Seconds, ShowPercentage = false, CustomMinimumSize = new Vector2(40, 14), MouseFilter = Control.MouseFilterEnum.Pass };
        middle.AddChild(progress);
        var status = Label("", 14, Style.Gold); status.HorizontalAlignment = HorizontalAlignment.Center; status.AutowrapMode = TextServer.AutowrapMode.Off; middle.AddChild(status);
        var statusText = new LiveText(shown => status.Text = shown);
        // Redrawn only when the whole seconds left or the waiting state change.
        int drawn = int.MinValue;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(progress)) return;
            progress.Value = t.Processing ? t.Progress : 0;
            int left = t.Processing ? (int)MathF.Ceiling(recipe.Seconds - t.Progress) : -1;
            if (left == drawn) return;
            drawn = left;
            if (left >= 0) statusText.Clear().Append(left).Append('s');
            else statusText.Clear().Append("waiting");
            statusText.Show();
            progress.TooltipText = left >= 0 ? "Working" : "Waiting for ingredients, fuel or heat";
        });
        // Leftovers from an earlier recipe or fuel choice get their own output slots so they stay reachable.
        foreach (var item in world.Leftovers(t).Append(recipe.Output).Distinct()) row.AddChild(MachineSlot(t, item, false, item == recipe.Output && recipe.Amount > 1 ? $"×{recipe.Amount}" : ""));
        var controls = Row();
        FuelToggle(t, controls);
        controls.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        controls.AddChild(Chip("×1", "Load one batch from your backpack", () => world.LoadBatch(t), "load_1"));
        controls.AddChild(Chip("×5", "Load five batches", () => world.LoadBatch(t, 5), "load_5"));
        var take = Chip("Take", "Collect everything made", () => world.Collect(t), "collect"); Style.Skin(take, Style.Primary, Style.PrimaryHover, Style.ButtonPressed);
        take.AddThemeColorOverride("font_color", Style.Ply); take.AddThemeColorOverride("font_hover_color", Style.Ply); controls.AddChild(take);
    }

    /// <summary>A small rounded button for a quick action, with the full meaning in its tooltip.</summary>
    private Button Chip(string text, string tooltip, Action action, string name = "") {
        var chip = Button(text, action); chip.TooltipText = tooltip; chip.CustomMinimumSize = new Vector2(46, 34);
        if (name != "") chip.Name = name;
        return chip;
    }

    /// <summary>Picks a wood-or-coal station's fuel between batches: two icon chips, the one in use lit.</summary>
    private void FuelToggle(Thing t, HBoxContainer row) {
        if (t.Kind is not ("furnace" or "cooktable" or "cannery")) return;
        var flame = Style.Icon(HotbarIcon("campfire"), 26); flame.TooltipText = "Fuel"; flame.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(flame);
        var chips = new[] { "wood", "coal" }.Select(fuel => {
            // A guest's switch comes back with the host's next update, so the lit chip follows the building rather than the click.
            var chip = Button("", () => world.SetFuel(t, fuel)); chip.Name = $"fuel_{fuel}"; chip.Icon = HotbarIcon(fuel); chip.ExpandIcon = true;
            chip.AddThemeConstantOverride("icon_max_width", 28); chip.CustomMinimumSize = new Vector2(44, 40); chip.TooltipText = $"Burn {world.Data.Name(fuel).ToLowerInvariant()}";
            row.AddChild(chip);
            return (Fuel: fuel, Chip: chip);
        }).ToArray();
        string shown = "";
        live.Add(() => {
            if (t.Fuel == shown || !GodotObject.IsInstanceValid(chips[0].Chip)) return;
            shown = t.Fuel;
            foreach (var (fuel, chip) in chips) chip.AddThemeStyleboxOverride("normal", fuel == t.Fuel ? Style.ButtonPressed : Style.ButtonNormal);
        });
    }

    /// <summary>The bellows furnace's heat dial beside a big bellows button.</summary>
    private void Bellows(Thing t) {
        var row = Row(); row.AddThemeConstantOverride("separation", 14);
        var dial = new HeatDial { Level = () => t.Heat, CustomMinimumSize = new Vector2(200, 112), Name = "heat", TooltipText = $"Steel only forms at {World.SteelHeat:0}° and above" };
        row.AddChild(dial);
        var pump = Button("PUMP", () => { if (paused) Notify("Resume before operating the bellows."); else world.Pump(t); });
        pump.Name = "pump"; pump.TooltipText = "Pump the bellows · Space"; pump.Icon = GD.Load<Texture2D>("res://assets/ui/bellows.svg"); pump.ExpandIcon = true;
        pump.AddThemeConstantOverride("icon_max_width", 64); pump.VerticalIconAlignment = VerticalAlignment.Top; pump.IconAlignment = HorizontalAlignment.Center;
        pump.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; pump.CustomMinimumSize = new Vector2(0, 104);
        var key = Style.Keycap("SPACE"); key.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight); key.GrowHorizontal = Control.GrowDirection.Begin; key.GrowVertical = Control.GrowDirection.Begin; key.OffsetRight = -8; key.OffsetBottom = -8;
        pump.AddChild(key); row.AddChild(pump);
        // Redrawn only when the heat moves a tenth of a degree.
        int drawn = int.MinValue;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(dial) || (int)(t.Heat * 10) == drawn) return;
            drawn = (int)(t.Heat * 10); dial.QueueRedraw();
        });
    }

    /// <summary>A socket for one input or output; empty, it shows a faint picture of its item, and below it what one batch takes or makes.</summary>
    private VBoxContainer MachineSlot(Thing t, string item, bool input, string perBatch) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 2);
        var (cell, count) = IconCell($"{(input ? "input" : "output")}_{item}");
        cell.CustomMinimumSize = input ? new Vector2(54, 54) : new Vector2(62, 62);
        if (!input) cell.AddThemeStyleboxOverride("normal", Style.SocketOutput);
        cell.Icon = HotbarIcon(item);
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
            cell.AddThemeColorOverride(IconNormal, held > 0 ? Colors.White : Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}/{t.Inventory.StackSize}\n{hint}";
        });
        column.AddChild(cell);
        var note = Label(perBatch, 13, Muted); note.HorizontalAlignment = HorizontalAlignment.Center; note.AutowrapMode = TextServer.AutowrapMode.Off; column.AddChild(note);
        return column;
    }

    private static readonly Color Ghost = new(1, 1, 1, .3f);
}
