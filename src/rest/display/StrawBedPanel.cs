using Godot;

namespace ChopChopMeow;

/// <summary>A straw bed's remaining life, salvage window and sleep controls.</summary>
public partial class StrawBedPanel(PageTools tools, Thing subject) : SleepPage(tools, subject) {
    protected override void Body() {
        AddLabel(world.Data.Buildings[ThingKind.StrawBed].Description);
        var life = Style.Wrapped("", 14); AddChild(life);
        live.Watch(() => (Minutes: world.Data.GameMinutes(StrawBed.Left(world, thing)), Salvage: StrawBed.Salvage(world, thing)), status => {
            life.Text = LiveText.Of(text => {
                PageText.AppendGameHours(world, text.Append("Falls apart in "), StrawBed.Left(world, thing));
                text.Append(status.Salvage ? ". Taking it down now returns its materials." : ". Taking it down returns nothing.");
            });
        });
        SleepStats();
        AddLabel("A straw bed does not shelter you from wolves.", 14);
        Row().AddChild(SleepButton());
    }
}
