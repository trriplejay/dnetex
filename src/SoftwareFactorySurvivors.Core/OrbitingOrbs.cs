using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Orbiting orbs weapon: orbs (levels 1-4) or a solid ring (level 5) circling the ship.
/// </summary>
public sealed class OrbitingOrbs
{
    public const int DefaultOrbCount = 3;
    public const float DefaultOrbitRadius = 80f;
    public const float DefaultOrbRadius = 8f;
    public const float DefaultRotationSpeedDeg = 90f; // degrees per second
    public const float DefaultDamage = 1f;
    public const float OrbHitCooldown = 0.5f; // seconds per enemy
    public const float RingContactSeconds = 1.0f; // contact needed per ring hit
    public const int MaxLevel = 5;

    // Upgrade options increments:
    public const int UpgradeOrbCountStep = 1;
    public const float UpgradeOrbitRadiusStep = 15f;
    public const float UpgradeOrbRadiusStep = 3f;
    public const float UpgradeRotationSpeedDegStep = 30f;
    public const float UpgradeDamageStep = 0.5f;

    // Seconds left before an enemy can be hit by an orb again.
    private readonly Dictionary<Enemy, float> _cooldowns = new();

    // Seconds of unbroken ring contact not yet paid out as a hit.
    private readonly Dictionary<Enemy, float> _contact = new();

    public int Level { get; private set; } = 1;
    public int OrbCount { get; private set; } = DefaultOrbCount;
    public float OrbitRadius { get; private set; } = DefaultOrbitRadius;
    public float OrbRadius { get; private set; } = DefaultOrbRadius;
    public float RotationSpeedDeg { get; private set; } = DefaultRotationSpeedDeg;
    public float Damage { get; private set; } = DefaultDamage;

    /// <summary>Current angle of the first orb in radians, measured clockwise (screen, y down) from the positive X axis.</summary>
    public float Angle { get; private set; }

    /// <summary>True at the maximum level, where the orbs become a solid ring band.</summary>
    public bool IsRingMode => Level >= MaxLevel;

    /// <summary>Outer radius of the level 5 ring band.</summary>
    public float RingOuterRadius => OrbitRadius + OrbRadius;

    /// <summary>Inner radius of the level 5 ring band.</summary>
    public float RingInnerRadius => MathF.Max(0f, OrbitRadius - OrbRadius);

    public OrbitingOrbs(int level = 1)
    {
        SetLevel(level);
    }

    /// <summary>Configures the weapon level (1-5); a level change forgets every running cooldown and contact timer.</summary>
    public void SetLevel(int level)
    {
        if (level < 1 || level > MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level), $"Level must be 1 to {MaxLevel}.");

        Level = level;
        _cooldowns.Clear();
        _contact.Clear();
    }

    // The four upgrade options.
    public void AddOrb() => OrbCount += UpgradeOrbCountStep;

    public void AddSpeedAndOrbit()
    {
        RotationSpeedDeg += UpgradeRotationSpeedDegStep;
        OrbitRadius += UpgradeOrbitRadiusStep;
    }

    public void AddOrbRadius() => OrbRadius += UpgradeOrbRadiusStep;
    public void AddDamage() => Damage += UpgradeDamageStep;

    /// <summary>Positions of the discrete orbs around shipPosition, evenly spaced; empty in ring mode.</summary>
    public IReadOnlyList<Vector2> GetOrbPositions(Vector2 shipPosition)
    {
        if (IsRingMode)
            return [];

        var step = MathF.Tau / OrbCount;
        var positions = new Vector2[OrbCount];
        for (var i = 0; i < OrbCount; i++)
        {
            var orbAngle = Angle + i * step;
            positions[i] = shipPosition + new Vector2(MathF.Cos(orbAngle), MathF.Sin(orbAngle)) * OrbitRadius;
        }

        return positions;
    }

    /// <summary>
    /// Turns the orbs, then returns the (enemy, damage) hits dealt this frame. An orb hits an enemy it touches at
    /// most once per OrbHitCooldown; the ring hits an enemy once per RingContactSeconds of unbroken contact.
    /// </summary>
    public IReadOnlyList<(Enemy Enemy, float Damage)> Update(
        float elapsedSeconds,
        Vector2 shipPosition,
        IReadOnlyList<Enemy> enemies)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        // A positive angle turns (1, 0) toward (0, 1), which is clockwise with y pointing down.
        Angle = NormalizeAngle(Angle + RotationSpeedDeg * (MathF.PI / 180f) * elapsedSeconds);

        return IsRingMode
            ? UpdateRing(elapsedSeconds, shipPosition, enemies)
            : UpdateOrbs(elapsedSeconds, shipPosition, enemies);
    }

    private List<(Enemy, float)> UpdateOrbs(float elapsedSeconds, Vector2 shipPosition, IReadOnlyList<Enemy> enemies)
    {
        foreach (var enemy in _cooldowns.Keys.ToArray())
        {
            var remaining = _cooldowns[enemy] - elapsedSeconds;
            if (remaining <= 0f || enemy.IsDead)
                _cooldowns.Remove(enemy);
            else
                _cooldowns[enemy] = remaining;
        }

        var hits = new List<(Enemy, float)>();
        var orbPositions = GetOrbPositions(shipPosition);
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || _cooldowns.ContainsKey(enemy))
                continue;

            var strokes = enemy.GetStrokes();
            if (orbPositions.Any(orb => Collision.CircleTouches(orb, OrbRadius, strokes)))
            {
                hits.Add((enemy, Damage));
                _cooldowns[enemy] = OrbHitCooldown;
            }
        }

        return hits;
    }

    private List<(Enemy, float)> UpdateRing(float elapsedSeconds, Vector2 shipPosition, IReadOnlyList<Enemy> enemies)
    {
        var hits = new List<(Enemy, float)>();
        var stillTouching = new Dictionary<Enemy, float>();
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || !EnemyTouchesRing(shipPosition, enemy))
                continue;

            var contact = _contact.GetValueOrDefault(enemy) + elapsedSeconds;
            while (contact >= RingContactSeconds)
            {
                hits.Add((enemy, Damage));
                contact -= RingContactSeconds;
            }

            stillTouching[enemy] = contact;
        }

        // An enemy that left the band starts its next second from zero.
        _contact.Clear();
        foreach (var (enemy, contact) in stillTouching)
            _contact[enemy] = contact;

        return hits;
    }

    /// <summary>True when any part of the enemy's outline lies within the band [inner radius, outer radius].</summary>
    private bool EnemyTouchesRing(Vector2 shipPosition, Enemy enemy)
    {
        var innerRadius = RingInnerRadius;
        var outerRadius = RingOuterRadius;
        foreach (var stroke in enemy.GetStrokes())
        {
            for (var i = 0; i < stroke.Length - 1; i++)
            {
                // Distance to a segment varies continuously, so it covers every value from nearest to furthest point.
                var nearest = Collision.PointSegmentDistance(shipPosition, stroke[i], stroke[i + 1]);
                var furthest = MathF.Max(Vector2.Distance(shipPosition, stroke[i]), Vector2.Distance(shipPosition, stroke[i + 1]));
                if (nearest <= outerRadius && furthest >= innerRadius)
                    return true;
            }
        }

        return false;
    }

    private static float NormalizeAngle(float angle)
    {
        var wrapped = angle % MathF.Tau;
        return wrapped < 0f ? wrapped + MathF.Tau : wrapped;
    }
}
