using static System.FormattableString;

namespace SoftwareFactorySurvivors.Core;

/// <summary>What a level-up card gives.</summary>
public enum UpgradeKind
{
    /// <summary>Adds a weapon the ship doesn't own yet, at level 1.</summary>
    NewWeapon,

    /// <summary>Applies one option to an owned weapon and raises it one level.</summary>
    WeaponUpgrade,

    /// <summary>Adds an enhancement the ship doesn't own yet, at level 1.</summary>
    NewEnhancement,

    /// <summary>Raises an owned enhancement one level.</summary>
    EnhancementUpgrade,
}

/// <summary>One improvement a weapon upgrade card can carry; each weapon has its own list (see Upgrades.OptionsFor).</summary>
public enum WeaponOption
{
    LaserProjectile,
    LaserDamage,
    LaserAttackSpeed,
    LightningDamage,
    LightningAttackSpeed,
    LightningChain,
    OrbsCount,
    OrbsSpeedAndOrbit,
    OrbsRadius,
    OrbsDamage,
}

/// <summary>
/// One level-up card. Cards are values: two cards are equal exactly when they would do the same thing, so an
/// offer of distinct cards never shows the same weapon option twice.
/// </summary>
public sealed record Upgrade(UpgradeKind Kind, WeaponKind? Weapon = null, WeaponOption? Option = null, Enhancement? Enhancement = null)
{
    public static Upgrade NewWeapon(WeaponKind weapon) => new(UpgradeKind.NewWeapon, Weapon: weapon);

    public static Upgrade ForWeapon(WeaponKind weapon, WeaponOption option)
    {
        if (!Upgrades.OptionsFor(weapon).Contains(option))
            throw new ArgumentException($"{option} is not an option of {weapon}.", nameof(option));
        return new(UpgradeKind.WeaponUpgrade, Weapon: weapon, Option: option);
    }

    public static Upgrade NewEnhancement(Enhancement enhancement) => new(UpgradeKind.NewEnhancement, Enhancement: enhancement);

    public static Upgrade ForEnhancement(Enhancement enhancement) => new(UpgradeKind.EnhancementUpgrade, Enhancement: enhancement);
}

/// <summary>
/// Which cards a level-up can offer, how each is described, and what it does. The ship has PlayerStats.MaxWeapons
/// weapon slots and PlayerStats.MaxEnhancements enhancement slots: new-weapon and new-enhancement cards appear only
/// while a slot of their kind is free, and upgrade cards only for owned weapons and enhancements below level 5.
/// </summary>
public static class Upgrades
{
    public const float LaserDamageStep = 1f;
    public const float LaserAttackSpeedStep = 0.25f;
    public const float LightningDamageStep = 0.4f;
    public const float LightningAttackSpeedStep = 0.2f;

    /// <summary>Every implemented weapon, in the order the starting pick shows them.</summary>
    public static IReadOnlyList<WeaponKind> AllWeapons { get; } = Enum.GetValues<WeaponKind>();

    /// <summary>The starting pick: one new-weapon card per implemented weapon, in fixed order, whatever the seed.</summary>
    public static IReadOnlyList<Upgrade> StartingWeapons { get; } = AllWeapons.Select(Upgrade.NewWeapon).ToArray();

    /// <summary>
    /// The enhancements cards can offer in production. Empty until the enhancements are defined; Offer takes
    /// another list for tests.
    /// </summary>
    public static IReadOnlyList<Enhancement> Enhancements { get; } = [];

    public static IReadOnlyList<WeaponOption> OptionsFor(WeaponKind weapon) => weapon switch
    {
        WeaponKind.Laser => [WeaponOption.LaserProjectile, WeaponOption.LaserDamage, WeaponOption.LaserAttackSpeed],
        WeaponKind.Lightning => [WeaponOption.LightningDamage, WeaponOption.LightningAttackSpeed, WeaponOption.LightningChain],
        WeaponKind.Orbs => [WeaponOption.OrbsCount, WeaponOption.OrbsSpeedAndOrbit, WeaponOption.OrbsRadius, WeaponOption.OrbsDamage],
        _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
    };

    public static string WeaponName(WeaponKind weapon) => weapon switch
    {
        WeaponKind.Laser => "LASER",
        WeaponKind.Lightning => "LIGHTNING",
        WeaponKind.Orbs => "ORBS",
        _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
    };

