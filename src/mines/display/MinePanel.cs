using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A mine's three building steps while it is built, then its miners and its two outputs once it is open.</summary>
public partial class MinePanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        string name = Mines.Name(thing); headingTitle.Text = char.ToUpper(name[0]) + name[1..];
        headingIcon.Texture = Icons.Of(Mines.Ore(thing)); headingIcon.Visible = true;
        var data = world.Data.Mine;
        int stage = (int)thing.MineStage; bool open = thing.MineStage == MineStage.Open;
        // Everything below is laid out for this stage.
        RedrawOnChange(() => thing.MineStage);
        var tag = Style.Pill(open ? "open" : $"{stage + 1} / 3", open ? Style.TagPill : Style.Tag(Style.Mustard), open ? Style.Rim : Style.Line, 13);
        panel.AddToHeader(tag);
        if (!open) {
            string workday = PageText.Workdays(data.StageWorkdays);
            (string Title, string Needs, string Icon)[] stages = {
                ("Dig the entrance", $"You or a worker with a pickaxe for {workday}", "steel_pick"),
                ("Shore it up", PageText.Cost(world, data.Materials), "wood"),
                ("Fit the frame and rails", $"You or a worker with a hammer for {workday}", "steel_hammer"),
            };
            var nodes = new PanelContainer[stages.Length];
            for (int i = 0; i < stages.Length; i++) {
                nodes[i] = new PanelContainer { Name = $"stage_{i + 1}", CustomMinimumSize = new Vector2(48, 48), MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = $"Stage {i + 1} · {stages[i].Title}\n{stages[i].Needs}" };
                var icon = Style.Icon(Icons.Of(stages[i].Icon), 30); if (i > stage) icon.Modulate = Style.Unready;
                nodes[i].AddChild(icon);
            }
            var line = new StageTrack(nodes); AddChild(line); line.At(stage);
            AddLabel($"Stage {stage + 1} · {stages[stage].Title}", 16);
            if (thing.MineStage == MineStage.Dug) {
                // Supplies: delivered on the pill, needed under the slot; a lit backpack stack put on its slot delivers it.
                var row = Well(new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }); row.AddThemeConstantOverride("separation", 28);
                foreach (var (item, amount) in data.Materials) {
                    var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); row.AddChild(column);
                    var (cell, count) = ItemSlot.Create($"supply_{item}"); cell.Icon = Icons.Of(item); column.AddChild(cell);
                    moves.TakeCell(cell, thing.Inventory, item, thing, held => held.Stack is { Box: 0 } stack && stack.Item == item, (good, units) => world.Mines.Supply(thing, good, units));
                    column.AddChild(Style.PerBatch($"/ {amount}"));
                    live.Watch(() => thing.Inventory[item], drawn => {
                        count.Text = drawn.ToString();
                        cell.AddThemeStyleboxOverride("normal", drawn >= amount ? Style.Round(Style.SlotFill, 14).Item1 : Style.SlotNormal);
                        cell.TooltipText = $"{world.Data.Name(item)} · {drawn} of {amount}\nPut it here from your backpack · {StationSlots.SlotHint}";
                    });
                }
            } else Dynamic(text => text.Append(stages[stage].Needs).Append(" · ").Append((int)(thing.Progress / world.Mines.StageSeconds * 100)).Append("% done"), 14, Style.Beige);
            BagGrid(id => world.Mines.Supply(thing, id, ItemMoves.Amount(world.S.Backpack, id)), $"{BagHint}\nShift: deliver a stack · Ctrl: all the mine needs", wanted: id => world.Mines.Need(thing, id) > 0);
        } else {
            var crew = Row(); crew.AddThemeConstantOverride("separation", 8);
            var hats = new TextureRect[data.Workers];
            var names = new string?[hats.Length];
            var hardHat = Icons.Of("hardhat");
            for (int i = 0; i < hats.Length; i++) {
                var hat = Style.Icon(hardHat, 30);
                hat.Name = $"miner_{i + 1}";
                hat.MouseFilter = Control.MouseFilterEnum.Pass;
                hat.Modulate = Style.Unready;
                crew.AddChild(hats[i] = hat);
            }
            var row = Well(new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }); row.AddThemeConstantOverride("separation", 36);
            var rates = new Label[2]; var bars = new ColorRect[2]; var items = new[] { Mines.Ore(thing), "stone" };
            for (int i = 0; i < 2; i++) {
                string item = items[i];
                var pair = new HBoxContainer(); pair.AddThemeConstantOverride("separation", 10); row.AddChild(pair);
                var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); pair.AddChild(column);
                var (cell, count) = ItemSlot.Create($"output_{item}", 64); cell.Icon = Icons.Of(item); cell.AddThemeStyleboxOverride("normal", Style.SlotWanted); column.AddChild(cell);
                moves.TakeCell(cell, thing.Output!, item, thing);
                var track = new Panel { CustomMinimumSize = new Vector2(64, 8) }; track.AddThemeStyleboxOverride("panel", Style.BarBack); column.AddChild(track);
                var fill = new ColorRect { Color = Style.Mustard, Size = new Vector2(0, 8) }; track.AddChild(fill); bars[i] = fill;
                rates[i] = Style.Text("", 13, Style.Tan); pair.AddChild(rates[i]);
                live.Watch(() => thing.Output![item], drawn => {
                    count.SetStack(drawn, -1);
                    float full = (float)drawn / thing.Output!.StackSize;
                    fill.Size = new Vector2(64 * full, 8); fill.Color = full > .85f ? Style.Brick : Style.Mustard;
                    cell.TooltipText = $"{world.Data.Name(item)} · {drawn}/{thing.Output!.StackSize}\nClick to take · mining stops when either is full";
                });
            }
            int shownCount = -1;
            live.Add(() => {
                int count = 0;
                foreach (var worker in world.Workers) {
                    if (!Mines.IsUnderground(worker, thing)) continue;
                    if (count < hats.Length) ShowHat(count, worker.Name);
                    count++;
                }
                for (int i = count; i < hats.Length; i++) ShowHat(i, null);
                if (count == shownCount) return;
                shownCount = count;
                rates[0].Text = rates[1].Text = $"{MathF.Round(count / data.Seconds * 100) / 100} /s";
            });

            void ShowHat(int index, string? name) {
                if (name == names[index]) return;
                names[index] = name;
                hats[index].Modulate = name == null ? Style.Unready : Colors.White;
                hats[index].TooltipText = name ?? "";
            }
        }
        JobButtons();
    }
}
