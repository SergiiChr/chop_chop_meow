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
        if (t.IsResource) { Resource(t); return; }
        if (t.Kind == ThingKind.Hut) { Cabin(t); return; }
        if (t.Tools != null) { AddChild(new BenchPanel(panel, t)); return; }
        if (t.Kind == ThingKind.Signpost) { AddChild(new MarketPanel(panel, t)); return; }
        if (t.Kind == ThingKind.Mine) { AddChild(new MinePanel(panel, t)); return; }
        if (t.Kind == ThingKind.Farm) { FarmPanel(t); return; }
        if (t.Kind == ThingKind.Campfire) { Campfire(t); return; }
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
    protected void Resource(Thing t) {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Icon(Icons.Of(World.ResourceItem(t)), 44));
        var left = Style.Text("", 16, Style.Ink); left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; left.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(left);
        var text = new LiveText(shown => left.Text = shown);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(left)) return;
            var line = text.Clear();
            if (t.Kind != ThingKind.Water) line.Append(t.Remaining).Append(" left · ");
            line.Append(world.GatherBlock(t) ?? (t.IsLoose ? "right-click to pick it up" : "hold right mouse to gather"));
            text.Show();
        });
        if (t.Kind == ThingKind.Water) AddLabel("Collect water · hold right mouse on the pond shore to fill a bottle, or drink if you have none.", 14, Style.Beige);
        JobButtons(t);
    }
    protected void Campfire(Thing fire) {
        var row = Well(new HBoxContainer()); row.AddThemeConstantOverride("separation", 12);
        row.AddChild(Style.Glyph("line_flame", 30, Style.Ember));
        var light = Style.Text("", 16, Style.Ink); light.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(light);
        var text = new LiveText(shown => light.Text = shown);
        int drawn = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(light)) return;
            int minutes = (int)(fire.Burn / world.Data.DaySeconds * 1440);
            if (minutes == drawn) return;
            drawn = minutes;
            AppendGameHours(text.Clear(), fire.Burn).Append(" of light · wolves keep 5 tiles off"); text.Show();
        });
        var (wood, count) = ItemSlot.Cup("fire_wood"); wood.Icon = Icons.Of("wood"); row.AddChild(wood);
        int held = -1;
        live.Add(() => { if (GodotObject.IsInstanceValid(wood) && fire.Inventory["wood"] != held) { held = fire.Inventory["wood"]; count.Text = held > 0 ? held.ToString() : ""; } });
        AddButton($"Feed · 1 wood · +{world.Data.CampfireHours:0} h", () => world.FuelFire(fire));
        JobButtons(fire);
    }
    protected void Cabin(Thing hut) {
        var tag = Style.Pill("", Style.TagPill, Style.Rim, 13); tag.MouseFilter = Control.MouseFilterEnum.Pass; tag.TooltipText = "Wolves back off while you are inside";
        heading.AddChild(tag); heading.MoveChild(tag, heading.GetChildCount() - 2);
        // What a night's sleep would leave: each need now, and at dawn.
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 12);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); well.AddChild(top);
        top.AddChild(Style.Glyph("line_moon", 20, Style.Ink));
        var until = Style.Text("", 14, Style.Ink); until.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(until);
        var block = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 13); top.AddChild(block);
        var vials = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; vials.AddThemeConstantOverride("separation", 36); well.AddChild(vials);
        var now = new Vial[3]; var then = new Vial[3];
        var colors = new[] { Style.Brick, Style.Mustard, Style.Sky };
        for (int i = 0; i < 3; i++) {
            var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 8); vials.AddChild(pair);
            pair.AddChild(now[i] = new Vial { Fill = colors[i] });
            pair.AddChild(Style.Glyph("line_arrow", 16, Style.Ink));
            pair.AddChild(then[i] = new Vial { Fill = colors[i], Faded = true });
        }
        var buttons = Row(); buttons.AddThemeConstantOverride("separation", 12);
        var door = kit.BigButton("", Style.Sage, () => { if (world.S.Sheltered == hut.Id) world.Leave(); else state.Entering = hut; });
        var sleep = kit.BigButton("Sleep", Style.Enamel, () => world.Sleep(hut)); sleep.Name = "sleep";
        buttons.AddChild(door); buttons.AddChild(sleep);
        var untilText = new LiveText(shown => until.Text = shown);
        (bool Inside, string? Block, int Minutes) shown = (false, "", -1);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(sleep)) return;
            bool inside = world.S.Sheltered == hut.Id;
            string? blocked = world.SleepBlock();
            int minutes = (int)(world.SecondsToDawn / world.Data.DaySeconds * 1440);
            if ((inside, blocked, minutes) == shown) return;
            shown = (inside, blocked, minutes);
            tag.Text = inside ? "inside" : "outside";
            tag.AddThemeStyleboxOverride("normal", inside ? Style.TagPill : Style.BlueTag);
            door.Text = inside ? "Step outside" : "Go inside";
            sleep.TooltipText = blocked ?? "Sleep until dawn. Production, hunger, trade and wages advance.";
            sleep.Modulate = blocked == null ? Colors.White : Style.Unready;
            block.Visible = blocked != null; block.Text = blocked == null ? "" : ShortBlock(blocked);
            AppendGameHours(untilText.Clear().Append("until dawn · +"), world.SecondsToDawn); untilText.Show();
            float seconds = world.SecondsToDawn;
            Show(0, world.S.Health, world.S.Health);
            Show(1, world.S.Hunger, world.S.Hunger - seconds * world.Data.HungerPerSecond);
            Show(2, world.S.Thirst, world.S.Thirst - seconds * world.Data.ThirstPerSecond);
        });
        void Show(int i, float level, float after) {
            now[i].Level = level; then[i].Level = Math.Max(0, after); then[i].Warn = after <= 0;
            now[i].QueueRedraw(); then[i].QueueRedraw();
        }
    }
    /// <summary>A sleep refusal cut to a tag: what stops it, in a word or two.</summary>
    protected static string ShortBlock(string reason) => reason.Contains("thirsty") && reason.Contains("hungry") ? "too hungry and thirsty" : reason.Contains("thirsty") ? "too thirsty" : reason.Contains("hungry") ? "too hungry" : reason.Contains("night") ? "only at night" : "not yet";
    protected void Storage(Thing t) {
        switch (t.Kind) {
            case ThingKind.Crate: Crate(t); return;
            case ThingKind.Grave: Recovery(t); return;
            case ThingKind.Toolstation: ToolRack(t); return;
            case ThingKind.Pile: Pile(t); return;
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
    /// <summary>A crate: its fill out of twelve, whether the village would buy it packed, and how to pick it up.</summary>
    protected void Crate(Thing t) {
        var sells = Style.Pill("✓", Style.TagPill, Style.Rim, 13); sells.MouseFilter = Control.MouseFilterEnum.Pass; sells.TooltipText = "The village buys this crate packed";
        heading.AddChild(sells); heading.MoveChild(sells, heading.GetChildCount() - 2);
        var meter = Row(); meter.AddThemeConstantOverride("separation", 10);
        var cells = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; cells.AddThemeConstantOverride("separation", 4); meter.AddChild(cells);
        var segments = Enumerable.Range(0, t.Inventory.Capacity).Select(_ => new ColorRect { CustomMinimumSize = new Vector2(0, 12), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }).ToArray();
        foreach (var segment in segments) cells.AddChild(segment);
        var fill = Style.Text("", 14, Style.Rim); meter.AddChild(fill);
        var fillText = new LiveText(shown => fill.Text = shown);
        int drawn = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(fill) || t.Inventory.Count == drawn) return;
            drawn = t.Inventory.Count;
            for (int i = 0; i < segments.Length; i++) segments[i].Color = i < drawn ? Style.Mustard : new Color(Style.Rim, .25f);
            fillText.Clear().Append(drawn).Append(" / ").Append(t.Inventory.Capacity); fillText.Show();
            bool sellable = drawn > 0;
            foreach (var pair in t.Inventory.Items) sellable &= world.Data.Market.Sells.ContainsKey(pair.Key);
            sells.Visible = sellable;
        });
        Well(new VBoxContainer()).AddChild(StorageGrid(t));
        var hint = Row(); hint.AddThemeConstantOverride("separation", 8);
        hint.AddChild(Style.Text("Hold", 14, Style.Beige)); hint.AddChild(Style.Glyph("line_mouse", 24, Style.Rim)); hint.AddChild(Style.Text("to pick up", 14, Style.Beige));
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move");
    }
    protected void Recovery(Thing t) {
        headingTitle.Text = "Recovery box";
        Well(new VBoxContainer()).AddChild(StorageGrid(t, 5));
        var row = Row(); row.AddChild(kit.BigButton("Take all", Style.Brick, () => TakeAll(t)));
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Drag to move");
    }
    /// <summary>A tool rack: a row per kind of tool workers use, with what hangs there, how many are out, and who waits for one.</summary>
    protected void ToolRack(Thing t) {
        HeaderAction("stash", "Hang spare tools · keeps your best of each", () => {
            if (!world.Near(t)) { kit.Notify("Walk closer first."); return; }
            foreach (var id in world.S.Backpack.Items.Keys.Where(i => world.Holds(t, i) && t.Inventory.Accepts(i)).ToArray())
                world.Transfer(world.S.Backpack, t.Inventory, id, world.BestTool(world.Data.Items[id].Tool) == id ? world.S.Backpack[id] - 1 : world.S.Backpack[id]);
        }, Style.ActionRound, "deposit");
        FilterButton(t);
        var rows = Well(new VBoxContainer()); rows.AddThemeConstantOverride("separation", 0);
        // The four kinds workers fetch always have a row; any other tool hung here, such as a chisel, gets one too.
        var kinds = new List<string> { "axe", "pickaxe", "rake", "hammer" };
        foreach (var id in t.Inventory.Items.Keys) if (world.Data.Items.TryGetValue(id, out var def) && def.Tool != "" && !kinds.Contains(def.Tool)) kinds.Add(def.Tool);
        for (int k = 0; k < kinds.Count; k++) {
            string kind = kinds[k];
            if (k > 0) rows.AddChild(new HSeparator { CustomMinimumSize = new Vector2(0, 14) });
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 60) }; row.AddThemeConstantOverride("separation", 12); rows.AddChild(row);
            string plainest = world.Data.Items.Where(p => p.Value.Tool == kind).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
            var (cell, count) = ItemSlot.Cup($"rack_{kind}"); cell.Icon = Icons.Of(plainest); row.AddChild(cell);
            cell.Pressed += () => { if (world.BestTool(kind, t.Inventory) is string best) { kit.Act(() => moves.Stack(t.Inventory, world.S.Backpack, best, t)); kit.Refresh(); } };
            cell.Drag = () => world.BestTool(kind, t.Inventory) is string best ? new ItemDrag(best, t.Inventory[best], t.Inventory, t) : null;
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var outPill = Style.Pill("", Style.Tag(Style.Enamel), Style.Rim, 12); outPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(outPill);
            var waitPill = Style.Pill("", Style.Tag(Style.Brick), Style.Rim, 12); waitPill.MouseFilter = Control.MouseFilterEnum.Pass; row.AddChild(waitPill);
            (string? Best, int Held, int Out, int Waiting) shown = (null, -1, -1, -1);
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(cell)) return;
                int held = 0, out_ = 0, waiting = 0;
                foreach (var pair in t.Inventory.Items) if (world.Data.Items.TryGetValue(pair.Key, out var def) && def.Tool == kind) held += pair.Value;
                foreach (var w in world.S.Workers) { if (w.Tool != "" && world.Data.Items[w.Tool].Tool == kind) out_++; if (w.Missing == kind) waiting++; }
                var now = (world.BestTool(kind, t.Inventory), held, out_, waiting);
                if (now == shown) return;
                shown = now;
                cell.Icon = Icons.Of(now.Item1 ?? plainest);
                cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
                count.Text = held > 1 ? held.ToString() : "";
                cell.TooltipText = held > 0 ? $"{world.Data.Name(now.Item1!)}\nClick to take" : $"No {kind} on the rack";
                outPill.Visible = out_ > 0; outPill.Text = $"{out_} out";
                outPill.TooltipText = string.Join("\n", world.S.Workers.Where(w => w.Tool != "" && world.Data.Items[w.Tool].Tool == kind).Select(w => w.Name));
                waitPill.Visible = waiting > 0; waitPill.Text = $"{waiting} waiting";
                waitPill.TooltipText = string.Join("\n", world.S.Workers.Where(w => w.Missing == kind).Select(w => $"{w.Name} needs {A(kind)}"));
            });
        }
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: hang a tool · Ctrl: all of them · Drag to move", wanted: id => world.Holds(t, id) && t.Inventory.Accepts(id));
        JobButtons(t);
    }
    protected static string A(string word) => "aeiou".Contains(word[0]) ? $"an {word}" : $"a {word}";
    /// <summary>
    /// A pile: fourteen stacks of the one resource it holds, filled left to right and top to bottom, and a switch that turns it into compost.
    /// Compost shows the same grid; under its bar is what it is composting and how long that lasts, and the fertilizer it makes.
    /// </summary>
    protected void Pile(Thing t) {
        headingTitle.Text = t.Compost ? "Compost" : t.Inventory.First() is string held ? $"Pile · {world.Data.Name(held).ToLowerInvariant()}" : "Pile";
        if (t.Compost) { headingIcon.Texture = Icons.Of("leaves"); headingIcon.Visible = true; }
        var compost = new CheckButton { ButtonPressed = t.Compost, FocusMode = Control.FocusModeEnum.None, TooltipText = "Compost: turns leaves, grass and food into fertilizer", Name = "compost" };
        compost.Toggled += on => { if (!world.SetCompost(t, on)) compost.SetPressedNoSignal(t.Compost); else panel.Open("inspect"); };
        heading.AddChild(compost); heading.MoveChild(compost, heading.GetChildCount() - 2);
        if (t.Compost) FilterButton(t);
        else HeaderAction("stash", "Store everything this pile takes", () => StoreAll(t), Style.ActionRound, "deposit");
        Well(new VBoxContainer()).AddChild(StorageGrid(t, 7, 44));
        if (!t.Compost) {
            Dynamic(text => text.Append(t.Inventory.UsedSlots).Append(" of ").Append(t.Inventory.Slots).Append(" stacks"), 14, Style.Beige);
        } else {
            var item = Style.Icon(Icons.Of("leaves"), 40); item.MouseFilter = Control.MouseFilterEnum.Pass;
            var lasts = Style.Text("", 13, Style.Tan);
            var making = Style.Icon(Icons.Of("fertilizer"), 52); making.TooltipText = "Makes fertilizer"; making.MouseFilter = Control.MouseFilterEnum.Pass;
            var main = new VBoxContainer(); main.AddThemeConstantOverride("separation", 10); main.AddChild(making); main.AddChild(PerBatch("×1"));
            float hour = world.Data.DaySeconds / 24;
            Process(Array.Empty<Control>(), "1 h", () => t.Progress / hour, FuelRow("line_leaf", new Color("8fae7e"), item, lasts), main, Array.Empty<Control>());
            var lastsText = new LiveText(shown => lasts.Text = shown);
            (string? Item, int Hours) shown = ("", -1);
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(lasts)) return;
                string? rotting = null; int units = 0;
                foreach (var pair in t.Inventory.Items) if (world.Compostable(pair.Key)) { rotting ??= pair.Key; units += pair.Value; }
                var now = (rotting, units / World.CompostBatch);
                if (now == shown) return;
                shown = now;
                item.Texture = Icons.Of(rotting ?? "leaves"); item.Modulate = rotting == null ? Style.Unready : Colors.White;
                item.TooltipText = rotting == null ? "Nothing to compost" : $"Composting {world.Data.Name(rotting).ToLowerInvariant()}";
                lastsText.Clear().Append('×').Append(World.CompostBatch).Append(" · ").Append(now.Item2).Append(" h"); lastsText.Show();
            });
            var skip = new CheckBox { Text = "Skip food delivery", ButtonPressed = t.SkipFood, FocusMode = Control.FocusModeEnum.None, Name = "skip_food", TooltipText = "Workers bring no food here; food you put in yourself still composts" };
            skip.Toggled += on => { world.SetSkipFood(t, on); skip.SetPressedNoSignal(t.SkipFood); };
            content.AddChild(skip);
            JobButtons(t);
        }
        BagGrid(id => moves.Stack(world.S.Backpack, t.Inventory, id, t), "Click: store one · Ctrl: store all · Drag to move", wanted: id => world.Holds(t, id));
    }
    /// <summary>
    /// A farm plot: its growth from bare soil to ripe wheat, then the process row: seeds in, fertilizer as its fuel, wheat out with seeds and grass beside.
    /// </summary>
    protected void FarmPanel(Thing farm) {
        var stages = new HBoxContainer(); stages.AddThemeConstantOverride("separation", 0); content.AddChild(stages);
        string?[] looks = { null, "wheat_seeds", "grass", "wheat" };
        string[] names = { "Fallow", "Sown", "Growing", "Ripe" };
        var nodes = new PanelContainer[4]; var links = new ColorRect[3];
        for (int i = 0; i < 4; i++) {
            if (i > 0) { links[i - 1] = new ColorRect { CustomMinimumSize = new Vector2(0, 6), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; stages.AddChild(links[i - 1]); }
            var node = new PanelContainer { CustomMinimumSize = new Vector2(44, 44), TooltipText = names[i], MouseFilter = Control.MouseFilterEnum.Pass };
            node.AddChild(looks[i] is string look ? Style.Icon(Icons.Of(look), 30) : new ColorRect { Color = new Color("8a6a48"), CustomMinimumSize = new Vector2(24, 10), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            nodes[i] = node; stages.AddChild(node);
        }
        var ripe = Style.Text("", 14, Style.Rim); ripe.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; content.AddChild(ripe);
        var ripeText = new LiveText(shown => ripe.Text = shown);
        (int Stage, int Hours) drawn = (-1, -1);
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(ripe)) return;
            int at = farm.FarmStage == FarmStage.Ripe ? 3 : farm.FarmStage == FarmStage.Growing ? (farm.Progress < world.GrowSeconds / 3 ? 1 : 2) : 0;
            int hours = farm.FarmStage == FarmStage.Growing ? (int)((world.GrowSeconds - farm.Progress) / world.Data.DaySeconds * 24) : -1;
            if ((at, hours) == drawn) return;
            drawn = (at, hours);
            for (int i = 0; i < 4; i++) nodes[i].AddThemeStyleboxOverride("panel", Style.Round(i < at ? Style.Sage : i == at ? Style.Mustard : Style.Line, 22).Item1);
            for (int i = 0; i < 3; i++) links[i].Color = i < at ? Style.Sage : Style.Line;
            var line = ripeText.Clear();
            if (hours >= 0) line.Append("ripe in ").Append(hours / 24).Append(" d ").Append(hours % 24).Append(" h");
            else line.Append(farm.FarmStage == FarmStage.Ripe ? "ripe · hold right mouse on the plot to harvest" : "sows once seeds and fertilizer are in");
            ripeText.Show();
        });
        var output = farm.Output ?? farm.Inventory;
        var main = FarmCup(farm, output, "wheat", false, $"×{world.Data.Farm.Yield.GetValueOrDefault("wheat")}");
        var side = world.Data.Farm.Yield.Keys.Where(k => k != "wheat").Select(k => FarmCup(farm, output, k, false, $"×{world.Data.Farm.Yield[k]}", 44)).ToArray();
        var fertilizer = FarmCup(farm, farm.Inventory, "fertilizer", true, "", 52, Style.CupFuel);
        var lasts = Style.Text("", 13, Style.Tan);
        var lastsText = new LiveText(shown => lasts.Text = shown);
        int stock = -1;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(lasts) || farm.Inventory["fertilizer"] == stock) return;
            stock = farm.Inventory["fertilizer"];
            lastsText.Clear().Append("×").Append(world.Data.Farm.Sow.GetValueOrDefault("fertilizer")).Append(" · ").Append(stock / Math.Max(1, world.Data.Farm.Sow.GetValueOrDefault("fertilizer"))).Append(stock == 1 ? " crop" : " crops"); lastsText.Show();
        });
        var inputs = world.Data.Farm.Sow.Keys.Where(k => k != "fertilizer").Select(k => FarmCup(farm, farm.Inventory, k, true, $"×{world.Data.Farm.Sow[k]}"));
        Process(inputs, $"{world.Data.Farm.GrowDays:0} d", () => farm.FarmStage == FarmStage.Growing ? farm.Progress / world.GrowSeconds : farm.FarmStage == FarmStage.Ripe ? 1 : 0,
            FuelRow("line_leaf", new Color("8fae7e"), FuelCup(fertilizer), lasts), main, side);
        BagGrid(id => moves.Stack(world.S.Backpack, farm.Inventory, id, farm), "Click: sow seeds or fertilizer · Drag to move", wanted: id => world.Holds(farm, id));
        JobButtons(farm);
    }
    /// <summary>Takes a cup out of the column it was built in, for the fuel row, and frees the column.</summary>
    protected static Control FuelCup(Control column) {
        var cup = column.GetChild<Control>(0); column.RemoveChild(cup); column.QueueFree();
        return cup;
    }
    /// <summary>A cup bound to one item of a farm's sowing or harvest slots, with what one crop takes or gives under it.</summary>
    protected VBoxContainer FarmCup(Thing farm, Inventory box, string item, bool input, string perBatch, float size = 52, StyleBoxFlat? look = null) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, count) = ItemSlot.Cup($"{(input ? "input" : "output")}_{item}", size);
        cell.Icon = Icons.Of(item);
        if (look != null) cell.AddThemeStyleboxOverride("normal", look); else if (!input) cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
        cell.Pressed += () => { kit.Act(() => moves.Stack(box, world.S.Backpack, item, farm)); kit.Refresh(); };
        cell.Drag = () => box[item] > 0 ? new ItemDrag(item, box[item], box, farm) : null;
        if (input) {
            // Seed comes back in from the plot's own harvest as well as from the backpack.
            cell.CanTake = drag => (drag.From == world.S.Backpack || drag.From == farm.Output) && drag.Item == item;
            cell.Take = drag => { int left = 0; kit.Act(() => left = moves.Drag(drag, box, farm)); kit.Refresh(); return left; };
        }
        int drawn = -1;
        live.Add(() => {
            int held = box[item];
            if (held == drawn) return;
            drawn = held;
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held > 0 ? Colors.White : Style.Ghost);
            count.Text = held > 0 ? held.ToString() : "";
            cell.TooltipText = $"{world.Data.Name(item)} · {held}\n{(input ? "Click: take back · Drag seeds here from the harvest or backpack to sow" : "Click: take · Drag seeds to the sowing slot")}";
        });
        column.AddChild(cell);
        if (perBatch != "") column.AddChild(PerBatch(perBatch));
        return column;
    }
    /// <summary>Burn time as in-game hours and minutes.</summary>
    protected StringBuilder AppendGameHours(StringBuilder text, float seconds) {
        int minutes = (int)(seconds / world.Data.DaySeconds * 1440);
        return text.Append(minutes / 60).Append(" h ").Append(minutes % 60).Append(" min");
    }
}
