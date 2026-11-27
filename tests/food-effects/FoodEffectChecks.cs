using System;
using ChopChopMeow;
using Godot;
using static Checks;
using static SetupSteps;
using static WorldSteps;

/// <summary>What eating cooked food does to a player.</summary>
public static class FoodEffectChecks {
    public static void Run(GameData data) {
        Test(BreadSpeedsUpSprintingForFourHours, data);
        Test(AnotherServingExtendsCarbLoadWithoutStackingSpeed, data);
        Test(SteakHealsTheAwakePlayerForFourHours, data);
        Test(StewExtendsProteinRecoveryWithoutStackingHealing, data);
        Test(SleepAndProteinRecoveryHealTogether, data);
        Test(FreshAndCannedStewFillTheSecondLapAndStartRecovery, data);
        Test(JamHealsAndFillsTheRaisedLimit, data);
        Test(VitaminsProtectThePlayerAndDoNotTakeAwayHealthBelowOneHundred, data);
        Test(HealingRefillsSpentBonusHealth, data);
        Test(VitaminsExpireAfterOneDay, data);
        Test(LongEffectsKeepCountingDownInSmallSteps, data);
        Test(SavingAndLoadingKeepsTheEffectsAndTheirTimeLeft, data);
    }

    private static void BreadSpeedsUpSprintingForFourHours(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "bread");
        var jog = world.PlayerSpeed;
        world.Consume("bread");
        Require(world.PlayerSpeed == jog, "bread should not make ordinary movement faster");
        world.Me.Sprinting = true;
        Require(Math.Abs(world.PlayerSpeed - 8.4f) < .001f, "bread should make a player sprint 25% faster");
        SafeTick(world, 139);
        Require(Math.Abs(world.PlayerSpeed - 8.4f) < .001f, "Carb load should last almost four hours");
        SafeTick(world, 2);
        Require(Math.Abs(world.PlayerSpeed - 6.72f) < .001f, "Carb load should end after four hours");
    }

    private static void AnotherServingExtendsCarbLoadWithoutStackingSpeed(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "bread"); Give(world, "canned_jam");
        world.Consume("bread");
        SafeTick(world, 100);
        world.Consume("canned_jam");
        world.Me.Sprinting = true;
        Require(Math.Abs(world.PlayerSpeed - 8.4f) < .001f && world.FoodEffects.SecondsLeft(FoodEffect.CarbLoad) == 180,
            "jam should add four hours to Carb load without repeating the speed bonus");
    }

    private static void SteakHealsTheAwakePlayerForFourHours(GameData data) {
        var world = EmptyWorld(data);
        SetNeeds(world, health: 50);
        Give(world, "burnt_steak"); world.Consume("burnt_steak");
        Require(world.Health == 50, "steak should start recovery without instantly healing");
        SafeTick(world, 60);
        Require(Math.Abs(world.Health - 57.2f) < .01f, "steak should heal an awake player at the sleep healing rate");
        SafeTick(world, 100);
        Require(Math.Abs(world.Health - 66.8f) < .01f && world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) == 0,
            "recovery should stop after four hours");
    }

    private static void StewExtendsProteinRecoveryWithoutStackingHealing(GameData data) {
        var world = EmptyWorld(data);
        SetNeeds(world, health: 50);
        Give(world, "burnt_steak"); world.Consume("burnt_steak");
        Give(world, "meat_stew"); world.Consume("meat_stew");
        Require(world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) == 280, "stew should add four hours to the steak's recovery");
        world.Tick(50, sleeping: true);
        Require(world.Health == 56, "two servings should heal at the same rate as one");
    }

    private static void SleepAndProteinRecoveryHealTogether(GameData data) {
        var world = EmptyWorld(data);
        SetNeeds(world, health: 10);
        Give(world, "burnt_steak"); world.Consume("burnt_steak");
        SkipClock(world, world.Sleep.SecondsToDawn - 100);
        var bed = world.Add(ThingKind.StrawBed, world.Player + Vector2.Right);
        Require(Math.Abs(world.Sleep.Preview(bed).Health - 34) < .01f, "the sleep forecast should add sleep healing and Protein recovery");
        world.Sleep.Start(bed);
        Require(world.Sleep.SleepingAt == bed.Id, "the player should lie down before testing sleep healing");
        world.Tick(50, sleeping: true);
        Require(Math.Abs(world.Health - 22) < .01f, "sleeping with Protein recovery should heal at twice the sleep rate");
    }

    private static void FreshAndCannedStewFillTheSecondLapAndStartRecovery(GameData data) {
        foreach (string item in new[] { "meat_stew", "canned_meat_stew" }) {
            var world = EmptyWorld(data);
            SetNeeds(world, hunger: 12, health: 50);
            Give(world, item); world.Consume(item);
            Require(world.Hunger == 150 && world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) == 140,
                $"{item} should fill hunger straight to 150% and give the same recovery as steak");
            Require(world.Health == 50, $"{item} should start gradual Protein recovery without an immediate heal");
            Require(Math.Abs(world.FoodEffects.SecondsLeft(FoodEffect.WellFed) - 420) < .01f,
                "a full second lap should last half a day at normal pace");
            Give(world, "bread"); world.Consume("bread");
            Require(world.Hunger == 150, "eating ordinary food should never take the second lap away");
            world.Me.Sprinting = true;
            Require(world.FoodEffects.SecondsLeft(FoodEffect.WellFed) < 250, "the Well fed countdown should reflect sprinting");
            world.Me.Sprinting = false;
            world.Tick(419, sleeping: true);
            Require(world.Hunger > 100 && world.FoodEffects.SecondsLeft(FoodEffect.WellFed) > 0, "Well fed should last while the extra hunger lasts");
            world.Tick(2);
            Require(world.Hunger < 100 && world.FoodEffects.SecondsLeft(FoodEffect.WellFed) == 0, "Well fed should end when the second lap runs out");
        }
    }

    private static void JamHealsAndFillsTheRaisedLimit(GameData data) {
        foreach (string item in new[] { BerryJam.Bottle, "canned_jam" }) {
            var hurt = EmptyWorld(data);
            SetNeeds(hurt, health: 10);
            Give(hurt, item); hurt.Consume(item);
            Require(hurt.Health == 70, "jam at 10 health should heal 10 and give 50 bonus health");
            var healthy = EmptyWorld(data);
            Give(healthy, item); healthy.Consume(item);
            Require(healthy.Health == 150, "jam at 100 health should give 50 bonus health without healing above the normal limit");
        }
    }

    private static void VitaminsProtectThePlayerAndDoNotTakeAwayHealthBelowOneHundred(GameData data) {
        var world = EmptyWorld(data);
        SetNeeds(world, health: 10);
        Give(world, "canned_jam", 2); world.Consume("canned_jam");
        SetNeeds(world, thirst: 0);
        world.Tick(10, sleeping: true);
        Require(world.Health == 56 && world.BonusHealth == 36, "thirst damage should spend 14 bonus HP and leave the 20 normal HP intact");
        SetNeeds(world, thirst: 100);
        Give(world, "burnt_steak"); world.Consume("burnt_steak");
        world.Tick(50, sleeping: true);
        Require(world.Health == 62 && world.BonusHealth == 36, "healing should fill missing normal HP before it refills the bonus");
        world.Consume("canned_jam");
        Require(world.Health == 86 && world.BonusHealth == 50, "another serving should heal up to 10 normal HP and refill, not stack, the bonus");
        Require(world.FoodEffects.SecondsLeft(FoodEffect.Vitamins) == 1620, "another serving should add one day to the remaining time");
        RunUntil(world, world.Time + 1621);
        Require(Math.Abs(world.Health - 96.8f) < .01f && world.BonusHealth == 0, "expiry should keep all health below 100, including former bonus HP");
    }

    private static void HealingRefillsSpentBonusHealth(GameData data) {
        foreach (bool sleep in new[] { false, true }) {
            var world = EmptyWorld(data);
            Give(world, "canned_jam"); world.Consume("canned_jam");
            SetNeeds(world, thirst: 0);
            world.Tick(10, sleeping: true);
            SetNeeds(world, thirst: 100);
            if (sleep) {
                var bed = world.Add(ThingKind.StrawBed, world.Player + Vector2.Right);
                Require(world.Sleep.Preview(bed).BonusHealth == 50, "the sleep forecast should show the bonus refilling by dawn");
                world.Sleep.Start(bed);
                Require(world.Sleep.SleepingAt == bed.Id, "the player should lie down before testing sleep healing");
            } else {
                Give(world, "burnt_steak"); world.Consume("burnt_steak");
            }
            world.Tick(50, sleeping: true);
            Require(world.Health == 142 && world.BonusHealth == 42, "sleep and Protein recovery should both refill the bonus below its cap");
            world.Tick(80, sleeping: true);
            Require(world.Health == 150 && world.BonusHealth == 50, "healing must not go above 150 HP");
        }
    }

    private static void VitaminsExpireAfterOneDay(GameData data) {
        var world = EmptyWorld(data);
        Give(world, BerryJam.Bottle); world.Consume(BerryJam.Bottle);
        RunUntil(world, world.Time + 839);
        Require(world.Health == 150 && world.FoodEffects.SecondsLeft(FoodEffect.Vitamins) == 1, "the extra limit should last one in-game day");
        RunUntil(world, world.Time + 2);
        Require(world.Health == 100 && world.BonusHealth == 0 && world.FoodEffects.SecondsLeft(FoodEffect.Vitamins) == 0,
            "expiration should trim health above 100");
    }

    private static void LongEffectsKeepCountingDownInSmallSteps(GameData data) {
        var world = EmptyWorld(data);
        for (int serving = 0; serving < 625; serving++) {
            Give(world, "canned_jam"); world.Consume("canned_jam");
        }
        double before = world.FoodEffects.SecondsLeft(FoodEffect.Vitamins);
        for (int frame = 0; frame < 60; frame++) world.Tick(1f / 60, sleeping: true);
        double elapsed = before - world.FoodEffects.SecondsLeft(FoodEffect.Vitamins);
        Require(Math.Abs(elapsed - 1) < .01, $"a long duration should count down one second in 60 steps, not {elapsed}");
    }

    private static void SavingAndLoadingKeepsTheEffectsAndTheirTimeLeft(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "canned_meat_stew"); world.Consume("canned_meat_stew");
        Give(world, "canned_jam", 2); world.Consume("canned_jam"); world.Consume("canned_jam");
        world.Tick(35);
        string save = world.Serialize();
        world.Tick(140);
        world.Deserialize(save);
        Require(world.Health == 150 && world.BonusHealth == 50 && world.Hunger > 145, "loading should restore the bonus health and both needs above the normal limit");
        Require(world.FoodEffects.SecondsLeft(FoodEffect.CarbLoad) == 245
            && world.FoodEffects.SecondsLeft(FoodEffect.ProteinRecovery) == 105
            && world.FoodEffects.SecondsLeft(FoodEffect.Vitamins) == 1645, "loading should restore the extended countdowns at the point of saving");
    }

    public static void ActiveFoodEffectsAllocateNothing(GameData data) {
        var world = EmptyWorld(data);
        Give(world, "meat_stew"); world.Consume("meat_stew");
        Give(world, "canned_jam"); world.Consume("canned_jam");
        AllocationChecks.Check("tick: active food effects", () => world.Tick(1 / 60f));
    }
}
