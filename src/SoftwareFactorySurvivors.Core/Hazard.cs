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
    public HazardKind Kind { get; }
    public ProjectileShape Shape { get; protected set; }
    public float AgeSeconds { get; protected set; }
    public float ArmSeconds { get; }
    public float LifetimeSeconds { get; }
    public bool ExpireOnContact { get; set; }

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
        _onUpdate = onUpdate;
    }

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
