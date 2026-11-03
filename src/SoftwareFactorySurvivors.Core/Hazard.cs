using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>The general category of a non-enemy hazard.</summary>
public enum HazardKind
{
    Trail,
    Ring,
    Bullet,
}

/// <summary>Allows an enemy movement pattern or ability to produce hazards.</summary>
public interface IHazardProducer
{
    IReadOnlyList<Hazard> TakeHazards();
}

/// <summary>
/// A non-enemy hazard on the play field (such as a trail, closing ring, or bullet).
/// Has a collision shape, age, lifetime, and an arming time clamped to at least 1.0 second
/// so the player always has warning before contact damage can occur.
/// </summary>
public class Hazard
{
    /// <summary>Base damage per contact tick of a trail segment, before time scaling.</summary>
    public const float TrailContactDamage = 4f;

    /// <summary>Base damage per contact tick of a closing-ring band, before time scaling.</summary>
    public const float RingContactDamage = 6f;

    /// <summary>Base damage of a bullet, dealt once before it is removed, before time scaling.</summary>
    public const float BulletContactDamage = 8f;

    public HazardKind Kind { get; }
    public ProjectileShape Shape { get; protected set; }
    public float AgeSeconds { get; protected set; }
    public float ArmSeconds { get; }
    public float LifetimeSeconds { get; }
    public bool ExpireOnContact { get; set; }

    /// <summary>Base damage per contact tick (see EnemyRoster.ScaledContactDamage for how it grows with the clock).</summary>
    public float ContactDamage { get; }

    /// <summary>True once a hazard that expires on contact has hit the ship; the session removes it.</summary>
    public bool IsSpent { get; private set; }

    public bool IsArmed => AgeSeconds >= ArmSeconds;
    public bool IsExpired => AgeSeconds >= LifetimeSeconds;

    private readonly Action<Hazard, float>? _onUpdate;

    public Hazard(
        HazardKind kind,
        ProjectileShape shape,
        float lifetimeSeconds,
        float armSeconds = 1.0f,
        Action<Hazard, float>? onUpdate = null)
    {
        Kind = kind;
        Shape = shape;
        LifetimeSeconds = lifetimeSeconds;
        ArmSeconds = MathF.Max(1.0f, armSeconds);
        ContactDamage = DefaultContactDamage(kind);
        _onUpdate = onUpdate;
    }

    /// <summary>The base contact damage of each hazard kind: trail 4, ring 6, bullet 8.</summary>
    public static float DefaultContactDamage(HazardKind kind) => kind switch
    {
        HazardKind.Trail => TrailContactDamage,
        HazardKind.Ring => RingContactDamage,
        HazardKind.Bullet => BulletContactDamage,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Hazard kind has no contact damage."),
    };

    /// <summary>Marks a hazard that expires on contact as used up, so the session removes it.</summary>
    internal void MarkSpent() => IsSpent = true;

    public virtual void Update(float elapsedSeconds)
    {
        AgeSeconds += elapsedSeconds;
        _onUpdate?.Invoke(this, elapsedSeconds);
    }

    public bool Touches(IReadOnlyList<Vector2[]> strokes) => Shape.Touches(strokes);

    /// <summary>Creates a trail hazard segment along a straight path.</summary>
    public static Hazard CreateTrail(
        Vector2 start,
        Vector2 end,
        float halfWidth = 25f,
        float lifetimeSeconds = 4.0f,
        float armSeconds = 1.0f)
    {
        var shape = new ProjectileShape([new ThickLine(start, end, halfWidth)], [], []);
        return new Hazard(HazardKind.Trail, shape, lifetimeSeconds, armSeconds) { ExpireOnContact = false };
    }

    /// <summary>Creates a ring hazard centered at a position.</summary>
    public static Hazard CreateRing(
        Vector2 center,
        float radius,
        float lifetimeSeconds = 5.0f,
        float armSeconds = 1.0f,
        Action<Hazard, float>? onUpdate = null)
    {
        var shape = new ProjectileShape([], [], [new Circle(center, radius)]);
        return new Hazard(HazardKind.Ring, shape, lifetimeSeconds, armSeconds, onUpdate) { ExpireOnContact = false };
    }

    /// <summary>Creates a bullet hazard at a position.</summary>
    public static Hazard CreateBullet(
        Vector2 position,
        float radius = 8f,
        float lifetimeSeconds = 4.0f,
        float armSeconds = 1.0f,
        Action<Hazard, float>? onUpdate = null)
    {
        var shape = new ProjectileShape([], [], [new Circle(position, radius)]);
        return new Hazard(HazardKind.Bullet, shape, lifetimeSeconds, armSeconds, onUpdate) { ExpireOnContact = true };
    }
}
