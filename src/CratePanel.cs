using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A crate: its fill out of twelve, whether the village would buy it packed, and how to pick it up.</summary>
public partial class CratePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        var sells = Style.Pill("✓", Style.TagPill, Style.Rim, 13); sells.MouseFilter = Control.MouseFilterEnum.Pass; sells.TooltipText = "The village buys this crate packed";
        panel.AddToHeader(sells);
        AddChild(Meter(world, live, () => thing.Inventory, sells));
        Well(new VBoxContainer()).AddChild(StorageGrid());
        var hint = Row(); hint.AddThemeConstantOverride("separation", 8);
        hint.AddChild(Style.Text("Hold", 14, Style.Beige)); hint.AddChild(Style.Glyph("line_mouse", 24, Style.Rim)); hint.AddChild(Style.Text("to pick up", 14, Style.Beige));
        StoreBagGrid();
    }

    /// <summary>How full a crate is out of its capacity, which also shows <paramref name="sells"/> while the village buys everything in it.</summary>
    public static Control Meter(World world, LiveUpdates live, Func<Inventory> contents, Control sells) {
        var meter = new HBoxContainer(); meter.AddThemeConstantOverride("separation", 10);
        var cells = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; cells.AddThemeConstantOverride("separation", 4); meter.AddChild(cells);
        var segments = Enumerable.Range(0, contents().Capacity).Select(_ => new ColorRect { CustomMinimumSize = new Vector2(0, 12), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }).ToArray();
        foreach (var segment in segments) cells.AddChild(segment);
        var fill = Style.Text("", 14, Style.Rim); meter.AddChild(fill);
        live.Watch(() => contents().Count, drawn => {
            for (int i = 0; i < segments.Length; i++) segments[i].Color = i < drawn ? Style.Mustard : new Color(Style.Rim, .25f);
            fill.Text = $"{drawn} / {contents().Capacity}";
            bool sellable = drawn > 0;
            foreach (var pair in contents().Items) sellable &= world.Data.Market.Sells.ContainsKey(pair.Key);
            sells.Visible = sellable;
        });
        return meter;
    }
}

/// <summary>
/// A crate opened from the backpack as storage: its contents move to and from the backpack by click, and it can be put down from here.
/// An empty crate opens with number 0; the first goods packed into it make it a packed crate, which the page then shows.
/// </summary>
public partial class CarriedCratePanel(PageTools tools, int crate) : PagePanel(tools) {
    public override bool Current => crate == 0 ? world.Backpack["crate"] > 0 : world.CarriedCrate(crate) != null;
    // An emptied, placed or dropped crate leaves the backpack, and its page gives way to the backpack's.
    public override Page GivesWayTo => Page.Backpack;
    protected override bool StoresFromHotbar => true;

    /// <summary>What the crate holds now, or an empty crate for an empty one or once it has gone.</summary>
    private Inventory Contents => world.CarriedCrate(crate) is string item && world.Packed(item) is Inventory held ? held : gone;
    private readonly Inventory gone = new() { Slots = 1, Capacity = 1 };

    protected override void Build() {
        headingIcon.Texture = Icons.Of("crate"); headingIcon.Visible = true;
        var sells = Style.Pill("✓", Style.TagPill, Style.Rim, 13); sells.MouseFilter = Control.MouseFilterEnum.Pass; sells.TooltipText = "The village buys this crate packed";
        panel.AddToHeader(sells);
        AddChild(CratePanel.Meter(world, live, () => Contents, sells));
        // Goods put on an empty crate's cells pack into it wherever they fit.
        Well(new VBoxContainer()).AddChild(NewInventoryGrid(() => Contents, null, id => world.MoveInCrate(crate, id, ItemMoves.Amount(Contents, id), false),
            $"{Cups.SlotHint}", held => held.Stack is { Box: 0 } stack && !World.IsCrate(stack.Item), put: crate == 0 ? (_, units) => world.MoveInCrate(0, cursor.Item!, units, true) : null));
        AddButton("Put it down", () => panel.Player.PlaceCrate(world.CarriedCrate(crate) ?? "crate"));
        BagGrid(id => world.MoveInCrate(crate, id, ItemMoves.Amount(world.S.Backpack, id), true), $"{BagHint}\nShift: pack a stack · Ctrl: all of it");
        if (crate == 0) FollowPacked();
    }

    /// <summary>Once goods packed into the empty crate make it a packed crate, shows that crate.</summary>
    private void FollowPacked() {
        var before = new HashSet<int>();
        foreach (var pair in world.Backpack.Items) if (World.PackedId(pair.Key) is int id and > 0) before.Add(id);
        live.Watch(() => { int packed = 0; foreach (var pair in world.Backpack.Items) if (World.PackedId(pair.Key) > 0) packed++; return packed; }, _ => {
            foreach (var pair in world.Backpack.Items) if (World.PackedId(pair.Key) is int id and > 0 && !before.Contains(id)) { panel.OpenCrate(id); return; }
        });
    }
}
