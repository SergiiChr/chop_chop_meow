using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Selecting charcoal shows its details in a vertical panel alongside the map.</summary>
    private async Task CheckRecipeMapDetailsAreStackedOnTheRight() {
        await OpenRecipeMap();
        await Click(RecipeMapCard("charcoal"));
        var map = (Control)recipeMap.FindChild("map_rows", true, false);
        var details = (Control)recipeMap.FindChild("map_details", true, false);
        var mapBounds = map.GetGlobalRect();
        var detailBounds = details.GetGlobalRect();
        Require(detailBounds.Position.X >= mapBounds.End.X && detailBounds.Position.Y < mapBounds.End.Y,
            "The details panel must sit beside the map on its right.");
        var labels = details.FindChildren("*", "Label", true, false).OfType<Label>().ToArray();
        var sections = new[] { "Charcoal", "Made at", "Requires", "Used for" }
            .Select(text => labels.First(label => label.Text == text).GetGlobalRect()).ToArray();
        for (int i = 1; i < sections.Length; i++)
            Require(sections[i].Position.Y >= sections[i - 1].End.Y,
                "The details panel must stack the item and recipe sections vertically.");
        var show = (Control)details.FindChild("map_show", true, false);
        Require(show.GetGlobalRect().Position.Y > sections[^1].End.Y && detailBounds.Encloses(show.GetGlobalRect()),
            "The recipe button must stay inside the sidebar below the recipe details.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Steel sheet branches to three separate products in one column, without station labels.</summary>
    private async Task CheckItemTreeBranchesToEachRecipeProduct() {
        await OpenRecipeMap();
        await Click(RecipeMapCard("steel_sheet"), alt: true);
        await ItemTreeShows("Steel sheet");
        var bottle = ItemTreeCard("empty_bottle");
        var barrel = ItemTreeCard("steel_barrel");
        var bucket = ItemTreeCard("steel_bucket");
        Require(bottle != null && barrel != null && bucket != null,
            "Steel sheet must branch to the bottle, barrel and bucket.");
        Rect2[] cards = [bottle!.GetGlobalRect(), barrel!.GetGlobalRect(), bucket!.GetGlobalRect()];
        var column = cards.OrderBy(rect => rect.Position.Y).ToArray();
        Require(column.All(rect => rect.Position.X == column[0].Position.X) &&
            column[1].Position.Y >= column[0].End.Y && column[2].Position.Y >= column[1].End.Y,
            "Each product must have a separate row in the item tree.");
        var tree = recipeMap.FindChild("item_tree", true, false);
        Require(!HasLabel(tree, "Crafting bench") && !HasLabel(tree, "Anvil") && HasLabel(tree, "Ingredient"),
            "The item tree should name how the item is used but not where it is made.");
        var labels = tree.FindChildren("*", "Label", true, false).OfType<Label>();
        Require(!labels.Any(label => label.Text is "Selected item" or "Direct recipes"),
            "The Item tree must show its cards without column captions.");
        await KeyStroke(Key.Escape);
    }
}
