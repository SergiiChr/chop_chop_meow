using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>The personal crafting menu: the recipe board, then what is being made by hand.</summary>
public partial class CraftPanel(SidePanel side) : RecipePage(side) {
    public override void _Ready() {
        headingIcon.Texture = GD.Load<Texture2D>("res://assets/ui/line_craft.svg"); headingIcon.SelfModulate = Style.Rim; headingIcon.Visible = true;
        headingIcon.TooltipText = "Crafting by hand. A crafting bench works twice as fast.";
        var groups = world.HandRecipes.GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key).ToList();
        RecipeBoard(groups, group => group.Tool == "" ? null : world.BestTool(group.Tool, null, group.Tier), null, id => kit.Act(() => world.Craft(id)), _ => false, world.CanCraft);
        CraftQueue();
        BagGrid(UseFromBag, UseHint, wanted: id => world.HandRecipes.Any(r => world.Data.Recipes[r].Inputs.ContainsKey(id)));
    }

    /// <summary>
    /// What is being made by hand, in the stations' process row: its materials on the left, the time over the bar and the product on the right.
    /// The rest of the queue waits in a row under it; clicking the product or a waiting recipe cancels it and returns its materials.
    /// </summary>
    private void CraftQueue() {
        var queue = world.Crafting;
        string? head = queue.Count > 0 ? queue[0] : null;
        int queued = queue.Count;
        // The row shows one recipe, so the page is rebuilt once when the queue moves on or grows.
        bool rebuilding = false;
        live.Add(() => {
            if (rebuilding || (queue.Count > 0 ? queue[0] : null) == head && queue.Count == queued) return;
            rebuilding = true; Callable.From(() => { if (panel.Page == "craft") panel.Open("craft"); }).CallDeferred();
        });
        if (head == null) { Well(new VBoxContainer()).AddChild(Style.Text("Nothing in the works. Click a recipe to make it.", 14, Style.Ink)); return; }
        var making = world.Data.Recipes[head];
        var inputs = making.Inputs.Select(p => (Control)RecipeCup($"queue_input_{p.Key}", p.Key, $"×{p.Value}", 52, null));
        var product = RecipeCup("queue_0", making.Output, $"×{making.Amount}", 64, () => world.CancelCraft(0));
        var waiting = new List<Control>();
        for (int i = 1; i < queue.Count; i++) {
            int index = i;
            var r = world.Data.Recipes[queue[i]];
            waiting.Add(RecipeCup($"queue_{i}", r.Output, "", 44, () => world.CancelCraft(index)));
        }
        Process(inputs, LiveText.Of(text => AppendDuration(text, making.Seconds / Math.Max(.01f, world.CraftRate))), () => making.Seconds > 0 ? world.CraftTime / making.Seconds : 0, null, product, waiting);
        if (making.Tool != "") {
            var status = Style.Text("", 14, Style.Warn); AddChild(status);
            bool shown = false;
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(status)) return;
                bool stalled = world.CraftRate <= 0;
                if (stalled == shown) return;
                shown = stalled; status.Text = stalled ? $"Waiting for a {making.Tool} in your backpack" : "";
            });
        }
    }

    /// <summary>A cup in the craft queue's row with what one batch takes or makes under it; with a cancel action, clicking it cancels that entry.</summary>
    private VBoxContainer RecipeCup(string name, string item, string perBatch, float size, Action? cancel) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10);
        var (cell, _) = ItemSlot.Cup(name, size); cell.Icon = Icons.Of(item);
        cell.TooltipText = cancel == null ? world.Data.Name(item) : $"{world.Data.Name(item)}\nClick to cancel and get the materials back.";
        if (cancel != null) {
            cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
            cell.Pressed += () => { kit.Act(cancel); kit.Refresh(); };
        }
        column.AddChild(cell);
        if (perBatch != "") column.AddChild(PerBatch(perBatch));
        return column;
    }
}

