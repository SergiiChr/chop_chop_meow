using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A station's page: the recipe board, the work of its current recipe, the bellows of a reinforced furnace
/// and the jobs it offers, with its tool slots in a window of their own to the left.
/// </summary>
public partial class StationPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    // The board opens the group of what the station makes when the page opens, and keeps the groups as the player leaves them after that.
    private bool opened;
    // The work of the recipe the station is on, as drawn now.
    private ProcessRow work = null!;

    protected override void Body() {
        // A station's own tool slots sit in a window beside the page: what is slotted sets which recipes workers can run and how fast.
        var slotted = world.SlotRules(thing.Kind);
        if (slotted.Length > 0) ToolWindow(slotted);
        if (thing.Kind == ThingKind.Bench) headingIcon.TooltipText = $"Works {world.Data.BenchSpeed:0.#}× as fast as by hand, at the speed of the slotted tool, or of the one you carry while you work it. Someone has to work it.";
        string? openOn = opened ? null : world.Selected(thing);
        opened = true;
        // A refused mark is answered a moment after its click, when the page may have closed.
        AddChild(new RecipeBoard(kit, live, memory, panel, Redraw, thing, openOn, () => { if (IsInstanceValid(this) && IsInstanceValid(work)) work.Shake(); }));
        AddChild(work = stationSlots.Machine());
        // The side slots are the leftovers the station held when the page was drawn.
        RedrawOnChange(stationSlots.SideCode);
        // The batch time and how long the fuel lasts follow the tool the station is worked with.
        RedrawOnChange(stationSlots.RateCode);
        if (thing.Kind == ThingKind.Reinforced) {
            Bellows();
            RedrawOnChange(() => world.Selected(thing) == GlassBlowing.Item ? 1 : 0);
        }
        // A tool of a slot's kind goes to that slot, which says why when it does not take it; only a tool it takes lights up.
        bool SlotKind(string id) => world.Data.Items.GetValueOrDefault(id) is ItemDef def && Array.Exists(slotted, slot => slot.Matches(id, def));
        BagGridWithPlumbingSlot(id => {
            if (SlotKind(id)) world.SlotTool(thing, id);
            else world.LoadInput(thing, id, ItemMoves.Amount(world.S.Backpack, id));
        }, slotted.Length > 0 ? $"{BagHint}\nShift: slot a tool, or load a stack · Ctrl: all of it" : $"{BagHint}\nShift: load a stack · Ctrl: all of it",
            wanted: id => world.Takes(thing, id) || world.SlotBlock(thing, id) == null);
        JobButtons();
    }

    /// <summary>The window left of the page with the station's tool slots, each named with the tool it holds.</summary>
    private void ToolWindow(ToolSlotRule[] slots) {
        var window = panel.ShowAside(190);
        window.AddChild(Style.Heading("Available tools", 19));
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); window.AddChild(Style.InWell(column));
        foreach (var slot in slots) {
            string tool = slot.Tool;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); column.AddChild(row);
            row.AddChild(ToolSlot(slot));
            var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center }; words.AddThemeConstantOverride("separation", 0); row.AddChild(words);
            words.AddChild(Style.Text($"{char.ToUpperInvariant(tool[0])}{tool[1..]} slot", 12, Style.Tan));
            var held = Style.Text("", 13, Style.Ink, wrap: true); held.Name = $"tool_name_{tool}"; words.AddChild(held);
            live.Watch(() => world.BenchTool(thing, tool), now => held.Text = now != null ? world.Data.Name(now) : "Empty");
        }
    }

    /// <summary>
    /// A station's slot for one tool kind; while empty it shows a faint picture of the plainest tool it takes.
    /// </summary>
    private ItemSlot ToolSlot(ToolSlotRule slot) {
        string tool = slot.Tool;
        var (cell, _) = ItemSlot.Create($"tool_{tool}");
        cell.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        string plainest = world.Data.PlainestTool(tool, slot.Tier, thing.Kind);
        // A slot that asks for a better tool than the plainest of its kind names it.
        string empty = slot.Item != null ? $"Empty {tool} slot\nPut one {world.Data.Name(slot.Item).ToLowerInvariant()} here" : slot.Tier > 0 ? $"{world.Data.Name(plainest)} slot." : $"Empty {tool} slot\nPut a {tool} here from your backpack";
        moves.TakeCell(cell, thing.Tools!, () => world.BenchTool(thing, tool), thing,
            held => held.Stack is { Box: 0 } stack && world.Data.Items.GetValueOrDefault(stack.Item) is ItemDef def && slot.Takes(stack.Item, def), (item, _) => world.SlotTool(thing, item));
        live.Watch(() => world.BenchTool(thing, tool), held => {
            cell.Icon = Icons.Of(held ?? plainest);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held != null ? Colors.White : Style.Ghost);
            cell.TooltipText = held != null ? $"{world.Data.Name(held)} · {world.Data.Items[held].Speed:0.#}× speed\n{StationSlots.SlotHint}" : empty;
        });
        return cell;
    }

    /// <summary>The reinforced furnace's heat dial in degrees Celsius, with the bellows' pump and its key.</summary>
    private void Bellows() {
        var row = Row(); row.AddThemeConstantOverride("separation", 18);
        bool glass = world.Selected(thing) == GlassBlowing.Item;
        string heatRange = glass ? "Glass requires 1,300–1,600 °C" : $"Steel requires {World.Celsius(World.SteelHeat)}+ °C";
        var dial = new HeatDial { Glass = glass, Level = () => thing.Heat, CustomMinimumSize = new Vector2(132, 132), Name = "heat", TooltipText = heatRange };
        row.AddChild(dial);
        var column = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; column.AddThemeConstantOverride("separation", 8); row.AddChild(column);
        var degrees = Style.Heading("", 30); column.AddChild(degrees);
        column.AddChild(Style.Text(heatRange, 14, Style.Beige));
        if (glass) {
            var hint = Style.Text("Stand behind the furnace to pump.", 14, Style.Beige, wrap: true); hint.Name = "bellows_hint"; column.AddChild(hint);
            var status = Style.Text("", 14, Style.Beige, wrap: true); status.Name = "glass_status"; column.AddChild(status);
            live.Watch(() => GlassBlowing.Status(world, thing), text => status.Text = text);
        }
        var pumpRow = new HBoxContainer(); pumpRow.AddThemeConstantOverride("separation", 8); column.AddChild(pumpRow);
        var pump = kit.Button("Pump", () => { if (kit.Paused) kit.Notify("Resume before operating the bellows."); else panel.Player.UseFurnaceBellows(thing); });
        pump.Name = "pump"; pump.TooltipText = "Pump the bellows · Space"; pump.CustomMinimumSize = new Vector2(84, 38);
        Style.SkinPill(pump, Style.Sage);
        pumpRow.AddChild(pump); pumpRow.AddChild(Style.Pill("Space", Style.KeyPill, Style.Line, 12));
        // Redrawn only when the shown degrees change.
        live.Watch(() => glass ? GlassBlowing.Celsius(thing.Heat) : World.Celsius(thing.Heat), celsius => { dial.QueueRedraw(); degrees.Text = $"{celsius} °C"; });
    }
}
