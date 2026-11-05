using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A station's page: its tool cups in the header, the recipe board, the work of its current recipe, what waits in its queue,
/// the bellows of a reinforced furnace, and the jobs it offers.
/// </summary>
public partial class StationPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    // The board opens on the tab of what the station makes when the page opens, and keeps the tab the player picks after that.
    private bool opened;

    protected override void Body() {
        // A station's own tool cups sit in the header: what is slotted sets which recipes can run and how fast.
        var slotted = World.SlotKinds(thing.Kind);
        foreach (string tool in slotted) panel.AddToHeader(ToolSlot(tool));
        if (thing.Kind == ThingKind.Bench) headingIcon.TooltipText = $"Works {world.Data.BenchSpeed:0.#}× as fast as by hand, at the speed of the slotted tool. Someone has to work it.";
        var recipes = world.Data.Recipes.Where(p => p.Value.Station == thing.Kind).Select(p => p.Key);
        string? openOn = opened ? null : thing.Keep != "" ? thing.Keep : thing.Recipe != "" ? thing.Recipe : null;
        opened = true;
        AddChild(new RecipeBoard(kit, live, memory, Redraw, recipes, group => group.Tool == "" ? null : world.BenchTool(thing, group.Tool, group.Tier), thing, openOn));
        RedrawOnChange(() => CraftPanel.QueueCode(new(thing.Queue)));
        if (thing.Recipe != "") AddChild(cups.Machine());
        else AddLabel("Click a recipe for workers to keep making it, or Shift-click to queue some.", 14, Style.Beige);
        AddChild(new QueueStrip(kit, live, thing.Queue, id => World.Waiting(new(thing.Queue), id), (id, count) => world.QueueRecipe(thing, id, -count)));
        if (thing.Kind == ThingKind.Reinforced) Bellows();
        // A tool whose cup is empty goes into it.
        bool FreeToolCup(string id) => slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(thing, world.Data.Items[id].Tool) == null;
        BagGrid(id => {
            if (FreeToolCup(id)) world.SlotTool(thing, id);
            else world.LoadInput(thing, id, ItemMoves.Amount(world.S.Backpack, id));
        }, slotted.Length > 0 ? "Click: slot a tool, or add one · Shift: a stack · Ctrl: all of it · Drag onto a slot" : "Click: add one · Shift: a stack · Ctrl: all of it · Drag onto a slot",
            wanted: id => world.Takes(thing, id) || FreeToolCup(id));
        JobButtons();
    }

    /// <summary>A station's cup for one tool kind; while empty it shows a faint picture of the tool it wants.</summary>
    private ItemSlot ToolSlot(string tool) {
        var (cell, _) = ItemSlot.Cup($"tool_{tool}");
        cell.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        string plainest = world.Data.PlainestTool(tool);
        cell.Pressed += () => { if (world.BenchTool(thing, tool) is string held) kit.Run(() => world.UnslotTool(thing, held)); };
        cell.Drag = () => world.BenchTool(thing, tool) is string held ? new ItemDrag(held, 1, thing.Tools, thing) : null;
        cell.CanTake = drag => drag.From == world.S.Backpack && world.Data.Items.GetValueOrDefault(drag.Item)?.Tool == tool;
        cell.Take = drag => { kit.Run(() => world.SlotTool(thing, drag.Item)); return 0; };
        live.Watch(() => world.BenchTool(thing, tool), held => {
            cell.Icon = Icons.Of(held ?? plainest);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held != null ? Colors.White : Style.Ghost);
            cell.TooltipText = held != null ? $"{world.Data.Name(held)} · {world.Data.Items[held].Speed:0.#}× speed\nClick: take it back" : $"Empty {tool} slot\nDrag a {tool} here from your backpack";
        });
        return cell;
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
        Style.SkinPill(pump, Style.Sage);
        pumpRow.AddChild(pump); pumpRow.AddChild(Style.Pill("Space", Style.KeyPill, Style.Line, 12));
        // Redrawn only when the shown degrees change.
        live.Watch(() => World.Celsius(thing.Heat), celsius => { dial.QueueRedraw(); degrees.Text = $"{celsius} °C"; });
    }
}
