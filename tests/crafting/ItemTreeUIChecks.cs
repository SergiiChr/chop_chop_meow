using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Each item opens its own direct recipes, and an ordinary click goes back to that item's place on the map.</summary>
    private async Task CheckItemTreeFollowsOneDirectRecipeAtATime() {
        await OpenRecipeMap();
        await Click(RecipeMapCard("steel_hammer"), alt: true);
        await ItemTreeShows("Steel hammer");
        Require(ItemTreeCard("tin_can") != null && ItemTreeCard("steel_parts") != null,
            "The steel hammer tree should show every direct recipe, including tin cans and steel parts.");
        Require(ItemTreeCard("canned_jam") == null && ItemTreeCard("nettle_rope") == null,
            "The tree should leave out the next step and recipes whose tool may not be steel.");
        await Click(ItemTreeCard("tin_can")!, alt: true);
        await ItemTreeShows("Tin can");
        await Click(ItemTreeCard("canned_jam")!);
        await Until(() => recipeMap.Chosen == "canned_jam" && recipeMap.NodeOf("canned_jam")!.Lit,
            "the main map showing canned jam highlighted", 2);
        Require(recipeMap.FindChild("map_rows", true, false) is Control rows && rows.IsVisibleInTree() &&
            rows.GetGlobalRect().Encloses(recipeMap.NodeOf("canned_jam")!.GetGlobalRect()),
            "The highlighted item must be scrolled into view on the main map.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>The Back button keeps the selection and scroll position the player left on the main map.</summary>
    private async Task CheckItemTreeBackRestoresTheMainMap() {
        await OpenRecipeMap();
        var mallet = RecipeMapCard("wood_hammer");
        await Click(mallet);
        var rows = (ScrollContainer)recipeMap.FindChild("map_rows", true, false);
        int scroll = rows.ScrollVertical;
        await Click(mallet, alt: true);
        await ItemTreeShows("Wooden mallet");
        Require(ItemTreeCard("nettle_rope") != null && ItemTreeCard("tin_can") == null,
            "The mallet should make nettle rope but not the tin cans that take a steel hammer.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        Require(rows.IsVisibleInTree() && rows.ScrollVertical == scroll && recipeMap.Chosen == "wood_hammer",
            "Back should show the previous selection at the same scroll position.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>An item shown in the main map's details also opens its own item tree.</summary>
    private async Task CheckItemTreeOpensFromADetailThumbnail() {
        await OpenRecipeMap();
        await Click(RecipeMapCard("tin_sheet"));
        await Click((Control)recipeMap.FindChild("use_tin_can", true, false), alt: true);
        await ItemTreeShows("Tin can");
        Require(ItemTreeCard("canned_jam") != null, "The detail icon should open the tin can's direct recipes.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Recipes include all direct roles, with a product listed once when it uses the item in two ways.</summary>
    private async Task CheckItemTreeDistinguishesIngredientsFuelAndFixedTools() {
        await OpenRecipeMap();
        await Click(RecipeMapCard("coal"), alt: true);
        await ItemTreeShows("Coal");
        Require(HasLabel(ItemTreeCard("steel_bar")!, "Ingredient · Fuel") && HasLabel(ItemTreeCard("iron_bar")!, "Fuel"),
            "Steel bars use coal as an ingredient and fuel, while iron bars burn it only as fuel.");
        Require(recipeMap.FindChildren("tree_item_steel_bar", "", true, false).Count == 1,
            "A recipe with two roles for the item must appear only once.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard("steel_pipe"), alt: true);
        await ItemTreeShows("Steel pipe");
        Require(HasLabel(ItemTreeCard("glass_jar")!, "Tool"), "Glass jars use a steel pipe fitted to the furnace as a tool.");
        await KeyStroke(Key.Escape);
    }
}
