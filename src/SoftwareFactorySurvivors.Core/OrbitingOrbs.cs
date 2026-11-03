using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Orbiting orbs weapon: discrete orbs circling the ship at every level (1-20).
/// </summary>
public sealed class OrbitingOrbs
{
    public const int DefaultOrbCount = 3;
    public const float DefaultOrbitRadius = 80f;
    public const float DefaultOrbRadius = 11f;
    public const float DefaultRotationSpeedDeg = 90f; // degrees per second
    public const float DefaultDamage = 1f;
    public const float OrbHitCooldown = 0.5f; // seconds per enemy
    public const int MaxLevel = 20;

    // Upgrade options increments:
    public const int UpgradeOrbCountStep = 1;
    public const float UpgradeOrbitRadiusStep = 15f;
    public const float UpgradeOrbRadiusStep = 3f;
    public const float UpgradeRotationSpeedDegStep = 30f;
    public const float UpgradeDamageStep = 0.5f;

    // Seconds left before an enemy can be hit by an orb again.
    private readonly Dictionary<Enemy, float> _cooldowns = new();

    public int Level { get; private set; } = 1;
    public int OrbCount { get; private set; } = DefaultOrbCount;
    public float OrbitRadius { get; private set; } = DefaultOrbitRadius;
    public float OrbRadius { get; private set; } = DefaultOrbRadius;
    public float RotationSpeedDeg { get; private set; } = DefaultRotationSpeedDeg;
    public float Damage { get; private set; } = DefaultDamage;

    /// <summary>Current angle of the first orb in radians, measured clockwise (screen, y down) from the positive X axis.</summary>
    public float Angle { get; private set; }

    public OrbitingOrbs(int level = 1)
    {
        SetLevel(level);
    }

    /// <summary>Configures the weapon level (1-20); a level change forgets every running cooldown.</summary>
    public void SetLevel(int level)
    {
        if (level < 1 || level > MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level), $"Level must be 1 to {MaxLevel}.");

        Level = level;
        _cooldowns.Clear();
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

    /// <summary>Positions of the discrete orbs around shipPosition, evenly spaced: one per orb.</summary>
    public IReadOnlyList<Vector2> GetOrbPositions(Vector2 shipPosition)
    {
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
    /// most once per OrbHitCooldown.
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

    private static float NormalizeAngle(float angle)
    {
        var wrapped = angle % MathF.Tau;
        return wrapped < 0f ? wrapped + MathF.Tau : wrapped;
    }
}
