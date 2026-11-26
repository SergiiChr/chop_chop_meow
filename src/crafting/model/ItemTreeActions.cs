using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>Direct uses outside crafting and construction, which have their own branches in the Item tree.</summary>
internal static class ItemTreeActions {
    public static IEnumerable<string> Of(World world, string item) {
        var data = world.Data;
        var def = data.Items[item];
        foreach (string action in ToolActions(def.Tool)) yield return action;
        if (data.Recipes.Values.Any(recipe => recipe.Tool == GlassBlowing.Tool && recipe.Station is ThingKind station &&
            data.FixedTool(station, recipe.Tool) == item)) yield return "Blow glass";
        if (item == Plumbing.InstallMaterial) yield return "Install plumbing";
        if (item == World.TorchItem) {
            yield return "Light torch";
            yield return "Wave torch at wolves";
        }
        if (item == World.CampfireFuel) yield return "Feed campfires";
        if (item == TreePlanting.Acorn) yield return "Plant acorn";
        if (data.Farm.Sow.ContainsKey(item)) yield return "Sow wheat";
        if (world.Compostable(item)) yield return "Make compost";
        if (def.Hunger > 0) yield return "Eat";
        else if (def.Thirst > 0) yield return "Drink";
        foreach (string action in ContainerActions(data, item)) yield return action;
        if (data.Market.Sells.ContainsKey(item)) yield return "Pack for sale";
        if (item == Inventory.Coins) {
            yield return "Buy goods";
            yield return "Hire workers";
            yield return "Fund worker wages";
        }
    }

    private static IEnumerable<string> ToolActions(string tool) {
        if (tool == GroundHoles.Tool) {
            yield return "Dig ground";
            yield return "Dig beach sand";
        }
        foreach (string action in GatherRules.ActionsFor(tool)) yield return action;
        if (tool == World.AttackTool) yield return "Fight wolves";
        if (tool == Mines.DiggingTool) {
            yield return "Dig mine entrances";
            yield return "Work open mines";
        }
        if (tool == Mines.FittingTool) yield return "Fit mine frames and rails";
    }

    private static IEnumerable<string> ContainerActions(GameData data, string item) {
        if (World.IsCrate(item)) {
            yield return "Place crate";
            yield return "Store and transport goods";
        }
        if (Barrel.Is(item)) {
            yield return "Place barrel";
            yield return "Open barrel";
            yield return "Store water or jam";
        }
        if (data.Vessels.Any(v => v.LitersIn(item) is >= 0 and int fill && fill < v.Capacity))
            yield return "Fill from stations";
        if (Water.BottleLiters(item) is >= 0 and int water && water < Water.Capacity ||
            Bucket.Liters(item) is >= 0 and < Bucket.Capacity) yield return "Collect water at the lake";
        if (data.VesselOf(item) is VesselDef filled && filled.LitersIn(item) > 0) {
            yield return "Pour into stations";
            yield return "Pour into barrels";
            if (filled.Liquid == Water.Liquid) {
                yield return "Water saplings";
                yield return "Water farm plots";
            }
        }
        if (data.VesselFor(item) != null) yield return "Fill containers";
        if (data.Vessels.Any(v => v.Liquid == item && data.Items[v.Full].Hunger > 0))
            yield return "Eat from a bottle";
    }
}
