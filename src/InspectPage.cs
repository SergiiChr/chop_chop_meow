using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Picks the page for what the side panel inspects.</summary>
public static class InspectPage {
    public static PagePanel For(PageTools tools, Selection selection) {
        var world = tools.Kit.World;
        if (selection.Pipe is Vector2I tile) return new PipePanel(tools, tile);
        if (selection.Worker > 0) return new WorkerPanel(tools, world.Workers.FirstOrDefault(w => w.Id == selection.Worker));
        return world.Find(selection.Thing) switch {
            null => new NothingSelectedPage(tools),
            { Kind: ThingKind.Hole } t => new HolePanel(tools, t),
            { IsResource: true } t => new ResourcePanel(tools, t),
            { Kind: ThingKind.Hut } t => new CabinPanel(tools, t),
            { Kind: ThingKind.Signpost } t => new MarketPanel(tools, t),
            { Kind: ThingKind.Mine } t => new MinePanel(tools, t),
            { Kind: ThingKind.Farm } t => new FarmPanel(tools, t),
            { Kind: ThingKind.Campfire } t => new CampfirePanel(tools, t),
            { Kind: ThingKind.Pump } t => new WaterPumpPanel(tools, t),
            { Kind: ThingKind.Packer } t => new PackerPanel(tools, t),
            { Kind: ThingKind.Crate } t => new CratePanel(tools, t),
            { Kind: ThingKind.Barrel } t => new BarrelPanel(tools, t),
            { Kind: ThingKind.Lantern } t => new LanternPanel(tools, Lanterns.Id(t.LanternItem), t),
            { Kind: ThingKind.Grave } t => new RecoveryBoxPanel(tools, t),
            { Kind: ThingKind.Toolstation } t => new ToolRackPanel(tools, t),
            { Kind: ThingKind.Pile } t => new PilePanel(tools, t),
            { IsStorage: true } t => new StoragePanel(tools, t),
            var t => new StationPanel(tools, t)
        };
    }
}

/// <summary>The inspect page with nothing to inspect.</summary>
public partial class NothingSelectedPage(PageTools tools) : PagePanel(tools) {
    protected override void Build() => AddLabel("Select a building or resource in the world.");
}

/// <summary>
/// A page about one thing in the world: its name and icon, how far it is, and what the page for that kind adds.
/// The page keeps which job list and filters are open while it shows.
/// </summary>
public abstract partial class ThingPage(PageTools tools, Thing subject) : PagePanel(tools) {
    /// <summary>What the page is about.</summary>
    protected readonly Thing thing = subject;
    private readonly JobChoice job = new();
    private readonly FilterState filters = new();

    /// <summary>The slots bound to this thing.</summary>
    protected StationSlots stationSlots => field ??= new StationSlots(kit, moves, live, thing);

    public override bool Current => world.Find(thing.Id) == thing;
    protected override bool StoresFromHotbar => true;
    protected override bool PoursFromBag(string item) => world.PoursInto(thing, thing.Inventory, item) != null;

    protected override void Build() {
        Headline();
        Reach();
        Body();
        if (thing.PlumbingSlot is Inventory plumbingSlot) {
            var window = panel.ShowAside(190);
            window.AddChild(Style.Heading("Plumbing", 19));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            window.AddChild(Style.InWell(row));
            var slot = stationSlots.Slot(plumbingSlot, "steel_pipe", true,
                count => $"Plumbing · {count}/{Plumbing.InstallCost} steel pipes\n{StationSlots.SlotHint}",
                takes: held => held.Stack?.Item == "steel_pipe");
            slot.Name = "plumbing_slot";
            row.AddChild(slot);
            var label = Style.Text("", 13, Style.Ink, wrap: true);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(label);
            live.Watch(() => plumbingSlot["steel_pipe"], count => label.Text =
                count == Plumbing.InstallCost ? "Installed" : $"{count}/{Plumbing.InstallCost} pipes");
        }
        RedrawOnChange(() => (thing.Packs, world.Selected(thing), thing.Compost, FilterCode()));
    }

    /// <summary>A number that changes with the filters' contents and their order.</summary>
    private int FilterCode() {
        var code = new HashCode();
        code.Add(thing.Inventory.InputFilter.Count);
        foreach (string item in thing.Inventory.InputFilter) code.Add(item);
        foreach (string item in thing.Inventory.OutputFilter) code.Add(item);
        return code.ToHashCode();
    }

