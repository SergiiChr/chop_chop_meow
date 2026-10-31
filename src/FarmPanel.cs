using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A farm plot: its growth from bare soil to ripe wheat, then the process row: seeds in, fertilizer as its fuel, wheat out with seeds and grass beside.
/// </summary>
public partial class FarmPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var stages = new HBoxContainer(); stages.AddThemeConstantOverride("separation", 0); AddChild(stages);
        string?[] looks = { null, "wheat_seeds", "grass", "wheat" };
        string[] names = { "Fallow", "Sown", "Growing", "Ripe" };
        var nodes = new PanelContainer[4]; var links = new ColorRect[3];
        for (int i = 0; i < 4; i++) {
            if (i > 0) { links[i - 1] = new ColorRect { CustomMinimumSize = new Vector2(0, 6), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; stages.AddChild(links[i - 1]); }
            var node = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), TooltipText = names[i], MouseFilter = Control.MouseFilterEnum.Pass };
            node.AddChild(looks[i] is string look ? Style.Icon(Icons.Of(look), 30) : new ColorRect { Color = new Color("8a6a48"), CustomMinimumSize = new Vector2(24, 10), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            nodes[i] = node; stages.AddChild(node);
        }
        var ripe = Style.Text("", 14, Style.Rim); ripe.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; AddChild(ripe);
        live.Watch(() => {
            int at = thing.FarmStage == FarmStage.Ripe ? 3 : thing.FarmStage == FarmStage.Growing ? (thing.Progress < world.GrowSeconds / 3 ? 1 : 2) : 0;
            int hours = thing.FarmStage == FarmStage.Growing ? (int)((world.GrowSeconds - thing.Progress) / world.Data.DaySeconds * 24) : -1;
            return (at, hours);
        }, now => {
            var (at, hours) = now;
            for (int i = 0; i < 4; i++) nodes[i].AddThemeStyleboxOverride("panel", Style.Round(i < at ? Style.Sage : i == at ? Style.Mustard : Style.Line, 22).Item1);
            for (int i = 0; i < 3; i++) links[i].Color = i < at ? Style.Sage : Style.Line;
            ripe.Text = hours >= 0 ? $"ripe in {hours / 24} d {hours % 24} h" : thing.FarmStage == FarmStage.Ripe ? "ripe · hold right mouse on the plot to harvest" : "sows once seeds and fertilizer are in";
        });
        var output = thing.Output ?? thing.Inventory;
        var main = FarmCup(output, "wheat", false, $"×{world.Data.Farm.Yield.GetValueOrDefault("wheat")}");
        var side = world.Data.Farm.Yield.Keys.Where(k => k != "wheat").Select(k => FarmCup(output, k, false, $"×{world.Data.Farm.Yield[k]}", 44)).ToArray();
        var fertilizer = FarmCup(thing.Inventory, "fertilizer", true, "", 52, Style.CupFuel);
        var lasts = Style.Text("", 13, Style.Tan);
        int perCrop = world.Data.Farm.Sow.GetValueOrDefault("fertilizer");
        live.Watch(() => thing.Inventory["fertilizer"], stock => lasts.Text = $"×{perCrop} · {stock / Math.Max(1, perCrop)}{(stock == 1 ? " crop" : " crops")}");
        var inputs = world.Data.Farm.Sow.Keys.Where(k => k != "fertilizer").Select(k => FarmCup(thing.Inventory, k, true, $"×{world.Data.Farm.Sow[k]}"));
        AddChild(new ProcessRow(live, inputs, $"{world.Data.Farm.GrowDays:0} d", () => thing.FarmStage == FarmStage.Growing ? thing.Progress / world.GrowSeconds : thing.FarmStage == FarmStage.Ripe ? 1 : 0,
            new FuelRow("line_leaf", new Color("8fae7e"), FuelCup(fertilizer), lasts), main, side));
        BagGrid(id => moves.Stack(world.S.Backpack, thing.Inventory, id, thing), "Click: sow seeds or fertilizer · Drag to move", wanted: id => world.Holds(thing, id));
        JobButtons();
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
        cell.Pressed += () => { kit.Act(() => moves.Stack(box, world.S.Backpack, item, thing)); kit.Refresh(); };
        cell.Drag = () => box[item] > 0 ? new ItemDrag(item, box[item], box, thing) : null;
        if (input) {
            // Seed comes back in from the plot's own harvest as well as from the backpack.
            cell.CanTake = drag => (drag.From == world.S.Backpack || drag.From == thing.Output) && drag.Item == item;
            cell.Take = drag => { int left = 0; kit.Act(() => left = moves.Drag(drag, box, thing)); kit.Refresh(); return left; };
        }
        live.Watch(() => box[item], held => {
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}\n{(input ? "Click: take back · Drag seeds here from the harvest or backpack to sow" : "Click: take · Drag seeds to the sowing slot")}";
        });
        column.AddChild(cell);
        if (perBatch != "") column.AddChild(PerBatch(perBatch));
        return column;
    }
}
