using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Closing-ring timing that applies from a boss phase on.</summary>
/// <param name="AtHealthFraction">The owner's Phases entry (by AtHealthFraction) this step is keyed to.</param>
/// <param name="CooldownSeconds">Seconds between ring spawns.</param>
/// <param name="ContractionSpeed">Pixels per second each ring spawned under this step contracts.</param>
/// <param name="LifetimeSeconds">Lifetime of each ring spawned under this step.</param>
public sealed record RingStep(float AtHealthFraction, float CooldownSeconds, float ContractionSpeed, float LifetimeSeconds);

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

    private readonly IReadOnlyList<RingStep> _phaseSchedule;

    // Cooldown clock: carries over phase changes and is never reset by one.
    private float _timer;
    // Seconds since the last ring spawned, accumulated exactly as that ring's own AgeSeconds is.
    private float _sinceLastSpawn;
    private float _lastLifetime;
    private bool _hasSpawned;
    private readonly List<Hazard> _pendingHazards = [];

    /// <param name="phaseSchedule">
    /// Optional per-phase overrides. Each step applies once the owner has entered the Phases entry whose
    /// AtHealthFraction matches the step's; the step of the lowest such fraction reached wins. Null or empty keeps
    /// the base cooldown, contraction speed and lifetime throughout.
    /// </param>
    public ClosingRing(
        float initialRadius = DefaultInitialRadius,
        float minRadius = DefaultMinRadius,
        float contractionSpeed = DefaultContractionSpeed,
        float warmupSeconds = DefaultWarmupSeconds,
        float lifetimeSeconds = DefaultLifetimeSeconds,
        float cooldownSeconds = DefaultCooldownSeconds,
        IReadOnlyList<RingStep>? phaseSchedule = null)
    {
        _initialRadius = initialRadius;
        _minRadius = MathF.Max(DefaultMinRadius, minRadius);
        _contractionSpeed = contractionSpeed;
        _warmupSeconds = MathF.Max(1.0f, warmupSeconds);
        _lifetimeSeconds = lifetimeSeconds;
        _cooldownSeconds = cooldownSeconds;
        _phaseSchedule = phaseSchedule ?? [];
    }

    /// <summary>Per-phase overrides of cooldown, contraction speed and lifetime.</summary>
    public IReadOnlyList<RingStep> PhaseSchedule => _phaseSchedule;

    public float InitialRadius => _initialRadius;
    public float MinRadius => _minRadius;
    public float ContractionSpeed => _contractionSpeed;
    public float WarmupSeconds => _warmupSeconds;
    public float LifetimeSeconds => _lifetimeSeconds;
    public float CooldownSeconds => _cooldownSeconds;

    /// <summary>
    /// The cooldown, contraction speed and lifetime in force for this owner: the schedule step keyed to the lowest
    /// Phases entry (by AtHealthFraction) the owner has entered, or the base values if none applies.
    /// </summary>
    public RingStep StepFor(Enemy owner)
    {
        var current = new RingStep(1f, _cooldownSeconds, _contractionSpeed, _lifetimeSeconds);
        var phases = owner.Type.Phases;
        if (phases is null || _phaseSchedule.Count == 0)
            return current;

        var currentFraction = float.PositiveInfinity;
        foreach (var step in _phaseSchedule)
        {
            for (var i = 0; i < phases.Count; i++)
            {
                if (MathF.Abs(phases[i].AtHealthFraction - step.AtHealthFraction) < 0.001f
                    && owner.PhasesReached > i
                    && step.AtHealthFraction < currentFraction)
                {
                    current = step;
                    currentFraction = step.AtHealthFraction;
                }
            }
        }

        return current;
    }

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        _timer += elapsedSeconds;
        _sinceLastSpawn += elapsedSeconds;
        var step = StepFor(owner);

        if (!_hasSpawned)
        {
            _hasSpawned = true;
            Spawn(playerPosition, step);
        }
        // A ring spawns once its cooldown has run AND the previous ring has expired, so at most one is ever out.
        else if (_timer >= step.CooldownSeconds && _sinceLastSpawn >= _lastLifetime)
        {
            var overdue = _timer - step.CooldownSeconds;
            // Keep a sub-frame remainder so the rhythm doesn't drift; a spawn held back longer counts from now.
            _timer = overdue < elapsedSeconds ? overdue : 0f;
            Spawn(playerPosition, step);
        }

        return [];
    }

    private void Spawn(Vector2 playerPosition, RingStep step)
    {
        _sinceLastSpawn = 0f;
        _lastLifetime = step.LifetimeSeconds;
        _pendingHazards.Add(CreateRingHazard(
            playerPosition, _initialRadius, _minRadius, step.ContractionSpeed, _warmupSeconds, step.LifetimeSeconds));
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
    /// The ring is represented as an outline of thick line segments (thin band) that contracts each frame down to minRadius.
    /// It is built as line segments rather than a filled polygon so that only the band hurts, not the interior.
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

        var ringLines = BuildCircleLines(center, currentRadius);
        var shape = new ProjectileShape(ringLines, [], []);

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
                    UpdateCircleLines(center, currentRadius, ringLines);
                    hazard.Shape.InvalidateBounds();
                }
            })
        {
            ExpireOnContact = false,
        };
    }

    private static ThickLine[] BuildCircleLines(Vector2 center, float radius)
    {
        var lines = new ThickLine[CircleSegments];
        UpdateCircleLines(center, radius, lines);
        return lines;
    }

    private static void UpdateCircleLines(Vector2 center, float radius, ThickLine[] destination)
    {
        const float bandHalfWidth = 2f;
        for (var i = 0; i < CircleSegments; i++)
        {
            var a1 = MathF.Tau * i / CircleSegments;
            var a2 = MathF.Tau * (i + 1) / CircleSegments;
            var p1 = new Vector2(center.X + radius * MathF.Cos(a1), center.Y + radius * MathF.Sin(a1));
            var p2 = new Vector2(center.X + radius * MathF.Cos(a2), center.Y + radius * MathF.Sin(a2));
            destination[i] = new ThickLine(p1, p2, bandHalfWidth);
        }
    }
}
