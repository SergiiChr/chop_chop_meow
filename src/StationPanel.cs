using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A station's page: the recipe board, the work of its current recipe, the bellows of a reinforced furnace
/// and the jobs it offers, with its tool cups in a window of their own to the left.
/// </summary>
public partial class StationPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    // The board opens the group of what the station makes when the page opens, and keeps the groups as the player leaves them after that.
    private bool opened;
    // The work as drawn now, or null while the station has no recipe.
    private ProcessRow? work;

    protected override void Body() {
        // A station's own tool cups sit in a window beside the page: what is slotted sets which recipes workers can run and how fast.
        var slotted = World.SlotKinds(thing.Kind);
        if (slotted.Length > 0) ToolWindow(slotted);
        if (thing.Kind == ThingKind.Bench) headingIcon.TooltipText = $"Works {world.Data.BenchSpeed:0.#}× as fast as by hand, at the speed of the slotted tool, or of the one you carry while you work it. Someone has to work it.";
        string? openOn = opened ? null : thing.Keep != "" ? thing.Keep : thing.Recipe != "" ? thing.Recipe : null;
        opened = true;
        AddChild(new RecipeBoard(kit, live, memory, panel, Redraw, thing, openOn, () => work?.Shake()));
        work = thing.Recipe != "" ? cups.Machine() : null;
        if (work != null) AddChild(work);
        else AddLabel("Click a recipe for workers to keep making it, or Shift-click to queue and load some.", 14, Style.Beige);
        // The side cups are the leftovers the station held when the page was drawn.
        RedrawOnChange(cups.SideCode);
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

    /// <summary>The window left of the page with the station's tool slots, each named with the tool it holds.</summary>
    private void ToolWindow(string[] kinds) {
        var window = panel.ShowAside(190);
        window.AddChild(Style.Heading("Available tools", 19));
        var slots = new VBoxContainer(); slots.AddThemeConstantOverride("separation", 10); window.AddChild(Style.InWell(slots));
        foreach (string tool in kinds) {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); slots.AddChild(row);
            row.AddChild(ToolSlot(tool));
            var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center }; words.AddThemeConstantOverride("separation", 0); row.AddChild(words);
            words.AddChild(Style.Text($"{char.ToUpperInvariant(tool[0])}{tool[1..]} slot", 12, Style.Tan));
            var held = Style.Text("", 13, Style.Ink, wrap: true); held.Name = $"tool_name_{tool}"; words.AddChild(held);
            live.Watch(() => world.BenchTool(thing, tool), now => held.Text = now != null ? world.Data.Name(now) : "Empty");
        }
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