    /// <summary>What this kind of thing shows under its name.</summary>
    protected abstract void Body();

    /// <summary>Names the page after the building it shows, with the building's own icon; its description becomes the icon's tooltip.</summary>
    private void Headline() {
        bool water = thing.Kind == ThingKind.Water;
        headingTitle.Text = water ? "Collect water" : world.Data.Name(thing.Kind);
        headingIcon.Texture = Icons.Of(thing.Kind switch {
            ThingKind.Water => Water.Liquid,
            ThingKind.LeafSpot => World.ResourceItem(thing),
            _ => thing.Kind.Id()
        });
        headingIcon.Visible = headingIcon.Texture != null;
        headingIcon.TooltipText = world.Data.Buildings.TryGetValue(thing.Kind, out var def) ? def.Description : "";
    }

    /// <summary>How far away a building is, shown only while it is out of reach.</summary>
    private void Reach() {
        var label = Style.Text("", 14, Style.Warn); label.Visible = false; AddChild(label);
        // Rewritten only when the distance moves a tenth of a tile or the building comes into reach.
        live.Watch(() => world.Reaches(thing) ? -1 : (int)(world.Player.DistanceTo(world.GatherAt(thing, world.Player)) * 10), tenths => {
            label.Visible = tenths >= 0;
            if (tenths >= 0) label.Text = $"{tenths / 10}.{tenths % 10} tiles away · walk closer";
        });
    }

    /// <summary>The thing's slots in a grid; Shift takes the stack clicked to the backpack and Ctrl all of that item.</summary>
    protected GridContainer StorageGrid(int columns = GridColumns, float size = 52) =>
        NewInventoryGrid(() => thing.Inventory, thing, id => moves.Stack(thing.Inventory, world.S.Backpack, id, thing), StationSlots.SlotHint, columns: columns, size: size);

    /// <summary>The backpack drawer, where Shift and Ctrl store into this thing.</summary>
    protected void StoreBagGrid(Func<string, bool>? wanted = null) =>
        BagGridWithPlumbingSlot(id => moves.Stack(world.S.Backpack, thing.Inventory, id, thing), $"{BagHint}\nShift: store a stack · Ctrl: all of it", wanted);

    /// <summary>Moves steel pipes to the building's Plumbing slot when a player Shift-clicks or Ctrl-clicks a stack.</summary>
    protected void BagGridWithPlumbingSlot(Action<string> move, string hint, Func<string, bool>? wanted) {
        if (thing.PlumbingSlot is not Inventory plumbingSlot) { BagGrid(move, hint, wanted: wanted); return; }
        BagGrid(item => {
            if (item == "steel_pipe") moves.Stack(world.S.Backpack, plumbingSlot, item, thing);
            else move(item);
        }, hint, wanted: item => item == "steel_pipe" ? plumbingSlot.Room(item) > 0 : wanted?.Invoke(item) == true);
    }

    /// <summary>The backpack drawer, where Shift and Ctrl load this station.</summary>
    protected void LoadBagGrid(Func<string, bool> wanted) =>
        BagGrid(id => world.LoadInput(thing, id, ItemMoves.Amount(world.S.Backpack, id)), $"{BagHint}\nShift: load a stack · Ctrl: all of it", wanted: wanted);

    /// <summary>Whether the filter window is open beside the panel.</summary>
    protected bool FiltersOpen => filters.Open;

    /// <summary>The funnel that opens the filter window beside the panel, ringed while it is open.</summary>
    protected void FilterButton() {
        var button = HeaderAction("filter", "Filters: what workers bring and take", () => { filters.Open = !filters.Open; filters.Picking = false; Redraw(); }, Style.FilterRound, "filters");
        if (!filters.Open) return;
        Style.Skin(button, Style.Round(Style.Mustard, 18).Item1, Style.FilterRound.Hover);
        panel.SideBoard(Style.Board, 340).AddChild(new FilterWindow(kit, cursor, thing, filters, Redraw, (list, rows) => panel.ShowSheetRows(list, rows, hidesDrawer: true)));
    }

    /// <summary>The row of job buttons, for a place that offers jobs.</summary>
    protected void JobButtons() { if (world.Jobs(thing).Length > 0) AddChild(new JobRow(kit, live, thing, job, Redraw)); }
}
