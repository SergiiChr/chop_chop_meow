using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// The personal crafting menu and the crafting bench panel: round tabs for the tools recipes need, beside a grid of recipe plates.
/// Each plate shows the product big and its inputs small, each count in brick while the backpack is short.
/// </summary>
public partial class Game {
    /// <summary>The tool group whose recipes are shown, kept while the page is rebuilt.</summary>
    private string craftTab = "";

    private void CraftMenu() {
        headingIcon.Texture = GD.Load<Texture2D>("res://assets/ui/line_craft.svg"); headingIcon.SelfModulate = Style.Rim; headingIcon.Visible = true;
        headingIcon.TooltipText = "Crafting by hand. A crafting bench works twice as fast.";
        var groups = world.HandRecipes.GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key).ToList();
        RecipeBoard(groups, group => group.Tool == "" ? null : world.BestTool(group.Tool, null, group.Tier), null, id => Act(() => world.Craft(id)), _ => false, world.CanCraft);
        CraftQueue();
        BagGrid(UseFromBag, UseHint, wanted: id => world.HandRecipes.Any(r => world.Data.Recipes[r].Inputs.ContainsKey(id)));
    }

    /// <summary>What is being made by hand: the first entry shows its progress; clicking any entry cancels it and returns its materials.</summary>
    private void CraftQueue() {
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 12);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", CupGap); well.AddChild(row);
        var cells = Enumerable.Range(0, 6).Select(i => IconCell($"queue_{i}", 44)).ToArray();
        for (int i = 0; i < cells.Length; i++) {
            int index = i;
            cells[i].Cell.Pressed += () => { Act(() => world.CancelCraft(index)); RefreshLive(); };
            row.AddChild(cells[i].Cell);
        }
        var progress = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14), Name = "craft_progress" };
        well.AddChild(progress);
        var status = Style.Text("", 14, Style.Ink); well.AddChild(status);
        var statusText = new LiveText(shown => status.Text = shown);
        // Each cell redraws only when the recipe queued in it changes.
        var drawn = new string?[cells.Length];
        bool first = true;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(progress)) return;
            var queue = world.S.Crafting;
            for (int i = 0; i < cells.Length; i++) {
                var (cell, count) = cells[i];
                string? queued = i < queue.Count ? queue[i] : null;
                if (!first && queued == drawn[i]) continue;
                drawn[i] = queued;
                var r = queued == null ? null : world.Data.Recipes[queued];
                cell.Visible = r != null || i == 0;
                cell.Icon = r == null ? null : HotbarIcon(r.Output);
                count.Text = r is { Amount: > 1 } ? r.Amount.ToString() : "";
                cell.TooltipText = r == null ? "" : $"{r.Name}\nClick to cancel and get the materials back.";
            }
            first = false;
            var making = queue.Count > 0 ? world.Data.Recipes[queue[0]] : null;
            progress.Value = making == null ? 0 : world.S.CraftTime / making.Seconds;
            var text = statusText.Clear();
            if (making == null) text.Append("Nothing in the works.");
            else if (world.CraftRate > 0) text.Append($"{making.Name} · {(making.Seconds - world.S.CraftTime) / world.CraftRate:0} s left");
            else text.Append($"{making.Name} · waiting for a {making.Tool} in your backpack");
            statusText.Show();
        });
    }

    private void Bench(Thing bench) {
        // The station's own tool cups sit in the header: what is slotted sets which recipes can run and how fast.
        foreach (string tool in World.SlotKinds(bench.Kind)) { var slot = ToolSlot(bench, tool); heading.AddChild(slot); heading.MoveChild(slot, heading.GetChildCount() - 2); }
        if (bench.Kind == "bench") headingIcon.TooltipText = $"Works {world.Data.BenchSpeed:0.#}× as fast as by hand, at the speed of the slotted tool. Someone has to work it.";
        var groups = world.Data.Recipes.Where(p => p.Value.Station == bench.Kind).Select(p => p.Key).GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key).ToList();
        RecipeBoard(groups, group => group.Tool == "" ? null : world.BenchTool(bench, group.Tool, group.Tier), bench,
            id => { world.SetRecipe(bench, id); OpenPage("inspect"); }, id => bench.Recipe == id, id => world.S.Backpack.Has(world.Data.Recipes[id].Inputs));
        if (bench.Recipe != "") Machine(bench);
        else AddLabel("Choose what to make.", 14, Muted);
        var slotted = World.SlotKinds(bench.Kind);
        BagGrid(id => {
            if (slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(bench, world.Data.Items[id].Tool) == null) world.SlotTool(bench, id);
            else world.LoadInput(bench, id, MoveAll ? world.S.Backpack[id] : world.S.Backpack.StackSize);
        }, "Click: slot a tool, or fill an input slot · Drag onto a slot",
            wanted: id => bench.Recipe != "" && world.BatchCost(bench).ContainsKey(id) || slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(bench, world.Data.Items[id].Tool) == null);
        JobButtons(bench);
    }

    /// <summary>A station's cup for one tool kind; while empty it shows a faint picture of the tool it wants.</summary>
    private ItemSlot ToolSlot(Thing bench, string tool) {
        var (cell, _) = IconCell($"tool_{tool}");
        cell.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        string plainest = world.Data.Items.Where(p => p.Value.Tool == tool).OrderBy(p => p.Value.Speed).First().Key;
        cell.Pressed += () => { if (world.BenchTool(bench, tool) is string held) { Act(() => world.UnslotTool(bench, held)); RefreshLive(); } };
        cell.Drag = () => world.BenchTool(bench, tool) is string held ? new ItemDrag(held, 1, bench.Tools, bench) : null;
        cell.CanTake = drag => drag.From == world.S.Backpack && world.Data.Items.GetValueOrDefault(drag.Item)?.Tool == tool;
        cell.Take = drag => { Act(() => world.SlotTool(bench, drag.Item)); RefreshLive(); return 0; };
        // Redrawn only when the slotted tool changes.
        string? drawn = null; bool first = true;
        live.Add(() => {
            if (!GodotObject.IsInstanceValid(cell)) return;
            string? held = world.BenchTool(bench, tool);
            if (!first && held == drawn) return;
            first = false; drawn = held;
            cell.Icon = HotbarIcon(held ?? plainest);
            cell.AddThemeColorOverride(IconNormal, held != null ? Colors.White : Ghost);
            cell.TooltipText = held != null ? $"{world.Data.Name(held)} · {world.Data.Items[held].Speed:0.#}× speed\nClick: take it back" : $"Empty {tool} slot\nDrag a {tool} here from your backpack";
        });
        return cell;
    }

    /// <summary>A recipe's hover popup: its name, each input as an icon with the amount (brick while the backpack is short), and the time where it is crafted.</summary>
    private Control RecipePopup(RecipeDef r, float seconds) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6);
        column.AddChild(Style.Text($"{r.Name}{(r.Amount > 1 ? $" ×{r.Amount}" : "")}", 15, Style.Rim, Style.Head));
        var inputs = new HBoxContainer(); inputs.AddThemeConstantOverride("separation", 10); column.AddChild(inputs);
        foreach (var (item, amount) in r.Inputs) {
            var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 2); inputs.AddChild(pair);
            pair.AddChild(Style.Icon(HotbarIcon(item), 30));
            pair.AddChild(Style.Text(amount.ToString(), 14, world.S.Backpack[item] >= amount ? Style.Rim : Style.Warn));
        }
        column.AddChild(Style.Text($"{seconds:0.#} s", 14, Style.Beige));
        return column;
    }

    private static string GroupKey((string Tool, int Tier) group) => $"{(group.Tool == "" ? "hand" : group.Tool)}{(group.Tier > 0 ? $"_{group.Tier}" : "")}";

    /// <summary>
    /// The recipe board: a column of round tabs, one per tool the recipes need (red while that tool is missing), beside the chosen tab's plates.
    /// <paramref name="held"/> names the tool in use for a group, or null when it is missing or none is needed; <paramref name="station"/> is null when crafting by hand.
    /// </summary>
    private void RecipeBoard(List<IGrouping<(string Tool, int Tier), string>> groups, Func<(string Tool, int Tier), string?> held, Thing? station, Action<string> click, Func<string, bool> chosen, Func<string, bool> ready) {
        // A board opens on the tab of the recipe its station makes, otherwise the first.
        if (groups.All(g => GroupKey(g.Key) != craftTab))
            craftTab = groups.FirstOrDefault(g => g.Any(chosen)) is { } making ? GroupKey(making.Key) : groups.Count > 0 ? GroupKey(groups[0].Key) : "";
        var board = Row(); board.AddThemeConstantOverride("separation", 14);
        var tabs = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; tabs.AddThemeConstantOverride("separation", 10); board.AddChild(tabs);
        var plates = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        plates.AddThemeConstantOverride("h_separation", 10); plates.AddThemeConstantOverride("v_separation", 10); board.AddChild(plates);
        foreach (var group in groups) {
            var (tool, tier) = group.Key;
            string key = GroupKey(group.Key);
            bool open = key == craftTab;
            var tab = Button("", () => { craftTab = key; OpenPage(page); }); tab.Name = $"section_{key}"; tab.CustomMinimumSize = new Vector2(48, 48);
            var look = open ? Style.TabActive : Style.TabRound; Style.Skin(tab, look.Normal, look.Hover);
            tabs.AddChild(tab);
            var badge = Style.Icon(null, 28); badge.Name = "badge"; badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center); badge.GrowHorizontal = badge.GrowVertical = Control.GrowDirection.Both; badge.MouseFilter = Control.MouseFilterEnum.Pass;
            tab.AddChild(badge);
            if (tool == "") { badge.Texture = HotbarIcon("stone"); badge.TooltipText = "No tool needed"; } else {
                string plainest = world.Data.Items.Where(p => p.Value.Tool == tool && p.Value.Tier >= tier).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
                string name = world.Data.ToolName(tool, tier);
                badge.Texture = HotbarIcon(plainest);
                // Redrawn only when the tool in use changes.
                string? drawn = null; bool first = true;
                live.Add(() => {
                    if (!GodotObject.IsInstanceValid(badge)) return;
                    string? inUse = held(group.Key);
                    if (!first && inUse == drawn) return;
                    first = false; drawn = inUse;
                    if (inUse != null) badge.Texture = HotbarIcon(inUse);
                    badge.Modulate = inUse != null ? Colors.White : new Color(1, .35f, .3f);
                    badge.TooltipText = inUse != null
                        ? $"Needs a {name} · using {world.Data.Name(inUse)}"
                        : station != null ? $"Needs a {name} in the {world.Data.Name(station.Kind).ToLowerInvariant()}'s {tool} slot" : $"Needs a {name} in your backpack{(tool == "chisel" && tier == 0 ? " · knap a stone chisel from 2 stone" : "")}";
                });
            }
            if (!open) continue;
            foreach (string id in group) plates.AddChild(Plate(id, () => held(group.Key), station, click, chosen, ready));
        }
    }

    /// <summary>A recipe plate: the product big, with what it makes in a pill, and its inputs small underneath.</summary>
    private ItemSlot Plate(string id, Func<string?> held, Thing? station, Action<string> click, Func<string, bool> chosen, Func<string, bool> ready) {
        var r = world.Data.Recipes[id];
        var (cell, count) = IconCell($"craft_{id}");
        cell.CustomMinimumSize = new Vector2(78, 84);
        Style.Skin(cell, chosen(id) ? Style.PlateChosen : Style.PlateNormal, Style.PlateHover);
        var face = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); face.AddThemeConstantOverride("separation", 4); cell.AddChild(face);
        var product = Style.Icon(HotbarIcon(r.Output), 42); product.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter; face.AddChild(product);
        var inputs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore }; inputs.AddThemeConstantOverride("separation", 4); face.AddChild(inputs);
        var needs = new List<(string Item, int Amount, Label Count)>();
        foreach (var (item, amount) in r.Inputs) {
            inputs.AddChild(Style.Icon(HotbarIcon(item), 18));
            var need = Style.Text(amount.ToString(), 13, Style.Ink);
            inputs.AddChild(need); needs.Add((item, amount, need));
        }
        count.Text = r.Amount > 1 ? $"×{r.Amount}" : ""; count.MoveToFront();
        cell.Pressed += () => { click(id); RefreshLive(); };
        float Time() => r.Seconds / ((station?.Kind == "bench" ? world.Data.BenchSpeed : 1) * (held() is string inUse ? world.Data.Items[inUse].Speed : 1));
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
            cell.Modulate = ready(id) ? Colors.White : Unready;
            for (int i = 0; i < needs.Count; i++) {
                bool lacking = world.S.Backpack[needs[i].Item] < needs[i].Amount;
                if (shortOf[i] == lacking) continue;
                shortOf[i] = lacking;
                needs[i].Count.AddThemeColorOverride("font_color", lacking ? Style.Brick : Style.Ink);
            }
        });
        return cell;
    }
}
