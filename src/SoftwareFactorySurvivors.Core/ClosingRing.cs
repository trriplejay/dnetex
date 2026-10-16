using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// A Claude-boss ability that periodically creates a closing ring hazard centred near the ship.
/// The ring gives at least 1.0s of warning (warm-up) before it becomes armed, contracts at a finite
/// speed, and never contracts below a minimum radius safely larger than the ship's diameter.
/// </summary>
public sealed class ClosingRing : IEnemyAbility, IHazardProducer
{
    private const int CircleSegments = 32;

    /// <summary>Minimum radius the ring can contract to (ship diameter ~48px, minimum radius 60px).</summary>
    public const float DefaultMinRadius = 60f;

    /// <summary>Initial radius when the ring hazard spawns.</summary>
    public const float DefaultInitialRadius = 300f;

    /// <summary>Speed in pixels per second at which the ring radius contracts.</summary>
    public const float DefaultContractionSpeed = 40f;

    /// <summary>Warm-up time before the ring becomes armed and can deal contact damage (clamped >= 1.0s).</summary>
    public const float DefaultWarmupSeconds = 1.2f;

    /// <summary>Total lifetime of the ring hazard in seconds.</summary>
    public const float DefaultLifetimeSeconds = 8.0f;

    /// <summary>Cooldown in seconds between successive ring hazard spawns.</summary>
    public const float DefaultCooldownSeconds = 10.0f;

    private readonly float _initialRadius;
    private readonly float _minRadius;
    private readonly float _contractionSpeed;
    private readonly float _warmupSeconds;
    private readonly float _lifetimeSeconds;
    private readonly float _cooldownSeconds;

    private float _timer;
    private readonly List<Hazard> _pendingHazards = [];

    public ClosingRing(
        float initialRadius = DefaultInitialRadius,
        float minRadius = DefaultMinRadius,
        float contractionSpeed = DefaultContractionSpeed,
        float warmupSeconds = DefaultWarmupSeconds,
        float lifetimeSeconds = DefaultLifetimeSeconds,
        float cooldownSeconds = DefaultCooldownSeconds)
    {
        _initialRadius = initialRadius;
        _minRadius = MathF.Max(DefaultMinRadius, minRadius);
        _contractionSpeed = contractionSpeed;
        _warmupSeconds = MathF.Max(1.0f, warmupSeconds);
        _lifetimeSeconds = lifetimeSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public float InitialRadius => _initialRadius;
    public float MinRadius => _minRadius;
    public float ContractionSpeed => _contractionSpeed;
    public float WarmupSeconds => _warmupSeconds;
    public float LifetimeSeconds => _lifetimeSeconds;
    public float CooldownSeconds => _cooldownSeconds;

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        _timer += elapsedSeconds;
        if (_timer >= _cooldownSeconds || _pendingHazards.Count == 0 && _timer == elapsedSeconds)
        {
            if (_timer >= _cooldownSeconds)
                _timer -= _cooldownSeconds;

            var hazard = SpawnRingHazard(playerPosition);
            _pendingHazards.Add(hazard);
        }

        return [];
    }

    public IReadOnlyList<Hazard> TakeHazards()
    {
        if (_pendingHazards.Count == 0)
            return [];

        var hazards = _pendingHazards.ToArray();
        _pendingHazards.Clear();
        return hazards;
    }

    /// <summary>
    /// Creates a closing ring Hazard centred at the specified position.
    /// The ring is represented as a circular polyline (thin band) that contracts each frame down to minRadius.
    /// </summary>
    public static Hazard CreateRingHazard(
        Vector2 center,
        float initialRadius = DefaultInitialRadius,
        float minRadius = DefaultMinRadius,
        float contractionSpeed = DefaultContractionSpeed,
        float warmupSeconds = DefaultWarmupSeconds,
        float lifetimeSeconds = DefaultLifetimeSeconds)
    {
        var currentRadius = initialRadius;
        var clampedMin = MathF.Max(DefaultMinRadius, minRadius);
        var clampedWarmup = MathF.Max(1.0f, warmupSeconds);

        var ringPoly = BuildCirclePolyline(center, currentRadius);
        var shape = new ProjectileShape([], [ringPoly], []);

        return new Hazard(
            HazardKind.Ring,
            shape,
            lifetimeSeconds,
            armSeconds: clampedWarmup,
            onUpdate: (hazard, dt) =>
            {
                if (currentRadius > clampedMin)
                {
                    currentRadius = MathF.Max(clampedMin, currentRadius - contractionSpeed * dt);
                    UpdateCirclePolyline(center, currentRadius, ringPoly);
                    hazard.Shape.InvalidateBounds();
                }
            })
        {
            ExpireOnContact = false,
        };
    }

    private Hazard SpawnRingHazard(Vector2 center) =>
        CreateRingHazard(center, _initialRadius, _minRadius, _contractionSpeed, _warmupSeconds, _lifetimeSeconds);

    private static Vector2[] BuildCirclePolyline(Vector2 center, float radius)
    {
        var points = new Vector2[CircleSegments + 1];
        UpdateCirclePolyline(center, radius, points);
        return points;
    }

    private static void UpdateCirclePolyline(Vector2 center, float radius, Vector2[] destination)
    {
        for (var i = 0; i < CircleSegments; i++)
        {
            var angle = MathF.Tau * i / CircleSegments;
            destination[i] = new Vector2(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));
        }
        destination[CircleSegments] = destination[0];
    }
}
