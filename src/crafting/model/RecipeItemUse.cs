using System.Collections.Generic;

namespace ChopChopMeow;

/// <summary>How one recipe directly uses an item, which can serve more than one role in a batch.</summary>
internal readonly record struct RecipeItemUse(RecipeDef Recipe, bool Ingredient, bool Tool, bool Fuel) {
    /// <summary>Every recipe that calls for the item itself, without following the products to their uses.</summary>
    public static IEnumerable<RecipeItemUse> Of(GameData data, string item) {
        var def = data.Items[item];
        foreach (var recipe in data.Recipes.Values) {
            bool ingredient = recipe.Inputs.ContainsKey(item);
            bool tool = recipe.Tool != "" && (recipe.MadeWith(def)
                || recipe.Station is ThingKind station && data.FixedTool(station, recipe.Tool) == item);
            bool fuel = recipe.Fuels.Contains(item);
            if (ingredient || tool || fuel) yield return new(recipe, ingredient, tool, fuel);
        }
    }

    /// <summary>The item's roles in this recipe, with all roles shown when it has several.</summary>
    public string Label() {
        List<string> roles = [];
        if (Ingredient) roles.Add("Ingredient");
        if (Tool) roles.Add("Tool");
        if (Fuel) roles.Add("Fuel");
        return string.Join(" · ", roles);
    }
}
