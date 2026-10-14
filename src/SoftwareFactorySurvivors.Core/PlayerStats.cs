namespace SoftwareFactorySurvivors.Core;

/// <summary>The ship's and its weapon's attributes for one game; upgrades raise them.</summary>
public sealed class PlayerStats
{
    /// <summary>Multiplier on laser width and length together (1 = 100%).</summary>
    public float ProjectileScale { get; set; } = 1f;

    /// <summary>Volleys fired per second.</summary>
    public float AttacksPerSecond { get; set; } = 1f;

    /// <summary>Lasers fired per volley, fanned out around the ship's heading.</summary>
    public int ProjectileCount { get; set; } = 1;

    /// <summary>Damage per laser hit.</summary>
    public float Damage { get; set; } = 1f;

    /// <summary>How many extra enemies each laser passes through before it's used up.</summary>
    public int Pierce { get; set; }

    /// <summary>Contact hits the ship can take; at the base of 1, any touch ends the game.</summary>
    public int MaxHitPoints { get; set; } = 1;

    public int HitPoints { get; set; } = 1;

    /// <summary>The weapon picked at level 3, which every later volley fires; chosen once per game.</summary>
    public WeaponPath WeaponPath { get; set; } = WeaponPath.None;
}

/// <summary>The weapon a game's volleys fire: the starting laser until a path is picked at level 3.</summary>
public enum WeaponPath
{
    None,
    Ballista,
    Bola,
}
