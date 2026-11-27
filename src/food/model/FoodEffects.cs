using System;

namespace ChopChopMeow;

/// <summary>Effects a cooked food can give the player who eats it.</summary>
public enum FoodEffect { CarbLoad, ProteinRecovery, WellFed, Vitamins }

/// <summary>Effects are shared across dishes and their canned versions. Only the player who eats gets them.</summary>
public sealed class FoodEffects(World world) {
    public const float NormalLimit = 100, OverloadLimit = 150;
    private const float BonusHealthLimit = OverloadLimit - NormalLimit;
    private const float CarbLoadHours = 4, SprintBoost = .25f, ProteinRecoveryHours = 4, VitaminsHours = 24, VitaminsHealth = 10;
    public static ReadOnlySpan<FoodEffect> All => [FoodEffect.CarbLoad, FoodEffect.ProteinRecovery, FoodEffect.WellFed, FoodEffect.Vitamins];

    public static (string Name, string Description) Describe(FoodEffect effect) => effect switch {
        FoodEffect.CarbLoad => ("Carb load", $"Sprint {SprintBoost * 100:0}% faster for {CarbLoadHours:0} hours."),
        FoodEffect.ProteinRecovery => ("Protein recovery", $"Regain health without sleeping for {ProteinRecoveryHours:0} hours."),
        FoodEffect.WellFed => ("Well fed", $"Fill hunger to {OverloadLimit:0}%. Lasts until hunger falls back to {NormalLimit:0}%."),
        FoodEffect.Vitamins => ("Vitamins", $"Heal up to {VitaminsHealth:0} HP and fill {BonusHealthLimit:0} bonus HP ({OverloadLimit:0}% limit) for one day."),
        _ => throw new ArgumentOutOfRangeException(nameof(effect))
    };

    /// <summary>Seconds left at the current pace, or zero if the acting player has no such effect.</summary>
    public float SecondsLeft(FoodEffect effect) {
        var p = world.S.Actor;
        if (world.Dead) return 0;
        return effect switch {
            FoodEffect.CarbLoad => p.CarbLoadLeft,
            FoodEffect.ProteinRecovery => p.ProteinRecoveryLeft,
            FoodEffect.WellFed => Math.Max(0, p.Hunger - NormalLimit) / (world.Data.HungerPerSecond * world.NeedsDrain),
            FoodEffect.Vitamins => p.VitaminsLeft,
            _ => throw new ArgumentOutOfRangeException(nameof(effect))
        };
    }

    internal float HealthLimit => HealthLimitAt(world.S.Actor, 0);
    private static float HealthLimitAt(PlayerState p, float seconds) => p.VitaminsLeft > seconds ? OverloadLimit : NormalLimit;

    internal float SprintMultiplier => world.S.Actor.CarbLoadLeft > 0 ? 1 + SprintBoost : 1;

    internal void Eat(ItemDef food) {
        var p = world.S.Actor;
        // Other food still stops at a full bar, but does not remove hunger already past it.
        p.Hunger = Math.Max(p.Hunger, Math.Min(NormalLimit, p.Hunger + food.Hunger));
        foreach (var effect in food.FoodEffects) {
            switch (effect) {
                case FoodEffect.CarbLoad: p.CarbLoadLeft += CarbLoadHours * world.Data.HourSeconds; break;
                case FoodEffect.ProteinRecovery: p.ProteinRecoveryLeft += ProteinRecoveryHours * world.Data.HourSeconds; break;
                case FoodEffect.WellFed: p.Hunger = OverloadLimit; break;
                case FoodEffect.Vitamins:
                    p.VitaminsLeft += VitaminsHours * world.Data.HourSeconds;
                    p.Health = Math.Min(NormalLimit, p.Health - p.BonusHealth + VitaminsHealth) + BonusHealthLimit;
                    p.BonusHealth = BonusHealthLimit;
                    break;
            }
        }
    }

    /// <summary>Health and its bonus after healing for part of the interval, under the limit in force at its end.</summary>
    internal static (float Health, float Bonus) HealthAfter(PlayerState p, float seconds, float sleepHealingSeconds) {
        float recovery = Math.Min(seconds, p.ProteinRecoveryLeft);
        // Either sleep or Protein recovery can heal at any given moment, but they do not heal twice.
        float health = Math.Min(HealthLimitAt(p, seconds), p.Health + Math.Max(sleepHealingSeconds, recovery) * SleepCycle.HealPerSecond);
        if (p.VitaminsLeft <= seconds) return (health, 0);
        // Healing fills normal health first. Any health above its limit is part of the bonus.
        return (health, Math.Max(p.BonusHealth, health - NormalLimit));
    }

    internal void Tick(float dt, float sleepHealingSeconds) {
        var p = world.S.Actor;
        (p.Health, p.BonusHealth) = HealthAfter(p, dt, sleepHealingSeconds);
        p.CarbLoadLeft = Math.Max(0, p.CarbLoadLeft - dt);
        p.ProteinRecoveryLeft = Math.Max(0, p.ProteinRecoveryLeft - dt);
        p.VitaminsLeft = Math.Max(0, p.VitaminsLeft - dt);
    }

    internal void Clear() {
        var p = world.S.Actor;
        p.CarbLoadLeft = p.ProteinRecoveryLeft = p.VitaminsLeft = 0;
        p.Health = Math.Min(p.Health, NormalLimit);
        p.BonusHealth = 0;
        p.Hunger = Math.Min(p.Hunger, NormalLimit);
    }

    private static bool ValidTime(float left) => float.IsFinite(left) && left >= 0;

    internal static bool Valid(PlayerState p) =>
        ValidTime(p.CarbLoadLeft) && ValidTime(p.ProteinRecoveryLeft) && ValidTime(p.VitaminsLeft)
        && p.BonusHealth is >= 0 and <= BonusHealthLimit && p.BonusHealth <= Math.Max(0, p.Health)
        && (p.VitaminsLeft > 0 || p.BonusHealth == 0) && p.Health <= NormalLimit + p.BonusHealth
        && p.Hunger is >= 0 and <= OverloadLimit;
}