/// <summary>A crafting bench or cooking table: its tool cups in the header, the recipe board, and the work of the chosen recipe.</summary>
public partial class BenchPanel(SidePanel side, Thing bench) : RecipePage(side) {
    public override void _Ready() {
        // The station's own tool cups sit in the header: what is slotted sets which recipes can run and how fast.
        foreach (string tool in World.SlotKinds(bench.Kind)) { var slot = ToolSlot(tool); heading.AddChild(slot); heading.MoveChild(slot, heading.GetChildCount() - 2); }
        if (bench.Kind == ThingKind.Bench) headingIcon.TooltipText = $"Works {world.Data.BenchSpeed:0.#}× as fast as by hand, at the speed of the slotted tool. Someone has to work it.";
        var groups = world.Data.Recipes.Where(p => p.Value.Station == bench.Kind).Select(p => p.Key).GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key).ToList();
        RecipeBoard(groups, group => group.Tool == "" ? null : world.BenchTool(bench, group.Tool, group.Tier), bench,
            id => { world.SetRecipe(bench, id); panel.Open("inspect"); }, id => bench.Recipe == id, id => world.Backpack.Has(world.Data.Recipes[id].Inputs),
            // A double click also loads one batch of inputs from the backpack.
            id => kit.Act(() => { foreach (var (item, amount) in world.Data.Recipes[id].Inputs) world.LoadInput(bench, item, amount); }));
        if (bench.Recipe != "") Machine(bench);
        else AddLabel("Choose what to make.", 14, Style.Beige);
        var slotted = World.SlotKinds(bench.Kind);
        BagGrid(id => {
            if (slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(bench, world.Data.Items[id].Tool) == null) world.SlotTool(bench, id);
            else world.LoadInput(bench, id, ItemMoves.All ? world.Backpack[id] : 1);
        }, "Click: slot a tool, or add one · Ctrl: all of it · Drag onto a slot",
            wanted: id => bench.Recipe != "" && world.BatchCost(bench).ContainsKey(id) || slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(bench, world.Data.Items[id].Tool) == null);
        JobButtons(bench);
    }

    /// <summary>A station's cup for one tool kind; while empty it shows a faint picture of the tool it wants.</summary>
    private ItemSlot ToolSlot(string tool) {
        var (cell, _) = ItemSlot.Cup($"tool_{tool}");
        cell.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        string plainest = world.Data.Items.Where(p => p.Value.Tool == tool).OrderBy(p => p.Value.Speed).First().Key;
        cell.Pressed += () => { if (world.BenchTool(bench, tool) is string held) { kit.Act(() => world.UnslotTool(bench, held)); kit.Refresh(); } };
        cell.Drag = () => world.BenchTool(bench, tool) is string held ? new ItemDrag(held, 1, bench.Tools, bench) : null;
        cell.CanTake = drag => drag.From == world.S.Backpack && world.Data.Items.GetValueOrDefault(drag.Item)?.Tool == tool;
        cell.Take = drag => { kit.Act(() => world.SlotTool(bench, drag.Item)); kit.Refresh(); return 0; };
        // Redrawn only when the slotted tool changes.
        string? drawn = null; bool first = true;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(cell)) return;
            string? held = world.BenchTool(bench, tool);
            if (!first && held == drawn) return;
            first = false; drawn = held;
            cell.Icon = Icons.Of(held ?? plainest);
            cell.AddThemeColorOverride(ItemSlot.IconNormal, held != null ? Colors.White : Style.Ghost);
            cell.TooltipText = held != null ? $"{world.Data.Name(held)} · {world.Data.Items[held].Speed:0.#}× speed\nClick: take it back" : $"Empty {tool} slot\nDrag a {tool} here from your backpack";
        });
        return cell;
    }
}

