using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A packing station: the good it packs, its goods and empty crates, the crate being packed with how full it is, and its jobs.</summary>
public partial class PackerPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var pick = Row(); pick.AddThemeConstantOverride("separation", 12);
        var (chosen, _) = ItemSlot.Cup("pack_good", 48); chosen.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        chosen.Icon = thing.Packs == "" ? null : Icons.Of(thing.Packs);
        chosen.Text = thing.Packs == "" ? "?" : "";
        chosen.TooltipText = "Choose what to pack";
        chosen.Pressed += () => { memory.PackPicking = !memory.PackPicking; Redraw(); };
        pick.AddChild(chosen);
        var packs = Style.Wrapped(thing.Packs == "" ? "Choose what to pack" : $"Packs {world.Data.Name(thing.Packs).ToLowerInvariant()}", 16);
        packs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; packs.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        pick.AddChild(packs);
        if (memory.PackPicking || thing.Packs == "") GoodPicker();
        if (thing.Packs != "") {
            var inputs = new Control[] { cups.StationCup(thing.Packs, true, "×1"), cups.StationCup("crate", true, "") };
            AddChild(new ProcessRow(live, inputs, LiveText.Of(text => PageText.AppendDuration(text, world.Data.PackSeconds)), () => thing.Processing ? thing.Progress / world.Data.PackSeconds : 0, null, PackedCrate(), Array.Empty<Control>()));
        }
        BagGrid(id => world.LoadInput(thing, id, ItemMoves.All ? world.Backpack[id] : 1), "Click: add one · Ctrl: all of it · Drag onto a slot", wanted: id => world.Holds(thing, id));
        JobButtons();
    }

    /// <summary>Every good a crate holds as a grid of icons; the one packed now is lit.</summary>
    private void GoodPicker() {
        var picker = new GridContainer { Columns = 7 }; picker.AddThemeConstantOverride("h_separation", 6); picker.AddThemeConstantOverride("v_separation", 6);
        Well(new VBoxContainer()).AddChild(picker);
        foreach (var (id, def) in world.Data.Items.Where(p => world.Packable(p.Key)).OrderBy(p => p.Value.Name)) {
            var (cell, _) = ItemSlot.Cup($"pack_{id}", 44); cell.Icon = Icons.Of(id); cell.TooltipText = def.Name;
            if (id == thing.Packs) cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
            cell.Pressed += () => { world.SetPacking(thing, id); memory.PackPicking = false; Redraw(); };
            picker.AddChild(cell);
        }
    }

    /// <summary>The crate being packed, with how many of its twelve places are filled; click takes it.</summary>
    private VBoxContainer PackedCrate() {
        var output = thing.Output!;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, _) = ItemSlot.Cup("packed_crate", 64);
        cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
        cell.Icon = Icons.Of("crate");
        moves.TakeCell(cell, output, output.First, thing);
        column.AddChild(cell);
        var fill = Style.PerBatch(""); column.AddChild(fill);
        live.Watch(() => output.First() is string crate && world.Packed(crate) is Inventory contents ? contents.Count : -1, packed => {
            cell.AddThemeColorOverride(ItemSlot.IconNormal, packed >= 0 ? Colors.White : Style.Ghost);
            cell.TooltipText = packed < 0 ? "The crate being packed shows here" : packed == world.Data.CrateCapacity ? "Full crate · click or drag to take" : "Crate being packed · click or drag to take";
            fill.Text = $"{Math.Max(0, packed)} / {world.Data.CrateCapacity}";
        });
        return column;
    }
}