    /// <summary>The option's effect as shown on its card, e.g. "+1 DAMAGE"; always with a decimal point the vector font can draw.</summary>
    public static string Effect(WeaponOption option) => option switch
    {
        WeaponOption.LaserProjectile => "+1 PROJECTILE",
        WeaponOption.LaserDamage => Invariant($"+{LaserDamageStep:0.##} DAMAGE"),
        WeaponOption.LaserAttackSpeed => Invariant($"+{LaserAttackSpeedStep:0.##} ATTACKS/S"),
        WeaponOption.LightningDamage => Invariant($"+{LightningDamageStep:0.##} DAMAGE"),
        WeaponOption.LightningAttackSpeed => Invariant($"+{LightningAttackSpeedStep:0.##} ATTACKS/S"),
        WeaponOption.LightningChain => "+1 CHAIN",
        WeaponOption.OrbsCount => Invariant($"+{OrbitingOrbs.UpgradeOrbCountStep} ORB"),
        WeaponOption.OrbsSpeedAndOrbit => Invariant($"+{OrbitingOrbs.UpgradeRotationSpeedDegStep:0} DEG/S +{OrbitingOrbs.UpgradeOrbitRadiusStep:0} PX"),
        WeaponOption.OrbsRadius => Invariant($"+{OrbitingOrbs.UpgradeOrbRadiusStep:0} PX ORB SIZE"),
        WeaponOption.OrbsDamage => Invariant($"+{OrbitingOrbs.UpgradeDamageStep:0.##} DAMAGE"),
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };

    /// <summary>The card's title: what it improves, and for an upgrade the level change, e.g. "LASER LV 2 > 3".</summary>
    public static string Title(Upgrade upgrade, PlayerStats stats) => upgrade.Kind switch
    {
        UpgradeKind.NewWeapon => WeaponName(WeaponOf(upgrade)),
        UpgradeKind.WeaponUpgrade => $"{WeaponName(WeaponOf(upgrade))} {LevelChange(OwnedWeaponOf(upgrade, stats).Level)}",
        UpgradeKind.NewEnhancement => EnhancementOf(upgrade).Name,
        UpgradeKind.EnhancementUpgrade => $"{EnhancementOf(upgrade).Name} {LevelChange(OwnedEnhancementOf(upgrade, stats).Level)}",
        _ => throw new ArgumentOutOfRangeException(nameof(upgrade)),
    };

    /// <summary>The card's second line: NEW for a new weapon or enhancement, otherwise the effect.</summary>
    public static string Describe(Upgrade upgrade, PlayerStats stats) => upgrade.Kind switch
    {
        UpgradeKind.NewWeapon => "NEW WEAPON",
        UpgradeKind.WeaponUpgrade => Effect(OptionOf(upgrade)),
        UpgradeKind.NewEnhancement => "NEW ENHANCEMENT",
        UpgradeKind.EnhancementUpgrade => EnhancementOf(upgrade).Effect,
        _ => throw new ArgumentOutOfRangeException(nameof(upgrade)),
    };

    /// <summary>
    /// Every card the ship could take now: a new-weapon card per unowned weapon while a weapon slot is free, one
    /// card per option of each owned weapon below level 5, and the same slot and level rules for enhancements.
    /// </summary>
    public static IReadOnlyList<Upgrade> Eligible(PlayerStats stats, IReadOnlyList<Enhancement>? enhancements = null)
    {
        enhancements ??= Enhancements;
        var cards = new List<Upgrade>();
        if (stats.Weapons.Count < PlayerStats.MaxWeapons)
            cards.AddRange(AllWeapons.Where(kind => stats.Find(kind) is null).Select(Upgrade.NewWeapon));
        foreach (var weapon in stats.Weapons.Where(weapon => weapon.Level < OwnedWeapon.MaxLevel))
            cards.AddRange(OptionsFor(weapon.Kind).Select(option => Upgrade.ForWeapon(weapon.Kind, option)));
        if (stats.Enhancements.Count < PlayerStats.MaxEnhancements)
            cards.AddRange(enhancements.Where(enhancement => stats.Find(enhancement) is null).Select(Upgrade.NewEnhancement));
        foreach (var owned in stats.Enhancements.Where(owned => owned.Level < OwnedEnhancement.MaxLevel))
            cards.Add(Upgrade.ForEnhancement(owned.Definition));
        return cards;
    }

    /// <summary>Up to count different cards, chosen at random from the eligible ones; empty when none is eligible.</summary>
    /// <param name="enhancements">The enhancements that can be offered; defaults to the production list.</param>
    public static IReadOnlyList<Upgrade> Offer(PlayerStats stats, Random random, int count = 3, IReadOnlyList<Enhancement>? enhancements = null) =>
        Eligible(stats, enhancements).OrderBy(_ => random.Next()).Take(count).ToArray();

