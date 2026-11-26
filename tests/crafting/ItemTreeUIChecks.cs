using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Each item opens its own direct recipes, and an ordinary click goes back to that item's place on the map.</summary>
    private async Task CheckItemTreeFollowsOneDirectRecipeAtATime() {
        Achieve(Hosted, "steel_hammer", "tin_can"); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard("steel_hammer"), alt: true);
        await ItemTreeShows("steel_hammer");
        Require(ItemTreeCard("tin_can") != null && ItemTreeCard("steel_parts") != null,
            "The steel hammer tree should show every direct recipe, including tin cans and steel parts.");
        Require(ItemTreeCard("canned_jam") == null && ItemTreeCard("nettle_rope") == null,
            "The tree should leave out the next step and recipes whose tool may not be steel.");
        await Click(ItemTreeCard("tin_can")!, alt: true);
        await ItemTreeShows("tin_can");
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
        Achieve(Hosted, "wood_hammer"); await Frames();
        await OpenRecipeMap();
        var mallet = RecipeMapCard("wood_hammer");
        await Click(mallet);
        var rows = (ScrollContainer)recipeMap.FindChild("map_rows", true, false);
        int scroll = rows.ScrollVertical;
        await Click(mallet, alt: true);
        await ItemTreeShows("wood_hammer");
        Require(ItemTreeCard("nettle_rope") != null && ItemTreeCard("tin_can") == null,
            "The mallet should make nettle rope but not the tin cans that take a steel hammer.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        Require(rows.IsVisibleInTree() && rows.ScrollVertical == scroll && recipeMap.Chosen == "wood_hammer",
            "Back should show the previous selection at the same scroll position.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>An item shown in the main map's details also opens its own item tree.</summary>
    private async Task CheckItemTreeOpensFromADetailThumbnail() {
        Achieve(Hosted, "tin_can"); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard("tin_sheet"));
        await Click((Control)recipeMap.FindChild("use_tin_can", true, false), alt: true);
        await ItemTreeShows("tin_can");
        Require(ItemTreeCard("canned_jam") != null, "The detail icon should open the tin can's direct recipes.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Coal appears under both of its uses in steel bars, and a fixed tool has its own branch.</summary>
    private async Task CheckItemTreeDistinguishesIngredientsFuelAndFixedTools() {
        Achieve(Hosted, "steel_pipe"); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard("coal"), alt: true);
        await ItemTreeShows("coal");
        var fuel = recipeMap.FindChild("item_tree_role_Fuel", true, false) as Control;
        var ingredient = recipeMap.FindChild("item_tree_role_Ingredient", true, false) as Control;
        Require(fuel != null && ingredient != null && recipeMap.FindChild("item_tree_role_Tool", true, false) == null,
            "Coal must show Fuel and Ingredient branches, without an empty Tool branch.");
        Require(fuel!.GetGlobalRect().End.Y <= ingredient!.GetGlobalRect().Position.Y,
            "Fuel and Ingredient must be laid out as separate branches.");
        Require(HasLabel(fuel, "Fuel") && HasLabel(ingredient, "Ingredient"), "Each branch must name its use.");
        Require(fuel.FindChild("tree_item_steel_bar", true, false) != null &&
            ingredient.FindChild("tree_item_steel_bar", true, false) != null &&
            fuel.FindChild("tree_item_iron_bar", true, false) != null &&
            ingredient.FindChild("tree_item_iron_bar", true, false) == null,
            "Steel bars must appear under both uses of coal, and iron bars only under Fuel.");
        Require(recipeMap.FindChildren("tree_item_steel_bar", "", true, false).Count == 2,
            "Steel bars must appear exactly once on each of the two branches.");
        await Click((Control)ingredient.FindChild("tree_item_steel_bar", true, false));
        await Until(() => recipeMap.Chosen == "steel_bar" && recipeMap.NodeOf("steel_bar")!.Lit,
            "the map highlighting Steel bar after a click on the Ingredient branch", 2);
        await Click(RecipeMapCard("steel_pipe"), alt: true);
        await ItemTreeShows("steel_pipe");
        var tool = recipeMap.FindChild("item_tree_role_Tool", true, false);
        Require(tool != null && HasLabel(tool, "Tool") && tool.FindChild("tree_item_glass_jar", true, false) != null,
            "Glass jars must appear under Tool when a steel pipe is the one fitted to the furnace.");
        Require(ItemTreeActionsShown().SequenceEqual(new[] { "Install plumbing" }),
            "The steel pipe must show plumbing without repeating its glassblowing use or adding Weapon.");
        await KeyStroke(Key.Escape);
    }
}
