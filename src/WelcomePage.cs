using Godot;

namespace ChopChopMeow;

/// <summary>The page a new game opens on.</summary>
public partial class WelcomePage(PageTools tools) : PagePanel(tools) {
    protected override void Build() {
        AddLabel("The woods are quiet. For now.", 21);
        AddLabel("First, secure water, a cabin and a fire. Then use your steel axe and your hands to build a workshop, trade berries for a pickaxe, and work toward steel.", 16, Style.Beige);
        AddLabel("FIRST STEPS", 14);
        AddLabel("1  Collect water at the pond and keep a bottle filled. The sea is salt.\n2  Gather wood and stone; build a cabin and campfire before night.\n3  With your steel axe, fell trees. Gather stone and berries by hand. Knap a chisel, carve planks and a mallet, then build a bench.\n4  Make crates, build a trading post and sell berries. Buy your first pickaxe, then mine toward iron and steel.\n5  Press Esc any time to see your current goal.");
        AddLabel("A bottle holds three sips and can be refilled at the lake. Without one, drink at the source. Each gathering action takes time, so plan ahead.", 15, Style.Beige);
        AddButton("Into the clearing", panel.Close);
    }
}
