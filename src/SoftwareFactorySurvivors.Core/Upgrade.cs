namespace SoftwareFactorySurvivors.Core;

public enum Upgrade
{
    AttackSpeed,
    ProjectileSize,
    ProjectileCount,
    Damage,
    Piercing,
    MaxHitPoints,
}

/// <summary>What each upgrade does, how it's described on its card, and which ones can still be offered.</summary>
public static class Upgrades
{
    public const float AttackSpeedStep = 0.25f;
    public const float ProjectileScaleStep = 0.5f;
    public const int MaxProjectileCount = 9;
    public const float DamageStep = 1f;

    public static IReadOnlyList<Upgrade> All { get; } = Enum.GetValues<Upgrade>();

    public static string Title(Upgrade upgrade) => upgrade switch
    {
        Upgrade.AttackSpeed => "ATTACK SPEED",
        Upgrade.ProjectileSize => "PROJECTILE SIZE",
        Upgrade.ProjectileCount => "PROJECTILES",
        Upgrade.Damage => "DAMAGE",
        Upgrade.Piercing => "PIERCING",
        Upgrade.MaxHitPoints => "MAX HP",
        _ => throw new ArgumentOutOfRangeException(nameof(upgrade)),
    };

    /// <summary>The change this upgrade would make, from current to new value, e.g. "100% > 150%".</summary>
    public static string Describe(Upgrade upgrade, PlayerStats stats) => upgrade switch
    {
        Upgrade.AttackSpeed => $"{stats.AttacksPerSecond:0.00}/S > {stats.AttacksPerSecond + AttackSpeedStep:0.00}/S",
        Upgrade.ProjectileSize => $"{Percent(stats.ProjectileScale)} > {Percent(stats.ProjectileScale + ProjectileScaleStep)}",
        Upgrade.ProjectileCount => $"{stats.ProjectileCount} > {stats.ProjectileCount + 1}",
        Upgrade.Damage => $"{stats.Damage:0.#} > {stats.Damage + DamageStep:0.#}",
        Upgrade.Piercing => $"{stats.Pierce} > {stats.Pierce + 1} ENEMIES",
        Upgrade.MaxHitPoints => $"{stats.HitPoints}/{stats.MaxHitPoints} > {stats.HitPoints + 1}/{stats.MaxHitPoints + 1}",
        _ => throw new ArgumentOutOfRangeException(nameof(upgrade)),
    };

    /// <summary>False once an upgrade is maxed out, so it stops appearing on cards.</summary>
    public static bool CanOffer(Upgrade upgrade, PlayerStats stats) => upgrade switch
    {
        Upgrade.ProjectileCount => stats.ProjectileCount < MaxProjectileCount,
        _ => true,
    };

    public static void Apply(Upgrade upgrade, PlayerStats stats)
    {
        switch (upgrade)
        {
            case Upgrade.AttackSpeed: stats.AttacksPerSecond += AttackSpeedStep; break;
            case Upgrade.ProjectileSize: stats.ProjectileScale += ProjectileScaleStep; break;
            case Upgrade.ProjectileCount: stats.ProjectileCount = Math.Min(MaxProjectileCount, stats.ProjectileCount + 1); break;
            case Upgrade.Damage: stats.Damage += DamageStep; break;
            case Upgrade.Piercing: stats.Pierce += 1; break;
            case Upgrade.MaxHitPoints: stats.MaxHitPoints += 1; stats.HitPoints += 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(upgrade));
        }
    }

    /// <summary>Up to count different upgrades, chosen at random from those that can still be offered.</summary>
    public static IReadOnlyList<Upgrade> Offer(PlayerStats stats, Random random, int count = 3) =>
        All.Where(upgrade => CanOffer(upgrade, stats)).OrderBy(_ => random.Next()).Take(count).ToArray();

    private static string Percent(float fraction) => $"{MathF.Round(fraction * 100f):0}%";
}
