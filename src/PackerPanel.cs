using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A packing station: the good it packs, its goods and empty crates, the crate being packed with how full it is, and its jobs.</summary>
public partial class Game {
    private bool packPicking;

    private void PackerPanel(Thing t) {
        var pick = Row(); pick.AddThemeConstantOverride("separation", 12);
        var (chosen, _) = IconCell("pack_good", 48); chosen.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        chosen.Icon = t.Packs == "" ? null : HotbarIcon(t.Packs);
        chosen.Text = t.Packs == "" ? "?" : "";
        chosen.TooltipText = "Choose what to pack";
        chosen.Pressed += () => { packPicking = !packPicking; OpenPage("inspect"); };
        pick.AddChild(chosen);
        var packs = Label(t.Packs == "" ? "Choose what to pack" : $"Packs {world.Data.Name(t.Packs).ToLowerInvariant()}", 16);
        packs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; packs.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        pick.AddChild(packs);
        if (packPicking || t.Packs == "") GoodPicker(t);
        if (t.Packs != "") {
            var inputs = new Control[] { MachineSlot(t, t.Inventory, t.Packs, true, "×1"), MachineSlot(t, t.Inventory, "crate", true, "") };
            Process(inputs, Text(text => AppendDuration(text, world.Data.PackSeconds)), () => t.Processing ? t.Progress / world.Data.PackSeconds : 0, null, PackedCrate(t), Array.Empty<Control>());
        }
        BagGrid(id => world.LoadInput(t, id, MoveAll ? world.S.Backpack[id] : 1), "Click: add one · Ctrl: all of it · Drag onto a slot", wanted: id => world.Holds(t, id));
        JobButtons(t);
    }

    /// <summary>Every good a crate holds as a grid of icons; the one packed now is lit.</summary>
    private void GoodPicker(Thing t) {
        var picker = new GridContainer { Columns = 7 }; picker.AddThemeConstantOverride("h_separation", 6); picker.AddThemeConstantOverride("v_separation", 6);
        Well(new VBoxContainer()).AddChild(picker);
        foreach (var (id, def) in world.Data.Items.Where(p => world.Packable(p.Key)).OrderBy(p => p.Value.Name)) {
            var (cell, _) = IconCell($"pack_{id}", 44); cell.Icon = HotbarIcon(id); cell.TooltipText = def.Name;
            if (id == t.Packs) cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
            cell.Pressed += () => { world.SetPacking(t, id); packPicking = false; OpenPage("inspect"); };
            picker.AddChild(cell);
        }
    }

    /// <summary>The crate being packed, with how many of its twelve places are filled; click takes it.</summary>
    private VBoxContainer PackedCrate(Thing t) {
        var output = t.Output!;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, _) = IconCell("packed_crate", 64);
        cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
        cell.Icon = HotbarIcon("crate");
        cell.Pressed += () => { if (output.First() is string crate) { Act(() => MoveStack(output, world.S.Backpack, crate, t)); RefreshLive(); } };
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
            cell.AddThemeColorOverride(IconNormal, packed >= 0 ? Colors.White : Ghost);
            cell.TooltipText = packed < 0 ? "The crate being packed shows here" : packed == world.Data.CrateCapacity ? "Full crate · click or drag to take" : "Crate being packed · click or drag to take";
            fillText.Clear().Append(Math.Max(0, packed)).Append(" / ").Append(world.Data.CrateCapacity); fillText.Show();
        });
        return column;
    }
}
