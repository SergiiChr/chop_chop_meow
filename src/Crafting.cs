using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>The personal crafting menu and the crafting bench panel: recipe grids grouped by the tool they need.</summary>
public partial class Game {
    private void CraftMenu() {
        headingIcon.Texture = HotbarIcon("stone_chisel"); headingIcon.Visible = true; headingIcon.TooltipText = "Crafting by hand. A crafting bench works twice as fast.";
        foreach (var group in world.HandRecipes.GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key)) {
            var (tool, tier) = group.Key;
            CraftSection(tool, tier, () => tool == "" ? null : world.BestTool(tool, null, tier), group, null,
                id => Act(() => world.Craft(id)), _ => false, world.CanCraft);
        }
        CraftQueue();
        BagGrid(UseFromBag, UseHint, wanted: id => world.HandRecipes.Any(r => world.Data.Recipes[r].Inputs.ContainsKey(id)));
    }

    /// <summary>What is being made by hand: the first entry shows its progress; clicking any entry cancels it and returns its materials.</summary>
    private void CraftQueue() {
        var well = Well();
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); well.AddChild(row);
        var cells = Enumerable.Range(0, 8).Select(i => IconCell($"queue_{i}")).ToArray();
        for (int i = 0; i < cells.Length; i++) {
            int index = i;
            cells[i].Cell.Pressed += () => { Act(() => world.CancelCraft(index)); RefreshLive(); };
            row.AddChild(cells[i].Cell);
        }
        var progress = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14), Name = "craft_progress" };
        well.AddChild(progress);
        var status = Label("", 14, Style.Gold); well.AddChild(status);
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
            else if (world.CraftRate > 0) text.Append($"{making.Name} · {(making.Seconds - world.S.CraftTime) / world.CraftRate:0}s left");
            else text.Append($"{making.Name} · waiting for a {making.Tool} in your backpack");
            statusText.Show();
        });
    }

    private void Bench(Thing bench) {
        // The station's own tool sockets sit at the top: what is slotted sets which recipes can run and how fast.
        var slots = Row(); slots.AddThemeConstantOverride("separation", 8);
        var speed = Label(bench.Kind == "bench" ? $"{world.Data.BenchSpeed:0.#}× speed" : "", 15, Style.Gold); speed.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; speed.VerticalAlignment = VerticalAlignment.Center; slots.AddChild(speed);
        foreach (string tool in World.SlotKinds(bench.Kind)) slots.AddChild(ToolSlot(bench, tool));
        var recipes = world.Data.Recipes.Where(p => p.Value.Station == bench.Kind).Select(p => p.Key);
        foreach (var group in recipes.GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key)) {
            var (tool, tier) = group.Key;
            CraftSection(tool, tier, () => tool == "" ? null : world.BenchTool(bench, tool, tier), group, bench,
                id => { world.SetRecipe(bench, id); OpenPage("inspect"); }, id => bench.Recipe == id, id => world.S.Backpack.Has(world.Data.Recipes[id].Inputs));
        }
        if (bench.Recipe != "") Machine(bench);
        else AddLabel("Choose what to make.", 14, Muted);
        var slotted = World.SlotKinds(bench.Kind);
        BagGrid(id => {
            if (slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(bench, world.Data.Items[id].Tool) == null) world.SlotTool(bench, id);
            else world.LoadInput(bench, id, Input.IsKeyPressed(Key.Shift) ? world.S.Backpack[id] : world.S.Backpack.StackSize);
        }, "Click: slot a tool, or fill an input slot · Drag onto a slot",
            wanted: id => bench.Recipe != "" && world.BatchCost(bench).ContainsKey(id) || slotted.Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(bench, world.Data.Items[id].Tool) == null);
        Assignment(bench);
    }

    /// <summary>A station's slot for one tool kind; while empty it shows a faint red outline of the tool it wants.</summary>
    private VBoxContainer ToolSlot(Thing bench, string tool) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 3);
        var (cell, _) = IconCell($"tool_{tool}");
        cell.CustomMinimumSize = new Vector2(56, 56);
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
        column.AddChild(cell);
        return column;
    }

    /// <summary>A recipe's hover popup: its name, each input as an icon with the amount (red while the backpack is short), and the time where it is crafted.</summary>
    private Control RecipePopup(RecipeDef r, float seconds) {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6);
        column.AddChild(Label($"{r.Name}{(r.Amount > 1 ? $" ×{r.Amount}" : "")}", 15));
        var inputs = new HBoxContainer(); inputs.AddThemeConstantOverride("separation", 10); column.AddChild(inputs);
        foreach (var (item, amount) in r.Inputs) {
            var icon = new TextureRect { Texture = HotbarIcon(item), CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            var count = Label(amount.ToString(), 14, world.S.Backpack[item] >= amount ? Cream : new Color("ff8a7a"));
            var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 2); pair.AddChild(icon); pair.AddChild(count); inputs.AddChild(pair);
        }
        column.AddChild(Label($"{seconds:0.#}s", 14, Muted));
        // The shared label helper wraps text, which squeezes a popup with no set width down to single letters.
        foreach (var label in column.FindChildren("*", "Label", true, false).OfType<Label>()) label.AutowrapMode = TextServer.AutowrapMode.Off;
        return column;
    }

    /// <summary>
    /// A row of the pegboard: a socket showing the tool the recipes need, at or above <paramref name="tier"/>, red while that tool is missing, beside a tile per recipe.
    /// Each tile shows the product big and its inputs small, each count red while the backpack is short.
    /// <paramref name="held"/> names the tool in use, or null when it is missing or none is needed; <paramref name="station"/> is null when crafting by hand.
    /// </summary>
    private void CraftSection(string tool, int tier, Func<string?> held, IEnumerable<string> recipes, Thing? station, Action<string> click, Func<string, bool> chosen, Func<string, bool> ready) {
        var section = new HBoxContainer { Name = $"section_{(tool == "" ? "hand" : tool)}{(tier > 0 ? $"_{tier}" : "")}" };
        section.AddThemeConstantOverride("separation", 0);
        content.AddChild(section);
        var rail = new PanelContainer { CustomMinimumSize = new Vector2(54, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; rail.AddThemeStyleboxOverride("panel", Style.Rail); section.AddChild(rail);
        if (tool != "") {
            string plainest = world.Data.Items.Where(p => p.Value.Tool == tool && p.Value.Tier >= tier).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
            string name = world.Data.ToolName(tool, tier);
            var badge = new TextureRect { Name = "badge", Texture = HotbarIcon(plainest), CustomMinimumSize = new Vector2(38, 38), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            rail.AddChild(badge);
            // Redrawn only when the tool in use changes.
            string? drawn = null; bool first = true;
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(badge)) return;
                string? inUse = held();
                if (!first && inUse == drawn) return;
                first = false; drawn = inUse;
                if (inUse != null) badge.Texture = HotbarIcon(inUse);
                badge.Modulate = inUse != null ? Colors.White : new Color(1, .25f, .2f);
                badge.TooltipText = inUse != null
                    ? $"Needs a {name} · using {world.Data.Name(inUse)}"
                    : station != null ? $"Needs a {name} in the {world.Data.Name(station.Kind).ToLowerInvariant()}'s {tool} slot" : $"Needs a {name} in your backpack{(tool == "chisel" && tier == 0 ? " · knap a stone chisel from 2 stone" : "")}";
            });
        } else {
            var bare = Style.Icon(HotbarIcon("stone"), 34); bare.TooltipText = "No tool needed"; bare.MouseFilter = Control.MouseFilterEnum.Pass; rail.AddChild(bare);
        }
        var board = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; board.AddThemeStyleboxOverride("panel", Style.Pegboard); section.AddChild(board);
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 6); grid.AddThemeConstantOverride("v_separation", 6);
        board.AddChild(grid);
        foreach (string id in recipes) {
            var r = world.Data.Recipes[id];
            var (cell, count) = IconCell($"craft_{id}");
            cell.CustomMinimumSize = new Vector2(76, 80);
            Style.Skin(cell, chosen(id) ? Style.TileChosen : Style.Tile, Style.TileHover, Style.TileChosen);
            var face = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); face.AddThemeConstantOverride("separation", 2); cell.AddChild(face);
            var product = Style.Icon(HotbarIcon(r.Output), 44); product.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter; face.AddChild(product);
            var inputs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore }; inputs.AddThemeConstantOverride("separation", 2); face.AddChild(inputs);
            var needs = new List<(string Item, int Amount, Label Count)>();
            foreach (var (item, amount) in r.Inputs) {
                inputs.AddChild(Style.Icon(HotbarIcon(item), 18));
                var need = Label(amount.ToString(), 13); need.AutowrapMode = TextServer.AutowrapMode.Off; need.AddThemeFontOverride("font", Style.Bold); need.MouseFilter = Control.MouseFilterEnum.Ignore;
                inputs.AddChild(need); needs.Add((item, amount, need));
            }
            count.Text = r.Amount > 1 ? $"×{r.Amount}" : ""; count.OffsetBottom = -22; count.MoveToFront();
            cell.Pressed += () => { click(id); RefreshLive(); };
            float Time() => r.Seconds / ((station?.Kind == "bench" ? world.Data.BenchSpeed : 1) * (held() is string inUse ? world.Data.Items[inUse].Speed : 1));
            void Describe(StringBuilder text) {
                text.Append(r.Name);
                if (r.Amount > 1) text.Append($" ×{r.Amount}");
                text.Append("\nNeeds: "); AppendNeeds(text, r.Inputs);
                text.Append($"\n{Time():0.#}s");
            }
            cell.Popup = () => RecipePopup(r, Time());
            var tooltip = new LiveText(shown => cell.TooltipText = shown);
            // Counts turn red while the backpack is short; each is recoloured only when that changes.
            var shortOf = new bool?[needs.Count];
            live.Add(() => {
                if (!GodotObject.IsInstanceValid(cell)) return;
                Describe(tooltip.Clear()); tooltip.Show();
                cell.Modulate = ready(id) ? Colors.White : Unready;
                for (int i = 0; i < needs.Count; i++) {
                    bool lacking = world.S.Backpack[needs[i].Item] < needs[i].Amount;
                    if (shortOf[i] == lacking) continue;
                    shortOf[i] = lacking;
                    needs[i].Count.AddThemeColorOverride("font_color", lacking ? Style.Short : Style.Cream);
                }
            });
            grid.AddChild(cell);
        }
    }

    private static readonly Color Unready = new(1, 1, 1, .55f);
}
