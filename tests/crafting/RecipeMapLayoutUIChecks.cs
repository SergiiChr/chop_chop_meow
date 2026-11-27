using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Checks;
using static SetupSteps;

namespace ChopChopMeow;

public partial class Game {
    /// <summary>Alt-click can look up a filter entry in either list without adding or removing it.</summary>
    private async Task CheckAltClickFilterItemsLooksThemUpWithoutEditingFilters() {
        var chest = await OpenFilters(ThingKind.Chest);
        await Click(Named("filter_add_in")!);
        var original = chest.Inventory.InputFilter.ToArray();
        await Click(Named("pick_stone")!, alt: true);
        Require(recipeMap.Visible && recipeMap.Chosen == "stone" && original.SequenceEqual(chest.Inventory.InputFilter),
            "Alt-clicking Stone in the picker must open the Recipe map without changing the input filter.");
        await KeyStroke(Key.Escape);
        await Click(Named("pick_stone")!);
        await Until(() => Named("filter_in_stone") != null, "Stone added to the filter", 2);
        await Click(Named("filter_in_stone")!, alt: true);
        Require(recipeMap.Visible && recipeMap.Chosen == "stone" && chest.Inventory.InputFilter.Contains("stone"),
            "Alt-clicking the listed Stone must open the Recipe map without taking it off the filter.");
        await KeyStroke(Key.Escape);
        await Click(Named("filters_close")!);
        RemoveThings(Hosted, t => t == chest);
        await CloseWithEsc();
    }

