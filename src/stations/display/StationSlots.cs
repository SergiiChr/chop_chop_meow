using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A building's item slots and its station's process row; each slot holds a single stack and shows a faint picture of what goes in it while empty.</summary>
public sealed class StationSlots(UiKit kit, ItemMoves moves, LiveUpdates live, Thing t) {
    private readonly World world = kit.World;

    /// <summary>
    /// A slot bound to one item of an inventory: a click picks it up, and <paramref name="tooltip"/> words the amount held.
    /// A held stack put on it goes in when <paramref name="takes"/> allows, with <paramref name="put"/> or as storage without one; with no <paramref name="takes"/> the slot takes nothing and shakes.
    /// </summary>
    /// <param name="look">The slot's rim, for one other than a plain input's or an output's.</param>
    public ItemSlot Slot(Inventory box, string item, bool input, Func<int, string> tooltip, float size = 52, StyleBoxFlat? look = null, Func<HeldCursor, bool>? takes = null, Action<string, int>? put = null) {
        var (cell, count) = ItemSlot.Create($"{(input ? "input" : "output")}_{item}", size);
        cell.Icon = Icons.Of(item);
        if ((look ?? (input ? null : Style.SlotWanted)) is StyleBoxFlat rim) cell.AddThemeStyleboxOverride("normal", rim);
        moves.TakeCell(cell, box, item, t, takes, put);
        // Redrawn only when the amount held or the firebugs' count or time left changes.
        bool lantern = Lanterns.IsItem(item);
        var food = FoodDescription.Of(world.Data, item);
        live.Watch(() => {
            int held = box[item];
            int life = item == Firebugs.Item && held > 0 ? world.Firebugs.AverageMinutes(box) : -1;
            int bugs = lantern && held > 0 ? Lanterns.BugCount(box, item) : -1;
            return (Held: held, Life: life, Bugs: bugs);
        }, shown => {
            cell.AddThemeColorOverride(ItemSlot.IconNormal, shown.Held > 0 ? Colors.White : Style.Ghost);
            count.SetStack(shown.Held, shown.Bugs);
            string hint = tooltip(shown.Held);
            if (shown.Held > 0 && food != null) {
                int lineEnd = hint.IndexOf('\n');
                FoodDescription.SetTooltip(cell, lineEnd < 0 ? hint : hint[..lineEnd], item, food);
            } else {
                cell.Popup = null;
                cell.TooltipText = $"{hint}{PageText.FirebugLifeLine(shown.Life)}\n{RecipeMap.AltClickHint}";
            }
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
        float seconds = recipe.Seconds / Math.Max(.01f, Rate(recipe));
        var inputs = recipe.Inputs.Select(p => (Control)StationSlot(p.Key, true, PerBatch(p.Key, p.Value)));
        // Leftovers and inputs loaded for the kept recipe stay reachable as side outputs.
        var side = t.Inventory.Items.Keys.Where(item => Leftover(recipe, item)).Select(item => (Control)StationSlot(item, false, "", 44));
        return new ProcessRow(live, inputs, LiveText.Of(text => PageText.AppendDuration(text, seconds)), () => t.Processing ? t.Progress / recipe.Seconds : 0,
            recipe.Burns && t.Burner != null ? FuelSlot(recipe, seconds) : null,
            StationSlot(recipe.Output, false, PerBatch(recipe.Output, recipe.Amount), 64), side.ToArray());
    }

    private string PerBatch(string item, int amount) => world.Data.VesselFor(item) != null ? $"{amount} L" : $"×{amount}";

    /// <summary>How fast the station works a recipe with the tool the acting player would work it with.</summary>
    private float Rate(RecipeDef recipe) => world.WorkRate(t.Kind, world.ToolInUse(t, recipe));

    /// <summary>The station's rate for the recipe it is on in hundredths, which changes with the tool slotted or carried.</summary>
    public int RateCode() => world.Data.Recipes.TryGetValue(world.Selected(t), out var recipe) ? (int)MathF.Round(Rate(recipe) * 100) : 0;

    /// <summary>Whether an item a station holds is neither an input nor the product of a recipe.</summary>
    private static bool Leftover(RecipeDef recipe, string item) => !recipe.Inputs.ContainsKey(item) && !World.IsProduct(recipe.Output, item);

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
        string ShownFuel() => World.ShownFuel(t, world.Data);
        moves.TakeCell(cell, burner, burner.First, t, held => held.Stack is { Box: 0 } stack && world.FuelFits(t, stack.Item),
            (item, units) => world.LoadFuel(t, item, units), shown: ShownFuel);
        var lasts = Style.Text("", 14, Style.Ink);
        string burns = world.FuelWords(recipe.Fuels);
        var accepted = new HBoxContainer { Name = "burns", TooltipText = $"{recipe.Name} burns {burns}", MouseFilter = Control.MouseFilterEnum.Stop };
        accepted.AddThemeConstantOverride("separation", 2);
        foreach (string fuel in recipe.Fuels) { var icon = Style.Icon(Icons.Of(fuel), 22); icon.MouseFilter = Control.MouseFilterEnum.Pass; accepted.AddChild(icon); }
        live.Watch(() => { string fuel = ShownFuel(); return (fuel, burner[fuel]); }, now => {
            var (fuel, held) = now;
            bool burnable = recipe.Fuels.Contains(fuel);
            cell.Icon = Icons.Of(fuel);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 && burnable ? Colors.White : held > 0 ? Style.Brick : Style.Ghost);
            count.SetStack(held, -1);
            string hint = held > 0 && !burnable
                ? $"Fuel · {world.Data.Name(fuel)} · {held}\n{recipe.Name} cannot burn it · take it out and load {burns}"
                : $"Fuel · {(held > 0 ? world.Data.Name(fuel) : "empty")} · {held}/{burner.StackOf(fuel)}\nOne burns per batch · put {burns} here";
            cell.TooltipText = $"{hint}\n{RecipeMap.AltClickHint}";
            lasts.Text = burnable ? LiveText.Of(text => PageText.AppendDuration(text, held * batchSeconds)) : "";
        });
        return new FuelRow("line_flame", Style.Ember, cell, lasts, accepted);
    }