/// <summary>
/// Round tabs for the tools recipes need, beside a grid of recipe plates.
/// Each plate shows the product big and its inputs small, each count in brick while the backpack is short.
/// </summary>
public abstract partial class RecipePage(SidePanel side) : PagePanel(side) {
    /// <summary>A recipe's hover popup: its name, each input as an icon with the amount (brick while the backpack is short), and the time where it is crafted.</summary>
    private Control RecipePopup(RecipeDef r, float seconds) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6);
        column.AddChild(Style.Text($"{r.Name}{(r.Amount > 1 ? $" ×{r.Amount}" : "")}", 15, Style.Rim, Style.Head));
        var inputs = new HBoxContainer(); inputs.AddThemeConstantOverride("separation", 10); column.AddChild(inputs);
        foreach (var (item, amount) in r.Inputs) {
            var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 2); inputs.AddChild(pair);
            pair.AddChild(Style.Icon(Icons.Of(item), 30));
            pair.AddChild(Style.Text(amount.ToString(), 14, world.Backpack[item] >= amount ? Style.Rim : Style.Warn));
        }
        column.AddChild(Style.Text($"{seconds:0.#} s", 14, Style.Beige));
        return column;
    }

    private static string GroupKey((string Tool, int Tier) group) => $"{(group.Tool == "" ? "hand" : group.Tool)}{(group.Tier > 0 ? $"_{group.Tier}" : "")}";

    /// <summary>
    /// The recipe board: a column of round tabs, one per tool the recipes need (red while that tool is missing), beside the chosen tab's plates.
    /// <paramref name="held"/> names the tool in use for a group, or null when it is missing or none is needed; <paramref name="station"/> is null when crafting by hand.
    /// </summary>
    protected void RecipeBoard(List<IGrouping<(string Tool, int Tier), string>> groups, Func<(string Tool, int Tier), string?> held, Thing? station, Action<string> click, Func<string, bool> chosen, Func<string, bool> ready, Action<string>? twice = null) {
        // A board opens on the tab of the recipe its station makes, otherwise the first.
        if (groups.All(g => GroupKey(g.Key) != memory.CraftTab))
            memory.CraftTab = groups.FirstOrDefault(g => g.Any(chosen)) is { } making ? GroupKey(making.Key) : groups.Count > 0 ? GroupKey(groups[0].Key) : "";
        var board = Row(); board.AddThemeConstantOverride("separation", 14);
        var tabs = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; tabs.AddThemeConstantOverride("separation", 10); board.AddChild(tabs);
        var plates = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        plates.AddThemeConstantOverride("h_separation", 10); plates.AddThemeConstantOverride("v_separation", 10); board.AddChild(plates);
        foreach (var group in groups) {
            var (tool, tier) = group.Key;
            string key = GroupKey(group.Key);
            bool open = key == memory.CraftTab;
            var tab = kit.Button("", () => { memory.CraftTab = key; panel.Open(panel.Page); }); tab.Name = $"section_{key}"; tab.CustomMinimumSize = new Vector2(48, 48);
            var look = open ? Style.TabActive : Style.TabRound; Style.Skin(tab, look.Normal, look.Hover);
            tabs.AddChild(tab);
            var badge = Style.Icon(null, 28); badge.Name = "badge"; badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center); badge.GrowHorizontal = badge.GrowVertical = Control.GrowDirection.Both; badge.MouseFilter = Control.MouseFilterEnum.Pass;
            tab.AddChild(badge);
            if (tool == "") { badge.Texture = Icons.Of("stone"); badge.TooltipText = "No tool needed"; } else {
                string plainest = world.Data.Items.Where(p => p.Value.Tool == tool && p.Value.Tier >= tier).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
                string name = world.Data.ToolName(tool, tier);
                badge.Texture = Icons.Of(plainest);
                // Redrawn only when the tool in use changes.
                string? drawn = null; bool first = true;
                live.Add(() => {
                    if (!GodotObject.IsInstanceValid(badge)) return;
                    string? inUse = held(group.Key);
                    if (!first && inUse == drawn) return;
                    first = false; drawn = inUse;
                    if (inUse != null) badge.Texture = Icons.Of(inUse);
                    badge.Modulate = inUse != null ? Colors.White : new Color(1, .35f, .3f);
                    badge.TooltipText = inUse != null
                        ? $"Needs a {name} · using {world.Data.Name(inUse)}"
                        : station != null ? $"Needs a {name} in the {world.Data.Name(station.Kind).ToLowerInvariant()}'s {tool} slot" : $"Needs a {name} in your backpack{(tool == "chisel" && tier == 0 ? " · knap a stone chisel from 2 stone" : "")}";
                });
            }
            if (!open) continue;
            foreach (string id in group) plates.AddChild(Plate(id, () => held(group.Key), station, click, chosen, ready, twice));
        }
    }

    /// <summary>A recipe plate: the product big, with what it makes in a pill, and its inputs small underneath.</summary>
    private ItemSlot Plate(string id, Func<string?> held, Thing? station, Action<string> click, Func<string, bool> chosen, Func<string, bool> ready, Action<string>? twice) {
        var r = world.Data.Recipes[id];
        var (cell, count) = ItemSlot.Cup($"craft_{id}");
        cell.CustomMinimumSize = new Vector2(78, 84);
        Style.Skin(cell, chosen(id) ? Style.PlateChosen : Style.PlateNormal, Style.PlateHover);
        var face = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); face.AddThemeConstantOverride("separation", 4); cell.AddChild(face);
        var product = Style.Icon(Icons.Of(r.Output), 42); product.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter; face.AddChild(product);
        var inputs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore }; inputs.AddThemeConstantOverride("separation", 4); face.AddChild(inputs);
        var needs = new List<(string Item, int Amount, Label Count)>();
        foreach (var (item, amount) in r.Inputs) {
            inputs.AddChild(Style.Icon(Icons.Of(item), 18));
            var need = Style.Text(amount.ToString(), 13, Style.Ink);
            inputs.AddChild(need); needs.Add((item, amount, need));
        }
        count.Text = r.Amount > 1 ? $"×{r.Amount}" : ""; count.MoveToFront();
        cell.Pressed += () => { click(id); kit.Refresh(); };
        if (twice != null) cell.DoubleClicked = () => { twice(id); kit.Refresh(); };
        float Time() => r.Seconds / ((station?.Kind == ThingKind.Bench ? world.Data.BenchSpeed : 1) * (held() is string inUse ? world.Data.Items[inUse].Speed : 1));
        void Describe(StringBuilder text) {
            text.Append(r.Name);
            if (r.Amount > 1) text.Append($" ×{r.Amount}");
            text.Append("\nNeeds: "); AppendNeeds(text, r.Inputs);
            text.Append($"\n{Time():0.#} s");
        }
        cell.Popup = () => RecipePopup(r, Time());
        var tooltip = new LiveText(shown => cell.TooltipText = shown);
        // Counts turn brick while the backpack is short; each is recoloured only when that changes.
        var shortOf = new bool?[needs.Count];
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(cell)) return;
            Describe(tooltip.Clear()); tooltip.Show();
            cell.Modulate = ready(id) ? Colors.White : Style.Unready;
            for (int i = 0; i < needs.Count; i++) {
                bool lacking = world.Backpack[needs[i].Item] < needs[i].Amount;
                if (shortOf[i] == lacking) continue;
                shortOf[i] = lacking;
                needs[i].Count.AddThemeColorOverride("font_color", lacking ? Style.Brick : Style.Ink);
            }
        });
        return cell;
    }
}
