using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Station view: its recipes as round tabs, its process row, the bellows of a reinforced furnace, and the jobs it offers.</summary>
public partial class StationPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var recipes = world.Data.Recipes.Where(p => p.Value.Station == thing.Kind).Select(p => p.Key).ToList();
        if (recipes.Count > 1) RecipePicker(recipes);
        if (thing.Recipe != "") AddChild(cups.Machine());
        else AddLabel("Choose what to make.", 14, Style.Beige);
        if (thing.Kind == ThingKind.Reinforced) Bellows();
        BagGrid(id => world.LoadInput(thing, id, ItemMoves.All ? world.Backpack[id] : 1), "Click: add one · Ctrl: all of it · Drag onto a slot",
            wanted: id => thing.Recipe != "" && (world.BatchCost(thing).ContainsKey(id) || thing.Burner != null && World.FuelFits(thing, id)));
        JobButtons();
    }

    /// <summary>A station's recipes as round tabs showing their products; the one it makes is lit.</summary>
    private void RecipePicker(System.Collections.Generic.List<string> recipes) {
        var row = Row(); row.AddThemeConstantOverride("separation", 10);
        foreach (string id in recipes) {
            var r = world.Data.Recipes[id];
            var pick = kit.Button("", () => { world.SetRecipe(thing, id); Redraw(); }); pick.Name = $"recipe_{id}";
            pick.Icon = Icons.Of(r.Output); pick.ExpandIcon = true; pick.AddThemeConstantOverride("icon_max_width", 30); pick.CustomMinimumSize = new Vector2(48, 48);
            pick.TooltipText = r.Name;
            var look = id == thing.Recipe ? Style.TabActive : Style.TabRound;
            Style.Skin(pick, look.Normal, look.Hover);
            row.AddChild(pick);
        }
    }

    /// <summary>The reinforced furnace's heat dial in degrees Celsius, with the bellows' pump and its key.</summary>
    private void Bellows() {
        var row = Row(); row.AddThemeConstantOverride("separation", 18);
        var dial = new HeatDial { Level = () => thing.Heat, CustomMinimumSize = new Vector2(132, 132), Name = "heat", TooltipText = $"Steel requires {World.Celsius(World.SteelHeat)}+ °C" };
        row.AddChild(dial);
        var column = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; column.AddThemeConstantOverride("separation", 8); row.AddChild(column);
        var degrees = Style.Heading("", 30); column.AddChild(degrees);
        column.AddChild(Style.Text($"Steel requires {World.Celsius(World.SteelHeat)}+ °C", 14, Style.Beige));
        var pumpRow = new HBoxContainer(); pumpRow.AddThemeConstantOverride("separation", 8); column.AddChild(pumpRow);
        var pump = kit.Button("Pump", () => { if (kit.Paused) kit.Notify("Resume before operating the bellows."); else world.Pump(thing); });
        pump.Name = "pump"; pump.TooltipText = "Pump the bellows · Space"; pump.CustomMinimumSize = new Vector2(84, 38);
        var look = Style.Pill(Style.Sage); Style.Skin(pump, look.Item1, look.Item2);
        pumpRow.AddChild(pump); pumpRow.AddChild(Style.Pill("Space", Style.KeyPill, Style.Line, 12));
        // Redrawn only when the shown degrees change.
        live.Watch(() => World.Celsius(thing.Heat), celsius => { dial.QueueRedraw(); degrees.Text = $"{celsius} °C"; });
    }
}
