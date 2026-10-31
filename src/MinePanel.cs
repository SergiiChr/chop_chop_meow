using System;
using System.Linq;
using Godot;

namespace ChopChopMeow;

/// <summary>A mine's three building steps while it is built, then its miners and its two outputs once it is open.</summary>
public partial class MinePanel(SidePanel side, Thing mine) : PagePanel(side) {
    public override void _Ready() {
        string name = World.MineName(mine); headingTitle.Text = char.ToUpper(name[0]) + name[1..];
        headingIcon.Texture = Icons.Of(World.MineOre(mine)); headingIcon.Visible = true;
        var data = world.Data.Mine;
        int stage = (int)mine.MineStage; bool open = mine.MineStage == MineStage.Open;
        var tag = Style.Pill(open ? "open" : $"{stage + 1} / 3", open ? Style.TagPill : Style.Tag(Style.Mustard), open ? Style.Rim : Style.Line, 13);
        heading.AddChild(tag); heading.MoveChild(tag, heading.GetChildCount() - 2);
        if (!open) {
            string workday = data.StageWorkdays == 1 ? "1 working day" : $"{data.StageWorkdays:0.#} working days";
            (string Title, string Needs, string Icon)[] stages = {
                ("Dig the entrance", $"A worker with a pickaxe for {workday}", "steel_pick"),
                ("Shore it up", Cost(data.Materials), "wood"),
                ("Fit the frame and rails", $"A worker with a hammer for {workday}", "steel_hammer"),
            };
            // The three steps on a line, done in sage, the current one in mustard.
            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 0); AddChild(line);
            for (int i = 0; i < stages.Length; i++) {
                if (i > 0) line.AddChild(new ColorRect { Color = i <= stage ? Style.Sage : Style.Line, CustomMinimumSize = new Vector2(0, 6), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
                var node = new PanelContainer { Name = $"stage_{i + 1}", CustomMinimumSize = new Vector2(48, 48), MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = $"Stage {i + 1} · {stages[i].Title}\n{stages[i].Needs}" };
                node.AddThemeStyleboxOverride("panel", Style.Round(i < stage ? Style.Sage : i == stage ? Style.Mustard : Style.Line, 24).Item1);
                var icon = Style.Icon(Icons.Of(stages[i].Icon), 30); if (i > stage) icon.Modulate = Style.Unready;
                node.AddChild(icon); line.AddChild(node);
            }
            AddLabel($"Stage {stage + 1} · {stages[stage].Title}", 16);
            if (mine.MineStage == MineStage.Dug) {
                // Supplies: delivered on the pill, needed under the cup; click a lit backpack stack to deliver it.
                var row = Well(new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }); row.AddThemeConstantOverride("separation", 28);
                foreach (var (item, amount) in data.Materials) {
                    var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); row.AddChild(column);
                    var (cell, count) = ItemSlot.Cup($"supply_{item}"); cell.Icon = Icons.Of(item); column.AddChild(cell);
                    cell.Pressed += () => { kit.Act(() => world.SupplyMine(mine)); kit.Refresh(); };
                    column.AddChild(PerBatch($"/ {amount}"));
                    live.Watch(() => mine.Inventory[item], drawn => {
                        count.Text = drawn.ToString();
                        cell.AddThemeStyleboxOverride("normal", drawn >= amount ? Style.Round(Style.CupFill, 14).Item1 : Style.CupNormal);
                        cell.TooltipText = $"{world.Data.Name(item)} · {drawn} of {amount}\nClick to deliver from your backpack";
                    });
                }
            } else Dynamic(text => text.Append(stages[(int)mine.MineStage].Needs).Append(" · ").Append((int)(mine.Progress / world.MineStageSeconds * 100)).Append("% done"), 14, Style.Beige);
            BagGrid(id => world.SupplyMine(mine), "Click: deliver what the mine needs", wanted: id => world.MineNeed(mine, id) > 0);
        } else {
            // Miners as hardhats, lit while underground, and the two outputs with how fast they fill.
            var crew = Row(); crew.AddThemeConstantOverride("separation", 8);
            var tunnel = Style.Glyph("line_tunnel", 28, Style.Rim); tunnel.MouseFilter = Control.MouseFilterEnum.Pass; tunnel.TooltipText = "Miners underground"; crew.AddChild(tunnel);
            var hats = Enumerable.Range(0, data.Workers).Select(_ => Style.Icon(GD.Load<Texture2D>("res://assets/ui/hardhat.svg"), 30)).ToArray();
            foreach (var hat in hats) crew.AddChild(hat);
            var row = Well(new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }); row.AddThemeConstantOverride("separation", 36);
            var rates = new Label[2]; var bars = new ColorRect[2]; var items = new[] { World.MineOre(mine), "stone" };
            for (int i = 0; i < 2; i++) {
                string item = items[i];
                var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 10); row.AddChild(pair);
                var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); pair.AddChild(column);
                var (cell, count) = ItemSlot.Cup($"output_{item}", 64); cell.Icon = Icons.Of(item); cell.AddThemeStyleboxOverride("normal", Style.CupWanted); column.AddChild(cell);
                cell.Pressed += () => { kit.Act(() => moves.Stack(mine.Output!, world.S.Backpack, item, mine)); kit.Refresh(); };
                cell.Drag = () => mine.Output![item] > 0 ? new ItemDrag(item, mine.Output[item], mine.Output, mine) : null;
                var track = new Panel { CustomMinimumSize = new Vector2(64, 8) }; track.AddThemeStyleboxOverride("panel", Style.BarBack); column.AddChild(track);
                var fill = new ColorRect { Color = Style.Mustard, Size = new Vector2(0, 8) }; track.AddChild(fill); bars[i] = fill;
                rates[i] = Style.Text("", 13, Style.Tan); pair.AddChild(rates[i]);
                live.Watch(() => mine.Output![item], drawn => {
                    count.Text = drawn > 0 ? drawn.ToString() : "";
                    float full = (float)drawn / mine.Output!.StackSize;
                    fill.Size = new Vector2(64 * full, 8); fill.Color = full > .85f ? Style.Brick : Style.Mustard;
                    cell.TooltipText = $"{world.Data.Name(item)} · {drawn}/{mine.Output!.StackSize}\nClick to take · mining stops when either is full";
                });
            }
            live.Watch(MinersUnderground, now => {
                for (int i = 0; i < hats.Length; i++) hats[i].Modulate = i < now ? Colors.White : Style.Unready;
                rates[0].Text = rates[1].Text = $"{MathF.Round(now / data.Seconds * 100) / 100} /s";
            });
        }
        JobButtons(mine);
    }

    private int MinersUnderground() {
        int miners = 0;
        foreach (var w in world.Workers) if (w.Assignment == mine.Id && w.WorkState == WorkerWorkState.Underground) miners++;
        return miners;
    }
}
