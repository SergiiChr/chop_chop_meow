using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static PlayerSteps;
using static SetupSteps;
using static WorldSteps;

internal static class LanternCraftChecks {
    public static void Run(GameData data) {
        Test(CheckTheBenchMakesEmptyLanternsWithASteelHammer, data);
        Test(CheckPackingTwoLanternsKeepsThemSeparate, data);
    }

    private static void CheckTheBenchMakesEmptyLanternsWithASteelHammer(GameData data) {
        Require(data.Recipes.ContainsKey("lantern"), "the crafting bench should have a lantern recipe");
        var world = EmptyWorld(data);
        Give(world, "glass_jar", 3); Give(world, "steel_sheet", 3); Give(world, "wood_parts", 3);
        var bench = BenchLeftWithAMallet(world, "lantern");
        Require(!bench.Processing && bench.Rounds == 0, "a lantern should not be made with a wooden mallet");
        SwapMalletForSteelHammer(world, bench);
        world.QueueBatch(bench, "lantern");
        Work(world, 15, () => bench.Rounds == 2);
        Require(bench.Rounds == 2, "the bench should make both lanterns with a steel hammer and no firebugs");
        world.Collect(bench);
        string[] lanterns = world.Backpack.Items.Keys.Where(id => Lanterns.Id(id) > 0).ToArray();
        Require(lanterns.Length == 2 && lanterns.All(id => Lanterns.BugCount(world.Backpack, id) == 0), "both lanterns should have their own empty slot");
        Require(world.Backpack["glass_jar"] == 1 && world.Backpack["steel_sheet"] == 1 && world.Backpack["wood_parts"] == 1,
            "two lanterns should use two jars, two sheets and two wood parts");
    }

    private static void CheckPackingTwoLanternsKeepsThemSeparate(GameData data) {
        var world = EmptyWorld(data);
        string first = GiveLantern(world), second = GiveLantern(world);
        Require(Give(world, "crate") == 1, "a crate should fit beside the two lanterns");
        var packer = world.Add(ThingKind.Packer, world.Player + Vector2.Down);
        world.SetPacking(packer, "lantern");
        Require(world.LoadInput(packer, "crate", 1) == 1 && world.LoadInput(packer, first, 1) == 1 && world.LoadInput(packer, second, 1) == 1,
            "the packing station set to Lantern should accept both lanterns and a crate");
        Work(world, 20, () => packer.Output!.First() is string crate && world.Packed(crate)?.Count == 2);
        string packed = packer.Output!.First() ?? "";
        var box = world.Packed(packed);
        Require(box != null && box[first] == 1 && box[second] == 1 && world.FilterEntryOf(packed) == World.CrateOf(Lanterns.Item),
            "two empty lanterns should pack as one kind of good without losing their individual items");
    }
}
