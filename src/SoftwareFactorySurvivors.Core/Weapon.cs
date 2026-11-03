using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>The weapons a ship can own; each one is owned at most once.</summary>
public enum WeaponKind
{
    Laser,
    Lightning,
    Orbs,
    RicochetDisc,
    ProximityMines,
}

/// <summary>
/// One weapon the ship owns: its level, its own stats and its own firing timer. Changing one weapon's stats or
/// level never touches another weapon's.
/// </summary>
public sealed class OwnedWeapon
{
    public const int MaxLevel = 20;

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
            // Discs thrown per attack; mines dropped per drop.
            WeaponKind.RicochetDisc => (2f, 0.5f, 1),
            WeaponKind.ProximityMines => (3f, 0.5f, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        if (kind == WeaponKind.Orbs)
            Orbs = new OrbitingOrbs();
        if (kind == WeaponKind.RicochetDisc)
            (SizeScale, Range, Speed) = (1f, DiscRange, DiscSpeed);
        if (kind == WeaponKind.ProximityMines)
            BlastRadius = MinesBlastRadius;
    }

    /// <summary>How far a Ricochet Disc flies out before it turns back, in pixels.</summary>
    public const float DiscRange = 250f;

    /// <summary>A Ricochet Disc's flight speed, out and back, in pixels per second.</summary>
    public const float DiscSpeed = 400f;

    /// <summary>A level-1 Proximity Mine's blast radius, in pixels.</summary>
    public const float MinesBlastRadius = 60f;

    /// <summary>Every mine after the first in one drop lands at most this far from the ship, in pixels.</summary>
    public const float MineScatterRadius = 40f;

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

    /// <summary>Disc size relative to level 1 (1 = base size); 0 for any weapon but the Ricochet Disc.</summary>
    public float SizeScale { get; set; }

    /// <summary>How far a disc flies out before turning back, in pixels; 0 for any weapon but the Ricochet Disc.</summary>
    public float Range { get; set; }

    /// <summary>Disc flight speed in pixels per second; 0 for any weapon but the Ricochet Disc.</summary>
    public float Speed { get; set; }

    /// <summary>Mine blast radius in pixels; 0 for any weapon but Proximity Mines.</summary>
    public float BlastRadius { get; set; }

    /// <summary>Fraction of the way to this weapon's next shot.</summary>
    public double TimerProgress => _timer.Progress;

    /// <summary>Raises the level by one; does nothing once the weapon is at MaxLevel.</summary>
    public void RaiseLevel() => Level = Math.Min(MaxLevel, Level + 1);

    /// <summary>
    /// Where one drop of mines lands: Count positions, the first at the ship and each further one at a random spot
    /// within MineScatterRadius of it.
    /// </summary>
    public IReadOnlyList<Vector2> MineDropPositions(Vector2 shipPosition, Random random)
    {
        var positions = new Vector2[Math.Max(0, Count)];
        for (var i = 0; i < positions.Length; i++)
        {
            if (i == 0)
            {
                positions[i] = shipPosition;
                continue;
            }
            // The square root spreads the spots evenly over the disc instead of bunching them at the centre.
            var angle = random.NextSingle() * MathF.Tau;
            var distance = MineScatterRadius * MathF.Sqrt(random.NextSingle());
            positions[i] = shipPosition + distance * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        return positions;
    }

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
