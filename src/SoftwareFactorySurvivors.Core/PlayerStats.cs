namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// The ship's attributes for one game, the weapons it owns (up to MaxWeapons, each with its own stats) and the
/// enhancements it owns (up to MaxEnhancements).
/// </summary>
public sealed class PlayerStats
{
    public const int MaxWeapons = 3;
    public const int MaxEnhancements = 3;

    private readonly List<OwnedWeapon> _weapons = [];
    private readonly List<OwnedEnhancement> _enhancements = [];

    /// <summary>Contact hits the ship can take; at the base of 1, any touch ends the game.</summary>
    public int MaxHitPoints { get; set; } = 1;

    public int HitPoints { get; set; } = 1;

    /// <summary>Owned weapons in the order they were picked.</summary>
    public IReadOnlyList<OwnedWeapon> Weapons => _weapons;

    /// <summary>The owned weapon of this kind, or null when the ship doesn't have it.</summary>
    public OwnedWeapon? Find(WeaponKind kind) => _weapons.Find(weapon => weapon.Kind == kind);

    /// <summary>Adds a level-1 weapon of this kind.</summary>
    /// <exception cref="InvalidOperationException">The ship already owns this kind or has no free weapon slot.</exception>
    public OwnedWeapon AddWeapon(WeaponKind kind)
    {
        if (Find(kind) is not null)
            throw new InvalidOperationException($"The ship already owns {kind}.");
        if (_weapons.Count >= MaxWeapons)
            throw new InvalidOperationException($"The ship already owns {MaxWeapons} weapons.");

        var weapon = new OwnedWeapon(kind);
        _weapons.Add(weapon);
        return weapon;
    }

    /// <summary>Owned enhancements in the order they were picked.</summary>
    public IReadOnlyList<OwnedEnhancement> Enhancements => _enhancements;

    /// <summary>The owned enhancement with this definition, or null when the ship doesn't have it.</summary>
    public OwnedEnhancement? Find(Enhancement definition) => _enhancements.Find(owned => owned.Definition == definition);

    /// <summary>Adds a level-1 enhancement.</summary>
    /// <exception cref="InvalidOperationException">The ship already owns it or has no free enhancement slot.</exception>
    public OwnedEnhancement AddEnhancement(Enhancement definition)
    {
        if (Find(definition) is not null)
            throw new InvalidOperationException($"The ship already owns {definition.Name}.");
        if (_enhancements.Count >= MaxEnhancements)
            throw new InvalidOperationException($"The ship already owns {MaxEnhancements} enhancements.");

        var owned = new OwnedEnhancement(definition);
        _enhancements.Add(owned);
        return owned;
    }
}
