using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>The weapons a ship can own; each one is owned at most once.</summary>
public enum WeaponKind
{
    Laser,
    Lightning,
    Orbs,
}

/// <summary>
/// One weapon the ship owns: its level, its own stats and its own firing timer. Changing one weapon's stats or
/// level never touches another weapon's.
/// </summary>
public sealed class OwnedWeapon
{
    public const int MaxLevel = 5;

    private readonly FiringTimer _timer = new();

    /// <summary>A weapon at level 1 with its kind's base stats.</summary>
    public OwnedWeapon(WeaponKind kind)
    {
        Kind = kind;
        (Damage, AttacksPerSecond, Count) = kind switch
        {
            WeaponKind.Laser => (1f, 1f, 1),
            WeaponKind.Lightning => (0.8f, 1.2f, 1),
            // Orbs circle the ship all the time instead of firing, so they have no attack rate.
            WeaponKind.Orbs => (1f, 0f, 3),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        if (kind == WeaponKind.Orbs)
            Orbs = new OrbitingOrbs();
    }

    public WeaponKind Kind { get; }

    /// <summary>The orbs' own state (count, radii, speed, damage, level) for an Orbs weapon; null for any other kind.</summary>
    public OrbitingOrbs? Orbs { get; }

    /// <summary>1 when picked; each upgrade card raises it by one, up to MaxLevel.</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Damage per hit.</summary>
    public float Damage { get; set; }

    /// <summary>Shots per second (laser volleys, lightning strikes); 0 for a weapon that never fires.</summary>
    public float AttacksPerSecond { get; set; }

    /// <summary>How many per shot: lasers per volley, lightning chains, or orbs circling the ship.</summary>
    public int Count { get; set; }

    /// <summary>How many extra enemies each projectile passes through before it's used up.</summary>
    public int Pierce { get; set; }

    /// <summary>Fraction of the way to this weapon's next shot.</summary>
    public double TimerProgress => _timer.Progress;

    /// <summary>Raises the level by one; does nothing once the weapon is at MaxLevel.</summary>
    public void RaiseLevel() => Level = Math.Min(MaxLevel, Level + 1);

    /// <summary>Advances this weapon's own timer and returns the pose of each shot it fires this frame.</summary>
    public IReadOnlyList<ShotPose> Fire(float elapsedSeconds, Vector2 nose, float heading) =>
        AttacksPerSecond > 0f ? _timer.Update(elapsedSeconds, nose, heading, AttacksPerSecond) : [];

    /// <summary>
    /// Advances this weapon's timer without firing, for a frame with nothing to shoot at: a shot that comes due waits,
    /// so the weapon fires on the first frame it next can.
    /// </summary>
    public void Charge(float elapsedSeconds)
    {
        if (AttacksPerSecond > 0f)
            _timer.Charge(elapsedSeconds, AttacksPerSecond);
    }
}
