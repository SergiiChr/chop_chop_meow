using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Round tabs for the tools recipes need, beside a grid of recipe plates.
/// Each plate shows the product big and its inputs small, each count in brick while the backpack is short.
/// </summary>
/// <param name="held">Names the tool in use for a group, or null when it is missing or none is needed.</param>
/// <param name="station">Null when crafting by hand.</param>
public partial class RecipeBoard(UiKit kit, LiveUpdates live, PageMemory memory, Action redraw, List<IGrouping<(string Tool, int Tier), string>> groups, Func<(string Tool, int Tier), string?> held, Thing? station, Action<string> click, Func<string, bool> chosen, Func<string, bool> ready, Action<string>? twice = null) : HBoxContainer {
    private readonly World world = kit.World;

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

    /// <summary>A column of round tabs, one per tool the recipes need (red while that tool is missing), beside the chosen tab's plates.</summary>
    public override void _Ready() {
        // A board opens on the tab of the recipe its station makes, otherwise the first.
        if (groups.All(g => GroupKey(g.Key) != memory.CraftTab))
            memory.CraftTab = groups.FirstOrDefault(g => g.Any(chosen)) is { } making ? GroupKey(making.Key) : groups.Count > 0 ? GroupKey(groups[0].Key) : "";
        AddThemeConstantOverride("separation", 14);
        var tabs = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; tabs.AddThemeConstantOverride("separation", 10); AddChild(tabs);
        var plates = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        plates.AddThemeConstantOverride("h_separation", 10); plates.AddThemeConstantOverride("v_separation", 10); AddChild(plates);
        foreach (var group in groups) {
            var (kind, tier) = group.Key;
            string key = GroupKey(group.Key);
            bool open = key == memory.CraftTab;
            var tab = kit.Button("", () => { memory.CraftTab = key; redraw(); }); tab.Name = $"section_{key}"; tab.CustomMinimumSize = new Vector2(48, 48);
            var look = open ? Style.TabActive : Style.TabRound; Style.Skin(tab, look.Normal, look.Hover);
            tabs.AddChild(tab);
            var badge = Style.Icon(null, 28); badge.Name = "badge"; badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center); badge.GrowHorizontal = badge.GrowVertical = Control.GrowDirection.Both; badge.MouseFilter = Control.MouseFilterEnum.Pass;
            tab.AddChild(badge);
            if (kind == "") { badge.Texture = Icons.Of("stone"); badge.TooltipText = "No kind needed"; } else {
                string plainest = world.Data.PlainestTool(kind, tier);
                string name = world.Data.ToolName(kind, tier);
                badge.Texture = Icons.Of(plainest);
                live.Watch(() => held(group.Key), inUse => {
                    if (inUse != null) badge.Texture = Icons.Of(inUse);
                    badge.Modulate = inUse != null ? Colors.White : new Color(1, .35f, .3f);
                    badge.TooltipText = inUse != null
                        ? $"Needs a {name} · using {world.Data.Name(inUse)}"
                        : station != null ? $"Needs a {name} in the {world.Data.Name(station.Kind).ToLowerInvariant()}'s {kind} slot" : $"Needs a {name} in your backpack{(kind == "chisel" && tier == 0 ? " · knap a stone chisel from 2 stone" : "")}";
                });
            }
            if (!open) continue;
            foreach (string id in group) plates.AddChild(Plate(id, () => held(group.Key)));
        }
    }

    /// <summary>A recipe plate: the product big, with what it makes in a pill, and its inputs small underneath.</summary>
    private ItemSlot Plate(string id, Func<string?> tool) {
        var r = world.Data.Recipes[id];
        var (cell, count) = ItemSlot.Cup($"craft_{id}");
        cell.CustomMinimumSize = new Vector2(78, 84);
        Style.Skin(cell, chosen(id) ? Style.PlateChosen : Style.PlateNormal, Style.PlateHover);
        var face = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); face.AddThemeConstantOverride("separation", 4); cell.AddChild(face);
        var product = Style.Icon(Icons.Of(r.Output), 42); product.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter; face.AddChild(product);
        var inputs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore }; inputs.AddThemeConstantOverride("separation", 4); face.AddChild(inputs);
        foreach (var (item, amount) in r.Inputs) {
            inputs.AddChild(Style.Icon(Icons.Of(item), 18));
            var need = Style.Text(amount.ToString(), 13, Style.Ink);
            inputs.AddChild(need);
            // Counts turn brick while the backpack is short.
            live.Watch(() => world.Backpack[item] < amount, lacking => need.AddThemeColorOverride("font_color", lacking ? Style.Brick : Style.Ink));
        }
        count.Text = r.Amount > 1 ? $"×{r.Amount}" : ""; count.MoveToFront();
        cell.Pressed += () => kit.Run(() => click(id));
        if (twice != null) cell.DoubleClicked = () => kit.Run(() => twice(id));
        float Time() => r.Seconds / ((station?.Kind == ThingKind.Bench ? world.Data.BenchSpeed : 1) * (tool() is string inUse ? world.Data.Items[inUse].Speed : 1));
        void Describe(StringBuilder text) {
            text.Append(r.Name);
            if (r.Amount > 1) text.Append($" ×{r.Amount}");
            text.Append("\nNeeds: "); PageText.AppendNeeds(world, text, r.Inputs);
            text.Append($"\n{Time():0.#} s");
        }
        cell.Popup = () => RecipePopup(r, Time());
        live.Bind(shown => cell.TooltipText = shown, Describe);
        live.Watch(() => ready(id), yes => cell.Modulate = yes ? Colors.White : Style.Unready);
        return cell;
    }
}
