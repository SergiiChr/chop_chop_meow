using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Selecting charcoal shows its details in a vertical panel alongside the map.</summary>
    private async Task CheckRecipeMapDetailsAreStackedOnTheRight() {
        Achieve(Hosted, "charcoal"); await Frames();
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

    /// <summary>Undiscovered entries can be followed in the map, but only reveal their identity once discovered.</summary>
    private async Task CheckUndiscoveredMapItemsStayAnonymous() {
        await OpenRecipeMap();
        var barrel = (ItemSlot)RecipeMapCard("steel_barrel");
        Require(HasLabel(barrel, "?") && !HasLabel(barrel, "Steel barrel") &&
            !barrel.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Any(icon => icon.Texture != null) &&
            !barrel.TooltipText.Contains("Steel barrel"), "An undiscovered entry must show a question mark without a name or icon.");
        await Click(barrel);
        var details = recipeMap.FindChild("map_details", true, false);
        Require(details.FindChild("detail_item", true, false) is Button { Text: "?", Icon: null } &&
            !HasLabel(details, "Made at") && details.FindChild("map_show", true, false) == null,
            "Selecting the question mark must not reveal the undiscovered item's recipe.");
        await Click(barrel, alt: true);
        await ItemTreeShows("steel_barrel");
        var unknownRoot = recipeMap.FindChild("item_tree_root_steel_barrel", true, false);
        Require(HasLabel(unknownRoot, "?") && !HasLabel(unknownRoot, "Steel barrel"),
            "Following an undiscovered item must not reveal its name in the Item tree.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        var sheet = RecipeMapCard("steel_sheet");
        await Click(sheet);
        var thumbnail = (ItemSlot)details.FindChild("use_steel_barrel", true, false);
        Require(thumbnail.Text == "?" && thumbnail.Icon == null && !thumbnail.TooltipText.Contains("Steel barrel"),
            "A detail thumbnail must not reveal an undiscovered product.");
        await Click(sheet, alt: true);
        await ItemTreeShows("steel_sheet");
        var product = (ItemSlot)ItemTreeCard("steel_barrel")!;
        var bottle = ItemTreeCard("empty_bottle")!;
        Require(HasLabel(product, "?") && !product.TooltipText.Contains("Steel barrel") &&
            !product.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Any(icon => icon.Texture != null) &&
            HasLabel(bottle, "Empty bottle"), "The Item tree must distinguish undiscovered products from known ones.");
        Achieve(Hosted, "steel_barrel");
        await Until(() => HasLabel(product, "Steel barrel") &&
            product.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Any(icon => icon.Texture != null),
            "the newly discovered barrel showing its name and icon in the open Item tree", 2);
        await Click(product);
        await Until(() => recipeMap.Chosen == "steel_barrel" && HasLabel(details, "Steel barrel") && HasLabel(details, "Made at"),
            "the discovered barrel's recipe appearing in the main map", 2);
        await KeyStroke(Key.Escape);
    }

    /// <summary>Steel sheet branches to three separate products in one column, without station labels.</summary>
    private async Task CheckItemTreeBranchesToEachRecipeProduct() {
        await OpenRecipeMap();
        await Click(RecipeMapCard("steel_sheet"), alt: true);
        await ItemTreeShows("steel_sheet");
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
