using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>The tool rack's slots, what workers have borrowed and who waits for a tool.</summary>
public partial class ToolRackPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        HeaderAction("stash", "Hang spare tools · keeps your best of each", () => moves.StoreAll(thing, SpareTools), Style.ActionRound, "deposit");
        FilterButton();
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 10);
        well.AddChild(StorageGrid(World.ToolSlots, 60));
        var crew = Style.Text("", 14, Style.Ink); crew.Name = "crew"; well.AddChild(crew);
        live.Bind(shown => crew.Text = shown, CrewTools);
        StoreBagGrid(id => world.Holds(thing, id));
        JobButtons();
    }

    private int SpareTools(string item) {
        string kind = world.Data.ItemDefinition(item)!.Tool;
        if (kind == "") return 0;
        return world.Backpack[item] - (world.BestTool(kind) == item ? 1 : 0);
    }

    /// <summary>Which worker has which tool out, and who waits for one.</summary>
    private void CrewTools(StringBuilder text) {
        foreach (var w in world.Workers) {
            if (w.Tool != "") text.Append(w.Name).Append(" has ").Append(world.A(world.Lower(world.Data.Name(w.Tool)))).Append('\n');
            if (w.Missing != "") text.Append(w.Name).Append(" waits for ").Append(world.A(w.Missing)).Append('\n');
        }
        if (text.Length == 0) text.Append("No worker has a tool out.");
        else text.Length--;
    }
}
