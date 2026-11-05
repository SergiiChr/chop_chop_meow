using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// Round tabs for the tools recipes need, beside a grid of square recipe plates.
/// Each plate shows the product big and its inputs small, each count in brick while the backpack is short, and how many are queued at its top right corner.
/// By hand a click queues a batch. At a station a click marks the recipe workers keep making, and a double-click queues a batch and loads it from the backpack.
/// Shift queues 5 and Ctrl 10; a right-click takes as many waiting batches off.
/// </summary>
/// <param name="station">Null when crafting by hand.</param>
/// <param name="openOn">A recipe whose tab the board opens on, for a page opening rather than drawing again.</param>
public partial class RecipeBoard(UiKit kit, LiveUpdates live, PageMemory memory, Action redraw, Thing? station, string? openOn) : HBoxContainer {
    private readonly World world = kit.World;
    private readonly List<IGrouping<(string Tool, int Tier), string>> groups =
        kit.World.RecipesAt(station).GroupBy(id => (kit.World.Data.Recipes[id].Tool, kit.World.Data.Recipes[id].Tier)).OrderBy(g => g.Key).ToList();

    /// <summary>The tool in use for a group: carried by hand, slotted at a station; null when it is missing or none is needed.</summary>
    private string? Held((string Tool, int Tier) group) => world.ToolInUse(station, group.Tool, group.Tier);

    /// <summary>How far the worker mark's hard hat stands out over the top of a plate.</summary>
    private const int HatRise = 18;

    /// <summary>How many batches a click queues or takes off: 10 with Ctrl, 5 with Shift, else 1.</summary>
    public static int Step => Input.IsKeyPressed(Key.Ctrl) ? 10 : Input.IsKeyPressed(Key.Shift) ? 5 : 1;

