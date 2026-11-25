using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>Checks of the player's hunger and thirst: what food and water give, sprinting, and the stomach's growl.</summary>
public static class NeedsChecks {
    public static void Run(GameData data) {
        Test(CheckStomachGrowlsAsFoodRunsLow, data);
        Test(CheckSprintBurnsFood, data);
        Test(CheckFoodsFeedAndWaterByTheirDataWithoutHealing, data);
        Test(CheckBerriesFeedMoreWhenPrepared, data);
    }

    private static void CheckStomachGrowlsAsFoodRunsLow(GameData data) {
        var world = EmptyWorld(data);
        var heard = Listen(world);
        // Seconds that burn a hundredth of a full stomach.
        float hundredth = 1 / data.HungerPerSecond;
        SetNeeds(world, hunger: 21.5f); world.Tick(hundredth);
        Require(heard.Count == 0, "a stomach with over a fifth of its food left should stay quiet");
        world.Tick(hundredth);
        Require(heard.SequenceEqual(new[] { "stomach_growl" }), "the stomach should growl as food falls under a fifth");
        world.Tick(hundredth);
        Require(heard.Count == 1, "the stomach should stay quiet until another twentieth of its food is gone");
        SetNeeds(world, hunger: 15.5f); world.Tick(hundredth);
        Require(heard.Count == 2, "the stomach should growl again as food falls under 15%");
    }

    private static void CheckSprintBurnsFood(GameData data) {
        (float Hunger, float Thirst) Burned(bool sprinting) {
            var world = new World(data);
            world.Me.Sprinting = sprinting;
            for (int i = 0; i < 10; i++) world.Tick(1);
            return (100 - world.Hunger, 100 - world.Thirst);
        }
        var (walking, sprinting) = (Burned(false), Burned(true));
        Require(Math.Abs(sprinting.Hunger / walking.Hunger - data.SprintDrain) < .001f && Math.Abs(sprinting.Thirst / walking.Thirst - data.SprintDrain) < .001f,
                "sprinting must drain food and drink sprintDrain times as fast");
    }

    private static void CheckBerriesFeedMoreWhenPrepared(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "berries", 1); Give(world, "crushed_berries", 1); Give(world, "canned_jam", 1);
        Give(world, BerryJam.Bottle);
        SetNeeds(world, hunger: 20);
        world.Consume("berries");
        Require(world.Hunger == 25, "a berry should restore five hunger");
        world.Consume("crushed_berries");
        Require(world.Hunger == 80, "crushed berries should restore their ten berries plus five");
        world.Consume("canned_jam");
        Require(world.Hunger == 100 && world.Backpack["canned_jam"] == 0, "a can of jam should fill but not overfill the hunger bar");
        SetNeeds(world, hunger: 1);
        world.Consume(BerryJam.Bottle);
        Require(world.Hunger == 100 && world.Backpack[BerryJam.Bottles[2]] == 1, "drinking jam should use one liter and leave the bottle");
    }

    /// <summary>Foods restore the hunger and thirst their data says and no health, and raw ones only say they need cooking.</summary>
    private static void CheckFoodsFeedAndWaterByTheirDataWithoutHealing(GameData data) {
        var world = EmptyWorld(data);
        SetNeeds(world, 20, 20, 50);
        Give(world, "meat_stew", 1); Give(world, "wheat", 1);
        world.Consume("meat_stew");
        var stew = data.Items["meat_stew"];
        Require(world.Hunger == 20 + stew.Hunger, "meat stew should feed by its data");
        Require(world.Thirst == 20 + stew.Thirst, "meat stew should water by its data");
        Require(world.Health == 50, "meat stew should not heal: only sleep does");
        Require(world.Backpack["meat_stew"] == 0, "meat stew should be used up");
        world.Consume("wheat");
        Require(world.Backpack["wheat"] == 1, "raw wheat should not be eaten as it is");
        Require(data.IsFood("wheat"), "raw wheat should count as food for compost and wolves");
        Require(data.IsConsumable("wheat"), "raw wheat should count as consumable");
        Require(!data.IsFood("water"), "water should not be food");
        Require(data.IsConsumable("water"), "water should be drinkable");
        Require(!data.IsFood("tin_can"), "cans should not be food");
    }
}
