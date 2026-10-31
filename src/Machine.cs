using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A building's item cups and its station's process row; each cup holds a single stack and shows a faint picture of what goes in it while empty.</summary>
public sealed class Cups(UiKit kit, ItemMoves moves, LiveUpdates live, Thing t) {
    private readonly World world = kit.World;

    /// <summary>A cup bound to one item of an inventory: a click or drag takes it, and <paramref name="tooltip"/> words the amount held.</summary>
    /// <param name="look">The cup's rim, for one other than a plain input's or an output's.</param>
    public ItemSlot Cup(Inventory box, string item, bool input, Func<int, string> tooltip, float size = 52, StyleBoxFlat? look = null) {
        var (cell, count) = ItemSlot.Cup($"{(input ? "input" : "output")}_{item}", size);
        cell.Icon = Icons.Of(item);
        if ((look ?? (input ? null : Style.CupWanted)) is StyleBoxFlat rim) cell.AddThemeStyleboxOverride("normal", rim);
        cell.Pressed += () => { kit.Act(() => moves.Stack(box, world.S.Backpack, item, t)); kit.Refresh(); };
        cell.Drag = () => box[item] > 0 ? new ItemDrag(item, box[item], box, t) : null;
        // Redrawn only when the amount held changes.
        live.Watch(() => box[item], held => {
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = tooltip(held);
        });
        return cell;
    }

    /// <summary>A cup with what one batch takes or makes under it.</summary>
    public static VBoxContainer Labelled(Control cup, string perBatch) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        column.AddChild(cup);
        if (perBatch != "") column.AddChild(Style.PerBatch(perBatch));
        return column;
    }

    /// <summary>A station's work: what one batch takes, how long it runs, what it burns and what it makes.</summary>
    public ProcessRow Machine() {
        var recipe = world.Data.Recipes[t.Recipe];
        float rate = (t.Kind == ThingKind.Bench ? world.Data.BenchSpeed : 1) * world.Speed(t.Tools != null && recipe.Tool != "" ? world.BenchTool(t, recipe.Tool, recipe.Tier) : null);
        float seconds = recipe.Seconds / Math.Max(.01f, rate);
        var inputs = world.BatchCost(t).Select(p => (Control)StationCup(p.Key, true, $"×{p.Value}"));
        // Leftovers from an earlier recipe or fuel stay reachable as side outputs.
        var side = world.Leftovers(t).Where(item => item != recipe.Output).Select(item => (Control)StationCup(item, false, "", 44));
        return new ProcessRow(live, inputs, LiveText.Of(text => PageText.AppendDuration(text, seconds)), () => t.Processing ? t.Progress / recipe.Seconds : 0,
            recipe.Fuel && t.Burner != null ? FuelSlot(seconds) : null,
            StationCup(recipe.Output, false, recipe.Amount > 1 ? $"×{recipe.Amount}" : "×1", 64), side.ToArray());
    }

    /// <summary>The fuel slot under the bar: a flame, its cup, and how long the fuel in it lasts at one per batch.</summary>
    private FuelRow FuelSlot(float batchSeconds) {
        var burner = t.Burner!;
        var (cell, count) = ItemSlot.Cup("fuel");
        cell.AddThemeStyleboxOverride("normal", Style.CupFuel);
        cell.Pressed += () => { if (burner.First() is string held) { kit.Act(() => moves.Stack(burner, world.S.Backpack, held, t)); kit.Refresh(); } };
        cell.Drag = () => burner.First() is string held ? new ItemDrag(held, burner[held], burner, t) : null;
        cell.CanTake = drag => drag.From == world.S.Backpack && World.FuelFits(t, drag.Item);
        cell.Take = drag => {
            int left = world.Near(t) ? ItemMoves.Leftover(drag, burner.First() is string held && held != drag.Item ? 0 : burner.StackOf(drag.Item) - burner[drag.Item]) : 0;
            kit.Act(() => world.LoadFuel(t, drag.Item, drag.Count)); kit.Refresh(); return left;
        };
        var lasts = Style.Text("", 14, Style.Ink);
        live.Watch(() => { string fuel = world.FuelOf(t) ?? World.BurnedFuel(t); return (fuel, burner[fuel]); }, now => {
            string fuel = now.Item1;
            cell.Icon = Icons.Of(fuel);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, now.Item2 > 0 ? Colors.White : Style.Ghost);
            count.Text = now.Item2 > 0 ? now.Item2.ToString() : "";
            cell.TooltipText = $"Fuel · {world.Data.Name(fuel)} · {now.Item2}/{burner.StackOf(fuel)}\nOne burns per batch · drag wood or coal here";
            lasts.Text = LiveText.Of(text => PageText.AppendDuration(text, now.Item2 * batchSeconds));
        });
        return new FuelRow("line_flame", Style.Ember, cell, lasts);
    }

    /// <summary>A cup for one input or output of a station, which takes a backpack stack dragged on as an input.</summary>
    public VBoxContainer StationCup(string item, bool input, string perBatch, float size = 52) {
        var box = t.Inventory;
        string hint = input ? "Click: take back · Drag a backpack stack here to load" : "Click or drag: take";
        var cell = Cup(box, item, input, held => $"{world.Data.Name(item)} · {held}/{box.StackSize}\n{hint}", size);
        if (input) {
            cell.CanTake = drag => drag.From == world.S.Backpack && (drag.Item == item || item == "water" && Water.IsBottle(drag.Item));
            cell.Take = drag => {
                int left = world.Near(t) ? ItemMoves.Leftover(drag, Math.Min(box.StackSize - box[item], box.Room(item))) : 0;
                kit.Act(() => world.LoadInput(t, drag.Item, drag.Count)); kit.Refresh(); return drag.Item == item ? left : 0;
            };
        }
        return Labelled(cell, perBatch);
    }
}
