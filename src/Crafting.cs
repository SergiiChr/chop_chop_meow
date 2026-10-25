using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>The personal crafting menu and the crafting bench panel: recipe grids grouped by the tool they need.</summary>
public partial class Game
{
    private void CraftMenu()
    {
        AddLabel("Crafting on the go. Using workbench is faster.", 13, Muted);
        var details = Label("Hover a recipe to see what it needs. Click to start making it.", 14, Muted);
        foreach (var group in world.HandRecipes.GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key))
        {
            var (tool, tier) = group.Key;
            CraftSection(tool, tier, () => tool == "" ? null : world.BestTool(tool, null, tier), group, details, null,
                id => Act(() => world.Craft(id)), _ => false, id => world.CraftBlock(id) == null);
        }
        content.AddChild(details);
        CraftQueue();
    }

    /// <summary>What is being made by hand: the first entry shows its progress; clicking any entry cancels it and returns its materials.</summary>
    private void CraftQueue()
    {
        AddLabel("MAKING", 14);
        var row = Row();
        var cells = Enumerable.Range(0, 8).Select(i => IconCell($"queue_{i}")).ToArray();
        for (int i = 0; i < cells.Length; i++)
        {
            int index = i;
            cells[i].Cell.Pressed += () => { Act(() => world.CancelCraft(index)); RefreshLive(); };
            row.AddChild(cells[i].Cell);
        }
        var progress = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10), Name = "craft_progress" };
        content.AddChild(progress);
        var status = Label("", 13, Muted); content.AddChild(status);
        live.Add(() =>
        {
            if (!GodotObject.IsInstanceValid(progress)) return;
            var queue = world.S.Crafting;
            for (int i = 0; i < cells.Length; i++)
            {
                var (cell, count) = cells[i];
                var r = i < queue.Count ? world.Data.Recipes[queue[i]] : null;
                cell.Visible = r != null || i == 0;
                cell.Icon = r == null ? null : HotbarIcon(r.Output);
                count.Text = r is { Amount: > 1 } ? r.Amount.ToString() : "";
                cell.TooltipText = r == null ? "" : $"{r.Name}\nClick to cancel and get the materials back.";
            }
            var first = queue.Count > 0 ? world.Data.Recipes[queue[0]] : null;
            progress.Value = first == null ? 0 : world.S.CraftTime / first.Seconds;
            status.Text = first == null ? "Nothing in the works." : world.CraftRate > 0 ? $"{first.Name} · {(first.Seconds - world.S.CraftTime) / world.CraftRate:0}s left" : $"{first.Name} · waiting for a {first.Tool} in your backpack";
        });
    }

    private void Bench(Thing bench)
    {
        if (world.Data.Buildings.TryGetValue(bench.Kind, out var def)) AddLabel(def.Description, 14, Muted);
        var slots = Row(); slots.Alignment = BoxContainer.AlignmentMode.Center;
        foreach (string tool in World.SlotKinds(bench.Kind)) slots.AddChild(ToolSlot(bench, tool));
        var details = Label($"Hover a recipe to see what it needs. Click to set the {world.Data.Name(bench.Kind).ToLowerInvariant()} to it.", 14, Muted);
        var recipes = world.Data.Recipes.Where(p => p.Value.Station == bench.Kind).Select(p => p.Key);
        foreach (var group in recipes.GroupBy(id => (world.Data.Recipes[id].Tool, world.Data.Recipes[id].Tier)).OrderBy(g => g.Key))
        {
            var (tool, tier) = group.Key;
            CraftSection(tool, tier, () => tool == "" ? null : world.BenchTool(bench, tool, tier), group, details, bench,
                id => { world.SetRecipe(bench, id); OpenPage("inspect"); }, id => bench.Recipe == id, id => world.S.Backpack.Has(world.Data.Recipes[id].Inputs));
        }
        content.AddChild(details);
        FuelToggle(bench);
        if (bench.Recipe != "")
        {
            Machine(bench);
            var row = Row(); row.AddChild(Button("Load 1 batch", () => world.LoadBatch(bench))); row.AddChild(Button("Load 5", () => world.LoadBatch(bench, 5))); row.AddChild(Button("Collect", () => world.Collect(bench)));
        }
        else AddLabel("Choose a recipe.", 14, Muted);
        AddLabel("BACKPACK", 14);
        AddInventoryGrid(() => world.S.Backpack, null, id =>
        {
            if (World.SlotKinds(bench.Kind).Contains(world.Data.Items.GetValueOrDefault(id)?.Tool) && world.BenchTool(bench, world.Data.Items[id].Tool) == null) world.SlotTool(bench, id);
            else world.LoadInput(bench, id, Input.IsKeyPressed(Key.Shift) ? world.S.Backpack[id] : world.S.Backpack.StackSize);
        }, "Click: slot a tool, or fill an input slot · Drag onto a slot");
        Assignment(bench);
    }

    /// <summary>A station's slot for one tool kind; while empty it shows a faint red outline of the tool it wants.</summary>
    private VBoxContainer ToolSlot(Thing bench, string tool)
    {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 3);
        var caption = Label(tool.ToUpperInvariant(), 12, Muted); caption.HorizontalAlignment = HorizontalAlignment.Center; caption.AutowrapMode = TextServer.AutowrapMode.Off; column.AddChild(caption);
        var (cell, _) = IconCell($"tool_{tool}");
        cell.CustomMinimumSize = new Vector2(52, 52);
        string plainest = world.Data.Items.Where(p => p.Value.Tool == tool).OrderBy(p => p.Value.Speed).First().Key;
        cell.Pressed += () => { if (world.BenchTool(bench, tool) is string held) { Act(() => world.UnslotTool(bench, held)); RefreshLive(); } };
        cell.Drag = () => world.BenchTool(bench, tool) is string held ? new ItemDrag(held, 1, bench.Tools, bench) : null;
        cell.CanTake = drag => drag.From == world.S.Backpack && world.Data.Items.GetValueOrDefault(drag.Item)?.Tool == tool;
        cell.Take = drag => { Act(() => world.SlotTool(bench, drag.Item)); RefreshLive(); };
        live.Add(() =>
        {
            if (!GodotObject.IsInstanceValid(cell)) return;
            string? held = world.BenchTool(bench, tool);
            cell.Icon = HotbarIcon(held ?? plainest);
            cell.SelfModulate = held != null ? Colors.White : new Color(1, .35f, .3f, .45f);
            cell.TooltipText = held != null ? $"{world.Data.Name(held)} · {world.Data.Items[held].Speed:0.#}× speed\nClick: take it back" : $"Empty {tool} slot\nDrag a {tool} here from your backpack";
        });
        column.AddChild(cell);
        return column;
    }

    /// <summary>A recipe's hover popup: its name, each input as an icon with the amount (red while the backpack is short), and the time where it is crafted.</summary>
    private Control RecipePopup(RecipeDef r, float seconds)
    {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6);
        column.AddChild(Label($"{r.Name}{(r.Amount > 1 ? $" ×{r.Amount}" : "")}", 15));
        var inputs = new HBoxContainer(); inputs.AddThemeConstantOverride("separation", 10); column.AddChild(inputs);
        foreach (var (item, amount) in r.Inputs)
        {
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
    /// A boxed grid of recipe icons under a heading naming the tool they need, at or above <paramref name="tier"/>, whose icon turns red while that tool is missing.
    /// <paramref name="held"/> names the tool in use, or null when it is missing or none is needed; <paramref name="station"/> is null when crafting by hand.
    /// </summary>
    private void CraftSection(string tool, int tier, Func<string?> held, IEnumerable<string> recipes, Label details, Thing? station, Action<string> click, Func<string, bool> chosen, Func<string, bool> ready)
    {
        var section = new PanelContainer { Name = $"section_{(tool == "" ? "hand" : tool)}{(tier > 0 ? $"_{tier}" : "")}" };
        section.AddThemeStyleboxOverride("panel", Box(new Color("1f342b"), 6, 10));
        content.AddChild(section);
        var column = new VBoxContainer(); section.AddChild(column);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 6); column.AddChild(heading);
        var title = Label(tool == "" ? "No requirements" : "Required:", 13, Muted); title.AutowrapMode = TextServer.AutowrapMode.Off; heading.AddChild(title);
        if (tool != "")
        {
            string plainest = world.Data.Items.Where(p => p.Value.Tool == tool && p.Value.Tier >= tier).OrderBy(p => p.Value.Tier).ThenBy(p => p.Value.Speed).First().Key;
            string name = world.Data.ToolName(tool, tier);
            var badge = new TextureRect { Name = "badge", Texture = HotbarIcon(plainest), CustomMinimumSize = new Vector2(26, 26), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            heading.AddChild(badge);
            live.Add(() =>
            {
                if (!GodotObject.IsInstanceValid(badge)) return;
                string? inUse = held();
                if (inUse != null) badge.Texture = HotbarIcon(inUse);
                badge.Modulate = inUse != null ? Colors.White : new Color(1, .25f, .2f);
                badge.TooltipText = inUse != null
                    ? $"Needs a {name} · using {world.Data.Name(inUse)}"
                    : station != null ? $"Needs a {name} in the {world.Data.Name(station.Kind).ToLowerInvariant()}'s {tool} slot" : $"Needs a {name} in your backpack{(tool == "chisel" && tier == 0 ? " · knap a stone chisel from 2 stone" : "")}";
            });
        }
        var grid = new GridContainer { Columns = GridColumns };
        grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 4);
        column.AddChild(grid);
        foreach (string id in recipes)
        {
            var r = world.Data.Recipes[id];
            var (cell, count) = IconCell($"craft_{id}");
            cell.Icon = HotbarIcon(r.Output);
            count.Text = r.Amount > 1 ? r.Amount.ToString() : "";
            if (chosen(id)) cell.AddThemeStyleboxOverride("normal", Box(new Color("7c8550"), 4, 3));
            cell.Pressed += () => { click(id); RefreshLive(); };
            float Time() => r.Seconds / ((station?.Kind == "bench" ? world.Data.BenchSpeed : 1) * (held() is string inUse ? world.Data.Items[inUse].Speed : 1));
            string Describe() => $"{r.Name}{(r.Amount > 1 ? $" ×{r.Amount}" : "")}\nNeeds: {Needs(r.Inputs)}\n{Time():0.#}s";
            cell.Popup = () => RecipePopup(r, Time());
            cell.MouseEntered += () => details.Text = Describe();
            live.Add(() =>
            {
                if (!GodotObject.IsInstanceValid(cell)) return;
                cell.TooltipText = Describe();
                cell.Modulate = ready(id) ? Colors.White : new Color(1, .7f, .65f);
            });
            grid.AddChild(cell);
        }
    }
}