    /// <summary>Gives what the card gives: a new weapon or enhancement at level 1, or one option and one level.</summary>
    /// <exception cref="InvalidOperationException">The card isn't eligible: its slot is full, it's already owned, or its target is missing or at level 5.</exception>
    public static void Apply(Upgrade upgrade, PlayerStats stats)
    {
        switch (upgrade.Kind)
        {
            case UpgradeKind.NewWeapon:
                stats.AddWeapon(WeaponOf(upgrade));
                break;
            case UpgradeKind.WeaponUpgrade:
                var weapon = OwnedWeaponOf(upgrade, stats);
                if (weapon.Level >= OwnedWeapon.MaxLevel)
                    throw new InvalidOperationException($"{weapon.Kind} is already at level {OwnedWeapon.MaxLevel}.");
                ApplyOption(OptionOf(upgrade), weapon);
                weapon.RaiseLevel();
                // Level 5 turns the orbs into their ring.
                weapon.Orbs?.SetLevel(weapon.Level);
                break;
            case UpgradeKind.NewEnhancement:
                stats.AddEnhancement(EnhancementOf(upgrade));
                break;
            case UpgradeKind.EnhancementUpgrade:
                var owned = OwnedEnhancementOf(upgrade, stats);
                if (owned.Level >= OwnedEnhancement.MaxLevel)
                    throw new InvalidOperationException($"{owned.Definition.Name} is already at level {OwnedEnhancement.MaxLevel}.");
                owned.RaiseLevel();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(upgrade));
        }
    }

    private static void ApplyOption(WeaponOption option, OwnedWeapon weapon)
    {
        switch (option)
        {
            case WeaponOption.LaserProjectile: weapon.Count += 1; break;
            case WeaponOption.LaserDamage: weapon.Damage += LaserDamageStep; break;
            case WeaponOption.LaserAttackSpeed: weapon.AttacksPerSecond += LaserAttackSpeedStep; break;
            case WeaponOption.LightningDamage: weapon.Damage += LightningDamageStep; break;
            case WeaponOption.LightningAttackSpeed: weapon.AttacksPerSecond += LightningAttackSpeedStep; break;
            case WeaponOption.LightningChain: weapon.Count += 1; break;
            case WeaponOption.OrbsCount: OrbsOf(weapon).AddOrb(); break;
            case WeaponOption.OrbsSpeedAndOrbit: OrbsOf(weapon).AddSpeedAndOrbit(); break;
            case WeaponOption.OrbsRadius: OrbsOf(weapon).AddOrbRadius(); break;
            case WeaponOption.OrbsDamage: OrbsOf(weapon).AddDamage(); break;
            default: throw new ArgumentOutOfRangeException(nameof(option));
        }

        // The orbs own their stats; the weapon's summary copies follow them.
        if (weapon.Orbs is { } orbs)
            (weapon.Damage, weapon.Count) = (orbs.Damage, orbs.OrbCount);
    }

    private static string LevelChange(int level) => $"LV {level} > {level + 1}";

    private static OrbitingOrbs OrbsOf(OwnedWeapon weapon) =>
        weapon.Orbs ?? throw new InvalidOperationException($"{weapon.Kind} has no orbs.");

    private static WeaponKind WeaponOf(Upgrade upgrade) =>
        upgrade.Weapon ?? throw new ArgumentException("The card names no weapon.", nameof(upgrade));

    private static WeaponOption OptionOf(Upgrade upgrade) =>
        upgrade.Option ?? throw new ArgumentException("The card carries no weapon option.", nameof(upgrade));

    private static Enhancement EnhancementOf(Upgrade upgrade) =>
        upgrade.Enhancement ?? throw new ArgumentException("The card names no enhancement.", nameof(upgrade));

    private static OwnedWeapon OwnedWeaponOf(Upgrade upgrade, PlayerStats stats) =>
        stats.Find(WeaponOf(upgrade)) ?? throw new InvalidOperationException($"The ship owns no {WeaponOf(upgrade)} to upgrade.");

    private static OwnedEnhancement OwnedEnhancementOf(Upgrade upgrade, PlayerStats stats) =>
        stats.Find(EnhancementOf(upgrade)) ?? throw new InvalidOperationException($"The ship owns no {EnhancementOf(upgrade).Name} to upgrade.");
}
