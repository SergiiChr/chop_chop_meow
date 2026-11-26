using System.Linq;
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

    private static bool ShowsIcon(Control card) =>
        card.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Any(icon => icon.Texture != null);

    private string[] ItemTreeActionsShown() =>
        recipeMap.FindChild("item_tree_role_Actions", true, false)?.FindChildren("*", "Label", true, false)
            .OfType<Label>().Select(label => label.Text).Where(text => text != "Actions").ToArray() ?? [];

    private async Task ItemTreeShows(string item) =>
        await Until(() => recipeMap.FindChild("item_tree", true, false) is Control tree && tree.IsVisibleInTree() &&
            tree.FindChild($"item_tree_root_{item}", true, false) is Control,
            $"the item tree rooted at {item}", 2);
}
