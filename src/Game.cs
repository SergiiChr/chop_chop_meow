using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

public partial class Game : Node
{
    private World world = null!;
    private WorldView view = null!;
    private Control ui = null!;
    private PanelContainer panel = null!;
    private VBoxContainer content = null!;
    private Label stats = null!;
    private Label toast = null!;
    private Label modeLabel = null!;
    private Label deathLabel = null!;
    private readonly List<Action> live = new();
    private string page = "welcome";
    private int selected;
    private int workerId;
    private int assigningWorker;
    private Thing? gathering;
    private bool paused;
    private float uiClock;
    private float toastClock;
    private float autosave;
    private float runTime;
    private bool capturing;
    private string marketItem = "steel_tools";
    private int quantity = 1;
    private static readonly Color Cream = new("f2e8cf");
    private static readonly Color Muted = new("b7c6ae");
    private static string SavePath => OS.GetCmdlineUserArgs().Contains("--showcase") ? "user://review-settlement.json" : OS.GetCmdlineUserArgs().Contains("--ui-test") ? "user://ui-check.json" : "user://settlement.json";
    public override void _Ready()
    {
        try
        {
            var data = GameData.Load(); world = new World(data);
            if (OS.GetCmdlineUserArgs().Contains("--self-test")) { SimulationChecks.Run(data); GetTree().Quit(); return; }
            world.Notify = Notify;
            LoadHotbarAtlases();
            view = new WorldView { World = world }; AddChild(view);
            var layer = new CanvasLayer(); AddChild(layer);
            ui = new Control { Name = "Interface" }; ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); layer.AddChild(ui); ui.MouseFilter = Control.MouseFilterEnum.Ignore;
            SetTheme(); BuildHud(); OpenPage("welcome");
            if (OS.GetCmdlineUserArgs().Contains("--showcase")) Showcase();
            Notify("Welcome. WASD to move; hold right mouse to approach and gather. Tab shows the field guide.");
            if (OS.GetCmdlineUserArgs().Contains("--ui-test")) RunUIChecks();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private void SetTheme()
    {
        var theme = new Theme();
        theme.DefaultFontSize = 15;
        var box = Box(new Color("263e34"), 10, 14);
        theme.SetStylebox("panel", "PanelContainer", box);
        theme.SetStylebox("normal", "Button", Box(new Color("405b47"), 5, 9));
        theme.SetStylebox("hover", "Button", Box(new Color("597252"), 5, 9));
        theme.SetStylebox("pressed", "Button", Box(new Color("7c8550"), 5, 9));
        theme.SetStylebox("disabled", "Button", Box(new Color("34483c"), 5, 9));
        theme.SetColor("font_color", "Label", Cream); theme.SetColor("font_color", "Button", Cream);
        theme.SetColor("font_disabled_color", "Button", new Color("82917e"));
        theme.SetColor("font_color", "CheckBox", Cream);
        theme.SetConstant("separation", "VBoxContainer", 9);
        theme.SetConstant("separation", "HBoxContainer", 8);
        theme.SetStylebox("normal", "OptionButton", Box(new Color("405b47"), 5, 10));
        ui.Theme = theme;
    }
    private static StyleBoxFlat Box(Color color, int radius, int margin) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = margin,
        ContentMarginRight = margin,
        ContentMarginTop = margin,
        ContentMarginBottom = margin,
        BorderWidthBottom = 1,
        BorderColor = new Color("738363")
    };
    private Label Label(string text, int size = 15, Color? color = null)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", size); if (color != null) l.AddThemeColorOverride("font_color", color.Value); return l;
    }
    private Button Button(string text, Action action, bool disabled = false)
    {
        var b = new Button { Text = text, Disabled = disabled, MouseDefaultCursorShape = Control.CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 34), FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () => { action(); RefreshLive(); }; return b;
    }
    private HBoxContainer Row() { var r = new HBoxContainer(); content.AddChild(r); return r; }
    private void AddLabel(string text, int size = 15, Color? color = null) => content.AddChild(Label(text, size, color));
    private void AddButton(string text, Action action, bool disabled = false) => content.AddChild(Button(text, action, disabled));
    private void Dynamic(Func<string> get, int size = 15, Color? color = null)
    {
        var l = Label(get(), size, color); content.AddChild(l); live.Add(() => { if (GodotObject.IsInstanceValid(l)) l.Text = get(); });
    }
    private void BuildHud()
    {
        var clock = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -374, OffsetRight = -16, OffsetTop = 14, OffsetBottom = 52, MouseFilter = Control.MouseFilterEnum.Ignore };
        clock.AddThemeStyleboxOverride("panel", Box(new Color(.14f, .23f, .18f, .89f), 8, 9)); ui.AddChild(clock);
        stats = Label("", 15); stats.HorizontalAlignment = HorizontalAlignment.Center; clock.AddChild(stats);
        modeLabel = Label("", 15); modeLabel.OffsetLeft = 20; modeLabel.OffsetTop = 18; modeLabel.OffsetRight = 550; modeLabel.OffsetBottom = 73; modeLabel.MouseFilter = Control.MouseFilterEnum.Ignore; ui.AddChild(modeLabel);
        var bottom = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1, OffsetLeft = 16, OffsetRight = -16, OffsetTop = -92, OffsetBottom = -14 }; ui.AddChild(bottom);
        var slots = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; bottom.AddChild(slots);
        slots.AddChild(MenuIcon("hammer", "Build · B", () => OpenPage("build"))); slots.AddChild(MenuIcon("backpack", "Inventory · I", () => OpenPage("inventory")));
        slots.AddChild(MenuIcon("hardhat", "Workers", () => OpenPage("workers"))); slots.AddChild(MenuIcon("book", "Guide · Tab", () => OpenPage("help")));
        slots.AddChild(new VSeparator { CustomMinimumSize = new Vector2(24, 0) });
        for (int i = 0; i < 9; i++) { int slot = i; var button = Button("", () => UseHotbar(slot)); ConfigureHotbarButton(button, slot); slots.AddChild(button); }
        toast = Label("", 15); toast.AnchorTop = 1; toast.AnchorBottom = 1; toast.OffsetLeft = 22; toast.OffsetTop = -140; toast.OffsetRight = 850; toast.OffsetBottom = -100; toast.MouseFilter = Control.MouseFilterEnum.Ignore; ui.AddChild(toast);
        panel = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = -374, OffsetRight = -16, OffsetTop = 64, OffsetBottom = -106 };
        ui.AddChild(panel);
        deathLabel = Label("", 26); deathLabel.AutowrapMode = TextServer.AutowrapMode.Off; deathLabel.AddThemeConstantOverride("outline_size", 8); deathLabel.AddThemeColorOverride("font_outline_color", new Color("1c2b24")); deathLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center); deathLabel.GrowHorizontal = Control.GrowDirection.Both;
        deathLabel.HorizontalAlignment = HorizontalAlignment.Center; deathLabel.MouseFilter = Control.MouseFilterEnum.Ignore; ui.AddChild(deathLabel);
        BuildEnding(); BuildMenu(); RefreshLive();
    }
    private void Notify(string message)
    {
        if (toast != null) { toast.Text = message; toastClock = 7; }
        GD.Print(message);
    }
    private void RefreshLive()
    {
        if (world == null || stats == null) return;
        int hour = (int)(world.DayPhase * 24 + 6) % 24;
        stats.Text = $"Day {world.Day} · {hour:00}:{(int)(world.DayPhase * 1440) % 60:00}  {(world.Night ? "NIGHT" : "DAYLIGHT")}";
        deathLabel.Text = world.Dead ? $"You collapsed\nRespawning in {MathF.Ceiling(world.S.RespawnIn):0}s" : "";
        modeLabel.Text = assigningWorker > 0 ? "WORK ORDER · Click a resource to assign this villager. Esc cancels." : view.BuildMode != "" ? $"PLACE {world.Data.Name(view.BuildMode).ToUpperInvariant()} · Left click ground. Esc cancels." : "";
        foreach (var update in live.ToArray()) update();
        RefreshHotbar();
    }
    private void OpenPage(string name)
    {
        page = name; live.Clear(); panel.Visible = true; view.PanelOpen = true;
        foreach (var child in panel.GetChildren()) { panel.RemoveChild(child); child.QueueFree(); }
        var outer = new VBoxContainer(); panel.AddChild(outer);
        var heading = new HBoxContainer(); outer.AddChild(heading);
        var title = Label(name switch { "welcome" => "A NEW BEGINNING", "build" => "BUILDING PLANS", "inventory" => "YOUR BACKPACK", "hotbar" => "HOTBAR ASSIGNMENT", "help" => "FIELD GUIDE", "workers" => "YOUR NEIGHBORS", "filters" => "STORAGE FILTERS", _ => "INSPECT" }, 20);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; heading.AddChild(title); heading.AddChild(Button("×", ClosePanel));
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; outer.AddChild(scroll);
        content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(content);
        switch (name)
        {
            case "welcome": Welcome(); break;
            case "help": Help(); break;
            case "build": BuildingPlans(); break;
            case "inventory": Backpack(); break;
            case "hotbar": HotbarAssignment(); break;
            case "workers": Workers(); break;
            case "filters": Filters(); break;
            default: Inspect(); break;
        }
        RefreshLive();
    }
    private void ClosePanel() { panel.Visible = false; view.PanelOpen = false; page = ""; live.Clear(); }
    private void Welcome()
    {
        AddLabel("The woods are quiet. For now.", 21);
        AddLabel("A small settlement begins with your own two hands. Shape iron, master the bellows, and send your first steel tools to the village.", 16, Muted);
        AddLabel("FIRST STEPS", 14);
        AddLabel("1  Hold right mouse on a tree or stone. You will walk over and gather.\n2  Press B to build your stone furnace.\n3  Pick up loose iron northeast of the clearing.\n4  Press Esc any time to see your current goal.");
        AddLabel("You begin with an iron axe, 10 berries and 6 wood. Drink at the pond to the west; gather water to carry. A fire and hut protect you after dark.", 15, Muted);
        AddButton("Into the clearing", ClosePanel);
        if (Godot.FileAccess.FileExists(SavePath)) AddButton("Continue saved settlement", () => { Load(); ClosePanel(); });
        AddLabel("10–15 minute POC · single player · Linux", 13, Muted);
    }
    private void Help()
    {
        AddLabel("MOVE & WORK", 16);
        AddLabel("WASD / arrows: move in screen directions\nHold right mouse: approach and gather\nLeft click: select resource, building or worker\nE: inspect nearest object\nSpace: use carried tool; at selected bellows furnace, pump\nMouse wheel: zoom\n1–9: use hotbar slot (berries and water consume; building plans place)\nB: build   I: inventory   Tab: guide\nF5: save   F9: load   Esc: cancel / pause");
        AddLabel("IRON → STEEL", 16);
        AddLabel("Loose iron needs no pickaxe. Smelt 2 ore + 1 wood/coal per iron bar. Build an anvil and forge an iron pickaxe to mine real coal. Steel needs the reinforced furnace: 2 iron bars + 2 mined coal, plus 1 mined coal as fuel. Pump regularly above 80°. Forge 2 steel bars + 2 wood into steel tools.", 15, Muted);
        AddLabel("VILLAGERS & STORAGE", 16);
        AddLabel("Hire at a signpost (24 coins; 8/day). Worker → Work → click a resource. Building → Assign → select named villagers. Put inputs in storage for hauling. One furnace worker alternates hauling and pumping; with two, the first pumps while the second hauls. No accepting chest means work pauses safely.", 15, Muted);
        AddLabel("Empty filters allow everything. Input filters control worker deposits; output filters control worker withdrawals. Workers prefer explicitly matching input-filter chests before the nearest general chest. Manual transfers ignore automation filters. Hold Shift while transferring to move a full stack.", 15, Muted);
        AddLabel("STAY ALIVE", 16);
        AddLabel("Food and water drain over time. Wolves fear campfires (5-tile radius) and huts. Feed fires with wood; each log adds 90s. Sleep in a hut at night; production, hunger, trade and wages advance. Death leaves your inventory in a recovery box and returns you to the original clearing. Unpaid workers strike until paid or dismissed.", 15, Muted);
        AddLabel("Trade returns in 30 seconds. Prices fall when you sell and slowly recover. Sale proceeds arrive with the returning coin cart. Your world autosaves every 90 seconds; F5 saves immediately.", 15, Muted);
    }
    private string Needs(Dictionary<string, int> cost) => string.Join("  ·  ", cost.Select(p => $"{world.Data.Name(p.Key)} {world.S.Backpack[p.Key]}/{p.Value}"));
    private string Cost(Dictionary<string, int> cost) => string.Join("  ·  ", cost.Select(p => $"{p.Value} {world.Data.Name(p.Key)}"));
    private void BuildingPlans()
    {
        var grid = AddGrid();
        var details = Label("Hover a plan to see what it needs. Click it, then click open ground nearby.", 14, Muted);
        foreach (var (id, def) in world.Data.Buildings)
        {
            var (cell, _) = IconCell($"build_{id}"); cell.Icon = HotbarIcon(id); cell.TooltipText = def.Name;
            cell.Pressed += () => { view.BuildMode = id; assigningWorker = 0; ClosePanel(); RefreshLive(); };
            cell.MouseEntered += () => details.Text = $"{def.Name}\n{def.Description}\n\nNeeds: {Needs(def.Cost)}";
            live.Add(() => { if (GodotObject.IsInstanceValid(cell)) cell.Modulate = world.S.Backpack.Has(def.Cost) ? Colors.White : new Color(1, .7f, .65f); });
            grid.AddChild(cell);
        }
        content.AddChild(details);
    }
    private void Backpack()
    {
        Dynamic(() => $"Coins {world.S.Coins:0.0}   Health {world.S.Health:0}\nHunger {world.S.Hunger:0}   Thirst {world.S.Thirst:0}\nCapacity {world.S.Backpack.UsedSlots}/{world.S.Backpack.Slots} slots   Daily wages {world.S.Workers.Count * world.Data.DailyWage:0}", 15);
        AddInventoryGrid(() => world.S.Backpack, id => { if (id is "berries" or "water") world.Consume(id); }, "Berries and water: click to consume. Tools work while carried.");
        AddButton("Assign hotbar slots", () => OpenPage("hotbar"));
    }
    private void Select(Thing t) { selected = t.Id; workerId = 0; view.Selected = t.Id; view.SelectedWorker = 0; view.BuildMode = ""; OpenPage("inspect"); }
    private void Select(Worker w) { workerId = w.Id; selected = 0; view.Selected = 0; view.SelectedWorker = w.Id; OpenPage("inspect"); }
    private void Inspect()
    {
        if (workerId > 0) { WorkerPanel(world.S.Workers.FirstOrDefault(w => w.Id == workerId)); return; }
        var t = world.Find(selected);
        if (t == null) { AddLabel("Select a building or resource in the world."); return; }
        AddLabel(t.Kind == "loose_iron" ? "Loose iron ore" : t.Kind == "grave" ? "Recovery box" : world.Data.Name(t.Kind), 23);
        Dynamic(() => world.Near(t) ? "Within reach" : $"{world.Player.DistanceTo(t.Pos):0.0} tiles away · walk closer", 13, Muted);
        if (t.IsResource)
        {
            Dynamic(() => $"{t.Remaining} remaining\n{world.GatherBlock(t) ?? "Hold right mouse on this resource to gather."}", 15);
            if (t.Kind == "water") AddButton("Drink from the pond", () => { if (world.Near(t)) { world.S.Thirst = 100; Notify("Cool, fresh water."); } else Notify("Walk closer to the pond."); });
            AddLabel("Select a villager and choose Work to automate this resource. Their gathered supplies go to accepting storage.", 14, Muted); return;
        }
        if (t.Kind == "hut") { AddButton("Sleep until dawn", () => world.Sleep(t)); AddLabel("Shelter keeps nearby wolves at bay. Sleeping advances production, hunger, trade and wages.", 14, Muted); return; }
        if (t.Kind == "signpost") { Market(t); return; }
        if (t.IsStorage) { Storage(t); return; }
        if (t.Kind == "campfire")
        {
            Dynamic(() => $"Light remaining: {t.Burn:0}s\nProtection radius: 5 tiles", 16); AddButton("Feed fire · 1 wood / +90s", () => world.FuelFire(t)); Assignment(t); return;
        }
        if (world.Data.Buildings.TryGetValue(t.Kind, out var bd)) AddLabel(bd.Description, 14, Muted);
        var recipes = world.Data.Recipes.Where(p => p.Value.Station == t.Kind).ToList();
        if (recipes.Count > 1)
        {
            var opt = new OptionButton { CustomMinimumSize = new Vector2(0, 38), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var r in recipes) opt.AddItem(r.Value.Name);
            opt.Selected = Math.Max(0, recipes.FindIndex(p => p.Key == t.Recipe));
            opt.ItemSelected += index => { world.SetRecipe(t, recipes[(int)index].Key); OpenPage("inspect"); }; content.AddChild(opt);
        }
        else if (recipes.Count == 1) AddLabel(recipes[0].Value.Name.ToUpperInvariant(), 16);
        if (t.Kind == "furnace") AddButton($"Fuel: {world.Data.Name(t.Fuel)} · click to switch", () => { if (!t.Processing) { t.Fuel = t.Fuel == "wood" ? "coal" : "wood"; OpenPage("inspect"); } });
        if (t.Recipe != "")
        {
            Dynamic(() => $"Batch: {Cost(world.BatchCost(t))}\n{(t.Processing ? $"Working · {t.Progress:0.0}/{world.Data.Recipes[t.Recipe].Seconds:0}s" : "Waiting for ingredients / temperature")}", 14, Muted);
            var row = Row(); row.AddChild(Button("Load 1 batch", () => world.LoadBatch(t))); row.AddChild(Button("Load 5", () => world.LoadBatch(t, 5)));
            AddButton("Collect finished goods", () => world.Collect(t));
        }
        if (t.Kind == "reinforced") { Dynamic(() => $"TEMPERATURE   {t.Heat:0}° / 80° required", 17, new Color("edc17c")); AddButton("Pump bellows · Space", () => world.Pump(t)); }
        AddLabel("INSIDE", 14);
        AddInventoryGrid(() => t.Inventory, id => MoveStack(t.Inventory, world.S.Backpack, id, t), "Click: take a stack · Shift: take all");
        Assignment(t);
    }
    private string Items(Inventory inv) => inv.Count == 0 ? "empty" : string.Join(", ", inv.Items.Select(p => $"{p.Value} {world.Data.Name(p.Key)}"));
    private void Storage(Thing t)
    {
        Dynamic(() => $"{t.Inventory.UsedSlots}/{t.Inventory.Slots} slots", 14, Muted);
        AddInventoryGrid(() => t.Inventory, id => MoveStack(t.Inventory, world.S.Backpack, id, t), "Click: take a stack · Shift: take all");
        AddLabel("BACKPACK", 14);
        AddInventoryGrid(() => world.S.Backpack, id => MoveStack(world.S.Backpack, t.Inventory, id, t), "Click: store a stack · Shift: store all");
        if (t.Kind == "chest") AddButton("Input / output filters", () => OpenPage("filters"));
        AddButton("Deposit all materials (keep tools)", () => { if (world.Near(t)) foreach (var id in world.S.Backpack.Items.Keys.Where(i => i is not "iron_axe" and not "iron_pick" and not "steel_tools").ToArray()) world.S.Backpack.TransferTo(t.Inventory, id, 999); });
        AddButton("Take everything", () => { if (world.Near(t)) foreach (var id in t.Inventory.Items.Keys.ToArray()) t.Inventory.TransferTo(world.S.Backpack, id, 999); });
    }
    private void Filters()
    {
        var t = world.Find(selected); if (t == null || !t.IsStorage) return;
        AddLabel("Separate automation allowlists. Empty column = allow all items. Explicit input matches have priority over general storage.", 14, Muted);
        var header = Row(); var l = Label("ITEM", 13); l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; header.AddChild(l); header.AddChild(Label("IN       OUT", 13));
        foreach (var p in world.Data.Items)
        {
            string id = p.Key; var row = Row(); var label = Label(p.Value.Name, 14); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(label);
            var input = new CheckBox { ButtonPressed = t.Inventory.InputFilter.Contains(id), TooltipText = "Allow worker deposits", FocusMode = Control.FocusModeEnum.None };
            var output = new CheckBox { ButtonPressed = t.Inventory.OutputFilter.Contains(id), TooltipText = "Allow worker withdrawals", FocusMode = Control.FocusModeEnum.None };
            input.Toggled += on => { if (on) t.Inventory.InputFilter.Add(id); else t.Inventory.InputFilter.Remove(id); };
            output.Toggled += on => { if (on) t.Inventory.OutputFilter.Add(id); else t.Inventory.OutputFilter.Remove(id); };
            row.AddChild(input); row.AddChild(output);
        }
        AddButton("Clear both filters (allow everything)", () => { t.Inventory.InputFilter.Clear(); t.Inventory.OutputFilter.Clear(); OpenPage("filters"); });
        AddButton("Back to chest", () => OpenPage("inspect"));
    }
    private void Assignment(Thing t)
    {
        AddLabel("ASSIGN VILLAGERS", 16);
        if (world.S.Workers.Count == 0) { AddLabel("Hire villagers at a village signpost.", 14, Muted); return; }
        foreach (var w in world.S.Workers)
        {
            var worker = w; var current = world.Find(w.Assignment);
            var check = new CheckBox { Text = $"{w.Name} · {(current == null ? "available" : world.Data.Name(current.Kind))}", ButtonPressed = w.Assignment == t.Id, FocusMode = Control.FocusModeEnum.None };
            check.Toggled += on => { world.Assign(worker, on ? t : null); RefreshLive(); };
            content.AddChild(check);
        }
        if (t.Kind == "reinforced") AddLabel("Two workers: first assigned operates bellows; the other hauls. One worker alternates pumping and hauling.", 13, Muted);
    }
    private void WorkerPanel(Worker? w)
    {
        if (w == null) { AddLabel("This worker has left."); return; }
        AddLabel(w.Name, 24); Dynamic(() => $"{w.Status}\nCarrying: {Items(w.Bag)}\nDaily wage: {world.Data.DailyWage:0} coins\nWages owed: {w.WageDebt:0}", 15, Muted);
        AddButton("Work → choose a resource", () => { assigningWorker = w.Id; view.BuildMode = ""; ClosePanel(); RefreshLive(); });
        AddButton("Unassign", () => world.Assign(w, null));
        if (w.WageDebt > 0) AddButton("Pay outstanding wages", () => world.SettleDebt(w));
        AddButton("Dismiss worker", () => { world.Fire(w); OpenPage("workers"); });
        AddLabel("For furnace work, select the building and tick the workers you want to assign.", 14, Muted);
    }
    private void Workers()
    {
        AddLabel($"{world.S.Workers.Count}/8 villagers · {world.S.Workers.Count * world.Data.DailyWage:0} coins/day", 17);
        if (world.S.Workers.Count == 0) AddLabel("Build a village signpost to hire help. Your first steel-tool sale can cover hiring and wages.", 15, Muted);
        foreach (var w in world.S.Workers)
        {
            var worker = w; AddButton(w.Name, () => Select(worker)); Dynamic(() => worker.Status, 14, Muted);
        }
    }
    private void Market(Thing sign)
    {
        AddLabel("BRAMBLEWICK MARKET", 17);
        AddLabel("Delivery takes 30 seconds. Prices respond to your trades and gradually recover. Goods and payment are reserved when dispatched.", 13, Muted);
        var opt = new OptionButton { CustomMinimumSize = new Vector2(0, 38) }; string[] ids = world.Data.Items.Keys.ToArray();
        foreach (var id in ids) opt.AddItem(world.Data.Name(id)); opt.Selected = Array.IndexOf(ids, marketItem);
        opt.ItemSelected += index => { marketItem = ids[(int)index]; RefreshLive(); }; content.AddChild(opt);
        Dynamic(() => $"Sell {world.UnitPrice(marketItem, false):0.00} coins each\nBuy {world.UnitPrice(marketItem, true):0.00} coins each\nDemand {world.S.Demand.GetValueOrDefault(marketItem, 1) * 100:0}%", 16);
        var row = Row(); row.AddChild(Button("−5", () => quantity = Math.Max(1, quantity - 5))); row.AddChild(Button("−", () => quantity = Math.Max(1, quantity - 1)));
        var count = Label(quantity.ToString(), 18); count.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(count); live.Add(() => count.Text = quantity.ToString());
        row.AddChild(Button("+", () => quantity = Math.Min(100, quantity + 1))); row.AddChild(Button("+5", () => quantity = Math.Min(100, quantity + 5)));
        AddButton("Sell selected quantity", () => world.Trade(sign, marketItem, quantity, false));
        AddButton("Buy selected quantity", () => world.Trade(sign, marketItem, quantity, true));
        Dynamic(() => world.S.Orders.Count == 0 ? "No carts on the road." : string.Join("\n", world.S.Orders.Select(o => $"{(o.Buying ? "Buy" : "Sell")} {o.Quantity} {world.Data.Name(o.Item)} · {Math.Max(0, world.Data.TradeSeconds - o.Elapsed):0}s")), 14, Muted);
        AddButton($"Hire villager · {world.Data.HireCost:0} coins", () => { world.Hire(sign); OpenPage("inspect"); });
        AddLabel($"{world.Data.DailyWage:0} coins per worker at each dawn. Unpaid workers strike.", 13, Muted);
        foreach (var worker in world.S.Workers) { var w = worker; AddButton($"{w.Name} · {w.Job} · manage", () => Select(w)); }
    }
    private void Save()
    {
        try { using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write); if (file == null) throw new InvalidOperationException("Cannot open save file"); file.StoreString(world.Serialize()); Notify("Settlement saved."); }
        catch (Exception e) { Notify($"Save failed: {e.Message}"); }
    }
    private void Load()
    {
        try
        {
            if (!Godot.FileAccess.FileExists(SavePath)) { Notify("No saved settlement yet."); return; }
            using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read); world.Deserialize(file.GetAsText());
            selected = 0; workerId = 0; gathering = null; assigningWorker = 0; view.BuildMode = ""; view.Selected = 0; view.SelectedWorker = 0; OpenPage("inventory");
        }
        catch (Exception e) { Notify($"Load failed: {e.Message}"); }
    }
    public override void _UnhandledInput(InputEvent ev)
    {
        if (world == null || view == null || ending.Visible) return;
        if (menu.Visible) { if (ev is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Escape }) CloseMenu(); return; }
        if (ev is InputEventKey key && key.Pressed && !key.Echo)
        {
            switch (key.PhysicalKeycode)
            {
                case Key.Escape:
                    if (view.BuildMode != "" || assigningWorker > 0) { view.BuildMode = ""; assigningWorker = 0; }
                    else if (panel.Visible) ClosePanel(); else OpenMenu();
                    RefreshLive(); break;
                case Key.B: OpenPage("build"); break;
                case Key.I: OpenPage("inventory"); break;
                case Key.Tab: if (page == "help") ClosePanel(); else OpenPage("help"); break;
                case Key.F5: Save(); break;
                case Key.F9: Load(); break;
                case Key.Key1: UseHotbar(0); break;
                case Key.Key2: UseHotbar(1); break;
                case Key.Key3: UseHotbar(2); break;
                case Key.Key4: UseHotbar(3); break;
                case Key.Key5: UseHotbar(4); break;
                case Key.Key6: UseHotbar(5); break;
                case Key.Key7: UseHotbar(6); break;
                case Key.Key8: UseHotbar(7); break;
                case Key.Key9: UseHotbar(8); break;
                case Key.E:
                    var t = world.Find(selected);
                    if (t == null || !world.Near(t)) t = world.S.Things.Where(t => world.Near(t)).OrderBy(t => t.Pos.DistanceTo(world.Player)).FirstOrDefault();
                    if (t != null) Select(t); break;
                case Key.Space:
                    var station = world.Find(selected);
                    if (station != null && station.Kind == "reinforced" && world.Near(station)) world.Pump(station); else world.Attack(); break;
            }
        }
        if (ev is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed) view.Zoom = Math.Min(1.6f, view.Zoom + .1f);
            if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed) view.Zoom = Math.Max(.55f, view.Zoom - .1f);
            if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed)
            {
                var t = view.Pick(mouse.Position); if (t?.IsResource == true) { gathering = t; selected = t.Id; view.Selected = t.Id; } else gathering = null;
            }
            if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed)
            {
                if (view.BuildMode != "")
                {
                    var built = world.Build(view.BuildMode, view.ToWorld(mouse.Position).Round()); if (built != null) { view.BuildMode = ""; Select(built); }
                    return;
                }
                var t = view.Pick(mouse.Position);
                if (assigningWorker > 0)
                {
                    var worker = world.S.Workers.FirstOrDefault(w => w.Id == assigningWorker);
                    if (worker != null && t?.IsResource == true)
                    {
                        string? blocked = world.GatherBlock(t, true);
                        if (blocked != null) Notify(blocked); else { world.Assign(worker, t); assigningWorker = 0; Select(worker); }
                    }
                    else Notify("Choose a tree, stone, ore, coal, berry bush or water source.");
                    return;
                }
                var w = view.PickWorker(mouse.Position);
                if (w != null) Select(w);
                else if (t != null)
                {
                    if (t.IsStorage && ActiveHotbarItem() is string item && IsTransferableHotbarItem(item))
                    {
                        if (world.Near(t)) { int moved = world.S.Backpack.TransferTo(t.Inventory, item, 999); Notify(moved > 0 ? $"Stored {moved} {world.Data.Name(item)}." : $"No {world.Data.Name(item)} to store, or chest is full."); }
                        else Notify("Walk closer to the chest to store the selected stack.");
                    }
                    Select(t);
                }
                else ClosePanel();
            }
        }
    }
    public override void _Process(double delta)
    {
        if (world == null || view == null) return;
        float dt = Math.Min((float)delta, .1f); runTime += dt;
        if (!paused)
        {
            float x = (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
            float y = (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0);
            Vector2 direction = new Vector2(x + y * 2, y * 2 - x).Normalized();
            bool right = Input.IsMouseButtonPressed(MouseButton.Right);
            if (gathering != null && right && direction == Vector2.Zero)
            {
                if (!world.Near(gathering, 2.5f)) direction = (gathering.Pos - world.Player).Normalized();
                else { world.Gather(gathering, dt); if (world.GatherBlock(gathering) is string blocked) { Notify(blocked); gathering = null; } }
            }
            else { world.ResetGather(); if (!right) gathering = null; }
            world.MovePlayer(direction * dt * 4.2f); view.MoveDirection = direction; view.Gathering = gathering != null && right && world.Near(gathering);
            world.Tick(dt); ShowEndingOnce(); autosave += dt;
            if (autosave >= 90) { Save(); autosave = 0; }
        }
        else view.MoveDirection = Vector2.Zero;
        view.Mouse = GetViewport().GetMousePosition();
        var hover = view.Pick(view.Mouse); view.Hover = hover?.Id ?? 0; view.HoverWorker = view.PickWorker(view.Mouse)?.Id ?? 0;
        uiClock += dt; if (uiClock > .2f) { uiClock = 0; RefreshLive(); }
        toastClock -= dt; if (toastClock <= 0 && toast != null) toast.Text = "";
        if (OS.GetCmdlineUserArgs().Contains("--smoke-test") && runTime > .5f) { GD.Print("SMOKE-TEST PASS"); GetTree().Quit(); }
        var args = OS.GetCmdlineUserArgs(); int shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0 && shot + 1 < args.Length && runTime > 2 && !capturing)
        {
            capturing = true; Capture(args[shot + 1]);
        }
    }
    private async void Capture(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"Screenshot: {path} ({error})"); GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
    private void Showcase()
    {
        foreach (var t in world.S.Things.Where(t => t.Pos.DistanceTo(World.Spawn) < 8).ToArray()) world.S.Things.Remove(t);
        var hut = world.Add("hut", new Vector2(21, 22)); var fire = world.Add("campfire", new Vector2(24, 21)); fire.Burn = 200;
        var f = world.Add("furnace", new Vector2(27, 22)); f.Inventory.Add("iron_ore", 12); f.Inventory.Add("wood", 6);
        var bellows = world.Add("reinforced", new Vector2(29, 25)); bellows.Inventory.Add("iron_bar", 12); bellows.Inventory.Add("coal", 20); bellows.Heat = 100;
        var chest = world.Add("chest", new Vector2(25, 27)); chest.Inventory.Add("wood", 35); chest.Inventory.Add("coal", 30); chest.Inventory.Add("iron_bar", 15);
        world.Add("anvil", new Vector2(27, 28)); var sign = world.Add("signpost", new Vector2(22, 28));
        world.S.Coins = 140; world.S.Backpack.Add("steel_tools", 2); world.S.Backpack.Add("water", 4); world.S.Backpack.Add("iron_pick", 1);
        world.S.Milestones.UnionWith(new[] { "gather", "furnace", "iron_bar", "anvil", "pick", "reinforced", "steel_bar", "steel_tools" });
        var w1 = world.Hire(sign); // The review fixture is deliberately separate from a fresh playable game.
        if (w1 == null) { world.Player = sign.Pos + Vector2.One; w1 = world.Hire(sign); }
        var w2 = world.Hire(sign); if (w1 != null) world.Assign(w1, bellows); if (w2 != null) world.Assign(w2, bellows);
        world.Trade(sign, "steel_tools", 1, false);
        if (OS.GetCmdlineUserArgs().Contains("--night")) world.S.Time = world.Data.DaySeconds * .78f;
        world.Player = World.Spawn; Select(bellows);
    }
}