    /// <summary>A recipe's hover popup: its name, each input as an icon with the amount (brick while the backpack is short), the time where it is crafted and what clicks do.</summary>
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
        column.AddChild(Style.Text(Hint, 13, Style.Beige));
        return column;
    }

    private string Hint => station != null
        ? "Click: workers keep making it · Double-click: queue one and load it\nShift/Ctrl-click: queue 5/10 · Right-click: take 1, 5 or 10 off"
        : "Click: queue one · Shift/Ctrl-click: queue 5/10\nRight-click: take 1, 5 or 10 off";

    private static string GroupKey((string Tool, int Tier) group) => $"{(group.Tool == "" ? "hand" : group.Tool)}{(group.Tier > 0 ? $"_{group.Tier}" : "")}";

    /// <summary>A column of round tabs, one per tool the recipes need (red while that tool is missing), beside the chosen tab's plates.</summary>
    public override void _Ready() {
        // A page opens on the tab of the recipe its station makes; a board drawn again keeps its tab, unless that tab is not on this board.
        if (openOn != null && groups.FirstOrDefault(g => g.Contains(openOn)) is { } opening) memory.CraftTab = GroupKey(opening.Key);
        else if (groups.All(g => GroupKey(g.Key) != memory.CraftTab)) memory.CraftTab = groups.Count > 0 ? GroupKey(groups[0].Key) : "";
        AddThemeConstantOverride("separation", 14);
        var tabs = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin }; tabs.AddThemeConstantOverride("separation", 10); AddChild(tabs);
        var plates = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        plates.AddThemeConstantOverride("h_separation", 10); plates.AddThemeConstantOverride("v_separation", 14);
        // Room above the first row for the hard hat that marks what workers keep making, which stands out over a plate's top edge.
        var platesRoom = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        platesRoom.AddThemeConstantOverride("margin_top", station != null ? HatRise : 0); platesRoom.AddChild(plates); AddChild(platesRoom);
        foreach (var group in groups) {
            var (kind, tier) = group.Key;
            string key = GroupKey(group.Key);
            bool open = key == memory.CraftTab;
            var tab = kit.Button("", () => { memory.CraftTab = key; redraw(); }); tab.Name = $"section_{key}"; tab.CustomMinimumSize = new Vector2(48, 48);
            var look = open ? Style.TabActive : Style.TabRound; Style.Skin(tab, look.Normal, look.Hover);
            tabs.AddChild(tab);
            var badge = Style.Icon(null, 28); badge.Name = "badge"; badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center); badge.GrowHorizontal = badge.GrowVertical = Control.GrowDirection.Both; badge.MouseFilter = Control.MouseFilterEnum.Pass;
            tab.AddChild(badge);
            if (kind == "") { badge.Texture = Icons.Of(station?.Burner != null ? "coal" : "stone"); badge.TooltipText = "No tool needed"; } else {
                string plainest = world.Data.PlainestTool(kind, tier);
                string name = world.Data.ToolName(kind, tier);
                badge.Texture = Icons.Of(plainest);
                live.Watch(() => Held(group.Key), inUse => {
                    if (inUse != null) badge.Texture = Icons.Of(inUse);
                    badge.Modulate = inUse != null ? Colors.White : new Color(1, .35f, .3f);
                    badge.TooltipText = inUse != null
                        ? $"Needs a {name} · using {world.Data.Name(inUse)}"
                        : station != null ? $"Needs a {name} in the {world.Data.Name(station.Kind).ToLowerInvariant()}'s {kind} slot" : $"Needs a {name} in your backpack{(kind == "chisel" && tier == 0 ? " · knap a stone chisel from 2 stone" : "")}";
                });
            }
            if (!open) continue;
            foreach (string id in group) plates.AddChild(Plate(id, () => Held(group.Key)));
        }
    }

    /// <summary>A square recipe plate: the product big, with what it makes in a pill, its inputs small underneath, and the queued count at the top right.</summary>
    private ItemSlot Plate(string id, Func<string?> tool) {
        var r = world.Data.Recipes[id];
        var (cell, count) = ItemSlot.Cup($"craft_{id}");
        cell.CustomMinimumSize = new Vector2(84, 84);
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
        var queued = ItemSlot.CountBadge(1, 0); queued.Name = "queued"; cell.AddChild(queued);
        live.Watch(() => world.Waiting(station, id), waiting => queued.Text = waiting > 0 ? waiting.ToString() : "");
        if (station != null) {
            // The worker mark: a thick outline with a hard hat on top.
            var hat = Style.Corner(Style.Glyph("hardhat", 34, Colors.White), .5f, 0, HatRise); hat.Name = "kept"; cell.AddChild(hat);
            live.Watch(() => station.Keep == id, kept => {
                Style.Skin(cell, kept ? Style.PlateKept : Style.PlateNormal, kept ? Style.PlateKept : Style.PlateHover);
                hat.Visible = kept;
            });
        } else Style.Skin(cell, Style.PlateNormal, Style.PlateHover);
        // The second press of a double-click undoes what its first press did, then queues one batch with its inputs.
        bool doubled = false;
        cell.Pressed += () => {
            if (doubled) { doubled = false; return; }
            string kept = memory.KeptBefore = station?.Keep ?? "";
            int step = Step;
            if (station == null || step > 1) { kit.Run(() => world.Enqueue(station, id, step)); return; }
            kit.Run(() => world.SetRecipe(station, kept == id ? "" : id));
        };
        cell.DoubleClicked = () => {
            doubled = true;
            if (station == null) return;
            string kept = memory.KeptBefore;
            kit.Run(() => { world.SetRecipe(station, kept); world.QueueBatch(station, id); });
        };
        cell.RightClicked = () => kit.Run(() => world.Enqueue(station, id, -Step));
        float Time() => r.Seconds / ((station?.Kind == ThingKind.Bench ? world.Data.BenchSpeed : 1) * (tool() is string inUse ? world.Data.Items[inUse].Speed : 1));
        void Describe(StringBuilder text) {
            text.Append(r.Name);
            if (r.Amount > 1) text.Append($" ×{r.Amount}");
            text.Append("\nNeeds: "); PageText.AppendNeeds(world, text, r.Inputs);
            text.Append($"\n{Time():0.#} s\n").Append(Hint);
        }
        cell.Popup = () => RecipePopup(r, Time());
        live.Bind(shown => cell.TooltipText = shown, Describe);
        live.Watch(() => station != null ? world.Backpack.Has(r.Inputs) : world.CanCraft(id), yes => cell.Modulate = yes ? Colors.White : Style.Unready);
        return cell;
    }
}

/// <summary>What waits in a queue, one cup per recipe in the order first queued; a right-click takes 1, 5 or 10 batches off.</summary>
/// <param name="station">The station whose queue it shows, or null for the hand's; the page draws it again when the queue's recipes change.</param>
public partial class QueueStrip(UiKit kit, LiveUpdates live, Thing? station) : HBoxContainer {
    public override void _Ready() {
        var world = kit.World;
        var queue = world.QueueOf(station);
        Name = "queue";
        AddThemeConstantOverride("separation", 10);
        if (queue.Count == 0) return;
        AddChild(Style.Text("Next", 14, Style.Beige));
        foreach (var q in queue) {
            var r = kit.World.Data.Recipes[q.Recipe];
            var (cell, count) = ItemSlot.Cup($"queued_{q.Recipe}", 44); cell.Icon = Icons.Of(r.Output);
            cell.AddThemeStyleboxOverride("normal", Style.CupWanted);
            string id = q.Recipe;
            live.Watch(() => world.Waiting(station, id), n => { count.Text = n.ToString(); cell.TooltipText = $"{r.Name} · {n} waiting\nRight-click: take 1, 5 or 10 off"; });
            cell.RightClicked = () => kit.Run(() => world.Enqueue(station, id, -RecipeBoard.Step));
            AddChild(cell);
        }
    }
}
