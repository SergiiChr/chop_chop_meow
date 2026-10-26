using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Station view: its recipes as round tabs, the process row with one cup per input, the fuel and the outputs, and the jobs it offers.
/// Each cup holds a single stack and shows a faint picture of what goes in it while empty.
/// </summary>
public partial class Game {
    private void Station(Thing t) {
        var recipes = world.Data.Recipes.Where(p => p.Value.Station == t.Kind).Select(p => p.Key).ToList();
        if (recipes.Count > 1) RecipePicker(t, recipes);
        if (t.Recipe != "") Machine(t);
        else AddLabel("Choose what to make.", 14, Muted);
        if (t.Kind == "reinforced") Bellows(t);
        BagGrid(id => world.LoadInput(t, id, MoveAll ? world.S.Backpack[id] : world.S.Backpack.StackSize), "Click: fill its slot · Ctrl: all of it · Drag onto a slot",
            wanted: id => t.Recipe != "" && (world.BatchCost(t).ContainsKey(id) || t.Burner != null && World.FuelFits(t, id)));
        JobButtons(t);
    }

    /// <summary>A station's recipes as round tabs showing their products; the one it makes is lit.</summary>
    private void RecipePicker(Thing t, System.Collections.Generic.List<string> recipes) {
        var row = Row(); row.AddThemeConstantOverride("separation", 10);
        foreach (string id in recipes) {
            var r = world.Data.Recipes[id];
            var pick = Button("", () => { world.SetRecipe(t, id); OpenPage("inspect"); }); pick.Name = $"recipe_{id}";
            pick.Icon = HotbarIcon(r.Output); pick.ExpandIcon = true; pick.AddThemeConstantOverride("icon_max_width", 30); pick.CustomMinimumSize = new Vector2(48, 48);
            pick.TooltipText = r.Name;
            var look = id == t.Recipe ? Style.TabActive : Style.TabRound;
            Style.Skin(pick, look.Normal, look.Hover);
            row.AddChild(pick);
        }
    }

    /// <summary>A station's work: what one batch takes, how long it runs, what it burns and what it makes.</summary>
    private void Machine(Thing t) {
        var recipe = world.Data.Recipes[t.Recipe];
        float rate = (t.Kind == "bench" ? world.Data.BenchSpeed : 1) * world.Speed(t.Tools != null && recipe.Tool != "" ? world.BenchTool(t, recipe.Tool, recipe.Tier) : null);
        float seconds = recipe.Seconds / Math.Max(.01f, rate);
        var inputs = world.BatchCost(t).Select(p => (Control)MachineSlot(t, t.Inventory, p.Key, true, $"×{p.Value}"));
        // Leftovers from an earlier recipe or fuel stay reachable as side outputs.
        var side = world.Leftovers(t).Where(item => item != recipe.Output).Select(item => (Control)MachineSlot(t, t.Inventory, item, false, "", 44));
        Process(inputs, Text(text => AppendDuration(text, seconds)), () => t.Processing ? t.Progress / recipe.Seconds : 0,
            recipe.Fuel && t.Burner != null ? FuelSlot(t, seconds) : null,
            MachineSlot(t, t.Inventory, recipe.Output, false, recipe.Amount > 1 ? $"×{recipe.Amount}" : "×1", 64), side.ToArray());
    }

