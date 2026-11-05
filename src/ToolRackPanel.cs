using System.Text;
using Godot;

namespace ChopChopMeow;

/// <summary>A tool rack: four slots that take any tool, then which tools workers have out and who waits for one.</summary>
public partial class ToolRackPanel(PageTools tools, Thing subject) : ThingPage(tools, subject) {
    protected override void Body() {
        HeaderAction("stash", "Hang spare tools · keeps your best of each", () => moves.StoreAll(thing, id => world.BestTool(world.Data.Items[id].Tool) == id ? world.Backpack[id] - 1 : world.Backpack[id]), Style.ActionRound, "deposit");
        FilterButton();
        var well = Well(new VBoxContainer()); well.AddThemeConstantOverride("separation", 10);
        well.AddChild(StorageGrid(World.ToolSlots, 60));
        var crew = Style.Text("", 14, Style.Ink); crew.Name = "crew"; well.AddChild(crew);
        live.Bind(shown => crew.Text = shown, CrewTools);
        StoreBagGrid("Click: hang a tool · Ctrl: all of them · Drag to move", id => world.Holds(thing, id) && thing.Inventory.Accepts(id));
        JobButtons();
    }

    /// <summary>Which worker has which tool out, and who waits for one.</summary>
    private void CrewTools(StringBuilder text) {
        foreach (var w in world.Workers) {
            if (w.Tool != "") {
                text.Append(w.Name).Append(" has a ");
                foreach (char c in world.Data.Name(w.Tool)) text.Append(char.ToLowerInvariant(c));
                text.Append('\n');
            }
            if (w.Missing != "") text.Append(w.Name).Append(" waits for ").Append("aeiou".Contains(w.Missing[0]) ? "an " : "a ").Append(w.Missing).Append('\n');
        }
        if (text.Length == 0) text.Append("No worker has a tool out.");
        else text.Length--;
    }
}
