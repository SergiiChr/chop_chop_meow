using System;
using Godot;

namespace ChopChopMeow;

/// <summary>A cabin: inside or outside, and what a night's sleep would leave of each need.</summary>
public partial class CabinPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
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
        var door = kit.BigButton("", Style.Sage, () => { if (world.Sheltered == thing.Id) world.Leave(); else panel.Player.WalkInto(thing); });
        var sleep = kit.BigButton("Sleep", Style.Enamel, () => world.Sleep(thing)); sleep.Name = "sleep";
        buttons.AddChild(door); buttons.AddChild(sleep);
        live.Watch(() => (world.Sheltered == thing.Id, world.SleepBlock(), (int)(world.SecondsToDawn / world.Data.DaySeconds * 1440)), now => {
            var (inside, blocked, _) = now;
            tag.Text = inside ? "inside" : "outside";
            tag.AddThemeStyleboxOverride("normal", inside ? Style.TagPill : Style.BlueTag);
            door.Text = inside ? "Step outside" : "Go inside";
            sleep.TooltipText = blocked ?? "Sleep until dawn. Production, hunger, trade and wages advance.";
            sleep.Modulate = blocked == null ? Colors.White : Style.Unready;
            block.Visible = blocked != null; block.Text = blocked == null ? "" : ShortBlock(blocked);
            until.Text = LiveText.Of(text => PageText.AppendGameHours(world, text.Append("until dawn · +"), world.SecondsToDawn));
            float seconds = world.SecondsToDawn;
            Show(0, world.Health, world.Health);
            Show(1, world.Hunger, world.Hunger - seconds * world.Data.HungerPerSecond);
            Show(2, world.Thirst, world.Thirst - seconds * world.Data.ThirstPerSecond);
        });
        void Show(int i, float level, float after) {
            now[i].Level = level; then[i].Level = Math.Max(0, after); then[i].Warn = after <= 0;
            now[i].QueueRedraw(); then[i].QueueRedraw();
        }
    }
    /// <summary>A sleep refusal cut to a tag: what stops it, in a word or two.</summary>
    private static string ShortBlock(string reason) => reason.Contains("thirsty") && reason.Contains("hungry") ? "too hungry and thirsty" : reason.Contains("thirsty") ? "too thirsty" : reason.Contains("hungry") ? "too hungry" : reason.Contains("night") ? "only at night" : "not yet";
}
