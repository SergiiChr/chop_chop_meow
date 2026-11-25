using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    private async Task OpenRecipeMap() {
        await Click((Button)hotbar.FindChild("map", true, false));
        Require(recipeMap.Visible, "The bottom bar must open the Recipe map.");
    }

    private Control RecipeMapCard(string item) => (Control)recipeMap.NodeOf(item)!.FindChild("slot", true, false);
    private Control? ItemTreeCard(string item) => recipeMap.FindChild($"tree_item_{item}", true, false) as Control;

    private async Task ItemTreeShows(string item) =>
        await Until(() => recipeMap.FindChild("item_tree", true, false) is Control tree && tree.IsVisibleInTree() &&
            tree.FindChild("item_tree_name", true, false) is Label name && name.Text == item,
            $"the item tree rooted at {item}", 2);
}
