using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The selected worker or thing: its name and icon, how far it is, and the panel for what it is.</summary>
public partial class InspectPage(SidePanel side) : PagePanel(side) {
    /// <summary>Names the page after the building it shows, with the building's own icon; its description becomes the icon's tooltip.</summary>
    private void Headline(ThingKind kind) {
        headingTitle.Text = world.Data.Name(kind);
        headingIcon.Texture = Icons.Of(kind.Id()); headingIcon.Visible = headingIcon.Texture != null;
        headingIcon.TooltipText = world.Data.Buildings.TryGetValue(kind, out var def) ? def.Description : "";
    }
    public override void _Ready() {
        if (state.WorkerId > 0) { AddChild(new WorkerPanel(panel, world.S.Workers.FirstOrDefault(w => w.Id == state.WorkerId))); return; }
        var t = world.Find(state.Selected);
        if (t == null) { AddLabel("Select a building or resource in the world."); return; }
        Headline(t.Kind);
        if (t.Kind == ThingKind.Water) headingTitle.Text = "Collect water";
        Reach(t);
        AddChild(t switch {
            { IsResource: true } => new ResourcePanel(panel, t),
            { Kind: ThingKind.Hut } => new CabinPanel(panel, t),
            { Tools: not null } => new BenchPanel(panel, t),
            { Kind: ThingKind.Signpost } => new MarketPanel(panel, t),
            { Kind: ThingKind.Mine } => new MinePanel(panel, t),
            { Kind: ThingKind.Farm } => new FarmPanel(panel, t),
            { Kind: ThingKind.Campfire } => new CampfirePanel(panel, t),
            { Kind: ThingKind.Packer } => new PackerPanel(panel, t),
            { Kind: ThingKind.Crate } => new CratePanel(panel, t),
            { Kind: ThingKind.Grave } => new RecoveryBoxPanel(panel, t),
            { Kind: ThingKind.Toolstation } => new ToolRackPanel(panel, t),
            { Kind: ThingKind.Pile } => new PilePanel(panel, t),
            { IsStorage: true } => new StoragePanel(panel, t),
            _ => new StationPanel(panel, t)
        });
    }
    /// <summary>How far away a building is, shown only while it is out of reach.</summary>
    private void Reach(Thing t) {
        var label = Style.Text("", 14, Style.Warn); label.Visible = false; content.AddChild(label);
        var text = new LiveText(shown => label.Text = shown);
        // Rewritten only when the distance moves a tenth of a tile or the building comes into reach.
        int drawn = int.MinValue;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(label)) return;
            int tenths = world.Near(t) ? -1 : (int)(world.Player.DistanceTo(world.GatherAt(t, world.Player)) * 10);
            if (tenths == drawn) return;
            drawn = tenths;
            label.Visible = tenths >= 0;
            if (tenths >= 0) { text.Clear().Append(tenths / 10).Append('.').Append(tenths % 10).Append(" tiles away · walk closer"); text.Show(); }
        });
    }
}
