namespace ChopChopMeow;

/// <summary>What a marked planting spot needs before digging can start.</summary>
public partial class TreePlantingPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() => AddLabel("Hold right mouse here with a shovel and an acorn in your backpack.", color: Style.Rim);
}
