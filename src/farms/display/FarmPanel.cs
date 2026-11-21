using System;
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
        var row = Well(new HBoxContainer());
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(Inputs());
        row.AddChild(Harvest());
        StoreBagGrid(id => world.Holds(thing, id));
        JobButtons();
    }

    private VBoxContainer Inputs() {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        foreach (var (item, perSowing) in world.Data.Farm.Sow) {
            bool fertilizer = item == "fertilizer";
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(InputSlot(item, fertilizer ? Style.SlotFuel : null));
            var amount = Style.Text($"×{perSowing}", 13, Style.Tan);
            amount.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            if (fertilizer) live.Watch(() => thing.Inventory[item], stock => amount.Text = $"×{perSowing} · {Count(stock / perSowing, "crop")}");
            row.AddChild(amount);
            column.AddChild(row);
        }
        column.AddChild(WaterRow());
        return column;
    }

    private VBoxContainer Harvest() {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        var outputs = new HBoxContainer();
        outputs.AddThemeConstantOverride("separation", 10);
        foreach (var (item, count) in world.Data.Farm.Yield)
            outputs.AddChild(StationSlots.Labelled(OutputSlot(item), $"×{count}"));
        column.AddChild(outputs);

        var growth = new VBoxContainer { SizeFlagsVertical = SizeFlags.Expand | SizeFlags.ShrinkCenter };
        growth.AddThemeConstantOverride("separation", 8);
        var clock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        clock.AddChild(Style.Glyph("line_clock", 16, Style.Ink));
        clock.AddChild(Style.Text($"{world.Data.Farm.GrowDays:0} d", 14, Style.Ink));
        growth.AddChild(clock);
        var bar = new ProgressBar { Name = "progress", MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(60, 14) };
        live.Watch(() => thing.FarmStage switch {
            FarmStage.Growing => (int)(world.Farms.Growth(thing) / world.Farms.GrowSeconds * 200),
            FarmStage.Ripe => 200,
            _ => 0
        }, at => bar.Value = at / 200f);
        growth.AddChild(bar);
        column.AddChild(growth);
        return column;
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

    private ItemSlot OutputSlot(string item) =>
        stationSlots.Slot(thing.Output ?? thing.Inventory, item, false, held => $"{world.Data.Name(item)} · {held}\n{StationSlots.SlotHint} · put seeds back in the input slot");
}