    /// <summary>What a click on a slot does, for its tooltip.</summary>
    public const string SlotHint = "LMB: pick up · RMB: pick up half · Shift or Ctrl: to backpack";

    /// <summary>
    /// A slot for one input or output of a station, which takes a held backpack stack as an input.
    /// The output slot of the recipe the station is on takes back what that recipe makes, and side slots take nothing.
    /// </summary>
    public VBoxContainer StationSlot(string item, bool input, string perBatch, float size = 52) {
        if (item == Lanterns.Item) return LanternSlot(input, perBatch, size);
        var box = t.Inventory;
        var vessel = world.Data.VesselFor(item);
        bool product = !input && world.Product(t) == item;
        string hint = product && vessel != null ? "Fill a backpack vessel to take this liquid"
            : product ? $"{SlotHint} · put {world.Lower(world.Data.Name(item))} from your backpack back here"
            : !input ? SlotHint : vessel != null ? $"{SlotHint} · put a filled vessel here to pour what it holds" : $"{SlotHint} · put a backpack stack here to load";
        // A liquid's input slot takes the vessels that hold some of it, at any fill.
        Func<HeldCursor, bool>? takes = input ? held => held.Stack is { Box: 0 } stack && (stack.Item == item || world.Data.VesselOf(stack.Item) != null && Vessels.LiquidIn(world.S.Backpack.Object(stack.Item)) == item)
            : product && vessel == null ? held => held.Stack is { Box: 0 } stack && stack.Item == item : null;
        // The product goes back as into storage; an input loads through the station's own rules.
        var cell = Slot(box, item, input, held => $"{world.Data.Name(item)} · {held}/{box.StackSize}{(vessel == null ? "" : " L")}\n{hint}", size, takes: takes,
            put: product ? null : (held, units) => world.LoadInput(t, held, units));
        return Labelled(cell, perBatch);
    }

    /// <summary>Shows lanterns together but takes one at a time, keeping the charge of each lantern.</summary>
    private VBoxContainer LanternSlot(bool input, string perBatch, float size) {
        var box = t.Inventory;
        var (cell, count) = ItemSlot.Create($"{(input ? "input" : "output")}_{Lanterns.Item}", size);
        var firebugs = ItemSlot.CountBadge(1, 0);
        firebugs.Name = "firebug_count";
        cell.AddChild(firebugs);
        cell.Icon = Icons.Of(Lanterns.Item);
        if (!input) cell.AddThemeStyleboxOverride("normal", Style.SlotWanted);
        moves.TakeCell(cell, box, () => box.FirstOfKind(Lanterns.Item), t,
            held => held.Stack is { Box: 0 } stack && Lanterns.IsItem(stack.Item),
            input ? (item, units) => world.LoadInput(t, item, units) : null);
        live.Watch(() => (Count: box.CountOfKind(Lanterns.Item), Bugs: box.FirstOfKind(Lanterns.Item) is string item ? Lanterns.BugCount(box, item) : -1), now => {
            cell.AddThemeColorOverride(ItemSlot.IconNormal, now.Count > 0 ? Colors.White : Style.Ghost);
            count.SetStack(now.Count, -1);
            firebugs.SetStack(0, now.Bugs);
            string name = world.Lanterns.Name(now.Bugs);
            cell.TooltipText = $"{name} · {now.Count}{world.Data.DescriptionLine(Lanterns.Item)}\n{SlotHint}";
        });
        return Labelled(cell, perBatch);
    }
}
