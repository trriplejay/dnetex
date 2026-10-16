using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Boss ability that periodically emits bullet hazards in radial bursts.
/// Bullets travel outwards at a speed strictly slower than the ship's top speed.
/// Each burst leaves at least one generous angular gap wider than shipDiameter / maxRange
/// so that the player can safely slip through at any distance across the arena.
/// </summary>
public sealed class BulletBursts : IEnemyAbility, IHazardProducer
{
    /// <summary>Ship top speed reference from Ship.Speed (200 px/s).</summary>
    public const float ShipSpeed = Ship.Speed;

    /// <summary>Speed of bullet hazards (must be strictly slower than ship top speed).</summary>
    public const float BulletSpeed = 120f;

    /// <summary>Ship diameter (2 * 24 = 48 px based on local strokes extent).</summary>
    public const float ShipDiameter = 48f;

    /// <summary>Play field radius / max range (e.g. 1000 px).</summary>
    public const float MaxPlayFieldRadius = 1000f;

    /// <summary>Minimum required angular gap width in radians: shipDiameter / maxRange.</summary>
    public const float MinRequiredGapRadians = ShipDiameter / MaxPlayFieldRadius;

    private readonly float _secondsBetweenBursts;
    private readonly int _bulletsPerBurst;
    private readonly float _bulletRadius;
    private readonly float _bulletLifetime;
    private readonly float _bulletArmSeconds;
    private readonly Random? _injectedRandom;
    private readonly List<Hazard> _pendingHazards = [];
    private float _sinceBurst;

    /// <summary>
    /// Creates a BulletBursts ability.
    /// </summary>
    /// <param name="secondsBetweenBursts">Interval between bursts in seconds.</param>
    /// <param name="bulletsPerBurst">Number of bullets per burst.</param>
    /// <param name="bulletSpeed">Speed of bullets; must be strictly less than Ship.Speed.</param>
    /// <param name="bulletRadius">Radius of each bullet hazard.</param>
    /// <param name="bulletLifetime">Lifetime of each bullet hazard in seconds.</param>
    /// <param name="bulletArmSeconds">Arming warning time in seconds (Hazard clamps >= 1.0s).</param>
    /// <param name="random">Optional seeded Random for burst rotation and gaps.</param>
    public BulletBursts(
        float secondsBetweenBursts = 3.0f,
        int bulletsPerBurst = 12,
        float bulletSpeed = BulletSpeed,
        float bulletRadius = 8f,
        float bulletLifetime = 6.0f,
        float bulletArmSeconds = 1.0f,
        Random? random = null)
    {
        if (bulletSpeed >= ShipSpeed)
            throw new ArgumentOutOfRangeException(nameof(bulletSpeed), "Bullet speed must be strictly slower than ship top speed.");

        _secondsBetweenBursts = secondsBetweenBursts;
        _bulletsPerBurst = bulletsPerBurst;
        Speed = bulletSpeed;
        _bulletRadius = bulletRadius;
        _bulletLifetime = bulletLifetime;
        _bulletArmSeconds = bulletArmSeconds;
        _injectedRandom = random;
    }

    /// <summary>Current bullet speed.</summary>
    public float Speed { get; }

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        var rng = _injectedRandom ?? random;
        _sinceBurst += elapsedSeconds;

        while (_sinceBurst >= _secondsBetweenBursts)
        {
            _sinceBurst -= _secondsBetweenBursts;
            EmitBurst(owner.Position, rng);
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

    private void EmitBurst(Vector2 origin, Random rng)
    {
        // Angular arrangement: evenly distribute with a reserved contiguous gap arc.
        // The reserved gap arc is generous (e.g. at least 60 degrees = ~1.05 rad),
        // which easily exceeds ShipDiameter / MaxPlayFieldRadius (48 / 1000 = 0.048 rad).
        const float gapArc = MathF.PI / 3f; // 60 degrees
        var availableArc = MathF.Tau - gapArc;
        var baseAngle = rng.NextSingle() * MathF.Tau;

        var count = Math.Max(1, _bulletsPerBurst);
        var step = availableArc / count;

        for (var i = 0; i < count; i++)
        {
            var angle = baseAngle + i * step;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var speed = Speed;
            var pos = origin;

            var circleArray = new Circle[] { new Circle(pos, _bulletRadius) };
            var shape = new ProjectileShape([], [], circleArray);
            var bullet = new Hazard(
                HazardKind.Bullet,
                shape,
                lifetimeSeconds: _bulletLifetime,
                armSeconds: _bulletArmSeconds,
                onUpdate: (h, dt) =>
                {
                    pos += direction * speed * dt;
                    circleArray[0] = new Circle(pos, _bulletRadius);
                    h.Shape.InvalidateBounds();
                })
            {
                ExpireOnContact = true,
            };

            _pendingHazards.Add(bullet);
        }
    }
}