    /// <summary>The map can reveal a marked mine without claiming the settlement has built it.</summary>
    private async Task CheckMarkedMineIsDiscoveredWithoutBeingBuilt() {
        var start = Hosted.Player;
        var cliff = await StandAtIronCliff();
        var mine = Hosted.Construction.Build(ThingKind.Mine, cliff.MineSpot)!;
        await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard(mine.Kind.Id()));
        var details = recipeMap.FindChild("map_details", true, false);
        Require(HasLabel(details, "Mine") && HasLabel(details, "Discovered") && !HasLabel(details, "Built before"),
            "A marked site should reveal Mine without saying it was built.");
        await KeyStroke(Key.Escape);
        RemoveThings(Hosted, t => t == mine);
        Warp(Hosted, start);
        await CloseWithEsc();
        await Frames();
    }

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

    /// <summary>Coal sits beside the Steel bar it helps make, and Food contains the wheat chain.</summary>
    private async Task CheckRecipeMapGroupsCoalAndWheatWithTheirChains() {
        await OpenRecipeMap();
        var ore = recipeMap.NodeOf("iron_ore")!;
        var iron = recipeMap.NodeOf("iron_bar")!;
        var coal = recipeMap.NodeOf("coal")!;
        var steel = recipeMap.NodeOf("steel_bar")!;
        var metals = recipeMap.FindChild("lane_Metals", true, false);
        Require(new[] { ore, iron, coal, steel }.All(metals.IsAncestorOf),
            "Iron ore, Iron bar, Coal and Steel bar must all be in Metals.");
        Require(ore.Position.Y == iron.Position.Y && ore.Position.X < iron.Position.X &&
            coal.Position.X == ore.Position.X && steel.Position.X == iron.Position.X &&
            coal.Position.Y == steel.Position.Y && coal.Position.Y > ore.Position.Y,
            "Coal and Steel bar must sit under Iron ore and Iron bar, on their own line together.");
        Require(recipeMap.NodeOf("steel_parts")!.Position.X > steel.Position.X &&
            recipeMap.NodeOf("steel_parts")!.Position.Y >= steel.Position.Y,
            "Steel products must branch to the right of the relocated Steel bar.");
        Require(new[] { "tin_ore", "tin_bar", "tin_sheet", "tin_can" }.All(item => metals.IsAncestorOf(recipeMap.NodeOf(item))),
            "The entire tin chain must be in Metals.");
        var rocks = recipeMap.FindChild("lane_Big rocks and small rocks", true, false);
        Require(new[] { "stone", Beach.Sand }.All(item => rocks.IsAncestorOf(recipeMap.NodeOf(item))),
            "Stone and sand must stay together in Big rocks and small rocks.");
        var food = recipeMap.FindChild("lane_Food", true, false);
        Require(new[] { "wheat_seeds", "wheat", "flour", "dough", "bread" }.All(item => food.IsAncestorOf(recipeMap.NodeOf(item))),
            "The entire wheat chain must be in Food.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Buildings join their main construction material, and stations link to what they make.</summary>
    private async Task CheckBuildingsBranchFromTheirMainMaterialAndShowRecipes() {
        Achieve(Hosted, "stone", "furnace"); await Frames();
        await OpenRecipeMap();
        Require(world.Data.Buildings.Keys.All(kind => recipeMap.FindChildren($"map_{kind.Id()}", "", true, false).Count == 1),
            "Each building must have exactly one card on the main map.");
        var stone = recipeMap.NodeOf("stone")!;
        var furnace = recipeMap.NodeOf("furnace")!;
        var planks = recipeMap.NodeOf("planks")!;
        var cookingTable = recipeMap.NodeOf("cooktable")!;
        Require(stone.GetParent() == furnace.GetParent() && stone.Position.X + MapTree.Pitch == furnace.Position.X &&
            planks.GetParent() == cookingTable.GetParent() && planks.Position.X + MapTree.Pitch == cookingTable.Position.X,
            "The furnace belongs under Stone, and the cooking table under Wood planks even though they are listed last in its cost.");
        await Click(RecipeMapCard("wood"), alt: true);
        await ItemTreeShows("wood");
        Require(ItemTreeCard("furnace") == null, "Wood must not repeat the furnace that belongs to Stone.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard("stone"), alt: true);
        await ItemTreeShows("stone");
        var buildings = recipeMap.FindChild("item_tree_role_Buildings", true, false);
        Require(buildings?.FindChild("tree_item_furnace", true, false) != null, "Stone must show the furnace under Buildings.");
        await Click(ItemTreeCard("furnace")!);
        var details = recipeMap.FindChild("map_details", true, false);
        Require(recipeMap.Chosen == "furnace" && HasLabel(details, "16 Stone") && HasLabel(details, "6 Wood"),
            "A click on the furnace must highlight it and show its complete construction cost.");
        await Click(RecipeMapCard("furnace"), alt: true);
        await ItemTreeShows("furnace");
        var recipes = recipeMap.FindChild("item_tree_role_Recipes", true, false);
        Require(recipes != null && new[] { "charcoal", "iron_bar", "tin_bar", "burnt_steak", "bread" }
            .All(item => recipes.FindChild($"tree_item_{item}", true, false) != null) && ItemTreeCard("steel_bar") == null,
            "The Stone furnace must show the recipes it makes, not those of the Reinforced furnace.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>The Item tree lists all direct actions the player can take with a tool, even when it is not used in recipes.</summary>
    private async Task CheckItemTreeShowsDirectActions() {
        Achieve(Hosted, "steel_shovel", World.TorchItem); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard("steel_shovel"), alt: true);
        await ItemTreeShows("steel_shovel");
        var actions = recipeMap.FindChild("item_tree_role_Actions", true, false);
        var digging = actions?.FindChildren("*", "Label", true, false).OfType<Label>()
            .Select(label => label.Text).Where(text => text.StartsWith("Dig", StringComparison.Ordinal)).Order().ToArray();
        Require(digging != null && digging.SequenceEqual(new[] { "Dig holes", "Dig up things." }),
            "A shovel must list digging for resources once and digging holes separately.");
        Require(!HasLabel(actions!, "Gather nettles without stings") && !HasLabel(actions!, "Chop trees"),
            "The shovel's nettle picking must come under Dig up things., and the shovel must not take the axe's actions.");
        Require(HasLabel(actions!, "Can be sold for profit.") && !HasLabel(actions!, "Pack"),
            "A shovel can be sold for profit, but packing is not an Item tree action.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard("steel_axe"), alt: true);
        await ItemTreeShows("steel_axe");
        actions = recipeMap.FindChild("item_tree_role_Actions", true, false);
        Require(actions != null && HasLabel(actions, "Chop trees") && HasLabel(actions, "Weapon") && !HasLabel(actions, "Fight wolves"),
            "An axe must show both gathering and combat actions.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard(World.TorchItem), alt: true);
        await ItemTreeShows(World.TorchItem);
        Require(ItemTreeActionsShown().SequenceEqual(new[] { "Source of light", "Weapon" }),
            "Torch must show only Source of light and Weapon.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Mining for resources and building a mine are separate uses shared by the relevant tools.</summary>
    private async Task CheckItemTreeGroupsMiningAndMineConstruction() {
        Achieve(Hosted, "steel_pick", "steel_hammer", "wood_hammer"); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard("steel_pick"), alt: true);
        await ItemTreeShows("steel_pick");
        Require(ItemTreeActionsShown().SequenceEqual(new[] {
            "Mine ores", "Building a mine", "Weapon", "Can be sold for profit." }),
            "The pickaxe must group extracting resources separately from preparing a mine.");
        foreach (string hammer in new[] { "wood_hammer", "steel_hammer" }) {
            await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
            await Click(RecipeMapCard(hammer), alt: true);
            await ItemTreeShows(hammer);
            var actions = ItemTreeActionsShown();
            Require(actions.Contains("Building a mine") && actions.Contains("Weapon") &&
                !actions.Contains("Mine ores") && !actions.Contains("Fit mine frames and rails"),
                "A hammer must share Building a mine with the pickaxe without taking its extraction action.");
        }
        await KeyStroke(Key.Escape);
    }

    /// <summary>Water and Berry jam keep their recipes and direct uses, without filling a vessel.</summary>
    private async Task CheckLiquidItemTreesShowDirectUses() {
        Achieve(Hosted, Water.Liquid, BerryJam.Liquid, "meat_stew", "canned_jam"); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard(Water.Liquid), alt: true);
        await ItemTreeShows(Water.Liquid);
        Require(ItemTreeActionsShown().SequenceEqual(new[] { "Drink", "Water farm plots", "Water saplings" }) &&
            ItemTreeCard("meat_stew") != null && ItemTreeCard("dough") != null,
            "Water must show Drink, its watering uses and its cooking recipes, without Fill containers.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard(BerryJam.Liquid), alt: true);
        await ItemTreeShows(BerryJam.Liquid);
        Require(ItemTreeActionsShown().SequenceEqual(new[] { "Food" }) && ItemTreeCard("canned_jam") != null,
            "Berry jam must show Food and its canning recipe, without generic container actions.");
        await KeyStroke(Key.Escape);
        await CloseWithEsc();
    }

    /// <summary>A tree with one result connects the root, branch caption and card on one horizontal line.</summary>
    private async Task CheckSingleResultItemTreeUsesAStraightLine() {
        Achieve(Hosted, "fertilizer"); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard("fertilizer"), alt: true);
        await ItemTreeShows("fertilizer");
        var root = (Control)recipeMap.FindChild("item_tree_root_fertilizer", true, false);
        var branch = recipeMap.FindChild("item_tree_role_Actions", true, false);
        var labels = branch.FindChildren("*", "Label", true, false).OfType<Label>().ToDictionary(label => label.Text);
        float y = root.GetGlobalRect().GetCenter().Y;
        float caption = labels["Actions"].GetGlobalRect().GetCenter().Y;
        float result = ((Control)labels["Sow wheat"].GetParent()).GetGlobalRect().GetCenter().Y;
        Require(Mathf.Abs(y - caption) < 1 && Mathf.Abs(y - result) < 1,
            $"A single result must line up with the root and caption. Got {y}, {caption}, {result}.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Containers show their uses without listing routine handling.</summary>
    private async Task CheckItemTreeShowsContainerUsesWithoutHandling() {
        Achieve(Hosted, Bucket.Item, Barrel.Item, Water.Empty, "crate", "stone"); await Frames();
        await OpenRecipeMap();
        await Click(RecipeMapCard(Bucket.Item), alt: true);
        await ItemTreeShows(Bucket.Item);
        var bucketActions = ItemTreeActionsShown();
        Require(bucketActions.SequenceEqual(new[] { "Transport liquids", "Weapon" }),
            "The Steel bucket must show Transport liquids once, plus Weapon.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard(Water.Empty), alt: true);
        await ItemTreeShows(Water.Empty);
        Require(ItemTreeActionsShown().SequenceEqual(new[] { "Transport liquids" }) &&
            HasLabel(recipeMap.FindChild("item_tree_root_empty_bottle", true, false), "Bottle"),
            "Bottle must share the bucket's liquid action, without Weapon.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard(Barrel.Item), alt: true);
        await ItemTreeShows(Barrel.Item);
        Require(ItemTreeActionsShown().SequenceEqual(new[] { "Store water or jam", "Mount on a cabin roof" }),
            "The Steel barrel must show storage and its cabin use, without Open or Place.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard("crate"), alt: true);
        await ItemTreeShows("crate");
        Require(ItemTreeActionsShown().SequenceEqual(new[] { "Store and transport goods" }),
            "The Wood crate must show storage and transport, without Open, Place or Pack.");
        await Click((Control)recipeMap.FindChild("item_tree_back", true, false));
        await Click(RecipeMapCard("stone"), alt: true);
        await ItemTreeShows("stone");
        Require(recipeMap.FindChild("item_tree_role_Actions", true, false) == null,
            "Stone must show neither a packing action nor a sale the village does not take.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>Looking up any bottle fill in the backpack opens the same Bottle entry without moving it.</summary>
    private async Task CheckBottleFillsShareOneRecipeMapEntry() {
        string[] fills = [GiveWater(Hosted), GiveWater(Hosted, 2), GiveWater(Hosted, 1), GiveVessel(Hosted, Water.Empty, BerryJam.Liquid, BerryJam.Looks.Length - 1)];
        await Frames();
        await KeyStroke(Key.Tab);
        foreach (string fill in fills) {
            int count = world.Carried(fill);
            await Click(BagCellOf(fill), alt: true);
            await Until(() => recipeMap.Visible && recipeMap.Chosen == Water.Empty, "the Bottle card highlighted", 2);
            var details = recipeMap.FindChild("map_details", true, false);
            Require(HasLabel(details, "Bottle") && world.Carried(fill) == count && cursor.Empty,
                "A fill must open Bottle without changing the inventory.");
            Require(fills.Concat(Water.Looks[1..]).Concat(BerryJam.Looks[1..]).All(item => recipeMap.NodeOf(item) == null) && recipeMap.NodeOf(Water.Liquid) != null,
                "Bottle fills must have no cards of their own, and Water must remain on the map.");
            await KeyStroke(Key.Escape);
        }
        foreach (string fill in fills) Drop(Hosted, fill, 1);
        await CloseWithEsc();
    }

    /// <summary>Undiscovered entries on the map and in its details remain anonymous and do not open an Item tree.</summary>
    private async Task CheckUndiscoveredMapItemsStayAnonymous() {
        await OpenRecipeMap();
        var barrel = (ItemSlot)RecipeMapCard("steel_barrel");
        Require(HasLabel(barrel, "?") && !HasLabel(barrel, "Steel barrel") && !ShowsIcon(barrel) &&
            !barrel.TooltipText.Contains("Steel barrel"),
            "An undiscovered entry must show a question mark without a name or icon.");
        Require(recipeMap.NodeOf("steel_barrel")!.FindChild("also_steel_parts", true, false) == null,
            "An undiscovered recipe must not expose ingredients as map chips.");
        await Click(barrel);
        var details = recipeMap.FindChild("map_details", true, false);
        Require(details.FindChild("detail_item", true, false) is MapSlot { State: MapState.Undiscovered } && HasLabel(details, "?") &&
            !HasLabel(details, "Made at") && details.FindChild("map_show", true, false) == null,
            "Selecting the question mark must not reveal the undiscovered item's recipe.");
        await Click(barrel, alt: true);
        Require(recipeMap.FindChild("item_tree", true, false) == null && recipeMap.Chosen == "steel_barrel",
            "Alt-clicking an undiscovered card must stay on the main map.");
        Require(!barrel.TooltipText.Contains(RecipeMap.ItemTreeHint), "An undiscovered card must not offer the Item tree.");
        var sheet = RecipeMapCard("steel_sheet");
        await Click(sheet);
        var thumbnail = (ItemSlot)details.FindChild("use_steel_barrel", true, false);
        Require(thumbnail.Icon == null && thumbnail.FindChild("undiscovered_badge", true, false) is MapSlot { Visible: true } &&
            !thumbnail.TooltipText.Contains("Steel barrel"),
            "A detail thumbnail must show the question-mark badge without revealing an undiscovered product.");
        await Click(thumbnail);
        Require(recipeMap.NodeOf("steel_barrel")!.Lit,
            "A regular click on an undiscovered detail thumbnail must still find it on the main map.");
        await Click(thumbnail, alt: true);
        Require(recipeMap.FindChild("item_tree", true, false) == null,
            "Alt-clicking an undiscovered thumbnail must stay on the main map.");
        await KeyStroke(Key.Escape);
    }

    /// <summary>An open Item tree hides an undiscovered product and shows it once the product is discovered.</summary>
    private async Task CheckDiscoveryRevealsAProductInTheOpenItemTree() {
        await OpenRecipeMap();
        await Click(RecipeMapCard("steel_sheet"), alt: true);
        await ItemTreeShows("steel_sheet");
        var product = (ItemSlot)ItemTreeCard("steel_barrel")!;
        var bottle = ItemTreeCard("empty_bottle")!;
        Require(HasLabel(product, "?") && !product.TooltipText.Contains("Steel barrel") && !ShowsIcon(product) &&
            HasLabel(bottle, "Bottle"),
            "The Item tree must distinguish undiscovered products from known ones.");
        var tree = recipeMap.FindChild("item_tree", true, false);
        await Click(product, alt: true);
        Require(recipeMap.FindChild("item_tree", true, false) == tree,
            "Alt-clicking an undiscovered product must stay in the current Item tree.");
        Achieve(Hosted, "steel_barrel");
        await Until(() => HasLabel(product, "Steel barrel") && ShowsIcon(product),
            "the newly discovered barrel showing its name and icon in the open Item tree", 2);
        await Click(product, alt: true);
        await ItemTreeShows("steel_barrel");
        await Click((Control)recipeMap.FindChild("item_tree_root_steel_barrel", true, false));
        var details = recipeMap.FindChild("map_details", true, false);
        await Until(() => recipeMap.Chosen == "steel_barrel" && HasLabel(details, "Steel barrel") && HasLabel(details, "Made at"),
            "the discovered barrel's recipe appearing in the main map", 2);
        Require(recipeMap.NodeOf("steel_barrel")!.FindChild("also_steel_parts", true, false) is Button { Icon: not null },
            "Discovery must also reveal the recipe's additional resources on the map card.");
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
