using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A crate: its fill out of twelve, whether the village would buy it packed, and how to pick it up.</summary>
public partial class CratePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var sells = Style.Pill("✓", Style.TagPill, Style.Rim, 13); sells.MouseFilter = Control.MouseFilterEnum.Pass; sells.TooltipText = "The village buys this crate packed";
        panel.AddToHeader(sells);
        var meter = Row(); meter.AddThemeConstantOverride("separation", 10);
        var cells = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; cells.AddThemeConstantOverride("separation", 4); meter.AddChild(cells);
        var segments = Enumerable.Range(0, thing.Inventory.Capacity).Select(_ => new ColorRect { CustomMinimumSize = new Vector2(0, 12), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }).ToArray();
        foreach (var segment in segments) cells.AddChild(segment);
        var fill = Style.Text("", 14, Style.Rim); meter.AddChild(fill);
        live.Watch(() => thing.Inventory.Count, drawn => {
            for (int i = 0; i < segments.Length; i++) segments[i].Color = i < drawn ? Style.Mustard : new Color(Style.Rim, .25f);
            fill.Text = $"{drawn} / {thing.Inventory.Capacity}";
            bool sellable = drawn > 0;
            foreach (var pair in thing.Inventory.Items) sellable &= world.Data.Market.Sells.ContainsKey(pair.Key);
            sells.Visible = sellable;
        });
        Well(new VBoxContainer()).AddChild(StorageGrid());
        var hint = Row(); hint.AddThemeConstantOverride("separation", 8);
        hint.AddChild(Style.Text("Hold", 14, Style.Beige)); hint.AddChild(Style.Glyph("line_mouse", 24, Style.Rim)); hint.AddChild(Style.Text("to pick up", 14, Style.Beige));
        StoreBagGrid("Click: store one · Ctrl: store all · Drag to move");
    }
}
