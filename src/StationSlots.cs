using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A building's item slots and its station's process row; each slot holds a single stack and shows a faint picture of what goes in it while empty.</summary>
public sealed class StationSlots(UiKit kit, ItemMoves moves, LiveUpdates live, Thing t) {
    private readonly World world = kit.World;

    /// <summary>
    /// A slot bound to one item of an inventory: a click picks it up, and <paramref name="tooltip"/> words the amount held.
    /// A held stack put on it goes in when <paramref name="takes"/> allows, by the rules of the inventory; with none the slot takes nothing and shakes.
    /// </summary>
    /// <param name="look">The slot's rim, for one other than a plain input's or an output's.</param>
    public ItemSlot Slot(Inventory box, string item, bool input, Func<int, string> tooltip, float size = 52, StyleBoxFlat? look = null, Func<HeldCursor, bool>? takes = null) {
        var (cell, count) = ItemSlot.Create($"{(input ? "input" : "output")}_{item}", size);
        cell.Icon = Icons.Of(item);
        if ((look ?? (input ? null : Style.SlotWanted)) is StyleBoxFlat rim) cell.AddThemeStyleboxOverride("normal", rim);
        moves.TakeCell(cell, box, item, t, takes, takes == null ? null : (_, units) => kit.Run(() => moves.PutDown(box, -1, units, t)));
        // Redrawn only when the amount held changes.
        live.Watch(() => box[item], held => {
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = tooltip(held);
        });
        return cell;
    }

    /// <summary>A slot with what one batch takes or makes under it.</summary>
    public static VBoxContainer Labelled(Control slot, string perBatch) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        column.AddChild(slot);
        if (perBatch != "") column.AddChild(Style.PerBatch(perBatch));
        return column;
    }

    /// <summary>A station's work: what one batch takes, how long it runs, what it burns and what it makes.</summary>
    public ProcessRow Machine() {
        var recipe = world.Data.Recipes[world.Selected(t)];
        float rate = world.WorkRate(t.Kind, world.ToolInUse(t, recipe.Tool, recipe.Tier));
        float seconds = recipe.Seconds / Math.Max(.01f, rate);
        var inputs = recipe.Inputs.Select(p => (Control)StationSlot(p.Key, true, $"×{p.Value}"));
        // Leftovers and inputs loaded for the kept recipe stay reachable as side outputs.
        var side = t.Inventory.Items.Keys.Where(item => Leftover(recipe, item)).Select(item => (Control)StationSlot(item, false, "", 44));
        return new ProcessRow(live, inputs, LiveText.Of(text => PageText.AppendDuration(text, seconds)), () => t.Processing ? t.Progress / recipe.Seconds : 0,
            recipe.Burns && t.Burner != null ? FuelSlot(recipe, seconds) : null,
            StationSlot(recipe.Output, false, recipe.Amount > 1 ? $"×{recipe.Amount}" : "×1", 64), side.ToArray());
    }

    /// <summary>Whether an item a station holds is neither an input nor the product of a recipe.</summary>
    private static bool Leftover(RecipeDef recipe, string item) => !recipe.Inputs.ContainsKey(item) && item != recipe.Output;

    /// <summary>A number that changes with which leftovers the station holds beside its recipe's inputs and product, built without allocating.</summary>
    public int SideCode() {
        if (!world.Data.Recipes.TryGetValue(world.Selected(t), out var recipe)) return 0;
        int code = 0;
        foreach (var pair in t.Inventory.Items) if (Leftover(recipe, pair.Key)) code += pair.Key.GetHashCode();
        return code;
    }

    /// <summary>The fuel slot under the bar: a flame, its slot, how long the fuel in it lasts at one per batch, and the fuels the recipe burns.</summary>
    private FuelRow FuelSlot(RecipeDef recipe, float batchSeconds) {
        var burner = t.Burner!;
        var (cell, count) = ItemSlot.Create("fuel");
        cell.AddThemeStyleboxOverride("normal", Style.SlotFuel);
        moves.TakeCell(cell, burner, burner.First, t, held => held.Stack is { Box: 0 } stack && world.FuelFits(t, stack.Item), (_, units) => kit.Run(() => world.PutDown(burner, -1, units)));
        var lasts = Style.Text("", 14, Style.Ink);
        string burns = world.FuelWords(recipe.Fuels);
        var accepted = new HBoxContainer { Name = "burns", TooltipText = $"{recipe.Name} burns {burns}", MouseFilter = Control.MouseFilterEnum.Stop };
        accepted.AddThemeConstantOverride("separation", 2);
        foreach (string fuel in recipe.Fuels) { var icon = Style.Icon(Icons.Of(fuel), 22); icon.MouseFilter = Control.MouseFilterEnum.Pass; accepted.AddChild(icon); }
        live.Watch(() => { string fuel = World.ShownFuel(t, world.Data); return (fuel, burner[fuel]); }, now => {
            var (fuel, held) = now;
            bool burnable = recipe.Fuels.Contains(fuel);
            cell.Icon = Icons.Of(fuel);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 && burnable ? Colors.White : held > 0 ? Style.Brick : Style.Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = held > 0 && !burnable
                ? $"Fuel · {world.Data.Name(fuel)} · {held}\n{recipe.Name} cannot burn it · take it out and load {burns}"
                : $"Fuel · {(held > 0 ? world.Data.Name(fuel) : "empty")} · {held}/{burner.StackOf(fuel)}\nOne burns per batch · put {burns} here";
            lasts.Text = burnable ? LiveText.Of(text => PageText.AppendDuration(text, held * batchSeconds)) : "";
        });
        return new FuelRow("line_flame", Style.Ember, cell, lasts, accepted);
    }

    /// <summary>What a click on a slot does, for its tooltip.</summary>
    public const string SlotHint = "LMB: pick up · RMB: pick up half · Shift or Ctrl: to backpack";

    /// <summary>A slot for one input or output of a station, which takes a held backpack stack as an input.</summary>
    public VBoxContainer StationSlot(string item, bool input, string perBatch, float size = 52) {
        var box = t.Inventory;
        string hint = input ? $"{SlotHint} · put a backpack stack here to load" : SlotHint;
        var cell = Slot(box, item, input, held => $"{world.Data.Name(item)} · {held}/{box.StackSize}\n{hint}", size,
            takes: input ? held => held.Stack is { Box: 0 } stack && (stack.Item == item || item == "water" && Water.IsBottle(stack.Item)) : null);
        return Labelled(cell, perBatch);
    }
}
