using System;
using System.Collections.Generic;
using System.Linq;

namespace ChopChopMeow;

/// <summary>Direct uses outside crafting and construction, which have their own branches in the Item tree.</summary>
internal static class ItemTreeActions {
    public static IEnumerable<string> Of(World world, string item) {
        var data = world.Data;
        var def = data.Items[item];
        foreach (string action in ToolActions(def.Tool)) yield return action;
        if (item == Plumbing.InstallMaterial) yield return "Install plumbing";
        if (item == World.TorchItem) yield return "Source of light";
        if (item == World.CampfireFuel) yield return "Feed campfires";
        if (item == TreePlanting.Acorn) yield return "Plant acorn";
        if (data.Farm.Sow.ContainsKey(item)) yield return "Sow wheat";
        if (world.Compostable(item)) yield return "Make compost";
        if (def.Hunger > 0) yield return "Eat";
        else if (def.Thirst > 0) yield return "Drink";
        foreach (string action in ContainerActions(data, item)) yield return action;
        if (item == Water.Liquid) {
            yield return "Water farm plots";
            yield return "Water saplings";
        }
        if (world.IsWeapon(item)) yield return "Weapon";
        if (data.Market.Sells.ContainsKey(item)) yield return "Can be sold for profit.";
        if (item == Inventory.Coins) {
            yield return "Buy goods";
            yield return "Hire workers";
            yield return "Fund worker wages";
        }
    }

    private static IEnumerable<string> ToolActions(string tool) {
        if (tool == "") yield break;
        var gathering = new SortedSet<GatherAction>(GatherRules.ActionsFor(tool));
        if (tool == GroundHoles.Tool) {
            yield return "Dig holes";
            // Digging at the beach yields sand rather than leaving a hole.
            gathering.Add(GatherAction.DigUpThings);
        }
        if (tool == Mines.DiggingTool) gathering.Add(GatherAction.MineOres);
        foreach (var action in gathering) yield return action switch {
            GatherAction.DigUpThings => "Dig up things.",
            GatherAction.ChopTrees => "Chop trees",
            GatherAction.MineOres => "Mine ores",
            GatherAction.RakeLeaves => "Rake leaves",
            GatherAction.GatherNettles => "Gather nettles without stings",
            GatherAction.CatchFirebugs => "Catch firebugs",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
        if (tool is Mines.DiggingTool or Mines.FittingTool) yield return "Building a mine";
    }

    private static IEnumerable<string> ContainerActions(GameData data, string item) {
        if (World.IsCrate(item)) yield return "Store and transport goods";
        if (Barrel.Is(item)) {
            yield return "Store water or jam";
            yield return "Mount on a cabin roof";
        }
        if (data.VesselOf(item) != null) yield return "Transport liquids";
        if (data.VesselFor(item) == null) yield break;
        var vessels = data.Vessels.Where(vessel => vessel.Liquid == item);
        if (vessels.Any(vessel => vessel.Hunger > 0)) yield return "Food";
        if (vessels.Any(vessel => vessel.Thirst > 0)) yield return "Drink";
    }
}
