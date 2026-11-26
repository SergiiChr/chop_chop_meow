using Godot;

namespace ChopChopMeow;

/// <summary>The sleep forecast and Sleep and Wake buttons shared by cabins and straw beds.</summary>
public abstract partial class SleepPage(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected VBoxContainer SleepStats() {
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 12);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); well.AddChild(top);
        top.AddChild(Style.Glyph("line_moon", 20, Style.Ink));
        var until = Style.Text("", 14, Style.Ink); until.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; top.AddChild(until);
        var after = Style.Text("Stats after sleep", 14, Style.Ink);
        after.HorizontalAlignment = HorizontalAlignment.Center; well.AddChild(after);
        var gauges = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        gauges.AddThemeConstantOverride("separation", 36); well.AddChild(gauges);
        var needs = NeedColumn.All();
        foreach (var need in needs) gauges.AddChild(need);

        live.Watch(() => (world.Data.GameMinutes(world.Sleep.SecondsToDawn), (int)world.Health,
            (int)world.Hunger, (int)world.Thirst, Barrel.In(thing.Inventory), world.Sheltered, world.Sleep.SleepingAt), _ => {
                var (seconds, health, hunger, thirst) = world.Sleep.Preview(thing);
                bool dawn = seconds == world.Sleep.SecondsToDawn;
                until.Text = LiveText.Of(text => PageText.AppendGameHours(world, text.Append(dawn ? "until dawn · +" : "up to "), seconds));
                Show(0, "Health", health); Show(1, "Hunger", hunger); Show(2, "Thirst", thirst);

                void Show(int i, string need, float level) {
                    var vial = needs[i].Vial;
                    vial.Level = level; vial.Warn = level <= 25;
                    vial.TooltipText = $"{need} {(dawn ? "at dawn" : "on waking")} {level:0}%";
                    vial.QueueRedraw();
                }
            });
        return well;
    }

    protected Button SleepButton() {
        bool straw = thing.Kind == ThingKind.StrawBed;
        var button = kit.BigButton("Sleep", Style.Enamel, () => {
            if (world.Sleep.SleepingAt == thing.Id) world.Sleep.Wake(); else world.Sleep.Start(thing);
        });
        button.Name = "sleep"; button.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        live.Watch(() => (world.Sleep.SleepingAt == thing.Id, world.Sleep.Block(thing), world.Sheltered), status => {
            var (asleep, blocked, sheltered) = status;
            button.Visible = straw || sheltered == thing.Id;
            button.Text = asleep ? "Wake" : "Sleep";
            button.TooltipText = asleep ? straw ? "Get up, or press W, A, S or D." : "Get up."
                : blocked ?? "Sleep now. Time runs faster when everyone sleeps, unless a wolf is near someone outside a locked cabin.";
            button.Modulate = asleep || blocked == null ? Colors.White : Style.Unready;
        });
        return button;
    }
}
