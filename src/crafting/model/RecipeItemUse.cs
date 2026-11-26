using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>One role an item plays in a recipe that uses it directly.</summary>
internal readonly record struct RecipeItemUse(RecipeDef Recipe, RecipeItemRole Role) {
    /// <summary>Every recipe that calls for the item itself, once for each role the item plays.</summary>
    public static IEnumerable<RecipeItemUse> Of(GameData data, string item) {
        var def = data.Items[item];
        foreach (var recipe in data.Recipes.Values) {
            if (recipe.Fuels.Contains(item)) yield return new(recipe, RecipeItemRole.Fuel);
            if (recipe.Inputs.ContainsKey(item)) yield return new(recipe, RecipeItemRole.Ingredient);
            if (recipe.Tool != "" && (recipe.MadeWith(def)
                || recipe.Station is ThingKind station && data.FixedTool(station, recipe.Tool) == item))
                yield return new(recipe, RecipeItemRole.Tool);
        }
    }
}
