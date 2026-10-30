using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>What a page opens as, and the pages of plain buildings.</summary>
public partial class PagePanel {
    /// <summary>Which page this is: a name from the side panel, or "inspect".</summary>
    public override void _Ready() {
        switch (pageName) {
            case "welcome": Welcome(); break;
            case "help": AddChild(new GuidePanel(panel)); break;
            case "build": BuildingPlans(); break;
            case "inventory": Backpack(); break;
            case "workers": AddChild(new CrewPanel(panel)); break;
            case "craft": AddChild(new CraftPanel(panel)); break;
            default: Inspect(); break;
        }
    }
    /// <summary>Names the page after the building it shows, with the building's own icon; its description becomes the icon's tooltip.</summary>
    protected void Headline(ThingKind kind) {
        headingTitle.Text = world.Data.Name(kind);
        headingIcon.Texture = Icons.Of(kind.Id()); headingIcon.Visible = headingIcon.Texture != null;
        headingIcon.TooltipText = world.Data.Buildings.TryGetValue(kind, out var def) ? def.Description : "";
    }
    /// <summary>Shows the backpack drawer with a grid that works with the open page: <paramref name="wanted"/> lights up what it takes.</summary>
    protected void BagGrid(Action<string> click, string hint, Func<ItemDrag, bool>? accepts = null, Func<string, bool>? wanted = null) {
        var host = content; content = bag.Content;
        AddInventoryGrid(() => world.S.Backpack, null, click, hint, accepts, wanted, BagColumns);
        content = host; bag.Visible = !bag.Dismissed;
    }
    protected void Welcome() {
        AddLabel("The woods are quiet. For now.", 21);
        AddLabel("First, secure water, a cabin and a fire. Then use your steel axe and your hands to build a workshop, trade berries for a pickaxe, and work toward steel.", 16, Style.Beige);
        AddLabel("FIRST STEPS", 14);
        AddLabel("1  Collect water at the pond and keep a bottle filled. The sea is salt.\n2  Gather wood and stone; build a cabin and campfire before night.\n3  With your steel axe, fell trees. Gather stone and berries by hand. Knap a chisel, carve planks and a mallet, then build a bench.\n4  Make crates, build a trading post and sell berries. Buy your first pickaxe, then mine toward iron and steel.\n5  Press Esc any time to see your current goal.");
        AddLabel("A bottle holds three sips and can be refilled at the lake. Without one, drink at the source. Each gathering action takes time, so plan ahead.", 15, Style.Beige);
        AddButton("Into the clearing", panel.Close);
    }
    protected HBoxContainer Row() { var r = new HBoxContainer(); content.AddChild(r); return r; }
    protected void AddLabel(string text, int size = 15, Color? color = null) => content.AddChild(Style.Wrapped(text, size, color));
    protected void AddButton(string text, Action action, bool disabled = false) => content.AddChild(kit.Button(text, action, disabled));
    /// <summary>A label whose text <paramref name="write"/> rebuilds on every refresh.</summary>
    protected void Dynamic(Action<StringBuilder> write, int size = 15, Color? color = null) {
        var l = Style.Wrapped("", size, color); content.AddChild(l);
        var text = new LiveText(shown => l.Text = shown);
        void Update() {
            if (!GodotObject.IsInstanceValid(l)) return;
            write(text.Clear()); text.Show();
        }
        Update(); live.Add(Update);
    }
    protected string Needs(Dictionary<string, int> cost) => LiveText.Of(text => AppendNeeds(text, cost));
    protected string Cost(Dictionary<string, int> cost) => LiveText.Of(text => AppendCost(text, cost));
    /// <summary>Each input with how many the backpack holds of how many are needed.</summary>
    protected void AppendNeeds(StringBuilder text, Dictionary<string, int> cost) {
        bool first = true;
        foreach (var (item, amount) in cost) {
            if (!first) text.Append("  ·  ");
            first = false;
            text.Append($"{world.Data.Name(item)} {world.S.Backpack[item]}/{amount}");
        }
    }
    protected void AppendCost(StringBuilder text, Dictionary<string, int> cost) {
        bool first = true;
        foreach (var (item, amount) in cost) {
            if (!first) text.Append("  ·  ");
            first = false;
            text.Append($"{amount} {world.Data.Name(item)}");
        }
    }
    protected void BuildingPlans() {
        var grid = AddGrid();
        var details = Style.Wrapped("Hover a plan to see what it needs. Click it, then click open ground nearby.", 14, Style.Beige);
        foreach (var (id, def) in world.Data.Buildings) {
            var (cell, _) = ItemSlot.Cup($"build_{id.Id()}"); cell.Icon = Icons.Of(id.Id()); cell.TooltipText = $"{def.Name}\nClick to place · Drag onto the hotbar"; DragReference(cell, id.Id());
            cell.Pressed += () => { view.BuildMode = id; state.AssigningWorker = 0; panel.Close(); kit.Refresh(); };
            cell.MouseEntered += () => details.Text = $"{def.Name}\n{def.Description}\n\nNeeds: {Needs(def.Cost)}";
            live.Add(() => { if (GodotObject.IsInstanceValid(cell)) cell.Modulate = world.S.Backpack.Has(def.Cost) ? Colors.White : Style.Unready; });
            grid.AddChild(cell);
        }
        content.AddChild(details);
    }
    /// <summary>The backpack is only the drawer; the side panel stays hidden while it shows.</summary>
    protected void Backpack() {
        BagGrid(UseFromBag, UseHint);
        // Only the drawer shows, so the camera keeps the player centred.
        panel.Visible = false; view.PanelOpen = false;
    }
    protected const string UseHint = "Food and water: click to consume. Crates: click to place. Torch: click to light. Drag onto the hotbar to assign.";
    protected void UseFromBag(string id) {
        if (Water.IsBottle(id) || world.Data.IsConsumable(id)) world.Consume(id); else if (World.IsCrate(id)) panel.PlaceCrate(id); else if (id == "torch" && !world.S.TorchLit) world.UseTorch(Vector2.Zero);
    }
    protected void Inspect() {
        if (state.WorkerId > 0) { AddChild(new WorkerPanel(panel, world.S.Workers.FirstOrDefault(w => w.Id == state.WorkerId))); return; }
        var t = world.Find(state.Selected);
        if (t == null) { AddLabel("Select a building or resource in the world."); return; }
        Headline(t.Kind);
        if (t.Kind == ThingKind.Water) headingTitle.Text = "Collect water";
        Reach(t);
        if (t.IsResource) { AddChild(new ResourcePanel(panel, t)); return; }
        if (t.Kind == ThingKind.Hut) { AddChild(new CabinPanel(panel, t)); return; }
        if (t.Tools != null) { AddChild(new BenchPanel(panel, t)); return; }
        if (t.Kind == ThingKind.Signpost) { AddChild(new MarketPanel(panel, t)); return; }
        if (t.Kind == ThingKind.Mine) { AddChild(new MinePanel(panel, t)); return; }
        if (t.Kind == ThingKind.Farm) { AddChild(new FarmPanel(panel, t)); return; }
        if (t.Kind == ThingKind.Campfire) { AddChild(new CampfirePanel(panel, t)); return; }
        if (t.Kind == ThingKind.Packer) { AddChild(new PackerPanel(panel, t)); return; }
        if (t.IsStorage) { Storage(t); return; }
        AddChild(new StationPanel(panel, t));
    }
    /// <summary>How far away a building is, shown only while it is out of reach.</summary>
    protected void Reach(Thing t) {
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
    protected void Storage(Thing t) {
        switch (t.Kind) {
            case ThingKind.Crate: AddChild(new CratePanel(panel, t)); return;
            case ThingKind.Grave: Recovery(t); return;
            case ThingKind.Toolstation: AddChild(new ToolRackPanel(panel, t)); return;
            case ThingKind.Pile: AddChild(new PilePanel(panel, t)); return;
        }
        HeaderAction("stash", "Store all materials · keeps tools", () => StoreAll(t), Style.ActionRound, "deposit");
        if (t.Kind == ThingKind.Chest) FilterButton(t);
        Well(new VBoxContainer()).AddChild(StorageGrid(t));
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move");
    }
    /// <summary>A storage's slots in a grid of cups; click takes one item, Ctrl all of it.</summary>
    protected GridContainer StorageGrid(Thing t, int columns = GridColumns, float size = 52) {
        var host = content; var column = new VBoxContainer(); content = column;
        var grid = AddInventoryGrid(() => t.Inventory, t, id => moves.Stack(t.Inventory, world.S.Backpack, id, t), "Click: take one · Ctrl: take all · Drag to move", columns: columns, size: size);
        content = host; column.RemoveChild(grid); column.QueueFree();
        return grid;
    }
    /// <summary>Moves every material the storage takes from the backpack; tools stay, and so do crates, so one is never packed inside another.</summary>
    protected void StoreAll(Thing t) {
        if (!world.Near(t)) { kit.Notify("Walk closer first."); return; }
        foreach (var id in world.S.Backpack.Items.Keys.Where(i => world.Data.Items.TryGetValue(i, out var def) && def.Tool == "" && i != "crate").ToArray())
            if (world.Holds(t, id) && t.Inventory.Accepts(id)) world.Transfer(world.S.Backpack, t.Inventory, id, 999);
    }
    protected void TakeAll(Thing t) {
        if (!world.Near(t)) { kit.Notify("Walk closer first."); return; }
        foreach (var id in t.Inventory.Items.Keys.ToArray()) world.Transfer(t.Inventory, world.S.Backpack, id, 999);
    }
    protected void Recovery(Thing t) {
        headingTitle.Text = "Recovery box";
        Well(new VBoxContainer()).AddChild(StorageGrid(t, 5));
        var row = Row(); row.AddChild(kit.BigButton("Take all", Style.Brick, () => TakeAll(t)));
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Drag to move");
    }
    /// <summary>Burn time as in-game hours and minutes.</summary>
    protected StringBuilder AppendGameHours(StringBuilder text, float seconds) {
        int minutes = (int)(seconds / world.Data.DaySeconds * 1440);
        return text.Append(minutes / 60).Append(" h ").Append(minutes % 60).Append(" min");
    }
}
