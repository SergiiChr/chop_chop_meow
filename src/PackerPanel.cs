using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A packing station: the good it packs, its goods and empty crates, the crate being packed with how full it is, and its jobs.</summary>
public partial class PackerPanel(SidePanel side, Thing t) : PagePanel(side) {
    public override void _Ready() {
        var pick = Row(); pick.AddThemeConstantOverride("separation", 12);
        var (chosen, _) = ItemSlot.Cup("pack_good", 48); chosen.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        chosen.Icon = t.Packs == "" ? null : Icons.Of(t.Packs);
        chosen.Text = t.Packs == "" ? "?" : "";
        chosen.TooltipText = "Choose what to pack";
        chosen.Pressed += () => { memory.PackPicking = !memory.PackPicking; panel.Open("inspect"); };
        pick.AddChild(chosen);
        var packs = Style.Wrapped(t.Packs == "" ? "Choose what to pack" : $"Packs {world.Data.Name(t.Packs).ToLowerInvariant()}", 16);
        packs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; packs.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        pick.AddChild(packs);
        if (memory.PackPicking || t.Packs == "") GoodPicker();
        if (t.Packs != "") {
            var inputs = new Control[] { MachineSlot(t, t.Inventory, t.Packs, true, "×1"), MachineSlot(t, t.Inventory, "crate", true, "") };
            Process(inputs, LiveText.Of(text => AppendDuration(text, world.Data.PackSeconds)), () => t.Processing ? t.Progress / world.Data.PackSeconds : 0, null, PackedCrate(), Array.Empty<Control>());
        }
        BagGrid(id => world.LoadInput(t, id, ItemMoves.All ? world.Backpack[id] : 1), "Click: add one · Ctrl: all of it · Drag onto a slot", wanted: id => world.Holds(t, id));
        JobButtons(t);
    }

    /// <summary>Every good a crate holds as a grid of icons; the one packed now is lit.</summary>
    private void GoodPicker() {
        var picker = new GridContainer { Columns = 7 }; picker.AddThemeConstantOverride("h_separation", 6); picker.AddThemeConstantOverride("v_separation", 6);
        Well(new VBoxContainer()).AddChild(picker);
        foreach (var (id, def) in world.Data.Items.Where(p => world.Packable(p.Key)).OrderBy(p => p.Value.Name)) {
            var (cell, _) = ItemSlot.Cup($"pack_{id}", 44); cell.Icon = Icons.Of(id); cell.TooltipText = def.Name;
            if (id == t.Packs) cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
            cell.Pressed += () => { world.SetPacking(t, id); memory.PackPicking = false; panel.Open("inspect"); };
            picker.AddChild(cell);
        }
    }

    /// <summary>The crate being packed, with how many of its twelve places are filled; click takes it.</summary>
    private VBoxContainer PackedCrate() {
        var output = t.Output!;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, _) = ItemSlot.Cup("packed_crate", 64);
        cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
        cell.Icon = Icons.Of("crate");
        cell.Pressed += () => { if (output.First() is string crate) { kit.Act(() => moves.Stack(output, world.S.Backpack, crate, t)); kit.Refresh(); } };
        cell.Drag = () => output.First() is string crate ? new ItemDrag(crate, 1, output, t) : null;
        column.AddChild(cell);
        var fill = PerBatch(""); column.AddChild(fill);
        var fillText = new LiveText(shown => fill.Text = shown);
        int drawn = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(cell)) return;
            int packed = output.First() is string crate && world.Packed(crate) is Inventory contents ? contents.Count : -1;
            if (packed == drawn) return;
            drawn = packed;
            cell.AddThemeColorOverride(ItemSlot.IconNormal, packed >= 0 ? Colors.White : Style.Ghost);
            cell.TooltipText = packed < 0 ? "The crate being packed shows here" : packed == world.Data.CrateCapacity ? "Full crate · click or drag to take" : "Crate being packed · click or drag to take";
            fillText.Clear().Append(Math.Max(0, packed)).Append(" / ").Append(world.Data.CrateCapacity); fillText.Show();
        });
        return column;
    }
}
