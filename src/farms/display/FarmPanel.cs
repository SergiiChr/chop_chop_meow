using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// A farm plot with its growth, sowing slots, water level and harvest.
/// </summary>
public partial class FarmPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        string?[] looks = { null, "wheat_seeds", "grass", "wheat" };
        string[] names = { "Fallow", "Sown", "Growing", "Ripe" };
        var nodes = new PanelContainer[4];
        for (int i = 0; i < 4; i++) {
            nodes[i] = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), TooltipText = names[i], MouseFilter = Control.MouseFilterEnum.Pass };
            nodes[i].AddChild(looks[i] is string look ? Style.Icon(Icons.Of(look), 30) : new ColorRect { Color = new Color("8a6a48"), CustomMinimumSize = new Vector2(24, 10), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        }
        var track = new StageTrack(nodes); AddChild(track);
        var ripe = Style.Text("", 14, Style.Rim); ripe.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; AddChild(ripe);
        live.Watch(() => thing.FarmStage == FarmStage.Ripe ? 3 : Math.Min(2, world.Farms.CropLook(thing)), track.At);
        live.Watch(() => (thing.FarmStage, HoursUntilRipe(), world.Farms.Dry(thing)), now => {
            var (stage, hours, dry) = now;
            ripe.Text = stage switch {
                FarmStage.Growing when dry => $"Needs water · {Until(hours)} of growth left",
                FarmStage.Growing => $"Growing, {Until(hours)} until ripe",
                FarmStage.Ripe => "Ripe · hold right mouse on the plot to harvest",
                _ => "Sows once seeds and fertilizer are in"
            };
        });
        var fertilizer = InputSlot("fertilizer", Style.SlotFuel);
        var lasts = Style.Text("", 13, Style.Tan);
        int perCrop = world.Data.Farm.Sow.GetValueOrDefault("fertilizer");
        live.Watch(() => thing.Inventory["fertilizer"], stock => lasts.Text = $"×{perCrop} · {stock / Math.Max(1, perCrop)}{(stock == 1 ? " crop" : " crops")}");
        var inputs = world.Data.Farm.Sow.Keys.Where(k => k != "fertilizer").Select(k => (Control)StationSlots.Labelled(InputSlot(k), $"×{world.Data.Farm.Sow[k]}"));
        var main = StationSlots.Labelled(OutputSlot("wheat"), $"×{world.Data.Farm.Yield.GetValueOrDefault("wheat")}");
        var side = world.Data.Farm.Yield.Keys.Where(k => k != "wheat").Select(k => (Control)StationSlots.Labelled(OutputSlot(k, 44), $"×{world.Data.Farm.Yield[k]}")).ToArray();
        var supplies = new VBoxContainer();
        supplies.AddThemeConstantOverride("separation", 8);
        supplies.AddChild(new FuelRow("line_leaf", new Color("8fae7e"), fertilizer, lasts));
        supplies.AddChild(WaterRow());
        AddChild(new ProcessRow(live, inputs, $"{world.Data.Farm.GrowDays:0} d",
            () => thing.FarmStage == FarmStage.Growing ? world.Farms.Growth(thing) / world.Farms.GrowSeconds : thing.FarmStage == FarmStage.Ripe ? 1 : 0,
            supplies, main, side));
        StoreBagGrid(id => world.Holds(thing, id));
        JobButtons();
    }

    private int HoursUntilRipe() => thing.FarmStage == FarmStage.Growing
        ? (int)((world.Farms.GrowSeconds - world.Farms.Growth(thing)) / world.Data.DaySeconds * 24) : -1;

    private HBoxContainer WaterRow() {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var slot = stationSlots.Slot(thing.Inventory, Water.Liquid, true,
            held => $"Water · {held}/{Farms.LitersPerDay} L\n{StationSlots.SlotHint} · put a filled vessel here to pour",
            takes: held => held.Stack is { Box: 0 } stack && Barrels.CanPour(world.Data, stack.Item));
        row.AddChild(slot);
        var level = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        level.AddChild(Style.Text($"{Farms.LitersPerDay} L per day", 13, Style.Ink));
        var bar = new ProgressBar { Name = "farm_water", MaxValue = Farms.LitersPerDay, ShowPercentage = false, CustomMinimumSize = new Vector2(60, 12) };
        bar.AddThemeStyleboxOverride("fill", Style.Flat(new Color("78b9d6"), 7, 0));
        live.Watch(() => (int)(world.Farms.WaterLevel(thing) * 20), at => bar.Value = at / 20f);
        level.AddChild(bar);
        row.AddChild(level);
        return row;
    }

    /// <summary>Whole days left while a day or more remains, then hours.</summary>
    private static string Until(int hours) => hours >= 24 ? Count(hours / 24, "day") : Count(Math.Max(1, hours), "hour");
    private static string Count(int n, string unit) => n == 1 ? $"1 {unit}" : $"{n} {unit}s";

    /// <summary>A slot for what the plot sows from; seed comes back in from the plot's own harvest as well as from the backpack.</summary>
    private ItemSlot InputSlot(string item, StyleBoxFlat? look = null) =>
        stationSlots.Slot(thing.Inventory, item, true, held => $"{world.Data.Name(item)} · {held}\n{StationSlots.SlotHint} · put seeds here from the harvest or backpack to sow", look: look,
            takes: held => held.Stack is HeldStack stack && stack.Item == item && (stack.Box == 0 || thing.Output != null && stack.Box == world.Locate(thing.Output)));

    private ItemSlot OutputSlot(string item, float size = 52) =>
        stationSlots.Slot(thing.Output ?? thing.Inventory, item, false, held => $"{world.Data.Name(item)} · {held}\n{StationSlots.SlotHint} · put seeds back in the input slot", size);
}
