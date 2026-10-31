using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>Picks the page for what the side panel inspects.</summary>
public static class InspectPage {
    public static PagePanel For(PageTools tools, Selection selection) {
        var world = tools.Kit.World;
        if (selection.Worker > 0) return new WorkerPanel(tools, world.Workers.FirstOrDefault(w => w.Id == selection.Worker));
        return world.Find(selection.Thing) switch {
            null => new NothingSelectedPage(tools),
            { IsResource: true } t => new ResourcePanel(tools, t),
            { Kind: ThingKind.Hut } t => new CabinPanel(tools, t),
            { Tools: not null } t => new BenchPanel(tools, t),
            { Kind: ThingKind.Signpost } t => new MarketPanel(tools, t),
            { Kind: ThingKind.Mine } t => new MinePanel(tools, t),
            { Kind: ThingKind.Farm } t => new FarmPanel(tools, t),
            { Kind: ThingKind.Campfire } t => new CampfirePanel(tools, t),
            { Kind: ThingKind.Packer } t => new PackerPanel(tools, t),
            { Kind: ThingKind.Crate } t => new CratePanel(tools, t),
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

    /// <summary>The cups bound to this thing.</summary>
    protected Cups cups => field ??= new Cups(kit, moves, live, thing);

    public override bool Current => world.Find(thing.Id) == thing;

    protected override void Build() {
        Headline();
        Reach();
        Body();
    }

    /// <summary>What this kind of thing shows under its name.</summary>
    protected abstract void Body();

    /// <summary>Names the page after the building it shows, with the building's own icon; its description becomes the icon's tooltip.</summary>
    private void Headline() {
        headingTitle.Text = thing.Kind == ThingKind.Water ? "Collect water" : world.Data.Name(thing.Kind);
        headingIcon.Texture = Icons.Of(thing.Kind.Id()); headingIcon.Visible = headingIcon.Texture != null;
        headingIcon.TooltipText = world.Data.Buildings.TryGetValue(thing.Kind, out var def) ? def.Description : "";
    }

    /// <summary>How far away a building is, shown only while it is out of reach.</summary>
    private void Reach() {
        var label = Style.Text("", 14, Style.Warn); label.Visible = false; AddChild(label);
        // Rewritten only when the distance moves a tenth of a tile or the building comes into reach.
        live.Watch(() => world.Near(thing) ? -1 : (int)(world.Player.DistanceTo(world.GatherAt(thing, world.Player)) * 10), tenths => {
            label.Visible = tenths >= 0;
            if (tenths >= 0) label.Text = $"{tenths / 10}.{tenths % 10} tiles away · walk closer";
        });
    }

    /// <summary>The thing's slots in a grid of cups; click takes one item, Ctrl all of it.</summary>
    protected GridContainer StorageGrid(int columns = GridColumns, float size = 52) =>
        NewInventoryGrid(() => thing.Inventory, thing, id => moves.Stack(thing.Inventory, world.S.Backpack, id, thing), "Click: take one · Ctrl: take all · Drag to move", columns: columns, size: size);

    /// <summary>The backpack drawer, where a click stores into this thing.</summary>
    protected void StoreBagGrid(string hint, Func<string, bool>? wanted = null) => BagGrid(id => moves.Stack(world.S.Backpack, thing.Inventory, id, thing), hint, wanted: wanted);

    /// <summary>The backpack drawer, where a click loads this station.</summary>
    protected void LoadBagGrid(Func<string, bool> wanted) =>
        BagGrid(id => world.LoadInput(thing, id, ItemMoves.All ? world.Backpack[id] : 1), "Click: add one · Ctrl: all of it · Drag onto a slot", wanted: wanted);

    /// <summary>The funnel that opens the filter window beside the panel, ringed while it is open.</summary>
    protected void FilterButton() {
        var button = HeaderAction("filter", "Filters: what workers bring and take", () => { filters.Open = !filters.Open; filters.Picking = false; Redraw(); }, Style.FilterRound, "filters");
        if (!filters.Open) return;
        Style.Skin(button, Style.Round(Style.Mustard, 18).Item1, Style.FilterRound.Hover);
        panel.SideBoard(Style.Board, 340).AddChild(new FilterWindow(kit, thing, filters, Redraw));
    }

    /// <summary>The row of job buttons, for a place that offers jobs.</summary>
    protected void JobButtons() { if (world.Jobs(thing).Length > 0) AddChild(new JobRow(kit, live, thing, job, Redraw)); }
}