    /// <summary>The fuel slot under the bar: a flame, its cup, and how long the fuel in it lasts at one per batch.</summary>
    private HBoxContainer FuelSlot(Thing t, float batchSeconds) {
        var burner = t.Burner!;
        var (cell, count) = IconCell("fuel");
        cell.AddThemeStyleboxOverride("normal", Style.CupFuel);
        cell.Pressed += () => { if (burner.First() is string held) { Act(() => MoveStack(burner, world.S.Backpack, held, t)); RefreshLive(); } };
        cell.Drag = () => burner.First() is string held ? new ItemDrag(held, burner[held], burner, t) : null;
        cell.CanTake = drag => drag.From == world.S.Backpack && World.FuelFits(t, drag.Item);
        cell.Take = drag => {
            int left = world.Near(t) ? Leftover(drag, burner.First() is string held && held != drag.Item ? 0 : burner.StackOf(drag.Item) - burner[drag.Item]) : 0;
            Act(() => world.LoadFuel(t, drag.Item, drag.Count)); RefreshLive(); return left;
        };
        var lasts = Style.Text("", 14, Style.Ink);
        var lastsText = new LiveText(shown => lasts.Text = shown);
        (string? Fuel, int Held) drawn = ("", -1);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(cell)) return;
            string fuel = world.FuelOf(t) ?? World.BurnedFuel(t);
            var now = (fuel, burner[fuel]);
            if (now == drawn) return;
            drawn = now;
            cell.Icon = HotbarIcon(fuel);
            cell.AddThemeColorOverride(IconNormal, now.Item2 > 0 ? Colors.White : Ghost);
            count.Text = now.Item2 > 0 ? now.Item2.ToString() : "";
            cell.TooltipText = $"Fuel · {world.Data.Name(fuel)} · {now.Item2}/{burner.StackOf(fuel)}\nOne burns per batch · drag wood or coal here";
            AppendDuration(lastsText.Clear(), now.Item2 * batchSeconds); lastsText.Show();
        });
        return FuelRow("line_flame", Style.Ember, cell, lasts);
    }

    /// <summary>The high-temp furnace's heat dial in degrees Celsius, with the bellows' pump and its key.</summary>
    private void Bellows(Thing t) {
        var row = Row(); row.AddThemeConstantOverride("separation", 18);
        var dial = new HeatDial { Level = () => t.Heat, CustomMinimumSize = new Vector2(132, 132), Name = "heat", TooltipText = $"Steel melts at {World.Celsius(World.SteelHeat)}+ °C" };
        row.AddChild(dial);
        var column = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; column.AddThemeConstantOverride("separation", 8); row.AddChild(column);
        var degrees = Style.Heading("", 30); column.AddChild(degrees);
        column.AddChild(Style.Text($"Steel melts at {World.Celsius(World.SteelHeat)}+ °C", 14, Style.Beige));
        var pumpRow = new HBoxContainer(); pumpRow.AddThemeConstantOverride("separation", 8); column.AddChild(pumpRow);
        var pump = Button("Pump", () => { if (paused) Notify("Resume before operating the bellows."); else world.Pump(t); });
        pump.Name = "pump"; pump.TooltipText = "Pump the bellows · Space"; pump.CustomMinimumSize = new Vector2(84, 38);
        var look = Style.Pill(Style.Sage); Style.Skin(pump, look.Item1, look.Item2);
        pumpRow.AddChild(pump); pumpRow.AddChild(Style.Pill("Space", Style.KeyPill, Style.Line, 12));
        var degreesText = new LiveText(shown => degrees.Text = shown);
        // Redrawn only when the shown degrees change.
        int drawn = int.MinValue;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(dial)) return;
            int celsius = World.Celsius(t.Heat);
            if (celsius == drawn) return;
            drawn = celsius; dial.QueueRedraw();
            degreesText.Clear().Append(celsius).Append(" °C"); degreesText.Show();
        });
    }

    /// <summary>A cup for one input or output; empty, it shows a faint picture of its item, and under it what one batch takes or makes.</summary>
    private VBoxContainer MachineSlot(Thing t, Inventory box, string item, bool input, string perBatch, float size = 52) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, count) = IconCell($"{(input ? "input" : "output")}_{item}", size);
        if (!input) cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
        cell.Icon = HotbarIcon(item);
        cell.Pressed += () => { Act(() => MoveStack(box, world.S.Backpack, item, t)); RefreshLive(); };
        cell.Drag = () => box[item] > 0 ? new ItemDrag(item, box[item], box, t) : null;
        if (input) {
            cell.CanTake = drag => drag.From == world.S.Backpack && drag.Item == item;
            cell.Take = drag => {
                int left = world.Near(t) ? Leftover(drag, Math.Min(box.StackSize - box[item], box.Room(item))) : 0;
                Act(() => world.LoadInput(t, item, drag.Count)); RefreshLive(); return left;
            };
        }
        string hint = input ? "Click: take back · Drag a backpack stack here to load" : "Click or drag: take";
        // Redrawn only when the amount held changes.
        int drawn = -1;
        live.Add(() => {
            int held = box[item];
            if (held == drawn) return;
            drawn = held;
            cell.AddThemeColorOverride(IconNormal, held > 0 ? Colors.White : Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}/{box.StackSize}\n{hint}";
        });
        column.AddChild(cell);
        if (perBatch != "") column.AddChild(PerBatch(perBatch));
        return column;
    }

    private static readonly Color Ghost = new(1, 1, 1, .28f);
    private static readonly Color Unready = new(1, 1, 1, .5f);
}
