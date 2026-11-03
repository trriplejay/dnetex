using static System.FormattableString;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// The single owner of the ship's five enhancements and every number they use: per-level values for levels 1-20
/// and the card text per level.
/// Other code reads these members and keeps no copies of the numbers.
/// </summary>
public static class ShipEnhancements
{
    /// <summary>Faster movement: Ship.Speed × SpeedMultiplier(level).</summary>
    public static readonly Enhancement MoveSpeed = new("MOVE SPEED", "MOVE FASTER");

    /// <summary>More maximum hit points: +HealthPerLevel per level.</summary>
    public static readonly Enhancement Health = new("HEALTH", "MORE MAX HP");

    /// <summary>Absorbs damage before hit points; recharges after a delay.</summary>
    public static readonly Enhancement Shield = new("SHIELD", "ABSORBS DAMAGE");

    /// <summary>Takes ArmorPerLevel off every hit that reaches hit points, never below 1.</summary>
    public static readonly Enhancement Armor = new("ARMOR", "BLOCKS DAMAGE");

    /// <summary>Heals over time.</summary>
    public static readonly Enhancement Regeneration = new("REGENERATION", "HEALS OVER TIME");

    /// <summary>All five, in a fixed order.</summary>
    public static IReadOnlyList<Enhancement> All { get; } = [MoveSpeed, Health, Shield, Armor, Regeneration];

    // ---- Move Speed ----

    /// <summary>Each Move Speed level adds this fraction of Ship.Speed.</summary>
    public const float MoveSpeedStep = 0.1f;

    // ---- Health ----

    /// <summary>Maximum hit points each Health level adds.</summary>
    public const float HealthPerLevel = 20f;

    // ---- Shield ----

    /// <summary>Shield points each Shield level holds.</summary>
    public const float ShieldCapacityPerLevel = 5f;

    // Seconds a broken shield waits before refilling, for levels 1-20: 10 - 0.5*(level-1).
    private static readonly float[] ShieldRechargeDelays =
    [
        10.0f, 9.5f, 9.0f, 8.5f, 8.0f, 7.5f, 7.0f, 6.5f, 6.0f, 5.5f,
        5.0f, 4.5f, 4.0f, 3.5f, 3.0f, 2.5f, 2.0f, 1.5f, 1.0f, 0.5f,
    ];

    // ---- Armor ----

    /// <summary>Damage each Armor level takes off a hit that reaches hit points.</summary>
    public const float ArmorPerLevel = 1f;

    /// <summary>Armor never brings a hit below this much damage.</summary>
    public const float ArmorMinimumDamage = 1f;

    // ---- Regeneration ----

    /// <summary>Seconds between regeneration heals.</summary>
    public const float RegenTickSeconds = 1f;

    /// <summary>Hit points each Regeneration level heals per tick (n/5 HP every second, so n HP per 5 seconds).</summary>
    public const float RegenPerTickPerLevel = 0.2f;

    /// <summary>The period cards and the HUD state the regeneration rate over: "n HP per 5 seconds".</summary>
    public const int RegenReadoutSeconds = 5;

    // ---- Per-level values ----

    /// <summary>The level of this enhancement the ship owns, or 0 when it doesn't own it.</summary>
    public static int LevelOf(PlayerStats stats, Enhancement enhancement) => stats.Find(enhancement)?.Level ?? 0;

    /// <summary>Speed multiple at a Move Speed level: 1 + 0.1 × level (1 when not owned).</summary>
    public static float SpeedMultiplier(int level) => 1f + MoveSpeedStep * CheckLevel(level);

    /// <summary>Maximum hit points added at a Health level.</summary>
    public static float HealthBonus(int level) => HealthPerLevel * CheckLevel(level);

    /// <summary>Shield points held at a Shield level: 5 × level.</summary>
    public static float ShieldCapacity(int level) => ShieldCapacityPerLevel * CheckLevel(level);

    /// <summary>Seconds a broken shield waits before refilling at a Shield level 1-20: 10 - 0.5*(level-1).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The level is not 1-20.</exception>
    public static float ShieldRechargeSeconds(int level) =>
        level is >= 1 and <= OwnedEnhancement.MaxLevel
            ? ShieldRechargeDelays[level - 1]
            : throw new ArgumentOutOfRangeException(nameof(level));

    /// <summary>Damage taken off each hit at an Armor level.</summary>
    public static float ArmorReduction(int level) => ArmorPerLevel * CheckLevel(level);

    /// <summary>
    /// What armor leaves of a hit that reaches hit points: the damage less ArmorReduction(level), never below
    /// ArmorMinimumDamage (and never more than the hit itself).
    /// </summary>
    public static float ArmoredDamage(float damage, int level) =>
        MathF.Min(damage, MathF.Max(ArmorMinimumDamage, damage - ArmorReduction(level)));

    /// <summary>Hit points healed per RegenTickSeconds at a Regeneration level.</summary>
    public static float RegenPerTick(int level) => RegenPerTickPerLevel * CheckLevel(level);

    /// <summary>Hit points healed per RegenReadoutSeconds at a Regeneration level: the "n" of "n HP per 5 seconds".</summary>
    public static float RegenPerReadout(int level) => RegenPerTick(level) * (RegenReadoutSeconds / RegenTickSeconds);

    // ---- Card text ----

    /// <summary>
    /// The card's second line for an enhancement reaching this level: what it gives at that level. Regeneration
    /// states its rate as "n HP PER 5 SECONDS".
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The level is not 1-20.</exception>
    /// <exception cref="ArgumentException">The enhancement is not one of All.</exception>
    public static string CardDescription(Enhancement enhancement, int level)
    {
        if (level is < 1 or > OwnedEnhancement.MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level));

        if (enhancement == MoveSpeed)
            return Invariant($"+{(SpeedMultiplier(level) - 1f) * 100f:0}% MOVE SPEED");
        if (enhancement == Health)
            return Invariant($"+{HealthBonus(level):0} MAX HP");
        if (enhancement == Shield)
            return Invariant($"{ShieldCapacity(level):0} SHIELD, REFILLS {ShieldRechargeSeconds(level):0.#} S AFTER BREAKING");
        if (enhancement == Armor)
            return Invariant($"-{ArmorReduction(level):0} DAMAGE PER HIT, NEVER BELOW {ArmorMinimumDamage:0}");
        if (enhancement == Regeneration)
            return Invariant($"+{RegenPerReadout(level):0.##} HP PER {RegenReadoutSeconds} SECONDS");
        throw new ArgumentException($"{enhancement.Name} is not a ship enhancement.", nameof(enhancement));
    }

    private static int CheckLevel(int level) =>
        level is >= 0 and <= OwnedEnhancement.MaxLevel ? level : throw new ArgumentOutOfRangeException(nameof(level));
}
