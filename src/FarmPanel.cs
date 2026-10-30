using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A farm plot: its growth from bare soil to ripe wheat, then the process row: seeds in, fertilizer as its fuel, wheat out with seeds and grass beside.
/// </summary>
public partial class FarmPanel(SidePanel side, Thing farm) : PagePanel(side) {
    public override void _Ready() {
        var stages = new HBoxContainer(); stages.AddThemeConstantOverride("separation", 0); content.AddChild(stages);
        string?[] looks = { null, "wheat_seeds", "grass", "wheat" };
        string[] names = { "Fallow", "Sown", "Growing", "Ripe" };
        var nodes = new PanelContainer[4]; var links = new ColorRect[3];
        for (int i = 0; i < 4; i++) {
            if (i > 0) { links[i - 1] = new ColorRect { CustomMinimumSize = new Vector2(0, 6), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; stages.AddChild(links[i - 1]); }
            var node = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), TooltipText = names[i], MouseFilter = Control.MouseFilterEnum.Pass };
            node.AddChild(looks[i] is string look ? Style.Icon(Icons.Of(look), 30) : new ColorRect { Color = new Color("8a6a48"), CustomMinimumSize = new Vector2(24, 10), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            nodes[i] = node; stages.AddChild(node);
        }
        var ripe = Style.Text("", 14, Style.Rim); ripe.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; content.AddChild(ripe);
        var ripeText = new LiveText(shown => ripe.Text = shown);
        (int Stage, int Hours) drawn = (-1, -1);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(ripe)) return;
            int at = farm.FarmStage == FarmStage.Ripe ? 3 : farm.FarmStage == FarmStage.Growing ? (farm.Progress < world.GrowSeconds / 3 ? 1 : 2) : 0;
            int hours = farm.FarmStage == FarmStage.Growing ? (int)((world.GrowSeconds - farm.Progress) / world.Data.DaySeconds * 24) : -1;
            if ((at, hours) == drawn) return;
            drawn = (at, hours);
            for (int i = 0; i < 4; i++) nodes[i].AddThemeStyleboxOverride("panel", Style.Round(i < at ? Style.Sage : i == at ? Style.Mustard : Style.Line, 22).Item1);
            for (int i = 0; i < 3; i++) links[i].Color = i < at ? Style.Sage : Style.Line;
            var line = ripeText.Clear();
            if (hours >= 0) line.Append("ripe in ").Append(hours / 24).Append(" d ").Append(hours % 24).Append(" h");
            else line.Append(farm.FarmStage == FarmStage.Ripe ? "ripe · hold right mouse on the plot to harvest" : "sows once seeds and fertilizer are in");
            ripeText.Show();
        });
        var output = farm.Output ?? farm.Inventory;
        var main = FarmCup(output, "wheat", false, $"×{world.Data.Farm.Yield.GetValueOrDefault("wheat")}");
        var side = world.Data.Farm.Yield.Keys.Where(k => k != "wheat").Select(k => FarmCup(output, k, false, $"×{world.Data.Farm.Yield[k]}", 44)).ToArray();
        var fertilizer = FarmCup(farm.Inventory, "fertilizer", true, "", 52, Style.CupFuel);
        var lasts = Style.Text("", 13, Style.Tan);
        var lastsText = new LiveText(shown => lasts.Text = shown);
        int stock = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(lasts) || farm.Inventory["fertilizer"] == stock) return;
            stock = farm.Inventory["fertilizer"];
            lastsText.Clear().Append("×").Append(world.Data.Farm.Sow.GetValueOrDefault("fertilizer")).Append(" · ").Append(stock / Math.Max(1, world.Data.Farm.Sow.GetValueOrDefault("fertilizer"))).Append(stock == 1 ? " crop" : " crops"); lastsText.Show();
        });
        var inputs = world.Data.Farm.Sow.Keys.Where(k => k != "fertilizer").Select(k => FarmCup(farm.Inventory, k, true, $"×{world.Data.Farm.Sow[k]}"));
        Process(inputs, $"{world.Data.Farm.GrowDays:0} d", () => farm.FarmStage == FarmStage.Growing ? farm.Progress / world.GrowSeconds : farm.FarmStage == FarmStage.Ripe ? 1 : 0,
            FuelRow("line_leaf", new Color("8fae7e"), FuelCup(fertilizer), lasts), main, side);
        BagGrid(id => moves.Stack(world.S.Backpack, farm.Inventory, id, farm), "Click: sow seeds or fertilizer · Drag to move", wanted: id => world.Holds(farm, id));
        JobButtons(farm);
    }
    /// <summary>Takes a cup out of the column it was built in, for the fuel row, and frees the column.</summary>
    private static Control FuelCup(Control column) {
        var cup = column.GetChild<Control>(0); column.RemoveChild(cup); column.QueueFree();
        return cup;
    }
    /// <summary>A cup bound to one item of a farm's sowing or harvest slots, with what one crop takes or gives under it.</summary>
    private VBoxContainer FarmCup(Inventory box, string item, bool input, string perBatch, float size = 52, StyleBoxFlat? look = null) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, count) = ItemSlot.Cup($"{(input ? "input" : "output")}_{item}", size);
        cell.Icon = Icons.Of(item);
        if (look != null) cell.AddThemeStyleboxOverride("normal", look); else if (!input) cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
        cell.Pressed += () => { kit.Act(() => moves.Stack(box, world.S.Backpack, item, farm)); kit.Refresh(); };
        cell.Drag = () => box[item] > 0 ? new ItemDrag(item, box[item], box, farm) : null;
        if (input) {
            // Seed comes back in from the plot's own harvest as well as from the backpack.
            cell.CanTake = drag => (drag.From == world.S.Backpack || drag.From == farm.Output) && drag.Item == item;
            cell.Take = drag => { int left = 0; kit.Act(() => left = moves.Drag(drag, box, farm)); kit.Refresh(); return left; };
        }
        int drawn = -1;
        live.Add(() => {
            int held = box[item];
            if (held == drawn) return;
            drawn = held;
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}\n{(input ? "Click: take back · Drag seeds here from the harvest or backpack to sow" : "Click: take · Drag seeds to the sowing slot")}";
        });
        column.AddChild(cell);
        if (perBatch != "") column.AddChild(PerBatch(perBatch));
        return column;
    }
}
