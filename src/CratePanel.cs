using System;
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
    // An empty crate's page lasts while one is carried, and until it turns to the crate its goods made; a crate picked up onto the cursor closes its page.
    public override bool Current => crate == 0 ? (world.Backpack["crate"] > 0 || NewlyPacked() > 0) && world.Held?.Item != "crate" : world.CarriedCrate(crate) is string item && world.Held?.Item != item;
    // An emptied, placed or dropped crate leaves the backpack, and its page gives way to the backpack's.
    public override Page GivesWayTo => Page.Backpack;
    protected override bool StoresFromHotbar => true;

    /// <summary>What the crate holds now, an empty crate's room for an empty one, or nothing once it has gone.</summary>
    private Inventory Contents => world.CarriedCrate(crate) is string item && world.Packed(item) is Inventory held ? held : crate == 0 ? empty ??= world.NewCrate() : gone;
    private readonly Inventory gone = new() { Slots = 1, Capacity = 1 };
    private Inventory? empty;

    protected override void Build() {
        headingIcon.Texture = Icons.Of("crate"); headingIcon.Visible = true;
        var sells = Style.Pill("✓", Style.TagPill, Style.Rim, 13); sells.MouseFilter = Control.MouseFilterEnum.Pass; sells.TooltipText = "The village buys this crate packed";
        panel.AddToHeader(sells);
        AddChild(CratePanel.Meter(world, live, () => Contents, sells));
        // Goods put on an empty crate's cells pack into it wherever they fit.
        Well(new VBoxContainer()).AddChild(NewInventoryGrid(() => Contents, null, id => world.MoveInCrate(crate, id, ItemMoves.Amount(Contents, id), false),
            $"{StationSlots.SlotHint}", held => held.Stack is { Box: 0 } stack && !World.IsCrate(stack.Item), put: (_, item, units) => world.MoveInCrate(crate, item, units, true)));
        AddButton("Put it down", () => panel.Player.PlaceCrate(world.CarriedCrate(crate) ?? "crate"));
        BagGrid(id => world.MoveInCrate(crate, id, ItemMoves.Amount(world.S.Backpack, id), true), $"{BagHint}\nShift: pack a stack · Ctrl: all of it",
            put: (slot, item, units) => { if (world.HeldBox == Contents) world.MoveInCrate(crate, item, units, false); else moves.Store(world.S.Backpack, slot)(item, units); });
        if (crate == 0) FollowPacked();
    }

    /// <summary>The highest number of a packed crate carried when the empty crate's page opened; crates get ever higher numbers. Null until then.</summary>
    private int? packedBefore;

    /// <summary>The number of a packed crate carried now that is newer than the empty crate's page, or 0.</summary>
    private int NewlyPacked() {
        if (packedBefore is not int before) return 0;
        foreach (var pair in world.Backpack.Items) if (World.PackedId(pair.Key) is int id && id > before) return id;
        return 0;
    }

    /// <summary>Once goods packed into the empty crate make it a packed crate, shows that crate.</summary>
    private void FollowPacked() {
        int highest = 0;
        foreach (var pair in world.Backpack.Items) highest = Math.Max(highest, World.PackedId(pair.Key));
        packedBefore = highest;
        live.Watch(NewlyPacked, id => { if (id > 0) panel.OpenCrate(id); });
    }
}
