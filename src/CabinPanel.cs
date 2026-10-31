using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A cabin: inside or outside, and what a night's sleep would leave of each need.</summary>
public partial class CabinPanel(SidePanel side, Thing hut) : PagePanel(side) {
    public override void _Ready() {
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
        var door = kit.BigButton("", Style.Sage, () => { if (world.S.Sheltered == hut.Id) world.Leave(); else panel.Player.WalkInto(hut); });
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
    private static string ShortBlock(string reason) => reason.Contains("thirsty") && reason.Contains("hungry") ? "too hungry and thirsty" : reason.Contains("thirsty") ? "too thirsty" : reason.Contains("hungry") ? "too hungry" : reason.Contains("night") ? "only at night" : "not yet";
}
